# Development roadmap

This is a prioritized shortlist, not a delivery schedule.

## Implemented in 1.6.0

- Running-action ribbon with nested macro step progress.
- Cancel the current run over persistent TLS or HTTPS; interrupt delays and skip later steps.
- Preserve completed steps and refuse stale cancellation requests.

## Next candidates

1. **Board profiles** — separate gaming, recording, Discord, and work boards, with quick switching and per-profile page selection. Keep OBS credentials and paired-device access outside profile exports.
2. **Automatic board backups** — keep a bounded history before saves/imports, preview a restore, preserve current credentials, and recover from a damaged configuration.
3. **Soundpad favourites and recent sounds** — a small personal collection above the full library, using stable sound identities so reordering does not change a favourite's sound.
4. **Explicit OBS state actions** — separate start/stop and mute/unmute choices for macros, reducing accidental reversals caused by toggles.
5. **Connection diagnostics** — recent round-trip distribution, transport/fallback status, and a guided local rehearsal. Network round-trip and touch-to-sound latency must remain separate measurements.

## Hardware checks still needed

Version 1.6.0 ribbon visibility and touch cancellation on the physical tablet; extended USB unplug/replug and Wi-Fi-loss tests; an actual Discord listener check; live hold-to-talk and Voicemeeter routing; long-session stability. See [the hardware report](LIVE-TEST.md) for the earlier completed tests and their limits.
