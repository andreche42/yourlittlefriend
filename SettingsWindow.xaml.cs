using System.ComponentModel;
using System.Windows;

namespace YourLittleFriend;

public partial class SettingsWindow : Window
{
    public bool LanguageChanged { get; private set; }

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
        BUninstall.Content = Loc.L("Disinstalla yourlittlefriend", "Uninstall yourlittlefriend");
        BCancel.Content = Loc.L("Annulla", "Cancel");
        BSave.Content = Loc.L("Salva", "Save");

        NameIn.Text = st.Name;
        CityIn.Text = st.City;
        RIt.IsChecked = st.Language == "it";
        REn.IsChecked = st.Language == "en";
        AutoStart.IsChecked = Startup.Enabled;
        ModelText.Text = Loc.L($"Modello attuale: {st.Model}", $"Current model: {st.Model}");
        Ai.Finished += m => ModelText.Text = Loc.L($"Modello attuale: {m}", $"Current model: {m}");
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
        base.OnClosing(e);
    }
}
