using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WPath = System.Windows.Shapes.Path;

namespace YourLittleFriend;

// dove si mette un vestito: "outfit" = vestito o costume (sta sul corpo), "hat" = cappello, "face" = occhiali
public enum Slot { Outfit, Hat, Face }

// un vestito: i disegni sono righe di testo (vedi Wardrobe.Data), così si aggiungono senza scrivere codice
public sealed class Item
{
    public string Id { get; init; } = "";
    public string It { get; init; } = "";
    public string En { get; init; } = "";
    public Slot Slot { get; init; }
    public bool OwnsHead { get; init; }   // il costume ha già il suo cappello/capelli/orecchie: con lui non si mette un cappello
    public string[] Shapes { get; init; } = Array.Empty<string>();
    public string Name => Loc.L(It, En);

    // disegna il vestito: ogni chiamata crea elementi nuovi (un elemento wpf non può stare in due posti)
    public IEnumerable<UIElement> Build()
    {
        foreach (var line in Shapes)
            if (Wardrobe.MakeShape(line) is { } el) yield return el;
    }
}

// guardaroba: cosa esiste, cosa hai comprato e cosa sta indossando l'omino (salvato nelle impostazioni).
// tutto è disegnato nello spazio 100x100 della mascotte: la testa va da (22,28) a (78,70), gli occhi stanno a y 45-53
public static class Wardrobe
{
    public static event Action? Changed;   // cambia quello che si indossa: ogni omino si ridisegna da solo

    public static readonly IReadOnlyList<Item> Items = Parse(Data);

    public static Item? Find(string? id) => Items.FirstOrDefault(i => i.Id == id);
    public static bool Owns(Item i) => Settings.Current.Owned.Contains(i.Id);

    public static Item? In(Slot s) => Find(s switch
    {
        Slot.Outfit => Settings.Current.WornOutfit,
        Slot.Hat => Settings.Current.WornHat,
        _ => Settings.Current.WornFace
    });

    public static bool IsWorn(Item i) => In(i.Slot) == i;
    public static bool WearingAnything => In(Slot.Outfit) != null || In(Slot.Hat) != null || In(Slot.Face) != null;

    public static IEnumerable<Item> WornItems()
    {
        foreach (var s in new[] { Slot.Outfit, Slot.Hat, Slot.Face })
            if (In(s) is { } i) yield return i;
    }

    static void Set(Slot s, string id)
    {
        var c = Settings.Current;
        if (s == Slot.Outfit) c.WornOutfit = id; else if (s == Slot.Hat) c.WornHat = id; else c.WornFace = id;
    }

    // indossa: un costume con il suo cappello toglie quello che c'era in testa, e viceversa
    public static void Wear(Item i)
    {
        Set(i.Slot, i.Id);
        if (i.Slot == Slot.Outfit && i.OwnsHead) Set(Slot.Hat, "");
        if (i.Slot == Slot.Hat && In(Slot.Outfit) is { OwnsHead: true }) Set(Slot.Outfit, "");
        Settings.Current.Save();
        Changed?.Invoke();
    }

    public static void Take(Slot s)
    {
        Set(s, "");
        Settings.Current.Save();
        Changed?.Invoke();
    }

    public static void TakeAll()
    {
        foreach (var s in new[] { Slot.Outfit, Slot.Hat, Slot.Face }) Set(s, "");
        Settings.Current.Save();
        Changed?.Invoke();
    }

    public static void Own(IEnumerable<Item> items)
    {
        foreach (var i in items)
            if (!Settings.Current.Owned.Contains(i.Id)) Settings.Current.Owned.Add(i.Id);
        Settings.Current.Save();
    }

    // il vestito addosso a una testa e due occhi, per le vetrine dello shop
    public static Canvas Preview(Item it)
    {
        var c = new Canvas { Width = 100, Height = 100 };
        var head = new Rectangle { Width = 56, Height = 42, RadiusX = 16, RadiusY = 16 };
        head.SetResourceReference(Shape.FillProperty, "MascotBrush");
        Canvas.SetLeft(head, 22);
        Canvas.SetTop(head, 28);
        c.Children.Add(head);
        if (it.Slot == Slot.Outfit) foreach (var el in it.Build()) c.Children.Add(el);   // i vestiti stanno sotto gli occhi...
        foreach (var x in new[] { 37.5, 57.5 })
        {
            var eye = new Ellipse { Width = 5, Height = 8 };
            eye.SetResourceReference(Shape.FillProperty, "MascotEyeBrush");
            Canvas.SetLeft(eye, x);
            Canvas.SetTop(eye, 45);
            c.Children.Add(eye);
        }
        if (it.Slot != Slot.Outfit) foreach (var el in it.Build()) c.Children.Add(el);   // ...cappelli e occhiali sopra
        return c;
    }

    // ---- disegno ----

    static double N(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    static void Paint(Shape s, DependencyProperty p, string c)
    {
        if (c == "-") return;
        if (c == "face") s.SetResourceReference(p, "MascotBrush");        // il colore dell'omino (cambia coi temi)
        else if (c == "eye") s.SetResourceReference(p, "MascotEyeBrush");
        else s.SetValue(p, new SolidColorBrush((Color)ColorConverter.ConvertFromString(c)));
    }

    // una riga di disegno:  forma riempimento contorno spessore opacità numeri...
    //   R ... x y larghezza altezza raggio     (rettangolo)
    //   E ... cx cy rx ry                      (ellisse)
    //   P ... percorso                         (come il Data di un Path)
    internal static UIElement? MakeShape(string line)
    {
        var t = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (t.Length < 6) return null;
        Shape sh;
        double x = 0, y = 0;
        switch (t[0])
        {
            case "R":
                sh = new Rectangle { Width = N(t[7]), Height = N(t[8]), RadiusX = N(t[9]), RadiusY = N(t[9]) };
                x = N(t[5]); y = N(t[6]);
                break;
            case "E":
                double rx = N(t[7]), ry = N(t[8]);
                sh = new Ellipse { Width = rx * 2, Height = ry * 2 };
                x = N(t[5]) - rx; y = N(t[6]) - ry;
                break;
            case "P":
                sh = new WPath { Data = Geometry.Parse(string.Join(' ', t.Skip(5))) };
                break;
            default:
                return null;
        }
        Paint(sh, Shape.FillProperty, t[1]);
        Paint(sh, Shape.StrokeProperty, t[2]);
        sh.StrokeThickness = N(t[3]);
        sh.Opacity = N(t[4]);
        sh.StrokeLineJoin = PenLineJoin.Round;
        sh.StrokeStartLineCap = sh.StrokeEndLineCap = PenLineCap.Round;
        Canvas.SetLeft(sh, x);
        Canvas.SetTop(sh, y);
        return sh;
    }

    static List<Item> Parse(string data)
    {
        var list = new List<Item>();
        foreach (var block in data.Split("@ ", StringSplitOptions.RemoveEmptyEntries))
        {
            var lines = block.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("//")).ToList();
            if (lines.Count == 0) continue;
            var h = lines[0].Split('|').Select(p => p.Trim()).ToArray();   // id | italiano | inglese | slot | 1 se ha il suo cappello
            if (h.Length < 5) continue;
            list.Add(new Item
            {
                Id = h[0], It = h[1], En = h[2],
                Slot = Enum.Parse<Slot>(h[3], true),
                OwnsHead = h[4] == "1",
                Shapes = lines.Skip(1).ToArray()
            });
        }
        return list;
    }

    // ---- il catalogo ----
    // colori: "face" e "eye" seguono il colore dell'omino, "-" = niente. i vestiti scuri hanno un contorno chiaro (#8E94A3)
    // così si vedono anche sul notch nero. l'ordine delle righe è l'ordine di disegno (la prima sta sotto)
    const string Data = """
@ felpa-lavanda | felpa lavanda | lavender hoodie | outfit | 1
R #8B7BE0 - 0 1 13 55 11 17 5.5
R #8B7BE0 - 0 1 76 55 11 17 5.5
R #A99BF5 - 0 1 19 25 62 47 18
P #C7BEFA - 0 1 M24,37 Q25,21 50,20 Q75,21 76,37 Q50,30 24,37 Z
R face - 0 1 27 37 46 25 12
R #8B7BE0 - 0 1 39 63 22 7 3.5

@ felpa-menta | felpa menta | mint hoodie | outfit | 1
E #8FDDBE - 0 1 29 27 7.5 7.5
E #8FDDBE - 0 1 71 27 7.5 7.5
E #C9F2E1 - 0 1 29 27 3.5 3.5
E #C9F2E1 - 0 1 71 27 3.5 3.5
R #6FC3A2 - 0 1 13 55 11 17 5.5
R #6FC3A2 - 0 1 76 55 11 17 5.5
R #A7E6CC - 0 1 19 25 62 47 18
R face - 0 1 27 37 46 25 12
R #6FC3A2 - 0 1 39 63 22 7 3.5

@ pigiama-stelline | pigiama stelline | starry pajamas | outfit | 1
R #2B3760 #8E94A3 1 1 20 26 60 46 17
R face - 0 1 27 37 46 25 12
P #FFE08A - 0 1 M31,64 l1,2 l2,1 l-2,1 l-1,2 l-1,-2 l-2,-1 l2,-1 Z
P #FFE08A - 0 1 M50,65 l1,2 l2,1 l-2,1 l-1,2 l-1,-2 l-2,-1 l2,-1 Z
P #FFE08A - 0 1 M69,64 l1,2 l2,1 l-2,1 l-1,2 l-1,-2 l-2,-1 l2,-1 Z
P #46589A #8E94A3 1 1 M23,34 Q26,10 56,9 Q78,9 90,30 Q72,22 62,28 Q46,32 23,34 Z
P #FFE08A - 0 1 M44,15 l1,2 l2,1 l-2,1 l-1,2 l-1,-2 l-2,-1 l2,-1 Z
P #FFE08A - 0 1 M60,11 l1,2 l2,1 l-2,1 l-1,2 l-1,-2 l-2,-1 l2,-1 Z
E #FFE08A - 0 1 90 32 5 5

@ pigiama-nuvole | pigiama nuvole | cloud pajamas | outfit | 1
R #9EC3EE - 0 1 20 26 60 46 17
R face - 0 1 27 37 46 25 12
E #C5DAF3 - 0 1 36 25 10 9
E #C5DAF3 - 0 1 50 20 12 11
E #C5DAF3 - 0 1 64 25 10 9
E #FFFFFF - 0 1 36 25 8.6 7.6
E #FFFFFF - 0 1 50 20 10.6 9.6
E #FFFFFF - 0 1 64 25 8.6 7.6
E #FFFFFF - 0 1 30 66 4.5 3.2
E #FFFFFF - 0 1 35 64.8 4.5 3.8
E #FFFFFF - 0 1 40 66 4 3
E #FFFFFF - 0 1 62 66 4 3
E #FFFFFF - 0 1 66.5 64.8 4.5 3.8
E #FFFFFF - 0 1 71 66 4.5 3.2

@ maglione | maglione a maglia | knit sweater | outfit | 0
R #A8693A - 0 1 13 55 11 17 5.5
R #A8693A - 0 1 76 55 11 17 5.5
R #C98B55 - 0 1 20 27 60 45 16
R face - 0 1 27 37 46 25 12
R #E0A86F - 0 1 24 58 52 8 4
P - #B9814B 1.2 .8 M31,58 v8 M38,58 v8 M45,58 v8 M52,58 v8 M59,58 v8 M66,58 v8 M73,58 v8
P - #A8693A 1.5 .9 M25,67.5 l3,3.5 l3,-3.5 l3,3.5 l3,-3.5 l3,3.5 l3,-3.5 l3,3.5 l3,-3.5 l3,3.5 l3,-3.5 l3,3.5 l3,-3.5 l3,3.5 l3,-3.5 l3,3.5

@ maglietta | maglietta blu | blue t-shirt | outfit | 0
P #3F63D8 - 0 1 M27,68 L11,77 L17,89 L29,81 Z
P #3F63D8 - 0 1 M73,68 L89,77 L83,89 L71,81 Z
P #4B73F0 - 0 1 M25,67 Q50,77 75,67 L79,97 L21,97 Z
P - #FFFFFF 2 .9 M37,69 Q50,78 63,69

@ cappellino | cappellino baseball | baseball cap | hat | 0
P #3F63F0 - 0 1 M27,36 Q28,16 50,15 Q72,16 73,36 Z
P #2D49C2 - 0 1 M62,33 L91,36 Q86,41 62,38 Z
P - #2D49C2 1.2 1 M50,15 Q46,25 47,35
E #FFFFFF - 0 1 50 15 3 3

@ bucket-hat | bucket hat | bucket hat | hat | 0
E #D5BC7E - 0 1 50 33 34 6
P #E7D3A0 - 0 1 M33,33 L36,17 Q50,13 64,17 L67,33 Z
R #B08A4A - 0 1 34 25 32 4 1

@ berretto | berretto | beanie | hat | 0
P #A66DF0 - 0 1 M25,36 Q25,16 50,15 Q75,16 75,36 Z
R #8247D6 - 0 1 23 31 54 9 4.5
P - #6B34BE 1.2 .7 M32,32 v7 M40,32 v7 M48,32 v7 M56,32 v7 M64,32 v7 M72,32 v7
E #8247D6 - 0 1 50 14 6.5 6.5

@ cuffie | cuffie | headphones | hat | 0
P - #A06BE8 3 1 M26,46 Q26,16 50,16 Q74,16 74,46
R #7B3FCB - 0 1 18 40 12 19 5
R #7B3FCB - 0 1 70 40 12 19 5
R #A06BE8 - 0 1 20 43 4 13 2
R #A06BE8 - 0 1 76 43 4 13 2
P - #A06BE8 2 1 M24,59 Q27,70 41,69
E #FF5FA8 - 0 1 41 69 3.6 3.6

@ occhiali-rotondi | occhiali rotondi | round glasses | face | 0
E - #2A2D34 2.2 1 40 49 9.5 9.5
E - #2A2D34 2.2 1 60 49 9.5 9.5
P - #2A2D34 2.2 1 M49.5,48 Q50,46.5 50.5,48
P - #2A2D34 2 1 M30.5,47 L24,45 M69.5,47 L76,45

@ occhiali-cuore | occhiali a cuore | heart glasses | face | 0
P #FF5FA8 #E03C8C 2 .6 M40,58 C28,50 30,40 36.5,40 C38.5,40 40,41.5 40,43.5 C40,41.5 41.5,40 43.5,40 C50,40 52,50 40,58 Z
P #FF5FA8 #E03C8C 2 .6 M60,58 C48,50 50,40 56.5,40 C58.5,40 60,41.5 60,43.5 C60,41.5 61.5,40 63.5,40 C70,40 72,50 60,58 Z
P - #E03C8C 2 1 M46,46 Q50,43.5 54,46

@ elfo | completo elfo | elf outfit | outfit | 1
P face - 0 1 M22,43 L10,38 L22,55 Z
P face - 0 1 M78,43 L90,38 L78,55 Z
R #2E8B57 - 0 1 20 27 60 45 16
R face - 0 1 27 37 46 25 12
R #D6453D - 0 1 22 62 56 7 3.5
P #2E8B57 #1F6B42 1 1 M27,36 L46,9 Q60,20 73,36 Z
R #D6453D - 0 1 26 31 48 6 3
E #F5C542 - 0 1 46 8 4.5 4.5

@ babbo-natale | babbo natale | santa outfit | outfit | 1
R #D9363E - 0 1 20 27 60 45 16
R face - 0 1 27 37 46 25 12
P #FFFFFF #E3E6EB 1.2 1 M24,55 Q24,63 31,68 Q40,77 50,78 Q60,77 69,68 Q76,63 76,55 Q70,60 62,58 Q50,64 38,58 Q30,60 24,55 Z
P #D9363E - 0 1 M25,37 Q28,15 55,12 Q73,12 77,30 L77,37 Z
R #FFFFFF #E3E6EB 1 1 22 33 56 8 4
E #FFFFFF #E3E6EB 1 1 79 25 6 6

@ renna | completo renna | reindeer outfit | outfit | 1
P - #6B4423 3 1 M36,32 Q32,19 23,10 M31,23 L23,23 M32,16 L25,15
P - #6B4423 3 1 M64,32 Q68,19 77,10 M69,23 L77,23 M68,16 L75,15
R #8A5A3C - 0 1 20 27 60 45 16
R face - 0 1 27 37 46 25 12
R #F2E6C9 - 0 1 22 62 56 7 3.5
E #E5484D - 0 1 50 56 3.4 3.4

@ fantasmino | completo fantasmino | ghost outfit | outfit | 1
P face #C9CED8 1.5 1 M20,52 Q20,22 50,21 Q80,22 80,52 L83,78 Q77,72 72,78 Q66,72 61,78 Q55,72 50,78 Q45,72 39,78 Q34,72 28,78 Q23,72 17,78 Z
E eye - 0 1 50 61 3 4

@ strega | completo streghetta | witch outfit | outfit | 1
R #6E4DB0 - 0 1 20 27 60 45 16
R face - 0 1 27 37 46 25 12
R #F5C542 - 0 1 40 63 20 3 1.5
E #26222F #8E94A3 1 1 50 33 33 5.5
P #26222F #8E94A3 1 1 M34,32 Q40,16 59,3 Q56,18 66,32 Z
P #6E4DB0 - 0 1 M35,28 L65,28 L66,32 L34,32 Z
P #F5C542 - 0 1 M50,24 l1.3,2.6 l2.7,.5 l-2,2 l.5,2.8 l-2.5,-1.4 l-2.5,1.4 l.5,-2.8 l-2,-2 l2.7,-.5 Z

@ vampiro | completo vampiro | vampire outfit | outfit | 1
P #1B1A21 #8E94A3 1 1 M24,46 L9,26 L9,78 L27,72 Z
P #1B1A21 #8E94A3 1 1 M76,46 L91,26 L91,78 L73,72 Z
P #B3262E - 0 1 M24,52 L14,37 L14,71 L26,68 Z
P #B3262E - 0 1 M76,52 L86,37 L86,71 L74,68 Z
P #1B1A21 #8E94A3 1 1 M22,41 Q22,24 50,24 Q78,24 78,41 Q70,32 63,34 L50,42 L37,34 Q30,32 22,41 Z
P - #2A2D34 1.6 1 M42,60 Q50,64 58,60
P #FFFFFF - 0 1 M44,61 L46,66 L48,62 Z
P #FFFFFF - 0 1 M52,62 L54,66 L56,61 Z

@ gatto-nero | completo gatto nero | black cat outfit | outfit | 1
P #2D3038 #8E94A3 1.2 1 M24,42 L26,13 L45,31 Z
P #2D3038 #8E94A3 1.2 1 M76,42 L74,13 L55,31 Z
P #FF9DB0 - 0 1 M28,35 L28.5,21 L38,30 Z
P #FF9DB0 - 0 1 M72,35 L71.5,21 L62,30 Z
R #2D3038 #8E94A3 1.2 1 20 27 60 45 16
R face - 0 1 27 37 46 25 12
P #FF9DB0 - 0 1 M47,56 L53,56 L50,59.5 Z
P - #C9CED8 1 .9 M29,57 L16,54 M29,61 L16,63 M71,57 L84,54 M71,61 L84,63

@ ape | completo ape | bee outfit | outfit | 1
E #CFE8FF #8EB8E6 1 .9 29 24 11 7
E #CFE8FF #8EB8E6 1 .9 71 24 11 7
P - #2A2D34 2 1 M42,31 Q40,19 34,13 M58,31 Q60,19 66,13
E #2A2D34 - 0 1 34 13 2.6 2.6
E #2A2D34 - 0 1 66 13 2.6 2.6
R #FFD25A - 0 1 20 27 60 45 16
R face - 0 1 27 37 46 25 12
R #2A2D34 - 0 1 25 63 50 4 0
R #2A2D34 - 0 1 28 68.5 44 3 0

@ fiorellino | completo fiorellino | flower outfit | outfit | 1
R #FFD6E8 - 0 1 20 27 60 45 16
R face - 0 1 27 37 46 25 12
P #6FCF7F - 0 1 M24,66 Q30,59 37,66 Q30,73 24,66 Z
P #6FCF7F - 0 1 M76,66 Q70,59 63,66 Q70,73 76,66 Z
R #6FCF7F - 0 1 36 67.5 28 4 2
E #FF8FC7 - 0 1 50 14 6.5 6.5
E #FF8FC7 - 0 1 57.6 19.5 6.5 6.5
E #FF8FC7 - 0 1 54.7 28.4 6.5 6.5
E #FF8FC7 - 0 1 45.3 28.4 6.5 6.5
E #FF8FC7 - 0 1 42.4 19.5 6.5 6.5
E #FFE28A - 0 1 50 22 4.2 4.2

@ coniglietto | completo coniglietto | bunny outfit | outfit | 1
R #FFFFFF #D4D8E0 1.2 1 33 5 12 33 6
R #FFFFFF #D4D8E0 1.2 1 55 5 12 33 6
R #FFB6CF - 0 1 36.5 10 5 23 2.5
R #FFB6CF - 0 1 58.5 10 5 23 2.5
R #FFFFFF #D4D8E0 1.2 1 20 27 60 45 16
R face - 0 1 27 37 46 25 12
E #FFB6CF - 0 1 50 56 2.8 2.2
E #FFB6CF - 0 1 78 71 7 7

@ farfalla | completo farfalla | butterfly outfit | outfit | 1
P #BFE3FF #F5D66B 1.5 1 M24,46 Q2,20 4,43 Q6,52 24,57 Z
P #BFE3FF #F5D66B 1.5 1 M24,55 Q8,59 11,73 Q22,73 27,63 Z
P #BFE3FF #F5D66B 1.5 1 M76,46 Q98,20 96,43 Q94,52 76,57 Z
P #BFE3FF #F5D66B 1.5 1 M76,55 Q92,59 89,73 Q78,73 73,63 Z
R #FFB3D9 - 0 1 20 27 60 45 16
R face - 0 1 27 37 46 25 12
P - #D14A97 2 1 M42,29 Q40,17 34,12 M58,29 Q60,17 66,12
E #D14A97 - 0 1 34 12 2.5 2.5
E #D14A97 - 0 1 66 12 2.5 2.5
""";
}
