# RaceDock Linux / Plasma Plan

Status: implementation in progress — shared Core session, the full Plasma widget/flyout, packaging scripts, and the settings UI are in the tree. Visual verification on a live Plasma/Wayland session and live-feed validation are still outstanding.

Last updated: 2026-10-23

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
  - Panel strip: live cells, alerts/flags, idle next-session state.
- `RaceDock.Plasma/package/contents/ui/FlyoutPanel.qml`
  - Shared flyout body: header, table header, classification list, schedule state, footer, pin/close.
- `RaceDock.Plasma/package/contents/ui/ClassificationRow.qml`
  - Configurable optional columns.
- `RaceDock.Plasma/package/contents/ui/ScheduleView.qml`
  - "Next on track" schedule state.
- `RaceDock.Plasma/package/contents/ui/SettingsCatalog.qml`
  - Championship keys/labels and column keys/flags shared by the config pages.
- `RaceDock.Plasma/package/contents/ui/ConfigGeneral.qml`
  - Config page: championships, rotation, retention, competitor counts, idle/animations, restore defaults.
- `RaceDock.Plasma/package/contents/ui/ConfigColumns.qml`
  - Config page: per-championship column order and visibility.
- `RaceDock.Plasma/package/contents/config/config.qml`
  - `ConfigModel` that lists the two pages.
- `RaceDock.Plasma/package/contents/config/main.xml`
  - Placeholder config group so the Configure action exists; it is not the settings source.
- `RaceDock.Plasma/tests/Test.qml`
  - `qmltestrunner` integration harness. It lives outside `contents/`, so packaging does not ship it.

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
- Full suite is green on Linux: **139/139** (122 original + 9 presentation + 8 session tests).
- Created the D-Bus sidecar and verified every method (`Ping`, `GetWidgetState`, `GetFlyoutState`, `GetSettings`, `ApplySettings`, `Command`) on an isolated session bus.
- Added shared presentation DTOs, presentation builder, and headless `RaceDockSession`.
- Added service CLI: `--scenario`, `--archive`, `--settings`, `--log-folder`, `--timezone`, `--paused`, `--print-state`, `--print-delay`.
- Implemented the Plasma UI: `main.qml`, `RaceDockClient.qml`, `CompactWidget.qml`, `FlyoutPanel.qml`, `ClassificationRow.qml`, `ScheduleView.qml`.
- Added Escape-to-close, accessible names for pin/close/compact, configure buttons in both flyout hosts, and WEC wheel cells with tooltips.
- Hardened `RaceDockSession`: coalesced `StateChanged` dispatch outside the source-switch semaphore, fallback-source cleanup, idempotent disposal, hide-championship snapshot clearing, and error payloads for commands after shutdown.
- Made `--print-state` wait for the first non-hidden snapshot instead of sleeping for a fixed delay.
- Added the settings UI: `config.qml`, `ConfigGeneral.qml`, `ConfigColumns.qml`, and `SettingsCatalog.qml`; `resetSettings` restores Core defaults.
- Added packaging: `scripts/install-linux.sh`, `scripts/uninstall-linux.sh`, `scripts/linux/racedock-service`, D-Bus activation, and an optional systemd user unit.
- Verified install → wrapper → `--print-state`, D-Bus activation, `kpackagetool6 --install`, and the generated activation file in a throwaway XDG prefix.
- Verified the QML client and both config pages against a running sidecar with `qmltestrunner`: live state, settings round-trip, pause, column reorder, and defaults reset all pass.
- Added the Linux CI workflow (`.github/workflows/linux.yml`) and a Linux README section.
- Validated archive replay through the service with a synthetic F1 `.ndjson` dump: classification, gaps,
  tyre ages, sectors/mini-sectors, yellow → clear → chequered transitions, and scenario ↔ archive switching.
- Confirmed the live service connects the enabled feeds (F1, GTWC, WEC, WRC) and fills the idle
  widget/flyout from the published schedule (US GP Practice 1 was the next session).

### In progress

- Visual verification of the compact strip, anchored flyout, and pinned dialog under Wayland.
- Live timing classification through the Linux service: the feeds connect and the schedule populates, but
  no on-track session has been observed yet (only idle/schedule state).

### Blocked / unverified

- Visual verification was blocked because the WSLg Plasma session was locked and later exited. Screenshot tooling (`spectacle`, ImageMagick `import`) could not capture the session. Restart WSLg/Plasma and verify on a real display before calling the UI done.
- The pinned `PlasmaCore.Dialog` path is implemented but has not been visually tested: initial placement, drag/move (`startSystemMove`), `DialogWindow` flags, `hideOnWindowDeactivate`, and sizing need confirmation.
- Panel width/height behavior in horizontal vs vertical panels and on multi-monitor setups is unverified.
- The applet reconnecting after the sidecar exits/restarts is handled as an error state in the client but has not been watched on a real panel.
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

- [ ] Add a `GetStatus`/version/capabilities method if the front end needs it.
- [x] Decided not to emit a `Changed(s topic)` signal for now; polling remains the Plasma path and other clients can poll too.
- [x] Add a proper service wrapper script (`racedock-service`) that resolves the .NET runtime and passes fixed arguments.
- [x] Add D-Bus activation file:
  - `~/.local/share/dbus-1/services/org.foxtrot47.RaceDock.service`
- [x] Add optional systemd user unit with `Restart=on-failure` (`--systemd`).
- [x] Make `--print-state` reliable for CI (waits for the first non-hidden snapshot, bounded by `--print-delay`).
- [x] Ensure logging goes to `~/.local/share/RaceDock/Logs/app.ndjson` by default (`AppPaths.Root`) and never blocks startup.
- [ ] Test service restart while the applet is polling. The client should reconnect without restarting Plasma.

### Phase 3 — Settings and configuration UI

- [x] Add `RaceDock.Plasma/package/contents/config/config.qml`.
- [x] Add `ConfigGeneral.qml` with:
  - enabled championships
  - rotation interval
  - completed-session retention
  - taskbar competitor count (1–5)
  - flyout competitor count (5–40)
  - single-row panel mode
  - animations
  - show next session when idle
  - position colors
  - manufacturer logos
- [x] Add a column configuration page (`ConfigColumns.qml`) with per-championship order and visibility.
- [x] Load the complete settings JSON from `GetSettings`, mutate a copy, and send the complete object through `ApplySettings`.
- [x] Add a recoverable "Restore defaults" path (`resetSettings` command).
- [ ] Add a "Test scenario / return to live" developer action, ideally hidden behind a settings or debug control.
- [x] Wire the flyout's configure button to `Plasmoid.internalAction("configure").trigger()`.

### Phase 4 — Plasma UI completion

- [ ] Verify `CompactWidget` in a horizontal panel:
  - 1–5 competitor cells
  - single-row vs two-row mode
  - leader/fastest borders
  - pit/retired/stopped labels
  - yellow/red/SC/VSC/chequered flags
  - idle headline/countdown and hidden mode
- [ ] Verify `FlyoutPanel` in the anchored popup:
  - column order and visibility
  - sector and split renderers
  - tyre and WEC wheel-age renderers
  - energy bar
  - empty/offline state
  - schedule mode
  - scroll behavior with 20–40 rows
- [ ] Verify the pinned `PlasmaCore.Dialog`:
  - pin from anchored -> close popup -> show floating window
  - close/unpin -> hide floating window
  - drag via `startSystemMove`
  - stays on top without stealing focus incorrectly
  - restores/does not restore across Plasma restarts
- [ ] Verify panel and flyout sizing at 1080p, 1440p, and fractional scaling.
- [x] Add keyboard handling:
  - Escape closes the flyout/pinned window
  - accessible names for pin/close/compact
- [ ] Check vertical-panel behavior. Either support it or explicitly declare horizontal-only in metadata/settings.
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
  - QML syntax/lint checks
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
  (synthetic F1 dump; a real captured dump is still worth replaying once one is available).
- [ ] Replay a real captured F1/GTWC/WEC `.ndjson` and confirm WEC energy rendering through `--archive`.
- [ ] Exercise a live on-track session with F1/WEC/WRC enabled and confirm rotation and stale/offline handling.
  Feed startup and the idle schedule are already verified.
- [ ] Confirm the applet recovers when the sidecar starts after Plasma.
- [ ] Confirm the applet does not crash when the sidecar exits mid-session.

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
- **Commands render their pending snapshot:** `CommandAsync` drains `_pending` before `BuildState`, so a
  `step` or `trigger` is visible to the next poll instead of waiting for the 250 ms render loop.
- **Settings JSON is destructive if partial:** `ApplySettings` deserializes a full `UserSettings`. Always
  round-trip what `GetSettings` returned. `resetSettings` is the only intentional way to get defaults.
- **Startup state can be hidden:** `GetWidgetState` may return `mode: "hidden"` for a second until the
  scenario/live source emits its first snapshot. The UI must tolerate this.
- **Polling is intentional:** do not block on the absence of D-Bus signals in QML. Keep the 1s compact
  poll and the 750ms flyout poll unless the transport changes. Set `pollWidget: false` for config pages.
- **Panel width is user-visible:** `CompactWidget` exposes `Layout.preferredWidth` derived from
  `display.competitorCount`; changing it requires re-checking panel layout and overflow.
- **Pinned window on Wayland:** compositor placement rules differ from X11. Do not assume `x`/`y` work;
  verify `startSystemMove` and initial placement separately.
- **`ConfigModel` is not a QML file:** Plasma registers `org.kde.plasma.configuration` inside
  plasmashell, so `qmllint`/`qmltestrunner` report `ConfigCategory` as unresolved and cannot instantiate
  `config.qml`. That warning is expected; the pages themselves are testable.
- **`main.xml` is a placeholder:** it exists so Plasma offers the Configure action. The settings source
  is the service's `settings.json`; config pages must not read or write `plasmoid.configuration`.
- **SettingsCatalog keys mirror JSON:** championship keys and column flags are camel-cased enum names
  (`worldEnduranceChampionship`, `flyoutShowSectorTimes`). Renaming Core enums requires updating that
  file and the JSON contract tests.
- **No visual verification yet:** do not mark the Plasma UI complete until it has been seen on an
  unlocked Plasma/Wayland session at least once.

## Definition of done

- The sidecar installs and starts automatically on the Plasma session bus.
- The panel widget renders live timing, alerts/flags, and idle schedule state without layout breakage.
- The flyout renders the configured columns, schedule, and empty/offline states.
- The pinned flyout can be opened, moved, kept on top, and closed without breaking the applet.
- Settings round-trip through the service and persist across restarts.
- Scenario, archive replay, and live feeds all work through the same shared session layer.
- Core tests and QML tests pass in CI.
- The Windows app still builds unchanged, and Core has no Linux-only dependencies.
