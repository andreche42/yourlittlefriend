using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace YourLittleFriend;

// un promemoria: At è l'ora in cui deve scattare
public record Reminder(string Text, DateTime At);

// promemoria dell'app. stanno in %APPDATA%\yourlittlefriend\reminders.json, così restano anche dopo il riavvio.
// il controllo lo fa la finestra ogni pochi secondi: quello che è scaduto mentre l'app era chiusa scatta al primo giro.
public static class Reminders
{
    static readonly object Sync = new();
    static string FilePath => Path.Combine(Settings.Dir, "reminders.json");

    public static List<Reminder> All { get; } = Load();

    // scatta quando è l'ora: la finestra lo fa vedere all'omino
    public static event Action<Reminder>? Due;

    // quando l'elenco cambia (ne aggiungi uno, ne scade uno): la finestra aggiorna la campanella
    public static event Action? Changed;

    static List<Reminder> Load()
    {
        try
        {
            var list = JsonSerializer.Deserialize<List<Reminder>>(File.ReadAllText(FilePath)) ?? new();
            list.RemoveAll(r => r.Text.Trim().Length == 0);
            return list;
        }
        catch { return new(); }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Settings.Dir);
            List<Reminder> copy;
            lock (Sync) copy = All.ToList();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(copy));
        }
        catch { }
    }

    public static Reminder Add(string text, DateTime at)
    {
        var r = new Reminder(text.Trim(), at);
        lock (Sync) All.Add(r);
        Save();
        Changed?.Invoke();
        return r;
    }

    // toglie quello che corrisponde al testo (o tutti, se il testo è vuoto)
    public static int Remove(string? text)
    {
        int n;
        lock (Sync)
        {
            n = string.IsNullOrWhiteSpace(text)
                ? All.Count
                : All.RemoveAll(r => r.Text.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase));
        }
        if (n > 0) { Save(); Changed?.Invoke(); }
        return n;
    }

    public static List<Reminder> Next(int count)
    {
        lock (Sync) return All.OrderBy(r => r.At).Take(count).ToList();
    }

    // chiamato dal timer della finestra: fa scattare quello che è arrivato a destinazione
    public static void Tick()
    {
        var now = DateTime.Now;
        List<Reminder> due;
        lock (Sync)
        {
            due = All.Where(r => r.At <= now).ToList();
            if (due.Count == 0) return;
            All.RemoveAll(r => due.Contains(r));
        }
        Save();
        Changed?.Invoke();
        foreach (var r in due) Due?.Invoke(r);
    }

    // legge l'ora da quello che ha scritto l'utente: "alle 18", "alle 18:30", "domani alle 9", "fra 20 minuti", "tra 2 ore", "fra mezz'ora"
    public static bool ParseWhen(string text, out DateTime at)
    {
        at = default;
        if (text.Trim().Length == 0) return false;
        var t = " " + text.ToLowerInvariant().Replace('’', '\'') + " ";

        // un'ora precisa: "alle 18", "per le 9", "ore 18", "18:30"
        var clock = Regex.Match(t, @"\b(?:alle|per le|ore)\s*(\d{1,2})(?:[:.](\d{2}))?\b");
        if (!clock.Success) clock = Regex.Match(t, @"\b(\d{1,2})[:.](\d{2})\b");
        if (clock.Success && int.TryParse(clock.Groups[1].Value, out var h) && h is >= 0 and < 24)
        {
            int m = clock.Groups[2].Success && int.TryParse(clock.Groups[2].Value, out var mm) ? Math.Min(mm, 59) : 0;
            var when = DateTime.Today.AddHours(h).AddMinutes(m);
            if (t.Contains("domani")) when = when.AddDays(1);
            if (when <= DateTime.Now) when = when.AddDays(1);   // quell'ora oggi è già passata: domani
            at = when;
            return true;
        }

        // fra quanto: "fra 20 minuti", "tra 2 ore"
        var delay = Regex.Match(t, @"\b(?:fra|tra)\s+(\d{1,3})\s*(minut|or[ae]|h)\b");
        if (delay.Success && int.TryParse(delay.Groups[1].Value, out var n) && n > 0)
        {
            var unit = delay.Groups[2].Value;
            at = DateTime.Now.Add(unit.StartsWith("or") || unit == "h" ? TimeSpan.FromHours(n) : TimeSpan.FromMinutes(n));
            return true;
        }

        if (Regex.IsMatch(t, @"\b(?:fra|tra)\s+mezz'?ora\b")) { at = DateTime.Now.AddMinutes(30); return true; }
        if (Regex.IsMatch(t, @"\b(?:fra|tra)\s+un'?ora\b")) { at = DateTime.Now.AddHours(1); return true; }
        return false;
    }

    // "oggi alle 18:00", "domani alle 9:30", "il 24/12 alle 20:00"
    public static string When(DateTime at)
    {
        var time = at.ToString("HH:mm");
        if (at.Date == DateTime.Today) return Loc.L($"oggi alle {time}", $"today at {time}");
        if (at.Date == DateTime.Today.AddDays(1)) return Loc.L($"domani alle {time}", $"tomorrow at {time}");
        return Loc.L($"il {at:dd/MM} alle {time}", $"on {at:dd/MM} at {time}");
    }
}
