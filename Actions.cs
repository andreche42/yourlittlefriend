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
     {"type":"function","function":{"name":"pc_power","description":"blocca, sospende, iberna, spegne o riavvia il pc. sospendere e ibernare sono cose diverse: usa sleep per la sospensione, hibernate per l'ibernazione","parameters":{"type":"object","properties":{"action":{"type":"string","enum":["lock","sleep","hibernate","shutdown","restart"]}},"required":["action"]}}},
     {"type":"function","function":{"name":"remind","description":"crea un promemoria per QUALSIASI cosa: una telefonata, una medicina, una mail, la lavatrice, buttare la spazzatura, una partita, una scadenza. metti in what le parole dell'utente, ora compresa (es. \"chiamare mamma alle 18\"): l'ora la legge anche l'app dal testo, gli altri parametri sono facoltativi","parameters":{"type":"object","properties":{"what":{"type":"string","description":"cosa deve ricordare, con le parole dell'utente, ora compresa"},"time":{"type":"string","description":"facoltativo: l'ora in formato HH:mm"},"minutes":{"type":"integer","description":"facoltativo: fra quanti minuti"}},"required":["what"]}}},
     {"type":"function","function":{"name":"screenshot","description":"fa uno screenshot dello schermo e lo mette nel portafile","parameters":{"type":"object","properties":{}}}},
     {"type":"function","function":{"name":"open_path","description":"apre una cartella o un file: va bene anche solo il nome (per esempio 'download', 'documenti', 'la bolletta'). se non è un percorso lo cerca fra i file recenti","parameters":{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}}},
     {"type":"function","function":{"name":"system_switch","description":"wifi (stato, accendi, spegni), bluetooth e modalità aereo","parameters":{"type":"object","properties":{"action":{"type":"string","enum":["wifi_status","wifi_on","wifi_off","bluetooth","airplane"]}},"required":["action"]}}}
    ]
    """;

    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    static void Key(byte vk) { keybd_event(vk, 0, 0, UIntPtr.Zero); keybd_event(vk, 0, 2, UIntPtr.Zero); }

    // dove mettere uno screenshot: lo decide la finestra, che ha il portafile
    public static Action<IEnumerable<string>>? ToHolder;

    // esegue un comando e restituisce quello che dice (vuoto = è andata bene). solo per i comandi di windows, mai roba scritta dal modello
    static string Run(string exe, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            });
            if (p == null) return "non riesco a eseguirlo";
            var o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit(4000);
            return o.Trim();
        }
        catch (Exception ex) { return ex.Message; }
    }

    // "18:30" oppure "18" (24 ore). TimeSpan da solo leggerebbe "18" come 18 giorni, quindi si controlla a mano
    static bool TryClock(string s, out TimeSpan clock)
    {
        clock = default;
        s = s.Trim().Replace('.', ':');
        if (s.Length == 0) return false;
        if (s.Contains(':') && TimeSpan.TryParse(s, out var t) && t >= TimeSpan.Zero && t < TimeSpan.FromHours(24)) { clock = t; return true; }
        if (int.TryParse(s, out var h) && h is >= 0 and < 24) { clock = TimeSpan.FromHours(h); return true; }
        return false;
    }

    // il nome della scheda wifi, per accenderla o spegnerla con netsh
    static string? WifiName()
    {
        foreach (var line in Run("netsh", "wlan show interfaces").Split('\n'))
        {
            var t = line.Trim();
            if (!t.StartsWith("Nome", StringComparison.OrdinalIgnoreCase) && !t.StartsWith("Name", StringComparison.OrdinalIgnoreCase)) continue;
            var i = t.IndexOf(':');
            if (i > 0) return t[(i + 1)..].Trim();
        }
        return null;
    }

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
                var target = FindApp(n);
                // non trovata: non parte niente. il nome scritto dal modello non arriva a windows così com'è,
                // altrimenti basterebbe una frase qualsiasi per lanciare un comando o un percorso
                if (target == null)
                    return new(Loc.L($"aprire «{n}»", $"open «{n}»"), new[] { Loc.L("cerco l'app sul pc", $"I look for the app on the PC") }, false,
                        Sync(() => Loc.L($"non trovo «{n}»: dimmi il nome che ha nel menu start", $"I can't find «{n}»: tell me its name in the start menu")));
                // nel popup c'è il testo esatto che parte, non il nome che ha detto il modello
                return new(Loc.L($"aprire «{n}»", $"open «{n}»"),
                    new[] { Loc.L($"avvio: {target}", $"I launch: {target}") }, true,
                    Sync(() =>
                    {
                        try { Open(target); return Loc.L($"aperto {n}", $"opened {n}"); }
                        catch (Exception ex)
                        {
                            return Loc.L($"non riesco ad aprire «{n}»: {ex.Message}", $"I can't open «{n}»: {ex.Message}");
                        }
                    }));
            }
            case "remind":
            {
                var what = S("what").Trim();
                if (what.Length == 0) return null;
                if (!Settings.Current.RemindOn)
                    return new(Loc.L("creare un promemoria", "set a reminder"),
                        new[] { Loc.L("i promemoria sono spenti nelle impostazioni", "reminders are turned off in the settings") }, false,
                        Sync(() => Loc.L("i promemoria sono spenti nelle impostazioni", "reminders are turned off in the settings")));

                // l'ora arriva come "18:30" oppure come "fra 20 minuti": si accetta tutto
                DateTime? at = null;
                if (TryClock(S("time"), out var clock))
                {
                    at = DateTime.Today + clock;
                    if (at <= DateTime.Now) at = at.Value.AddDays(1);   // quell'ora oggi è già passata: vale domani
                }
                else if (S("minutes").Length > 0)
                    at = DateTime.Now.AddMinutes(Math.Clamp(I("minutes"), 1, 60 * 24 * 30));
                else if (Reminders.ParseWhen(what, out var parsed))
                    at = parsed;   // l'ora è scritta dentro il testo: non serve che il modello la calcoli

                if (at == null)
                    return new(Loc.L("creare un promemoria", "set a reminder"),
                        new[] { Loc.L("manca l'ora", "the time is missing") }, false,
                        Sync(() => Loc.L("a che ora te lo ricordo?", "what time should I remind you?")));

                var when = at.Value;
                return new(Loc.L($"ricordarti «{what}» {Reminders.When(when)}", $"remind you to «{what}» {Reminders.When(when)}"),
                    new[] { Loc.L($"metto il promemoria per le {when:HH:mm}", $"I set the reminder for {when:HH:mm}") }, false,
                    Sync(() =>
                    {
                        Reminders.Add(what, when);
                        return Loc.L($"ok, te lo ricordo {Reminders.When(when)}", $"ok, I'll remind you {Reminders.When(when)}");
                    }));
            }
            case "screenshot":
                return new(Loc.L("fare uno screenshot", "take a screenshot"),
                    new[] { Loc.L("fotografo tutti gli schermi", "I capture all the screens"), Loc.L("lo metto nel portafile", "I put it in the file holder") }, false,
                    Sync(() =>
                    {
                        var path = Screen.Shoot();
                        if (path == null) return Loc.L("non riesco a fare lo screenshot", "I can't take the screenshot");
                        ToHolder?.Invoke(new[] { path });
                        return Loc.L("screenshot nel portafile", "screenshot in the file holder");
                    }));
            case "open_path":
            {
                var n = S("name").Trim();
                if (n.Length == 0) return null;
                var path = Recent.Resolve(n);
                if (path == null)
                    return new(Loc.L($"aprire «{n}»", $"open «{n}»"), new[] { Loc.L("cerco la cartella o il file", "I look for the folder or file") }, false,
                        Sync(() => Loc.L($"non trovo «{n}»: dimmi il percorso o il nome del file", $"I can't find «{n}»: tell me the path or the file name")));
                return new(Loc.L($"aprire «{n}»", $"open «{n}»"), new[] { Loc.L($"apro: {path}", $"I open: {path}") }, true,
                    Sync(() =>
                    {
                        try { Open(path); return Loc.L($"aperto {Path.GetFileNameWithoutExtension(path)}", $"opened {Path.GetFileNameWithoutExtension(path)}"); }
                        catch (Exception ex) { return Loc.L($"non riesco ad aprirlo: {ex.Message}", $"I can't open it: {ex.Message}"); }
                    }));
            }
            case "system_switch":
            {
                var act = S("action");
                switch (act)
                {
                    case "wifi_status":
                        return new(Loc.L("vedere com'è il wifi", "check the wifi"), Array.Empty<string>(), false,
                            Sync(() =>
                            {
                                var o = Run("netsh", "wlan show interfaces");
                                return o.Length == 0 ? Loc.L("non vedo nessuna scheda wifi", "I can't see any wifi adapter") : o;
                            }));

                    case "wifi_on":
                    case "wifi_off":
                    {
                        bool on = act == "wifi_on";
                        return new(Loc.L(on ? "accendere il wifi" : "spegnere il wifi", on ? "turn wifi on" : "turn wifi off"),
                            new[] { Loc.L(on ? "riaccendo la scheda wifi" : "spengo la scheda wifi", on ? "I turn the wifi adapter on" : "I turn the wifi adapter off") }, true,
                            Sync(() =>
                            {
                                // la scheda si comanda da amministratore: se windows dice no, si apre la pagina giusta delle impostazioni
                                var iface = WifiName();
                                if (iface != null)
                                {
                                    var o = Run("netsh", $"interface set interface name=\"{iface}\" admin={(on ? "enabled" : "disabled")}");
                                    if (o.Length == 0) return on ? Loc.L("wifi acceso", "wifi on") : Loc.L("wifi spento", "wifi off");
                                }
                                Open("ms-settings:network-wifi");
                                return Loc.L("windows non mi lascia accendere o spegnere la scheda: ti ho aperto le impostazioni del wifi", "windows won't let me switch the adapter: I opened the wifi settings for you");
                            }));
                    }

                    case "bluetooth":
                        return new(Loc.L("aprire il bluetooth", "open bluetooth"),
                            new[] { Loc.L("apro le impostazioni del bluetooth", "I open the bluetooth settings") }, true,
                            Sync(() => { Open("ms-settings:bluetooth"); return Loc.L("impostazioni del bluetooth aperte", "bluetooth settings opened"); }));

                    case "airplane":
                        return new(Loc.L("aprire la modalità aereo", "open airplane mode"),
                            new[] { Loc.L("apro le impostazioni della modalità aereo", "I open the airplane mode settings") }, true,
                            Sync(() => { Open("ms-settings:network-airplanemode"); return Loc.L("modalità aereo aperta", "airplane mode opened"); }));
                }
                return null;
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
