# Changelog

All notable RaceDock changes are recorded here. Release packages include the corresponding version entry.

## [0.2.0] — 2026-08-22

### Added

- Live timing for MotoGP, Moto2, Moto3, Formula E, DTM, and INDYCAR.
- Championship-specific taskbar and flyout display profiles.
- Settings dashboard with timing status, visibility controls, archive replay, and deterministic testing.
- Structured logs, crash markers, settings recovery, and versioned settings persistence.
- Reduced archive replay memory use by generating snapshots from a rolling state.
- Windows application identity artwork and an unsigned MSIX packaging path.

### Changed

- Settings and logs are stored under `%LocalAppData%\RaceDock`.
- Completed classifications remain visible for the configured retention interval.
- Formula E, IMSA, DTM, INDYCAR, and motorcycle feed selection is independently guarded against stale or unsupported sessions.

[0.2.0]: https://github.com/Foxtrot47/RaceDock/releases/tag/v0.2.0
