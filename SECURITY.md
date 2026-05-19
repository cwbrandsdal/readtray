# Security

ReadTray stores user-specific configuration outside the repository under `%APPDATA%\ReadTray`.

## Secrets

- ElevenLabs API keys are stored in `%APPDATA%\ReadTray\secrets.json`.
- Secret values are protected with Windows DPAPI for the current Windows user.
- API keys are never intentionally written to normal settings, logs, or repository files.
- Do not commit local `settings.json`, `secrets.json`, logs, publish profiles, certificates, or packaged artifacts.

## Logs

Logs are written under `%LOCALAPPDATA%\ReadTray\logs`.

Default logging is informational. Debug logging is opt-in from Settings and applies after restarting the app. Captured-text previews are also opt-in and should stay off for normal public builds.

## Before Publishing

1. Run a secret scan over tracked files.
2. Confirm `git status --ignored` does not show sensitive files staged.
3. Test with a throwaway API key.
4. Delete local logs before sharing support bundles.
5. Review release artifacts before uploading them.
