using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace YourLittleFriend;

// reazioni di sistema: la cpu che scalda troppo e la batteria scarica.
// si guarda ogni tanto (non in continuazione) e si restituisce una reazione da far vedere all'omino.
public static class SystemWatch
{
    [DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out PowerStatus s);
    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);

    [StructLayout(LayoutKind.Sequential)]
    struct PowerStatus
    {
        public byte AC, BatteryFlag, LifePercent, Reserved;
        public int LifeTime, FullLifeTime;
    }

    const double HotC = 80;          // gradi oltre i quali la cpu "scalda troppo"
    const double HotLoad = .90;      // se la temperatura non si riesce a leggere si guarda il carico
    const double LowBattery = 20;    // percento sotto il quale la batteria è "scarica"

    static Computer? pc;             // sensori della cpu (librehardwaremonitor)
    static bool noSensors;           // la libreria non riesce a leggere niente su questo pc
    static double? temperature;
    static double load;
    static (ulong Idle, ulong Kernel, ulong User) lastCpu;
    static int? battery;
    static bool charging = true;
    static bool wasHot, wasLow;
    static DateTime hotAt = DateTime.MinValue, lowAt = DateTime.MinValue;

    public static double? Temperature => temperature;

    // la prossima reazione da mostrare (None = niente da dire)
    public static MascotReaction Next()
    {
        var now = DateTime.UtcNow;

        // batteria: sotto la soglia e senza caricabatterie. una volta ogni ora, non a ogni controllo
        ReadPower();
        bool low = battery is int b && b < LowBattery && !charging;
        if (low && !wasLow && now - lowAt > TimeSpan.FromHours(1)) { wasLow = true; lowAt = now; return MascotReaction.Battery; }
        if (!low) wasLow = false;

        // cpu: prima la temperatura vera, se il pc la espone; altrimenti il carico
        double? t = ReadTemperature();
        bool hot = t is double temp ? temp >= HotC : ReadLoad() >= HotLoad;
        if (hot && !wasHot && now - hotAt > TimeSpan.FromMinutes(3)) { wasHot = true; hotAt = now; return MascotReaction.Hot; }
        if (!hot) wasHot = false;

        return MascotReaction.None;
    }

    // ---- batteria ----
    static void ReadPower()
    {
        try
        {
            if (!GetSystemPowerStatus(out var s)) return;
            battery = s.BatteryFlag == 128 || s.LifePercent > 100 ? null : s.LifePercent;   // 128 = niente batteria (fisso)
            charging = s.AC == 1;
        }
        catch { }
    }

    // ---- cpu ----
    // la temperatura arriva dai sensori della scheda madre/cpu: serve la libreria e, su molti pc, i permessi di amministratore.
    // se non si legge niente si guarda il carico: l'omino suda lo stesso, solo con una stima.
    static double? ReadTemperature()
    {
        if (noSensors) return null;
        try
        {
            if (pc == null)
            {
                var c = new Computer { IsCpuEnabled = true };
                c.Open();
                pc = c;
            }
            double? best = null, any = null;
            foreach (var hw in pc.Hardware)
            {
                if (hw.HardwareType != HardwareType.Cpu) continue;
                try { hw.Update(); } catch { }
                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType != SensorType.Temperature || s.Value is not float v || v <= 0 || v > 130) continue;
                    string n = s.Name.ToLowerInvariant();
                    bool pack = n.Contains("package") || n.Contains("tctl") || n.Contains("tdie") || n.Contains("core max");
                    if (pack) best = best is double pb ? Math.Max(pb, v) : v;
                    else any = any is double ab ? Math.Max(ab, v) : v;
                }
            }
            temperature = best ?? any;
            return temperature;
        }
        catch
        {
            noSensors = true;   // su questo pc non c'è modo di leggere la temperatura: si passa al carico
            pc = null;
            return null;
        }
    }

    // carico della cpu dall'ultimo controllo (0..1)
    static double ReadLoad()
    {
        try
        {
            if (!GetSystemTimes(out ulong idle, out ulong kernel, out ulong user)) return load;
            if (lastCpu == default) { lastCpu = (idle, kernel, user); return load; }
            ulong di = idle - lastCpu.Idle, dk = kernel - lastCpu.Kernel, du = user - lastCpu.User;
            lastCpu = (idle, kernel, user);
            ulong total = dk + du;   // i tempi del kernel comprendono quelli di inattività
            if (total == 0) return load;
            load = Math.Clamp((double)(total - di) / total, 0, 1);
            return load;
        }
        catch { return load; }
    }
}
