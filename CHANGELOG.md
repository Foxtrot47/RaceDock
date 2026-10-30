# Changelog

All notable RaceDock changes are recorded here. Release packages include the corresponding version entry.

## [1.5.0] — 2026-10-30

### Added

- NASCAR Cup Series, Xfinity Series and CRAFTSMAN Truck Series live timing, with stage progress, playoff eligibility, and PIT, DVP and OUT statuses.
- F1 Academy live timing and its season calendar, with lap and sector times and the F1 team each driver races for.
- The classification flyout lists every live series and switches between them on demand instead of only when the rotation reaches them.
- A pinned classification holds the series it was pinned to, and a feed that ends is left in place as finished with its tab disabled instead of being replaced by another stream.
- Settings opens from the flyout and from the schedule.
- NASCAR and F1 Academy publish schedules, so the idle taskbar counts down to their next session.

### Changed

- The four new series start disabled on an existing install, so nothing begins polling them unasked.
- INDYCAR Chevrolet entries show the Chevrolet bowtie; sportscar Chevrolet entries keep the Corvette mark.
- Flyout buttons sit on their own row above the event title, and series marks are trimmed of their padding.

### Fixed

- The taskbar widget is rebuilt after Windows closes it instead of staying gone until RaceDock restarts.
- A flyout opened from a taskbar at the top of the screen is anchored inside the monitor's work area instead of above it.
- A classification that has finished is dropped when its feed goes off, instead of staying on the taskbar.
- A car is no longer listed twice when a timing hub sends a position update keyed by display slot rather than by car number.
- A pinned classification updates as soon as its own feed does, instead of waiting for the rotation to come back around.
- Flyout badges are centred on the title row rather than crowding the settings and pin buttons.

## [1.4.0] — 2026-10-22

### Added

- Full-course yellow is its own track state, shown by the taskbar flag and a FULL COURSE YELLOW badge in the classification header.
- Formula 1 classifications show live mini-sectors for the lap in progress.
- World Endurance Championship classifications show each car's reported virtual energy allocation and four-wheel tyre ages with compound colours.
- Formula 1 tyre ages are shown next to the compound.
- World Rally Championship classifications show stage split times.
- The idle taskbar and schedule list carry every remaining session of the next event.
- The testing page replays recorded Formula 1 feed dumps, RaceDock GTWC and WEC feed dumps, and Timing71 archives with their sector times.

### Changed

- Feeds with no live session poll their providers less often.
- A whole-track yellow from the Griiip feeds (WEC, ELMS, Le Mans Cup, DTM), an INDYCAR caution, and a recorded archive caution are reported as a full-course yellow, while sector-scoped yellows stay local.

### Fixed

- The packaged app icon no longer draws an accent-coloured plate behind the taskbar icon, and ships the icon at every size Windows asks for.
- Alerts that lose the priority race to a persistent alert are no longer consumed by it, and a second yellow later in the same session is announced again.
- GTWC keeps re-reading the schedule while a unit stops answering, so the session that replaced it is picked up instead of the feed retrying a dead unit.
- Schedule rows for meetings without a published clock time keep their calendar date in every timezone, show no invented time, and name the time as unknown.
- Formula 1 tyre ages report every lap run on a set instead of subtracting the age a used set was fitted with.
- Formula 1 qualifying follows the active qualifying part, empty MotoGP responses keep the session connected, and manufacturer logos keep their spacing when a championship publishes none.

## [1.3.2] — 2026-09-06

### Added

- Formula 1 classifications can optionally show colour-coded S1, S2, and S3 timing for the current or last lap, or for each driver's official best lap.

### Fixed

- Formula 1 practice and qualifying now derive gaps and intervals from ranked best laps instead of showing race-style values such as lap counts.

## [1.3.1] — 2026-09-05

### Changed

- Dashboard cards and championship navigation follow a popularity-focused order.
- Circuit of the Americas is shown as COTA on compact live and schedule surfaces.
- Idle taskbar schedules size to their content, keep session details stationary in one-row mode, and use consistent taskbar typography.

### Fixed

- One-row classifications, live session context, and upcoming-session titles use constrained layouts to prevent clipping and excessive spacing.
- Overflowing taskbar text now loops continuously across transient Windows shell visibility changes.
- The packaged app icon no longer places an opaque square behind the speedometer artwork.
- WEC entrants without explicit manufacturer data now resolve to the appropriate Ferrari, Mercedes-AMG, or Aston Martin mark, and Peugeot artwork retains its original colors.

## [1.3.0] — 2026-09-05

### Added

- Live timing and upcoming-session schedules for the World Endurance Championship, including compact Hypercar and LMGT3 classifications; WEC is enabled by default.
- Hidden, disabled-by-default live timing and schedule integrations for the European Le Mans Series and Michelin Le Mans Cup.
- An optional one-row taskbar layout with inline positions, driver codes, and intervals.

### Fixed

- Formula 1 practice and qualifying placeholders remain hidden until a driver records a valid lap time.

## [1.2.0] — 2026-08-31

### Added

- Between sessions the taskbar shows the next session of the championships you follow and counts down to it. Clicking the countdown opens the upcoming schedule with championship logos, venues, and local start times. Settings › General turns it off for an empty taskbar when nothing is running.

### Changed

- Taskbar, schedule, classification, and Settings surfaces use concise event names without provider boilerplate or title-sponsor wording. Raw feed names remain unchanged in diagnostics.
- The Dashboard header now presents the RaceDock name and version without redundant page labels.

### Fixed

- Formula 1 practice sessions now appear in the upcoming schedule and wake the live feed; previously the calendar skipped directly to qualifying.
- The taskbar widget now opens from a click anywhere across its body, rather than only where opaque text or artwork is drawn.
- The classification no longer opens as an empty panel: a flyout that was never shown could be pushed on screen by the closing animation that follows the pointer leaving the taskbar strip.
- Championship names in the settings navigation pane no longer run underneath the session status badge: a championship showing a badge is listed by its short name until the badge clears.

## [1.0.2] — 2026-08-28

### Added

- A What's new page in Settings shows the release notes for the running version and earlier versions, instead of sending you to the commit history.
- Builds downloaded from the project releases check whether a newer release was published and link to it. Microsoft Store installations update through the Store and do not perform this check.

### Changed

- Countdowns target the next session rather than the start of the race weekend: rally stages, motorcycle class sessions, and GT World Challenge track sessions each carry their own time.

### Fixed

- The back button in Settings now returns to the previous page instead of doing nothing.

## [1.0.1] — 2026-08-28

### Added

- Championship cards and championship pages count down to the next published session for Formula 1, MotoGP, Moto2, Moto3, the World Rally Championship, DTM, and GT World Challenge Europe, so race information is available between sessions.
- Launching RaceDock now opens the Settings window, so the app always shows something when started by hand.
- Starting RaceDock while it is already running brings the Settings window back instead of quitting silently.

### Changed

- Sign-in launches stay in the notification area: unpackaged startup entries carry a startup flag, and the packaged build recognizes startup-task activation.

## [1.0.0] — 2026-08-24

### Added

- Rebranded the application as RaceDock with dedicated speedometer identity artwork.
- Live timing for Formula 2, Formula 3, Formula E, DTM, INDYCAR, MotoGP, Moto2, Moto3, GT World Challenge Europe, IMSA, and the World Rally Championship alongside Formula 1.
- Championship-specific taskbar and flyout display profiles, configurable columns, taskbar placement, and championship visibility.
- A redesigned Settings experience with timing status, per-championship controls, archive replay, and deterministic testing scenarios.
- Timing71 archive and compatible HAR replay with pause, restart, stepping, and playback-speed controls.
- Structured logs, crash markers, settings recovery, versioned settings persistence, and provider schema-drift diagnostics.
- An unsigned Microsoft Store MSIX packaging workflow with packaged startup registration and legacy-settings migration.

### Changed

- Settings and logs are stored under `%LocalAppData%\RaceDock`.
- Archive replay generates snapshots from a rolling state, substantially reducing memory use on long recordings.
- Completed classifications remain visible for the configured retention interval.
- Application theme changes now refresh the Settings window, taskbar surface, classification flyout, and racing-state badges.
- Automatic startup registration is limited to explicit distribution builds.

### Fixed

- Stabilized acrylic classification flyout opening, closing, and rapid reopening without fading blurred content to an empty surface.
- Corrected Formula 1 qualifying and sprint-qualifying part labels and the leader interval presentation.
- Hardened Formula E, IMSA, DTM, INDYCAR, and motorcycle session selection against stale or unsupported timing data.

[1.0.0]: https://github.com/Foxtrot47/RaceDock/releases/tag/v1.0.0
