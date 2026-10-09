using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace YourLittleFriend;

public record HolderItem(string Kind, string Value);   // Kind: "file" oppure "text"

// holder: ci trascini file o testo, restano qui (anche dopo il riavvio) e li puoi ritrascinare dove vuoi.
// i file sono solo riferimenti al percorso, non vengono copiati.
public partial class HolderPanel : UserControl
{
    public event Action<bool>? InternalDrag;   // true mentre stai trascinando fuori un elemento

    const int Max = 40;
    static string FilePath => Path.Combine(Settings.Dir, "holder.json");
    static readonly string[] ImageExt = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };
    List<HolderItem> items = new();

    public HolderPanel()
    {
        InitializeComponent();
        Empty.Text = Loc.L("Trascina qui file o testo", "Drop files or text here");
        ClearBtn.ToolTip = Loc.L("Svuota", "Clear all");
        try { items = JsonSerializer.Deserialize<List<HolderItem>>(File.ReadAllText(FilePath)) ?? new(); } catch { }
        items.RemoveAll(i => i.Kind == "file" && !File.Exists(i.Value) && !Directory.Exists(i.Value));   // file spariti nel frattempo
        Render();
    }

    // ---- aggiungere ----
    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (var p in paths)
        {
            items.RemoveAll(i => i.Kind == "file" && string.Equals(i.Value, p, StringComparison.OrdinalIgnoreCase));
            items.Insert(0, new HolderItem("file", p));
        }
        Commit();
    }

    public void AddText(string text)
    {
        text = text.Trim();
        if (text == "") return;
        if (text.Length > 20000) text = text[..20000];
        items.RemoveAll(i => i.Kind == "text" && i.Value == text);
        items.Insert(0, new HolderItem("text", text));
        Commit();
    }

    void Remove(HolderItem it) { items.Remove(it); Commit(false); }

    void Clear_Click(object s, RoutedEventArgs e) { items.Clear(); Commit(false); }

    void Wheel(object s, MouseWheelEventArgs e)
    {
        Scroll.ScrollToHorizontalOffset(Scroll.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    void Commit(bool toStart = true)
    {
        if (items.Count > Max) items.RemoveRange(Max, items.Count - Max);
        try
        {
            Directory.CreateDirectory(Settings.Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(items));
        }
        catch { }
        Render();
        if (toStart) Scroll.ScrollToHorizontalOffset(0);
    }

    // ---- disegno ----
    void Render()
    {
        Items.Children.Clear();
        foreach (var it in items) Items.Children.Add(Tile(it));
        bool none = items.Count == 0;
        Empty.Visibility = none ? Visibility.Visible : Visibility.Collapsed;
        ClearBtn.Visibility = none ? Visibility.Collapsed : Visibility.Visible;
        Frame.Opacity = none ? .8 : .25;   // con qualcosa dentro la cornice si fa discreta
    }

    static BitmapSource? FileIcon(string path)
    {
        try
        {
            if (Array.IndexOf(ImageExt, Path.GetExtension(path).ToLowerInvariant()) >= 0)
            {
                var b = new BitmapImage();   // le immagini si vedono in miniatura
                b.BeginInit();
                b.DecodePixelWidth = 80;
                b.CacheOption = BitmapCacheOption.OnLoad;
                b.UriSource = new Uri(path);
                b.EndInit();
                b.Freeze();
                return b;
            }
            using var ic = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (ic == null) return null;
            var bs = Imaging.CreateBitmapSourceFromHIcon(ic.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bs.Freeze();
            return bs;
        }
        catch { return null; }
    }

    UIElement Tile(HolderItem it)
    {
        bool file = it.Kind == "file";
        var tile = new Border
        {
            Width = file ? 80 : 140, Height = 92, Margin = new Thickness(4, 0, 4, 0), Padding = new Thickness(8, 8, 8, 4),
            CornerRadius = new CornerRadius(12), Cursor = Cursors.Hand
        };
        tile.SetResourceReference(Border.BackgroundProperty, "PanelBrush");
        var grid = new Grid();
        TextBlock? label = null;   // la scritta del testo, per il "copiato!"

        if (file)
        {
            bool dir = Directory.Exists(it.Value);
            bool missing = !dir && !File.Exists(it.Value);
            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(it.Value));
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var icon = dir || missing ? null : FileIcon(it.Value);
            sp.Children.Add(icon != null
                ? new Image { Source = icon, Width = 40, Height = 40, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center }
                : new TextBlock { Text = dir ? "📁" : "📄", FontSize = 34, HorizontalAlignment = HorizontalAlignment.Center });
            sp.Children.Add(new TextBlock
            {
                Text = string.IsNullOrEmpty(name) ? it.Value : name, FontSize = 11, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 30
            });
            grid.Children.Add(sp);
            tile.ToolTip = it.Value;
            if (missing) tile.Opacity = .45;
        }
        else
        {
            label = new TextBlock
            {
                Text = Regex.Replace(it.Value, @"\s+", " ").Trim(), FontSize = 12, TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 76, VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(label);
            tile.ToolTip = it.Value.Length > 400 ? it.Value[..400] + "…" : it.Value;
        }

        var x = new Button
        {
            Content = "✕", FontSize = 10, Padding = new Thickness(5, 1, 5, 1), Visibility = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -6, -6, 0)
        };
        x.SetResourceReference(Control.BackgroundProperty, "BgSolidBrush");
        x.ToolTip = Loc.L("Togli", "Remove");
        x.Click += (_, _) => Remove(it);
        grid.Children.Add(x);
        tile.Child = grid;

        tile.MouseEnter += (_, _) => { tile.SetResourceReference(Border.BackgroundProperty, "PanelHoverBrush"); x.Visibility = Visibility.Visible; };
        tile.MouseLeave += (_, _) => { tile.SetResourceReference(Border.BackgroundProperty, "PanelBrush"); x.Visibility = Visibility.Collapsed; };

        // doppio click: apre il file, oppure copia il testo
        // trascinamento: inizia solo dopo qualche pixel, così i click normali restano click
        Point? down = null;
        tile.PreviewMouseLeftButtonDown += (_, e) =>
        {
            down = e.GetPosition(null);
            if (e.ClickCount == 2) { down = null; Activate(it, label); }
        };
        tile.PreviewMouseLeftButtonUp += (_, _) => down = null;
        tile.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || down is not Point d) return;
            var p = e.GetPosition(null);
            if (Math.Abs(p.X - d.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(p.Y - d.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            down = null;
            DragOut(it, tile);
        };
        return tile;
    }

    void Activate(HolderItem it, TextBlock? label)
    {
        try
        {
            if (it.Kind == "file") { Actions.Open(it.Value); return; }
            Clipboard.SetText(it.Value);
            if (label == null) return;
            var old = label.Text;
            label.Text = Loc.L("Copiato! ✓", "Copied! ✓");
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            t.Tick += (_, _) => { t.Stop(); label.Text = old; };
            t.Start();
        }
        catch { }
    }

    // trascina fuori dal holder: l'elemento resta qui (si toglie con la ✕)
    void DragOut(HolderItem it, UIElement source)
    {
        var data = new DataObject();
        if (it.Kind == "file")
        {
            if (!File.Exists(it.Value) && !Directory.Exists(it.Value)) { Remove(it); return; }
            data.SetData(DataFormats.FileDrop, new[] { it.Value });
        }
        else data.SetText(it.Value);

        InternalDrag?.Invoke(true);
        try { DragDrop.DoDragDrop(source, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link); }
        finally { InternalDrag?.Invoke(false); }

        // se chi l'ha ricevuto ha spostato il file, non è più dov'era
        if (it.Kind == "file" && !File.Exists(it.Value) && !Directory.Exists(it.Value)) Remove(it);
    }
}
