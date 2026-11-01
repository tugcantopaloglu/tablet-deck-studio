# Physical Android test — 2026-10-25

## Version 1.10 development and connection investigation — 2026-11-01

### Version1.11 follow-up on the same date

The user reported dead desktop controls. A real headless Chromium session against the current console reproduced `TypeError: Illegal invocation` in PreviewLoop.stop: a stored native clearTimeout function was invoked with the PreviewLoop receiver. The callback defaults now use wrappers; a late dialog close event is also prevented from hiding a newly opened editor. All existing checks were rerun, with31 additional rendered/API checks and16 native offscreen WebView2 checks. Content captures cover1280×860 and900×620; native frame/taskbar appearance was not visually inspected. The reviewer's one placeholder-contrast finding was fixed and scored resolved after recapture.

APK1.11.0/code14 was installed over the existing app and confirmed via Android package metadata. The running companion was stopped through its protected endpoint and replaced at its existing firewall-allowed path, then launched as a native Windows app. Process metadata confirmed a responsive window titled Tablet Deck Studio; no initialization error was logged. Desktop and Start menu shortcuts were updated with the new ICO. Board configuration, saved pairing, certificate and desktop preference hashes were unchanged. The app's window/close/reopen behavior was exercised separately in the isolated native fixture. Android launcher icon appearance, current1.11 app connection/controls, live OBS preview and provider rendering were not physically approved in this pass; the APK update stops the Android activity until the user reopens it.

Before publication, the user additionally requested in-app Windows updates and optional USB APK automation/reopening. These were implemented in Windows options, with verified release metadata/downloads, explicit Windows restart confirmation, activity/edit guards, a ready-before-exit helper handoff, program-file backup/rollback and status on reopening. APK automation/reopen remain separate off-by-default preferences. Mocked protocol/ADB, controller and API tests and a real isolated installer-worker fixture cover the mechanisms; no live upgrade to a newer public Windows release or automatic real-device APK installation is claimed. The earlier development deployment and user's navigation confirmation predate these updater additions. Final deployment/check results are recorded separately below.

Final development deployment: the updater-enabled native app was started at the same executable path and had a responsive Tablet Deck Studio window. Board, device-pairing and certificate hashes were unchanged. Startup update checks were enabled in response to the user's request; automatic USB APK updating/reopening remained off for the user to choose. The final APK was installed with its original signature, package metadata confirmed1.11.0/code14, and the authorized Tablet Deck activity was reopened. The companion then reported one authenticated fast link, with an established connection from the tablet's Wi-Fi address192.168.1.20 to PC port8744. This verifies reconnection after deployment, without claiming sound/scene/provider controls or an actual future-release self-upgrade were rehearsed.

### Earlier1.10 investigation

Dedicated Windows management UI, opt-in silent OBS preview and public Twitch/Kick viewers have isolated protocol/API/native-policy/controller checks. No provider page, live preview capture or rendered interface was opened during development. Actual provider behavior and camera/touch/focus/hardware rehearsal remain unverified. See MONITORING.md and VERIFICATION.txt.

The connected tablet was authorized and had version1.5.1. Both USB tunnels were absent; restoring them made raw tablet-to-PC TCP8743 and8744 probes succeed. PC used Ethernet and tablet Wi-Fi on the same IPv4 subnet, a supported topology. Wi-Fi probes to the companion timed out. Windows already had app allow rules; user then ran the prepared Administrator helper adding a program/port-specific LocalSubnet rule. Rule presence alone is not proof of Wi-Fi success.

Before the retest/update, Android disappeared from Windows PnP/ADB; it remained undetected after the reported cable reconnection, and no wireless-debug service appeared. APK installation was not performed and Wi-Fi could not be retested from the tablet. Physical data cable/port/USB-mode/driver access remains required; do not describe this as resolved Wi-Fi or a verified app-level pairing.

Automatic USB reconnect was enabled for the known tablet and missing ADB binaries supplied to the current headless companion. That action preserved board content and startup settings. Current firmware-independent device status distinguishes installed package/tunnels, saved pairings, authenticated activity and active fast links.

The tablet subsequently reappeared as authorized. An in-place APK update succeeded; Android package metadata confirmed version1.10.0/code13. The PC companion was stopped through its authenticated shutdown endpoint, allowed to exit, updated at its existing firewall-allowed executable path and restarted with `--no-browser --no-tray`. Its version and served desktop management document were checked. Hashes of config.json, devices.json, companion.pfx and desktop.json were unchanged; the existing pairing was retained.

After that update, fresh tablet-side TCP probes passed for both USB reverse endpoints (127.0.0.1:8743/8744) and both Ethernet LAN endpoints (192.168.1.21:8743/8744). This verifies current network reachability across Ethernet/Wi-Fi after the bounded repair. It does not independently verify TLS pinning, authenticated app controls, preview/chat rendering or audible output. The user was asked to open the updated tablet app and confirm its Wi-Fi status; no app window was opened by the agent.

Post-release follow-up on the same date: the user confirmed that **Switch to Wi-Fi** connected successfully. The companion subsequently reported one authenticated device and one active fast link. This adds app-level Wi-Fi connection evidence, without claiming that scene actions, audio, preview or provider rendering were retested.

The user could not find the desktop companion because the development deployment still used `--no-browser --no-tray`. The companion was stopped cleanly and restarted with `--tray --no-browser`, enabling the existing notification-area icon without opening a browser window. Desktop and Start menu shortcuts named **Tablet Deck Studio** were created for the current executable. The management document was served successfully; board, pairing, certificate and desktop preference hashes remained unchanged, and the tablet reconnected with one authenticated fast link. Windows may place the tray icon in its hidden-icons overflow; double-click it to reopen the console. Closing the console leaves the companion running; tray **Exit** stops it. Startup with Windows remains opt-in.

## Version 1.9 development boundary — 2026-11-01

All eight requested studio additions have isolated storage, mock-OBS/API, native QR and controller checks. Real camera scanning, foreground-app switching, rendered layout, touch, keyboard/screen-reader use, live OBS/Soundpad and hardware rehearsal remain unverified. Live video and Twitch/Kick chat are a following phase.

A failed build was followed by an older executable because the test command initially lacked an exit guard. It briefly started the companion/control panel against the default private data directory; this was disclosed and the process stopped. Startup performed the normal backed-up profile/config migration, preserving existing board actions and credentials. Later runners require successful builds, disable browser/tray explicitly and use isolated data. This incident was not a visual or hardware test. No claim of an entirely GUI-free development pass is made.

## Version 1.8 development boundary — 2026-11-01

Desktop setup has isolated API/controller checks, including OBS password modes, preservation of board/profile/pairing data, first-run behaviour, and stale navigation/save responses. No browser, app window, or computer-use surface was opened. The new desktop interface has no rendered-layout or physical keyboard/screen-reader approval. Tests did not change the running user installation, OBS, Discord, Soundpad, startup registry settings, or tablet app. See [VERIFICATION.txt](VERIFICATION.txt) for current automated coverage; the reports below describe earlier hardware sessions, not a new live rehearsal.

## Version 1.7.0 development — 2026-10-26

Profiles, automatic backups/recovery, and Soundpad favourites were implemented using isolated storage/API/UI-logic fixtures. Tests include legacy migration without changing existing action IDs, new IDs for duplicated boards, stale editor/action rejection, authenticated switch/admin boundaries, all-profile restore preserving current credentials, twenty-snapshot retention, damaged-file recovery/quarantine, favourite index/rename changes, missing/ambiguous identity rejection, async view ownership, and keyboard-focus retention across rerenders. Existing OBS and transport regressions use simulated OBS. These tests did not change the user's live installation or audio setup, and no browser, CUA, or app window was opened. Rendered UI, physical tablet touch, and actual favourite-add/play flows with live Soundpad still need rehearsal.

## Discord microphone follow-up — 2026-10-26

The user authorized live configuration of Soundpad's devices and Discord Voice & Video settings. Both selected the same NVIDIA Broadcast microphone; Soundpad's enabled recording endpoint reported Good. Discord retained Custom input, Noise Suppression None, and Echo Cancellation off; Automatic Gain Control and Bypass System Audio Input Processing were disabled following Soundpad's Discord guide. All 76 local collection buttons were set to both microphone and speaker/headphone output.

A microphone-only Bruh command succeeded, Discord's local microphone-test meter showed a signal, and the user confirmed hearing the clip through that test. Direct Soundpad speaker playback was disabled for this test; the local microphone test was stopped afterward. No call or broadcast was started. Hearing the clip at another Discord account remains unverified. No user audio files or private configuration are included in the public source or release.

## Version 1.6.0 development

Progress and cancellation tests use isolated data and simulated OBS. They do not alter the user's live board, audio routing, or running companion. The 1.6.0 ribbon and touch cancellation have not yet been checked on the physical tablet. No browser or app window was opened for this development pass.

## Version 1.5.1 follow-up

The Soundpad library handlers were being selected as HttpContext-only RequestDelegates, which discarded successful return values and produced an empty HTTP200 response. Shared CancellationToken-based route handlers now return explicit JSON results. GET and refresh POST each have a regression check against the actual HTTP route mapping. The manual update-check route used the same pattern and was corrected too.

With real Soundpad4.0.35 running, the corrected companion returned JSON library data. A private 76-clip user collection was imported and verified in six categories, then linked to a Memes tablet page with speakers-only routing. The physical tablet displayed 40 buttons and then36 on its second group. A tablet key press returned a success notice while Soundpad reported PLAYING. The user subsequently confirmed audible Bruh playback; Soundpad reported46% speaker volume and unmuted. The collection files, source list, and user settings are local; no third-party audio was published in GitHub or the app release. Discord listener audio and broadcast rights are unverified.

## Version 1.5 follow-up

- The user confirmed audible output after the tablet triggered the bundled applause sound over USB.
- Real OBS was launched minimized and authenticated with its existing local WebSocket configuration. The PC was offline for broadcasting: neither streaming nor recording. The existing three scenes were imported into the board, and the desktop input was matched to its actual OBS name.
- Tapping scene keys on the physical tablet switched to another real OBS scene and restored the original. Mic mute and unmute both changed the real OBS input, restoring its original unmuted state.
- The configured Discord app-launch action succeeded. The Windows audio API changed a real Discord playback session's mute state and restored its original value. This does not verify Discord microphone mute/deafen shortcuts, voice transmission, or screen sharing.
- APK 1.5.0/versionCode7 installed as an update and preserved pairing. Full pad entered on the physical tablet, selected the 40-button layout, displayed the existing twelve actions across eight columns, and hid the mixer. An isolated browser fixture supplies actual 24/40-action coverage.
- After Soundpad's first-run setup, the real 4.0.35 named-pipe library query succeeded with an empty list, and the Stop Soundpad command succeeded. A Stop Soundpad key was added to the user's Sounds page. Playback awaits a sound in the empty library; automatic demo import was rejected by Soundpad as “File does not exist.” No microphone routing was enabled for this test.

The historical 1.4.1 report below records the state before this follow-up; its earlier OBS launch restriction was resolved by the user's explicit request to continue those tests.

Version 1.4.1 was installed on a physical Android 16 tablet (model 25079RPDCG). The Windows PC used Ethernet to the same local router for the Wi-Fi test. Testing used ADB and Android's accessibility hierarchy, without remote WebView debugging or disabling the secure-window flag. Device serials, local addresses, pairing credentials, and runtime files are excluded from this report.

## Results

- Installing 1.4.0 succeeded, but the screen showed `net::ERR_HTTP_RESPONSE_CODE_FAILURE` for the local bundled page. Inspection found ZIP entries such as `assets/www\index.html`. Android expects `assets/www/index.html`.
- The corrected build uses the JDK ZIP packer for assets and rejects backslash entries or missing required UI files before signing. The 1.4.1 APK retained its signing certificate, installed as an update, and loaded the pairing form and twelve-button board.
- USB pairing completed through the real tablet interface. Both reverse tunnels, TCP 8743 and 8744, were present. The native connection panel reported a persistent encrypted link.
- Switching to Wi-Fi succeeded without re-pairing. The PC confirmed an established fast-control TCP connection directly from the tablet's LAN address. Android reported the app's foreground low-latency Wi-Fi lock active.
- The Windows app-volume panel loaded twelve real playback sessions, including Discord. This checked enumeration and rendering; no existing app's volume or mute state was changed.
- Tapping Applause over Wi-Fi returned `Applause — done`. This confirms the command reached the Windows sound player successfully; audible output and capture routing require a human listening check.
- Returning to USB, backgrounding/resuming the app, and restoring the fast tunnel re-established the persistent connection without re-pairing. With the fast reverse tunnel removed, tapping Stop sounds still returned `Stop sounds — done`; the tunnel was restored afterward.

## Latency observations

The connection panel showed individual recent heartbeat round-trips of 3.9 ms and 10.5 ms over USB, and 6.5 ms over Wi-Fi. These are sparse observations, **not medians, percentiles, bounds, or a comparison benchmark**. They include the native encrypted transport and reply, but exclude touch sampling, UI rendering, sound buffers, OBS scene completion, and broadcast delay. Radio conditions and machine load were uncontrolled.

## Remaining checks

Real OBS scene switching, recording/streaming, Soundpad playback/routing, Voicemeeter, Discord listener audio, and hold-key injection were not exercised. OBS was installed but closed; automatic approval review rejected starting/configuring it and gave no specific reason. No broadcast or recording was started. Windows tray/startup UI, a physical cable unplug/replug, long-duration stability, packet-loss behavior, and end-to-end audiovisual latency remain unverified. Accessibility hierarchy checks confirm UI structure and controls, not a pixel-level visual review.
