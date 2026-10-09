using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace YourLittleFriend;

public partial class WikiPanel : UserControl
{
    public event Action? SearchStarted;   // il pannello si espande quando cerchi

    string url = "";
    bool busy;

    public WikiPanel()
    {
        InitializeComponent();
        Hint.Text = Loc.L("Cerca su Wikipedia…", "Search Wikipedia…");
        BOpen.Content = Loc.L("Apri su Wikipedia ↗", "Open on Wikipedia ↗");
        Status.Text = Loc.L("Scrivi cosa cercare e premi Invio. Scorri in basso per leggere l'articolo intero.", "Type what to look up and press Enter. Scroll down to read the whole article.");
        Input.TextChanged += (_, _) => Hint.Visibility = Input.Text == "" ? Visibility.Visible : Visibility.Collapsed;
        Input.PreviewKeyDown += async (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            await Search();
        };
    }

    async Task Search()
    {
        var q = Input.Text.Trim();
        if (q == "" || busy) return;
        busy = true;
        SearchStarted?.Invoke();
        Article.Visibility = Visibility.Collapsed;
        Status.Text = Loc.L("Cerco…", "Searching…");
        try
        {
            var page = await Wiki.Lookup(q);
            if (page == null) { Status.Text = Loc.L($"Non ho trovato niente per «{q}».", $"Nothing found for “{q}”."); return; }
            Status.Text = "";
            Title.Text = page.Title;
            Desc.Text = page.Description;
            Desc.Visibility = page.Description == "" ? Visibility.Collapsed : Visibility.Visible;
            Summary.Text = page.Summary;
            Rest.Text = page.Rest;
            Rest.Visibility = page.Rest == "" ? Visibility.Collapsed : Visibility.Visible;
            Photo.Source = page.Photo;
            Gallery.Children.Clear();
            foreach (var g in page.Gallery)
            {
                var img = new Image { Source = g.Image, Stretch = Stretch.UniformToFill };
                var tile = new Border { Width = 172, Height = 118, Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(10), ClipToBounds = true, Cursor = Cursors.Hand, ToolTip = g.Caption, Child = img, Tag = "nodrag" };
                var link = g.Url;
                tile.MouseLeftButtonUp += (_, _) => { try { if (link != "") Actions.Open(link); } catch { } };
                Gallery.Children.Add(tile);
            }
            PhotoBox.Visibility = page.Photo == null ? Visibility.Collapsed : Visibility.Visible;
            url = page.Url;
            Article.Visibility = Visibility.Visible;
            Scroll.ScrollToTop();
        }
        catch { Status.Text = Loc.L("Non riesco a collegarmi a Wikipedia (sei offline?).", "I can't reach Wikipedia (are you offline?)."); }
        finally { busy = false; }
    }

    void Open_Click(object s, RoutedEventArgs e)
    {
        try { if (url != "") Actions.Open(url); } catch { }
    }
}
