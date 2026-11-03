using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace YourLittleFriend;

// il microfonino accanto alle caselle di scrittura: preme, parla, il testo compare nel campo.
// il riconoscimento parte solo al clic e si spegne da solo quando lo fermi o quando chiudi il notch.
public partial class MicButton : UserControl
{
    public TextBox? Target { get; set; }

    public MicButton()
    {
        InitializeComponent();
        B.Click += Click_;
        ToolTip = Loc.L("Detta invece di scrivere", "Speak instead of typing");
        B.ToolTip = ToolTip;
        Voice.Stopped += () => Show(false);
        Unloaded += (_, _) => { if (Voice.Listening) Voice.Stop(); };
    }

    void Click_(object s, RoutedEventArgs e)
    {
        if (Voice.Listening) { Voice.Stop(); return; }
        if (Target == null) return;
        if (Voice.Start(Add))
        {
            Show(true);
            return;
        }
        // se il riconoscimento di windows non c'è (manca il pacchetto lingua o il microfono)
        B.ToolTip = Loc.L("Il riconoscimento vocale di Windows non è disponibile: ", "Windows speech recognition isn't available: ")
                    + (Voice.Error ?? "")
                    + Loc.L("\nProva a installare la lingua dalla sezione Lingua di Windows.", "\nTry installing the language from the Language section of Windows.");
        ToolTipService.SetShowDuration(B, 12000);
    }

    // il testo riconosciuto si aggiunge a quello che c'è già
    void Add(string text)
    {
        if (Target == null) return;
        Target.Text = (Target.Text.TrimEnd() + " " + text).TrimStart();
        Target.CaretIndex = Target.Text.Length;
        Target.Focus();
    }

    // acceso: l'icona prende il colore d'accento e pulsa, così si vede che sta ascoltando
    void Show(bool listening)
    {
        Icon.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, listening ? "AccentBrush" : "FgBrush");
        B.ToolTip = listening
            ? Loc.L("Sto ascoltando… premi di nuovo per fermare", "Listening… press again to stop")
            : Loc.L("Detta invece di scrivere", "Speak instead of typing");
        B.BeginAnimation(OpacityProperty, listening
            ? new DoubleAnimation(1, .45, TimeSpan.FromMilliseconds(650)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever }
            : null);
        B.Opacity = 1;
    }
}
