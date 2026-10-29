# Grapekit

**Small, handy and free tools for Windows.**
Each app does one job well, starts fast and stays light on your PC. No accounts, no trials, no ads.

[grapekit.com](https://grapekit.com) · [All apps](https://grapekit.com/en/apps) · [Buy us a coffee](https://buymeacoffee.com/grapekit)

## Apps

| App | What it does | Download |
|---|---|---|
| **Keep Awake**<br><sub>Uyku Engelleyici</sub> | Keeps your PC and screen awake for as long as you choose, with app, full-screen and battery rules and an optional sleep / lock / shut down timer. | [grapekit.com](https://grapekit.com/en/app/keep-awake) · [Releases](https://github.com/grape-kit/grapekit/releases?q=keep-awake) |
| **Caps Lock Indicator**<br><sub>Caps Lock Göstergesi</sub> | Shows Caps Lock, Num Lock and Scroll Lock on screen, with sound and a password-field warning. | [grapekit.com](https://grapekit.com/en/app/caps-lock-indicator) · [Releases](https://github.com/grape-kit/grapekit/releases?q=caps-lock) |

## Why Grapekit?

- **Free forever.** No hidden fees, no trials, no required accounts.
- **Lightweight.** Event-driven, no busy timers, minimal memory and battery use.
- **Private.** No telemetry. The optional update check only reads a version number from this repository.
- **Consistent.** Every app shares the same look, tray panel, settings window, installer and update system ([`core/Grapekit.Core`](core/Grapekit.Core)).
- **Works everywhere.** Windows 10 (1809+) and Windows 11 on x64, ARM64 and x86. No .NET installation needed.

## Repository layout

```
core/Grapekit.Core/   shared brand, theme, controls, tray panel, settings window, updater
apps/<App>/           one folder per app (app.json describes it)
installer/            shared Inno Setup script, MSIX manifest template and wizard artwork
tools/                build-app.ps1, build-msix.ps1, icon and wizard generators
updates/<id>.json     update manifests (written by GitHub Actions on release)
```

## Building

Requirements: .NET 10 SDK, [Inno Setup 6](https://jrsoftware.org/isinfo.php), Node.js (only for icons).

```powershell
dotnet build apps/KeepAwake -c Release                           # run locally
powershell -File tools/build-app.ps1 -App keep-awake -Archs x64   # build an installer
powershell -File tools/build-msix.ps1 -App keep-awake             # build a Microsoft Store package
```

Releases are automatic: pushing a tag such as `keep-awake-v1.2.0` builds all architectures, publishes a GitHub Release and updates `updates/keep-awake.json`.

## Feedback

Found a bug or have an idea? Tell us on the [feedback page](https://grapekit.com/en/feedback).
If Grapekit saves you time, you can [buy us a coffee](https://buymeacoffee.com/grapekit). Every app stays free.

## License

[MIT](LICENSE) © 2026 Grapekit
