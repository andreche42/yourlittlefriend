using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YourLittleFriend;

public partial class AiSetupPanel : UserControl
{
    public event Action<string>? Finished;   // modello installato
    CancellationTokenSource? cts;

    readonly HashSet<string> installedNames = new(StringComparer.OrdinalIgnoreCase);   // modelli già presenti in ollama
    bool detecting, detected, serverUp;

    string Model =>
        RCus.IsChecked == true ? CusIn.Text.Trim()
        : RDef.IsChecked == true ? Llm.DefaultModel
        : Installed.Children.OfType<RadioButton>().FirstOrDefault(r => r.IsChecked == true)?.Tag as string ?? Llm.DefaultModel;

    bool IsInstalled(string m) => installedNames.Contains(m) || installedNames.Contains(m + ":latest");

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
        OllamaStatus.Text = Loc.L("Controllo se Ollama è già installato…", "Checking whether Ollama is already installed…");
        IsVisibleChanged += (_, e) => { if ((bool)e.NewValue && !detected) Detect(); };
        Refresh();
    }

    // all'apertura: Ollama c'è già? quali modelli ha? così si evita di scaricare di nuovo quello che c'è
    async void Detect()
    {
        detected = detecting = true;
        Refresh();
        try
        {
            if (!Ollama.IsInstalled && !await Ollama.IsUp())
            {
                OllamaStatus.Text = Loc.L("Ollama non è installato: lo scarico e lo installo io.", "Ollama isn't installed: I'll download and install it for you.");
                return;
            }
            OllamaStatus.Text = Loc.L("✓ Ollama è già installato.", "✓ Ollama is already installed.");
            serverUp = await Ollama.StartIfInstalled();
            if (!serverUp) return;
            var models = await Ollama.ListModels();
            foreach (var (name, size) in models)
            {
                installedNames.Add(name);
                var r = new RadioButton { GroupName = "model", Tag = name, Margin = new Thickness(0, 2, 0, 2), Content = $"{name}  ({size / 1e9:0.#} GB)" };
                r.SetResourceReference(Control.ForegroundProperty, "FgBrush");
                r.Checked += (_, _) => Refresh();
                Installed.Children.Add(r);
            }
            if (models.Count == 0) return;

            InstHeader.Text = Loc.L("Modelli già installati (si usano subito, senza scaricare):", "Models already installed (used right away, no download):");
            InstHeader.Visibility = Visibility.Visible;
            if (IsInstalled(Llm.DefaultModel)) RDef.Content += Loc.L("  ✓ già installato", "  ✓ already installed");

            // si propone quello che usi già, altrimenti il consigliato se c'è, altrimenti il primo che c'è
            var current = Settings.Current.Model;
            var pick = Installed.Children.OfType<RadioButton>().FirstOrDefault(r => string.Equals((string)r.Tag, current, StringComparison.OrdinalIgnoreCase));
            if (pick == null && !IsInstalled(Llm.DefaultModel)) pick = Installed.Children.OfType<RadioButton>().First();
            if (pick != null) pick.IsChecked = true;
        }
        catch { }   // se qualcosa non va si può comunque procedere con l'installazione normale
        finally { detecting = false; Refresh(); }
    }

    // verifica se il modello scelto va bene per questo pc
    void Refresh()
    {
        CusIn.IsEnabled = RCus.IsChecked == true;
        var m = Model;
        if (m == "") { Verdict.Text = ""; Go.IsEnabled = false; return; }
        bool have = serverUp && IsInstalled(m);
        Go.Content = have ? Loc.L("Usa questo modello", "Use this model") : Loc.L("Scarica e installa", "Download and install");
        if (have)
        {
            Verdict.Text = Loc.L("✓ Già installato: nessun download.", "✓ Already installed: no download needed.");
            Verdict.Foreground = new SolidColorBrush(Color.FromRgb(0x7A, 0xE0, 0x8A));
            Go.IsEnabled = !detecting;
            return;
        }
        var (fit, msg) = Ollama.Check(m);
        Verdict.Text = msg;
        Verdict.Foreground = new SolidColorBrush(fit switch
        {
            Fit.Good => Color.FromRgb(0x7A, 0xE0, 0x8A),
            Fit.Tight or Fit.Unknown => Color.FromRgb(0xF2, 0xC9, 0x4C),
            _ => Color.FromRgb(0xFF, 0x7B, 0x7B)
        });
        Go.IsEnabled = !detecting && fit != Fit.NoDisk;
    }

    void SetBusy(bool busy)
    {
        Go.IsEnabled = !busy && !detecting;
        foreach (var r in Installed.Children.OfType<RadioButton>()) r.IsEnabled = !busy;
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
        // già installato: niente da scaricare, si salva e basta
        if (serverUp && IsInstalled(model))
        {
            Settings.Current.Model = model;
            Settings.Current.Save();
            Status.Text = Loc.L("Fatto! Uso il modello già installato.", "Done! Using the model that's already installed.");
            Finished?.Invoke(model);
            return;
        }
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
