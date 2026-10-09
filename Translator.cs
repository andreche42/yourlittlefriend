using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace YourLittleFriend;

public record Lang(string Code, string It, string En);

// traduzioni con DeepL (chiave api) e/o LibreTranslate (server tuo o pubblico). se sono impostati entrambi si prova prima DeepL.
public static class Translator
{
    public static readonly Lang[] Langs =
    {
        new("it", "Italiano", "Italian"), new("en", "Inglese", "English"), new("es", "Spagnolo", "Spanish"), new("fr", "Francese", "French"),
        new("de", "Tedesco", "German"), new("pt", "Portoghese", "Portuguese"), new("nl", "Olandese", "Dutch"), new("pl", "Polacco", "Polish"),
        new("ru", "Russo", "Russian"), new("ja", "Giapponese", "Japanese"), new("zh", "Cinese", "Chinese"), new("ko", "Coreano", "Korean"),
        new("ar", "Arabo", "Arabic"), new("tr", "Turco", "Turkish"), new("sv", "Svedese", "Swedish"),
    };

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static string Name(string code) =>
        code == "auto" ? Loc.L("Rileva lingua", "Detect language") : Array.Find(Langs, x => x.Code == code) is { } lang ? Loc.L(lang.It, lang.En) : code;

    public static bool Configured => !string.IsNullOrWhiteSpace(Settings.Current.DeepLKey) || !string.IsNullOrWhiteSpace(Settings.Current.LibreUrl);

    // restituisce testo tradotto, nome del servizio usato e lingua rilevata (se nota)
    public static async Task<(string Text, string Engine, string? Detected)> Translate(string text, string from, string to)
    {
        var s = Settings.Current;
        var errors = new List<string>();
        if (!string.IsNullOrWhiteSpace(s.DeepLKey))
            try { return await DeepL(text, from, to, s.DeepLKey.Trim()); } catch (Exception ex) { errors.Add(ex.Message); }
        if (!string.IsNullOrWhiteSpace(s.LibreUrl))
            try { return await Libre(text, from, to, s.LibreUrl.Trim(), s.LibreKey.Trim()); } catch (Exception ex) { errors.Add(ex.Message); }
        throw new Exception(errors.Count > 0 ? string.Join(" | ", errors) : Loc.L("nessun traduttore impostato", "no translator configured"));
    }

    static string Short(string s) => s.Length > 160 ? s[..160] + "…" : s;

    static async Task<(string, string, string?)> DeepL(string text, string from, string to, string key)
    {
        var host = key.EndsWith(":fx") ? "api-free.deepl.com" : "api.deepl.com";   // le chiavi gratuite finiscono con :fx
        var target = to switch { "en" => "EN-US", "pt" => "PT-PT", _ => to.ToUpperInvariant() };
        var body = new JsonObject { ["text"] = new JsonArray(JsonValue.Create(text)), ["target_lang"] = target };
        if (from != "auto") body["source_lang"] = from.ToUpperInvariant();
        var req = new HttpRequestMessage(HttpMethod.Post, $"https://{host}/v2/translate") { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
        req.Headers.TryAddWithoutValidation("Authorization", "DeepL-Auth-Key " + key);
        using var res = await Http.SendAsync(req);
        var txt = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception($"DeepL {(int)res.StatusCode}: {Short(txt)}");
        using var d = JsonDocument.Parse(txt);
        var t = d.RootElement.GetProperty("translations")[0];
        return (t.GetProperty("text").GetString() ?? "", "DeepL", t.TryGetProperty("detected_source_language", out var ds) ? ds.GetString()?.ToLowerInvariant() : null);
    }

    static async Task<(string, string, string?)> Libre(string text, string from, string to, string url, string key)
    {
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
        url = url.TrimEnd('/') + "/translate";
        var body = new JsonObject { ["q"] = text, ["source"] = from, ["target"] = to, ["format"] = "text" };
        if (key != "") body["api_key"] = key;
        using var res = await Http.PostAsync(url, new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));
        var txt = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception($"LibreTranslate {(int)res.StatusCode}: {Short(txt)}");
        using var d = JsonDocument.Parse(txt);
        string? det = d.RootElement.TryGetProperty("detectedLanguage", out var dl) && dl.ValueKind == JsonValueKind.Object && dl.TryGetProperty("language", out var l) ? l.GetString() : null;
        return (d.RootElement.GetProperty("translatedText").GetString() ?? "", "LibreTranslate", det);
    }
}
