using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Microsoft.Win32;
using NAudio.CoreAudioApi;

namespace YourLittleFriend;

public record Plan(string Title, string[] Steps, bool Sensitive, Func<Task<string>> Run);

// azioni sul pc che il modello puo' chiamare. niente comandi liberi: solo queste, con argomenti controllati.
public static class Actions
{
    public const string AppName = "YourLittleFriend";

    public const string ToolsJson = """
    [
     {"type":"function","function":{"name":"set_volume","description":"imposta il volume del pc da 0 a 100","parameters":{"type":"object","properties":{"level":{"type":"integer"}},"required":["level"]}}},
     {"type":"function","function":{"name":"change_volume","description":"alza (delta positivo) o abbassa (delta negativo) il volume","parameters":{"type":"object","properties":{"delta":{"type":"integer"}},"required":["delta"]}}},
     {"type":"function","function":{"name":"toggle_mute","description":"muta o riattiva l'audio","parameters":{"type":"object","properties":{}}}},
     {"type":"function","function":{"name":"media_control","description":"play/pausa, brano successivo o precedente","parameters":{"type":"object","properties":{"action":{"type":"string","enum":["play_pause","next","previous"]}},"required":["action"]}}},
     {"type":"function","function":{"name":"play_on_spotify","description":"cerca una canzone su spotify e la mette in riproduzione","parameters":{"type":"object","properties":{"query":{"type":"string","description":"titolo e artista"}},"required":["query"]}}},
     {"type":"function","function":{"name":"open_app","description":"apre un'app installata sul pc, anche una che non conosci: basta il nome che ha nel menu start (es. calcolatrice, blocco note, esplora file, impostazioni, spotify, steam, visual studio code, whatsapp)","parameters":{"type":"object","properties":{"name":{"type":"string","description":"nome dell'app come compare nel menu start"}},"required":["name"]}}},
     {"type":"function","function":{"name":"search_web","description":"apre il browser con una ricerca google. usalo SOLO se l'utente chiede esplicitamente di aprire il browser","parameters":{"type":"object","properties":{"query":{"type":"string"}},"required":["query"]}}},
     {"type":"function","function":{"name":"web_search","description":"cerca su internet e restituisce i primi risultati. usalo per notizie, fatti recenti o cose che non sai","parameters":{"type":"object","properties":{"query":{"type":"string"}},"required":["query"]}}},
     {"type":"function","function":{"name":"pc_power","description":"blocca, sospende, iberna, spegne o riavvia il pc. sospendere e ibernare sono cose diverse: usa sleep per la sospensione, hibernate per l'ibernazione","parameters":{"type":"object","properties":{"action":{"type":"string","enum":["lock","sleep","hibernate","shutdown","restart"]}},"required":["action"]}}}
    ]
    """;

    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    static void Key(byte vk) { keybd_event(vk, 0, 0, UIntPtr.Zero); keybd_event(vk, 0, 2, UIntPtr.Zero); }

    static AudioEndpointVolume Vol() =>
        new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).AudioEndpointVolume;

    public static void Open(string target, string? args = null) =>
        Process.Start(new ProcessStartInfo(target, args ?? "") { UseShellExecute = true });

    // le app che si aprono al volo, senza cercare: le altre si trovano nel menu start
    static readonly Dictionary<string, string> Apps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["calcolatrice"] = "calc.exe", ["calculator"] = "calc.exe",
        ["blocco note"] = "notepad.exe", ["notepad"] = "notepad.exe",
        ["esplora file"] = "explorer.exe", ["explorer"] = "explorer.exe",
        ["impostazioni"] = "ms-settings:", ["settings"] = "ms-settings:",
        ["spotify"] = "spotify:"
    };

    // dove stanno i collegamenti di tutte le app installate (anche quelle dello store, quando ne hanno uno)
    static IEnumerable<string> StartMenus()
    {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
    }

    // cerca un'app per nome: prima le note, poi i collegamenti del menu start, poi il registro di windows.
    // se non la trova torna null e si lascia provare a windows (percorso, comando nel path, protocollo)
    static string? FindApp(string name)
    {
        if (Apps.TryGetValue(name, out var known)) return known;
        var key = name.Trim();
        if (key.Length == 0) return null;

        // 1. collegamenti del menu start: prima il nome identico, poi uno che lo contiene
        string? partial = null;
        foreach (var root in StartMenus())
        {
            IEnumerable<string> files;
            try { if (!Directory.Exists(root)) continue; files = Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories); }
            catch { continue; }
            foreach (var f in files)
            {
                var n = Path.GetFileNameWithoutExtension(f);
                if (string.Equals(n, key, StringComparison.OrdinalIgnoreCase)) return f;
                if (partial == null && n.Contains(key, StringComparison.OrdinalIgnoreCase)) partial = f;
            }
        }
        if (partial != null) return partial;

        // 2. registro: le app registrate come eseguibile (chrome, msedge, code...)
        var exe = key.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? key : key + ".exe";
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var k = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exe);
                if (k?.GetValue(null) is string path && path.Trim().Length > 0) return path.Trim().Trim('"');
            }
            catch { }
        }
        return null;
    }

    // sospensione e ibernazione: chiamata diretta alla funzione di windows. con rundll32 non si può, perché
    // passa una stringa al posto dei tre parametri booleani: il flag "iberna" risultava sempre acceso e il pc
    // finiva in ibernazione anche quando chiedevi la sospensione.
    [DllImport("powrprof.dll", SetLastError = true)]
    static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    static void Suspend(bool hibernate)
    {
        if (SetSuspendState(hibernate, false, false)) return;
        // se windows rifiuta la chiamata (permessi o criteri di gruppo) si passa dai comandi di sistema
        if (hibernate) Open("shutdown", "/h");
        else Open("rundll32.exe", "powrprof.dll,SetSuspendState Sleep");
    }

    static Func<Task<string>> Sync(Func<string> f) => () => Task.FromResult(f());

    // trasforma la chiamata del modello in un piano: titolo, passaggi (mostrati nel popup) e azione
    public static Plan? Make(string name, JsonNode? a)
    {
        string S(string k) => a?[k]?.ToString() ?? "";
        int I(string k) => int.TryParse(a?[k]?.ToString(), out var v) ? v : 0;
        switch (name)
        {
            case "set_volume":
            {
                int l = Math.Clamp(I("level"), 0, 100);
                return new(Loc.L($"mettere il volume al {l}%", $"set the volume to {l}%"),
                    new[] { Loc.L($"imposto il volume di sistema al {l}%", $"I set the system volume to {l}%") }, false,
                    Sync(() => { Vol().MasterVolumeLevelScalar = l / 100f; return Loc.L($"volume al {l}%", $"volume at {l}%"); }));
            }
            case "change_volume":
            {
                int d = Math.Clamp(I("delta"), -100, 100);
                return new(Loc.L($"cambiare il volume di {d:+#;-#}%", $"change the volume by {d:+#;-#}%"),
                    new[] { Loc.L($"sposto il volume di {d:+#;-#}%", $"I move the volume by {d:+#;-#}%") }, false,
                    Sync(() =>
                    {
                        var v = Vol();
                        v.MasterVolumeLevelScalar = Math.Clamp(v.MasterVolumeLevelScalar + d / 100f, 0f, 1f);
                        var now = Math.Round(v.MasterVolumeLevelScalar * 100);
                        return Loc.L($"volume al {now}%", $"volume at {now}%");
                    }));
            }
            case "toggle_mute":
                return new(Loc.L("mutare o riattivare l'audio", "mute or unmute the audio"), new[] { Loc.L("inverto lo stato muto", "I toggle mute") }, false,
                    Sync(() => { var v = Vol(); v.Mute = !v.Mute; return v.Mute ? Loc.L("audio mutato", "audio muted") : Loc.L("audio riattivato", "audio unmuted"); }));
            case "media_control":
            {
                var act = S("action");
                byte vk = act switch { "next" => 0xB0, "previous" => 0xB1, _ => 0xB3 };
                return new(Loc.L("controllare la musica", "control the music"), new[] { Loc.L($"premo il tasto multimediale «{act}»", $"I press the media key «{act}»") }, false,
                    Sync(() => { Key(vk); return Loc.L("fatto", "done"); }));
            }
            case "play_on_spotify":
            {
                var q = S("query");
                return new(Loc.L($"cercare «{q}» su spotify", $"search «{q}» on spotify"),
                    new[] { Loc.L("cerco il brano su spotify", "I search the track on spotify"), Loc.L("lo metto in riproduzione", "I start playing it") }, false,
                    () => Spotify.Play(q));
            }
            case "open_app":
            {
                var n = S("name").Trim();
                if (n.Length == 0) return null;
                var target = FindApp(n);   // null = non trovata: ci prova windows col nome così com'è
                var step = target == null
                    ? Loc.L($"provo ad aprirla come la conosce windows", $"I try to open it the way windows knows it")
                    : Loc.L($"avvio «{Path.GetFileName(target)}» ({target})", $"I launch «{Path.GetFileName(target)}» ({target})");
                return new(Loc.L($"aprire «{n}»", $"open «{n}»"), new[] { step }, true,
                    Sync(() =>
                    {
                        try { Open(target ?? n); return Loc.L($"aperto {n}", $"opened {n}"); }
                        catch (Exception ex)
                        {
                            return Loc.L($"non riesco ad aprire «{n}»: controlla il nome nel menu start. {ex.Message}",
                                          $"I can't open «{n}»: check its name in the start menu. {ex.Message}");
                        }
                    }));
            }
            case "web_search":
            {
                var q = S("query");
                return new(Loc.L($"cerco «{q}» sul web", $"searching «{q}» on the web"), Array.Empty<string>(), false,
                    () => Settings.Current.WebSearch
                        ? Web.Search(q)
                        : Task.FromResult(Loc.L("la ricerca sul web è disattivata nelle impostazioni", "web search is turned off in the settings")));
            }
            case "search_web":
            {
                var q = S("query");
                var url = "https://www.google.com/search?q=" + Uri.EscapeDataString(q);
                return new(Loc.L($"cercare «{q}» su google", $"search «{q}» on google"), new[] { Loc.L("apro il browser predefinito", "I open the default browser"), url }, true,
                    Sync(() => { Open(url); return Loc.L("ricerca aperta", "search opened"); }));
            }
            case "pc_power":
            {
                var act = S("action");
                switch (act)
                {
                    case "lock":
                        return new(Loc.L("bloccare il pc", "lock the PC"),
                            new[] { Loc.L("blocco la sessione", "I lock the session") }, true,
                            Sync(() => { Open("rundll32.exe", "user32.dll,LockWorkStation"); return Loc.L("fatto", "done"); }));

                    case "sleep":
                    case "hibernate":
                    {
                        bool hib = act == "hibernate";
                        var title = hib ? Loc.L("ibernare il pc", "hibernate the PC") : Loc.L("mettere il pc in sospensione", "put the PC to sleep");
                        var step = hib
                            ? Loc.L("iberno: il pc salva tutto su disco e si spegne, poi riprende dallo stesso punto", "I hibernate: the PC saves everything to disk and turns off, then resumes where it left off")
                            : Loc.L("sospendo: il pc va in pausa e riprende in un attimo, il lavoro resta in memoria", "I suspend: the PC pauses and wakes in a moment, the work stays in memory");
                        var done = hib ? Loc.L("ibernazione avviata", "hibernation started") : Loc.L("sospensione avviata", "sleep started");
                        return new(title, new[] { step }, true, Sync(() => { Suspend(hib); return done; }));
                    }

                    case "shutdown":
                    case "restart":
                    {
                        bool off = act == "shutdown";
                        var args = off ? "/s /t 30" : "/r /t 30";
                        var desc = off ? Loc.L("spegnere il pc tra 30 secondi", "shut down the PC in 30 seconds")
                                       : Loc.L("riavviare il pc tra 30 secondi", "restart the PC in 30 seconds");
                        return new(desc, new[]
                        {
                            Loc.L($"eseguo: shutdown {args}", $"I run: shutdown {args}"),
                            Loc.L("puoi annullare entro 30 secondi con: shutdown /a", "you can cancel within 30 seconds with: shutdown /a")
                        }, true, Sync(() => { Open("shutdown", args); return Loc.L("fatto", "done"); }));
                    }
                }
                return null;
            }
        }
        return null;
    }
}
