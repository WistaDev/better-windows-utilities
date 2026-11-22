using System.Text.Json;

namespace BWU.Services;

public sealed record BackupEntry(bool Existed, string Kind, string? Data);

/// <summary>
/// Persistent store of original values captured before BWU changes anything
/// (%APPDATA%\BWU\backups\backup.json). The first captured original is never overwritten,
/// so reverting always returns to the true original state.
/// </summary>
public sealed class BackupStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BWU", "backups", "backup.json");

    private readonly object _lock = new();
    private readonly Dictionary<string, BackupEntry> _items;

    public static string BackupDirectory => Path.GetDirectoryName(FilePath)!;

    public BackupStore() => _items = Load();

    public bool Has(string id) { lock (_lock) return _items.ContainsKey(id); }
    public BackupEntry? Get(string id) { lock (_lock) return _items.TryGetValue(id, out var e) ? e : null; }

    public void AddIfMissing(string id, BackupEntry entry)
    {
        lock (_lock)
        {
            if (_items.ContainsKey(id)) return;
            _items[id] = entry;
            Save();
        }
    }

    public void Remove(string id)
    {
        lock (_lock)
        {
            if (_items.Remove(id)) Save();
        }
    }

    private static Dictionary<string, BackupEntry> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Dictionary<string, BackupEntry>>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { /* corrupt file: start clean, never block the app */ }
        return new();
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, FilePath, true);
    }
}
