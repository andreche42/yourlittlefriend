using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace YourLittleFriend;

// impostazioni dell'utente, salvate in %APPDATA%\yourlittlefriend\settings.json
public class Settings
{
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    public double? Lat { get; set; }
    public double? Lon { get; set; }
    public string Language { get; set; } = "";   // "it" o "en"
    public string Model { get; set; } = Llm.DefaultModel;
    public bool Onboarded { get; set; }

    static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "yourlittlefriend");
    static string FilePath => Path.Combine(Dir, "settings.json");

    public static Settings Current { get; } = Load();

    static Settings Load()
    {
        Settings s;
        try { s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); } catch { s = new(); }
        if (s.Language != "it" && s.Language != "en")
        {
            // la prima volta vale la lingua scelta nell'installer, altrimenti quella di windows
            string? l = null;
            try { l = Registry.CurrentUser.OpenSubKey(@"Software\yourlittlefriend")?.GetValue("Language") as string; } catch { }
            s.Language = l is "it" or "en" ? l : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "it" ? "it" : "en";
        }
        if (string.IsNullOrWhiteSpace(s.Model)) s.Model = Llm.DefaultModel;
        return s;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
