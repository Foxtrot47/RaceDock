# RaceDock Linux / Plasma Plan

Status: implementation complete for the Plasma front end. Shared Core session, the full widget/flyout, packaging, and the settings UI are in the tree, and the applet, popup, pinned dialog, and config dialog have been verified on a throwaway X11 Plasma session. Wayland, multi-monitor, and 1440p layouts are still unverified.

Last updated: 2026-10-29

## Goal

Build a Linux-native RaceDock front end for KDE Plasma 6:

- A compact taskbar-style widget in the Plasma panel.
- A richer classification flyout with the same information and column model as the Windows app.
- A pinned, free-floating flyout host in addition to the normal anchored popup.
- Reuse `RaceDock.Core` for all feed, settings, schedule, timing, and presentation logic.
- Keep the Linux project a thin D-Bus/OS adapter so a future macOS front end can reuse the same Core APIs.

## Host / command note

This workspace is a WSL path:

`\\wsl.localhost\Debian\home\foxtrot47\RaceDock`

The current OpenCode session runs inside Debian, so shell commands are already Linux. A Windows-hosted
OpenCode backend would instead need Linux commands wrapped explicitly:

```powershell
wsl -d Debian -- bash -lc 'cd /home/foxtrot47/RaceDock && dotnet build RaceDock.Core/RaceDock.Core.csproj'
```

Do not assume the shell is Bash just because the workspace is Linux; check the session host first.

## Architecture decisions

- **No Avalonia.** Use a native Plasma 6 QML applet plus a headless .NET 10 sidecar.
- **Sidecar IPC:** D-Bus session bus using `Tmds.DBus.Protocol` `0.95.1`.
- **Shared orchestration:** `RaceDock.Core` owns feed selection, rotation, alerts, schedule, replay/scenario control, and presentation DTO construction.
- **Thin front ends:** Windows WPF and Plasma QML render the same Core presentation values. Do not put feed or schedule logic in QML.
- **One flyout body:** `FlyoutPanel.qml` is reused by the anchored applet popup and the pinned `PlasmaCore.Dialog` host. Pinning is a host/lifecycle concern, not a second UI implementation.
- **Polling, not signals:** the Plasma `org.kde.plasma.workspace.dbus` QML module does not expose signal subscriptions. The widget polls `GetWidgetState`; the flyout polls `GetFlyoutState` while open or pinned.
- **Settings are full snapshots:** `ApplySettings` accepts a complete `UserSettings` JSON document. Partial JSON must not be sent because missing properties would fall back to defaults. `resetSettings` builds the defaults in Core and applies them the same way.
- **Settings UI is a client, not `plasmoid.configuration`:** the applet's config pages load `GetSettings`, edit a deep copy, and send the whole document back through `ApplySettings`. `main.xml` exists only so Plasma offers the Configure action; the real settings file belongs to the service so Windows and Plasma share it.
- **Windows app untouched for now:** the new Core presentation/session code is additive. Refactoring WPF to consume it is a later, separate task.

## D-Bus contract

Service: `org.foxtrot47.RaceDock`

Object path: `/org/foxtrot47/RaceDock`

Interface: `org.foxtrot47.RaceDock1`

Methods:

| Method | Signature | Purpose |
| --- | --- | --- |
| `Ping` | `() -> s` | Health check; returns `pong`. |
| `GetWidgetState` | `() -> s` | JSON snapshot for the compact panel strip. |
| `GetFlyoutState` | `() -> s` | JSON snapshot for the classification/schedule flyout. |
| `GetSettings` | `() -> s` | Complete `UserSettings` JSON. |
| `ApplySettings` | `(s) -> s` | Apply a complete settings JSON snapshot. Returns `{ok, settings}` or `{ok:false,error}`. |
| `Command` | `(ss) -> s` | Control command and argument. Returns `{ok,...}`. |

Commands already implemented in `RaceDockSession.CommandAsync`:

- `resume`, `pause`, `restart`, `step`, `speed <number>`, `trigger <ScenarioCommand>`
- `scenario <true|false>`
- `archive <path>`
- `live`
- `refreshSchedule`
- `refreshState`
- `resetSettings` (restores Core defaults, persists them, and returns `{ok, settings}`)

All payloads use camel-case JSON with string enums, via `RaceDock.Core.Presentation.RaceDockJson`.

## Repository layout

Shared Core:

- `RaceDock.Core/Presentation/PresentationModels.cs`
  - JSON-facing widget/flyout/settings DTOs and serialization options.
- `RaceDock.Core/Presentation/RaceDockPresentationBuilder.cs`
  - Toolkit-neutral formatting for panel cells, classification rows, sectors, tyres, energy, flags, and schedules.
- `RaceDock.Core/Presentation/RaceDockSession.cs`
  - Headless live/scenario/archive orchestration, source switching, schedule refresh, render loop, state snapshots.

Linux sidecar:

- `RaceDock.Linux/Program.cs`
  - CLI, session lifecycle, D-Bus connection/name acquisition.
- `RaceDock.Linux/Ipc/RaceDockMethodHandler.cs`
  - D-Bus method dispatch and JSON payloads.
- `RaceDock.Linux/RaceDock.Linux.csproj`

Plasma package:

- `RaceDock.Plasma/package/metadata.json`
- `RaceDock.Plasma/package/contents/ui/main.qml`
  - `PlasmoidItem`, compact representation, anchored full representation, pinned dialog host.
- `RaceDock.Plasma/package/contents/ui/RaceDockClient.qml`
  - Polling D-Bus client and JSON parsing.
- `RaceDock.Plasma/package/contents/ui/CompactWidget.qml`
  - Panel strip with the Windows taskbar geometry: a 40 px strip, a left context column
    (100 px two-row, 150 px single-row) carrying the event/flag/progress, and competitor cells to
    the right. Idle mode shows the next session instead.
- `RaceDock.Plasma/package/contents/ui/MarqueeText.qml`
  - Single-line label that scrolls when the text is wider than its slot, used for the event name.
- `RaceDock.Plasma/package/contents/ui/FlyoutPanel.qml`
  - Shared flyout body: header, table header, classification list, schedule state, footer, pin/close.
- `RaceDock.Plasma/package/contents/ui/ClassificationRow.qml`
  - Configurable optional columns.
- `RaceDock.Plasma/package/contents/ui/ScheduleView.qml`
  - "Next on track" schedule state.
- `RaceDock.Plasma/package/contents/ui/SettingsCatalog.qml`
  - Championship keys/labels, each championship's `UserSettings` power switch (`enableKey`), and
    column keys/flags shared by the config pages.
- `RaceDock.Plasma/package/contents/ui/ConfigSettingsBase.qml`
  - Shared settings plumbing: load the complete document, edit a deep copy, apply it whole, and
    restore defaults.
- `RaceDock.Plasma/package/contents/ui/ConfigGeneral.qml`
  - Config page: next-session idle mode, animations, single-row strip, rotation interval,
    stale-data timeout, and restore defaults.
- `RaceDock.Plasma/package/contents/ui/ConfigChampionshipPage.qml`
  - Per-championship page: live-timing power switch, competitor counts, manufacturer logos,
    widget/flyout options, and column order and visibility.
- `RaceDock.Plasma/package/contents/ui/ConfigChampionship*.qml`
  - Fifteen static wrappers, one per championship, over `ConfigChampionshipPage.qml`.
- `RaceDock.Plasma/package/contents/ui/ConfigTesting.qml`
  - Testing page: start the test scenario, return to live timing, replay an archive, refresh the
    calendar.
- `RaceDock.Plasma/package/contents/config/config.qml`
  - `ConfigModel` that lists General, the fifteen championship pages, and Testing.
- `RaceDock.Plasma/package/contents/config/main.xml`
  - Placeholder config group so the Configure action exists; it is not the settings source.
- `RaceDock.Plasma/tests/Test.qml`
  - `qmltestrunner` integration harness. It lives outside `contents/`, so packaging does not ship it.
- `RaceDock.Plasma/tests/Screenshots.qml`
  - `qmltestrunner` harness that grabs each surface to PNG (`grabToImage`) for layout review without a
    Plasma session. Run it from the folder that should receive the images.
- `RaceDock.Plasma/tests/Startup.qml`
  - `qmltestrunner` harness for a client that starts before the sidecar exists; the shell starts the
    service after the first failed polls.
- `RaceDock.Plasma/tests/Reconnect.qml`
  - `qmltestrunner` harness for a sidecar that stops and restarts while the client polls.

Packaging and CI:

- `scripts/install-linux.sh`
  - Publishes the sidecar, installs the wrapper and D-Bus activation file, installs the applet, and optionally enables a systemd user unit.
- `scripts/uninstall-linux.sh`
  - Removes the app, applet, wrapper, and activation file; `--purge` also deletes settings and logs.
- `scripts/linux/racedock-service`
  - Runtime wrapper that resolves `DOTNET_ROOT` and forwards arguments.
- `.github/workflows/linux.yml`
  - Linux build/test job plus QML lint and `kpackagetool6` package validation.

## Current status

### Done

- Confirmed `RaceDock.Core` is cross-platform and built it on Linux.
- Full suite is green on Linux: **140/140** (122 original + 10 presentation + 8 session tests).
- Created the D-Bus sidecar and verified every method (`Ping`, `GetWidgetState`, `GetFlyoutState`, `GetSettings`, `ApplySettings`, `Command`) on an isolated session bus.
- Added shared presentation DTOs, presentation builder, and headless `RaceDockSession`.
- Added service CLI: `--scenario`, `--archive`, `--settings`, `--log-folder`, `--dump-feed`, `--timezone`, `--paused`, `--print-state`, `--print-delay`.
- Implemented the Plasma UI: `main.qml`, `RaceDockClient.qml`, `CompactWidget.qml`, `FlyoutPanel.qml`, `ClassificationRow.qml`, `ScheduleView.qml`.
- Added Escape-to-close, accessible names for pin/close/compact, configure buttons in both flyout hosts, and WEC wheel cells with tooltips.
- Hardened `RaceDockSession`: coalesced `StateChanged` dispatch outside the source-switch semaphore, fallback-source cleanup, idempotent disposal, hide-championship snapshot clearing, and error payloads for commands after shutdown.
- Made `--print-state` wait for the first non-hidden snapshot instead of sleeping for a fixed delay.
- Added the settings UI: `config.qml`, `ConfigSettingsBase.qml`, `ConfigGeneral.qml`,
  `ConfigChampionshipPage.qml` with fifteen championship wrappers, `ConfigTesting.qml`, and
  `SettingsCatalog.qml`; `resetSettings` restores Core defaults.
- Added packaging: `scripts/install-linux.sh`, `scripts/uninstall-linux.sh`, `scripts/linux/racedock-service`, D-Bus activation, and an optional systemd user unit.
- Verified install → wrapper → `--print-state`, D-Bus activation, `kpackagetool6 --install`, and the generated activation file in a throwaway XDG prefix.
- Verified the QML client and the config pages against a running sidecar with `qmltestrunner`: live
  state, settings round-trip, pause, column reorder, defaults reset, championship power switches, and
  the Testing page's calendar refresh all pass.
- Added the Linux CI workflow (`.github/workflows/linux.yml`) and a Linux README section.
- Aligned the flyout with the Windows design (2026-10-25): status chip lives in the header (no
  footer), the header is one row with the event name marquee and the session info under it, sector
  columns get individual S1/S2/S3 headers, tyre badges pick text colour by luminance, and manufacturer
  logos render in the widget cells and beside flyout driver names.
- Validated archive replay through the service with a synthetic F1 `.ndjson` dump: classification, gaps,
  tyre ages, sectors/mini-sectors, yellow → clear → chequered transitions, and scenario ↔ archive switching.
- Confirmed the live service connects the enabled feeds (F1, GTWC, WEC, WRC) and fills the idle
  widget/flyout from the published schedule (US GP Practice 1 was the next session).
- Watched a live F1 on-track session through the service during US GP Practice 1: classification,
  intervals, lap-time mode, tyre compounds/ages, sector and mini-sector data, and the session countdown
  all rendered, and sector columns could be enabled live through `ApplySettings`.
- Recorded a real F1 feed dump with `--dump-feed` and replayed it through `--archive`: 20 rows with
  sectors/mini-sectors, tyre ages, pit cars, countdown progress, archive reload, and archive → live switching.
- Replayed a recorded WEC fixture through `--archive` and confirmed the energy column renders
  (`83%` good → `15%` low red) along with WEC wheel ages.
- Added `RaceDock.Plasma/tests/Screenshots.qml` and reviewed every surface as a PNG with the offscreen
  platform: live/idle/flags on the panel strip, live/schedule/sector/energy flyouts, a scrolled
  20-row archive flyout, and the General, Formula 1, WEC, and Testing config pages. This verifies
  layout, column alignment, colour coding, and content, but not panel integration or the pinned
  dialog.
- Rebuilt the two-row panel strip to the Windows taskbar geometry (2026-10-29): a 40 px strip, a
  100 px left context column (event + flag above, progress below), and competitor cells to the
  right. Single-row mode widens the context column to 150 px, folds `position code interval` into
  the driver line, and hides the manufacturer marker; two-row mode keeps the logo or gradient
  position tile. Fixed a `ColumnLayout` bug where two `fillHeight` rows with different implicit
  heights collapsed the event row to 2 px, and made `MarqueeText` report an implicit height and
  restart its scroll when the text changes. The idle strip sizes itself (`idleWidth`, 248–520 px)
  and the inline `FlagChip` is shared by both context layouts.
- Reworked the settings UI into per-championship pages (`ConfigChampionshipPage.qml` plus fifteen
  wrappers) over a shared `ConfigSettingsBase.qml`, with General and Testing pages. The
  championship master switch now writes the real `UserSettings` flag via
  `SettingsCatalog.enableKeyFor` (`enableWec`, `enableElms`, `enableWrc`, …), which fixes ELMS/WEC/WRC
  staying unfetched after being "enabled".
- Added `ManufacturerLogos` coverage to `RaceDock.Tests`: exact aliases, case/punctuation/diacritic
  folding, substring fallback, unknown → null, the embedded manifest, and the builder's
  manufacturer-before-team fallback.
- Fixed the Testing page's archive card (2026-10-29): it promised a drop zone it never had, its
  buttons overflowed the config dialog, and there was no way to pick a file. The card now has a
  `Browse…` button that opens a `QtQuick.Dialogs.FileDialog`, created on first use through a
  `Loader` so plasmashell is never asked to build a dialog off-scene, plus a path field, a
  `Load archive` button, and an inline status line that reports loader errors in the card instead
  of the page footer. Config page rows are capped at the page column width, which stops the right
  edge of every row from being clipped.
- Isolated the QML harnesses from the real settings file: `Test.qml`, `Screenshots.qml`,
  `Startup.qml`, and `Reconnect.qml` all document and use `--settings`, so a test run can no longer
  reset `~/.local/share/RaceDock/settings.json`.
- Verified the applet on a throwaway Plasma 6.3 X11 session (Xvfb + kwin + plasmashell, 2026-10-29):
  the two-row and single-row strips render in a real panel with logos and live scenario timing; the
  anchored popup shows the classification; pin detaches it into a floating `PlasmaCore.Dialog` that
  drags via `startSystemMove`, stays visible when the desktop is clicked, and unpins; the config
  dialog opens from the flyout and its General and championship pages scroll and apply live; and the
  panel, flyout, and config dialog hold up at 1.5× scaling. A 44 px vertical panel clips the strip,
  so it stays declared horizontal-only.
- Captured the single-row strip offscreen with the scenario service and the real F1 replay, with and
  without a yellow flag, plus a WEC-only idle single-row strip.
- Captured the flyout with a yellow track condition, confirming the header badge renders from
  `trackLabel`/`trackColor` exactly like the Windows badge (and stays hidden when the track is clear).
- Added `Startup.qml` and `Reconnect.qml` harnesses: the QML client survives a service that starts after
  it (the first polls report an outage) and recovers when the sidecar stops and is replaced, with
  `connected`, `lastError`, and both snapshots returning to live without a restart.

### In progress

- None. The applet, anchored popup, pinned dialog, and config dialog have now been exercised on a
  real Plasma session (throwaway Xvfb X11, 2026-10-29); the remaining gaps are Wayland, multi-monitor,
  and a 1440p run, listed below.

### Blocked / unverified

- The pinned `PlasmaCore.Dialog` was verified on X11 with kwin: pin detaches the flyout, drag via
  `startSystemMove` moves it, it stays visible when another window is clicked, and unpin hides it.
  Wayland placement and the compositor's own move rules are still unverified.
- Panel width/height behavior on a vertical panel is known to clip (the strip is horizontal-only and
  declared as such); multi-monitor and 1440p layouts are unverified. 1080p at 1.0 and 1.5 scale were
  checked.
- The applet reconnecting after the sidecar exits/restarts is covered by the `Startup.qml` and
  `Reconnect.qml` client harnesses, but it has not been watched on a real panel.
- Live stale/offline transitions (a feed dropping mid-session) have not been observed; the 12 s health
  timeout is only covered by unit-level behavior.
- `qmltestrunner` here runs from extracted binaries in `/tmp`; the CI job only lints QML and validates the package, because the QML harness needs the Plasma D-Bus QML module and a session bus.

## Implementation plan

### Phase 1 — Stabilize shared Core

- [x] Re-run the full `RaceDock.Tests` suite after the new Core files (139/139).
- [x] Add focused Core tests for `RaceDockPresentationBuilder`:
  - leader/interval/gap cells
  - position colors
  - sector/mini-sector colors
  - tyre compounds and WEC wheel ages
  - rally splits
  - energy thresholds
  - alert/flag mapping
  - schedule wording and countdowns
- [x] Add tests for `RaceDockSession` state transitions where practical (live↔scenario switching still needs a live feed):
  - live -> scenario -> live
  - archive replay loading
  - settings apply with feed-changing vs presentation-only changes
  - schedule refresh and idle mode
- [x] Verify `Change` event ordering does not deadlock with D-Bus polls (coalesced dispatcher + subscriber re-entry test).
- [x] Review `RaceDockSession` shutdown for cancellation, source disposal, and semaphore release.
- [ ] Decide whether Windows `TaskbarWindow`/`ClassificationFlyout` should later be refactored onto the shared presentation builder. Do not do that before the Plasma path is working.

### Phase 2 — Finish Linux D-Bus service

- [ ] Add a `GetStatus`/version/capabilities method if the front end needs it. Not needed so far: the
  footer already shows the connection state and view mode, and version drift is not surfaced in the UI.
- [x] Decided not to emit a `Changed(s topic)` signal for now; polling remains the Plasma path and other clients can poll too.
- [x] Add a proper service wrapper script (`racedock-service`) that resolves the .NET runtime and passes fixed arguments.
- [x] Add D-Bus activation file:
  - `~/.local/share/dbus-1/services/org.foxtrot47.RaceDock.service`
- [x] Add optional systemd user unit with `Restart=on-failure` (`--systemd`).
- [x] Make `--print-state` reliable for CI (waits for the first non-hidden snapshot, bounded by `--print-delay`).
- [x] Ensure logging goes to `~/.local/share/RaceDock/Logs/app.ndjson` by default (`AppPaths.Root`) and never blocks startup.
- [x] Test service restart while the applet is polling. The client reconnects without restarting Plasma
  (`RaceDock.Plasma/tests/Reconnect.qml`).

### Phase 3 — Settings and configuration UI

- [x] Add `RaceDock.Plasma/package/contents/config/config.qml`.
- [x] Add `ConfigGeneral.qml` with:
  - next-session idle mode
  - animations
  - single-row panel mode
  - rotation interval
  - stale-data timeout
  - restore defaults
- [x] Add a per-championship page (`ConfigChampionshipPage.qml`, with fifteen static wrappers) for
  the live-timing power switch, taskbar/flyout competitor counts, manufacturer logos,
  widget/flyout options, position colours, sector mode, and column order and visibility.
- [x] Map each championship key to its real `UserSettings` power switch (`enableWec`, `enableElms`,
  `enableWrc`, …) in `SettingsCatalog.enableKeyFor`; a name derived from the enum key silently left
  WEC/ELMS/WRC unfetched.
- [x] Load the complete settings JSON from `GetSettings`, mutate a copy, and send the complete object through `ApplySettings`.
- [x] Add a recoverable "Restore defaults" path (`resetSettings` command).
- [x] Add a "Test scenario / return to live" action on the **Testing** page (`ConfigTesting.qml`),
  together with archive replay and a calendar refresh.
- [x] Wire the flyout's configure button to `Plasmoid.internalAction("configure").trigger()`.

### Phase 4 — Plasma UI completion

- [x] Verify `CompactWidget` in a horizontal panel:
  - 1–5 competitor cells
  - single-row vs two-row mode
  - leader/fastest borders
  - pit/retired/stopped labels
  - yellow/red/SC/VSC/chequered flags
  - idle headline/countdown and hidden mode
  Two-row and single-row modes were seen in a real panel (2026-10-29), including logos, the context
  column, and live settings changes; flag variants, pit/retired labels, and the idle strip were
  verified offscreen with `Screenshots.qml`.
- [x] Verify `FlyoutPanel` in the anchored popup:
  - column order and visibility
  - sector and split renderers
  - tyre and WEC wheel-age renderers
  - energy bar
  - empty/offline state
  - schedule mode
  - scroll behavior with 20–40 rows
  The live classification, tyre badges, logos, and the anchored host were seen in a real panel. Column
  order, sectors/splits, WEC wheel ages, energy, schedule, empty/offline, and a 20-row scroll were
  verified offscreen (scrolling also works in the real config dialog).
- [x] Verify the pinned `PlasmaCore.Dialog`:
  - pin from anchored -> close popup -> show floating window
  - close/unpin -> hide floating window
  - drag via `startSystemMove`
  - stays on top without stealing focus incorrectly
  - restores/does not restore across Plasma restarts
  All but the restart-restore case were checked on X11 (2026-10-29); the dialog does not persist
  across a Plasma restart by design.
- [x] Verify panel and flyout sizing at 1080p and fractional scaling. Checked at 1080p/1.0 and
  1080p/1.5 (panel, flyout, config dialog); 1440p and multi-monitor are still open.
- [x] Add keyboard handling:
  - Escape closes the flyout/pinned window
  - accessible names for pin/close/compact
- [x] Check vertical-panel behavior. Declared horizontal-only: the strip renders but clips in a 44 px
  vertical panel, and the README says so.
- [x] Remove the temporary `Test.qml` from the shipping package and replace it with tests outside `contents/ui` (`RaceDock.Plasma/tests/`).

### Phase 5 — Packaging and install

- [x] Choose a release layout:
  - framework-dependent publish plus a wrapper that resolves `DOTNET_ROOT`.
- [x] Add `scripts/install-linux.sh` and `scripts/uninstall-linux.sh`.
- [x] Install the plasmoid with `kpackagetool6 --type Plasma/Applet --install` or `--upgrade`.
- [x] Install the D-Bus service file and wrapper into the user prefix.
- [x] Install the systemd user unit if chosen.
- [x] Update `metadata.json` version and description for the Linux front end.
- [x] Add a README section for Linux/Plasma installation and the sidecar requirement.
- [x] Add CI for:
  - `dotnet build` on Linux
  - `dotnet test` on Linux
  - QML syntax/lint checks (package UI and the `tests/` harnesses)
  - package validation with `kpackagetool6`
  (`.github/workflows/linux.yml`)

### Phase 6 — End-to-end validation

- [x] Start the sidecar with `--scenario --print-state --print-delay 2` and inspect widget/flyout JSON.
- [x] Run the sidecar on the Plasma session bus and verify every D-Bus method with `gdbus`.
- [x] Run the QML client harness against the sidecar:
  - widget live state
  - flyout live state
  - settings load/apply/reset
  - pause command
  - config pages load and apply
  (resume/speed/restart are covered by Core session tests)
- [x] Replay an archive through the service and verify classification, sectors, tyres, and flag transitions
  (synthetic F1 dump).
- [x] Replay a real captured F1 `.ndjson` through `--archive` and confirm WEC energy rendering
  (real F1 capture from Practice 1; WEC energy via the recorded Barcelona fixture, since no WEC session
  was on track during the capture window).
- [x] Exercise a live on-track session with F1 enabled and confirm classification, tyres, sectors, and
  live settings changes. Feed startup and the idle schedule are verified for all feeds.
- [ ] Confirm rotation and stale/offline handling with several live feeds at once.
- [x] Confirm the applet recovers when the sidecar starts after Plasma (`RaceDock.Plasma/tests/Startup.qml`).
  This covers the polling client; watching it in a real panel is still part of the visual verification.
- [x] Confirm the applet does not crash when the sidecar exits mid-session
  (`RaceDock.Plasma/tests/Reconnect.qml` stops and replaces the sidecar under a polling client).

### Phase 7 — macOS reuse (later)

- [ ] Keep `RaceDock.Core.Presentation` free of Linux, D-Bus, and Plasma types.
- [ ] Define a small transport-neutral session interface for a future macOS host.
- [ ] Reuse `RaceDockSession`, `RaceDockWidgetState`, `RaceDockFlyoutState`, and the JSON contract where possible.
- [ ] Do not move QML- or WPF-specific layout decisions into Core.

## Build and test commands

Set up the user-local SDK:

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
```

Build:

```bash
cd /home/foxtrot47/RaceDock
dotnet build RaceDock.Core/RaceDock.Core.csproj
dotnet build RaceDock.Linux/RaceDock.Linux.csproj
```

Run the tests. `RaceDock.Tests` is a custom executable runner, not an xunit project, so `dotnet test`
only restores:

```bash
dotnet run --project RaceDock.Tests/RaceDock.Tests.csproj --no-launch-profile
```

Deterministic snapshot without D-Bus:

```bash
dotnet RaceDock.Linux/bin/Debug/net10.0/racedock-service.dll \
  --scenario --print-state --print-delay 2
```

Capture live payloads, then replay them:

```bash
dotnet RaceDock.Linux/bin/Debug/net10.0/racedock-service.dll \
  --dump-feed /tmp/opencode/dumps --log-folder /tmp/opencode/logs
# Ctrl-C or SIGTERM to stop; the folder is printed at startup and in app.ndjson.
dotnet RaceDock.Linux/bin/Debug/net10.0/racedock-service.dll \
  --archive /tmp/opencode/dumps/feed-dump-*/formula1.ndjson --print-state --print-delay 5
```

D-Bus smoke test in an isolated bus:

```bash
dbus-run-session -- bash -lc '
  dotnet RaceDock.Linux/bin/Debug/net10.0/racedock-service.dll --scenario &
  sleep 2
  gdbus call --session --dest org.foxtrot47.RaceDock \
    --object-path /org/foxtrot47/RaceDock \
    --method org.foxtrot47.RaceDock1.Ping
  gdbus call --session --dest org.foxtrot47.RaceDock \
    --object-path /org/foxtrot47/RaceDock \
    --method org.foxtrot47.RaceDock1.GetWidgetState
'
```

QML lint/test tools are not installed system-wide in this environment. `qmllint` and `qmltestrunner`
were extracted from `qt6-declarative-dev-tools` (plus `libqt6qmlcompiler6`, `libqt6quicktest6`,
`libqt6test6`, and `qml6-module-qttest`) into `/tmp/qmltools2`, `/tmp/qmllibs`, `/tmp/qmllibs2`,
`/tmp/qmltestlib`, and `/tmp/qmlmods`. On a normal dev machine, install `qt6-declarative-dev-tools` and
`qml6-module-qttest` and use the system binaries.

The integration harness needs a running sidecar on the same bus and an offscreen platform:

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
export QT_QPA_PLATFORM=offscreen
dbus-run-session -- bash -lc '
  dotnet RaceDock.Linux/bin/Debug/net10.0/racedock-service.dll --scenario --paused &
  sleep 3
  qmltestrunner -input RaceDock.Plasma/tests/Test.qml
'
```

With the extracted tools, prepend `PATH=/tmp/qmltools2/usr/lib/qt6/bin:$PATH`,
`LD_LIBRARY_PATH=/tmp/qmllibs2/usr/lib/x86_64-linux-gnu:/tmp/qmltestlib/usr/lib/x86_64-linux-gnu:/tmp/qmllibs/usr/lib/x86_64-linux-gnu:/usr/lib/x86_64-linux-gnu`,
and `QML_IMPORT_PATH=/tmp/qmlmods/usr/lib/x86_64-linux-gnu/qt6/qml`.

The lifecycle harnesses need the shell to start or replace the sidecar while the client polls, so they
have their own orchestration. `Startup.qml` expects no service at launch and connects to the one started
five seconds later; `Reconnect.qml` expects an outage and a replacement:

```bash
dbus-run-session -- bash -lc '
  QT_QPA_PLATFORM=offscreen qmltestrunner -input RaceDock.Plasma/tests/Startup.qml &
  runner=$!
  sleep 5
  dotnet RaceDock.Linux/bin/Debug/net10.0/racedock-service.dll --scenario --paused &
  pid=$!
  wait $runner; kill $pid 2>/dev/null
'

dbus-run-session -- bash -lc '
  dotnet RaceDock.Linux/bin/Debug/net10.0/racedock-service.dll --scenario --paused &
  pid=$!
  sleep 3
  QT_QPA_PLATFORM=offscreen qmltestrunner -input RaceDock.Plasma/tests/Reconnect.qml &
  runner=$!
  sleep 4; kill $pid 2>/dev/null; wait $pid 2>/dev/null
  sleep 5
  dotnet RaceDock.Linux/bin/Debug/net10.0/racedock-service.dll --scenario --paused &
  pid=$!
  wait $runner; kill $pid 2>/dev/null
'
```

Grab the surfaces as PNGs for layout review. Run it from the folder that should receive them, and
point it at a scenario, an archive, or a live service depending on what you want to see:

```bash
mkdir -p /tmp/racedock-shots && cd /tmp/racedock-shots
dbus-run-session -- bash -lc '
  dotnet "$OLDPWD/RaceDock.Linux/bin/Debug/net10.0/racedock-service.dll" --scenario --paused \
    --settings /tmp/racedock-shots/settings.json &
  sleep 3
  QT_QPA_PLATFORM=offscreen qmltestrunner -input "$OLDPWD/RaceDock.Plasma/tests/Screenshots.qml"
'
```

Always pass `--settings`: the harness applies settings, and without it the captures overwrite the
real `~/.local/share/RaceDock/settings.json`.

It writes `panel.png`, `panel-yellow.png`, `panel-redflag.png`, `panel-single-row.png`,
`panel-single-row-yellow.png`, `flyout.png`, `flyout-yellow.png`, `flyout-sectors.png`,
`flyout-scrolled.png` (only for a source with a long classification; the scenario has six rows, so
use an archive for this one), `config-general.png`, `config-championships.png`,
`config-championships-wec.png`, and `config-testing.png`. The offscreen platform has no Breeze style,
so colours come from Kirigami's fallback theme; these captures check layout and content, not the
exact Plasma palette.

Install from the checkout:

```bash
scripts/install-linux.sh              # sidecar, wrapper, D-Bus activation, applet
scripts/install-linux.sh --systemd    # also install and start a systemd user unit
scripts/install-linux.sh --no-applet  # sidecar only
scripts/uninstall-linux.sh --purge    # remove the app and its settings/logs
```

Manual plasmoid install/update:

```bash
kpackagetool6 --type Plasma/Applet --install RaceDock.Plasma/package
kpackagetool6 --type Plasma/Applet --upgrade RaceDock.Plasma/package
```

## Known gotchas

- **QML DBus message construction:** use `new DBus.dbusMessage({ ... })`, not `DBus.dbusMessage { ... }`
  and not `DBus.DBusMessage`. The constructor map needs `service`, `path`, `iface`, **`interface`**, and
  `member`; `iface` alone is not enough. It ignores `arguments` and an explicit `signature`, so assign
  `message.arguments = [...]` after construction and let Qt derive the wire signature. Setting
  `signature` makes the call arrive with an empty signature and no arguments.
- **Archive replays carry snapshots, not alert history:** archive sources own a private alert arbiter, so
  the session's alerts stay empty during replay. Track-condition flags still render from the snapshot,
  and the presentation builder treats a `SessionLifecycle.Ended` snapshot as CHEQUERED, but
  fastest-lap/race-control alerts do not appear until a source feeds the session arbiter.
- **Feed dumps are only replayable if the feed sent a session bootstrap:** F1 always sends a full
  initial snapshot when the websocket connects, so an F1 capture replays from wherever it started.
  GTWC and WEC only send timing while a session is live, so a `--dump-feed` capture taken between
  sessions contains schedule/discovery payloads and the replay loader rejects it with
  "no usable timing bootstraps".
- **Commands render their pending snapshot:** `CommandAsync` drains `_pending` before `BuildState`, so a
  `step` or `trigger` is visible to the next poll instead of waiting for the 250 ms render loop.
- **Settings JSON is destructive if partial:** `ApplySettings` deserializes a full `UserSettings`. Always
  round-trip what `GetSettings` returned. `resetSettings` is the only intentional way to get defaults.
- **Startup state can be hidden:** `GetWidgetState` may return `mode: "hidden"` for a second until the
  scenario/live source emits its first snapshot. The UI must tolerate this.
- **Polling is intentional:** do not block on the absence of D-Bus signals in QML. Keep the 1s compact
  poll and the 750ms flyout poll unless the transport changes. Set `pollWidget: false` for config pages.
- **Panel width is user-visible:** `CompactWidget` exposes `Layout.preferredWidth` derived from
  `display.competitorCount`; changing it requires re-checking panel layout and overflow. Two-row mode
  is `min(570, 12 + count * 78 + 100)` and single-row is `min(620, 12 + count * 74 + 150)`: the
  single-row context block reserves 50 px more (100 → 150) and each cell 4 px less (78 → 74), because
  the position number is inline rather than a tile. Re-check both display modes when the width
  formula changes.
- **Single-row is a Core setting, not a QML choice:** `CompactWidget` reads `display.singleRow`
  (serialized from `UserSettings.UseSingleRowTaskbar`). Do not read `plasmoid.configuration` for it, and
  keep the cell detail text in one place (`detailText`) so the two layouts cannot drift.
- **Pinned window on Wayland:** compositor placement rules differ from X11. Do not assume `x`/`y` work;
  verify `startSystemMove` and initial placement separately.
- **Config pages lay their rows out one spacing too wide:** `SimpleKCM`/`ScrollablePage` sizes the
  content before the page has its final width, and Qt Quick Layouts keeps that first-pass geometry,
  so every direct child of a page's `ColumnLayout` ends up ~19 px wider than the column and the
  right edge is clipped. `Layout.fillWidth` alone does not fix it; cap each row with
  `Layout.maximumWidth: <pageColumn>.width` (as `ConfigTesting.qml` does). Verify with the offscreen
  harness at a 594 px content width, not with a full-width standalone window.
- **Config pages must not build dialogs while loading:** `QtQuick.Dialogs.FileDialog` opens a window
  when it is instantiated, and plasmashell builds config pages off-scene, so a dialog created at page
  build crashes the config dialog. Declare it inside a `Component` and create it on demand through a
  `Loader { active: false }` that the button flips on (`ConfigTesting.qml`).
- **`ConfigModel` is not a QML file:** Plasma registers `org.kde.plasma.configuration` inside
  plasmashell, so `qmllint`/`qmltestrunner` report `ConfigCategory` as unresolved and cannot instantiate
  `config.qml`. That warning is expected; the pages themselves are testable.
- **`main.xml` is a placeholder:** it exists so Plasma offers the Configure action. The settings source
  is the service's `settings.json`; config pages must not read or write `plasmoid.configuration`.
- **SettingsCatalog keys mirror JSON:** championship keys and column flags are camel-cased enum names
  (`worldEnduranceChampionship`, `flyoutShowSectorTimes`). Renaming Core enums requires updating that
  file and the JSON contract tests.
- **No visual verification yet:** do not mark the Plasma UI complete until it has been seen on an
  unlocked Plasma/Wayland session at least once. (2026-10-24: verified on Xvfb/X11 and on the user's
  live WSLg Wayland session via a Windows-side desktop capture; the strip renders idle and live.)
- **Plasma 6.3 tooltip API:** `Plasmoid.toolTipSubText` no longer exists; set `toolTipMainText`/
  `toolTipSubText` directly on the `PlasmoidItem` root or the applet fails to load.
- **Manufacturer logos:** Core resolves team/manufacturer names to asset file names via the embedded
  `Assets/ManufacturerLogos/manifest.json` alias catalog (`ManufacturerLogos.ResolveAsset`); rows and
  widget competitors carry `manufacturerLogo`, the flyout carries `showManufacturerLogos`. Artwork
  ships per front end (WPF `Assets/ManufacturerLogos`, Plasma `contents/ui/logos/`); keep the two
  asset folders and the Core manifest in sync when logos change.
- **Config UI parity:** config page strings mirror the Windows settings UI
  (`ChampionshipSettingsPresentation.cs`, `SettingsDraft.cs` column titles, Testing page wording);
  `SettingsCatalog.qml` is the ported catalog. Config pages are per championship (15 static wrapper
  pages over `ConfigChampionshipPage.qml`) plus General and Testing. The taskbar strip mirrors the
  Windows widget: a small gradient square behind the position number only, competitor cells across
  the top, and the event line as a full-width second row (single-row mode keeps one line).
  `viewMode` ("live"/"scenario"/"archive") on the widget/flyout states drives the Testing transport
  bar; replay is wired through the `archive`/`live` commands. Windows-only cards (sign-in autostart,
  Mica blur, taskbar position) are intentionally not shown on Plasma.

## Definition of done

- The sidecar installs and starts automatically on the Plasma session bus.
- The panel widget renders live timing, alerts/flags, and idle schedule state without layout breakage.
- The flyout renders the configured columns, schedule, and empty/offline states.
- The pinned flyout can be opened, moved, kept on top, and closed without breaking the applet.
- Settings round-trip through the service and persist across restarts.
- Scenario, archive replay, and live feeds all work through the same shared session layer.
- Core tests and QML tests pass in CI.
- The Windows app still builds unchanged, and Core has no Linux-only dependencies.
