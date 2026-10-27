using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;

namespace YourLittleFriend;

public partial class App : Application
{
    // versione dell'app (es. 1.1.2-beta): viene dal tag della release, o dal csproj se compili da sorgente
    public static string Version =>
        typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "?";

    static Mutex? single;   // una sola copia alla volta (anche l'installer lo usa per sapere se l'app è aperta)

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        single = new Mutex(true, "yourlittlefriend-single", out bool first);
        if (!first) { Shutdown(); return; }
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        YourLittleFriend.Startup.Repair();   // se l'app è appena stata aggiornata l'exe ha un altro nome: l'avvio automatico si riallinea
        YourLittleFriend.Update.CleanOldExe();   // e la copia vecchia con il nome minuscolo non resta in giro
        Theme.Apply();

        bool fresh = !Settings.Current.Onboarded;
        if (fresh)
        {
            new OnboardingWindow().ShowDialog();   // primo avvio
            Settings.Current.LastVersion = App.Version;   // appena installata: non c'è niente di nuovo da raccontare
            Settings.Current.Save();
        }

        var main = new MainWindow();
        MainWindow = main;
        main.Show();
        if (!fresh) WhatsNew();   // se l'app è appena stata aggiornata: cosa c'è di nuovo
    }

    // prima apertura dopo un aggiornamento: si aprono le note della release di questa versione,
    // solo nella lingua scelta. se la release non ha note (o si è offline) non si apre niente.
    async void WhatsNew()
    {
        var st = Settings.Current;
        var seen = st.LastVersion;
        if (seen == App.Version) return;
        st.LastVersion = App.Version;   // in ogni caso non si ripropone
        st.Save();
        if (seen != "") await Task.Delay(1500);   // l'app riparte da sola dopo l'aggiornamento: un attimo per far vedere l'omino
        try
        {
            var notes = await YourLittleFriend.Update.Notes(App.Version);
            var text = notes.Length == 0 ? "" : YourLittleFriend.Update.Pick(notes, Loc.En);
            if (text.Length == 0) return;
            new WhatsNewWindow(App.Version, text).ShowDialog();
        }
        catch { }
    }

    // riavvia l'app (serve dopo il cambio di lingua)
    public static void Restart()
    {
        try { single?.ReleaseMutex(); } catch { }
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true });
        Current.Shutdown();
    }

    // lancia il disinstallatore dell'installer (unins000.exe) e chiude l'app. false se l'app non è stata installata con l'installer
    public static bool Uninstall()
    {
        var unins = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? "", "unins000.exe");
        if (!File.Exists(unins)) return false;
        // piccola pausa così l'app ha il tempo di chiudersi prima che parta il disinstallatore
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping 127.0.0.1 -n 3 >nul & \"{unins}\" /SILENT") { CreateNoWindow = true, UseShellExecute = false });
        Current.Shutdown();
        return true;
    }
}
