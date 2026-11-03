using System.Globalization;
using System.Speech.Recognition;

namespace YourLittleFriend;

// riconoscimento vocale di windows (quello del sistema, non un servizio online).
// parte solo quando premi il microfono e si spegne appena lo fermi: non resta mai in ascolto da solo.
public static class Voice
{
    static SpeechRecognitionEngine? engine;
    static Action<string>? sink;
    static bool starting;

    public static bool Listening { get; private set; }
    public static string? Error { get; private set; }
    public static event Action? Stopped;   // per far tornare normale l'icona del microfono

    public static bool Start(Action<string> onText)
    {
        Stop();
        Error = null;
        starting = true;
        try
        {
            // la lingua di windows: italiano o inglese, la stessa scelta nell'app
            var culture = CultureInfo.GetCultureInfo(Loc.En ? "en-US" : "it-IT");
            var e = new SpeechRecognitionEngine(culture);
            sink = onText;
            e.LoadGrammar(new DictationGrammar());
            e.SetInputToDefaultAudioDevice();
            e.SpeechRecognized += (_, ev) =>
            {
                var text = ev.Result?.Text?.Trim();
                if (!string.IsNullOrEmpty(text)) sink?.Invoke(text);
            };
            e.RecognizeCompleted += (_, _) => { if (!starting) Stop(); };
            e.RecognizeAsync(RecognizeMode.Multiple);
            engine = e;
            Listening = true;
            return true;
        }
        catch (Exception ex)
        {
            // succede se manca il pacchetto lingua di windows o non c'è un microfono
            Error = ex.Message;
            Stop();
            return false;
        }
        finally { starting = false; }
    }

    public static void Stop()
    {
        var e = engine;
        engine = null;
        Listening = false;
        sink = null;
        try { e?.RecognizeAsyncCancel(); } catch { }
        try { e?.Dispose(); } catch { }
        Stopped?.Invoke();
    }
}
