using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace YourLittleFriend;

// lo schermo: uno screenshot di tutti gli schermi, in un png dentro la cartella dell'app
public static class Screen
{
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);

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
