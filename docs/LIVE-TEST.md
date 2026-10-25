# Physical Android test — 2026-10-25

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
