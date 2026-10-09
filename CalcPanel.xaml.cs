using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace YourLittleFriend;

public record CalcEntry(string Expr, string Result);

// calcolatrice con cronologia (salvata): si scrive a tastiera, i pulsantini inseriscono funzioni e costanti
public partial class CalcPanel : UserControl
{
    public event Action? Easter;   // il risultato è 104, 67, 69 o 420

    const int Max = 60;
    static string FilePath => Path.Combine(Settings.Dir, "calc.json");
    List<CalcEntry> items = new();
    double ans;
    bool quiet;   // evita che la scritta del risultato venga cancellata quando si svuota la casella dopo Invio
    readonly List<Button> degBtns = new();   // i pulsanti DEG/RAD (compatto e tastiera) mostrano lo stesso stato

    public CalcPanel()
    {
        InitializeComponent();
        BClear.ToolTip = Loc.L("Svuota la cronologia", "Clear history");
        try { items = JsonSerializer.Deserialize<List<CalcEntry>>(File.ReadAllText(FilePath)) ?? new(); } catch { }
        if (items.Count > 0 && double.TryParse(items[0].Result, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var last)) ans = last;

        foreach (var (label, text) in new[] { ("π", "π"), ("√", "√("), ("^", "^"), ("(", "("), (")", ")"), ("%", "%"), ("!", "!"), ("ans", "ans") })
            Chips.Children.Add(Chip(label, () => Insert(text)));
        var degChip = Chip("", ToggleDeg);
        degChip.ToolTip = Loc.L("Gradi o radianti per seno, coseno e tangente", "Degrees or radians for sin, cos and tan");
        degBtns.Add(degChip);
        Chips.Children.Add(degChip);
        BuildKeypad();
        ShowDeg();
        SizeChanged += (_, _) => AdaptLayout();

        Input.TextChanged += (_, _) => Preview();
        Input.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { Commit(); e.Handled = true; }
            else if (e.Key == Key.Escape) { Input.Clear(); e.Handled = true; }
        };
        RenderHistory();
        Result.Foreground = Theme.Res("FgDimBrush");
    }

    Button Chip(string label, Action click)
    {
        var b = new Button { Content = label, FontSize = 12, Padding = new Thickness(7, 1, 7, 1), Margin = new Thickness(0, 0, 3, 3), Focusable = false };
        b.SetResourceReference(Control.BackgroundProperty, "PanelBrush");
        b.Click += (_, _) => click();
        return b;
    }

    void ToggleDeg()
    {
        Settings.Current.CalcDeg = !Settings.Current.CalcDeg;
        Settings.Current.Save();
        ShowDeg();
        Preview();
    }

    void ShowDeg() { foreach (var b in degBtns) b.Content = Settings.Current.CalcDeg ? "DEG" : "RAD"; }

    // pannello grande = tastiera completa e risultato più grande; piccolo = pulsantini compatti
    void AdaptLayout()
    {
        bool big = ActualHeight >= 230 && ActualWidth >= 520;
        Keypad.Visibility = big ? Visibility.Visible : Visibility.Collapsed;
        Chips.Visibility = big ? Visibility.Collapsed : Visibility.Visible;
        LeftCol.Width = new GridLength(big ? Math.Min(430, ActualWidth * .6) : 285);
        Result.FontSize = big ? 34 : 25;
        Result.Height = big ? 50 : 34;
    }

    // tastiera: 3 colonne scientifiche, 3 di cifre, 1 di operazioni
    void BuildKeypad()
    {
        string[][] rows =
        {
            new[] { "(", ")", "^", "C", "⌫", "%", "÷" },
            new[] { "√", "π", "!", "7", "8", "9", "×" },
            new[] { "sin", "cos", "tan", "4", "5", "6", "−" },
            new[] { "ln", "log", "ans", "1", "2", "3", "+" },
            new[] { "DEG", "e", "x²", "±", "0", ".", "=" },
        };
        foreach (var row in rows)
            foreach (var k in row)
            {
                var key = k;
                var b = new Button { Content = key, Style = (Style)FindResource(key == "=" ? "Primary" : "Key") };
                if (key == "=") b.Margin = new Thickness(2);
                if (key is "÷" or "×" or "−" or "+") b.SetResourceReference(Control.BackgroundProperty, "PanelHoverBrush");
                b.Click += (_, _) => Press(key);
                if (key == "DEG") degBtns.Add(b);
                Keypad.Children.Add(b);
            }
    }

    // cosa fa ogni tasto
    void Press(string key)
    {
        switch (key)
        {
            case "=": Commit(); break;
            case "C": Input.Clear(); Result.Text = ""; Input.Focus(); break;
            case "⌫":
                if (Input.SelectionLength > 0) Input.SelectedText = "";
                else if (Input.CaretIndex > 0) { int i = Input.CaretIndex; Input.Text = Input.Text.Remove(i - 1, 1); Input.CaretIndex = i - 1; }
                Input.Focus();
                break;
            case "±":
                Input.Text = Input.Text.StartsWith('-') ? Input.Text[1..] : "-" + Input.Text;
                Input.CaretIndex = Input.Text.Length;
                Input.Focus();
                break;
            case "DEG": ToggleDeg(); break;
            case "x²": Insert("^2"); break;
            case "√": Insert("√("); break;
            case "sin" or "cos" or "tan" or "ln" or "log": Insert(key + "("); break;
            default: Insert(key); break;
        }
    }

    void Insert(string text)
    {
        Input.SelectedText = text;
        Input.CaretIndex = Input.SelectionStart + Input.SelectionLength;
        Input.Focus();
    }

    // risultato provvisorio mentre scrivi
    void Preview()
    {
        if (quiet) return;
        Result.SetResourceReference(TextBlock.ForegroundProperty, "FgDimBrush");
        var t = Input.Text.Trim();
        if (t == "") { Result.Text = ""; return; }
        try { Result.Text = "= " + Calc.Format(Calc.Eval(t, ans, Settings.Current.CalcDeg)); }
        catch { Result.Text = ""; }
    }

    void Commit()
    {
        var t = Input.Text.Trim();
        if (t == "") return;
        try
        {
            double v = Calc.Eval(t, ans, Settings.Current.CalcDeg);
            var r = Calc.Format(v);
            ans = v;
            items.Insert(0, new CalcEntry(t, r));
            if (items.Count > Max) items.RemoveRange(Max, items.Count - Max);
            try { Directory.CreateDirectory(Settings.Dir); File.WriteAllText(FilePath, JsonSerializer.Serialize(items)); } catch { }
            RenderHistory();
            quiet = true;
            Input.Clear();
            quiet = false;
            Result.Text = "= " + r;
            Result.SetResourceReference(TextBlock.ForegroundProperty, "FgBrush");
            if (Calc.IsSpecial(v)) Easter?.Invoke();
        }
        catch
        {
            Result.Text = Loc.L("Non ho capito", "Can't compute");
            Result.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x7B, 0x7B));
        }
    }

    void RenderHistory()
    {
        History.Children.Clear();
        if (items.Count == 0)
            History.Children.Add(new TextBlock { Text = Loc.L("La cronologia è vuota.\nProva: 2(3+4)^2, sqrt(81), 5!", "History is empty.\nTry: 2(3+4)^2, sqrt(81), 5!"), Foreground = Theme.Res("FgDimBrush"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        foreach (var it in items)
        {
            var row = new Border { Padding = new Thickness(6, 2, 6, 2), CornerRadius = new CornerRadius(8), Cursor = Cursors.Hand, Background = Brushes.Transparent, Margin = new Thickness(0, 1, 0, 1), Tag = "nodrag" };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = it.Expr, FontSize = 11, Foreground = Theme.Res("FgDimBrush"), TextTrimming = TextTrimming.CharacterEllipsis });
            sp.Children.Add(new TextBlock { Text = "= " + it.Result, FontSize = 15, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            row.Child = sp;
            row.ToolTip = Loc.L("Clic: inserisce il risultato · doppio clic: riprende l'espressione", "Click: insert the result · double click: reuse the expression");
            row.MouseEnter += (_, _) => row.SetResourceReference(Border.BackgroundProperty, "PanelBrush");
            row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
            row.MouseLeftButtonUp += (_, e) =>
            {
                if (e.ClickCount >= 2) { Input.Text = it.Expr; Input.CaretIndex = Input.Text.Length; Input.Focus(); }
                else Insert(it.Result);
            };
            History.Children.Add(row);
        }
    }

    void Clear_Click(object s, RoutedEventArgs e)
    {
        items.Clear();
        try { File.Delete(FilePath); } catch { }
        RenderHistory();
    }
}
