using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
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
     {"type":"function","function":{"name":"open_app","description":"apre un'app: calcolatrice, blocco note, esplora file, impostazioni, spotify","parameters":{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}}},
     {"type":"function","function":{"name":"search_web","description":"apre il browser con una ricerca google. usalo SOLO se l'utente chiede esplicitamente di aprire il browser","parameters":{"type":"object","properties":{"query":{"type":"string"}},"required":["query"]}}},
     {"type":"function","function":{"name":"web_search","description":"cerca su internet e restituisce i primi risultati. usalo per notizie, fatti recenti o cose che non sai","parameters":{"type":"object","properties":{"query":{"type":"string"}},"required":["query"]}}},
     {"type":"function","function":{"name":"pc_power","description":"blocca, sospende, spegne o riavvia il pc","parameters":{"type":"object","properties":{"action":{"type":"string","enum":["lock","sleep","shutdown","restart"]}},"required":["action"]}}}
    ]
    """;

    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    static void Key(byte vk) { keybd_event(vk, 0, 0, UIntPtr.Zero); keybd_event(vk, 0, 2, UIntPtr.Zero); }

    static AudioEndpointVolume Vol() =>
        new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).AudioEndpointVolume;

    public static void Open(string target, string? args = null) =>
        Process.Start(new ProcessStartInfo(target, args ?? "") { UseShellExecute = true });

    static readonly Dictionary<string, string> Apps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["calcolatrice"] = "calc.exe", ["calculator"] = "calc.exe",
        ["blocco note"] = "notepad.exe", ["notepad"] = "notepad.exe",
        ["esplora file"] = "explorer.exe", ["explorer"] = "explorer.exe",
        ["impostazioni"] = "ms-settings:", ["settings"] = "ms-settings:",
        ["spotify"] = "spotify:"
    };

    static readonly Dictionary<string, (string Cmd, string Args, string Desc)> Power = new()
    {
        ["lock"] = ("rundll32.exe", "user32.dll,LockWorkStation", Loc.L("bloccare il pc", "lock the PC")),
        ["sleep"] = ("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0", Loc.L("mettere il pc in sospensione", "put the PC to sleep")),
        ["shutdown"] = ("shutdown", "/s /t 30", Loc.L("spegnere il pc tra 30 secondi", "shut down the PC in 30 seconds")),
        ["restart"] = ("shutdown", "/r /t 30", Loc.L("riavviare il pc tra 30 secondi", "restart the PC in 30 seconds"))
    };

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
                if (!Apps.TryGetValue(n, out var target))
                    return new(Loc.L($"aprire «{n}»", $"open «{n}»"), Array.Empty<string>(), false,
                        () => Task.FromResult(Loc.L("app non in lista, posso aprire: ", "app not in the list, I can open: ") + string.Join(", ", Apps.Keys)));
                return new(Loc.L($"aprire «{n}»", $"open «{n}»"), new[] { Loc.L($"avvio «{target}»", $"I launch «{target}»") }, true,
                    Sync(() => { Open(target); return Loc.L($"aperto {n}", $"opened {n}"); }));
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
                if (!Power.TryGetValue(act, out var p)) return null;
                var steps = new List<string> { Loc.L($"eseguo: {p.Cmd} {p.Args}", $"I run: {p.Cmd} {p.Args}") };
                if (act is "shutdown" or "restart") steps.Add(Loc.L("puoi annullare entro 30 secondi con: shutdown /a", "you can cancel within 30 seconds with: shutdown /a"));
                return new(p.Desc, steps.ToArray(), true, Sync(() => { Open(p.Cmd, p.Args); return Loc.L("fatto", "done"); }));
            }
        }
        return null;
    }
}
