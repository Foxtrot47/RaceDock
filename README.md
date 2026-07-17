# MotorsportTaskbar

A portable Windows 11 motorsport live-classification widget embedded in the primary taskbar. It shows a fixed-width top-five strip, a non-activating full-field click panel, priority-controlled alerts, a deterministic developer scenario, and Formula 2 plus WRC live-timing adapters.

## Build and run

Requires the .NET 10 SDK and Windows 11 build 22000 or newer.

```powershell
dotnet build MotorsportTaskbar.slnx -c Release
dotnet run --project MotorsportTaskbar.Tests -c Release
dotnet publish MotorsportTaskbar -c Release -r win-x64 --self-contained false -o artifacts/publish/win-x64
```

Launch `artifacts/publish/win-x64/MotorsportTaskbar.exe`. The app currently connects to Rally Estonia live stages and stays hidden until timing data arrives. Use the tray menu to enable deterministic test mode or open the developer controls. No launch-at-login entry is created.

## Data and privacy

The active WRC adapter polls the public WRC Promoter API for Rally Estonia entries, stages, and live stage times. The retained Formula 2 adapter connects to the official public FIA Formula 2 SignalR live-timing hub. Application logs are stored in `Logs/app.log`.
git 