using Windows.Media.SpeechSynthesis;

namespace CapsGosterge;

/// <summary>
/// Windows'un kendi konuşma motoruyla "Büyük harf" / "Küçük harf" gibi kısa cümleleri seslendirir.
/// Üretilen ses bellekte saklanır; aynı cümle ikinci kez sentezlenmez.
/// </summary>
internal static class Speech
{
    static readonly Dictionary<string, byte[]> Cache = [];

    static string LangCode(int lang) => lang switch { 0 => "tr", 2 => "de", _ => "en" };

    public static VoiceInformation? VoiceFor(int lang)
    {
        try
        {
            var code = LangCode(lang);
            return SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.Language.StartsWith(code, StringComparison.OrdinalIgnoreCase));
        }
        catch { return null; }
    }

    public static string? VoiceName(int lang) => VoiceFor(lang)?.DisplayName;

    public static async Task<byte[]?> GetAsync(string text, int lang, int rate, int volumePercent)
    {
        string key = $"{lang}|{rate}|{volumePercent}|{text}";
        lock (Cache) if (Cache.TryGetValue(key, out var hit)) return hit;

        try
        {
            using var synth = new SpeechSynthesizer();
            var voice = VoiceFor(lang);
            if (voice != null) synth.Voice = voice;
            try { synth.Options.SpeakingRate = rate switch { 0 => 0.85, 2 => 1.45, _ => 1.15 }; } catch { }

            using var stream = await synth.SynthesizeTextToStreamAsync(text);
            using var input = stream.AsStreamForRead();
            using var ms = new MemoryStream();
            await input.CopyToAsync(ms);
            var data = Wav.Scale(ms.ToArray(), Math.Clamp(volumePercent, 0, 100) / 100.0 * 1.2);
            lock (Cache)
            {
                if (Cache.Count > 32) Cache.Clear();
                Cache[key] = data;
            }
            return data;
        }
        catch { return null; }
    }

    public static void Clear() { lock (Cache) Cache.Clear(); }
}
