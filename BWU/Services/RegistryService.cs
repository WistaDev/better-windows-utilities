using Microsoft.Win32;

namespace BWU.Services;

public sealed record RegistryValueSpec(RegistryHive Hive, string KeyPath, string ValueName, RegistryValueKind Kind, object Applied);

/// <summary>Registry access with automatic backup of the original value before every change.</summary>
public sealed class RegistryService
{
    private readonly BackupStore _backups;
    public RegistryService(BackupStore backups) => _backups = backups;

    private static RegistryKey Base(RegistryHive hive) => RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
    private static string Id(RegistryHive h, string p, string n) => $"reg:{h}\\{p}\\{n}";

    public object? Read(RegistryHive hive, string path, string name)
    {
        using var bk = Base(hive);
        using var k = bk.OpenSubKey(path);
        return k?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
    }

    public bool ValueEquals(RegistryValueSpec spec)
    {
        var current = Read(spec.Hive, spec.KeyPath, spec.ValueName);
        return spec.Kind switch
        {
            RegistryValueKind.DWord => current is int c && spec.Applied is int a && c == a,
            _ => current is string s && string.Equals(s, spec.Applied?.ToString(), StringComparison.OrdinalIgnoreCase)
        };
    }

    public bool HasBackup(RegistryHive hive, string path, string name) => _backups.Has(Id(hive, path, name));

    private BackupEntry Capture(RegistryHive hive, string path, string name)
    {
        using var bk = Base(hive);
        using var k = bk.OpenSubKey(path);
        if (k == null || !k.GetValueNames().Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
            return new BackupEntry(false, "None", null);

        var kind = k.GetValueKind(name);
        var value = k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return kind switch
        {
            RegistryValueKind.DWord => new BackupEntry(true, "DWord", ((int)value!).ToString()),
            RegistryValueKind.String => new BackupEntry(true, "String", (string?)value ?? ""),
            RegistryValueKind.ExpandString => new BackupEntry(true, "ExpandString", (string?)value ?? ""),
            _ => throw new NotSupportedException($"Registry value type {kind} is not supported for safe backup.")
        };
    }

    public void SetWithBackup(RegistryHive hive, string path, string name, object value, RegistryValueKind kind)
    {
        _backups.AddIfMissing(Id(hive, path, name), Capture(hive, path, name));
        using var bk = Base(hive);
        using var k = bk.CreateSubKey(path, true);
        k.SetValue(name, value, kind);
    }

    /// <summary>Restores the backed-up original. Returns false when no backup exists.</summary>
    public bool RestoreFromBackup(RegistryHive hive, string path, string name)
    {
        var id = Id(hive, path, name);
        var entry = _backups.Get(id);
        if (entry == null) return false;

        using var bk = Base(hive);
        if (!entry.Existed)
        {
            using var existing = bk.OpenSubKey(path, true);
            existing?.DeleteValue(name, false);
        }
        else
        {
            using var k = bk.CreateSubKey(path, true);
            switch (entry.Kind)
            {
                case "DWord": k.SetValue(name, int.Parse(entry.Data!), RegistryValueKind.DWord); break;
                case "ExpandString": k.SetValue(name, entry.Data ?? "", RegistryValueKind.ExpandString); break;
                default: k.SetValue(name, entry.Data ?? "", RegistryValueKind.String); break;
            }
        }
        _backups.Remove(id);
        return true;
    }

    public void DeleteKeyTree(RegistryHive hive, string path)
    {
        using var bk = Base(hive);
        bk.DeleteSubKeyTree(path, false);
    }
}
