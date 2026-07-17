# MotorsportTaskbar

A portable Windows 11 motorsport live-classification widget embedded in the primary taskbar. It shows a fixed-width top-five strip, a non-activating full-field click panel, priority-controlled alerts, a deterministic developer scenario, and concurrent Formula 1 plus WRC live-timing adapters.

## Build and run

Requires the .NET 10 SDK and Windows 11 build 22000 or newer.

```powershell
dotnet build MotorsportTaskbar.slnx -c Release
dotnet run --project MotorsportTaskbar.Tests -c Release
dotnet publish MotorsportTaskbar -c Release -p:PublishProfile=FolderProfile
```

Launch the generated `MotorsportTaskbar.exe`. `FolderProfile` produces a self-contained, compressed single-file build. Trimming was tested but is not safe for this WPF/WinForms application: the .NET SDK blocks it by default, and suppressing that guard caused a published startup failure in the live smoke test. Keep trimming disabled unless the UI and all reflection-based paths are migrated and revalidated. The app connects to active Formula 1 and Rally Estonia sessions concurrently, rotating the taskbar display between them every five seconds when both have timing data. It stays hidden until timing data arrives. Use the tray menu to enable deterministic test mode or open the developer controls. No launch-at-login entry is created.

## Data and privacy

The Formula 1 adapter connects to the official Formula 1 SignalR Core live-timing stream. The WRC adapter polls the public WRC Promoter API for Rally Estonia entries, stages, and live stage times. Application logs are stored in `Logs/app.log`.
git 