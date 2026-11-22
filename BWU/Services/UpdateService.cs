using System.Net.Http;
using System.Text.Json;

namespace BWU.Services;

public sealed record UpdateCheckResult(bool Success, bool UpdateAvailable, string Message, string? DownloadUrl);

/// <summary>
/// Checks a JSON manifest over HTTPS: { "version": "1.1", "url": "https://...", "notes": "..." }.
/// No default URL is shipped, because BWU does not ship with a hosted update server.
/// </summary>
public sealed class UpdateService
{
    public async Task<UpdateCheckResult> CheckAsync(string manifestUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(manifestUrl))
            return new(false, false, "No update source is configured. Enter an HTTPS update manifest URL above.", null);
        if (!Uri.TryCreate(manifestUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return new(false, false, "The update source must be a valid HTTPS URL.", null);

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"BWU/{AppInfo.Version}");
            var json = await http.GetStringAsync(uri, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var versionText = root.GetProperty("version").GetString() ?? "";
            if (!Version.TryParse(versionText, out var latest))
                return new(false, false, $"The manifest contains an invalid version: '{versionText}'.", null);
            var url = root.TryGetProperty("url", out var u) ? u.GetString() : null;
            if (url != null && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) url = null;

            return latest > AppInfo.VersionObject
                ? new(true, true, $"Version {latest} is available (you have {AppInfo.Version}).", url)
                : new(true, false, $"You are up to date (version {AppInfo.Version}).", null);
        }
        catch (Exception ex)
        {
            return new(false, false, "Could not check for updates: " + ex.Message, null);
        }
    }
}
