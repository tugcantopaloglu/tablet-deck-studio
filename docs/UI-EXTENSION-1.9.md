# UI extension record: Studio tools, 1.9

This scoped extension adds eight studio capabilities to the incumbent dark Studio / Operate world described in [the 1.8 extension record](UI-EXTENSION-1.8.md). `companion/wwwroot/style.css`, bundled Manrope, existing CSS tokens, board layouts, dialogs, fields, and buttons remain authoritative. This record introduces no new global identity or token values and does not replace `PRODUCT.md`, `DESIGN.md`, or the design sidecar. Live video and Twitch/Kick chat remain later roadmap work; neither is an implemented capability of this extension.

## Existing system used

Tools use the existing modal panel with a fixed heading/close area and scrollable body, primary and quiet buttons, labelled form grids, wrapping action rows, help copy, profile rows, confirmation dialog, and unsaved-change protection. Dark canvas and surface layers, outline dividers, ink/secondary text hierarchy, and cyan focus/action treatment come from the incumbent stylesheet. No decorative card system, new font, or palette is introduced. Existing violet scene, amber sound, blue app, and red broadcast button choices remain available.

The added toolbar entry uses a wrapping toolbar and automatic-height header row so controls can occupy a second line. Studio and Full pad retain their board arrangements. The tools menu is a vertical list with a maximum width of 560px. Expanded mixer channels use an automatic grid with a 210px minimum column and 24px gap; channel labels and input names wrap. Editor additions use the existing form and wrapping action row, separated by outline rules. Preview content wraps, contains its image, and permits long button names to break. QR artwork is constrained to its panel width, with a 400px maximum. Existing 760px rules collapse forms/editor columns and reduce panel padding. These are source-level layout provisions, not rendered-fit approval.

## Discoverability and destinations

**Tools** in the main toolbar opens **Studio tools**. Diagnostics, Expanded OBS mixer, and Recent Soundpad sounds are available to the PC and paired tablet. Automatic profiles, Portable board packs, and the PC setup shortcut appear on the PC. PC setup adds **More studio tools**; the Soundpad library adds **Recent sounds**. Design preview explains that the companion or a paired tablet is needed. The button editor receives its own contextual preview and target helpers. Pairing retains manual entry and adds QR actions at each end.

Opening tools is blocked while changes are unsaved or buttons are being arranged. Tool destinations claim a loading panel before awaiting data. Generation/view checks keep late responses, errors, imports, and previews from replacing a newer destination. An outstanding operation may finish after navigation; view ownership prevents its response from writing into the replacement panel and is not a rollback guarantee.

## Eight added capabilities

### 1. Connection diagnostics

**Connection diagnostics** distinguishes the tablet's Wi-Fi/USB path and persistent TLS link from HTTPS fallback. The PC reports loopback and USB status and explicitly directs users to the tablet for its network path. **Run 10 checks** collects request round trips, retaining up to 120 samples and showing count, median, nearest-rank p95, maximum, and failures. **Refresh status** reads current connection information; **Reset measurements** clears the manual sample/failure history and native fast-link statistics. Connection history and native heartbeat samples/timeouts are shown separately.

The interface states what these numbers mean: request round trips do not measure touch-to-sound, OBS completion, broadcast latency, or packet loss. Backgrounding can disconnect the tablet's fast link. Help covers private Wi-Fi, data cables, USB authorization, both tunnels, and checking OBS/Soundpad separately.

Measurement identity includes host, mode, both ports, paired state, and pairing identity. A changed identity clears manual samples/failures so re-pairing or changing path does not silently mix histories. Native pairing and transport changes also clear fast-link statistics. Explicit reset remains available for comparing sessions on the same path.

### 2. Explicit OBS Start / Stop / Mute actions

The existing Action selector adds **Start streaming**, **Stop streaming**, **Start recording**, **Stop recording**, **Mute OBS input**, and **Unmute OBS input**. Legacy toggle actions remain. Start/stop checks current OBS output state before sending the requested transition; mute/unmute sets the requested boolean state. Repeating a start action therefore does not intentionally toggle an already running output off.

Stream and recording actions always require confirmation, including when nested inside a macro. Explicit mute actions use an exact OBS input name. These additions specify requested outcomes; they do not add a new live-state badge to every board key or replace the existing stream/recording status display.

### 3. Recent Soundpad sounds

The recent list contains the last 30 distinct linked sounds accepted by Soundpad, newest first and shared across controllers. It is persisted separately from favourites. Each entry shows a name, time, and Play action; playback output defaults to **Speakers + microphone** with the incumbent output alternatives. Playback is accepted-command evidence, not proof of audible output or routing.

Stable sound keys are resolved against the current library. Missing or ambiguous matches show **Unavailable** with Play disabled. Library failure gives visible guidance to open Soundpad and refresh. Empty history explains how to populate it. **Clear history** asks for confirmation and preserves favourites/library. Persistence failure has explicit feedback. After playback, the panel tells the user to refresh to update order.

### 4. Friendly button editing

The PC action editor adds **Button preview**, a **Custom PNG icon** chooser, **Use built-in icon**, and **Check target**. App actions add **Find installed apps** and **Choose app file**; local sound actions add **Choose sound file**. Installed choices come from registered app paths and Start-menu shortcuts; selecting one fills the existing Target field and marks the form dirty. File selection is restricted to supported app/shortcut or audio extensions and does not run the selection.

The preview shows the current icon, label, and action type within the existing editor. PNG guidance specifies up to 512 × 512 pixels and under 72 KB. Client selection checks type/size; target checking and saving perform server validation, including PNG decoding/dimensions. The custom icon also travels to the board; an empty-alt image leaves the key's text/accessible label as its name. The preview is an editing aid, not a rendered replica of every board state or a rehearsal.

**Check target** validates action structure, supported files and their existence, required names, hotkey syntax, nested steps, and the icon. It does not execute actions, establish that a shortcut does what the user expects, or prove that OBS/Soundpad names are correct. Its result explicitly calls for rehearsal. Native file selection waits for the person to finish or cancel; it has no inherited network deadline. Cancel returns no path and leaves the target unchanged. A second picker is rejected while one is open, and a late choice only updates the editor that requested it. Ordinary API requests retain their network timeout.

### 5. Offline QR pairing

The PC's existing pairing panel adds **Show pairing QR**. The QR view lets the user choose Wi-Fi/USB and a PC address, then explicitly **Generate QR and PIN**. Generating replaces the previous single-use PIN; the displayed QR/PIN expires after five minutes. USB uses loopback and still requires debugging authorization and both tunnels.

The tablet's connection form adds **Scan pairing QR**. The native camera activity requests camera permission only for scanning, provides **Cancel scan**, and releases the camera when paused. Camera frames are decoded locally with the bundled decoder. Generation/decoding needs no external QR service or internet connection. Permission refusal, unavailable camera, invalid QR, and cancellation retain a manual-pairing route. Scan completion or user cancellation has no network deadline.

The strict pairing payload accepts the versioned Tablet Deck scheme and expected local address, transport, ports, certificate code, and PIN fields. Decoding does not open a URL or connect. It fills the form; the user checks the PC and selects **Pair and connect** to use the existing pinned-certificate pairing flow. QR-provided ports are carried through pairing and later connections. The QR does not bypass certificate/PIN trust or grant access merely by being scanned.

### 6. Opt-in foreground-app profiles

**Automatic profiles** on the PC starts disabled. An explicit checkbox and saved rules map executable names to existing profiles, with up to 30 unique executable rules. The foreground app must remain matched for two seconds before a switch is attempted. An unmatched app keeps the current board; missing profiles and inability to read the foreground app have explanatory status. Rules persist on the PC and are excluded from packs.

Automatic switching pauses for active action execution, held keys, editing/panels, arrangement, and 30 seconds after a manual profile switch. Controllers renew an editing/profile lease every three seconds while a panel is open, changes are dirty, or arrangement is active; the server lease expires after 12 seconds if renewal stops. Closing the panel releases the lease when no other protected state remains. This bounds abandoned-controller locks rather than making an indefinite editing lock. The two-second candidate debounce, renewable controller leases, and manual-switch pause address different causes of unwanted switching.

### 7. Expanded OBS mixer

**Expanded OBS mixer** controls the two configured main OBS inputs plus up to eight additional labelled inputs. It reads the existing OBS state and separates these controls from Windows per-app volumes. Each channel has an input name, decibel output, horizontal volume slider, mute/unmute button, and per-input availability feedback. Sliders span −60 to 0 dB; dragging updates the readout and change commits the value. State refreshes every two seconds while the view owns the panel and supports explicit **Refresh levels**.

Controls start disabled and remain disabled when OBS or a source is unavailable. Errors remain visible; mute labels and `aria-pressed` track the returned muted state. Refresh does not overwrite a slider during its active drag. The PC's **Choose extra inputs** uses existing labelled forms with Add, Remove, Find OBS inputs, and Save inputs. The two main inputs remain in Set up OBS. Saving locks fields against duplicate edits, validates channel identities/names/limits, and returns to the mixer after success.

### 8. Reviewed portable board packs

**Portable board packs** exports one chosen profile with its PNG icons and only the local sound files the user selects after **Choose included sounds**. Sound inclusion is opt-in; missing/oversize sounds cannot be selected. Export clears Windows app targets and unselected local sound targets, including nested actions. Credentials, other profiles, automatic rules, Windows executable files, and Soundpad's native library are excluded. Users are told to share only sounds they have permission to distribute.

Import first previews a selected pack's name, page/button counts, bundled sounds, action-type counts, and trust warning. **Import reviewed pack** requires explicit confirmation. Import creates an inactive profile, runs no action, and directs the user to Profiles to switch and review buttons. App paths are cleared. Hotkeys, websites, OBS controls, and locally dependent Soundpad actions still require trust and review; clearing executable paths does not make every action harmless. Unbundled sounds need configuration.

The source limits packs to 64 MB and individual sounds to 16 MB, bounds metadata and entry counts, checks listed hashes/lengths, rejects unexpected/duplicate archive entries and symlinks, and writes sound assets under generated local directories rather than extracting arbitrary archive paths. A preview expires after ten minutes; no more than two previews wait for review. Imported profiles obey the existing ten-profile limit. Pack upload has a dedicated body allowance for valid files above the ordinary API limit. These validation provisions do not replace the human trust decision.

## Accessibility and state handling

New forms retain native labels, named controls, status/error text, visible disabled treatment, and the incumbent cyan `:focus-visible` outline. Diagnostics, operation results, scanner guidance, and errors communicate state in text rather than color alone. Server-supplied names are escaped, generated markup follows the existing sanitizer path, and custom icons are limited to PNG rather than active-content formats. Save operations in rules/mixer settings lock their controls; unsaved changes participate in existing discard protection. These source features do not establish keyboard traversal, focus return, announcement quality, touch fit, or screen-reader approval.

## Evidence and limits

Inspected the 1.8 record and the Impeccable `document.md` reference; `studio.js`; changed `app.js`, `features.js`, `desktop.js`, `style.css`, and `index.html`; `Models.cs`, `StudioRoutes.cs`, `StudioTools.cs`, `AutoProfiles.cs`, `BoardPacks.cs`, `RecentSounds.cs`, relevant `DeckEngine.cs`/`FastCommands.cs`; and native `QrScanActivity.java`, `PairingPayload.java`, `MainActivity.java`, and `FastLink.java`. The scoped extension record preserves the incumbent documentation authority instead of regenerating root design files.

Inspected saved logs `.test-output/studio-ui.log` (**43 controller checks**), `.test-output/studio-api.log` (**46 API/logic checks and 18 native QR checks**), and `.test-output/studio-storage-next.log` (**48 storage/safety checks**), plus the UI/API result summaries. The logs include path-identity resets, human-operation cancellation/deadline separation, upload limits, icon transport/validation, and explicit broadcast confirmation coverage. This documentation pass did not rerun tests. The parent implementation review reports two P2 findings resolved and approval limited to source/logic.

No browser, computer-use surface, app, or screenshot was opened for this documentation pass. Earlier in the overall development pass, a failed-build runner briefly started a stale companion; that incident was disclosed and the process stopped. It produced no visual verification. The isolated check summaries' no-GUI flags describe those checks, not a claim that the entire development pass had zero app openings.

There is no rendered comparison or human UI/hardware signoff. Actual tablet camera/permission behavior, visual fit, touch/keyboard/focus use, screen-reader announcements, real foreground-app switching, Wi-Fi/USB hardware, live OBS/Soundpad interaction, sound output/routing, and broadcast rehearsal remain unverified. Passing source/controller/API/native-payload checks is evidence for the implemented logic only.
