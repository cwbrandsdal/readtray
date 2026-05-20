# Installer Options

ReadTray currently has two practical installer/update paths.

## Recommended: Velopack

Velopack is the closest fit to the Electron updater model: installer, GitHub-hosted release packages, delta updates, background download, and app restart into the new version.

Why it fits ReadTray:

- Works with WPF/.NET desktop apps.
- Can create a one-click Windows installer.
- Creates Start Menu and desktop shortcuts.
- Can host releases on GitHub Releases.
- Supports app-side update checks and applying updates.
- MIT/open-source tooling and SDK.

Tradeoffs:

- Opinionated installer UX, not a custom wizard.
- Proper trust still benefits from code signing. Unsigned installers can trigger Windows SmartScreen warnings.
- App-side update checks require the app to be installed by the Velopack setup package. Dev runs and raw publish folders can still open the GitHub release page manually.

Local package command:

```powershell
.\scripts\package-velopack.ps1 -Version 0.1.0
```

Output goes to:

```text
artifacts\velopack
```

## Alternative: MSIX + App Installer

MSIX is the most Windows-native option. With an `.appinstaller` file, Windows can check for and install updates from a hosted URI.

Why it may fit:

- Native Windows install/uninstall story.
- Strong Start Menu integration.
- Built-in update support through App Installer.
- Good path if publishing to Microsoft Store later.

Tradeoffs:

- More packaging ceremony.
- Signing is effectively required for a smooth external distribution story.
- MSIX has app-container/package identity behavior that can affect file system, registry, startup, and protocol integration expectations.
- GitHub Releases can host assets, but MSIX update feeds are usually cleaner from stable HTTPS hosting with predictable URLs.

## Decision

Use Velopack first for ReadTray. It matches the desired GitHub Releases updater model and is less invasive for a tray-first WPF app. Revisit MSIX later if Microsoft Store distribution or enterprise deployment becomes a priority.
