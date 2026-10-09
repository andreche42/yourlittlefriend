using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace YourLittleFriend;

public record WikiImage(BitmapSource Image, string Caption, string Url);

public record WikiPage(string Title, string Description, string Summary, string Rest, BitmapSource? Photo, List<WikiImage> Gallery, string Url);

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

        // foto principale e galleria (le altre immagini dell'articolo) si scaricano insieme
        string? mainName = page.TryGetProperty("pageimage", out var pi) ? pi.GetString()?.Replace('_', ' ') : null;
        var photoTask = page.TryGetProperty("thumbnail", out var th) && th.TryGetProperty("source", out var src) && src.GetString() is { } url
            ? LoadImage(url, 220) : Task.FromResult<BitmapSource?>(null);
        var galleryTask = Gallery(api, title, mainName);
        await Task.WhenAll(photoTask, galleryTask);

        return new WikiPage(title, desc, summary, rest, photoTask.Result, galleryTask.Result, $"https://{host}.wikipedia.org/wiki/{Uri.EscapeDataString(title.Replace(' ', '_'))}");
    }

    // loghi, icone e simili non sono foto: si scartano
    static readonly Regex Junk = new(@"\b(flags?|icons?|logos?|symbols?|ambox|commons|wikimedia|wikipedia|wikiquote|wikidata|edit|padlock|question|stub|portal|increase|decrease|steady|locator|blank|disambig|pencil|crystal|nuvola|folder|speaker|audio|oojs|bandiera|stemma|pushpin|map pin)\b", RegexOptions.IgnoreCase);

    // le altre immagini dell'articolo (fino a 8, solo foto abbastanza grandi)
    static async Task<List<WikiImage>> Gallery(string api, string title, string? mainName)
    {
        var result = new List<WikiImage>();
        try
        {
            using var d = JsonDocument.Parse(await Http.GetStringAsync(
                $"{api}?action=query&generator=images&titles={Uri.EscapeDataString(title)}&gimlimit=40&prop=imageinfo&iiprop=url|mime|size&iiurlwidth=360&format=json&formatversion=2"));
            if (!d.RootElement.TryGetProperty("query", out var q) || !q.TryGetProperty("pages", out var pages)) return result;
            var picks = new List<(string Name, string Thumb, string Page)>();
            foreach (var pg in pages.EnumerateArray())
            {
                var name = pg.TryGetProperty("title", out var tt) ? tt.GetString() ?? "" : "";
                if (name == "" || Junk.IsMatch(name)) continue;
                if (mainName != null && name.EndsWith(mainName, StringComparison.OrdinalIgnoreCase)) continue;   // è già la foto principale
                if (!pg.TryGetProperty("imageinfo", out var ii) || ii.GetArrayLength() == 0) continue;
                var info = ii[0];
                var mime = info.TryGetProperty("mime", out var m) ? m.GetString() ?? "" : "";
                if (mime is not ("image/jpeg" or "image/png" or "image/webp")) continue;
                int w = info.TryGetProperty("width", out var wi) ? wi.GetInt32() : 0, h = info.TryGetProperty("height", out var hi) ? hi.GetInt32() : 0;
                if (w < 220 || h < 140) continue;
                if (info.TryGetProperty("thumburl", out var tu) && tu.GetString() is { } thumb)
                    picks.Add((name, thumb, info.TryGetProperty("descriptionurl", out var du) ? du.GetString() ?? "" : ""));
            }
            var take = picks.Take(8).ToList();
            var imgs = await Task.WhenAll(take.Select(t => LoadImage(t.Thumb, 360)));
            for (int i = 0; i < take.Count; i++)
                if (imgs[i] != null)
                {
                    var cap = Regex.Replace(take[i].Name, @"^[^:]+:|\.\w{3,4}$", "").Replace('_', ' ').Trim();   // "File:Foo bar.jpg" -> "Foo bar"
                    result.Add(new WikiImage(imgs[i]!, cap, take[i].Page));
                }
        }
        catch { }
        return result;
    }

    static async Task<BitmapSource?> LoadImage(string url, int width)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(url);
            var b = new BitmapImage();
            b.BeginInit();
            b.CacheOption = BitmapCacheOption.OnLoad;
            b.DecodePixelWidth = width;
            b.StreamSource = new MemoryStream(bytes);
            b.EndInit();
            b.Freeze();
            return b;
        }
        catch { return null; }
    }
}
