using System.ComponentModel;
using System.Windows;

namespace YourLittleFriend;

// primo avvio: nome, città (si può saltare) e installazione guidata di ollama + modello
public partial class OnboardingWindow : Window
{
    int page;

    public OnboardingWindow()
    {
        InitializeComponent();
        T0.Text = Loc.L("Ciao! Sono il tuo piccolo amico che vive nel notch del PC. Come ti chiami?", "Hi! I'm your little friend living in your PC's notch. What's your name?");
        T1.Text = Loc.L("Dove vivi? Mi serve per dirti che tempo fa. Puoi anche saltare questo passaggio.", "Where do you live? I need it to tell you the weather. You can skip this step.");
        Ai.Finished += _ => GoTo(3);
        GoTo(0);
        Loaded += (_, _) => NameIn.Focus();
    }

    void GoTo(int p)
    {
        page = p;
        P0.Visibility = p == 0 ? Visibility.Visible : Visibility.Collapsed;
        P1.Visibility = p == 1 ? Visibility.Visible : Visibility.Collapsed;
        P2.Visibility = p == 2 ? Visibility.Visible : Visibility.Collapsed;
        P3.Visibility = p == 3 ? Visibility.Visible : Visibility.Collapsed;
        BNext.Visibility = p == 2 ? Visibility.Collapsed : Visibility.Visible;
        BSkip.Visibility = p is 1 or 2 ? Visibility.Visible : Visibility.Collapsed;
        switch (p)
        {
            case 0: Heading.Text = Loc.L("Benvenuto!", "Welcome!"); BNext.Content = Loc.L("Avanti", "Next"); break;
            case 1: Heading.Text = Loc.L("Il meteo", "The weather"); BNext.Content = Loc.L("Avanti", "Next"); BSkip.Content = Loc.L("Salta", "Skip"); CityIn.Focus(); break;
            case 2: Heading.Text = Loc.L("Il mio cervello", "My brain"); BSkip.Content = Loc.L("Salta per ora", "Skip for now"); break;
            case 3:
                Heading.Text = Loc.L("Tutto pronto!", "All set!");
                T3.Text = Loc.L($"Ecco fatto, {Settings.Current.Name}! Passa il mouse sul notch in alto per aprirmi. Puoi cambiare tutto dalle impostazioni (⚙).",
                                $"That's it, {Settings.Current.Name}! Hover over the notch at the top to open me. You can change everything in the settings (⚙).");
                BNext.Content = Loc.L("Iniziamo!", "Let's go!");
                break;
        }
    }

    async void Next_Click(object s, RoutedEventArgs e)
    {
        var st = Settings.Current;
        switch (page)
        {
            case 0:
                st.Name = NameIn.Text.Trim();
                if (st.Name == "") st.Name = Loc.L("amico", "friend");
                GoTo(1);
                break;
            case 1:
                var city = CityIn.Text.Trim();
                if (city == "") { GoTo(2); break; }
                BNext.IsEnabled = false;
                CityStatus.Text = Loc.L("Cerco…", "Searching…");
                var g = await Geo.Find(city);
                BNext.IsEnabled = true;
                if (g == null) { CityStatus.Text = Loc.L("Non trovo questa città (o sei offline). Riprova o salta.", "I can't find this city (or you're offline). Try again or skip."); break; }
                (st.City, st.Lat, st.Lon) = (g.Value.Name, g.Value.Lat, g.Value.Lon);
                CityStatus.Text = "";
                GoTo(2);
                break;
            case 3:
                st.Onboarded = true;
                st.Save();
                DialogResult = true;
                break;
        }
    }

    void Skip_Click(object s, RoutedEventArgs e) => GoTo(page + 1);

    protected override void OnClosing(CancelEventArgs e)
    {
        Ai.Abort();
        if (DialogResult != true) Settings.Current.Save();   // se chiudi a metà ti rifaccio le domande al prossimo avvio
        base.OnClosing(e);
    }
}
