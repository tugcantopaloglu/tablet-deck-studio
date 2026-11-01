# Studio tools — version 1.9

Install the matching Windows companion and Android APK. Their previous board, profiles, pairing, settings and Soundpad integration remain compatible. The APK retains its signing certificate, so install it as an update rather than uninstalling first. Camera permission is optional and requested only when you choose Scan pairing QR.

Choose **Tools** in the toolbar. On the PC, **PC setup → More studio tools** opens the same menu. Android exposes diagnostics, expanded mixer and recent sounds. Profile rules, pack import/export and file selection require the local Windows control panel.

## Connection diagnostics

Choose **Tools → Connection diagnostics → Run 10 checks**. These harmless authenticated probes measure request/reply round trips; they do not run board actions or play audio. Up to 120 manual samples are retained in the current interface session, with median, nearest-rank p95, maximum and failed-check count. Refresh status to update the current link details.

On Android, the screen shows USB or Wi-Fi, persistent TLS connection or HTTPS fallback, up to 120 native heartbeat samples, heartbeat timeouts, and the last 16 link events. Switching transport or pairing again resets native statistics. Manual samples also reset when the host, mode, either port, pairing state or pairing identity changes; results from different links are not combined. Reset measurements clears both sets of samples.

On the PC, these probes measure localhost only. USB helper status is shown separately and does not prove that the tablet is online. Open diagnostics on the tablet to measure its path to the companion. Disconnect events can include app backgrounding and transport changes. Heartbeat timeout count is not a packet-loss measurement.

Network timings exclude touch sampling, rendering, sound buffers, OBS scene completion and broadcast delay. They are not a zero-latency promise. The screen includes USB/Wi-Fi recovery guidance; it does not change your router, firewall or audio devices.

## Explicit OBS controls

In **Edit → Action**, choose **Start streaming**, **Stop streaming**, **Start recording**, **Stop recording**, **Mute OBS input** or **Unmute OBS input**. Input mute actions need the exact OBS input name. These choices are also available as macro steps.

Start/Stop checks the current saved OBS state and sends the requested command only if needed. Repeating Start does not stop an active output; repeating Stop does not start it. Mute/Unmute sets a boolean state. If OBS changes externally during a request, an error may still occur; completed macro steps are retained.

Streaming and recording controls require confirmation, including inside macros. Existing Toggle actions remain available for boards that use them. Never start a real broadcast merely to check configuration; use saved-connection checks and a rehearsal appropriate to your studio.

## Recent Soundpad clips

Choose **Tools → Recent Soundpad sounds**, or **Soundpad → Recent sounds**. The list keeps the last 30 distinct linked sounds successfully accepted by Soundpad, shared between controllers and retained across companion restarts. A replay moves its stable identity to the front. Failed plays and unlinked legacy index-only buttons do not enter history.

Recent history is separate from favourites, Soundpad's native library, board backups and portable packs. Missing or duplicated identities are shown unavailable; the app does not guess another index. Refresh after playing to update the order. Clear history asks for confirmation and keeps favourites and library contents.

Select playback output before pressing Play. The recent screen defaults to Speakers + microphone; choose another output if needed. Soundpad must already be running, and Discord must use the microphone enabled in Soundpad. An accepted command does not prove that another Discord participant heard it.

## Easier desktop editor

Open **Edit**, select a button and choose its action. App buttons offer **Find installed apps** and **Choose app file**. Sound buttons offer **Choose sound file**. The Windows chooser waits for your selection or Cancel; ordinary network requests retain their timeout. Closing a chooser cancels selection, and a late result cannot overwrite another button or panel.

Installed apps are found from registered app paths and Start-menu shortcuts. If an app is missing, browse to its absolute .exe or .lnk path. File selection does not run the target. Shortcuts may include arguments or open another program, so a valid path does not guarantee the intended behavior.

**Button preview** shows its label, icon and action. Choose a custom PNG under 72 KB and at most 512 × 512 pixels, or **Use built-in icon** to remove it. SVG and other active formats are rejected. Icons are stored with the board and count toward the existing one-MB total configuration limit; use smaller images for large collections.

**Check target** validates action structure, file existence, supported shortcuts and required names without running the action. OBS/Soundpad names still need live verification; this check does not open an app, play a clip or execute a hotkey. Save changes to sync the edited button.

## QR pairing

On the PC, choose **Pair / manage tablets → Show pairing QR**. Select Wi-Fi and the PC's private IPv4 address, or USB, then choose **Generate QR and PIN**. For USB, connect a data cable, enable debugging, authorize the PC and establish both tunnels using USB-Connect.cmd or automatic reconnect first.

On Android, open **Connection settings → Scan pairing QR** and allow camera access if you want to scan. The bundled decoder processes frames on the tablet; frames are not uploaded. Cancel or decline permission to use the existing manual form instead.

Scanning fills the address, mode, both transport ports, certificate prefix and PIN. Check that this is your PC, then choose **Pair and connect**. A scan never connects automatically or opens a URL. The normal certificate-pinning and single-use PIN checks still apply. Generate a fresh code if it expires after five minutes. Generating another QR replaces the previous PIN; share codes only with your own tablet.

USB pairing uses loopback. To switch later to Wi-Fi, enter the actual PC Wi-Fi address in Connection settings. QR pairing can carry custom development ports; release USB helpers use the standard HTTPS 8743 and fast-control 8744 ports.

## Automatic profiles

On the PC, choose **Tools → Automatic profiles**. Add a rule with an executable name such as obs64.exe, Discord.exe or your game's .exe filename, then choose an existing profile. Rules compare executable names, not window titles or launch commands. Up to 30 unique app names can be configured.

Enable automatic profiles and save. Close settings and bring the configured app to the foreground for at least two seconds. Unmatched apps keep the current profile. Automatic switching is off by default and never opens an app.

Switching pauses while a controller holds an editing/tool-panel lease, a key is held, an action runs, and for 30 seconds after a manual profile switch. Editing leases renew every three seconds and expire after 12 seconds without renewal, so an abandoned controller does not block switching indefinitely. If a connection fails while editing, a stale profile save is refused rather than applied to a different board. Closing the panel releases its lease.

Missing/deleted target profiles are skipped and reported in status. Rules stay on this PC and are not included in board snapshots or portable packs. Disable and save to return to manual switching.

## Expanded OBS mixer

Choose **Tools → Expanded OBS mixer** on either device. The two existing microphone/desktop inputs are joined by up to eight extra configured inputs. Sliders set -60 to 0 dB; Mute/Unmute buttons control their corresponding OBS inputs. Missing inputs and disconnected OBS disable their controls.

On the PC, choose **Choose extra inputs**, enter a label and exact OBS input name, or use **Find OBS inputs** to load names from the saved OBS connection. Save to apply. The two main inputs are configured separately through Set up OBS. Extra channels are shared across profiles and included in normal configuration backups.

The expanded mixer refreshes levels without replacing a fader currently being dragged. Windows per-app playback volumes remain in App volumes; these OBS controls do not change the Windows master volume or choose Discord's microphone.

## Portable board packs

On the PC, choose **Tools → Portable board packs**. Select a profile, choose **Choose included sounds**, check the local sound files to copy, then **Download pack**. The .deckpack file is a ZIP with profile metadata, embedded PNG icons and only the selected sound assets.

Each sound is limited to 16 MB, each pack to 64 MB, and at most 128 sound assets. Only referenced WAV, MP3 and WMA files can be selected. Unselected/missing local sounds are exported unconfigured. Repeated/nested references share a bundled asset. Only include files you have permission to share.

Packs exclude OBS passwords, paired-device credentials, certificates, other profiles, favourites, recent history, automatic rules, Windows executables and Soundpad's native library. App targets are cleared, including inside macros. Linked Soundpad identities remain references and must exist in the local Soundpad library or be configured again.

To import, select a pack, inspect its name, page/button/sound counts and action types, then choose **Import reviewed pack** and confirm. Only import packs you trust: their buttons can still send hotkeys, open websites and control OBS. No action runs during preview/import. The import creates an inactive profile with new page/button identities; switch to it and review the editor before use. At most ten profiles can exist.

Preview validates allowed ZIP entries, sizes, bounded metadata and sound checksums. It rejects traversal paths, duplicate/unexpected entries and symbolic links. A preview expires after ten minutes and can be imported once; at most two previews await review. Imported sounds are stored beneath the companion's private pack-assets folder and are retained so older backups can still reference them. Deleting a profile does not automatically delete those files. Back up your private data folder if you need to retain imported sound assets when moving PCs.

## Verification and following phase

Automated fixtures cover storage, authentication, repeated OBS state commands, native QR round trips, pack validation and upload, controller navigation and unsaved-state protection. They do not replace rendered layout, real camera scanning, physical touch, keyboard/screen-reader use, foreground-app integration, or a studio audio rehearsal. See [VERIFICATION.txt](VERIFICATION.txt) for the current evidence and the disclosed test-runner startup incident.

Version 1.10 adds local silent OBS preview and public Twitch/Kick viewers, along with a separate Windows console. See [monitoring and desktop instructions](MONITORING.md). Full-motion video/audio and authenticated chat/moderation remain separate future work; these tools do not automatically connect streaming accounts.
