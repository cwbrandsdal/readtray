using System.Reflection;
using Microsoft.Extensions.Logging;
using ReadTray.Core;
using Velopack;
using Velopack.Sources;

namespace ReadTray.Infrastructure;

public sealed class GitHubReleaseUpdateService : IUpdateService
{
    private const string RepositoryUrl = "https://github.com/cwbrandsdal/readtray";
    private readonly ILogger<GitHubReleaseUpdateService> _logger;

    public GitHubReleaseUpdateService(ILogger<GitHubReleaseUpdateService> logger) => _logger = logger;

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct)
    {
        var current = GetCurrentVersion();
        var manager = CreateUpdateManager();
        if (!manager.IsInstalled)
        {
            _logger.LogInformation("Skipping Velopack update check because the app is not installed by Velopack. CurrentVersion={CurrentVersion}", current);
            return new UpdateCheckResult(
                false,
                current,
                null,
                RepositoryUrl + "/releases/latest",
                null,
                null,
                "In-app updates are available after installing ReadTray with the setup installer.");
        }

        _logger.LogInformation("Checking for Velopack updates. CurrentVersion={CurrentVersion} Repository={Repository}", current, RepositoryUrl);
        var update = await manager.CheckForUpdatesAsync().WaitAsync(ct);
        return BuildResult(current, update, update is not null ? $"ReadTray {update.TargetFullRelease.Version} is available." : "ReadTray is up to date.");
    }

    public async Task<UpdateCheckResult> DownloadAndApplyLatestUpdateAsync(IProgress<int>? progress, CancellationToken ct)
    {
        var current = GetCurrentVersion();
        var manager = CreateUpdateManager();
        if (!manager.IsInstalled)
        {
            return new UpdateCheckResult(false, current, null, RepositoryUrl + "/releases/latest", null, null, "Install ReadTray with the setup installer before using in-app updates.");
        }

        var update = await manager.CheckForUpdatesAsync().WaitAsync(ct);
        if (update is null)
        {
            return new UpdateCheckResult(false, current, null, RepositoryUrl + "/releases/latest", null, null, "ReadTray is up to date.");
        }

        _logger.LogInformation("Downloading Velopack update. CurrentVersion={CurrentVersion} LatestVersion={LatestVersion} Package={Package}",
            current,
            update.TargetFullRelease.Version,
            update.TargetFullRelease.FileName);

        await manager.DownloadUpdatesAsync(update, value => progress?.Report(value), ct);
        _logger.LogInformation("Applying Velopack update and restarting. LatestVersion={LatestVersion}", update.TargetFullRelease.Version);
        manager.ApplyUpdatesAndRestart(update.TargetFullRelease);

        return BuildResult(current, update, "ReadTray update downloaded. Restarting to apply it.");
    }

    private static UpdateManager CreateUpdateManager()
    {
        var source = new GithubSource(RepositoryUrl, accessToken: null, prerelease: false, downloader: null);
        return new UpdateManager(source);
    }

    private static UpdateCheckResult BuildResult(string current, UpdateInfo? update, string message)
    {
        if (update is null)
        {
            return new UpdateCheckResult(false, current, null, RepositoryUrl + "/releases/latest", null, null, message);
        }

        var asset = update.TargetFullRelease;
        return new UpdateCheckResult(
            true,
            current,
            asset.Version.ToString(),
            RepositoryUrl + "/releases/latest",
            null,
            asset.FileName,
            message);
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
}
