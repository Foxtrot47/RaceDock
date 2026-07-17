# MotorsportTaskbar

A portable Windows 11 motorsport live-classification widget embedded in the primary taskbar. It shows a fixed-width top-five strip, a non-activating full-field click panel, priority-controlled alerts, a deterministic developer scenario, and an FIA Formula 2 live-timing adapter.

## Build and run

Requires the .NET 10 SDK and Windows 11 build 22000 or newer.

```powershell
dotnet build MotorsportTaskbar.slnx -c Release
dotnet run --project MotorsportTaskbar.Tests -c Release
dotnet publish MotorsportTaskbar -c Release -r win-x64 --self-contained false -o artifacts/publish/win-x64
```

Launch `artifacts/publish/win-x64/MotorsportTaskbar.exe`. The app connects to the current Formula 2 session and stays hidden until timing data arrives. Use the tray menu to enable deterministic test mode or open the developer controls. No launch-at-login entry is created.

## Data and privacy

The Formula 2 adapter connects directly to the official public FIA Formula 2 SignalR live-timing hub and requests classification, session, track-status, clock, and race-detail feeds. Application logs are stored in `Logs/app.log`.
git 