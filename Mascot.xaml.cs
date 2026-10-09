using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace YourLittleFriend;

public enum MascotMode { Idle, Working, Dance }

// la mascotte: sbatte gli occhi, ogni tanto va in giro, lavora mentre l'ia pensa, balla con la musica e sorride quando glielo chiedi
public partial class Mascot : UserControl
{
    public double WanderRange { get; set; }   // quanto può camminare (px). 0 = sta ferma e fa solo saltelli

    readonly DispatcherTimer idle = new() { Interval = TimeSpan.FromSeconds(3) };
    readonly DispatcherTimer cheerEnd = new() { Interval = TimeSpan.FromMilliseconds(1700) };
    readonly Random rnd = new();
    readonly ScaleTransform flip = new(1, 1);
    readonly TranslateTransform walk = new();
    MascotMode mode;
    bool cheering, walking;
    DateTime walkUntil;   // rete di sicurezza: se la camminata viene interrotta, "walking" non resta bloccato

    public MascotMode Mode
    {
        get => mode;
        set { if (mode == value) return; mode = value; Apply(); }
    }

    public Mascot()
    {
        InitializeComponent();
        RenderTransformOrigin = new Point(.5, .5);
        RenderTransform = new TransformGroup { Children = { flip, walk } };

        EyeSc.BeginAnimation(ScaleTransform.ScaleYProperty, Keys(true, (0, 1), (3000, 1), (3080, .1), (3200, 1), (3300, 1)));   // sbatte gli occhi

        idle.Tick += (_, _) => { idle.Interval = TimeSpan.FromSeconds(rnd.Next(4, 10)); IdleTick(); };
        cheerEnd.Tick += (_, _) => { cheerEnd.Stop(); cheering = false; Apply(); };
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

    // ferma tutto e fa partire l'animazione dello stato attuale
    void Apply()
    {
        Tr.BeginAnimation(TranslateTransform.YProperty, null);
        Rot.BeginAnimation(RotateTransform.AngleProperty, null);
        Sc.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        EyeTr.BeginAnimation(TranslateTransform.XProperty, null);
        foreach (var d in new[] { D1, D2, D3 }) d.BeginAnimation(OpacityProperty, null);

        Eyes.Visibility = cheering ? Visibility.Collapsed : Visibility.Visible;
        HappyEyes.Visibility = cheering ? Visibility.Visible : Visibility.Collapsed;
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
