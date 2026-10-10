using System.Text.Json;

namespace SalesWeb.Server.Services.B1;

/// <summary>Lỗi trả về từ Service Layer.</summary>
public sealed class B1Exception : Exception
{
    public B1Exception(string message) : base(message) { }
}

/// <summary>Helper đọc payload B1 thô (Dictionary&lt;string, object?&gt;).</summary>
public static class B1Json
{
    public static object? ToObject(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Object => el.EnumerateObject()
            .ToDictionary(p => p.Name, p => ToObject(p.Value), StringComparer.OrdinalIgnoreCase),
        JsonValueKind.Array => el.EnumerateArray().Select(ToObject).ToList(),
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? (object)l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    public static Dictionary<string, object?> ToDict(JsonElement el)
        => (Dictionary<string, object?>)ToObject(el)!;

    public static List<Dictionary<string, object?>> ToDictList(JsonElement el)
        => el.ValueKind == JsonValueKind.Array
            ? el.EnumerateArray().Select(ToDict).ToList()
            : new List<Dictionary<string, object?>>();

    public static string Str(Dictionary<string, object?> d, string key)
        => d.TryGetValue(key, out var v) && v is not null ? v.ToString()! : "";

    public static double Num(Dictionary<string, object?> d, string key, double dflt = 0)
    {
        if (!d.TryGetValue(key, out var v) || v is null) return dflt;
        try { return Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture); }
        catch { return dflt; }
    }

    public static int? IntN(Dictionary<string, object?> d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v is null) return null;
        try { return Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture); }
        catch { return null; }
    }

    public static long? LongN(Dictionary<string, object?> d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v is null) return null;
        try { return Convert.ToInt64(v, System.Globalization.CultureInfo.InvariantCulture); }
        catch { return null; }
    }

    public static List<Dictionary<string, object?>> DictList(Dictionary<string, object?> d, string key)
    {
        if (!d.TryGetValue(key, out var v)) return new List<Dictionary<string, object?>>();
        return v switch
        {
            List<Dictionary<string, object?>> l => l,
            List<object?> lo => lo.OfType<Dictionary<string, object?>>().ToList(),
            _ => new List<Dictionary<string, object?>>(),
        };
    }

    public static string EscapeOData(string value) => value.Replace("'", "''");

    public static Dictionary<string, object?> DeepClone(Dictionary<string, object?> d)
    {
        var json = JsonSerializer.Serialize(d);
        return ToDict(JsonDocument.Parse(json).RootElement);
    }
}
