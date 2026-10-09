using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace YourLittleFriend;

public partial class OllamaStatusPanel : UserControl
{
    static readonly Brush Green = new SolidColorBrush(Color.FromRgb(0x7A, 0xE0, 0x8A));
    static readonly Brush Yellow = new SolidColorBrush(Color.FromRgb(0xF2, 0xC9, 0x4C));
    static readonly Brush Red = new SolidColorBrush(Color.FromRgb(0xFF, 0x7B, 0x7B));
    static readonly Brush Gray = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));

    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    bool busy, refreshing;   // busy: c'è un'operazione in corso (avvio, stop, caricamento): l'aggiornamento automatico non tocca lo stato

    public OllamaStatusPanel()
    {
        InitializeComponent();
        Title.Text = "Ollama";
        BStart.Content = Loc.L("Avvia", "Start");
        BStop.Content = Loc.L("Ferma", "Stop");
        BLoad.Content = Loc.L("Carica in memoria", "Load into memory");
        ActiveTitle.Text = Loc.L("Modelli attivi in memoria", "Models active in memory");
        timer.Tick += async (_, _) => await Refresh();
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue) { timer.Start(); _ = Refresh(); }
            else timer.Stop();
        };
        Unloaded += (_, _) => timer.Stop();
    }

    static bool Same(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase)
        || string.Equals(a, b + ":latest", StringComparison.OrdinalIgnoreCase)
        || string.Equals(a + ":latest", b, StringComparison.OrdinalIgnoreCase);

    static string Gb(long bytes) => $"{bytes / 1e9:0.#} GB";

    void SetState(Brush dot, string text) { Dot.Foreground = dot; StateText.Text = text; }

    async Task Refresh()
    {
        if (busy || refreshing) return;
        refreshing = true;
        try
        {
            bool installed = Ollama.IsInstalled, up = await Ollama.IsUp();
            var running = new List<(string Name, long Size, long Vram)>();
            bool have = false;
            var model = Settings.Current.Model;
            if (up)
            {
                try { running = await Ollama.Running(); } catch { }
                try { have = (await Ollama.ListModels()).Any(m => Same(m.Name, model)); } catch { }
            }
            if (busy) return;   // nel frattempo è partita un'operazione

            Bar.Visibility = Visibility.Collapsed;
            BStart.IsEnabled = installed && !up;
            BStop.IsEnabled = up;
            if (up) SetState(Green, Loc.L("In esecuzione", "Running"));
            else if (installed) SetState(Red, Loc.L("Spento", "Stopped"));
            else SetState(Gray, Loc.L("Non installato (installalo da «Cambia modello / installa Ollama»)", "Not installed (install it from “Change model / install Ollama”)"));

            ModelLine.Text = Loc.L($"Modello in uso: {model}", $"Model in use: {model}");
            var loaded = running.FirstOrDefault(r => Same(r.Name, model));
            if (!up) { MDot.Foreground = Gray; MState.Text = "—"; }
            else if (!have) { MDot.Foreground = Red; MState.Text = Loc.L("Non installato: scaricalo da «Cambia modello»", "Not installed: download it from “Change model”"); }
            else if (loaded.Name != null) { MDot.Foreground = Green; MState.Text = Loc.L($"In memoria ({Gb(loaded.Size)}): risponde subito", $"Loaded in memory ({Gb(loaded.Size)}): answers right away"); }
            else { MDot.Foreground = Yellow; MState.Text = Loc.L("Installato, non in memoria: si carica alla prima domanda (qualche secondo)", "Installed, not in memory: loads on the first question (a few seconds)"); }
            BLoad.IsEnabled = up && have && loaded.Name == null;

            ActiveList.Children.Clear();
            if (running.Count == 0)
                ActiveList.Children.Add(new TextBlock { Text = Loc.L("Nessuno", "None"), Foreground = Theme.Res("FgDimBrush") });
            foreach (var r in running)
            {
                var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
                var eject = new Button { Content = "⏏", FontSize = 13, Padding = new Thickness(8, 2, 8, 2), ToolTip = Loc.L("Scarica dalla memoria", "Unload from memory") };
                eject.Click += async (_, _) => await Run(Loc.L("Scarico il modello…", "Unloading the model…"), () => Ollama.Unload(r.Name));
                DockPanel.SetDock(eject, Dock.Right);
                row.Children.Add(eject);
                var gpu = r.Size > 0 ? $" · GPU {Math.Round(100.0 * r.Vram / r.Size)}%" : "";
                row.Children.Add(new TextBlock { Text = $"{r.Name} · {Gb(r.Size)}{gpu}", VerticalAlignment = VerticalAlignment.Center });
                ActiveList.Children.Add(row);
            }
        }
        catch { }
        finally { refreshing = false; }
    }

    // esegue un'operazione lunga mostrando la barra di progresso e bloccando i pulsanti
    async Task Run(string text, Func<Task> work)
    {
        busy = true;
        Msg.Text = "";
        SetState(Yellow, text);
        Bar.Visibility = Visibility.Visible;
        BStart.IsEnabled = BStop.IsEnabled = BLoad.IsEnabled = false;
        try { await work(); }
        catch (Exception ex) { Msg.Text = Loc.L("Errore: ", "Error: ") + ex.Message; }
        finally { busy = false; }
        await Refresh();
    }

    async void Start_Click(object s, RoutedEventArgs e) =>
        await Run(Loc.L("Avvio Ollama…", "Starting Ollama…"), async () =>
        {
            if (!await Ollama.StartIfInstalled()) throw new Exception(Loc.L("Ollama non si avvia", "Ollama won't start"));
        });

    async void Stop_Click(object s, RoutedEventArgs e) =>
        await Run(Loc.L("Fermo Ollama…", "Stopping Ollama…"), Ollama.Stop);

    async void Load_Click(object s, RoutedEventArgs e)
    {
        var model = Settings.Current.Model;
        await Run(Loc.L($"Carico {model} in memoria…", $"Loading {model} into memory…"), () => Ollama.Load(model));
    }
}
