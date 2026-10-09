using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace YourLittleFriend;

public enum MascotMode { Idle, Working, Dance }

// come reagisce al tempo: sole = occhiali da sole, pioggia = ombrello, temporale = paura, nuvoloso = "boh", freddo/neve = sciarpa, notte = sonno
public enum WeatherMood { None, Sun, Rain, Storm, Cloud, Snow, Night }

// la mascotte: sbatte gli occhi, ogni tanto va in giro, lavora mentre l'ia pensa, balla con la musica e sorride quando glielo chiedi
public partial class Mascot : UserControl
{
    double wander;
    public double WanderRange   // quanto può camminare (px). 0 = sta ferma e fa solo saltelli
    {
        get => wander;
        set { wander = value; if (value <= 0) GoHome(); }   // se non può più passeggiare torna al centro (altrimenti resta dov'era, magari fuori dalla bolla)
    }

    // riporta l'omino al centro, subito
    public void Home()
    {
        walking = false;
        flip.ScaleX = 1;
        walk.BeginAnimation(TranslateTransform.XProperty, null);
        walk.X = 0;
    }

    readonly DispatcherTimer idle = new() { Interval = TimeSpan.FromSeconds(3) };
    readonly DispatcherTimer cheerEnd = new() { Interval = TimeSpan.FromMilliseconds(1700) };
    readonly DispatcherTimer coolEnd = new() { Interval = TimeSpan.FromMilliseconds(3400) };
    readonly Random rnd = new();
    readonly ScaleTransform flip = new(1, 1);
    readonly TranslateTransform walk = new();
    MascotMode mode;
    WeatherMood weather;
    bool cheering, walking, windy, cool;
    DateTime walkUntil;   // rete di sicurezza: se la camminata viene interrotta, "walking" non resta bloccato

    public MascotMode Mode
    {
        get => mode;
        set { if (mode == value) return; mode = value; Apply(); }
    }

    public WeatherMood Weather
    {
        get => weather;
        set
        {
            if (weather == value) return;
            weather = value;
            Apply();
            if (value == WeatherMood.Sun)   // gli occhiali cadono dall'alto
                ShadesTr.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-18, 0, TimeSpan.FromMilliseconds(600)) { EasingFunction = new BounceEase { Bounces = 2, Bounciness = 3 } });
        }
    }

    // vento forte: con la pioggia l'omino vola via appeso all'ombrello
    public bool Windy
    {
        get => windy;
        set { if (windy == value) return; windy = value; Apply(); }
    }

    public Mascot()
    {
        InitializeComponent();
        RenderTransformOrigin = new Point(.5, .5);
        RenderTransform = new TransformGroup { Children = { flip, walk } };

        EyeSc.BeginAnimation(ScaleTransform.ScaleYProperty, Keys(true, (0, 1), (3000, 1), (3080, .1), (3200, 1), (3300, 1)));   // sbatte gli occhi

        idle.Tick += (_, _) => { idle.Interval = TimeSpan.FromSeconds(rnd.Next(4, 10)); IdleTick(); };
        cheerEnd.Tick += (_, _) => { cheerEnd.Stop(); cheering = false; Apply(); };
        coolEnd.Tick += (_, _) => { coolEnd.Stop(); cool = false; Apply(); };
        IsVisibleChanged += (_, e) => { if ((bool)e.NewValue) idle.Start(); else idle.Stop(); };
    }

    // ---- utilità per le animazioni ----
    static DoubleAnimationUsingKeyFrames Keys(bool loop, params (double ms, double v)[] frames)
    {
        var k = new DoubleAnimationUsingKeyFrames { RepeatBehavior = loop ? RepeatBehavior.Forever : new RepeatBehavior(1) };
        foreach (var (ms, v) in frames)
            k.KeyFrames.Add(new EasingDoubleKeyFrame(v, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms)), new SineEase { EasingMode = EasingMode.EaseInOut }));
        return k;
    }

    static DoubleAnimation Swing(double from, double to, double ms) => new(from, to, TimeSpan.FromMilliseconds(ms))
    { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };

    // ---- stati ----
    public void Cheer()
    {
        cheering = true;
        cheerEnd.Stop();
        cheerEnd.Start();
        Apply();
    }

    // occhiali da sole e pollice in su per qualche secondo
    public void Cool()
    {
        cool = true;
        coolEnd.Stop();
        coolEnd.Start();
        Cheer();
        var pop = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = .7 };
        ThumbSc.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = pop });
        ThumbSc.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = pop });
        ShadesTr.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-18, 0, TimeSpan.FromMilliseconds(600)) { EasingFunction = new BounceEase { Bounces = 2, Bounciness = 3 } });
    }

    // ferma tutto e fa partire l'animazione dello stato attuale
    void Apply()
    {
        Tr.BeginAnimation(TranslateTransform.YProperty, null);
        Rot.BeginAnimation(RotateTransform.AngleProperty, null);
        Sc.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        EyeTr.BeginAnimation(TranslateTransform.XProperty, null);
        foreach (var d in new[] { D1, D2, D3 }) d.BeginAnimation(OpacityProperty, null);
        ApplyWeatherLook();
        ThumbSc.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        ThumbSc.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        ThumbSc.ScaleX = ThumbSc.ScaleY = cool ? 1 : 0;
        Thumb.Visibility = cool ? Visibility.Visible : Visibility.Collapsed;

        bool scared = weather == WeatherMood.Storm && !cheering;
        Eyes.Visibility = cheering || scared ? Visibility.Collapsed : Visibility.Visible;
        HappyEyes.Visibility = cheering ? Visibility.Visible : Visibility.Collapsed;
        ScaredEyes.Visibility = scared ? Visibility.Visible : Visibility.Collapsed;
        Cheeks.Opacity = cheering ? 1 : 0;
        Dots.Visibility = !cheering && mode == MascotMode.Working ? Visibility.Visible : Visibility.Collapsed;

        if (mode != MascotMode.Idle) GoHome();

        if (cheering)
        {
            Tr.BeginAnimation(TranslateTransform.YProperty, Keys(false, (0, 0), (170, -14), (340, 0), (510, -14), (680, 0)));
            Rot.BeginAnimation(RotateTransform.AngleProperty, Keys(false, (0, 0), (170, -7), (340, 0), (510, 7), (680, 0)));
            return;
        }

        switch (mode)
        {
            case MascotMode.Working:   // guarda a destra e sinistra come se leggesse, respira, e i tre puntini si accendono a turno
                EyeTr.BeginAnimation(TranslateTransform.XProperty, Swing(-5, 5, 600));
                Sc.BeginAnimation(ScaleTransform.ScaleYProperty, Swing(1, .96, 500));
                var dots = new[] { D1, D2, D3 };
                for (int i = 0; i < dots.Length; i++)
                    dots[i].BeginAnimation(OpacityProperty, new DoubleAnimation(.15, 1, TimeSpan.FromMilliseconds(400))
                    { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromMilliseconds(i * 150) });
                break;
            case MascotMode.Dance:     // dondola e saltella a tempo
                Rot.BeginAnimation(RotateTransform.AngleProperty, Swing(-8, 8, 350));
                Tr.BeginAnimation(TranslateTransform.YProperty, Keys(true, (0, 0), (175, -6), (350, 0)));
                break;
            default:                   // a riposo: il corpo reagisce al tempo
                switch (weather)
                {
                    case WeatherMood.Storm:   // trema dalla paura
                        Rot.BeginAnimation(RotateTransform.AngleProperty, Keys(true, (0, -2.2), (45, 2.2), (90, -2.2)));
                        break;
                    case WeatherMood.Snow:    // ha freddo
                        Rot.BeginAnimation(RotateTransform.AngleProperty, Keys(true, (0, -1.3), (70, 1.3), (140, -1.3)));
                        break;
                    case WeatherMood.Cloud:   // "boh": inclina la testa e alza le spalle
                        Rot.BeginAnimation(RotateTransform.AngleProperty, Keys(true, (0, 0), (900, 4), (2400, 4), (3300, 0), (5200, 0)));
                        Tr.BeginAnimation(TranslateTransform.YProperty, Keys(true, (0, 0), (900, -2.5), (2400, -2.5), (3300, 0), (5200, 0)));
                        break;
                    case WeatherMood.Night:   // respira piano, dorme
                        Sc.BeginAnimation(ScaleTransform.ScaleYProperty, Swing(1, .97, 1700));
                        break;
                    case WeatherMood.Rain when windy:   // vola con l'ombrello
                        Tr.BeginAnimation(TranslateTransform.YProperty, Swing(0, -10, 1100));
                        Rot.BeginAnimation(RotateTransform.AngleProperty, Swing(-4, 4, 1300));
                        break;
                }
                break;
        }
    }

    // costumi e animazioni del meteo che non dipendono dallo stato (ombrello, gocce, sudore, zzz): partono sempre
    void ApplyWeatherLook()
    {
        UmbRot.BeginAnimation(RotateTransform.AngleProperty, null);
        SweatTr.BeginAnimation(TranslateTransform.YProperty, null);
        Sweat.BeginAnimation(OpacityProperty, null);
        var drops = new[] { R1, R2, R3, R4 };
        foreach (var r in drops) { r.BeginAnimation(Canvas.TopProperty, null); r.Opacity = 0; }
        foreach (var z in new[] { Z1, Z2 }) { z.BeginAnimation(OpacityProperty, null); z.BeginAnimation(Canvas.TopProperty, null); }

        bool rain = weather == WeatherMood.Rain, storm = weather == WeatherMood.Storm && !cheering, night = weather == WeatherMood.Night && !cheering;
        bool boh = weather == WeatherMood.Cloud && !cheering;

        Shades.Visibility = weather == WeatherMood.Sun || cool ? Visibility.Visible : Visibility.Collapsed;
        Umbrella.Visibility = RainDrops.Visibility = rain ? Visibility.Visible : Visibility.Collapsed;
        Scarf.Visibility = weather == WeatherMood.Snow ? Visibility.Visible : Visibility.Collapsed;
        Sweat.Visibility = storm ? Visibility.Visible : Visibility.Collapsed;
        Zzz.Visibility = night ? Visibility.Visible : Visibility.Collapsed;
        Lids.Visibility = boh || night ? Visibility.Visible : Visibility.Collapsed;
        LidSc.ScaleY = night ? .85 : .5;

        // bocca: spalancata dalla paura, una riga se "boh", tremolante dal freddo
        string? mouth = cheering ? null : weather switch
        {
            WeatherMood.Storm => "M46.5,60 A3.5,4 0 1 1 53.5,60 A3.5,4 0 1 1 46.5,60 Z",
            WeatherMood.Cloud => "M44,61 L56,61",
            WeatherMood.Snow => "M42.5,61 Q46.2,57 50,61 T57.5,61",
            _ => null
        };
        if (mouth == null) Mouth.Visibility = Visibility.Collapsed;
        else
        {
            Mouth.Data = Geometry.Parse(mouth);
            if (weather == WeatherMood.Storm) Mouth.SetResourceReference(Shape.FillProperty, "MascotEyeBrush"); else Mouth.Fill = null;
            Mouth.Visibility = Visibility.Visible;
        }

        if (rain)
        {
            UmbRot.BeginAnimation(RotateTransform.AngleProperty, Swing(-3, 3, windy ? 500 : 1500));
            for (int i = 0; i < drops.Length; i++)
            {
                drops[i].Opacity = .85;
                drops[i].BeginAnimation(Canvas.TopProperty, new DoubleAnimation(16, 60, TimeSpan.FromMilliseconds(750)) { RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromMilliseconds(i * 190) });
            }
        }
        if (storm)
        {
            SweatTr.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, 14, TimeSpan.FromMilliseconds(1100)) { RepeatBehavior = RepeatBehavior.Forever });
            Sweat.BeginAnimation(OpacityProperty, Keys(true, (0, 0), (150, 1), (800, 1), (1100, 0)));
        }
        if (night)
        {
            var zs = new[] { Z1, Z2 };
            for (int i = 0; i < zs.Length; i++)
            {
                var delay = TimeSpan.FromMilliseconds(i * 800);
                var fade = Keys(true, (0, 0), (500, 1), (1500, 1), (2200, 0)); fade.BeginTime = delay;
                var rise = new DoubleAnimation(i == 0 ? 24 : 12, i == 0 ? 12 : 0, TimeSpan.FromMilliseconds(2200)) { RepeatBehavior = RepeatBehavior.Forever, BeginTime = delay };
                zs[i].BeginAnimation(OpacityProperty, fade);
                zs[i].BeginAnimation(Canvas.TopProperty, rise);
            }
        }
    }

    // torna al centro (quando smette di girare per lavorare o ballare)
    void GoHome()
    {
        walking = false;
        flip.ScaleX = 1;
        double from = walk.X;
        walk.BeginAnimation(TranslateTransform.XProperty, null);
        walk.X = 0;
        if (from != 0)
            walk.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(from, 0, TimeSpan.FromMilliseconds(350)) { FillBehavior = FillBehavior.Stop });
    }

    // ---- vita da mascotte: ogni tanto fa qualcosa da sola ----
    void IdleTick()
    {
        if (walking && DateTime.UtcNow > walkUntil) walking = false;
        if (mode != MascotMode.Idle || cheering || walking) return;
        // con la paura, il freddo, il sonno o il volo sta fermo: ci pensano già le animazioni del tempo
        if (weather is WeatherMood.Storm or WeatherMood.Snow or WeatherMood.Night || (weather == WeatherMood.Rain && windy)) return;
        switch (rnd.Next(3))
        {
            case 0: if (WanderRange > 0) Wander(); else Hop(); break;
            case 1: Look(); break;
            default: Hop(); break;
        }
    }

    void Hop() =>
        Tr.BeginAnimation(TranslateTransform.YProperty, Keys(false, (0, 0), (160, -12), (320, 0), (480, -8), (640, 0)));

    void Look() =>
        EyeTr.BeginAnimation(TranslateTransform.XProperty, Keys(false, (0, 0), (250, -5), (1000, -5), (1250, 5), (1900, 5), (2150, 0)));

    // va a spasso fino a un punto a caso, saltellando
    void Wander()
    {
        double from = walk.X, target = (rnd.NextDouble() * 2 - 1) * WanderRange, dx = target - from;
        if (Math.Abs(dx) < 20) return;
        walking = true;
        walkUntil = DateTime.UtcNow.AddMilliseconds(Math.Abs(dx) / 45 * 1000 + 1500);
        flip.ScaleX = dx > 0 ? 1 : -1;
        double ms = Math.Abs(dx) / 45 * 1000;   // 45 px al secondo
        walk.X = target;
        var go = new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(ms))
        { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        go.Completed += (_, _) => walking = false;
        walk.BeginAnimation(TranslateTransform.XProperty, go);
        Tr.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -8, TimeSpan.FromMilliseconds(170))
        { AutoReverse = true, RepeatBehavior = new RepeatBehavior(TimeSpan.FromMilliseconds(ms)) });
    }
}
