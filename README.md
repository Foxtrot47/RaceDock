# RaceDock

**Live motorsport timing, right on your Windows taskbar.**

RaceDock is a desktop app that shows live race information without taking over your screen.
Follow the leaders from the taskbar, open the full classification when you need more detail, and receive clear flag and session alerts while you use other apps.

[Download the latest release](https://github.com/Foxtrot47/RaceDock/releases/latest) · [View all releases](https://github.com/Foxtrot47/RaceDock/releases)

![RaceDock showing Formula 1 timing in the Windows taskbar with the full classification open](assets/screenshot.png)

## What RaceDock does

- Shows up to five leading drivers directly in the Windows taskbar.
- Opens a full-field classification without taking focus from your current app.
- Displays event, session, lap, stage, gap, tyre, class, pit, status, and flag information when the live feed provides it.
- Supports Formula 1, Formula 2, Formula 3, Formula E, DTM, the NTT INDYCAR SERIES, MotoGP, Moto2, Moto3, GT World Challenge Europe, the IMSA WeatherTech SportsCar Championship, and the World Rally Championship.
- Connects to several enabled championships at once and rotates between active sessions.
- Uses position cards or team and manufacturer logos.
- Includes deterministic scenarios and archive replay, so contributors can check the interface without waiting for a live event.

RaceDock stays hidden until an enabled stream provides live timing data.

## Download and run

RaceDock targets Windows 10 version 1809 (build 17763) or newer, including Windows 11. Windows 10 support is best-effort until it is covered by CI; use a Windows edition and build still supported by Microsoft.

1. Open the [latest release](https://github.com/Foxtrot47/RaceDock/releases/latest).
2. Download `RaceDock-vX.Y.Z-win-x64.zip` and, if you want to verify it, the matching `.sha256` file.
3. Extract the ZIP to a folder you can keep.
4. Run `RaceDock.exe`.

## Using the app

RaceDock runs from the Windows notification area. Open **Settings** from the tray menu, or double-click the tray icon, to:

- enable or disable live championships;
- choose how quickly active sessions rotate;
- show between one and five drivers;
- show or hide event, session, gap, position-colour, logo, and flag information;
- choose whether RaceDock starts automatically when you sign in to Windows.

Settings are saved in `%LocalAppData%\RaceDock\settings.json`. Windows startup is enabled by default and can be disabled from the **General** settings tab. Keep the extracted app in a stable folder because the startup entry points to that copy of `RaceDock.exe`.

Application logs are stored in `%LocalAppData%\RaceDock\Logs`. The Formula 1 diagnostic recorder is optional, limited to 2 MiB, and excludes connection credentials.

### Replay recorded timing

Open **Settings → Testing → Choose archive…** to replay a Timing71 ZIP export or a HAR containing Timing71 manifest and state messages. Replay uses the normal taskbar widget and classification flyout. The Testing tab provides pause, resume, restart, single-step, and 0.25×–20× speed controls. Long recording gaps over 30 seconds are compressed to one second so sparse archives do not appear frozen.

Archive replay is temporary and does not change the selected live streams. Choose **Return to live** to reconnect them. HAR files from arbitrary provider protocols are not converted automatically; the HAR must contain Timing71-compatible manifest and state payloads.

## Live timing sources

- **Formula 1:** the official Formula 1 SignalR Core live-timing stream.
- **Formula 2 and Formula 3:** the official FIA timing hubs for practice, qualifying, and races.
- **Formula E:** the official Al Kamel Formula E timing stream for practice, qualifying, and races.
- **DTM:** the public Griiip Insights timing feed for practice, qualifying, and races.
- **NTT INDYCAR SERIES:** the public INDYCAR race-control timing feed for practice, qualifying, warmup, and races.
- **MotoGP, Moto2, and Moto3:** separate opt-in streams from the official MotoGP live-timing gateway.
- **GT World Challenge Europe:** the public Swiss Timing SRO cache for practice, qualifying, and races.
- **IMSA WeatherTech SportsCar Championship:** the Al Kamel live-timing stream for practice, qualifying, and races.
- **World Rally Championship:** the public WRC Promoter API for entries, stages, and live stage times.

Live data remains subject to the availability and behaviour of each external service.

## Build from source

You need Windows 10 version 1809 or newer and the .NET 10 SDK.

```powershell
dotnet build RaceDock.slnx -c Release
dotnet run --project RaceDock.Tests -c Release
dotnet publish RaceDock/RaceDock.csproj -c Release -p:PublishProfile=FolderProfile
pwsh ./scripts/Build-Msix.ps1 -Configuration Release
```

The publish profile creates a self-contained, compressed single-file build for unpackaged use. `Build-Msix.ps1` creates an unsigned MSIX with the reserved Microsoft Store identity and packaged startup task; the MSIX path intentionally disables single-file publishing because package files are read-only. Keep trimming disabled: trimming is not currently safe for the WPF and Windows Forms code paths used by the app.

## Contributing

Bug reports, documentation improvements, new timing streams, feed fixes, and logo additions are welcome.

Before opening a pull request:

1. Keep the change focused and explain which event, series, or feed state it covers.
2. Do not commit API keys, authentication tokens, captured personal data, or other secrets.
3. Add or update regression tests when timing behaviour changes.
4. Run the release test suite:

   ```powershell
   dotnet run --project RaceDock.Tests -c Release
   ```

5. Build the application and use the **Testing** tab or a live session to check the visible result.

### Add a new event stream

Every live timing adapter implements `ILiveTimingSource`, defined in `RaceDock.Core/Domain.cs`. The interface publishes normalized `TimingSnapshot` values, connection changes, timing deltas, and feed failures so the rest of the app does not need to understand the provider's original data format.

To add another championship or event stream:

1. Create an adapter in `RaceDock.Core` that implements `ILiveTimingSource`.
2. Convert the provider's data into `TimingSnapshot` and `CompetitorStanding` records. Report connection and parsing failures through the interface events rather than hiding them.
3. If the stream represents a new championship, add it to the `Championship` enum in `Domain.cs` and add its short and full labels in `ChampionshipDisplay.cs`.
4. Add an enable setting in `UserSettings.cs`, expose it as a checkbox in `RaceDock/SettingsWindow.cs`, and include it in the feed-restart checks.
5. Register the adapter factory in `RaceDock/AppController.cs`. `CompositeLiveTimingSource` will start enabled adapters together and rotate between streams that are currently live.
6. Add focused tests in `RaceDock.Tests/Program.cs`. Cover normal timing, incomplete or unexpected feed data, connection changes, session endings, and any series-specific rules your adapter introduces.
7. Update the supported-stream list in this README.

Use public or properly authorized data sources, follow the provider's terms, and avoid embedding credentials in the application.

### Add or update a logo

Logo files and their feed-name mappings live in `RaceDock/Assets/ManufacturerLogos`.

1. Add a `.svg`, `.png`, or `.webp` asset to that folder. The project file includes those formats as application resources automatically.
2. Add one entry to `manifest.json` with the asset filename and every team or manufacturer name that a feed may return:

   ```json
   {
     "asset": "example.svg",
     "color": "#ffffff",
     "aliases": ["Example Racing", "Example Motorsport"]
   }
   ```

3. Use `"preserveColors": true` when the original colours must remain unchanged. Otherwise, the renderer tints supported artwork using the entry's `color` value.
4. Start the app, enable **Manufacturer logos**, and confirm the mark remains recognizable in both the taskbar strip and the full classification.

Logos appear inside a 26 x 26 DIP square with a 2 DIP internal margin and uniform scaling. In the full classification, the logo appears beside the driver name while the position remains visible. Providers can supply a separate manufacturer name for logo resolution while preserving the team name shown to the user. For a clear result at this size:

- prefer a compact emblem, shield, or monogram over a wide wordmark;
- tightly crop transparent bitmap files, ideally on a 256 x 256 canvas;
- use SVG files with a square view box and tightly bounded geometry; and
- add alternate feed names as aliases instead of duplicating an asset.

The built-in SVG reader supports fills, nested transforms, paths, polygons, and polylines.

### Update Formula 2 and Formula 3 team rosters

The FIA Formula 2 and Formula 3 feeds identify drivers but do not include their teams. Season-specific racing-number-to-team mappings are therefore stored in `RaceDock.Core/F2LiveTimingSource.cs`.

At the start of a season:

1. update the F2 and F3 roster dictionaries and season identifiers;
2. confirm every team name matches an alias in `RaceDock/Assets/ManufacturerLogos/manifest.json`; and
3. update the roster regression tests in `RaceDock.Tests/Program.cs`.

Unknown seasons intentionally use numbered position cards instead of showing a potentially incorrect team logo. The current mappings are based on the official [2026 Formula 2 driver list](https://www.fiaformula2.com/en/drivers) and [2026 Formula 3 driver list](https://www.fiaformula3.com/en/drivers).

## Releases and versioning

`Version.props` is the source of truth for the application version. The app, libraries, tests, executable, workflow artifacts, Git tag, and GitHub Release all use that version.

To inspect it locally:

```powershell
dotnet msbuild RaceDock/RaceDock.csproj -getProperty:Version
```

Maintainers publish a release by updating `VersionPrefix`, merging the change into `main`, and running the **Release Windows application** workflow. Released tags are immutable; publish a new version instead of replacing an existing release.

## License

See [LICENSE](LICENSE) for the repository's licence terms.
