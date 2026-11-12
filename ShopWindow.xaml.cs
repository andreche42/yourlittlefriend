using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using WPath = System.Windows.Shapes.Path;

namespace YourLittleFriend;

// guardaroba e shop: i vestiti sono gratis, è solo un gioco per sbloccarli.
//  1. "aggiungi": un carrello con l'articolo dentro attraversa la finestra e l'articolo finisce nel carrello
//  2. "compra": la busta vola dal carrello al camerino e i vestiti entrano nel guardaroba
//  3. nel camerino l'omino sta sotto una luce che sale dal pavimento: tocchi un vestito, fa "puf!" e se lo ritrova addosso, con una reazione
public partial class ShopWindow : Window
{
    static readonly Random rnd = new();
    readonly List<Item> cart = new();
    readonly HashSet<string> pending = new();   // il carrello sta passando con questo articolo: non si può aggiungere due volte
    readonly HashSet<string> fresh = new();     // appena comprati e ancora da provare (hanno il pallino rosso)
    string filter = "all";
    bool busy;                                  // una animazione grossa (acquisto, prova) è in corso

    public ShopWindow()
    {
        InitializeComponent();
        Title = Loc.L("Guardaroba e shop", "Wardrobe and shop");
        ShopTitle.Text = Loc.L("Shop", "Shop");
        CartTitle.Text = Loc.L("Carrello", "Cart");
        RoomTitle.Text = Loc.L("Camerino", "Fitting room");
        BTakeOff.Content = Loc.L("Togli tutto", "Take all off");
        BuildFilters();
        BuildCatalog();
        RefreshCart();
        BuildOwned();
    }

    static SolidColorBrush Hex(string c) => new((Color)ColorConverter.ConvertFromString(c));

    // ---- shop ----

    void BuildFilters()
    {
        Filters.Children.Clear();
        foreach (var (id, it, en) in new[] { ("all", "Tutto", "All"), ("outfit", "Vestiti e costumi", "Outfits"), ("hat", "Cappelli", "Hats"), ("face", "Occhiali", "Glasses") })
        {
            var b = new Button { Content = Loc.L(it, en), Style = (Style)FindResource(filter == id ? "Primary" : "Pill") };
            b.Click += (_, _) => { filter = id; BuildFilters(); BuildCatalog(); };
            Filters.Children.Add(b);
        }
    }

    void BuildCatalog()
    {
        Catalog.Children.Clear();
        foreach (var it in Wardrobe.Items)
            if (filter == "all" || it.Slot.ToString().ToLowerInvariant() == filter)
                Catalog.Children.Add(Card(it));
    }

    Border Card(Item it)
    {
        bool owned = Wardrobe.Owns(it), inCart = cart.Contains(it) || pending.Contains(it.Id);
        var pic = new Viewbox { Width = 78, Height = 78, Child = Wardrobe.Preview(it) };
        var name = new TextBlock
        {
            Text = it.Name, FontSize = 12.5, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap,
            Foreground = Theme.Res("FgBrush"), Margin = new Thickness(0, 4, 0, 6), MinHeight = 32
        };
        var btn = new Button
        {
            Style = (Style)FindResource("Pill"), Margin = new Thickness(0), Padding = new Thickness(10, 4, 10, 4), FontSize = 12.5,
            IsEnabled = !owned && !inCart,
            Content = owned ? Loc.L("Già tuo", "Owned") : inCart ? Loc.L("Nel carrello", "In cart") : Loc.L("Aggiungi", "Add")
        };
        btn.Click += (_, _) => AddToCart(it, pic);
        return new Border
        {
            Width = 128, Margin = new Thickness(0, 0, 12, 12), Padding = new Thickness(8, 10, 8, 10),
            CornerRadius = new CornerRadius(14), Background = Theme.Res("PanelBrush"),
            Child = new StackPanel { Children = { pic, name, btn } }
        };
    }

    async void AddToCart(Item it, FrameworkElement from)
    {
        if (cart.Contains(it) || pending.Contains(it.Id) || Wardrobe.Owns(it)) return;
        pending.Add(it.Id);
        var p = from.TranslatePoint(new Point(from.ActualWidth / 2, from.ActualHeight / 2), Overlay);   // prima di ridisegnare lo shop
        BuildCatalog();                                                                                  // il bottone diventa subito "nel carrello"
        await RollCart(it, p.Y);
        pending.Remove(it.Id);
        cart.Add(it);
        RefreshCart();
        BuildCatalog();
        Pop(CartBox);
    }

    // ---- carrello ----

    void RefreshCart()
    {
        CartItems.Children.Clear();
        foreach (var it in cart.ToList())
        {
            var b = new Button
            {
                Content = new Viewbox { Width = 44, Height = 44, Child = Wardrobe.Preview(it) }, Style = (Style)FindResource("Pill"),
                Padding = new Thickness(2), Margin = new Thickness(0, 0, 6, 6), ToolTip = Loc.L("Clic per toglierlo", "Click to remove")
            };
            b.Click += (_, _) => { if (busy) return; cart.Remove(it); RefreshCart(); BuildCatalog(); };
            CartItems.Children.Add(b);
        }
        if (cart.Count == 0)
            CartItems.Children.Add(new TextBlock { Text = Loc.L("Il carrello è vuoto", "Your cart is empty"), Opacity = .6, FontSize = 12.5, Margin = new Thickness(0, 14, 0, 0) });
        CartTotal.Text = Loc.L("Totale: gratis ✨", "Total: free ✨");
        BBuy.Content = Loc.L("Compra", "Buy");
        BBuy.IsEnabled = cart.Count > 0 && !busy;
    }

    async void Buy_Click(object s, RoutedEventArgs e)
    {
        if (cart.Count == 0 || busy) return;
        var items = cart.ToList();
        var from = BBuy.TranslatePoint(new Point(BBuy.ActualWidth / 2, BBuy.ActualHeight / 2), Overlay);
        var to = Stage.TranslatePoint(new Point(Stage.ActualWidth / 2, Stage.ActualHeight / 2), Overlay);
        busy = true;
        cart.Clear();           // le cose sono nella busta
        RefreshCart();
        BuildCatalog();
        await FlyBag(from, to);
        Puff(to, 1.1, false);   // la busta arriva nel camerino
        Wardrobe.Own(items);
        foreach (var it in items) fresh.Add(it.Id);
        Glow(1);
        SetBusy(false);
        BuildCatalog();
        await Task.Delay(700);
        if (!busy) Glow(.35);
    }

    // ---- camerino ----

    void BuildOwned()
    {
        OwnedPanel.Children.Clear();
        var mine = Wardrobe.Items.Where(Wardrobe.Owns).ToList();
        RoomHint.Text = mine.Count == 0
            ? Loc.L("Il guardaroba è vuoto: scegli qualcosa dallo shop!", "Your wardrobe is empty: pick something from the shop!")
            : Loc.L("Tocca un vestito per provarlo, di nuovo per toglierlo", "Tap a piece to try it on, tap again to take it off");
        BTakeOff.IsEnabled = Wardrobe.WearingAnything && !busy;
        foreach (var it in mine)
        {
            var pic = new Grid();
            pic.Children.Add(new Viewbox { Width = 42, Height = 42, Child = Wardrobe.Preview(it) });
            if (fresh.Contains(it.Id))
                pic.Children.Add(new Ellipse { Width = 9, Height = 9, Fill = Hex("#FF3B30"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top });
            var b = new Button
            {
                Content = pic, ToolTip = it.Name, Padding = new Thickness(3), Margin = new Thickness(0, 0, 6, 6),
                Style = (Style)FindResource(Wardrobe.IsWorn(it) ? "Primary" : "Pill")
            };
            b.Click += async (_, _) => await TryOn(it);
            OwnedPanel.Children.Add(b);
        }
    }

    void SetBusy(bool b)
    {
        busy = b;
        RefreshCart();
        BuildOwned();
    }

    async Task TryOn(Item it)
    {
        if (busy) return;
        bool on = !Wardrobe.IsWorn(it);
        await Change(() =>
        {
            if (on) Wardrobe.Wear(it); else Wardrobe.Take(it.Slot);
            fresh.Remove(it.Id);
        }, on);
    }

    async void TakeOff_Click(object s, RoutedEventArgs e)
    {
        if (busy || !Wardrobe.WearingAnything) return;
        await Change(Wardrobe.TakeAll, false);
    }

    // la luce si accende, "puf!" copre l'omino, nel frattempo cambiano i vestiti, poi la nuvola si apre e lui reagisce
    async Task Change(Action change, bool happy)
    {
        SetBusy(true);
        Glow(1);
        var c = RoomMascot.TranslatePoint(new Point(RoomMascot.ActualWidth / 2, RoomMascot.ActualHeight / 2), Overlay);
        Puff(c, 1.5, true);
        await Task.Delay(230);
        change();
        await Task.Delay(400);
        React(happy);
        await Task.Delay(500);
        Glow(.35);
        SetBusy(false);
    }

    // con un vestito nuovo salta di gioia, saluta o fa festa; se se lo toglie fa solo un saltino
    void React(bool happy)
    {
        if (!happy) { RoomMascot.Poke(); return; }
        switch (rnd.Next(3))
        {
            case 0: RoomMascot.Cheer(); break;
            case 1: RoomMascot.Celebrate(); break;
            default: RoomMascot.Wave(); break;
        }
    }

    void Glow(double to)
    {
        var d = TimeSpan.FromMilliseconds(260);
        Beam.BeginAnimation(OpacityProperty, new DoubleAnimation(to, d));
        Floor.BeginAnimation(OpacityProperty, new DoubleAnimation(to < .5 ? .6 : 1, d));
    }

    // ---- animazioni ----

    static void Pop(FrameworkElement el)
    {
        var sc = new ScaleTransform(1, 1);
        el.RenderTransform = sc;   // l'origine (centro) è già impostata nello xaml
        var a = new DoubleAnimation(1, 1.04, TimeSpan.FromMilliseconds(130)) { AutoReverse = true };
        sc.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        sc.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }

    static WPath Ink(string data, Brush stroke, double w) => new()
    {
        Data = Geometry.Parse(data), Stroke = stroke, StrokeThickness = w, StrokeLineJoin = PenLineJoin.Round,
        StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
    };

    // il carrello: l'articolo sporge dal cestino
    FrameworkElement MakeCart(Item it)
    {
        var c = new Canvas { Width = 78, Height = 64 };
        var pic = new Viewbox { Width = 40, Height = 40, Child = Wardrobe.Preview(it) };
        Canvas.SetLeft(pic, 19);
        Canvas.SetTop(pic, -2);
        c.Children.Add(pic);
        var accent = Theme.Res("AccentBrush");
        c.Children.Add(new WPath { Data = Geometry.Parse("M10,18 L68,18 L60,44 L19,44 Z"), Fill = accent, Stroke = accent, StrokeThickness = 3, StrokeLineJoin = PenLineJoin.Round });
        c.Children.Add(Ink("M2,6 L10,6 L22,46 L60,46", Theme.Res("FgBrush"), 3));
        foreach (var x in new[] { 18.0, 50.0 })
        {
            var w = new Ellipse { Width = 9, Height = 9, Fill = Theme.Res("FgBrush") };
            Canvas.SetLeft(w, x);
            Canvas.SetTop(w, 52);
            c.Children.Add(w);
        }
        return c;
    }

    // il carrello attraversa tutta la finestra (veloce) all'altezza della vetrina da cui è partito
    Task RollCart(Item it, double y)
    {
        var cartEl = MakeCart(it);
        double top = y - 40;
        Canvas.SetTop(cartEl, top);
        Canvas.SetLeft(cartEl, -90);
        Overlay.Children.Add(cartEl);
        var tcs = new TaskCompletionSource();
        var move = new DoubleAnimation(-90, Overlay.ActualWidth + 20, TimeSpan.FromMilliseconds(650)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
        move.Completed += (_, _) => { Overlay.Children.Remove(cartEl); tcs.SetResult(); };
        cartEl.BeginAnimation(Canvas.LeftProperty, move);
        // le ruote sobbalzano
        cartEl.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(top, top - 4, TimeSpan.FromMilliseconds(80)) { AutoReverse = true, RepeatBehavior = new RepeatBehavior(4) });
        return tcs.Task;
    }

    FrameworkElement MakeBag()
    {
        var c = new Canvas { Width = 56, Height = 68 };
        c.Children.Add(Ink("M14,22 C14,2 42,2 42,22", Hex("#B8924A"), 3));
        c.Children.Add(new WPath { Data = Geometry.Parse("M6,20 L50,20 L54,66 L2,66 Z"), Fill = Hex("#E8C17A"), Stroke = Hex("#B8924A"), StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round });
        c.Children.Add(new WPath { Data = Geometry.Parse("M6,20 L50,20 L52,30 L4,30 Z"), Fill = Hex("#D8AE65") });   // il bordo piegato
        var star = new TextBlock { Text = "★", FontSize = 20, Foreground = Theme.Res("AccentBrush") };
        Canvas.SetLeft(star, 18);
        Canvas.SetTop(star, 36);
        c.Children.Add(star);
        return c;
    }

    // la busta vola dal carrello al camerino: va dritta ma salta (fa un arco) e a metà strada inizia a rimpicciolire
    Task FlyBag(Point from, Point to)
    {
        var bag = MakeBag();
        var tr = new TranslateTransform();
        var sc = new ScaleTransform(1, 1);
        bag.RenderTransformOrigin = new Point(.5, .5);
        bag.RenderTransform = new TransformGroup { Children = { sc, tr } };
        Canvas.SetLeft(bag, from.X - 28);
        Canvas.SetTop(bag, from.Y - 34);
        Overlay.Children.Add(bag);

        var dur = TimeSpan.FromMilliseconds(800);
        var tcs = new TaskCompletionSource();

        tr.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, to.X - from.X, dur) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });

        var y = new DoubleAnimationUsingKeyFrames { Duration = dur };
        y.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        y.KeyFrames.Add(new EasingDoubleKeyFrame(-90, KeyTime.FromPercent(.4), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        y.KeyFrames.Add(new EasingDoubleKeyFrame(to.Y - from.Y, KeyTime.FromPercent(1), new QuadraticEase { EasingMode = EasingMode.EaseIn }));
        y.Completed += (_, _) => { Overlay.Children.Remove(bag); tcs.SetResult(); };
        tr.BeginAnimation(TranslateTransform.YProperty, y);

        var shrink = new DoubleAnimationUsingKeyFrames { Duration = dur };
        shrink.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0)));
        shrink.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(.6)));
        shrink.KeyFrames.Add(new LinearDoubleKeyFrame(.35, KeyTime.FromPercent(1)));
        sc.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
        sc.BeginAnimation(ScaleTransform.ScaleYProperty, shrink);
        return tcs.Task;
    }

    // "puf!": una nuvoletta di palline bianche che si apre dal centro e svanisce, con la scritta
    void Puff(Point c, double k, bool word)
    {
        var dur = TimeSpan.FromMilliseconds(600);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        for (int i = 0; i < 9; i++)
        {
            bool center = i == 8;
            double a = i * Math.PI / 4, r = center ? 0 : 22 * k, size = (center ? 20 : i % 2 == 0 ? 15 : 12) * k;
            var ball = new Ellipse { Width = size * 2, Height = size * 2, Fill = Hex("#FFFFFF"), Stroke = Hex("#D5DBE6"), StrokeThickness = 1 };
            var sc = new ScaleTransform(.2, .2);
            var tr = new TranslateTransform();
            ball.RenderTransformOrigin = new Point(.5, .5);
            ball.RenderTransform = new TransformGroup { Children = { sc, tr } };
            Canvas.SetLeft(ball, c.X - size);
            Canvas.SetTop(ball, c.Y - size);
            Overlay.Children.Add(ball);
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(.2, 1.15, dur) { EasingFunction = ease });
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(.2, 1.15, dur) { EasingFunction = ease });
            tr.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, Math.Cos(a) * r, dur) { EasingFunction = ease });
            tr.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, Math.Sin(a) * r, dur) { EasingFunction = ease });
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400)) { BeginTime = TimeSpan.FromMilliseconds(200) };
            fade.Completed += (_, _) => Overlay.Children.Remove(ball);
            ball.BeginAnimation(OpacityProperty, fade);
        }
        if (!word) return;
        var t = new TextBlock { Text = "puf!", FontSize = 24, FontWeight = FontWeights.Black, Foreground = Theme.Res("AccentBrush") };
        var tsc = new ScaleTransform(.3, .3);
        t.RenderTransformOrigin = new Point(.5, .5);
        t.RenderTransform = tsc;
        Canvas.SetLeft(t, c.X + 22 * k);
        Canvas.SetTop(t, c.Y - 56 * k);
        Overlay.Children.Add(t);
        tsc.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(.3, 1.2, dur) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut } });
        tsc.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(.3, 1.2, dur) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut } });
        var life = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(800) };
        life.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        life.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(.2)));
        life.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(.65)));
        life.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        life.Completed += (_, _) => Overlay.Children.Remove(t);
        t.BeginAnimation(OpacityProperty, life);
    }
}
