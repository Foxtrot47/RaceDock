# MotorsportTaskbar

A portable Windows 11 F1 live-classification widget embedded in the primary taskbar. It shows a fixed-width top-five strip, a non-activating full-field hover panel, priority-controlled race alerts, a deterministic developer scenario, and an unofficial F1 live-timing adapter.

## Build and run

Requires the .NET 10 SDK and Windows 11 build 22000 or newer.

```powershell
dotnet build MotorsportTaskbar.slnx -c Release
dotnet run --project MotorsportTaskbar.Tests -c Release
dotnet publish MotorsportTaskbar -c Release -r win-x64 --self-contained false -o artifacts/publish/win-x64
```

Launch `artifacts/publish/win-x64/MotorsportTaskbar.exe`. The app starts in scheduled live mode and stays hidden when no session has data. Use the tray menu to enable deterministic test mode or open the developer controls. No launch-at-login entry is created.

## Data and privacy

The live adapter uses the unofficial public Formula 1 SignalR endpoint and the Jolpica schedule API. Diagnostic recording is off by default. When enabled from the developer window, bounded feed frames are written without credentials to `Logs/diagnostic-feed.ndjson` (maximum 2 MiB). Application logs are stored in `Logs/app.log`.
git 