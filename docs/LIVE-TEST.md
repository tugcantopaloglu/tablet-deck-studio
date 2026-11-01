# Physical Android test — 2026-10-25

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
