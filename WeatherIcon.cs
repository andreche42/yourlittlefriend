using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Line = System.Windows.Shapes.Line;

namespace YourLittleFriend;

public enum WeatherKind { ClearDay, ClearNight, PartlyCloudy, Cloudy, Fog, Rain, Snow, Storm }

// icona del meteo disegnata a mano (nessuna immagine) e animata: sole che gira, gocce che cadono, lampi, neve...
public class WeatherIcon : UserControl
{
    readonly Canvas canvas = new() { Width = 48, Height = 48 };
    WeatherKind? shown;

    public WeatherIcon() { Content = new Viewbox { Child = canvas }; }

    public void Set(WeatherKind kind)
    {
        if (shown == kind) return;
        shown = kind;
        canvas.Children.Clear();
        Build(kind);
    }

    // ---- pezzi ----
    static SolidColorBrush B(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    static Ellipse Dot(double cx, double cy, double r, Brush fill)
    {
        var e = new Ellipse { Width = 2 * r, Height = 2 * r, Fill = fill };
        Canvas.SetLeft(e, cx - r);
        Canvas.SetTop(e, cy - r);
        return e;
    }

    static DoubleAnimation Loop(double from, double to, double sec, double delaySec = 0, bool reverse = false) =>
        new(from, to, TimeSpan.FromSeconds(sec)) { RepeatBehavior = RepeatBehavior.Forever, AutoReverse = reverse, BeginTime = TimeSpan.FromSeconds(delaySec) };

    // nuvola 30x18 (scalata e spostata)
    static Canvas Cloud(string hex, double x, double y, double scale)
    {
        var f = B(hex);
        var c = new Canvas { Width = 30, Height = 18 };
        var body = new Border { Width = 24, Height = 8, CornerRadius = new CornerRadius(4), Background = f };
        Canvas.SetLeft(body, 3);
        Canvas.SetTop(body, 10);
        c.Children.Add(body);
        c.Children.Add(Dot(9, 11, 7, f));
        c.Children.Add(Dot(17, 7.5, 8, f));
        c.Children.Add(Dot(23, 11, 6, f));
        c.RenderTransform = new TransformGroup { Children = { new ScaleTransform(scale, scale), new TranslateTransform(x, y) } };
        return c;
    }

    // sole: nucleo + 8 raggi che girano piano
    static Canvas Sun(double cx, double cy, double scale)
    {
        var g = new Canvas { Width = 0, Height = 0 };
        for (int i = 0; i < 8; i++)
            g.Children.Add(new Line
            {
                X1 = 0, Y1 = -13, X2 = 0, Y2 = -17.5, Stroke = B("#FFB300"), StrokeThickness = 2.4,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, RenderTransform = new RotateTransform(i * 45)
            });
        g.Children.Add(Dot(0, 0, 9, B("#FFC83D")));
        var spin = new RotateTransform();
        spin.BeginAnimation(RotateTransform.AngleProperty, Loop(0, 360, 22));
        g.RenderTransform = new TransformGroup { Children = { spin, new ScaleTransform(scale, scale), new TranslateTransform(cx, cy) } };
        return g;
    }

    void Build(WeatherKind kind)
    {
        var c = canvas.Children;
        switch (kind)
        {
            case WeatherKind.ClearDay:
                c.Add(Sun(24, 24, 1.15));
                break;

            case WeatherKind.ClearNight:
            {
                var moon = new System.Windows.Shapes.Path
                {
                    Fill = B("#F5E6A8"),
                    Data = new CombinedGeometry(GeometryCombineMode.Exclude, new EllipseGeometry(new Point(22, 25), 14, 14), new EllipseGeometry(new Point(29.5, 19.5), 11.5, 11.5))
                };
                c.Add(moon);
                int n = 0;
                foreach (var (x, y, r) in new[] { (36.0, 12.0, 1.6), (41.0, 25.0, 1.2), (31.0, 38.0, 1.3) })
                {
                    var star = Dot(x, y, r, B("#FFF3B0"));
                    star.BeginAnimation(OpacityProperty, Loop(.25, 1, 1.1 + n * .3, n * .4, true));
                    c.Add(star);
                    n++;
                }
                break;
            }

            case WeatherKind.PartlyCloudy:
                c.Add(Sun(17, 17, .85));
                c.Add(Cloud("#F1F4F9", 6, 20, 1.2));
                break;

            case WeatherKind.Cloudy:
            {
                var back = Cloud("#A9B5C5", 13, 8, .95);
                var front = Cloud("#E9EEF5", 4, 17, 1.3);
                var sway = new TranslateTransform();
                sway.BeginAnimation(TranslateTransform.XProperty, Loop(-1.5, 1.5, 3.2, 0, true));
                front.RenderTransform = new TransformGroup { Children = { new ScaleTransform(1.3, 1.3), new TranslateTransform(4, 17), sway } };
                c.Add(back);
                c.Add(front);
                break;
            }

            case WeatherKind.Fog:
            {
                c.Add(Cloud("#DDE3EC", 6, 5, 1.2));
                int n = 0;
                foreach (var (x1, x2, y) in new[] { (10.0, 36.0, 32.0), (16.0, 42.0, 37.5), (8.0, 32.0, 43.0) })
                {
                    var tr = new TranslateTransform();
                    tr.BeginAnimation(TranslateTransform.XProperty, Loop(-2.5, 2.5, 2.4 + n * .5, 0, true));
                    c.Add(new Line { X1 = x1, X2 = x2, Y1 = y, Y2 = y, Stroke = B("#B8C3D1"), StrokeThickness = 2.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, RenderTransform = tr });
                    n++;
                }
                break;
            }

            case WeatherKind.Rain:
            {
                c.Add(Cloud("#BFCADA", 5, 3, 1.25));
                int n = 0;
                foreach (double x in new[] { 15.0, 24.0, 33.0 })
                {
                    var tr = new TranslateTransform();
                    var drop = new Line { X1 = x, Y1 = 30, X2 = x - 2, Y2 = 36, Stroke = B("#4FA3FF"), StrokeThickness = 2.4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, RenderTransform = tr };
                    tr.BeginAnimation(TranslateTransform.YProperty, Loop(0, 11, .8, n * .27));
                    drop.BeginAnimation(OpacityProperty, Loop(1, 0, .8, n * .27));
                    c.Add(drop);
                    n++;
                }
                break;
            }

            case WeatherKind.Snow:
            {
                c.Add(Cloud("#D3DCE8", 5, 3, 1.25));
                int n = 0;
                foreach (double x in new[] { 14.0, 24.0, 34.0 })
                {
                    var tr = new TranslateTransform();
                    var flake = Dot(x, 33, 2, Brushes.White);
                    flake.RenderTransform = tr;
                    tr.BeginAnimation(TranslateTransform.YProperty, Loop(0, 11, 1.8, n * .6));
                    tr.BeginAnimation(TranslateTransform.XProperty, Loop(-2, 2, .9, n * .3, true));
                    flake.BeginAnimation(OpacityProperty, Loop(1, 0, 1.8, n * .6));
                    c.Add(flake);
                    n++;
                }
                break;
            }

            case WeatherKind.Storm:
            {
                c.Add(Cloud("#7A8596", 5, 3, 1.25));
                var bolt = new System.Windows.Shapes.Path { Fill = B("#FFD23F"), Data = Geometry.Parse("M27,27 L19,39 L24.5,39 L21,48 L33,33.5 L27,33.5 Z") };
                var flick = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
                foreach (var (ms, v) in new[] { (0.0, 1.0), (900.0, 1.0), (940.0, .15), (1010.0, 1.0), (1070.0, .2), (1130.0, 1.0), (2300.0, 1.0) })
                    flick.KeyFrames.Add(new LinearDoubleKeyFrame(v, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms))));
                bolt.BeginAnimation(OpacityProperty, flick);
                c.Add(bolt);
                break;
            }
        }
    }
}
