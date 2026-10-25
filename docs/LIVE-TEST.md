# Physical Android test — 2026-10-25

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
