using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YourLittleFriend;

public partial class AiSetupPanel : UserControl
{
    public event Action<string>? Finished;   // modello installato
    CancellationTokenSource? cts;

    string Model => RCus.IsChecked == true ? CusIn.Text.Trim() : Llm.DefaultModel;

    public AiSetupPanel()
    {
        InitializeComponent();
        Intro.Text = Loc.L("Per chattare uso un'IA che gira sul tuo PC (Ollama). Scarico e installo tutto io!",
                           "To chat I use an AI that runs on your PC (Ollama). I'll download and install everything!");
        RDef.Content = Loc.L($"Consigliato: {Llm.DefaultModel} (circa 3 GB, leggero)", $"Recommended: {Llm.DefaultModel} (about 3 GB, lightweight)");
        RCus.Content = Loc.L("Scelgo io un modello", "I'll pick a model");
        Hint.Text = Loc.L("Scrivi il nome come su ollama.com/library, per esempio qwen3.5:8b", "Type the name as on ollama.com/library, e.g. qwen3.5:8b");
        Go.Content = Loc.L("Scarica e installa", "Download and install");
        Cancel.Content = Loc.L("Annulla", "Cancel");
        RDef.Checked += (_, _) => Refresh();
        RCus.Checked += (_, _) => Refresh();
        CusIn.TextChanged += (_, _) => Refresh();
        Refresh();
    }

    // verifica se il modello scelto va bene per questo pc
    void Refresh()
    {
        CusIn.IsEnabled = RCus.IsChecked == true;
        var m = Model;
        if (m == "") { Verdict.Text = ""; Go.IsEnabled = false; return; }
        var (fit, msg) = Ollama.Check(m);
        Verdict.Text = msg;
        Verdict.Foreground = new SolidColorBrush(fit switch
        {
            Fit.Good => Color.FromRgb(0x7A, 0xE0, 0x8A),
            Fit.Tight or Fit.Unknown => Color.FromRgb(0xF2, 0xC9, 0x4C),
            _ => Color.FromRgb(0xFF, 0x7B, 0x7B)
        });
        Go.IsEnabled = fit != Fit.NoDisk;
    }

    void SetBusy(bool busy)
    {
        Go.IsEnabled = !busy;
        Cancel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        Bar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        RDef.IsEnabled = RCus.IsEnabled = !busy;
        CusIn.IsEnabled = !busy && RCus.IsChecked == true;
        if (!busy) Refresh();
    }

    void Report(string text, double p)
    {
        Status.Text = p >= 0 ? $"{text} {p:P0}" : text;
        Bar.IsIndeterminate = p < 0;
        if (p >= 0) Bar.Value = p;
    }

    public void Abort() => cts?.Cancel();

    async void Go_Click(object s, RoutedEventArgs e)
    {
        var model = Model;
        if (model == "") return;
        cts = new CancellationTokenSource();
        SetBusy(true);
        Status.Text = "";
        try
        {
            await Ollama.EnsureInstalled(Report, cts.Token);
            await Ollama.Pull(model, Report, cts.Token);
            Settings.Current.Model = model;
            Settings.Current.Save();
            Status.Text = Loc.L("Fatto! Il tuo amico è pronto a chiacchierare.", "Done! Your friend is ready to chat.");
            SetBusy(false);
            Finished?.Invoke(model);
        }
        catch (OperationCanceledException) { Status.Text = Loc.L("Annullato.", "Cancelled."); SetBusy(false); }
        catch (Exception ex) { Status.Text = Loc.L("Errore: ", "Error: ") + ex.Message; SetBusy(false); }
    }

    void Cancel_Click(object s, RoutedEventArgs e) => Abort();
}
