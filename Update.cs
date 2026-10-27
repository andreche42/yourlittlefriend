using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace YourLittleFriend;

// aggiornamenti: si guarda l'ultima release su github, si scarica l'installer, si controlla che sia integro
// e lo si fa partire in silenzio. poi l'app riparte da sola, con impostazioni e portafile intatti.
public static class Update
{
    public sealed record Info(string Version, string Page, string? AssetUrl, string? ShaUrl);

    const string Repo = "andreche42/yourlittlefriend";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };

    // la copia è stata installata con l'installer? solo allora può aggiornarsi da sola
    public static bool Installed =>
        File.Exists(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? "", "unins000.exe"));

    // l'ultima release, ma solo se è più nuova di questa. null = niente da fare (o offline, o nessuna release)
    public static async Task<Info?> Find()
    {
        try
        {
            if (await Json($"https://api.github.com/repos/{Repo}/releases/latest") is not { } root) return null;
            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            var page = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "";
            if (!IsNewer(tag, App.Version)) return null;

            string? asset = null, sha = null;
            if (root.TryGetProperty("assets", out var assets))
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var url = a.TryGetProperty("browser_download_url", out var b) ? b.GetString() ?? "" : "";
                    if (url == "") continue;
                    if (name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase)) sha = url;
                    else if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) asset = url;
                }
            return new Info(tag.TrimStart('v', 'V'), page, asset, sha);
        }
        catch { return null; }   // offline o github che non risponde: si riprova più tardi
    }

    // una chiamata all'api di github con le intestazioni giuste. null = non c'è, non risponde o è lenta
    static async Task<JsonElement?> Json(string url)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("YourLittleFriend");
            req.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var res = await Http.SendAsync(req, cts.Token);
            if (!res.IsSuccessStatusCode) return null;
            using var d = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            return d.RootElement.Clone();
        }
        catch { return null; }
    }

    // le note (descrizione) della release di una versione: quelle che l'app mostra dopo un aggiornamento.
    // stringa vuota = niente da mostrare (release senza note, tag con un altro nome, oppure offline)
    public static async Task<string> Notes(string version)
    {
        foreach (var tag in new[] { version, "v" + version })
        {
            var r = await Json($"https://api.github.com/repos/{Repo}/releases/tags/{tag}");
            if (r != null && Body(r.Value) is { } b && !string.IsNullOrWhiteSpace(b)) return b;
        }
        // nessun tag con quel nome (es. build fatta a mano): si guarda l'ultima release, se è proprio questa
        var latest = await Json($"https://api.github.com/repos/{Repo}/releases/latest");
        if (latest != null && latest.Value.TryGetProperty("tag_name", out var t) &&
            SameTag(t.GetString() ?? "", version) && Body(latest.Value) is { } lb && !string.IsNullOrWhiteSpace(lb))
            return lb;
        return "";
    }

    static string? Body(JsonElement release) =>
        release.TryGetProperty("body", out var b) ? b.GetString() : null;

    static bool SameTag(string tag, string version) =>
        string.Equals(tag.TrimStart('v', 'V'), version.TrimStart('v', 'V'), StringComparison.OrdinalIgnoreCase);

    // le note di una release hanno due lingue (di solito inglese, una riga di trattini, italiano):
    // si tiene solo quella scelta nelle impostazioni, così l'utente non se le vede tutte e due.
    public static string Pick(string body, bool english)
    {
        var text = body.Replace("\r\n", "\n").Replace('\r', '\n');
        var want = english ? "en" : "it";

        // marcatori scritti a mano: <!-- it --> ... <!-- /it -->
        var marked = Regex.Match(text, $@"<!--\s*{want}\s*-->(.*?)<!--\s*/{want}\s*-->", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (marked.Success) return Clean(marked.Groups[1].Value);

        // altrimenti si spezza in blocchi: righe di trattini, oppure titoli che nominano una lingua ("## Italiano")
        var blocks = new List<(string? Lang, List<string> Lines)> { (null, new()) };
        foreach (var line in text.Split('\n'))
        {
            var t = line.Trim();
            if (IsRule(t)) { blocks.Add((null, new())); continue; }
            if (HeadingLang(t) is { } lang) { blocks.Add((lang, new())); continue; }
            blocks[^1].Lines.Add(line);
        }
        var full = blocks.Where(b => b.Lines.Any(l => l.Trim().Length > 0)).ToList();
        if (full.Count == 0) return "";
        if (full.Count == 1) return Clean(string.Join("\n", full[0].Lines));   // una lingua sola: quella c'è
        var mine = full.Where(b => b.Lang == want).ToList();
        if (mine.Count > 0) return Clean(string.Join("\n", mine.SelectMany(b => b.Lines)));
        // nessun blocco dichiarato: si sceglie quello che sembra scritto nella lingua voluta
        return Clean(string.Join("\n", full.OrderByDescending(b => Score(string.Join("\n", b.Lines), english)).First().Lines));
    }

    // riga di divisione (---, ***, ___): separa le due lingue
    static bool IsRule(string t) =>
        t.Length >= 3 && (t.All(c => c == '-') || t.All(c => c == '*') || t.All(c => c == '_'));

    // un titolo che è solo il nome di una lingua non si mostra: serve solo a dividere
    static string? HeadingLang(string t)
    {
        if (!t.StartsWith('#')) return null;
        var k = Regex.Replace(t.TrimStart('#').Trim().ToLowerInvariant(), @"[^a-z ]", " ");
        k = Regex.Replace(k, @"\s+", " ").Trim();
        return k switch
        {
            "it" or "ita" or "italiano" or "italian" or "versione italiana" or "descrizione in italiano" or "italian description" => "it",
            "en" or "eng" or "inglese" or "english" or "versione inglese" or "descrizione in inglese" or "english description" => "en",
            _ => null
        };
    }

    // parole comuni delle due lingue: servono a capire in che lingua è scritto un blocco
    static readonly string[] ItWords =
        [" il ", " lo ", " la ", " le ", " gli ", " un ", " una ", " di ", " del ", " della ", " che ", " non ", " con ", " per ", " più ", " sono ", " come ", " quando ", " anche ", " solo ", " ora ", " si ", " è ", " ma ", " dal ", " nel "];
    static readonly string[] EnWords =
        [" the ", " and ", " with ", " when ", " of ", " to ", " is ", " are ", " it ", " its ", " this ", " that ", " while ", " not ", " no ", " also ", " only ", " now ", " back ", " from ", " on ", " as "];

    static int Score(string text, bool english)
    {
        var low = " " + Regex.Replace(text.ToLowerInvariant(), @"\s+", " ") + " ";
        int mine = (english ? EnWords : ItWords).Sum(w => Regex.Matches(low, w).Count);
        int theirs = (english ? ItWords : EnWords).Sum(w => Regex.Matches(low, w).Count);
        return mine - theirs;
    }

    static string Clean(string s) => s.Replace("\r\n", "\n").Trim('\n', ' ', '\t');

    // "1.2.0" è più nuova di "1.1.9"; una beta non è più nuova della stessa versione stabile
    public static bool IsNewer(string remote, string local)
    {
        var a = Parts(remote);
        var b = Parts(local);
        for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            int x = i < a.Length ? a[i] : 0, y = i < b.Length ? b[i] : 0;
            if (x != y) return x > y;
        }
        return !remote.Contains('-') && local.Contains('-');
    }

    static int[] Parts(string v)
    {
        var head = v.Trim().TrimStart('v', 'V').Split('-', '+')[0];
        return head.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => int.TryParse(new string(p.TakeWhile(char.IsDigit).ToArray()), out var n) ? n : 0)
            .ToArray();
    }

    // scarica l'installer (riportando l'avanzamento) e controlla l'impronta del file
    public static async Task<string> Download(Info info, Action<double, string?> report)
    {
        if (info.AssetUrl == null) throw new Exception(Loc.L("la release non ha l'installer", "this release has no installer"));
        if (info.ShaUrl == null) throw new Exception(Loc.L("la release non ha il controllo di integrità (checksum)", "this release has no integrity check (checksum)"));

        var dir = Path.Combine(Path.GetTempPath(), "yourlittlefriend-update");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, Path.GetFileName(new Uri(info.AssetUrl).AbsolutePath));

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using (var res = await Http.GetAsync(info.AssetUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            res.EnsureSuccessStatusCode();
            long total = res.Content.Headers.ContentLength ?? 0, got = 0;
            await using var src = await res.Content.ReadAsStreamAsync();
            await using var dst = File.Create(file);
            var buf = new byte[81920];
            int n;
            while ((n = await src.ReadAsync(buf)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n));
                hash.AppendData(buf, 0, n);
                got += n;
                report(total > 0 ? (double)got / total : -1, null);   // -1 = lunghezza sconosciuta
            }
        }

        var expected = await Sha(info.ShaUrl);
        var actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (expected != actual)
        {
            try { File.Delete(file); } catch { }
            throw new Exception(Loc.L("il file scaricato è danneggiato (controllo di integrità fallito)", "the downloaded file is damaged (integrity check failed)"));
        }
        return file;
    }

    // il file .sha256 contiene l'impronta (sha256sum ci scrive anche il nome del file: va bene lo stesso)
    static async Task<string> Sha(string url)
    {
        var text = await Http.GetStringAsync(url);
        var m = Regex.Match(text, "[0-9a-fA-F]{64}");
        if (!m.Success) throw new Exception(Loc.L("checksum illeggibile", "unreadable checksum"));
        return m.Value.ToLowerInvariant();
    }

    // lancia l'installer in silenzio e poi riapre l'app: un .cmd aspetta la fine dell'installazione
    // (dopo il primo aggiornamento l'exe si chiama YourLittleFriend.exe: si guarda quale c'è)
    public static void Launch(string installer)
    {
        var dir = Path.GetDirectoryName(Environment.ProcessPath) ?? "";
        var renamed = Path.Combine(dir, "YourLittleFriend.exe");
        var old = Environment.ProcessPath ?? "";
        var bat = Path.Combine(Path.GetDirectoryName(installer)!, "update.cmd");
        var cmd = new System.Text.StringBuilder();
        cmd.AppendLine("@echo off");
        cmd.AppendLine($"\"{installer}\" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOCANCEL /SP-");
        cmd.AppendLine($"if exist \"{renamed}\" ( start \"\" \"{renamed}\" ) else ( start \"\" \"{old}\" )");
        File.WriteAllText(bat, cmd.ToString());
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c call \"{bat}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    // fino alla 1.1.3 l'exe si chiamava tutto minuscolo: dopo l'aggiornamento ne resta una copia vecchia accanto al nuovo.
    // si toglie da qui (e non dall'installer) così il nuovo exe c'è già, e non si rischia di restare senza niente
    public static void CleanOldExe()
    {
        try
        {
            var dir = Path.GetDirectoryName(Environment.ProcessPath);
            if (dir == null || Path.GetFileName(Environment.ProcessPath ?? "") != "YourLittleFriend.exe") return;
            var old = Path.Combine(dir, "yourlittlefriend.exe");
            if (!File.Exists(old)) return;
            // su windows i nomi non distinguono le maiuscole: si guarda il nome vero del file sul disco
            bool real = Directory.GetFiles(dir, "*.exe").Any(f => Path.GetFileName(f) == "yourlittlefriend.exe");
            if (real) File.Delete(old);
        }
        catch { }
    }

    public static void OpenPage(Info info)
    {
        if (info.Page == "") return;
        try { Process.Start(new ProcessStartInfo(info.Page) { UseShellExecute = true }); } catch { }
    }
}
