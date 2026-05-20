# Update Strategy

ReadTray uses GitHub Releases as the update channel.

## Release Flow

The `.github/workflows/release.yml` workflow runs on every push to `main` and on manual dispatch.

It:

1. Computes a version like `0.1.<github run number>`.
2. Runs tests.
3. Restores the repo-local Velopack CLI tool.
4. Publishes a self-contained Windows x64 app.
5. Packages Velopack installer/update assets into `artifacts\velopack`.
6. Creates a GitHub Release with all Velopack assets.

## In-App Update Check

Installed builds use Velopack's GitHub release source for `https://github.com/cwbrandsdal/readtray`.

If a newer release is available, the tray update command prompts the user, downloads the update, and restarts ReadTray into the new version. Dev runs and raw publish folders are not installed by Velopack, so they show a clear message and do not try to self-update.

## Installer Choice

Velopack is the current installer path because it is closest to the Electron/GitHub Releases update flow:

- `ReadTray-win-Setup.exe` is the user-facing installer.
- `ReadTray-<version>-full.nupkg`, `RELEASES`, and the JSON feed files are used by the updater.
- Unsigned installers will likely trigger Windows trust warnings. Code signing should be added before broad public distribution.

MSIX remains a reasonable future option for Microsoft Store or enterprise deployment, but it has more signing/package identity constraints.
