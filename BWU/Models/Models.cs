using BWU.Utilities;

namespace BWU.Models;

public sealed record InfoItem(string Label, string Value);
public sealed record InfoSection(string Title, IReadOnlyList<InfoItem> Items);
public sealed record OperationResult(bool Success, string Message)
{
    public static OperationResult Ok(string message) => new(true, message);
    public static OperationResult Fail(string message) => new(false, message);
}
public sealed record ShortcutItem(string Key, string Title, string Description, string Glyph);

public sealed class StartupEntry
{
    public required string Name { get; init; }
    public required string Command { get; init; }
    public required string Location { get; init; }
    public required bool IsMachine { get; init; }
    public required string ApprovedKeyName { get; init; }   // "Run" or "Run32"
    public bool IsEnabled { get; set; }
    public string? Hint { get; init; }
}

public sealed class ServiceInfo
{
    public required string Name { get; init; }
    public required string Title { get; init; }
    public required string Explanation { get; init; }
    public required string StartType { get; init; }
    public required string Status { get; init; }
    public required bool IsAutomatic { get; init; }
    public bool HasBackup { get; init; }
}

public enum AppCategory { Optional, Other, Protected }

public sealed class AppxItem : ObservableObject
{
    private bool _isSelected;
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required string PackageFullName { get; init; }
    public required string Publisher { get; init; }
    public required string Version { get; init; }
    public required AppCategory Category { get; init; }
    public required string Note { get; init; }
    public bool CanSelect => Category != AppCategory.Protected;
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value && CanSelect); }
    public string CategoryText => Category switch
    {
        AppCategory.Optional => "Optional",
        AppCategory.Protected => "Protected",
        _ => "Other app"
    };
}

public sealed class PackageItem
{
    public required string Name { get; init; }
    public required string Id { get; init; }
    public string Version { get; init; } = "";
}

public sealed record OptimizationProfile(string Name, string Description, IReadOnlyList<string> TweakIds);
public sealed record SummaryItem(string Operation, bool Success, string Detail);
