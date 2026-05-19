using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ReadTray.Core;

namespace ReadTray.Infrastructure;

public sealed class GitHubReleaseUpdateService : IUpdateService
{
    private const string Owner = "cwbrandsdal";
    private const string Repository = "readtray";
    private readonly ILogger<GitHubReleaseUpdateService> _logger;
    private readonly HttpClient _httpClient = new();

    public GitHubReleaseUpdateService(ILogger<GitHubReleaseUpdateService> logger)
    {
        _logger = logger;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ReadTray-update-checker");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct)
    {
        var current = GetCurrentVersion();
        var url = $"https://api.github.com/repos/{Owner}/{Repository}/releases/latest";
        _logger.LogInformation("Checking for updates. CurrentVersion={CurrentVersion} Url={Url}", current, url);

        using var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Update check failed. StatusCode={StatusCode} Body={Body}", (int)response.StatusCode, body);
            return new UpdateCheckResult(false, current, null, null, null, null, $"GitHub returned {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
        var releaseUrl = root.GetProperty("html_url").GetString();
        var latest = NormalizeVersion(tag);
        var asset = FindWindowsAsset(root);
        var updateAvailable = TryParseVersion(latest, out var latestVersion)
            && TryParseVersion(current, out var currentVersion)
            && latestVersion > currentVersion;

        _logger.LogInformation("Update check completed. CurrentVersion={CurrentVersion} LatestVersion={LatestVersion} UpdateAvailable={UpdateAvailable} Asset={AssetName}",
            current,
            latest,
            updateAvailable,
            asset?.Name);

        return new UpdateCheckResult(
            updateAvailable,
            current,
            latest,
            releaseUrl,
            asset?.DownloadUrl,
            asset?.Name,
            updateAvailable ? $"ReadTray {latest} is available." : "ReadTray is up to date.");
    }

    private static string GetCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            return informational.Split('+')[0];
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static string NormalizeVersion(string tag)
    {
        return tag.Trim().TrimStart('v', 'V');
    }

    private static bool TryParseVersion(string value, out Version version)
    {
        var normalized = NormalizeVersion(value);
        var dash = normalized.IndexOf('-');
        if (dash >= 0)
        {
            normalized = normalized[..dash];
        }

        return Version.TryParse(normalized, out version!);
    }

    private static ReleaseAsset? FindWindowsAsset(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        ReleaseAsset? fallback = null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            var downloadUrl = asset.GetProperty("browser_download_url").GetString();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(downloadUrl))
            {
                continue;
            }

            var candidate = new ReleaseAsset(name, downloadUrl);
            fallback ??= candidate;
            if (name.Contains("win", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return fallback;
    }

    private sealed record ReleaseAsset(string Name, string DownloadUrl);
}
