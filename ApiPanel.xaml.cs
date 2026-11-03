using System.Windows;
using System.Windows.Controls;

namespace YourLittleFriend;

// pannello "porta la tua llm": scegli il servizio, incolla la chiave, scegli il modello.
// va bene chiunque parli la lingua di openai (openai, claude, gemini, nvidia, groq, openrouter, lm studio...)
public partial class ApiPanel : UserControl
{
    public event Action? Ready;   // chiave, indirizzo e modello funzionano: si può usare
    bool syncing;

    public ApiPanel()
    {
        InitializeComponent();
        Intro.Text = Loc.L("Se hai una chiave API la uso al posto del modello sul PC: va bene OpenAI, Claude, Gemini, NVIDIA, Groq, OpenRouter e chiunque altro parli la lingua di OpenAI. La chiave resta su questo PC.",
                           "If you have an API key I'll use it instead of the model on your PC: OpenAI, Claude, Gemini, NVIDIA, Groq, OpenRouter and anyone else speaking OpenAI's language all work. The key stays on this PC.");
        LProvider.Text = Loc.L("Servizio", "Service");
        LKey.Text = Loc.L("La tua chiave API", "Your API key");
        LBase.Text = Loc.L("Indirizzo (di solito non serve toccarlo)", "Address (usually you don't need to touch it)");
        LModel.Text = Loc.L("Modello (scrivilo, oppure carica l'elenco)", "Model (type it, or load the list)");
        BLoad.Content = Loc.L("Carica i modelli", "Load the models");
        BTest.Content = Loc.L("Prova", "Test");
        BKey.Content = Loc.L("Dove prendo la chiave?", "Where do I get the key?");
        Loaded += (_, _) => Init();
        // appena incolli la chiave e passi al campo dopo, i modelli si caricano da soli: non devi sapere come si chiamano
        KeyIn.LostFocus += async (_, _) =>
        {
            if (ProviderCb.Items.Count > 0 && ModelCb.Items.Count == 0 && (KeyIn.Password ?? "").Trim().Length >= 12)
                await LoadModels();
        };
    }

    // il nome che si legge nella tendina: solo l'ultima voce cambia con la lingua
    static string Label(Provider p) => p.Id == Providers.Custom
        ? Loc.L("Altro (compatibile OpenAI)", "Other (OpenAI-compatible)")
        : p.Name;

    public Provider Selected => Providers.Get((ProviderCb.SelectedItem as ComboBoxItem)?.Tag as string);
    public string SelectedId => Selected.Id;

    void Init()
    {
        var st = Settings.Current;
        if (ProviderCb.Items.Count == 0)
        {
            syncing = true;
            foreach (var p in Providers.All) ProviderCb.Items.Add(new ComboBoxItem { Content = Label(p), Tag = p.Id });
            ProviderCb.SelectedIndex = Math.Max(0, Array.FindIndex(Providers.All, p => p.Id == st.ApiProvider));
            KeyIn.Password = st.ApiKey ?? "";
            BaseIn.Text = string.IsNullOrWhiteSpace(st.ApiBase) ? Selected.Base : st.ApiBase;
            ModelCb.Text = st.ApiModel ?? "";
            syncing = false;
        }
        Sync();
    }

    void Sync()
    {
        var p = Selected;
        KeyIn.IsEnabled = p.NeedsKey;
        LKey.Opacity = p.NeedsKey ? 1 : 0.5;
        BKey.Visibility = string.IsNullOrWhiteSpace(p.KeysUrl) ? Visibility.Collapsed : Visibility.Visible;
    }

    void Provider_Changed(object s, SelectionChangedEventArgs e)
    {
        if (syncing) return;
        BaseIn.Text = Selected.Base;   // l'indirizzo del servizio scelto (per "altro" resta vuoto: lo scrive l'utente)
        Sync();
    }

    // quello che c'è scritto qui va nelle impostazioni
    public void Apply()
    {
        var st = Settings.Current;
        st.ApiKey = (KeyIn.Password ?? "").Trim();
        st.ApiBase = BaseIn.Text.Trim();
        st.ApiModel = (ModelCb.Text ?? "").Trim();
    }

    async void Load_Click(object s, RoutedEventArgs e) => await LoadModels();

    // chiede al servizio quali modelli ci sono con questa chiave, così non c'è niente da indovinare
    async Task LoadModels()
    {
        Settings.Current.ApiProvider = SelectedId;
        Apply();
        var typed = (ModelCb.Text ?? "").Trim();
        BLoad.IsEnabled = false;
        Status.Text = Loc.L("Chiedo l'elenco dei modelli…", "Asking for the model list…");
        try
        {
            var list = await Llm.ApiModels();
            ModelCb.Items.Clear();
            foreach (var m in list) ModelCb.Items.Add(m);
            ModelCb.Text = typed.Length > 0 ? typed : list.Count > 0 ? list[0] : "";
            Settings.Current.ApiModel = (ModelCb.Text ?? "").Trim();
            Status.Text = list.Count == 0
                ? Loc.L("Questo servizio non ha dato nessun modello: scrivilo a mano.", "This service gave no models: type it by hand.")
                : Loc.L($"{list.Count} modelli, per esempio: {string.Join(", ", list.Take(3))}", $"{list.Count} models, for example: {string.Join(", ", list.Take(3))}");
        }
        catch (Exception ex) { Status.Text = Loc.L("Non riesco a leggere i modelli: ", "I can't read the models: ") + ex.Message; }
        finally { BLoad.IsEnabled = true; }
    }

    async void Test_Click(object s, RoutedEventArgs e)
    {
        Settings.Current.ApiProvider = SelectedId;
        Apply();
        if (string.IsNullOrWhiteSpace(ModelCb.Text) && ModelCb.Items.Count > 0) ModelCb.Text = ModelCb.Items[0]?.ToString();
        if (!Llm.Ready)
        {
            Status.Text = Loc.L("Manca la chiave o il modello (o, per «altro», l'indirizzo).", "The key or the model is missing (or, for “other”, the address).");
            return;
        }
        BTest.IsEnabled = false;
        Status.Text = Loc.L("Chiedo qualcosa di piccolissimo…", "Asking something tiny…");
        try
        {
            var reply = await Llm.Test();
            Settings.Current.Save();
            Status.Text = Loc.L("✓ Funziona! ", "✓ It works! ") + reply;
            Ready?.Invoke();
        }
        catch (Exception ex) { Status.Text = Loc.L("Non funziona: ", "It doesn't work: ") + ex.Message; }
        finally { BTest.IsEnabled = true; }
    }

    void Key_Click(object s, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(Selected.KeysUrl)) Actions.Open(Selected.KeysUrl);
    }
}
