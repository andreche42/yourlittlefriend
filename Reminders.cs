using System.IO;
using System.Text.Json;

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
        if (n > 0) Save();
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
        foreach (var r in due) Due?.Invoke(r);
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
