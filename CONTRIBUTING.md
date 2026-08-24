# Contributing to RaceDock

Bug reports, documentation improvements, new timing streams, feed fixes, and logo additions are all welcome

## Getting set up

You need Windows 10 version 1809 or newer and the .NET 10 SDK.

```powershell
dotnet build RaceDock.slnx -c Release
dotnet run --project RaceDock.Tests -c Release
```

To see the interface without waiting for a live session, run the app and use **Settings → Testing**, which offers deterministic scenarios and archive replay.

## Before opening a pull request

1. Keep the change focused and explain which event, series, or feed state it covers.
2. Never commit API keys, authentication tokens, captured personal data, or other secrets.
3. Add or update regression tests when timing behaviour changes.
4. Run the release test suite:

   ```powershell
   dotnet run --project RaceDock.Tests -c Release
   ```

5. Build the app and confirm the visible result using the **Testing** tab or a live session.

CI runs the same build and test commands on `main` and on every pull request.

## Project layout

| Path | Purpose |
| --- | --- |
| `RaceDock.Core` | Timing sources, normalization, domain model, settings, logging |
| `RaceDock` | WPF application, taskbar widget, classification flyout, settings UI |
| `RaceDock.Tests` | Regression suite (plain console runner) |
| `RaceDock.Package` | MSIX manifest and App Installer templates |
| `scripts` | Packaging and release helper scripts |

## Add a new event stream

Every live timing adapter implements `ILiveTimingSource`, defined in `RaceDock.Core/Domain.cs`. The interface publishes normalized `TimingSnapshot` values, connection changes, timing deltas, and feed failures, so the rest of the app never needs to understand a provider's original data format.

To add another championship or event stream:

1. Create an adapter in `RaceDock.Core` that implements `ILiveTimingSource`.
2. Convert the provider's data into `TimingSnapshot` and `CompetitorStanding` records. Report connection and parsing failures through the interface events rather than hiding them.
3. If the stream is a new championship, add it to the `Championship` enum in `Domain.cs` and add its short and full labels in `ChampionshipDisplay.cs`.
4. Add an enable setting in `UserSettings.cs`, expose it as a checkbox in `RaceDock/SettingsWindow.cs`, and include it in the feed-restart checks.
5. Register the adapter factory in `RaceDock/AppController.cs`. `CompositeLiveTimingSource` starts enabled adapters together and rotates between streams that are currently live.
6. Add focused tests in `RaceDock.Tests/Program.cs`. Cover normal timing, incomplete or unexpected feed data, connection changes, session endings, and any series-specific rules your adapter introduces.
7. Update the supported-stream table in the README.

Use public or properly authorized data sources, follow the provider's terms, and never embed credentials in the application.

## Add or update a logo

Logo files and their feed-name mappings live in `RaceDock/Assets/ManufacturerLogos`.

1. Add a `.svg`, `.png`, or `.webp` asset to that folder. The project file includes those formats as application resources automatically.
2. Add one entry to `manifest.json` with the asset filename and every team or manufacturer name a feed may return:

   ```json
   {
     "asset": "example.svg",
     "color": "#ffffff",
     "aliases": ["Example Racing", "Example Motorsport"]
   }
   ```

3. Use `"preserveColors": true` when the original colours must remain unchanged. Otherwise the renderer tints supported artwork using the entry's `color` value.
4. Start the app, enable **Manufacturer logos**, and confirm the mark stays recognizable in both the taskbar strip and the full classification.

Logos render inside a 26 × 26 DIP square with a 2 DIP internal margin and uniform scaling. In the full classification the logo sits beside the driver name while the position remains visible. Providers can supply a separate manufacturer name for logo resolution while preserving the team name shown to the user. For a clear result at this size:

- prefer a compact emblem, shield, or monogram over a wide wordmark;
- tightly crop transparent bitmaps, ideally on a 256 × 256 canvas;
- use SVG files with a square view box and tightly bounded geometry; and
- add alternate feed names as aliases instead of duplicating an asset.

The built-in SVG reader supports fills, nested transforms, paths, polygons, and polylines.

## Update Formula 2 and Formula 3 team rosters

The FIA Formula 2 and Formula 3 feeds identify drivers but not their teams, so season-specific racing-number-to-team mappings live in `RaceDock.Core/F2LiveTimingSource.cs`.

At the start of a season:

1. update the F2 and F3 roster dictionaries and season identifiers;
2. confirm every team name matches an alias in `RaceDock/Assets/ManufacturerLogos/manifest.json`; and
3. update the roster regression tests in `RaceDock.Tests/Program.cs`.

Unknown seasons intentionally use numbered position cards instead of showing a potentially incorrect team logo. Current mappings are based on the official [2026 Formula 2 driver list](https://www.fiaformula2.com/en/drivers) and [2026 Formula 3 driver list](https://www.fiaformula3.com/en/drivers).

## Translations

RaceDock is currently English-only and has no localization infrastructure. Adding it is a larger change than translating strings, so please open an issue to discuss the approach before starting work.

## Licensing of contributions

RaceDock is licensed under the GNU General Public License v3.0. By contributing, you agree that your contribution is licensed under the same terms.
