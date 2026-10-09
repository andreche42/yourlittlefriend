using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Ellipse = System.Windows.Shapes.Ellipse;
using Line = System.Windows.Shapes.Line;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace YourLittleFriend;

static class LensNative
{
    [StructLayout(LayoutKind.Sequential)] public struct Pt { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Pt p);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(IntPtr h, uint affinity);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int index);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int index, int value);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
    [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr dest, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);

    public static readonly IntPtr TopMost = new(-1);
    public const uint NoSize = 0x1, NoActivate = 0x10;
    public const int VirtualLeft = 76, VirtualTop = 77, VirtualWidth = 78, VirtualHeight = 79;   // GetSystemMetrics: tutti i monitor insieme
    public const uint SrcCopy = 0x00CC0020, CaptureBlt = 0x40000000;   // CAPTUREBLT include anche le finestre trasparenti (come quella dell'app)

    // la finestra non prende il focus e non compare in Alt+Tab; con click = true i clic ci passano attraverso
    public static void Soft(IntPtr h, bool click)
    {
        int ex = GetWindowLong(h, -20);   // GWL_EXSTYLE
        SetWindowLong(h, -20, ex | 0x08000000 | 0x80 | (click ? 0x20 : 0));   // NOACTIVATE | TOOLWINDOW | TRANSPARENT
    }
}

// strato trasparente a tutto schermo (i clic ci passano attraverso) dove si disegnano le sottolineature
sealed class LensOverlay : Window
{
    readonly Canvas canvas = new();
    readonly Brush brush;
    Line? current;

    public LensOverlay()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = SystemParameters.VirtualScreenLeft;   // copre tutto il desktop, anche con più monitor
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Content = canvas;
        var c = Theme.TryParse(Settings.Current.LensColor) ?? Color.FromRgb(0xFF, 0xE6, 0x00);
        brush = new SolidColorBrush(Color.FromArgb(205, c.R, c.G, c.B));   // un po' trasparente, come un evidenziatore: il testo resta leggibile
        SourceInitialized += (_, _) => LensNative.Soft(new WindowInteropHelper(this).Handle, true);
    }

    // x e y sono coordinate dello schermo (unità WPF)
    public Visual Layer => canvas;   // per rivedere la sottolineatura dentro la lente

    public void Begin(double x, double y)
    {
        x -= Left;
        y -= Top;
        current = new Line
        {
            X1 = x, X2 = x, Y1 = y, Y2 = y, Stroke = brush, StrokeThickness = 7,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        };
        canvas.Children.Add(current);
    }

    public void Extend(double x) { if (current != null) current.X2 = x - Left; }

    public void End()
    {
        if (current != null && Math.Abs(current.X2 - current.X1) < 3) canvas.Children.Remove(current);   // un semplice clic non lascia segni
        current = null;
    }
}

// lente di ingrandimento. segue il cursore e ingrandisce quello che c'è sotto, con due strati:
//  1) l'app stessa (YourLittleFriend) ridisegnata in vettoriale e dal vivo: testo nitido anche a forte ingrandimento. è la cosa principale
//  2) lo schermo di windows, copiato dal vivo (e, se la copia dal vivo non funziona, da uno scatto fatto all'apertura)
// la finestra dell'app resta dov'è e visibile. tenendo premuto il pulsante si sottolinea in giallo, la rotella cambia l'ingrandimento,
// doppio clic (o clic destro) chiude la lente
sealed class LensWindow : Window
{
    readonly Window app;
    readonly int dip;
    readonly Image view = new() { Stretch = Stretch.Fill };
    readonly VisualBrush? appBrush;
    readonly VisualBrush lineBrush;
    readonly DispatcherTimer timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
    readonly LensOverlay overlay = new();
    System.Drawing.Bitmap? shot, snap;
    int snapX, snapY;   // angolo in alto a sinistra dello scatto, nelle coordinate dello schermo (può essere negativo con più monitor)
    System.Drawing.Graphics? gfx, snapGfx;
    WriteableBitmap? wb;
    IntPtr hwnd;
    double scale = 1, zoom;
    bool holding, useSnap, hasFrame, everLive;
    int blackRun;

    public LensWindow(Window app)
    {
        this.app = app;
        var st = Settings.Current;
        dip = (int)Math.Clamp(st.LensSize, 160, 800);
        zoom = Math.Clamp(st.LensZoom, 1.2, 8);

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -10000;   // la posizione vera arriva al primo fotogramma
        Top = -10000;
        Width = Height = dip;

        var circle = new EllipseGeometry(new Point(dip / 2.0, dip / 2.0), dip / 2.0 - 2, dip / 2.0 - 2);
        RenderOptions.SetBitmapScalingMode(view, BitmapScalingMode.HighQuality);
        view.Width = view.Height = dip;
        view.Clip = circle;
        var root = new Grid { Width = dip, Height = dip, Cursor = Cursors.Cross };
        // disco quasi invisibile: una finestra trasparente lascia passare i clic, così la lente li riceve anche prima del primo fotogramma
        root.Children.Add(new Ellipse { Width = dip - 3, Height = dip - 3, Fill = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) });
        root.Children.Add(view);

        // strato dell'app: sempre nitido. con lo sfondo trasparente (opacità < 100%) si salta, altrimenti si sommerebbe alla copia dello schermo
        if (app.Content is Visual appVisual && st.Opacity >= .99)
        {
            appBrush = new VisualBrush(appVisual) { ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill, TileMode = TileMode.None };
            var appLayer = new Rectangle { Width = dip, Height = dip, Fill = appBrush, IsHitTestVisible = false, Clip = circle.Clone() };
            root.Children.Add(appLayer);
        }

        // le sottolineature gialle sono disegnate in un'altra finestra: qui si rivedono sopra l'app, ingrandite e nitide
        lineBrush = new VisualBrush(overlay.Layer) { ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill, TileMode = TileMode.None };
        root.Children.Add(new Rectangle { Width = dip, Height = dip, Fill = lineBrush, IsHitTestVisible = false, Clip = circle.Clone() });

        var ring = new Ellipse { Width = dip - 3, Height = dip - 3, StrokeThickness = 4, IsHitTestVisible = false };
        ring.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "AccentBrush");
        root.Children.Add(ring);
        Content = root;

        TakeSnapshot();   // prima di mostrare le finestre della lente, così lo scatto non le contiene

        SourceInitialized += (_, _) =>
        {
            hwnd = new WindowInteropHelper(this).Handle;
            scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            LensNative.Soft(hwnd, false);
            // WDA_EXCLUDEFROMCAPTURE: la lente non vede se stessa. se windows non lo supporta si usa lo scatto fisso
            if (!LensNative.SetWindowDisplayAffinity(hwnd, 0x11) && snap != null) useSnap = true;
        };
        timer.Tick += Tick;
        Loaded += (_, _) => timer.Start();
        overlay.Show();   // prima della lente, così la lente sta sopra
    }

    // scatto dell'intero schermo, fatto una volta sola quando si apre la lente
    void TakeSnapshot()
    {
        try
        {
            snapX = LensNative.GetSystemMetrics(LensNative.VirtualLeft);
            snapY = LensNative.GetSystemMetrics(LensNative.VirtualTop);
            int w = LensNative.GetSystemMetrics(LensNative.VirtualWidth), h = LensNative.GetSystemMetrics(LensNative.VirtualHeight);
            if (w <= 0 || h <= 0) return;
            snap = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            snapGfx = System.Drawing.Graphics.FromImage(snap);
            IntPtr dst = snapGfx.GetHdc();
            IntPtr screen = LensNative.GetDC(IntPtr.Zero);
            try { LensNative.BitBlt(dst, 0, 0, w, h, screen, snapX, snapY, LensNative.SrcCopy | LensNative.CaptureBlt); }
            finally { LensNative.ReleaseDC(IntPtr.Zero, screen); snapGfx.ReleaseHdc(dst); }
        }
        catch
        {
            snapGfx?.Dispose();
            snap?.Dispose();
            snapGfx = null;
            snap = null;
        }
    }

    void Tick(object? s, EventArgs e)
    {
        if (hwnd == IntPtr.Zero) return;
        LensNative.GetCursorPos(out var c);
        scale = VisualTreeHelper.GetDpi(this).DpiScaleX;   // si può cambiare monitor mentre la lente è aperta
        int side = (int)Math.Round(dip * scale);
        LensNative.SetWindowPos(hwnd, LensNative.TopMost, c.X - side / 2, c.Y - side / 2, 0, 0, LensNative.NoSize | LensNative.NoActivate);
        Capture(c.X, c.Y);
        double lw = dip / zoom;
        lineBrush.Viewbox = new Rect(c.X / scale - overlay.Left - lw / 2, c.Y / scale - overlay.Top - lw / 2, lw, lw);
        if (appBrush != null)
        {
            // la zona dell'app da ingrandire: stessa area dello schermo, ma nelle coordinate (in unità WPF) della finestra dell'app
            double w = dip / zoom;
            appBrush.Viewbox = new Rect(c.X / scale - app.Left - w / 2, c.Y / scale - app.Top - w / 2, w, w);
        }
        if (holding) overlay.Extend(c.X / scale);
    }

    // copia una zona dello schermo (o dello scatto fisso) nel bitmap della lente
    bool Blit(int x, int y, int size, bool fromSnap)
    {
        IntPtr dst = gfx!.GetHdc();
        try
        {
            if (fromSnap && snapGfx != null)
            {
                IntPtr src = snapGfx.GetHdc();
                try { return LensNative.BitBlt(dst, 0, 0, size, size, src, x - snapX, y - snapY, LensNative.SrcCopy); }
                finally { snapGfx.ReleaseHdc(src); }
            }
            IntPtr screen = LensNative.GetDC(IntPtr.Zero);
            try { return LensNative.BitBlt(dst, 0, 0, size, size, screen, x, y, LensNative.SrcCopy | LensNative.CaptureBlt); }
            finally { LensNative.ReleaseDC(IntPtr.Zero, screen); }
        }
        finally { gfx.ReleaseHdc(dst); }
    }

    // true se la zona è tutta nera (campiona qualche decina di punti)
    static bool IsBlack(System.Drawing.Bitmap bmp, int size)
    {
        var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, size, size), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        try
        {
            int step = Math.Max(1, size / 7);
            for (int y = 0; y < size; y += step)
                for (int x = 0; x < size; x += step)
                    if ((Marshal.ReadInt32(data.Scan0, y * data.Stride + x * 4) & 0xFFFFFF) != 0) return false;
            return true;
        }
        finally { bmp.UnlockBits(data); }
    }

    void Capture(int cx, int cy)
    {
        try
        {
            int size = Math.Max(8, (int)Math.Round(dip * scale / zoom));
            if (shot == null || shot.Width != size)
            {
                gfx?.Dispose();
                shot?.Dispose();
                shot = new System.Drawing.Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                gfx = System.Drawing.Graphics.FromImage(shot);
                wb = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgr32, null);
                hasFrame = false;
            }
            int x = cx - size / 2, y = cy - size / 2;
            if (!Blit(x, y, size, useSnap)) return;

            // se la copia dal vivo resta tutta nera ma nello scatto iniziale c'è altro, la copia dal vivo non funziona su questo pc: si passa allo scatto fisso
            if (!useSnap && snap != null)
            {
                if (IsBlack(shot, size))
                {
                    // solo se la copia dal vivo non ha mai funzionato: se ha già funzionato, un'area nera è nera davvero (es. un video) e non si congela lo schermo
                    if (!everLive && ++blackRun >= 8 && Blit(x, y, size, true) && !IsBlack(shot, size)) useSnap = true;
                }
                else { everLive = true; blackRun = 0; }
            }

            var data = shot.LockBits(new System.Drawing.Rectangle(0, 0, size, size), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            try { wb!.WritePixels(new Int32Rect(0, 0, size, size), data.Scan0, data.Stride * size, data.Stride); }
            finally { shot.UnlockBits(data); }
            if (!hasFrame) { view.Source = wb; hasFrame = true; }   // si mostra solo dopo la prima copia riuscita: niente schermo nero
        }
        catch { }   // schermata protetta (es. richiesta di permessi): si salta il fotogramma
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ClickCount >= 2) { Close(); return; }
        LensNative.GetCursorPos(out var c);
        holding = true;
        overlay.Begin(c.X / scale, c.Y / scale + 8);   // la sottolineatura va un po' sotto il cursore, come sotto una riga di testo
        CaptureMouse();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        holding = false;
        ReleaseMouseCapture();
        overlay.End();
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        Close();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        zoom = Math.Clamp(zoom * (e.Delta > 0 ? 1.15 : 1 / 1.15), 1.2, 8);
    }

    protected override void OnClosed(EventArgs e)
    {
        timer.Stop();
        overlay.Close();
        gfx?.Dispose();
        shot?.Dispose();
        snapGfx?.Dispose();
        snap?.Dispose();
        Settings.Current.LensZoom = zoom;   // l'ingrandimento scelto con la rotella si ricorda
        Settings.Current.Save();
        base.OnClosed(e);
    }
}
