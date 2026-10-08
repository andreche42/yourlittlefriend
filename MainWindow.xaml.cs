using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace YourLittleFriend;

public partial class MainWindow : Window
{
    // ---- impostazioni ----
    const string Lat = "45.07", Lon = "7.69";              // per il meteo
    const string Model = "claude-haiku-5-5";                // ia veloce
    const string SystemPrompt = "sei un piccolo assistente nel notch del pc. rispondi in italiano, molto breve.";
    const double WOpen = 640, HOpen = 170, WClosed = 230, HClosed = 34;

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    readonly DispatcherTimer hold = new() { Interval = TimeSpan.FromMilliseconds(500) };
    readonly JsonArray local = new();
    const string LocalSystem = "sei MyLittleFriend, un piccolo assistente nel notch del pc di Andrea. rispondi sempre in italiano, brevissimo. "
        + "usa gli strumenti solo se l'utente chiede un'azione sul pc, poi conferma in una frase. non inventare risultati. "
        + "se non esiste uno strumento adatto dillo.";
    GlobalSystemMediaTransportControlsSessionManager? mgr;
    string? lastTitle, file;
    BitmapSource? cover;
    bool dragging;

    public MainWindow()
    {
        InitializeComponent();
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = 0;
        hold.Tick += (_, _) => { hold.Stop(); MaybeClose(); };

        var media = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        media.Tick += async (_, _) => await PollMedia();
        media.Start();
        var weather = new DispatcherTimer { Interval = TimeSpan.FromMinutes(20) };
        weather.Tick += async (_, _) => await LoadWeather();
        weather.Start();
        _ = LoadWeather();
        ShowTab(0);
    }

    // ---- apri / chiudi ----
    void SetOpen(bool o)
    {
        double w = o ? WOpen : WClosed, h = o ? HOpen : HClosed;
        Full.Visibility = o ? Visibility.Visible : Visibility.Collapsed;
        MiniMascot.Visibility = o ? Visibility.Collapsed : Visibility.Visible;
        var d = TimeSpan.FromMilliseconds(220);
        DoubleAnimation Anim(double to) => new(to, d) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        BeginAnimation(WidthProperty, Anim(w));
        BeginAnimation(HeightProperty, Anim(h));
        BeginAnimation(LeftProperty, Anim((SystemParameters.PrimaryScreenWidth - w) / 2));
    }

    void MaybeClose()
    {
        if (Root.IsMouseOver || dragging || ChatIn.IsKeyboardFocusWithin || FileIn.IsKeyboardFocusWithin) hold.Start();
        else SetOpen(false);
    }

    void OnEnter(object s, MouseEventArgs e) { hold.Stop(); SetOpen(true); }
    void OnLeave(object s, MouseEventArgs e) => hold.Start();

    // ---- schede e menu ----
    void ShowTab(int i)
    {
        PageHome.Visibility = i == 0 ? Visibility.Visible : Visibility.Collapsed;
        PageChat.Visibility = i == 1 ? Visibility.Visible : Visibility.Collapsed;
        PageFile.Visibility = i == 2 ? Visibility.Visible : Visibility.Collapsed;
        var tabs = new[] { T0, T1, T2 };
        for (int j = 0; j < tabs.Length; j++)
            tabs[j].Background = j == i ? new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B)) : Brushes.Transparent;
    }

    void Tab_Click(object s, RoutedEventArgs e) => ShowTab(int.Parse((string)((Button)s).Tag));

    void Gear_Click(object s, RoutedEventArgs e)
    {
        var m = (ContextMenu)Resources["Menu"];
        m.PlacementTarget = (UIElement)s;
        m.IsOpen = true;
    }

    void Quit_Click(object s, RoutedEventArgs e) => Application.Current.Shutdown();

    // ---- musica ----
    async Task PollMedia()
    {
        try
        {
            mgr ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var s = mgr.GetCurrentSession();
            var p = s == null ? null : await s.TryGetMediaPropertiesAsync();
            if (s == null || p == null || string.IsNullOrEmpty(p.Title)) { SetMusic(false); return; }
            if (p.Title != lastTitle || cover == null)
            {
                lastTitle = p.Title;
                cover = await LoadCover(p.Thumbnail);
                ApplyCover();
            }
            BPlay.Content = s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing ? "⏸" : "▶";
            SongText.Text = $"{p.Title} - {p.Artist}";
            SetMusic(true);
        }
        catch { SetMusic(false); }
    }

    void SetMusic(bool on)
    {
        Greet.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        MusicPanel.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
    }

    static async Task<BitmapSource?> LoadCover(IRandomAccessStreamReference? t)
    {
        if (t == null) return null;
        try
        {
            using var rs = await t.OpenReadAsync();
            var mem = new MemoryStream();
            await rs.AsStreamForRead().CopyToAsync(mem);
            mem.Position = 0;
            var b = new BitmapImage();
            b.BeginInit();
            b.CacheOption = BitmapCacheOption.OnLoad;
            b.StreamSource = mem;
            b.EndInit();
            b.Freeze();
            return b;
        }
        catch { return null; }
    }

    static Color Mix(Color a, Color b, double t) =>
        Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    static Color Average(BitmapSource b)
    {
        var small = new TransformedBitmap(b, new ScaleTransform(16.0 / b.PixelWidth, 16.0 / b.PixelHeight));
        var fc = new FormatConvertedBitmap(small, PixelFormats.Bgra32, null, 0);
        int n = fc.PixelWidth * fc.PixelHeight;
        var px = new byte[n * 4];
        fc.CopyPixels(px, fc.PixelWidth * 4, 0);
        long r = 0, g = 0, bl = 0;
        for (int i = 0; i < px.Length; i += 4) { bl += px[i]; g += px[i + 1]; r += px[i + 2]; }
        return Color.FromRgb((byte)(r / n), (byte)(g / n), (byte)(bl / n));
    }

    void ApplyCover()
    {
        Color tint = Color.FromRgb(40, 40, 70), accent = Color.FromRgb(0x8A, 0xB4, 0xFF);
        CoverImg.Source = cover;
        if (cover != null)
        {
            var avg = Average(cover);
            tint = Mix(avg, Colors.Black, .45);
            accent = Mix(avg, Colors.White, .55);
        }
        MusicPanel.Background = new LinearGradientBrush(tint, Color.FromRgb(8, 8, 20), 0);
        var ab = new SolidColorBrush(accent);
        SongText.Foreground = ab;
        BPrev.Foreground = BPlay.Foreground = BNext.Foreground = ab;
    }

    async void Ctl(object s, RoutedEventArgs e)
    {
        var c = mgr?.GetCurrentSession();
        if (c == null) return;
        switch ((string)((Button)s).Tag)
        {
            case "prev": await c.TrySkipPreviousAsync(); break;
            case "play": await c.TryTogglePlayPauseAsync(); break;
            case "next": await c.TrySkipNextAsync(); break;
        }
    }

    // ---- meteo ----
    async Task LoadWeather()
    {
        try
        {
            var j = await Http.GetStringAsync($"https://api.open-meteo.com/v1/forecast?latitude={Lat}&longitude={Lon}&current=temperature_2m,weather_code");
            using var d = JsonDocument.Parse(j);
            var cur = d.RootElement.GetProperty("current");
            int c = cur.GetProperty("weather_code").GetInt32();
            double t = cur.GetProperty("temperature_2m").GetDouble();
            string x = c <= 1 ? "è previsto sole" : c <= 3 ? "sono previste nuvole" : c <= 48 ? "è prevista nebbia"
                : (c is >= 71 and <= 77 or 85 or 86) ? "è prevista neve" : c >= 95 ? "è previsto temporale" : "è prevista pioggia";
            WeatherText.Text = $"Oggi ci sono {Math.Round(t).ToString(CultureInfo.InvariantCulture)}°\nPer oggi {x}";
        }
        catch { }
    }

    // ---- ia ----
    static async Task<string> CallApi(List<object> messages)
    {
        var key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrEmpty(key)) return "manca la variabile ANTHROPIC_API_KEY";
        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", key);
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = new StringContent(
            JsonSerializer.Serialize(new { model = Model, max_tokens = 500, system = SystemPrompt, messages }),
            Encoding.UTF8, "application/json");
        var res = await Http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) return "errore: " + body;
        using var d = JsonDocument.Parse(body);
        return string.Concat(d.RootElement.GetProperty("content").EnumerateArray()
            .Select(b => b.TryGetProperty("text", out var t) ? t.GetString() : ""));
    }

    static async Task<string> Safe(Func<Task<string>> f)
    {
        try { return await f(); } catch (Exception ex) { return "errore: " + ex.Message; }
    }

    static List<object> FileBlocks(string path, string q)
    {
        var img = new Dictionary<string, string>
        { [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".webp"] = "image/webp", [".gif"] = "image/gif" };
        var blocks = new List<object>();
        if (img.TryGetValue(Path.GetExtension(path).ToLowerInvariant(), out var mt))
            blocks.Add(new { type = "image", source = new { type = "base64", media_type = mt, data = Convert.ToBase64String(File.ReadAllBytes(path)) } });
        else
        {
            string t;
            try { t = File.ReadAllText(path); if (t.Length > 20000) t = t[..20000]; } catch { t = ""; }
            blocks.Add(new { type = "text", text = $"file {Path.GetFileName(path)}:\n{t}" });
        }
        blocks.Add(new { type = "text", text = q });
        return blocks;
    }

    async void ChatKey(object s, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        var q = ChatIn.Text.Trim();
        if (q == "") return;
        ChatIn.Clear();
        ChatOut.Text = "…";
        ChatOut.Text = await Safe(() => LocalChat(q));
    }

    // chat con llm locale (ollama) + azioni sul pc. le azioni sensibili passano dal popup di conferma.
    async Task<string> LocalChat(string q)
    {
        if (local.Count == 0) local.Add(new JsonObject { ["role"] = "system", ["content"] = LocalSystem });
        local.Add(new JsonObject { ["role"] = "user", ["content"] = q });
        var tools = JsonNode.Parse(Actions.ToolsJson)!;
        for (int i = 0; i < 4; i++)
        {
            var msg = await Llm.Chat(local, tools);
            local.Add(msg.DeepClone());
            if (msg["tool_calls"] is not JsonArray calls || calls.Count == 0)
            {
                while (local.Count > 15) local.RemoveAt(1);
                while (local.Count > 1 && local[1]!["role"]!.ToString() != "user") local.RemoveAt(1);
                return Llm.Clean(msg["content"]?.ToString());
            }
            foreach (var c in calls)
            {
                var fn = c!["function"]!;
                var name = fn["name"]!.ToString();
                var plan = Actions.Make(name, fn["arguments"]);
                string res;
                if (plan == null) res = "strumento sconosciuto";
                else if (plan.Sensitive && !Confirm.Ask(plan)) res = "l'utente ha rifiutato l'azione";
                else
                {
                    try { res = await plan.Run(); } catch (Exception ex) { res = "errore: " + ex.Message; }
                }
                local.Add(new JsonObject { ["role"] = "tool", ["tool_name"] = name, ["content"] = res });
            }
        }
        return "non sono riuscito a finire, riprova";
    }

    // file di testo: llm locale, senza strumenti (cosi' un file non puo' far partire azioni)
    static async Task<string> LocalFile(string path, string q)
    {
        string t;
        try { t = File.ReadAllText(path); if (t.Length > 8000) t = t[..8000]; } catch { t = ""; }
        var msgs = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = LocalSystem },
            new JsonObject { ["role"] = "user", ["content"] = $"file {Path.GetFileName(path)}:\n{t}\n\n{q}" }
        };
        var m = await Llm.Chat(msgs, null);
        return Llm.Clean(m["content"]?.ToString());
    }

    async void FileKey(object s, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || file == null) return;
        var q = FileIn.Text.Trim();
        if (q == "") return;
        FileIn.Clear();
        Bubble.Text = "…";
        var path = file;
        bool isImg = Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif";
        Bubble.Text = await Safe(() => isImg
            ? CallApi(new List<object> { new { role = "user", content = FileBlocks(path, q) } })
            : LocalFile(path, q));
    }

    // ---- drag & drop ----
    void OnDragEnter(object s, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        e.Effects = DragDropEffects.Copy;
        dragging = true;
        hold.Stop();
        SetOpen(true);
        ShowTab(2);
    }

    void OnDragOver(object s, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    void OnDragLeave(object s, DragEventArgs e) { dragging = false; hold.Start(); }

    void OnDrop(object s, DragEventArgs e)
    {
        dragging = false;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] f && f.Length > 0) SetFile(f[0]);
        hold.Start();
    }

    void SetFile(string path)
    {
        file = path;
        FileNameText.Text = Path.GetFileName(path);
        Bubble.Text = "Cosa faccio con questo?";
        DropZone.Visibility = Visibility.Collapsed;
        FileView.Visibility = Visibility.Visible;
        ShowTab(2);
    }
}
