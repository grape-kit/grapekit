<p align="center">
  <a href="https://grapekit.com">
    <picture>
      <source media="(prefers-color-scheme: dark)" srcset="docs/logo-dark.svg">
      <img src="docs/logo-light.svg" alt="Grapekit" height="64">
    </picture>
  </a>
</p>

<p align="center"><b>Small, handy and free tools for Windows.</b><br>
Each app does one job well, starts fast and stays light on your PC. No accounts, no trials, no ads.</p>

<p align="center">
  <a href="https://grapekit.com">grapekit.com</a> ·
  <a href="https://grapekit.com/en/apps">All apps</a> ·
  <a href="https://buymeacoffee.com/grapekit">☕ Buy us a coffee</a>
</p>

---

## Apps

| | App | What it does | Download |
|---|---|---|---|
| <img src="docs/caps-lock.png" width="40"> | **Caps Lock Indicator**<br><sub>Caps Lock Göstergesi</sub> | Shows Caps Lock, Num Lock and Scroll Lock on screen, with sound and a password-field warning. | [grapekit.com](https://grapekit.com/en/app/caps-lock-indicator) · [Releases](https://github.com/grape-kit/grapekit/releases?q=caps-lock) |

More tools are on the way.

## Why Grapekit?

- **Free forever.** No hidden fees, no trials, no required accounts.
- **Lightweight.** Event-driven, no busy timers, minimal memory and battery use.
- **Private.** No telemetry. The optional update check only reads a version number from this repository.
- **Consistent.** Every app shares the same look, About page, tray menu, installer and update system ([`core/Grapekit.Core`](core/Grapekit.Core)).
- **Works everywhere.** Windows 10 (1809+) and Windows 11 on x64, ARM64 and x86. No .NET installation needed.

## Repository layout

```
core/Grapekit.Core/   shared brand, theme, controls, updater, standard menu
apps/<App>/           one folder per app (app.json describes it)
installer/            shared Inno Setup script and wizard artwork
tools/                build-app.ps1, make-icon.mjs, make-wizard.mjs
updates/<id>.json     update manifests (written by GitHub Actions on release)
```

## Building

Requirements: .NET 10 SDK, [Inno Setup 6](https://jrsoftware.org/isinfo.php), Node.js (only for icons).

```powershell
dotnet build apps/CapsLock -c Release                          # run locally
powershell -File tools/build-app.ps1 -App caps-lock -Archs x64  # build an installer
```

Releases are automatic: pushing a tag such as `caps-lock-v1.2.0` builds all architectures, publishes a GitHub Release and updates `updates/caps-lock.json`.

New app icon in the Grapekit style:

```powershell
cd tools; npm i
node make-icon.mjs <lucide-icon-name> "#7C3AED" ../apps/<App>/brand
node make-wizard.mjs ../apps/<App>/brand
```

## Support

Found a bug or have an idea? [Open an issue](https://github.com/grape-kit/grapekit/issues/new).
If Grapekit saves you time, you can [buy us a coffee](https://buymeacoffee.com/grapekit). Every app stays free.

## License

[MIT](LICENSE) © 2026 Grapekit
