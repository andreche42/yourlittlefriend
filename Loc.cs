using System.Windows.Markup;

namespace YourLittleFriend;

// testi in due lingue: Loc.L("ciao", "hello"). la lingua si cambia dalle impostazioni (serve riavvio)
public static class Loc
{
    public static bool En => Settings.Current.Language == "en";
    public static string L(string it, string en) => En ? en : it;
}

// per lo xaml: Text="{local:L It='ciao', En='hello'}"
public class LExtension : MarkupExtension
{
    public string It { get; set; } = "";
    public string En { get; set; } = "";
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.L(It, En);
}
