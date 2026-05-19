# Update Strategy

ReadTray uses GitHub Releases as the update channel.

## Release Flow

The `.github/workflows/release.yml` workflow runs on every push to `main` and on manual dispatch.

It:

1. Computes a version like `0.1.<github run number>`.
2. Runs tests.
3. Publishes a self-contained Windows x64 app.
4. Compresses the publish output into `ReadTray-<version>-win-x64.zip`.
5. Creates a GitHub Release with the zip and a SHA256 checksum.

## In-App Update Check

The app checks `https://api.github.com/repos/cwbrandsdal/readtray/releases/latest`.

If the latest release tag is newer than the running app version, the app prompts the user and opens the release page. This is deliberately conservative for the first public version because replacing a running Windows desktop app safely requires an installer or companion updater process.

## Next Step: Real Auto-Install

To move from update notification to true auto-install, use one of these paths:

- MSIX/App Installer: best Windows-native update story, supports Start Menu integration and package identity.
- Squirrel.Windows or Velopack: closest to Electron-style app update behavior for unpackaged desktop apps.
- Custom updater executable: possible, but more maintenance because it must download, verify, stop ReadTray, replace files, restart, and roll back on failure.

Recommended next step: evaluate Velopack if you want Electron-like GitHub release updates without MSIX packaging constraints.
