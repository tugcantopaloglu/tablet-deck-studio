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

## Implemented in 1.8.0

- **PC setup:** visible desktop entry, guided first-run, shared connection checks and direct access to setup tools.
- **Independent OBS settings:** save connection/audio names without resubmitting board contents; explicit password keep/replace/clear, saved-connection checks and input discovery.
- **Navigation protection:** late setup/editor/pairing/Windows requests cannot replace a newer form or reopen a dismissed panel; Windows and OBS forms protect unsaved changes.

## Implemented in 1.9.0

- Connection diagnostics with bounded request/heartbeat samples, percentiles, events, recovery guidance and identity resets.
- Explicit OBS stream/record Start/Stop and input Mute/Unmute actions, also usable in macros.
- Persistent recent linked Soundpad sounds with safe missing/duplicate handling and independent clearing.
- Desktop app/file selection, target checks, custom PNG icons and button preview.
- Offline camera QR pairing with optional permission, manual fallback and certificate/PIN confirmation.
- Opt-in foreground-app profile rules with debounce, editing leases, active-action/hold guards and manual-switch pause.
- Expanded OBS mixer with two main plus eight additional configured inputs.
- Reviewed portable profile packs with selected sound assets, embedded icons, checksum/ZIP validation and inactive imports.

## Implemented in 1.10.0

- Separate Windows management console, inline editors and sidebar; tablet retains its action board.
- Opt-in silent OBS JPEG preview with a separate OBS connection, bounded capture budget, cache/cancellation and lifecycle guards.
- Editable Twitch/Kick channel viewers: desktop provider frames/popout fallback and isolated bridge-less native view.
- Android script allowlist coherence checks; physical LAN addresses first; saved pairing, recent activity, fast-link and USB app status distinguished.
- Bounded local-subnet Wi-Fi repair helper and automatic USB reconnect guidance.

## Following work

Rendered/hardware rehearsals and real provider checks; full-motion video/audio preview as a separate performance-budget decision; optional authenticated chat/moderation features only after a dedicated account/permissions design.

## Hardware checks still needed

Version 1.6–1.9 rendered interfaces and physical touch use, including profiles, favourites, new Tools views and custom icons; real QR camera/permission use and foreground-app switching; extended USB unplug/replug and Wi-Fi-loss tests; an actual Discord listener check; live hold-to-talk and Voicemeeter routing; long-session stability. See [the hardware report](LIVE-TEST.md) for the earlier completed tests and their limits.
