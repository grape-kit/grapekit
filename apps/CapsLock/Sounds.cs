using System.Runtime.InteropServices;

namespace CapsGosterge;

/// <summary>
/// Sesleri sırayla çalan küçük oynatıcı. İş parçacığı yalnızca ses çalarken yaşar,
/// 10 sn boş kalınca kendini kapatır (arka planda kaynak tutmaz).
/// </summary>
internal sealed class AudioPlayer : IDisposable
{
    [DllImport("winmm.dll")] static extern bool PlaySound(IntPtr data, IntPtr hmod, uint flags);
    const uint SND_SYNC = 0, SND_MEMORY = 0x4, SND_NODEFAULT = 0x2;

    readonly object _lock = new();
    readonly Queue<byte[]> _queue = new();
    readonly AutoResetEvent _signal = new(false);
    Thread? _thread;
    bool _disposed;

    public void Play(params byte[]?[] clips)
    {
        lock (_lock) _queue.Clear();
        PlaySound(IntPtr.Zero, IntPtr.Zero, 0); // çalanı durdur
        lock (_lock)
        {
            foreach (var c in clips) if (c is { Length: > 44 }) _queue.Enqueue(c);
            if (_queue.Count == 0) return;
            if (_thread == null)
            {
                _thread = new Thread(Worker) { IsBackground = true, Name = "Ses" };
                _thread.Start();
            }
        }
        _signal.Set();
    }

    void Worker()
    {
        while (true)
        {
            byte[]? clip;
            lock (_lock)
            {
                if (_disposed) { _thread = null; return; }
                _queue.TryDequeue(out clip);
            }
            if (clip == null)
            {
                if (_signal.WaitOne(10_000)) continue;
                lock (_lock)
                {
                    if (_queue.Count > 0) continue;
                    _thread = null;
                    return;
                }
            }
            var h = GCHandle.Alloc(clip, GCHandleType.Pinned);
            try { PlaySound(h.AddrOfPinnedObject(), IntPtr.Zero, SND_MEMORY | SND_SYNC | SND_NODEFAULT); }
            catch { }
            finally { h.Free(); }
        }
    }

    public void Dispose()
    {
        lock (_lock) { _disposed = true; _queue.Clear(); }
        PlaySound(IntPtr.Zero, IntPtr.Zero, 0);
        _signal.Set();
    }
}

/// <summary>Ses paketleri: hepsi program içinde sentezlenir, dosya gerekmez.</summary>
internal static class SoundBank
{
    public const int PackCount = 7, CustomPack = 6;
    const int Sr = 44100;

    static readonly Dictionary<string, byte[]> Cache = [];

    /// <summary>Açılma/kapanma sesi. Num/Scroll Lock için perde biraz kaydırılır ki ayırt edilsin.</summary>
    public static byte[]? Get(int pack, bool on, LockKey key, int volumePercent, string customOn, string customOff)
    {
        double amp = Math.Pow(Math.Clamp(volumePercent, 0, 100) / 100.0, 1.6) * 0.75;
        if (amp <= 0.001) return null;

        if (pack == CustomPack)
        {
            string path = on ? customOn : customOff;
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length < 5_000_000)
                    return Wav.Scale(File.ReadAllBytes(path), volumePercent / 100.0);
            }
            catch { }
            pack = 0; // dosya yoksa yumuşak pakete düş
        }

        string k = $"{pack}|{on}|{key}|{volumePercent}";
        lock (Cache)
        {
            if (Cache.TryGetValue(k, out var hit)) return hit;
            double pitch = key switch { LockKey.Num => 1.26, LockKey.Scroll => 0.84, _ => 1.0 };
            var data = Wav.Encode(Synth(pack, on, pitch), amp);
            if (Cache.Count > 64) Cache.Clear();
            Cache[k] = data;
            return data;
        }
    }

    /// <summary>Uyarı sesi (şifre alanı / koruma ipucu): iki kısa, aynı perdede ton.</summary>
    public static byte[]? Warning(int volumePercent)
    {
        double amp = Math.Pow(Math.Clamp(volumePercent, 0, 100) / 100.0, 1.6) * 0.6;
        if (amp <= 0.001) return null;
        var buf = new double[(int)(Sr * 0.36)];
        Tone(buf, 0, 880, 0.12, 30, harm: 0.15);
        Tone(buf, (int)(Sr * 0.15), 880, 0.18, 22, harm: 0.15);
        return Wav.Encode(buf, amp);
    }

    static double[] Synth(int pack, bool on, double p)
    {
        switch (pack)
        {
            case 1: // Zil
            {
                var f = on ? new[] { 784.0, 1175.0 } : [1175.0, 784.0];
                var b = new double[(int)(Sr * 0.95)];
                for (int i = 0; i < 2; i++) Bell(b, (int)(i * 0.12 * Sr), f[i] * p);
                return b;
            }
            case 2: // Tık
            {
                var b = new double[(int)(Sr * 0.2)];
                if (on)
                {
                    Click(b, 0, 2400 * p, 0.018);
                    Click(b, (int)(0.07 * Sr), 3000 * p, 0.018);
                }
                else Click(b, 0, 1100 * p, 0.03);
                return b;
            }
            case 3: // Piyano
            {
                var f = on ? new[] { 523.25, 659.25, 783.99 } : [783.99, 659.25, 523.25];
                var b = new double[(int)(Sr * 0.85)];
                for (int i = 0; i < 3; i++) Piano(b, (int)(i * 0.075 * Sr), f[i] * p);
                return b;
            }
            case 4: // Mekanik klavye
            {
                var b = new double[(int)(Sr * 0.22)];
                var rnd = new Random(on ? 7 : 11);
                if (on)
                {
                    Clack(b, 0, 1.0 * p, rnd);
                    Clack(b, (int)(0.075 * Sr), 1.35 * p, rnd);
                }
                else Clack(b, 0, 0.7 * p, rnd);
                return b;
            }
            case 5: // Damla
            {
                var b = new double[(int)(Sr * 0.4)];
                if (on) Drop(b, 600 * p, 1500 * p);
                else Drop(b, 1400 * p, 480 * p);
                return b;
            }
            default: // Yumuşak
            {
                var f = on ? new[] { 659.25, 987.77 } : [783.99, 523.25];
                var b = new double[(int)(Sr * 0.45)];
                for (int i = 0; i < 2; i++) Tone(b, (int)(i * 0.095 * Sr), f[i] * p, 0.34, 11, harm: 0.2);
                return b;
            }
        }
    }

    static void Tone(double[] b, int start, double f, double len, double decay, double harm)
    {
        int n = Math.Min((int)(len * Sr), b.Length - start);
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)Sr;
            double env = Math.Min(1, t / 0.005) * Math.Exp(-t * decay);
            b[start + i] += env * (Math.Sin(2 * Math.PI * f * t)
                + harm * Math.Sin(4 * Math.PI * f * t) * Math.Exp(-t * 25)
                + 0.05 * Math.Sin(6 * Math.PI * f * t) * Math.Exp(-t * 35));
        }
    }

    static void Bell(double[] b, int start, double f)
    {
        double[] ratio = [1, 2.0, 2.76, 5.4], gain = [1, 0.45, 0.3, 0.15], dec = [5, 7, 9, 14];
        int n = b.Length - start;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)Sr, atk = Math.Min(1, t / 0.003), v = 0;
            for (int h = 0; h < ratio.Length; h++) v += gain[h] * Math.Sin(2 * Math.PI * f * ratio[h] * t) * Math.Exp(-t * dec[h]);
            b[start + i] += atk * v;
        }
    }

    static void Piano(double[] b, int start, double f)
    {
        int n = Math.Min((int)(0.6 * Sr), b.Length - start);
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)Sr, atk = Math.Min(1, t / 0.004), v = 0;
            for (int h = 1; h <= 6; h++) v += Math.Sin(2 * Math.PI * f * h * t) / (h * h * 0.7 + 0.3) * Math.Exp(-t * (4 + h * 2.5));
            b[start + i] += atk * v;
        }
    }

    static void Click(double[] b, int start, double f, double len)
    {
        int n = Math.Min((int)(len * Sr), b.Length - start);
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)Sr;
            b[start + i] += Math.Sin(2 * Math.PI * f * t) * Math.Exp(-t * 180) + 0.3 * Math.Sin(2 * Math.PI * f * 0.5 * t) * Math.Exp(-t * 120);
        }
    }

    static void Clack(double[] b, int start, double p, Random rnd)
    {
        int n = Math.Min((int)(0.06 * Sr), b.Length - start);
        double prev = 0;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)Sr;
            double noise = rnd.NextDouble() * 2 - 1;
            double hp = noise - prev * 0.6; prev = noise; // basit tiz geçiren
            b[start + i] += 0.55 * hp * Math.Exp(-t * 160 / p)
                + 0.8 * Math.Sin(2 * Math.PI * 180 * p * t) * Math.Exp(-t * 70)
                + 0.35 * Math.Sin(2 * Math.PI * 1900 * p * t) * Math.Exp(-t * 220);
        }
    }

    static void Drop(double[] b, double f0, double f1)
    {
        double phase = 0;
        for (int i = 0; i < b.Length; i++)
        {
            double t = i / (double)Sr;
            double k = Math.Min(1, t / 0.09);
            double f = f0 * Math.Pow(f1 / f0, k);
            phase += 2 * Math.PI * f / Sr;
            b[i] += Math.Min(1, t / 0.004) * Math.Exp(-t * 13) * Math.Sin(phase);
        }
    }
}

internal static class Wav
{
    const int Sr = 44100;

    public static byte[] Encode(double[] buf, double amp)
    {
        double peak = 0;
        foreach (var v in buf) peak = Math.Max(peak, Math.Abs(v));
        if (peak <= 0) peak = 1;
        int fade = Sr / 100, n = buf.Length;
        using var ms = new MemoryStream(44 + n * 2);
        using (var w = new BinaryWriter(ms, System.Text.Encoding.ASCII, true))
        {
            WriteHeader(w, n * 2, Sr, 1, 16);
            for (int i = 0; i < n; i++)
            {
                double g = i > n - fade ? (n - i) / (double)fade : 1.0;
                w.Write((short)Math.Round(Math.Clamp(buf[i] / peak * amp * g, -1, 1) * short.MaxValue));
            }
        }
        return ms.ToArray();
    }

    static void WriteHeader(BinaryWriter w, int dataLen, int sr, short ch, short bits)
    {
        w.Write("RIFF"u8); w.Write(36 + dataLen); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write(ch);
        w.Write(sr); w.Write(sr * ch * bits / 8); w.Write((short)(ch * bits / 8)); w.Write(bits);
        w.Write("data"u8); w.Write(dataLen);
    }

    /// <summary>16 bit PCM WAV verisinin ses düzeyini değiştirir; desteklenmeyen biçimde veriyi olduğu gibi döndürür.</summary>
    public static byte[] Scale(byte[] wav, double gain)
    {
        try
        {
            if (wav.Length < 44 || wav[0] != 'R' || wav[8] != 'W') return wav;
            int pos = 12, bits = 0, fmt = 0;
            var copy = (byte[])wav.Clone();
            while (pos + 8 <= copy.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(copy, pos, 4);
                int size = BitConverter.ToInt32(copy, pos + 4);
                if (id == "fmt ")
                {
                    fmt = BitConverter.ToInt16(copy, pos + 8);
                    bits = BitConverter.ToInt16(copy, pos + 22);
                }
                else if (id == "data")
                {
                    if (fmt != 1 || bits != 16) return wav;
                    int end = Math.Min(copy.Length, pos + 8 + size);
                    for (int i = pos + 8; i + 1 < end; i += 2)
                    {
                        int s = (int)Math.Round(BitConverter.ToInt16(copy, i) * gain);
                        s = Math.Clamp(s, short.MinValue, short.MaxValue);
                        copy[i] = (byte)(s & 0xFF);
                        copy[i + 1] = (byte)((s >> 8) & 0xFF);
                    }
                    return copy;
                }
                pos += 8 + size + (size & 1);
            }
        }
        catch { }
        return wav;
    }
}
