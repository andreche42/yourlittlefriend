using NAudio.Wave;

namespace YourLittleFriend;

// ascolta quello che esce dalle casse (wasapi loopback) e stima il battito, così la mascotte balla a tempo.
// non si salva niente: si guarda solo quanto è forte l'audio, pezzetto per pezzetto, e si butta via tutto.
public sealed class Beat : IDisposable
{
    public event Action<double>? OnBeat;   // battito trovato, con i bpm stimati finora
    public event Action? OnPeak;           // il pezzo "si apre" (il ritornello): energia alta per un po'

    WasapiLoopbackCapture? cap;
    WaveFormat? fmt;   // formato dell'audio in arrivo (float o 16 bit, dipende dal dispositivo)
    readonly object gate = new();
    readonly List<(DateTime At, double Level)> hist = new();   // energia degli ultimi secondi
    readonly List<DateTime> beats = new();                     // ultimi battiti trovati
    double bpm;
    DateTime lastPeak = DateTime.MinValue;

    public bool Running => cap != null;

    // true se riesce ad ascoltare (con cuffie e casse spente non si sente niente: si torna al ballo di sempre)
    public bool Start()
    {
        if (cap != null) return true;
        WasapiLoopbackCapture? c = null;
        try
        {
            c = new WasapiLoopbackCapture();
            fmt = c.WaveFormat;
            c.DataAvailable += Data;
            c.RecordingStopped += (_, _) => Stop();
            c.StartRecording();
            cap = c;
            return true;
        }
        catch
        {
            try { c?.Dispose(); } catch { }   // niente dispositivo (o driver strano): non resta niente appeso
            Stop();
            return false;
        }
    }

    public void Stop()
    {
        var c = cap;
        cap = null;
        if (c == null) return;
        try { c.DataAvailable -= Data; c.StopRecording(); } catch { }
        try { c.Dispose(); } catch { }
        lock (gate) { hist.Clear(); beats.Clear(); bpm = 0; }
    }

    public void Dispose() => Stop();

    // l'audio arriva dal thread di windows: qui si guarda un pezzetto per volta (~20 ms)
    void Data(object? s, WaveInEventArgs e)
    {
        if (fmt is not { } f) return;
        int span = Math.Max(4, f.AverageBytesPerSecond / 50);
        for (int off = 0; off + span <= e.BytesRecorded; off += span)
            Feed(Rms(e.Buffer, off, span, f));
    }

    // volume del pezzetto (0..1 circa)
    static double Rms(byte[] b, int off, int len, WaveFormat f)
    {
        if (f.Encoding == WaveFormatEncoding.IeeeFloat && f.BitsPerSample == 32)
        {
            int n = len / 4;
            double sum = 0;
            for (int i = 0; i < n; i++) { double v = BitConverter.ToSingle(b, off + i * 4); sum += v * v; }
            return n == 0 ? 0 : Math.Sqrt(sum / n);
        }
        if (f.BitsPerSample == 16)
        {
            int n = len / 2;
            double sum = 0;
            for (int i = 0; i < n; i++) { double v = BitConverter.ToInt16(b, off + i * 2) / 32768.0; sum += v * v; }
            return n == 0 ? 0 : Math.Sqrt(sum / n);
        }
        return 0;
    }

    void Feed(double level)
    {
        var now = DateTime.UtcNow;
        double? found = null;
        bool peak = false;
        lock (gate)
        {
            hist.Add((now, level));
            while (hist.Count > 0 && now - hist[0].At > TimeSpan.FromSeconds(6)) hist.RemoveAt(0);
            if (hist.Count < 25) return;   // meno di mezzo secondo: ancora non si capisce niente

            // media "lenta" dell'ultimo ~1,5 s: un colpo è un salto netto sopra la media
            double slow = 0;
            int n = Math.Min(hist.Count, 75);
            for (int i = hist.Count - n; i < hist.Count; i++) slow += hist[i].Level;
            slow /= n;
            double prev = hist[^2].Level;

            if (slow > .002 && level > slow * 1.45 && level > .008 && prev < level && (beats.Count == 0 || (now - beats[^1]).TotalSeconds > .22))
            {
                beats.Add(now);
                while (beats.Count > 10) beats.RemoveAt(0);
                if (beats.Count >= 4)
                {
                    var gaps = new List<double>(beats.Count - 1);
                    for (int i = 1; i < beats.Count; i++) gaps.Add((beats[i] - beats[i - 1]).TotalSeconds);
                    gaps.Sort();
                    double med = gaps[gaps.Count / 2];   // mediana: un colpo perso non rovina la stima
                    if (med is > .25 and < 1.3)
                    {
                        double b = 60 / med;
                        bpm = bpm == 0 ? b : bpm * .7 + b * .3;   // si muove piano, senza saltare a ogni colpo
                        found = bpm;
                    }
                }
            }

            // ritornello: gli ultimi 2 s sono molto più forti degli ultimi 6 (e non succede troppo spesso)
            if (now - lastPeak > TimeSpan.FromSeconds(10) && hist.Count > 100)
            {
                double recent = 0;
                for (int i = hist.Count - 100; i < hist.Count; i++) recent += hist[i].Level;
                recent /= 100;
                double all = 0;
                foreach (var h in hist) all += h.Level;
                all /= hist.Count;
                if (recent > all * 1.35 && recent > .02) { lastPeak = now; peak = true; }
            }
        }
        if (found is double b2) OnBeat?.Invoke(b2);
        if (peak) OnPeak?.Invoke();
    }
}
