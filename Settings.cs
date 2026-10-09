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
    public bool WebSearch { get; set; } = true;   // l'ia può cercare su internet
    public string Theme { get; set; } = "dark";
    public string Accent { get; set; } = "";       // colore principale personalizzato (vuoto = quello del tema)
    public string MascotColor { get; set; } = ""; // colore dell'omino personalizzato (vuoto = quello del tema)
    public double Opacity { get; set; } = 1;       // opacità dello sfondo del notch
    public bool Outline { get; set; }              // contorno colorato attorno al notch

    // funzioni attivabili e disattivabili (la home c'è sempre)
    public bool ChatOn { get; set; } = true;
    public bool HolderOn { get; set; } = true;
    public bool AskFileOn { get; set; } = true;
    public bool CalcOn { get; set; } = true;
    public bool TranslateOn { get; set; } = true;
    public bool WikiOn { get; set; } = true;
    public bool WeatherOn { get; set; } = true;

    // calcolatrice
    public bool CalcDeg { get; set; } = true;       // gradi (true) o radianti

    // traduttore: DeepL e/o LibreTranslate
    public string DeepLKey { get; set; } = "";
    public string LibreUrl { get; set; } = "";
    public string LibreKey { get; set; } = "";
    public string TrFrom { get; set; } = "auto";
    public string TrTo { get; set; } = "";

    // aspetto: "notch" (attaccato in alto) oppure "bubble" (bolla volante che si sposta e ricorda dove l'hai lasciata)
    public string Style { get; set; } = "notch";
    public double? BubbleX { get; set; }            // centro della bolla sullo schermo
    public double? BubbleY { get; set; }
    public double BubbleSize { get; set; } = 84;

    // dimensione del pannello espanso (si regola trascinando l'angolo in basso a destra)
    public double ExpW { get; set; } = 820;
    public double ExpH { get; set; } = 460;

    // lente di ingrandimento (doppio clic sull'omino): colore della sottolineatura, dimensione e ingrandimento
    public bool LensOn { get; set; } = true;
    public string LensColor { get; set; } = "#FFE600";
    public double LensSize { get; set; } = 380;
    public double LensZoom { get; set; } = 2.5;

    internal static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "yourlittlefriend");
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
