using System.Diagnostics;
using System.IO;
using System.Windows;

namespace YourLittleFriend;

public partial class App : Application
{
    static Mutex? single;   // una sola copia alla volta (anche l'installer lo usa per sapere se l'app è aperta)

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        single = new Mutex(true, "yourlittlefriend-single", out bool first);
        if (!first) { Shutdown(); return; }
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (!Settings.Current.Onboarded) new OnboardingWindow().ShowDialog();   // primo avvio

        var main = new MainWindow();
        MainWindow = main;
        main.Show();
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
