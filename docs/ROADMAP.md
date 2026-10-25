# Development roadmap

This is a prioritized shortlist, not a delivery schedule.

## Implemented in 1.6.0

- Running-action ribbon with nested macro step progress.
- Cancel the current run over persistent TLS or HTTPS; interrupt delays and skip later steps.
- Preserve completed steps and refuse stale cancellation requests.

## Implemented in 1.7.0

- **Board profiles:** create empty/duplicate boards, rename/delete, switch on PC/tablet; credentials are shared and excluded from exports.
- **Automatic backups:** latest20 snapshots, manual backup, full preview and confirmed restore with current credentials, damaged-config recovery/quarantine.
- **Soundpad favourites:** stable identities, shared persistence, filter, missing/duplicate protection, safe removal without Soundpad.

## Further candidates

1. **Recent sounds** — a bounded recent-play history separate from starred favourites.
2. **Explicit OBS state actions** — separate start/stop and mute/unmute choices for macros, reducing accidental reversals caused by toggles.
3. **Connection diagnostics** — recent round-trip distribution, transport/fallback status, and a guided local rehearsal. Network round-trip and touch-to-sound latency must remain separate measurements.

## Hardware checks still needed

Version 1.6/1.7 rendered interfaces and physical touch use, including profiles and favourites; extended USB unplug/replug and Wi-Fi-loss tests; an actual Discord listener check; live hold-to-talk and Voicemeeter routing; long-session stability. See [the hardware report](LIVE-TEST.md) for the earlier completed tests and their limits.
