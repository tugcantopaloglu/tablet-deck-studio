# Set up the Windows companion

Install the Windows companion, then open its control panel from the Start menu shortcut or the Windows tray menu. Select **PC setup** in the toolbar. Closing the browser or this panel leaves tablet control running; use the tray menu to exit the companion.

A PC with no paired tablets opens setup automatically once per browser tab session. Existing paired setups go straight to the board. The PC setup entry stays available. It is hidden in the Android app and design preview.

## Pair a tablet

Choose **Pair tablet** or **Pair / manage tablets**. Install the APK on Android and enter the address, certificate code, and single-use PIN shown on the PC. PINs expire after five minutes; generate another if needed. Pairing is shared across profiles.

For Wi-Fi, keep both devices on the same local network and allow the companion through Windows Firewall on that private network. For USB, connect a data cable, enable USB debugging, and approve this PC on the tablet. Use `USB-Connect.cmd` from the Windows package, or enable **Reconnect USB automatically** in **Windows options**. Select USB on the tablet. With multiple tablets, set the tablet serial in Windows options. Pairing still requires the PIN/certificate check.

The setup hub counts saved pairings. It does not claim a paired tablet is online or measure its latency.

## Connect OBS

1. Open OBS Studio on the same PC. In **Tools → WebSocket Server Settings**, enable the server and keep authentication enabled.
2. In **PC setup → Set up OBS**, copy the port and password from OBS. The usual port is 4455.
3. Choose **Save OBS settings**. The companion saves and checks the connection. Connection checks do not start streaming, recording, playback, or other board actions.
4. Use **Find inputs** to load audio input names from the saved OBS connection. Choose exact names for the microphone and desktop-audio fields, then save again. A connected OBS server can still have a missing audio input; the check reports both separately.

A blank password field keeps the current saved password. Entering a new password replaces it. **Clear saved password** asks for confirmation and removes it for all profiles. Do not enter a replacement and choose Clear simultaneously. Saved passwords are never displayed back in this form. Choose **Check saved connection** to test persisted values; unsaved field changes are not used by that check or Find inputs.

OBS settings apply to every board profile. Saving here changes only the port, password choice, and audio input names; it preserves pages, actions, inactive profiles, favourites, and paired devices. Settings cannot change during a running macro. The normal automatic backup rules apply; snapshots exclude the password.

## Build your board

**Edit buttons** opens the existing board editor for apps, shortcuts, sounds, scenes, macros, and page arrangement. Changes sync after saving. **Manage profiles** creates or switches boards. **Backups and restore** previews a snapshot before confirmed restore. See [profiles and backups](PROFILES-AND-BACKUPS.md).

**Browse sounds** opens the Soundpad library. Soundpad must be running on the PC. Star favourites and choose playback output before adding buttons. For Discord, choose **Speakers + microphone** and select the same microphone in Soundpad and Discord. Discord voice processing can filter clips; rehearse with its local microphone test. The setup hub does not alter audio devices or Discord settings automatically.

**Windows options** contains Start with Windows, automatic USB reconnect, optional tablet serial, and update notifications. All are optional. Startup changes affect only the current Windows user. Update checks contact the project's public GitHub release API; downloads and installation remain manual. Save changes before leaving. If USB reconnect is enabled, its status updates within five seconds; choose **Refresh checks** to see the latest snapshot in the hub.

## If something fails

- **Old or locked browser tab:** reopen the control panel through the tray menu to get the current local admin session. Then choose PC setup or Retry checks.
- **OBS not connected:** check that OBS is open, its WebSocket server is enabled, and the saved port/password match. Save before finding inputs. OBS reconnects automatically.
- **Input not found:** use Find inputs and select the exact name. These faders control OBS inputs, not Windows master volume.
- **Cannot leave a form:** save changes, or Close and explicitly discard them. Setup navigation protects unsaved button, OBS, and Windows settings.
- **Status looks old:** use Refresh checks. Saved pairing count, USB tunnel status, and OBS state are separate facts; none proves touch-to-sound latency.

The new desktop flow has isolated API/controller regression coverage. Browser-rendered layout, real keyboard/screen-reader use, and physical tablet interaction were not checked during this development pass because app/browser automation was disabled by request. Use a rehearsal before relying on any setup change during a broadcast.
