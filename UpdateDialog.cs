using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace YourLittleFriend;

// le due finestrelle dell'aggiornamento: la domanda "lo faccio?" e la barra "torno tra poco".
// sono costruite qui, come il popup di conferma delle azioni, e usano i colori scelti dall'utente.
public static class UpdateAsk
{
    static Window Shell(double width) => new()
    {
        Title = Loc.L("Aggiornamento", "Update"),
        Width = width, SizeToContent = SizeToContent.Height,
        WindowStartupLocation = WindowStartupLocation.CenterScreen, Topmost = true, ResizeMode = ResizeMode.NoResize,
        Background = Theme.Res("WindowBrush")
    };

    static TextBlock Text(string s, double size, Brush b, FontWeight? fw = null) => new()
    {
        Text = s, FontSize = size, Foreground = b, TextWrapping = TextWrapping.Wrap,
        FontWeight = fw ?? FontWeights.Normal, Margin = new Thickness(0, 0, 0, 8)
    };

    static Button Btn(string s, bool main, Window w, bool ok)
    {
        var b = new Button
        {
            Content = s, MinWidth = 96, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(12, 6, 12, 6),
            Background = Theme.Res(main ? "AccentBrush" : "PanelBrush"),
            Foreground = Theme.Res(main ? "AccentFgBrush" : "FgBrush")
        };
        b.Click += (_, _) => w.DialogResult = ok;
        return b;
    }

    // true = aggiorna adesso
    public static bool Ask(Update.Info info)
    {
        var w = Shell(430);
        var sp = new StackPanel { Margin = new Thickness(22, 20, 22, 20) };
        sp.Children.Add(Text(Loc.L("C'è un aggiornamento", "There's an update"), 19, Theme.Res("FgBrush"), FontWeights.Bold));
        sp.Children.Add(Text(Loc.L($"La versione {info.Version} è pronta: tu hai la {App.Version}.",
                                   $"Version {info.Version} is ready: you have {App.Version}."), 15, Theme.Res("FgBrush")));
        sp.Children.Add(Text(Loc.L("Se dici sì si chiude, si aggiorna e torna da sola. Impostazioni e portafile restano com'erano.",
                                   "If you say yes it closes, updates itself and comes back. Settings and the file holder stay as they are."), 12, Theme.Res("FgDimBrush")));
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        row.Children.Add(Btn(Loc.L("Dopo", "Later"), false, w, false));
        row.Children.Add(Btn(Loc.L("Aggiorna", "Update"), true, w, true));
        sp.Children.Add(row);
        w.Content = sp;
        return w.ShowDialog() == true;
    }

    // un avviso con un solo pulsante (aggiornamento non riuscito, copia non installata, ...)
    public static void Warn(string message)
    {
        var w = Shell(430);
        var sp = new StackPanel { Margin = new Thickness(22, 20, 22, 20) };
        sp.Children.Add(Text(Loc.L("Aggiornamento", "Update"), 18, Theme.Res("FgBrush"), FontWeights.Bold));
        sp.Children.Add(Text(message, 14, Theme.Res("FgBrush")));
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
        row.Children.Add(Btn("Ok", true, w, true));
        sp.Children.Add(row);
        w.Content = sp;
        w.ShowDialog();
    }
}

// "aggiornamento, torno tra poco!": la barra si riempie durante lo scaricamento, col colore scelto dall'utente
public sealed class UpdateProgress : IDisposable
{
    const double Bar = 300;   // larghezza della barra
    readonly Window w;
    readonly Border fill;
    readonly TextBlock state;
    bool moving;   // barra che va avanti e indietro (quando la lunghezza del file non si sa)

    public UpdateProgress()
    {
        fill = new Border
        {
            Width = 0, Height = 8, CornerRadius = new CornerRadius(4),
            Background = Theme.Res("AccentBrush"), HorizontalAlignment = HorizontalAlignment.Left
        };
        var track = new Border
        {
            Width = Bar, Height = 8, CornerRadius = new CornerRadius(4), Background = Theme.Res("PanelBrush"),
            HorizontalAlignment = HorizontalAlignment.Left, Child = fill
        };
        state = new TextBlock
        {
            Text = Loc.L("Scarico l'aggiornamento…", "Downloading the update…"),
            FontSize = 12, Foreground = Theme.Res("FgDimBrush"), Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap
        };
        var sp = new StackPanel { Margin = new Thickness(22, 18, 22, 20) };
        sp.Children.Add(new TextBlock
        {
            Text = Loc.L("Aggiornamento, torno tra poco!", "Updating, back in a moment!"),
            FontSize = 17, FontWeight = FontWeights.Bold, Foreground = Theme.Res("FgBrush"), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });
        sp.Children.Add(track);
        sp.Children.Add(state);
        var card = new Border
        {
            Background = Theme.Res("WindowBrush"), CornerRadius = new CornerRadius(18), Margin = new Thickness(10),
            BorderBrush = Theme.Res("AccentBrush"), BorderThickness = new Thickness(1.5), Child = sp
        };
        w = new Window
        {
            WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent,
            Width = Bar + 64, SizeToContent = SizeToContent.Height, Topmost = true, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen, ResizeMode = ResizeMode.NoResize,
            Content = card
        };
    }

    public void Show() => w.Show();

    // t: 0..1 (oppure -1 se non si sa quanto manca). text: cosa scrivere sotto la barra
    public void Report(double t, string? text = null)
    {
        if (t < 0)
        {
            state.Text = text ?? Loc.L("Scarico l'aggiornamento…", "Downloading the update…");
            if (moving) return;
            moving = true;
            var a = new DoubleAnimation(Bar * .18, Bar * .55, TimeSpan.FromMilliseconds(900))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
            fill.BeginAnimation(FrameworkElement.WidthProperty, a);
            return;
        }
        moving = false;
        fill.BeginAnimation(FrameworkElement.WidthProperty, null);
        t = Math.Clamp(t, 0, 1);
        fill.Width = Bar * t;
        state.Text = text ?? $"{Math.Round(t * 100)}%";
    }

    public void Close()
    {
        try { w.Close(); } catch { }
    }

    public void Dispose() => Close();
}
