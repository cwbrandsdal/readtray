# Public Release Checklist

- Confirm `.gitignore` excludes `.vs`, `bin`, `obj`, `artifacts`, logs, local settings, local secrets, publish settings, certificates, and key files.
- Keep API keys only in DPAPI-protected `%APPDATA%\ReadTray\secrets.json`.
- Keep user settings only in `%APPDATA%\ReadTray\settings.json`.
- Run `dotnet test ReadTray.slnx`.
- Run `dotnet build ReadTray.slnx`.
- Run a repository text scan for `api key`, `secret`, provider tokens, and known local voice IDs before pushing.
- Publish from a clean tree using `scripts\publish.ps1`.
- Verify the generated app starts hidden to tray, uses the custom icon, and does not write debug logs unless enabled.
