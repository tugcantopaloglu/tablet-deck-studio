# Tablet Deck Studio

Turn an Android tablet into a customizable control board for a Windows streaming PC. Change OBS scenes, play sound effects, control audio, open apps, send hotkeys, and run macros over **Wi-Fi or USB**.

**[Download the latest release](https://github.com/tugcantopaloglu/tablet-deck-studio/releases/latest)** · [Build from source](docs/BUILDING.md) · [Verification results](docs/VERIFICATION.txt)

![Studio layout with twelve action keys and two audio faders — illustrative design preview](docs/images/studio-preview.png)

The Studio layout has twelve large buttons beside two audio faders and five page tabs. Version **1.1.0** adds direct Soundpad control and a Discord setup guide. The screenshot shows illustrative state, not a live broadcast. The release passes **64 automated checks**; physical tablet, Soundpad audio, Discord sharing, actual Wi-Fi, and USB still need a rehearsal on your setup.

## Features

| Area | Controls |
| --- | --- |
| Scenes | Switch program scene, set Studio Mode preview, take a transition |
| Audio | Two configurable OBS faders, input mute, PC soundboard with two synthesized demo effects |
| Broadcast | Streaming, recording, replay-buffer save, virtual camera, Studio Mode |
| Sources | Scene-source visibility, source-filter toggle, media restart/stop |
| Windows | App/shortcut launching, websites, keyboard shortcuts, volume/media keys |
| Soundpad | Play by index to mic/speakers/both, stop, pause/resume, next, previous |
| Discord and other tools | Matching app keybinds; Discord mute/deafen/screen-share setup documented |
| Macros | Multiple steps, delays, confirmation, stop on first error |
| Customization | Labels, icons, colors, button order, multiple pages, JSON import/export |
| Connection | Encrypted local Wi-Fi or an ADB reverse tunnel over a USB data cable |

## Contents

- [Requirements](#requirements)
- [First run](#first-run)
- [Wi-Fi](#wi-fi)
- [USB](#usb)
- [Customize the board](#customize-the-board)
- [Action reference](#action-reference)
- [Soundpad and Discord](#soundpad-and-discord)
- [Examples](#examples)
- [Backups and updates](#backups-and-updates)
- [Troubleshooting](#troubleshooting)
- [Data and security](#data-and-security)
- [Build and contribute](#build-and-contribute)

## Requirements

- Android 8.0 or newer, with an up-to-date Android System WebView / Chrome. Landscape tablet is the primary layout.
- Windows 10 or 11, x64. The companion includes its .NET runtime; Node, Python, Android Studio, and .NET installation are not required to use it.
- For OBS controls: OBS Studio 28 or newer with WebSocket server enabled. Soundpad and other non-OBS actions also work with OBS closed.
- For Soundpad actions: Soundpad installed and running on the Windows PC under the same account as the companion.

## First run

Download `TabletDeck-Studio-1.1.zip` from [Releases](https://github.com/tugcantopaloglu/tablet-deck-studio/releases/latest) for the complete package, or download `TabletDeck-Studio.apk` separately if you only need the Android installer.

```text
TabletDeck-Studio/
├── TabletDeck-Studio.apk
├── SHA256SUMS.txt
├── Verification.txt
└── Windows/
    ├── TabletDeck-Companion.exe
    ├── Install-APK.cmd
    ├── USB-Connect.cmd
    ├── wwwroot/
    ├── Sounds/
    └── platform-tools/
```

1. Extract the whole ZIP. Keep the Windows folder together: the executable needs its wwwroot, Sounds, and platform-tools folders.
2. Run Windows/TabletDeck-Companion.exe on your streaming PC. It opens the control panel in your default browser. Keep the companion window running; Ctrl+C or closing it stops tablet control.
3. In OBS, open Tools → WebSocket Server Settings. Enable the server and keep authentication enabled. The usual port is 4455. Copy the server password.
4. In the companion’s browser panel, choose Edit. Enter the OBS password and port. Choose Save changes. Reopen Edit → Find OBS inputs to choose your exact microphone and desktop audio input names. Import OBS scenes to add real scene names to the Scenes page. The Main page starts with example scene names; change them to match your OBS scenes.
5. Install TabletDeck-Studio.apk on the tablet. You can copy the APK to the tablet and allow installation from that file source, or enable USB debugging and run Windows/Install-APK.cmd.
6. In the Windows panel choose Pair tablet, then follow either Wi-Fi or USB below.

For Soundpad or Discord-only use, skip the OBS setup steps. An OBS disconnected indicator is expected while OBS is closed; Soundpad, app launches, and keyboard shortcuts remain available.

The Android package has a personal signing certificate, not a Play Store identity. Windows binaries are unsigned and may trigger SmartScreen. The APK is signed with APK Signature Schemes v2 and v3. Verify the provided SHA256SUMS.txt if you transfer the files.

Installing by file manager: copy the APK to the tablet, open it, and allow that file source to install apps if Android prompts. Installing from Windows: authorize USB debugging, then run `Install-APK.cmd` with exactly one Android device connected.

OBS includes WebSocket support in version 28 and later. See the [official OBS remote-control guide](https://obsproject.com/kb/remote-control-guide). The tablet reaches the companion on port 8743; the companion reaches OBS locally, normally on port 4455. You do not normally need to expose OBS's port to the tablet.

## Wi-Fi

Connect the PC and tablet to the same private network. Avoid guest Wi-Fi, which often isolates devices. Allow the companion through Windows Firewall on the private network when Windows asks.

On the tablet select Wi-Fi, enter the PC address listed in Pair tablet, then enter the 24-character certificate code (hyphens and spaces are optional) and the six-digit PIN. The PIN expires in five minutes, is single-use, and locks after five wrong attempts. Generate a new PIN on the PC if it expires. The certificate code must match the intended PC before the tablet sends the PIN.

If Windows does not show a firewall prompt, allow TabletDeck-Companion.exe inbound on TCP port 8743 for the private network only. Do not forward this port on your router. No cloud account or subscription is needed.

## USB

USB uses an ADB reverse tunnel and works with Wi-Fi off. It is not USB tethering.

1. On Android, enable Developer options (usually tap Build number seven times), then enable USB debugging. Menu locations vary by manufacturer.
2. Connect a USB data cable, unlock the tablet, and approve the PC’s USB debugging authorization prompt. Some tablets need their manufacturer’s Windows USB driver.
3. Run Windows/USB-Connect.cmd. It creates the tablet-to-PC port tunnel. If no device is found, check authorization, cable, and driver. The included helper refuses to guess if multiple devices are attached; use USB-Connect.ps1 -Serial SERIAL in that case.
4. In the tablet’s connection screen select USB. Enter the same certificate code and a fresh pairing PIN from the Windows panel. A PC Wi-Fi address is optional for initial USB-only pairing.
5. Run USB-Connect.cmd again after unplugging the tablet or rebooting it.

Once paired, tap the tablet’s connection status to switch between Wi-Fi and USB. If you initially paired only over USB, enter the PC’s Wi-Fi address before selecting Switch to Wi-Fi. The certificate and device credential are reused. Revoke a tablet through Pair tablet on the PC if it should lose access.

The helper creates the equivalent of this tunnel:

```powershell
# Run from the extracted Windows folder:
.\platform-tools\adb.exe -s YOUR_DEVICE_SERIAL reverse tcp:8743 tcp:8743
```

With multiple devices connected, select a serial explicitly:

```powershell
.\platform-tools\adb.exe devices
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\USB-Connect.ps1 -Serial YOUR_DEVICE_SERIAL
```

See Android's [ADB documentation](https://developer.android.com/tools/adb) and [Developer options guide](https://developer.android.com/studio/debug/dev-options) for device-specific debugging setup.

## Customize the board

Use Edit in the Windows panel. Select a page and button, then change its name, action, icon, and color. Add pages or buttons and move buttons earlier or later. Each screen shows 12 buttons; larger pages have Previous and Next controls. Save changes to sync to the tablet. Board editing is intentionally restricted to the local PC.

Actions:

- Switch OBS scene or set a Studio Mode preview scene; take the preview transition.
- Play WAV, MP3, or WMA on the PC; stop all sounds started by the deck. Two original synthesized demo sounds are included.
- Launch an existing Windows .exe or .lnk using an absolute path, or open an HTTP/HTTPS website. There is no arbitrary remote shell command facility.
- Send hotkeys such as CTRL+SHIFT+M, ALT+TAB, F13, PLAYPAUSE, NEXTTRACK, PREVTRACK, VOLUMEUP, VOLUMEDOWN, or VOLUMEMUTE. Keyboard shortcuts reach the foreground application or a configured global keybind in a running app; Windows can block injection into elevated apps.
- Mute an OBS input; toggle a scene source or source filter; restart or stop an OBS media input.
- Toggle recording, streaming, Studio Mode, or the virtual camera; save an enabled OBS replay buffer. Streaming and recording always ask for confirmation, including inside macros.
- Run multiple steps in sequence. Each macro step has an action, target, and optional source/filter/media value. Delay steps use milliseconds (0–10000). Steps stop at the first error. Execution has a 45-second budget; changes already made are not rolled back.

The audio faders adjust the two configured OBS inputs from -60 to 0 dB. They are OBS input controls, not Windows master-volume controls. Mute is neutral; muted channels show red Unmute. PC/OBS loss clears ACTIVE scene state and shows Stream unknown.

For sound effects to reach viewers, capture the PC’s playback output in OBS Desktop Audio, application audio capture, or your virtual mixer. Sound playback alone does not inject audio into the microphone. The deck’s Stop sounds button only stops deck playback. Restart/stop OBS media actions target media sources separately.

Record and replay controls require those features to be configured in OBS. An unconfigured replay buffer or invalid scene/source/input returns the OBS error on the tablet. Defaults are examples, not a claim about your PC’s current setup. OBS and Discord launch paths can be adjusted in Edit; Discord is detected on first run when installed in its usual user folder.

Export/Import saves board JSON without the OBS password. Sound files and applications are referenced by absolute PC paths; they are not copied into an export. The app does not include Elgato’s proprietary plugin marketplace or automatic support for every third-party plugin.

## Action reference

Scene, input, source, and filter names must match OBS exactly.

| Action | Target | Additional field or prerequisite |
| --- | --- | --- |
| Switch OBS scene | Scene name | None |
| Set preview scene | Scene name | Studio Mode enabled |
| Take studio transition | None | Studio Mode and a preview scene |
| Play sound on PC | Absolute WAV/MP3/WMA path | Capture playback output in OBS |
| Stop all deck sounds | None | Stops deck playback only |
| Open Windows app | Absolute .exe or .lnk path | File must exist |
| Open website | HTTP/HTTPS URL | Opens the PC browser |
| Send keyboard shortcut | For example CTRL+SHIFT+M | Foreground app receives the keys |
| Toggle OBS input mute | Input name | Input must exist |
| Toggle source visibility | Scene name | Source name in that scene |
| Toggle source filter | Source name | Filter name |
| Restart / stop OBS media | Media input name | restart or stop |
| Toggle recording / streaming | None | Configure OBS first; confirmation required |
| Save replay buffer | None | Configure and start the replay buffer in OBS |
| Toggle virtual camera / Studio Mode | None | Feature must be available in OBS |
| Run multiple actions | Steps in the editor | Executes in order |
| Delay | Milliseconds, 0–10000 | Usually a macro step |
| Soundpad · play sound | Positive index from All sounds | Output: mic, speakers, both, or default |
| Soundpad · stop / pause / next / previous | None | Keep Soundpad running |

Supported shortcuts include letters, digits, F1–F24, CTRL, SHIFT, ALT, WIN, ENTER, TAB, ESC, SPACE, BACKSPACE, DELETE, arrow keys, and the volume/media keys listed above. Join simultaneous keys with +.

## Soundpad and Discord

Yes, the board can be used with **Discord desktop streams and calls**, as well as OBS. See the detailed [integration guide](docs/INTEGRATIONS.md).

**Soundpad:** keep it running on the PC, add a **Soundpad · play sound** button, and enter the index from its All sounds category. Use output **mic** to send an effect through Soundpad's configured microphone, **both** to hear it locally as well, or **speakers** for local-only playback. Dedicated stop, pause/resume, next, and previous controls do not need keyboard focus or an executable path.

**Discord controls:** configure Toggle Mute, Toggle Deafen, or Toggle Screen Share in Discord's desktop **User Settings → Keybinds**, then create matching **Send keyboard shortcut** buttons here. Leave Discord's keybind editor before testing. The share picker or stream settings may still need confirmation in Discord. These are shortcut controls, not live Discord-state indicators. The board's Start stream button controls OBS, not Discord.

**Discord audio:** select the same microphone in Soundpad and Discord. If effects are filtered, follow [Soundpad's Discord FAQ](https://www.leppsoft.com/soundpad/help/faq). Normal PC playback alone does not send audio into Discord's microphone. Application-share audio and voice audio are separate paths; avoid capturing the same effect twice.

**Other stream tools:** use a tool's supported global keybinds, foreground shortcuts, app launches, or macros. There is no automatic integration with every tool or plugin. The current shortcut action sends a tap, so use toggle actions rather than a hold-to-talk workflow.

## Examples

### Open an application

In the PC editor, add a button, choose **Open Windows app**, and enter the full path to an executable, such as `C:\Program Files\obs-studio\bin\64bit\obs64.exe`.

Choose a label, icon, and color, then **Save changes**. Use a Windows shortcut for a specific launch configuration; command-line arguments are not a separate button field.

### Add your own sound

Choose **Play sound on PC** and set Target to an existing file, for example `C:\StreamSounds\applause.wav`.

The companion plays the file through the PC's default output. Check that output's meter in OBS and listen to a short test recording. If you use a virtual mixer, route that playback output into the stream mix. The sound is not played by the tablet or injected into the microphone automatically.

### Make a scene-and-sound macro

1. Choose the Macros page in Edit and select **Add button**.
2. Name it **Scene + sting**, then choose **Run multiple actions**.
3. Add a **Switch OBS scene** step, targeting an actual scene such as Gameplay.
4. Add a **Delay** step with Target 300.
5. Add a **Play sound on PC** step with the absolute path to your transition sound.
6. Save and test it offline.

Macros support up to 30 steps. They stop at the first error and have a 45-second execution budget. Earlier steps are not rolled back. A stream or recording step causes the macro to require confirmation. A toggle reverses the current state, so avoid adding toggles unless that reversal is intended.

### Read the board's state

A cyan outline and ACTIVE label mark the current OBS scene. Muted channels show red Unmute buttons. Buttons can show Set up on PC when their file/app/shortcut target is missing. After a connection failure, Stream unknown means the companion cannot confirm OBS's state; it does not mean OBS stopped broadcasting. Use Mute for a muted input rather than relying on the fader's −60 dB minimum.

## Backups and updates

Use **Edit → Export** to download board JSON without the OBS password. Use **Import**, review the result, then **Save changes** to apply it. Exported boards reference absolute sound and app paths; transfer those files separately when moving PCs.

For a Windows update, close the companion console, extract the full new package, and keep its executable, wwwroot, Sounds, and helpers together. Settings remain in `%LOCALAPPDATA%\TabletDeck`. If saved sound buttons point inside an old release folder, update those paths after moving the package.

An APK signed with the same key can update the existing installation. Install-APK.cmd uses adb install -r. A differently signed APK requires uninstalling the old app, which clears its saved pairing.

To remove access, use **Revoke** in the PC's Pair tablet panel. To clear the tablet's connection, use **Forget this PC**. The companion installs no service or startup task: close it and remove its extracted folder to remove the program. Delete its local-data directory only when you also intend to erase settings, paired-device records, and the certificate.

## Troubleshooting

| Problem | What to check |
| --- | --- |
| PC disconnected over Wi-Fi | Companion running, correct local PC address, private-network TCP 8743 allowed, no guest/client isolation |
| OBS disconnected | OBS running, WebSocket enabled, correct companion port/password; normally port 4455 |
| Pairing PIN rejected | Generate a fresh PIN; it is single-use, expires after five minutes, and locks after five wrong attempts |
| Certificate mismatch | Use the certificate code from the intended PC's current companion; deleting its data changes its identity |
| Invalid scene or source | Match OBS names exactly; importing scenes adds real names to the Scenes page, not the Main page |
| Fader needs setup | Use Edit → Find OBS inputs and configure exact Mic/Desktop input names |
| Sound heard locally but not on stream | Capture playback output in OBS; check virtual-mixer routing and make a test recording |
| App/sound says Set up on PC | Choose an existing absolute file path in Edit |
| Hotkey does nothing | Focus the intended app and check its shortcut; Windows may block injection into elevated apps |
| Save replay fails | Configure and start OBS's replay buffer first |
| No authorized Android device | Data cable, USB debugging, unlocked tablet, PC authorization, manufacturer USB driver if required |
| ADB shows unauthorized | Approve the USB-debugging prompt on the tablet |
| Multiple USB devices | Run USB-Connect.ps1 with the intended device serial |
| USB stopped after reconnect | Run USB-Connect.cmd again to recreate the reverse tunnel |
| Editor tab was closed | Close and restart the companion to reopen an authenticated tab |
| Already running / port in use | Close the existing companion or the process using port 8740/8743 |
| APK will not install | Android 8.0+, permission to install from that file source, matching signing key for an update |
| Layout misbehaves on an older tablet | Update Android System WebView / Chrome, then repeat the rehearsal |

When reporting an issue, include Windows/Android/OBS versions, connection mode, exact error text, and reproduction steps. Do not post OBS passwords, pairing codes, credentials, private keys, or the runtime data directory.

## Data and security

The PC editor is bound to loopback port 8740 and requires a random local session credential. Remote tablet control uses TLS on port 8743, the PC certificate is pinned by the Android app, and each paired tablet gets a separately revocable random credential. Server-side device credentials are stored as SHA-256 hashes. The initial certificate code is a 96-bit SHA-256 prefix; after pairing the full certificate fingerprint is pinned. No certificates are installed into the system trust store.

Settings, the OBS password, certificate private key, and device records live under %LOCALAPPDATA%/TabletDeck, protected by a directory ACL for the current Windows user and SYSTEM. The OBS password and PFX are not application-encrypted at rest; users with access to the account can read them. Android credentials are in app-private storage and Android backups are disabled. The app’s WebView loads only bundled assets, blocks external navigation and frames, sanitizes dynamic markup, and permits only the required control endpoints through its native bridge.

If you close the PC browser tab, the companion keeps running. Restart the companion executable to reopen a newly authenticated control-panel tab. It does not install a service or add startup tasks. If already running, close the existing companion console first.

## Verification and limits

See [Verification results](docs/VERIFICATION.txt) for build, test, and security scan results. No physical Android tablet was connected during creation: installation, on-device WebView rendering, real Wi-Fi routing, and real USB transport still need verification on your tablet. Browser captures prove the shared UI layout; mocked OBS integration tests prove the protocol and action wiring, not a live stream. Run a rehearsal before relying on it in a broadcast.

## Build and contribute

See [BUILDING.md](docs/BUILDING.md) for building the Windows companion and Android APK, signing-key handling, assembling a distribution, and running the isolated integration tests. Private release signing keys are deliberately excluded from this repository.

```text
android/                 Manifest, resources, and Java HTTPS bridge
companion/               C# ASP.NET Core companion
companion/wwwroot/       Shared HTML, CSS, JavaScript, fonts, DOMPurify
companion/Sounds/        Synthesized demo WAV files
scripts/                 APK build and Windows USB/install helpers
tests/                   Mock OBS service and browser/API checks
docs/                    Build guide, screenshot, and verification
```

For changes, fork the repository, create a branch, run the relevant checks, and open a pull request describing the behavior and how it was verified. Keep hardware-test claims separate from browser and mock-OBS checks.

## Third-party notices

Manrope is distributed under the [SIL Open Font License](companion/wwwroot/fonts/OFL.txt). DOMPurify is vendored with its [license](companion/wwwroot/vendor/DOMPurify-LICENSE.txt). OBS and Discord vector marks are from [Simple Icons under CC0](companion/wwwroot/vendor/Simple-Icons-CC0.txt); trademarks belong to their owners. Release packages retain Android platform-tools and Microsoft .NET notices.

No project-wide open-source license has been assigned. Public visibility does not replace the licenses of the third-party components above. This project is not affiliated with or endorsed by Elgato, OBS, Discord, Google, or Microsoft.
