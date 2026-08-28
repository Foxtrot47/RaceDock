# Changelog

All notable RaceDock changes are recorded here. Release packages include the corresponding version entry.

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
