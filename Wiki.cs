using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace YourLittleFriend;

public record WikiPage(string Title, string Description, string Summary, string Rest, BitmapSource? Photo, string Url);

// cerca su wikipedia (nella lingua dell'app) e restituisce titolo, descrizione, riassunto, resto dell'articolo e foto
public static class Wiki
{
    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("yourlittlefriend/1.1 (https://github.com/andreche42/yourlittlefriend)");   // wikimedia chiede di dichiararsi
        return c;
    }

    public static async Task<WikiPage?> Lookup(string query)
    {
        var host = Loc.En ? "en" : "it";
        var api = $"https://{host}.wikipedia.org/w/api.php";

        // 1) il titolo che corrisponde meglio
        using var sr = JsonDocument.Parse(await Http.GetStringAsync($"{api}?action=query&list=search&srsearch={Uri.EscapeDataString(query)}&srlimit=1&format=json&formatversion=2"));
        var hits = sr.RootElement.GetProperty("query").GetProperty("search");
        if (hits.GetArrayLength() == 0) return null;
        var title = hits[0].GetProperty("title").GetString() ?? query;

        // 2) articolo completo in testo semplice, descrizione breve e miniatura
        using var pg = JsonDocument.Parse(await Http.GetStringAsync(
            $"{api}?action=query&prop=extracts|pageimages|description&explaintext=1&exsectionformat=plain&redirects=1&piprop=thumbnail&pithumbsize=400&titles={Uri.EscapeDataString(title)}&format=json&formatversion=2"));
        var page = pg.RootElement.GetProperty("query").GetProperty("pages")[0];
        if (page.TryGetProperty("missing", out _)) return null;

        title = page.TryGetProperty("title", out var t) ? t.GetString() ?? title : title;
        var desc = page.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
        var full = (page.TryGetProperty("extract", out var ex) ? ex.GetString() ?? "" : "").Trim();
        if (full.Length > 20000) full = full[..20000] + "…";

        // il riassunto è il primo paragrafo, il resto va sotto (si scorre per leggerlo)
        int cut = full.IndexOf('\n');
        string summary = cut < 0 ? full : full[..cut].Trim();
        string rest = cut < 0 ? "" : full[cut..].Trim();
        if (summary.Length > 700) { rest = (summary[700..] + "\n" + rest).Trim(); summary = summary[..700] + "…"; }

        BitmapSource? photo = null;
        if (page.TryGetProperty("thumbnail", out var th) && th.TryGetProperty("source", out var src) && src.GetString() is { } url)
            photo = await LoadImage(url);

        return new WikiPage(title, desc, summary, rest, photo, $"https://{host}.wikipedia.org/wiki/{Uri.EscapeDataString(title.Replace(' ', '_'))}");
    }

    static async Task<BitmapSource?> LoadImage(string url)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(url);
            var b = new BitmapImage();
            b.BeginInit();
            b.CacheOption = BitmapCacheOption.OnLoad;
            b.DecodePixelWidth = 220;
            b.StreamSource = new MemoryStream(bytes);
            b.EndInit();
            b.Freeze();
            return b;
        }
        catch { return null; }
    }
}
