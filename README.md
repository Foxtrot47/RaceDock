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

## Data and privacy

The Formula 1 adapter connects to the official Formula 1 SignalR Core live-timing stream. The FIA adapter connects to the official FIA timing hub for both Formula 2 and Formula 3 series, including practice, qualifying, and race sessions. The WRC adapter polls the public WRC Promoter API for event entries, stages, and live stage times. Application logs are stored in `Logs/app.log`.
