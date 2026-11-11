using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace YourLittleFriend;

// lo schermo: la luminosità (dxva2, funziona sul pannello dei portatili) e uno screenshot di tutti gli schermi
public static class Screen
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PhysicalMonitor
    {
        public IntPtr Handle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
    }

    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Point pt, uint flags);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("dxva2.dll")] static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);
    [DllImport("dxva2.dll")] static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint count, [Out] PhysicalMonitor[] monitors);
    [DllImport("dxva2.dll")] static extern bool SetMonitorBrightness(IntPtr monitor, uint brightness);
    [DllImport("dxva2.dll")] static extern bool DestroyPhysicalMonitors(uint count, [Out] PhysicalMonitor[] monitors);

    // luminosità del pannello principale. false = questo schermo non si lascia regolare (i fissi e i monitor esterni)
    public static bool SetBrightness(uint level)
    {
        var pm = Primary();
        if (pm == null) return false;
        try { return SetMonitorBrightness(pm[0].Handle, Math.Min(level, 100u)); }
        catch { return false; }
        finally { try { DestroyPhysicalMonitors((uint)pm.Length, pm); } catch { } }
    }

    static PhysicalMonitor[]? Primary()
    {
        try
        {
            var h = MonitorFromPoint(new Point(0, 0), 1);   // MONITOR_DEFAULTTOPRIMARY
            if (h == IntPtr.Zero || !GetNumberOfPhysicalMonitorsFromHMONITOR(h, out var n) || n == 0) return null;
            var list = new PhysicalMonitor[n];
            return GetPhysicalMonitorsFromHMONITOR(h, n, list) ? list : null;
        }
        catch { return null; }
    }

    // screenshot di tutti gli schermi in un png dentro la cartella dell'app. null se non riesce
    public static string? Shoot()
    {
        try
        {
            int x = GetSystemMetrics(76), y = GetSystemMetrics(77), w = GetSystemMetrics(78), h = GetSystemMetrics(79);   // schermo virtuale
            if (w <= 0 || h <= 0) return null;
            var dir = Path.Combine(Settings.Dir, "screenshots");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"screenshot-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            using var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(x, y, 0, 0, new Size(w, h));
            bmp.Save(path, ImageFormat.Png);
            return path;
        }
        catch { return null; }
    }
}
