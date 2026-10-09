using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace YourLittleFriend;

public partial class TranslatePanel : UserControl
{
    string srcLang, dstLang, lastResult = "";
    bool busy;

    public TranslatePanel()
    {
        InitializeComponent();
        var s = Settings.Current;
        srcLang = string.IsNullOrEmpty(s.TrFrom) ? "auto" : s.TrFrom;
        dstLang = string.IsNullOrEmpty(s.TrTo) ? (Loc.En ? "it" : "en") : s.TrTo;
        BGo.Content = Loc.L("Traduci", "Translate");
        BCopy.ToolTip = Loc.L("Copia la traduzione", "Copy the translation");
        BSwap.ToolTip = Loc.L("Scambia le lingue", "Swap languages");
        ShowLangs();
        Output.Show(Translator.Configured ? Loc.L("La traduzione compare qui", "The translation appears here") : NotConfigured());

        // Invio traduce, Maiusc+Invio va a capo
        Input.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) { e.Handled = true; Go_Click(this, new RoutedEventArgs()); }
        };
    }

    static string NotConfigured() => Loc.L("Per tradurre serve una chiave DeepL o un server LibreTranslate: aggiungili da ⚙ Impostazioni.",
                                           "To translate you need a DeepL key or a LibreTranslate server: add them in ⚙ Settings.");

    void ShowLangs()
    {
        BFrom.Content = Translator.Name(srcLang) + " ▾";
        BTo.Content = Translator.Name(dstLang) + " ▾";
    }

    void Save()
    {
        Settings.Current.TrFrom = srcLang;
        Settings.Current.TrTo = dstLang;
        Settings.Current.Save();
    }

    // menu a tendina con l'elenco delle lingue
    void Pick(Button anchor, bool withAuto, Action<string> chosen)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        var codes = new List<string>();
        if (withAuto) codes.Add("auto");
        codes.AddRange(Translator.Langs.Select(l => l.Code));
        foreach (var c in codes)
        {
            var code = c;
            var item = new MenuItem { Header = Translator.Name(code) };
            item.Click += (_, _) => { chosen(code); ShowLangs(); Save(); };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    void From_Click(object s, RoutedEventArgs e) => Pick(BFrom, true, c => srcLang = c);
    void To_Click(object s, RoutedEventArgs e) => Pick(BTo, false, c => dstLang = c);

    void Swap_Click(object s, RoutedEventArgs e)
    {
        if (srcLang == "auto") return;   // non si può scambiare "rileva lingua"
        (srcLang, dstLang) = (dstLang, srcLang);
        ShowLangs();
        Save();
    }

    void Copy_Click(object s, RoutedEventArgs e)
    {
        if (lastResult == "") return;
        try { Clipboard.SetText(lastResult); Info.Text = Loc.L("Copiato ✓", "Copied ✓"); } catch { }
    }

    async void Go_Click(object s, RoutedEventArgs e)
    {
        var text = Input.Text.Trim();
        if (text == "" || busy) return;
        if (!Translator.Configured) { Output.Show(NotConfigured()); return; }
        busy = true;
        BGo.IsEnabled = false;
        Info.Text = "";
        Output.Show("…");
        try
        {
            var (result, engine, detected) = await Translator.Translate(text, srcLang, dstLang);
            lastResult = result;
            Output.Show("");
            Output.Append(result + " ");
            Output.Flush();
            Info.Text = detected != null && srcLang == "auto" ? $"{engine} · {Translator.Name(detected)} →" : engine;
        }
        catch (Exception ex) { Output.Show(Loc.L("Errore: ", "Error: ") + ex.Message); }
        finally { busy = false; BGo.IsEnabled = true; }
    }
}
