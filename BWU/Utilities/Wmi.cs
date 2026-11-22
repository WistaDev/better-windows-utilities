using System.Management;

namespace BWU.Utilities;

internal static class Wmi
{
    /// <summary>Runs a WQL query and returns every row as a case-insensitive dictionary. Returns an empty list on failure.</summary>
    public static List<Dictionary<string, object?>> Query(string query, string scope = @"root\cimv2")
    {
        var rows = new List<Dictionary<string, object?>>();
        try
        {
            using var searcher = new ManagementObjectSearcher(scope, query);
            using var results = searcher.Get();
            foreach (ManagementBaseObject mo in results)
            {
                var d = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (PropertyData p in mo.Properties) d[p.Name] = p.Value;
                rows.Add(d);
                mo.Dispose();
            }
        }
        catch { /* WMI unavailable or access denied: callers show "Unavailable" */ }
        return rows;
    }

    public static string? Str(this Dictionary<string, object?> d, string key)
        => d.TryGetValue(key, out var v) ? v?.ToString()?.Trim() : null;

    public static ulong? Num(this Dictionary<string, object?> d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v is not IConvertible c) return null;
        try { return Convert.ToUInt64(c); } catch { return null; }
    }

    public static DateTime? Date(this Dictionary<string, object?> d, string key)
    {
        var s = d.Str(key);
        if (string.IsNullOrEmpty(s)) return null;
        try { return ManagementDateTimeConverter.ToDateTime(s); } catch { return null; }
    }
}
