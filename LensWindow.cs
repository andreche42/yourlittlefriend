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

namespace YourLittleFriend;

static class LensNative
{
    [StructLayout(LayoutKind.Sequential)] public struct Pt { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Pt p);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(IntPtr h, uint affinity);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int index);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int index, int value);

    public static readonly IntPtr TopMost = new(-1);
    public const uint NoSize = 0x1, NoActivate = 0x10;

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
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
        Content = canvas;
        var c = Theme.TryParse(Settings.Current.LensColor) ?? Color.FromRgb(0xFF, 0xE6, 0x00);
        brush = new SolidColorBrush(Color.FromArgb(205, c.R, c.G, c.B));   // un po' trasparente, come un evidenziatore: il testo resta leggibile
        SourceInitialized += (_, _) => LensNative.Soft(new WindowInteropHelper(this).Handle, true);
    }

    public void Begin(double x, double y)
    {
        current = new Line
        {
            X1 = x, X2 = x, Y1 = y, Y2 = y, Stroke = brush, StrokeThickness = 7,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        };
        canvas.Children.Add(current);
    }

    public void Extend(double x) { if (current != null) current.X2 = x; }

    public void End()
    {
        if (current != null && Math.Abs(current.X2 - current.X1) < 3) canvas.Children.Remove(current);   // un semplice clic non lascia segni
        current = null;
    }
}

// lente di ingrandimento: segue il cursore e mostra lo schermo ingrandito. tenendo premuto il pulsante sottolinea in giallo,
// doppio clic (o clic destro) la chiude, la rotella cambia l'ingrandimento
sealed class LensWindow : Window
{
    readonly int dip;
    readonly Image view = new() { Stretch = Stretch.Fill };
    readonly DispatcherTimer timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
    readonly LensOverlay overlay = new();
    System.Drawing.Bitmap? shot;
    System.Drawing.Graphics? gfx;
    WriteableBitmap? wb;
    IntPtr hwnd;
    double scale = 1, zoom;
    bool holding;

    public LensWindow()
    {
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

        RenderOptions.SetBitmapScalingMode(view, BitmapScalingMode.HighQuality);
        view.Width = view.Height = dip;
        view.Clip = new EllipseGeometry(new Point(dip / 2.0, dip / 2.0), dip / 2.0 - 2, dip / 2.0 - 2);
        var ring = new Ellipse { Width = dip - 3, Height = dip - 3, StrokeThickness = 4, IsHitTestVisible = false };
        ring.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "AccentBrush");
        var root = new Grid { Width = dip, Height = dip, Cursor = Cursors.Cross };
        root.Children.Add(view);
        root.Children.Add(ring);
        Content = root;

        SourceInitialized += (_, _) =>
        {
            hwnd = new WindowInteropHelper(this).Handle;
            scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            LensNative.Soft(hwnd, false);
            LensNative.SetWindowDisplayAffinity(hwnd, 0x11);   // WDA_EXCLUDEFROMCAPTURE: la lente non vede se stessa
        };
        timer.Tick += Tick;
        Loaded += (_, _) => timer.Start();
        overlay.Show();   // prima dello strato, così la lente sta sopra
    }

    void Tick(object? s, EventArgs e)
    {
        if (hwnd == IntPtr.Zero) return;
        LensNative.GetCursorPos(out var c);
        int side = (int)Math.Round(dip * scale);
        LensNative.SetWindowPos(hwnd, LensNative.TopMost, c.X - side / 2, c.Y - side / 2, 0, 0, LensNative.NoSize | LensNative.NoActivate);
        Capture(c.X, c.Y);
        if (holding) overlay.Extend(c.X / scale);
    }

    // copia la parte di schermo sotto la lente (CAPTUREBLT include anche le finestre trasparenti) e la mostra ingrandita
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
                view.Source = wb;
            }
            gfx!.CopyFromScreen(cx - size / 2, cy - size / 2, 0, 0, new System.Drawing.Size(size, size), (System.Drawing.CopyPixelOperation)0x40CC0020);
            var data = shot.LockBits(new System.Drawing.Rectangle(0, 0, size, size), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            try { wb!.WritePixels(new Int32Rect(0, 0, size, size), data.Scan0, data.Stride * size, data.Stride); }
            finally { shot.UnlockBits(data); }
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
        Settings.Current.LensZoom = zoom;   // l'ingrandimento scelto con la rotella si ricorda
        Settings.Current.Save();
        base.OnClosed(e);
    }
}
