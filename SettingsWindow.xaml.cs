using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace YourLittleFriend;

public partial class SettingsWindow : Window
{
    public bool LanguageChanged { get; private set; }

    readonly (string Theme, string Accent, string Mascot, double Opacity, bool Outline) snap;   // per tornare indietro se annulli
    readonly List<(Preset P, Border Tile)> presetTiles = new();
    bool syncing;

    public SettingsWindow()
    {
        InitializeComponent();
        var st = Settings.Current;
        Title = Loc.L("Impostazioni", "Settings");
        HTitle.Text = Title;
        LName.Text = Loc.L("Il tuo nome", "Your name");
        LCity.Text = Loc.L("Città per il meteo (vuoto = nessun meteo)", "City for the weather (empty = no weather)");
        LLang.Text = Loc.L("Lingua (l'app si riavvia)", "Language (the app restarts)");
        LModel.Text = Loc.L("Intelligenza artificiale", "Artificial intelligence");
        BChange.Content = Loc.L("Cambia modello / installa Ollama", "Change model / install Ollama");
        AutoStart.Content = Loc.L("Avvia yourlittlefriend all'avvio del PC", "Start yourlittlefriend when the PC starts");
        VersionText.Text = $"yourlittlefriend {App.Version}";
        BUninstall.Content = Loc.L("Disinstalla yourlittlefriend", "Uninstall yourlittlefriend");
        BCancel.Content = Loc.L("Annulla", "Cancel");
        BSave.Content = Loc.L("Salva", "Save");

        snap = (st.Theme, st.Accent, st.MascotColor, st.Opacity, st.Outline);
        LLook.Text = Loc.L("Aspetto", "Look");
        LAccent.Text = Loc.L("Colore principale", "Accent color");
        LMascot.Text = Loc.L("Colore dell'omino", "Mascot color");
        LOpacity.Text = Loc.L("Trasparenza del notch", "Notch transparency");
        OutlineCb.Content = Loc.L("Contorno colorato attorno al notch", "Colored outline around the notch");
        BResetLook.Content = Loc.L("Ripristina aspetto", "Reset look");
        WebCb.Content = Loc.L("Permetti all'IA di cercare sul web (le domande vanno su DuckDuckGo/Wikipedia)", "Let the AI search the web (queries go to DuckDuckGo/Wikipedia)");
        WebCb.IsChecked = st.WebSearch;
        BuildLook();

        NameIn.Text = st.Name;
        CityIn.Text = st.City;
        RIt.IsChecked = st.Language == "it";
        REn.IsChecked = st.Language == "en";
        AutoStart.IsChecked = Startup.Enabled;
        ModelText.Text = Loc.L($"Modello attuale: {st.Model}", $"Current model: {st.Model}");
        Ai.Finished += m => ModelText.Text = Loc.L($"Modello attuale: {m}", $"Current model: {m}");
    }

    // ---- aspetto: ogni scelta si vede subito, e se annulli torna tutto com'era ----
    void Changed()
    {
        Theme.Apply();
        foreach (var (p, tile) in presetTiles)
        {
            bool on = p.Id == Settings.Current.Theme;
            tile.BorderBrush = Theme.Res(on ? "AccentBrush" : "FgDimBrush");
            tile.BorderThickness = new Thickness(on ? 2.5 : 1);
        }
    }

    Border Swatch(string hex, Action pick)
    {
        var b = new Border
        {
            Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 6, 6), Cursor = Cursors.Hand,
            Background = new SolidColorBrush(Theme.TryParse(hex)!.Value), BorderThickness = new Thickness(1), ToolTip = hex
        };
        b.SetResourceReference(Border.BorderBrushProperty, "FgDimBrush");
        b.MouseLeftButtonUp += (_, _) => pick();
        return b;
    }

    void SyncControls()
    {
        var st = Settings.Current;
        syncing = true;
        AccentHex.Text = st.Accent;
        MascotHex.Text = st.MascotColor;
        OpacitySl.Value = st.Opacity;
        OutlineCb.IsChecked = st.Outline;
        syncing = false;
    }

    void BuildLook()
    {
        var st = Settings.Current;
        foreach (var p in Theme.Presets)
        {
            var bg = Theme.TryParse(p.Bg)!.Value;
            var fg = Theme.TryParse(p.Fg)!.Value;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            row.Children.Add(new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Theme.TryParse(p.Mascot)!.Value), Margin = new Thickness(0, 0, 4, 0) });
            row.Children.Add(new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Theme.TryParse(p.Accent)!.Value) });
            var tile = new Border
            {
                Width = 96, Height = 56, CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(10, 8, 6, 6), Cursor = Cursors.Hand,
                Background = new SolidColorBrush(bg),
                Child = new StackPanel
                {
                    Children = { row, new TextBlock { Text = Loc.L(p.It, p.En), Foreground = new SolidColorBrush(fg), FontSize = 12, FontWeight = FontWeights.SemiBold } }
                }
            };
            tile.MouseLeftButtonUp += (_, _) =>
            {
                st.Theme = p.Id;
                st.Accent = st.MascotColor = "";   // il tema porta i suoi colori
                SyncControls();
                Changed();
            };
            presetTiles.Add((p, tile));
            PresetBox.Children.Add(tile);
        }
        foreach (var hex in Theme.AccentColors) AccentSw.Children.Add(Swatch(hex, () => { st.Accent = hex; SyncControls(); Changed(); }));
        foreach (var hex in Theme.MascotColors) MascotSw.Children.Add(Swatch(hex, () => { st.MascotColor = hex; SyncControls(); Changed(); }));

        SyncControls();
        AccentHex.TextChanged += (_, _) =>
        {
            if (syncing || (AccentHex.Text.Length > 0 && Theme.TryParse(AccentHex.Text) == null)) return;
            st.Accent = AccentHex.Text.Trim();
            Changed();
        };
        MascotHex.TextChanged += (_, _) =>
        {
            if (syncing || (MascotHex.Text.Length > 0 && Theme.TryParse(MascotHex.Text) == null)) return;
            st.MascotColor = MascotHex.Text.Trim();
            Changed();
        };
        OpacitySl.ValueChanged += (_, e) => { if (!syncing) { st.Opacity = e.NewValue; Changed(); } };
        OutlineCb.Click += (_, _) => { st.Outline = OutlineCb.IsChecked == true; Changed(); };
        Changed();
    }

    void ResetLook_Click(object s, RoutedEventArgs e)
    {
        var st = Settings.Current;
        (st.Theme, st.Accent, st.MascotColor, st.Opacity, st.Outline) = ("dark", "", "", 1, false);
        SyncControls();
        Changed();
    }

    void RestoreLook()
    {
        var st = Settings.Current;
        (st.Theme, st.Accent, st.MascotColor, st.Opacity, st.Outline) = snap;
        Theme.Apply();
    }

    void Change_Click(object s, RoutedEventArgs e) => AiBox.Visibility = AiBox.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    async void Save_Click(object s, RoutedEventArgs e)
    {
        var st = Settings.Current;
        var city = CityIn.Text.Trim();
        if (city != st.City)
        {
            if (city == "") { st.City = ""; st.Lat = st.Lon = null; }
            else
            {
                BSave.IsEnabled = false;
                CityStatus.Text = Loc.L("Cerco…", "Searching…");
                var g = await Geo.Find(city);
                BSave.IsEnabled = true;
                if (g == null) { CityStatus.Text = Loc.L("Non trovo questa città (o sei offline).", "I can't find this city (or you're offline)."); return; }
                (st.City, st.Lat, st.Lon) = (g.Value.Name, g.Value.Lat, g.Value.Lon);
            }
        }
        st.Name = NameIn.Text.Trim() is { Length: > 0 } n ? n : Loc.L("amico", "friend");
        var lang = REn.IsChecked == true ? "en" : "it";
        LanguageChanged = lang != st.Language;
        st.Language = lang;
        st.WebSearch = WebCb.IsChecked == true;
        Startup.Set(AutoStart.IsChecked == true);
        st.Save();
        DialogResult = true;
    }

    void Cancel_Click(object s, RoutedEventArgs e) => DialogResult = false;

    void Uninstall_Click(object s, RoutedEventArgs e)
    {
        var ask = MessageBox.Show(
            Loc.L("Vuoi davvero disinstallare yourlittlefriend?\n\nOllama e i modelli scaricati restano sul PC (puoi toglierli da Impostazioni di Windows).",
                  "Do you really want to uninstall yourlittlefriend?\n\nOllama and the downloaded models stay on your PC (you can remove them from Windows Settings)."),
            "yourlittlefriend", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (ask != MessageBoxResult.Yes) return;
        if (!App.Uninstall())
            MessageBox.Show(Loc.L("Questa copia non è stata installata con l'installer, quindi non c'è nulla da disinstallare: basta cancellare la cartella.",
                                  "This copy wasn't installed with the installer, so there's nothing to uninstall: just delete its folder."),
                            "yourlittlefriend", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        Ai.Abort();
        if (DialogResult != true) RestoreLook();   // chiuso senza salvare: l'aspetto torna quello di prima
        base.OnClosing(e);
    }
}
