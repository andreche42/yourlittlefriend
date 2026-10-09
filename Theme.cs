using System.Windows;
using System.Windows.Media;

namespace YourLittleFriend;

public record Preset(string Id, string It, string En, string Bg, string Fg, string Panel, string Accent, string Mascot);

// aspetto dell'app: i colori sono risorse dinamiche (BgBrush, FgBrush, ...) che qui si ricalcolano, così tutto cambia al volo
public static class Theme
{
    public static readonly Preset[] Presets =
    {
        new("dark",   "Notte",     "Night",    "#000000", "#FFFFFF", "#2B2B2B", "#3A6BFF", "#F4F4F4"),
        new("light",  "Giorno",    "Day",      "#F5F5F7", "#1D1D1F", "#E2E2E7", "#3A6BFF", "#3A3A3C"),
        new("ocean",  "Oceano",    "Ocean",    "#06202E", "#E6F6FF", "#0F3A52", "#2EC4B6", "#BDF0FF"),
        new("sunset", "Tramonto",  "Sunset",   "#2B0F1E", "#FFEFE8", "#4A1B33", "#FF8A4C", "#FFD2A8"),
        new("forest", "Foresta",   "Forest",   "#0E1F14", "#EAF7EC", "#1C3A26", "#6FCF7F", "#D9F5C8"),
        new("candy",  "Caramella", "Candy",    "#2A1030", "#FFF0FA", "#4B2352", "#FF5FA8", "#FFC4E3"),
    };

    public static readonly string[] AccentColors = { "#3A6BFF", "#2EC4B6", "#6FCF7F", "#F5C542", "#FF8A4C", "#FF5C5C", "#FF5FA8", "#B07CFF", "#FFFFFF" };
    public static readonly string[] MascotColors = { "#F4F4F4", "#FFD2A8", "#FFC4E3", "#BDF0FF", "#D9F5C8", "#FFF3B0", "#C9B6FF", "#8A8A8A", "#3A3A3C" };

    public static Color? TryParse(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        try { return (Color)ColorConverter.ConvertFromString(hex.Trim()); } catch { return null; }
    }

    static Color Mix(Color a, Color b, double t) =>
        Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    // colore leggibile sopra c: scuro se c è chiaro, chiaro se c è scuro
    static Color On(Color c) => 0.299 * c.R + 0.587 * c.G + 0.114 * c.B > 150 ? Color.FromRgb(0x22, 0x22, 0x22) : Color.FromRgb(0xF4, 0xF4, 0xF4);

    static void Set(string key, Color c) => Application.Current.Resources[key] = new SolidColorBrush(c);

    public static Brush Res(string key) => (Brush)Application.Current.Resources[key];

    public static void Apply()
    {
        var s = Settings.Current;
        var p = Array.Find(Presets, x => x.Id == s.Theme) ?? Presets[0];
        var bg = TryParse(p.Bg)!.Value;
        var fg = TryParse(p.Fg)!.Value;
        var panel = TryParse(p.Panel)!.Value;
        var accent = TryParse(s.Accent) ?? TryParse(p.Accent)!.Value;
        var mascot = TryParse(s.MascotColor) ?? TryParse(p.Mascot)!.Value;
        byte alpha = (byte)(Math.Clamp(s.Opacity, 0.5, 1) * 255);

        Set("BgBrush", Color.FromArgb(alpha, bg.R, bg.G, bg.B));   // sfondo del notch (può essere trasparente)
        Set("BgSolidBrush", bg);
        Set("FgBrush", fg);
        Set("FgDimBrush", Mix(fg, bg, .4));
        Set("PanelBrush", panel);
        Set("PanelHoverBrush", Mix(panel, fg, .14));
        Set("WindowBrush", Mix(bg, fg, .06));
        Set("CardBrush", Mix(bg, fg, .12));
        Set("AccentBrush", accent);
        Set("AccentFgBrush", On(accent));
        Set("MascotBrush", mascot);
        Set("MascotEyeBrush", On(mascot));
        Set("OutlineBrush", s.Outline ? accent : Colors.Transparent);
        // lo spessore è 0 quando il contorno è spento: un bordo trasparente lascerebbe uno spazio visibile attorno al notch. in alto non c'è mai (è attaccato allo schermo)
        double t = s.Outline ? 1.5 : 0;
        Application.Current.Resources["OutlineThickness"] = new Thickness(t, 0, t, t);
    }
}
