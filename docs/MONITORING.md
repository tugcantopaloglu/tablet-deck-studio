# Windows console, OBS preview and Twitch/Kick chat

Version 1.10 keeps the two device roles separate. The **Windows console** configures connections, boards, sounds, profiles and settings. Its sidebar opens full-area management views; it has no action-pad grid. The **Android tablet** keeps the touch board, mixer and Full pad. Both can open monitoring.

Update both the Windows companion and APK. Android 1.10 is version code 13 and retains the signing certificate, so update it without uninstalling to keep pairing. This release corrects a native allowlist omission that blocked newer bundled scripts; the build now checks every referenced script and stylesheet against that allowlist.

## Desktop management

Start the companion to open the Windows console. If it is already running, starting it normally opens the existing console; headless --no-browser runs open no window. Overview shows the active profile, OBS state, preferred physical LAN address, saved pairings, active fast links and USB status. Connection summaries refresh every five seconds while the overview is visible. Counts are not latency measurements.

Use Board editor for buttons and ordering; OBS setup for credentials/audio names; Tablet connections for PIN/QR pairing; Soundpad library for clips; Profiles, Backups, OBS mixer setup, Automatic profiles and Portable packs for those tools. Windows options contains startup, USB reconnect and update preferences. Stop companion asks for confirmation and refuses unsaved edits or an active/held action; stopping disconnects controllers and stops deck sounds, while OBS outputs remain running.

## Local OBS live preview

On the tablet, choose **Monitor** in the toolbar; on Windows, choose **Monitoring** in the sidebar. Opening the monitor alone does not capture a frame or contact a chat provider. Choose **Start preview** when needed.

Settings offers Program output or OBS Studio preview, widths 320/640/960, and 1/2/4 frames per second. Default is program, 640 pixels, two frames per second. Studio preview requires Studio Mode and a selected preview scene. Choose Video only, Chat only, or Video + chat for the available screen area. On compact tablet displays, a single-view layout gives chat or video more space.

This is a **silent, low-frame-rate JPEG monitor**, not a full-motion 30/60 fps player or a stream-delay measurement. Images remain between OBS, the PC and the paired tablet. No additional OBS video plugin, public streaming service or audio capture is required. Preview does not start a broadcast, recording or camera.

The companion uses a separate OBS WebSocket client, bounded image sizes, one capture at a time, a global ceiling of four capture attempts per second, short-lived shared frames and no queued image backlog. Android image requests use their own worker. Multiple controllers share the capture budget; a busy capture retries rather than building a stale queue. Button commands retain the existing control connection. This protects responsiveness by design; hardware performance still needs a rehearsal.

Pause preview to retain its last image with paused/age text. Errors are shown as unavailable and old images become stale. Closing Monitor clears the image and stops requests. Opening another tool, backgrounding the app or hiding the browser pauses requests; an explicitly requested preview can resume when visible. A late response cannot reopen a closed monitor or replace another generation. No frames are written to disk by this feature.

## Twitch and Kick chat

Open **Monitor → Settings**, enter editable Twitch and/or Kick channel names, and save. Names start blank. Enter channel names, not URLs; an initial @ is accepted. These preferences are stored on that browser/tablet, independently of board profiles, backups and portable packs. Saving channel names does not load a provider or connect an account.

Choose the Twitch or Kick tab, then **Load chat**. This contacts the selected provider over the device's Internet connection, including when the tablet controls the PC through USB. The app does not require developer credentials, store provider passwords, scrape messages, or automatically post chat. Preview images are not sent to either provider.

Windows attempts a sandboxed provider frame; Twitch's embed receives the actual console hostname as its parent. If the provider refuses embedding, displays a cookie/sign-in prompt or remains blank, use **Open chat window** to open that channel's public popout. A frame-load event means only that its page opened; it does not prove chat joined or messages are arriving.

Android uses a **separate, unprivileged WebView** for the public provider page. It has no DeckNative/PC-control interface and cannot load local files, private companion pages, native intents or downloads. It uses normal certificate validation and a provider/CDN HTTPS allowlist. The board's privileged WebView remains bundled/local-only. Native chat is clipped to its visible monitor area and destroyed when hidden, another tool opens, or the app backgrounds. It reloads when an explicitly requested viewer becomes visible again.

Choose **Unload chat**, then **Load chat** to retry. Closing the monitor unloads it. Provider cookies, connectivity, regional restrictions, mobile behavior and page changes may affect viewing; this implementation does not bypass challenges or guarantee that every embed works. Kick's own guidance notes that its logged-out dock may say Disconnected while still receiving messages. Real provider behavior was not verified by opening pages during development.

Official integration references: [Twitch chat embed](https://dev.twitch.tv/docs/embed/chat/), [Kick public chat-dock URL and limitations](https://help.kick.com/en/articles/7978574-how-to-create-a-kick-chat-dock-in-obs), [OBS WebSocket protocol](https://github.com/obsproject/obs-websocket/blob/master/docs/generated/protocol.md#getsourcescreenshot).

## Ethernet PC and Wi-Fi tablet

That arrangement is supported. Both devices need a reachable local network, not the same connection type. Use the physical LAN IPv4 address shown in Overview/Tablet connections; virtual-machine and VPN addresses are listed after gateway-backed LAN interfaces. The tablet must run the app and select Wi-Fi with that PC address. There is no promise of automatic Wi-Fi discovery or automatic app launch after plugging in USB.

The shipping ports are encrypted HTTPS 8743 and persistent TLS control 8744. The admin console on 8740 remains PC-loopback only. Do not forward these ports on the router. Guest Wi-Fi/client isolation, firewall policy, a wrong PC address and a stopped companion can prevent access even when both devices use the same router.

The complete Windows package includes **Repair-WiFi.ps1**. It requires an Administrator PowerShell because Windows firewall changes are privileged. Run it with the companion executable in that folder; use -DryRun to inspect it. It creates a program-specific TCP 8743/8744 allow rule for LocalSubnet on Private/Public profiles and disables matching inbound block rules for that exact executable. It does not disable the firewall, forward router ports, or change unrelated program rules. A rule is not proof of end-to-end connectivity; retest from the tablet.

For USB, enable debugging, use a data cable, authorize this PC and run USB-Connect.cmd or enable automatic reconnect. The helper needs the bundled platform-tools folder. It checks tunnels every five seconds and app presence every thirty. Windows must enumerate an Android/ADB device; if PnP/ADB see no device at all, check cable, port, USB mode and manufacturer driver before blaming app pairing. Install-APK.cmd updates the app without opening it; Android may require installation approval.

## Verification boundary

Generated/mock images, independent OBS connections, nonblocking controls, caching/cancellation, native chat URL policy, asset coherence and controller lifecycle have automated checks. They do not verify rendered fit, provider content, camera/touch/focus behavior or network hardware. During the live investigation, Wi-Fi initially timed out; the user ran the bounded firewall helper. After a temporary device disconnection, the authorized tablet returned, its APK was updated to1.10.0/code13 and fresh tablet-side TCP probes passed for both USB and both LAN ports. The companion was updated headlessly with board, pairing and certificate files unchanged. Authenticated app controls and real provider/preview rendering remain separate verification steps. See [LIVE-TEST.md](LIVE-TEST.md) for dated updates and [VERIFICATION.txt](VERIFICATION.txt) for current counts.
