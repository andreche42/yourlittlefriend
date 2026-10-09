using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace YourLittleFriend;

// avvio automatico con windows (chiave Run dell'utente, la stessa che scrive l'installer)
public static class Startup
{
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run", ValueName = "yourlittlefriend";

    public static bool Enabled
    {
        get { try { using var k = Registry.CurrentUser.OpenSubKey(Key); return k?.GetValue(ValueName) != null; } catch { return false; } }
    }

    public static void Set(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(Key);
            if (on && Environment.ProcessPath != null) k.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
            else k.DeleteValue(ValueName, false);
        }
        catch { }
    }
}

// cerca una città con l'api di geocoding di open-meteo
public static class Geo
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static async Task<(string Name, double Lat, double Lon)?> Find(string city)
    {
        try
        {
            var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1&language={(Loc.En ? "en" : "it")}";
            using var d = JsonDocument.Parse(await Http.GetStringAsync(url));
            if (!d.RootElement.TryGetProperty("results", out var r) || r.GetArrayLength() == 0) return null;
            var f = r[0];
            return (f.GetProperty("name").GetString() ?? city, f.GetProperty("latitude").GetDouble(), f.GetProperty("longitude").GetDouble());
        }
        catch { return null; }
    }
}

public enum Fit { Unknown, Good, Tight, Bad, NoDisk }

// installazione guidata di ollama e dei modelli. report(testo, avanzamento 0..1, oppure -1 se non misurabile)
public static class Ollama
{
    const string Api = "http://localhost:11434";
    static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public static async Task<bool> IsUp()
    {
        try
        {
            using var cts = new CancellationTokenSource(2000);
            using var r = await Http.GetAsync(Api + "/api/version", cts.Token);
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public static string? FindExe()
    {
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe");
        if (File.Exists(local)) return local;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            try { var p = Path.Combine(dir.Trim(), "ollama.exe"); if (File.Exists(p)) return p; } catch { }
        return null;
    }

    // controlla se il modello sta nel pc (ram e spazio su disco). la stima è a occhio: ~0.62 gb per miliardo di parametri (quantizzato a 4 bit)
    public static (Fit Fit, string Msg) Check(string model)
    {
        var m = Regex.Match(model.ToLowerInvariant(), @"(\d+(?:\.\d+)?)b");
        if (!m.Success)
            return (Fit.Unknown, Loc.L("Non riesco a stimare le dimensioni di questo modello: controlla di avere RAM a sufficienza.",
                                       "I can't estimate this model's size: make sure you have enough RAM."));
        double size = double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) * 0.62 + 0.3;
        double need = size * 1.2 + 1;
        double ram = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1e9;
        double disk = double.MaxValue;
        try { disk = new DriveInfo(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))!).AvailableFreeSpace / 1e9; } catch { }

        if (disk < size + 1)
            return (Fit.NoDisk, Loc.L($"✗ Spazio su disco insufficiente: servono circa {size:0.#} GB, ne hai {disk:0.#} GB liberi.",
                                      $"✗ Not enough disk space: about {size:0.#} GB needed, {disk:0.#} GB free."));
        if (ram >= need + 4)
            return (Fit.Good, Loc.L($"✓ Va bene per il tuo PC ({ram:0} GB di RAM, il modello pesa circa {size:0.#} GB).",
                                    $"✓ Good for your PC ({ram:0} GB of RAM, the model is about {size:0.#} GB)."));
        if (ram >= need + 1)
            return (Fit.Tight, Loc.L($"⚠ Ci sta, ma potrebbe essere lento: hai {ram:0} GB di RAM e servono circa {need:0.#} GB. Meglio un modello più piccolo.",
                                     $"⚠ It fits, but may be slow: you have {ram:0} GB of RAM and need about {need:0.#} GB. A smaller model would be better."));
        return (Fit.Bad, Loc.L($"✗ Troppo pesante: hai {ram:0} GB di RAM e ne servirebbero almeno {need:0.#}. Scegli un modello più piccolo.",
                               $"✗ Too heavy: you have {ram:0} GB of RAM and would need at least {need:0.#}. Pick a smaller model."));
    }

    public static bool IsInstalled => FindExe() != null;

    // se ollama è installato ma spento, avvia il server in nascosto (senza finestre). true se alla fine risponde
    public static async Task<bool> StartIfInstalled(CancellationToken ct = default)
    {
        if (await IsUp()) return true;
        var exe = FindExe();
        if (exe == null) return false;
        try { Process.Start(new ProcessStartInfo(exe, "serve") { UseShellExecute = false, CreateNoWindow = true }); } catch { return false; }
        for (int i = 0; i < 20 && !await IsUp(); i++) await Task.Delay(1000, ct);
        return await IsUp();
    }

    // modelli già scaricati (nome e dimensione in byte)
    public static async Task<List<(string Name, long Size)>> ListModels()
    {
        var list = new List<(string, long)>();
        using var cts = new CancellationTokenSource(5000);
        using var d = JsonDocument.Parse(await Http.GetStringAsync(Api + "/api/tags", cts.Token));
        if (d.RootElement.TryGetProperty("models", out var ms))
            foreach (var m in ms.EnumerateArray())
                list.Add((m.GetProperty("name").GetString() ?? "", m.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0));
        list.RemoveAll(m => m.Item1 == "");
        return list;
    }

    // modelli caricati in memoria in questo momento (nome, dimensione, quanta sta nella scheda video)
    public static async Task<List<(string Name, long Size, long Vram)>> Running()
    {
        var list = new List<(string, long, long)>();
        using var cts = new CancellationTokenSource(4000);
        using var d = JsonDocument.Parse(await Http.GetStringAsync(Api + "/api/ps", cts.Token));
        if (d.RootElement.TryGetProperty("models", out var ms))
            foreach (var m in ms.EnumerateArray())
                list.Add((m.GetProperty("name").GetString() ?? "",
                          m.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0,
                          m.TryGetProperty("size_vram", out var vr) ? vr.GetInt64() : 0));
        list.RemoveAll(m => m.Item1 == "");
        return list;
    }

    static async Task Generate(string model, JsonNode keepAlive, CancellationToken ct)
    {
        var body = new JsonObject { ["model"] = model, ["keep_alive"] = keepAlive };
        using var res = await Http.PostAsync(Api + "/api/generate", new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"), ct);
        if (!res.IsSuccessStatusCode) throw new Exception(await res.Content.ReadAsStringAsync(ct));
    }

    // carica il modello in memoria (senza fare domande), così la prima risposta non aspetta. resta caricato 30 minuti
    public static Task Load(string model, CancellationToken ct = default) => Generate(model, JsonValue.Create("30m")!, ct);

    // toglie il modello dalla memoria
    public static Task Unload(string model, CancellationToken ct = default) => Generate(model, JsonValue.Create(0), ct);

    // ferma ollama: scarica i modelli e chiude il server (e la sua icona nella barra, se c'è)
    public static async Task Stop()
    {
        try { foreach (var m in await Running()) await Unload(m.Name); } catch { }
        foreach (var name in new[] { "ollama app", "ollama", "ollama_llama_server" })
            foreach (var pr in Process.GetProcessesByName(name))
            {
                try { pr.Kill(true); } catch { }
                finally { pr.Dispose(); }
            }
        for (int i = 0; i < 10 && await IsUp(); i++) await Task.Delay(500);
    }

    // l'installer di ollama apre la sua app (finestra + icona nella barra): chiudiamo la finestra per non confondere l'utente.
    // il server resta attivo, oppure lo riavviamo noi in nascosto con "ollama serve"
    static void CloseOllamaWindows()
    {
        foreach (var name in new[] { "ollama app", "Ollama" })
            foreach (var pr in Process.GetProcessesByName(name))
            {
                try { if (pr.MainWindowHandle != IntPtr.Zero) pr.CloseMainWindow(); } catch { }
                finally { pr.Dispose(); }
            }
    }

    // scarica e installa ollama se manca, poi si assicura che il server sia acceso (senza finestre)
    public static async Task EnsureInstalled(Action<string, double> report, CancellationToken ct)
    {
        if (await IsUp()) return;
        var exe = FindExe();
        if (exe == null)
        {
            var tmp = Path.Combine(Path.GetTempPath(), "OllamaSetup.exe");
            var msg = Loc.L("Scarico Ollama…", "Downloading Ollama…");
            report(msg, 0);
            using (var res = await Http.GetAsync("https://ollama.com/download/OllamaSetup.exe", HttpCompletionOption.ResponseHeadersRead, ct))
            {
                res.EnsureSuccessStatusCode();
                long total = res.Content.Headers.ContentLength ?? 0, got = 0;
                using var src = await res.Content.ReadAsStreamAsync(ct);
                using var dst = File.Create(tmp);
                var buf = new byte[81920];
                int n;
                while ((n = await src.ReadAsync(buf, ct)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, n), ct);
                    got += n;
                    report(msg, total > 0 ? (double)got / total : -1);
                }
            }
            report(Loc.L("Installo Ollama…", "Installing Ollama…"), -1);
            using var p = Process.Start(new ProcessStartInfo(tmp, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-") { UseShellExecute = true })
                ?? throw new Exception(Loc.L("non riesco ad avviare l'installer di Ollama", "can't start the Ollama installer"));
            // non si aspetta che l'installer esca: dopo l'installazione resta aperto finché c'è l'app di ollama che ha lanciato.
            // basta che i file ci siano e che il programma sia partito (o che sia passato un po' di tempo)
            var started = DateTime.UtcNow;
            DateTime? seen = null;
            while (!p.HasExited)
            {
                ct.ThrowIfCancellationRequested();
                if (FindExe() != null)
                {
                    seen ??= DateTime.UtcNow;
                    if (DateTime.UtcNow - seen > TimeSpan.FromSeconds(10) || await IsUp()) break;
                }
                if (DateTime.UtcNow - started > TimeSpan.FromMinutes(5)) break;
                await Task.Delay(1000, ct);
            }
            exe = FindExe() ?? throw new Exception(Loc.L("l'installazione di Ollama non è andata a buon fine", "the Ollama installation failed"));
        }

        report(Loc.L("Avvio Ollama…", "Starting Ollama…"), -1);
        for (int i = 0; i < 15; i++)   // l'installer di solito lo avvia da solo: intanto si chiudono le sue finestre
        {
            CloseOllamaWindows();
            if (await IsUp()) break;
            await Task.Delay(1000, ct);
        }
        if (!await IsUp())
        {
            Process.Start(new ProcessStartInfo(exe, "serve") { UseShellExecute = false, CreateNoWindow = true });   // server in nascosto
            for (int i = 0; i < 30 && !await IsUp(); i++) await Task.Delay(1000, ct);
        }
        CloseOllamaWindows();
        if (!await IsUp()) throw new Exception(Loc.L("Ollama non si avvia", "Ollama won't start"));
    }

    // scarica un modello (se c'è già finisce subito)
    public static async Task Pull(string model, Action<string, double> report, CancellationToken ct)
    {
        var body = new JsonObject { ["model"] = model, ["stream"] = true };
        var req = new HttpRequestMessage(HttpMethod.Post, Api + "/api/pull") { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
        using var res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!res.IsSuccessStatusCode) throw new Exception(await res.Content.ReadAsStringAsync(ct));
        using var sr = new StreamReader(await res.Content.ReadAsStreamAsync(ct));
        string? line;
        while ((line = await sr.ReadLineAsync(ct)) != null)
        {
            if (line.Length == 0) continue;
            var j = JsonNode.Parse(line)!;
            if (j["error"] != null) throw new Exception(j["error"]!.ToString());
            double total = j["total"]?.GetValue<double>() ?? 0, done = j["completed"]?.GetValue<double>() ?? 0;
            if (total > 0) report(Loc.L($"Scarico il modello {model}…", $"Downloading model {model}…"), done / total);
            else report(j["status"]?.ToString() ?? "", -1);
        }
    }
}
