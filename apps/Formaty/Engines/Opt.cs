using System.Text.Json.Nodes;

namespace Formaty.Engines;

/// <summary>Arayüzden gelen seçenekleri (JSON) güvenle okur; eksik ya da yanlış türde değer varsayılana düşer.</summary>
public sealed class Opt(JsonObject? o)
{
    readonly JsonObject _o = o ?? [];

    public string S(string k, string def = "") => _o[k] is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0 ? s : def;
    public bool B(string k, bool def = false) => _o[k] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : def;
    public double N(string k, double def = 0)
    {
        if (_o[k] is not JsonValue v) return def;
        if (v.TryGetValue<double>(out var d)) return d;
        if (v.TryGetValue<int>(out var i)) return i;
        if (v.TryGetValue<long>(out var l)) return l;
        if (v.TryGetValue<float>(out var f)) return f;
        if (v.TryGetValue<decimal>(out var m)) return (double)m;
        if (v.TryGetValue<string>(out var s) && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) return d;
        return def;
    }
    public int I(string k, int def = 0) => (int)Math.Round(N(k, def));
    public Opt Sub(string k) => new(_o[k] as JsonObject);
    public bool Has(string k) => _o[k] != null;
    public JsonObject Raw => _o;
}
