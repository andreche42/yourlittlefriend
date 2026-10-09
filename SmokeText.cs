using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace YourLittleFriend;

// testo che arriva a pezzi: ogni parola compare come fumo (sfocata e trasparente, sale e si definisce)
public class SmokeText : WrapPanel
{
    readonly StringBuilder buf = new();
    bool placeholder;
    int burst;

    // testo fisso, senza animazione (es. "…" o messaggi di errore)
    public void Show(string text)
    {
        Children.Clear();
        buf.Clear();
        placeholder = true;
        Children.Add(new TextBlock { Text = text });
    }

    // aggiunge un pezzo di testo: le parole complete compaiono subito, l'ultima aspetta lo spazio (o Flush)
    public void Append(string chunk)
    {
        if (placeholder) { Children.Clear(); placeholder = false; }
        burst = 0;
        foreach (var ch in chunk)
        {
            if (ch == '\n') { Flush(); Children.Add(new Border { Width = Math.Max(ActualWidth, 1), Height = 0 }); }
            else if (char.IsWhiteSpace(ch)) Flush();
            else buf.Append(ch);
        }
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => (Parent as ScrollViewer)?.ScrollToEnd()));
    }

    // fa comparire l'ultima parola rimasta in attesa
    public void Flush()
    {
        if (buf.Length == 0) return;
        AddWord(buf.ToString());
        buf.Clear();
    }

    void AddWord(string word)
    {
        var blur = new BlurEffect { Radius = 8 };
        var lift = new TranslateTransform(0, 5);
        var tb = new TextBlock { Text = word + " ", Opacity = 0, Effect = blur, RenderTransform = lift };
        Children.Add(tb);

        var dur = TimeSpan.FromMilliseconds(700);
        var delay = TimeSpan.FromMilliseconds(Math.Min(burst++, 30) * 45);   // se arrivano tante parole insieme, escono una dopo l'altra
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var fade = new DoubleAnimation(0, 1, dur) { BeginTime = delay, EasingFunction = ease };
        var clear = new DoubleAnimation(8, 0, dur) { BeginTime = delay, EasingFunction = ease };
        clear.Completed += (_, _) => tb.Effect = null;   // a fine animazione l'effetto non serve più
        tb.BeginAnimation(OpacityProperty, fade);
        blur.BeginAnimation(BlurEffect.RadiusProperty, clear);
        lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(5, 0, dur) { BeginTime = delay, EasingFunction = ease });
    }
}
