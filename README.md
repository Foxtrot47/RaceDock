# MotorsportTaskbar

A portable Windows 11 motorsport live-classification widget embedded in the primary taskbar. It shows a configurable leading-driver strip, a non-activating full-field flyout, priority-controlled alerts, and concurrent Formula 1, Formula 2, Formula 3, and WRC live-timing adapters.

## Build and run

Requires the .NET 10 SDK and Windows 11 build 22000 or newer.

```powershell
dotnet build MotorsportTaskbar.slnx -c Release
dotnet run --project MotorsportTaskbar.Tests -c Release
dotnet publish MotorsportTaskbar -c Release -p:PublishProfile=FolderProfile
```

Launch the generated `MotorsportTaskbar.exe`. `FolderProfile` produces a self-contained, compressed single-file build. Trimming was tested but is not safe for this WPF/WinForms application: the .NET SDK blocks it by default, and suppressing that guard caused a published startup failure in the live smoke test. Keep trimming disabled unless the UI and all reflection-based paths are migrated and revalidated. The app connects to enabled live championships concurrently and rotates between active sessions. It stays hidden until timing data arrives. Open **Settings** from the tray menu (or double-click the tray icon) to select streams, change rotation timing, choose how many drivers are visible, toggle event/session/gap/flag elements, and replace taskbar position cards with manufacturer marks. The **Testing** tab contains the deterministic scenario, feed-state controls, editable classification, and bounded diagnostic recording. Settings are persisted in `%LocalAppData%\MotorsportTaskbar\settings.json`. No launch-at-login entry is created.

## Versioning and releases

`Version.props` is the single source of truth for the application SemVer. It is imported by `Directory.Build.props`, so the app, core library, tests, published executable, workflow artifacts, Git tag, and GitHub Release use the same version. The running version is shown in the tray menu.

To inspect the evaluated version locally:

```powershell
dotnet msbuild MotorsportTaskbar/MotorsportTaskbar.csproj -getProperty:Version
```

To publish a release:

1. Update `VersionPrefix` in `Version.props` to a new stable SemVer and merge it into `main`.
2. Run the **Release Windows application** workflow from the `main` branch.
3. The workflow validates the version, runs the tests, builds the self-contained application, and creates matching `vX.Y.Z` tag and GitHub Release entries.
4. Download the versioned `win-x64` ZIP and its SHA-256 checksum from the release or workflow artifacts.

Released versions are immutable. If a `vX.Y.Z` tag or release already exists, increment `VersionPrefix` and publish a new version rather than replacing it.

## Data and privacy

The Formula 1 adapter connects to the official Formula 1 SignalR Core live-timing stream. The FIA adapter connects to the official FIA timing hub for both Formula 2 and Formula 3 series, including practice, qualifying, and race sessions. The WRC adapter polls the public WRC Promoter API for event entries, stages, and live stage times. Application logs are stored in `Logs/app.log`.

## Logo asset guidelines

Taskbar logos are displayed in a 26 x 26 DIP square with a 2 DIP internal margin, leaving an effective 22 x 22 DIP artwork area. The renderer uses uniform scaling, so it preserves each logo's aspect ratio rather than stretching it to a square.

- Prefer a compact emblem, shield, or monogram with an artwork ratio between 0.75:1 and 1.5:1. Avoid horizontal wordmarks wider than 2:1; at taskbar size a 4:1 wordmark is only about 5 pixels tall.
- Use a tightly cropped, transparent 256 x 256 PNG, with visible artwork occupying roughly 86-92% of the canvas. A 128 x 128 PNG is the practical minimum; assets larger than 512 x 512 provide no useful taskbar detail.
- Prefer SVG when available. Use a square view box and keep the visible geometry tightly bounded. Embedded fills, nested transforms, paths, polygons, and polylines are supported.
- Add `"preserveColors": true` to the logo's `manifest.json` entry when its PNG or SVG contains intentional colors. Leave it unset for monochrome artwork that should be tinted using the entry's `"color"` value.
- Keep alternate feed names as aliases of one canonical asset instead of adding duplicate logos.
