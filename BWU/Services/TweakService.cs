using System.Security;
using BWU.Models;
using BWU.Utilities;
using Microsoft.Win32;

namespace BWU.Services;

public sealed record RegistryTweak(
    string Id,
    string Title,
    string Description,
    string Consequence,
    IReadOnlyList<RegistryValueSpec> Values,
    bool RequiresAdmin = false,
    int MinBuild = 0,
    string? DeleteKeyOnRevert = null,
    string? Note = null);

/// <summary>Applies and reverts registry-based settings with backup of the original values.</summary>
public sealed class TweakService
{
    private readonly RegistryService _reg;
    public TweakService(RegistryService reg) => _reg = reg;

    public bool IsSupported(RegistryTweak t) => WindowsService.IsSupportedOs && WindowsService.BuildNumber >= t.MinBuild;

    public bool IsApplied(RegistryTweak t)
    {
        try { return t.Values.All(_reg.ValueEquals); }
        catch { return false; }
    }

    public bool HasBackup(RegistryTweak t) => t.Values.Any(v => _reg.HasBackup(v.Hive, v.KeyPath, v.ValueName));

    public OperationResult Apply(RegistryTweak t)
    {
        if (!IsSupported(t)) return OperationResult.Fail("This setting is not supported on this version of Windows.");
        if (t.RequiresAdmin && !AdminHelper.IsAdministrator)
            return OperationResult.Fail("Administrator rights are required for this setting.");
        try
        {
            foreach (var v in t.Values) _reg.SetWithBackup(v.Hive, v.KeyPath, v.ValueName, v.Applied, v.Kind);
            return IsApplied(t)
                ? OperationResult.Ok("Applied." + (t.Note != null ? " " + t.Note : ""))
                : OperationResult.Fail("The value was written but Windows reports a different value. It may be controlled by policy.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException)
        {
            return OperationResult.Fail("Access denied. Run BWU as administrator for this setting.");
        }
        catch (Exception ex) { return OperationResult.Fail(ex.Message); }
    }

    public OperationResult Revert(RegistryTweak t)
    {
        if (t.RequiresAdmin && !AdminHelper.IsAdministrator)
            return OperationResult.Fail("Administrator rights are required to revert this setting.");
        try
        {
            var restored = 0;
            foreach (var v in t.Values)
                if (_reg.RestoreFromBackup(v.Hive, v.KeyPath, v.ValueName)) restored++;

            if (restored == 0)
                return OperationResult.Fail("No BWU backup exists for this setting, so BWU cannot restore it automatically.");

            if (t.DeleteKeyOnRevert != null)
                _reg.DeleteKeyTree(t.Values[0].Hive, t.DeleteKeyOnRevert);

            return OperationResult.Ok("Reverted to the original values." + (t.Note != null ? " " + t.Note : ""));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException)
        {
            return OperationResult.Fail("Access denied. Run BWU as administrator for this setting.");
        }
        catch (Exception ex) { return OperationResult.Fail(ex.Message); }
    }
}
