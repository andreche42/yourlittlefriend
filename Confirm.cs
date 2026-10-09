using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YourLittleFriend;

// popup di conferma per le azioni sensibili: i passaggi li genera il codice, non il modello
public static class Confirm
{
    public static bool Ask(Plan p)
    {
        var w = new Window
        {
            Title = Actions.AppName, Width = 420, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen, Topmost = true, ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(17, 17, 17))
        };
        var sp = new StackPanel { Margin = new Thickness(20) };
        TextBlock T(string s, double size, Brush b, FontWeight? fw = null) => new()
        {
            Text = s, FontSize = size, Foreground = b, TextWrapping = TextWrapping.Wrap,
            FontWeight = fw ?? FontWeights.Normal, Margin = new Thickness(0, 0, 0, 8)
        };
        sp.Children.Add(T(Loc.L($"Vuoi che {Actions.AppName} faccia questo?", $"Do you want {Actions.AppName} to do this?"), 18, Brushes.White, FontWeights.Bold));
        sp.Children.Add(T(p.Title, 15, Brushes.White));
        for (int i = 0; i < p.Steps.Length; i++) sp.Children.Add(T($"{i + 1}. {p.Steps[i]}", 13, Brushes.LightGray));
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        Button B(string s, bool ok)
        {
            var b = new Button
            {
                Content = s, Width = 90, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(0, 6, 0, 6),
                Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B)), Foreground = Brushes.White
            };
            b.Click += (_, _) => w.DialogResult = ok;
            return b;
        }
        row.Children.Add(B("No", false));
        row.Children.Add(B(Loc.L("Sì, fai", "Yes, do it"), true));
        sp.Children.Add(row);
        w.Content = sp;
        return w.ShowDialog() == true;
    }
}
