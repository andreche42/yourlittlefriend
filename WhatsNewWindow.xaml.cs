using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YourLittleFriend;

// "cosa c'è di nuovo": le note della release appena installata, in una sola lingua (quella scelta).
// si apre da sola la prima volta che l'app parte dopo un aggiornamento (vedi App.WhatsNew).
public partial class WhatsNewWindow : Window
{
    public WhatsNewWindow(string version, string notes)
    {
        InitializeComponent();
        Heading.Text = Loc.L("Cosa c'è di nuovo", "What's new");
        Sub.Text = Loc.L($"Versione {version}", $"Version {version}");
        BOk.Content = Loc.L("Perfetto!", "Nice!");
        Render(notes);
    }

    void Ok_Click(object s, RoutedEventArgs e) => Close();

    // le note sono markdown leggero: titoli, elenchi, **grassetto**
    void Render(string notes)
    {
        foreach (var raw in notes.Split('\n'))
        {
            var t = raw.Trim();
            if (t.Length == 0 || t.All(c => c is '-' or '*' or '_')) continue;   // righe vuote e divisorie

            if (t.StartsWith('#'))
                Notes.Children.Add(Line(Plain(t.TrimStart('#').Trim()), 15.5, FontWeights.Bold, 14));
            else if (t.StartsWith("- ") || t.StartsWith("* ") || t.StartsWith("+ "))
                Notes.Children.Add(Bullet(Plain(t[2..].Trim())));
            else
                Notes.Children.Add(Line(Plain(t), 13.5, FontWeights.Normal, 6));
        }
    }

    static TextBlock Line(string text, double size, FontWeight weight, double top) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap, FontSize = size, FontWeight = weight,
        Foreground = Theme.Res("FgBrush"), Margin = new Thickness(0, top, 0, 0), LineHeight = size * 1.45
    };

    // elenco: il pallino sta fuori e il testo va a capo rientrato
    static Grid Bullet(string text)
    {
        var g = new Grid { Margin = new Thickness(0, 5, 0, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        var dot = new TextBlock
        {
            Text = "•", FontSize = 13.5, Foreground = Theme.Res("AccentBrush"), Margin = new Thickness(2, 0, 9, 0)
        };
        var body = Line(text, 13.5, FontWeights.Normal, 0);
        Grid.SetColumn(body, 1);
        g.Children.Add(dot);
        g.Children.Add(body);
        return g;
    }

    // via i segni del markdown: le note si leggono come testo normale
    static string Plain(string s)
    {
        s = Regex.Replace(s, @"\*\*(.+?)\*\*", "$1");
        s = Regex.Replace(s, @"__(.+?)__", "$1");
        s = Regex.Replace(s, @"\[(.+?)\]\((.+?)\)", "$1");
        s = Regex.Replace(s, @"`(.+?)`", "$1");
        s = Regex.Replace(s, @"(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*", "$1");
        return s.Trim();
    }
}
