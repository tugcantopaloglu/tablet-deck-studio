# UI extension record: PC setup and guided OBS, 1.8

This scoped extension makes the Windows control panel easier to set up while preserving the incumbent dark Studio / Operate identity. As in [the 1.7 extension record](UI-EXTENSION-1.7.md), `companion/wwwroot/style.css`, bundled Manrope, existing CSS tokens, and existing components remain authoritative. This record describes the added desktop surface and its relationship to that system. It introduces no global product brief, design system, token specification, or sidecar.

## Existing system used

PC setup reuses the settings dialog, scrollable panel body, quiet and primary buttons, labelled form grid, wrapping action rows, help text, and confirmation dialog. The guided OBS form inherits the existing dark fields, disabled treatment, and cyan focus outline. Setup rows separate content with `--outline`; status and instructions use the existing text hierarchy rather than a new palette or decorative card system.

The PC toolbar wraps and uses an automatic-height first grid row so an extra line of controls has layout space. The execution-visible layout already uses an automatic header row. The separate Pair tablet toolbar entry is hidden on the PC, where pairing is reached through PC setup. The existing Studio and Full pad layouts remain the basis for board use.

Setup rows place a heading, text status, and explanation beside a non-shrinking action button. Long status text can wrap, and action labels can wrap within their maximum width. At the added 600px breakpoint, rows stack their action beneath the copy. The panel and form continue to use the incumbent 760px responsive rules, including a single-column form and reduced panel padding. These are source-level layout provisions; no rendered fit is asserted.

## Entry, checks, and destinations

**PC setup entry.** The toolbar entry is enabled only in the PC control panel, excluding Android and design preview. When no tablets are paired, setup opens automatically once per browser-tab session. Detection does not interrupt an already opened panel, newer navigation, unsaved changes, or arrangement. Existing pairings leave the board as the initial view. Closing the panel keeps the companion running; the Windows tray menu is the exit route.

**Read-only checks.** Opening or refreshing the hub reads local admin information, desktop preferences/status, OBS state, backup summaries, and profiles. It does not run board actions, generate a pairing PIN, query the Soundpad library, or initiate an update check. The hub shows a checked time and offers Refresh checks. A failed request produces visible error text, Retry checks, and guidance to reopen an expired local admin session through the tray menu.

**Status semantics.** Tablet count means saved pairings, with explicit copy that it does not establish a current connection. OBS connection and availability of the two configured audio inputs are separate facts; a connected server may have missing inputs. The board row identifies the active profile and page/button counts. Backups show saved snapshot count or an empty state. USB status is a snapshot of the tunnel/reconnect state. These checks do not establish tablet latency or broadcast readiness.

**Existing destinations.** Pair tablet opens the established address, certificate-code, and single-use PIN flow. Edit buttons opens the incumbent editor. Manage profiles and Backups and restore reuse the 1.7 management and confirmed restore flows. Browse sounds opens the existing Soundpad library and favourites; the hub explains that Soundpad must be running and suggests Speakers + microphone for Discord. It does not change audio devices or Discord settings. Use board closes the setup panel. User instructions are in [PC setup](PC-SETUP.md).

## Guided OBS and Windows options

The OBS surface leads with numbered steps for opening OBS, enabling its WebSocket server with authentication, and copying the port/password. Labelled fields cover port, password, microphone input, and desktop audio input. Save OBS settings changes only those connection settings through the authenticated PC endpoint; settings apply across profiles. Check saved connection and Find inputs use persisted settings, so editing a field alone does not change what they check. Checks report connection/input state without starting streaming, recording, playback, or another board action.

A blank password keeps the stored value; a replacement is explicit and the field clears after a successful save. Saved passwords are not returned for display. Clear saved password requires confirmation, and combining it with a replacement produces visible feedback without submitting. Port validation precedes the write. Inputs and form buttons are disabled during an outstanding operation, preventing duplicate submissions and edits that a response could otherwise lose. OBS and Windows edits join the existing unsaved-change protection; setup navigation also blocks while arranging buttons. Save or explicitly discard changes before leaving.

Windows options reuses the desktop settings form for Start with Windows, automatic USB reconnect, optional tablet serial, and update notifications. Help text retains the USB debugging/authorization requirement, five-second reconnect cadence, and multiple-tablet serial guidance. Update notifications describe contact with the public GitHub release API and manual installation. Windows fields lock during save, with visible save or failure feedback and separate update status. Hub status refresh remains explicit.

## Asynchronous ownership and accessibility

The hub and guided OBS view use panel-generation and view-ownership guards. Late loads, saves, input discovery, or errors cannot overwrite a newer destination, clear that destination's unsaved state, or reopen a dismissed panel. The existing editor, pairing, and Windows destinations now claim a loading panel before awaiting their initial request and check ownership before applying results. This closes the navigation race where a legacy destination could replace a newer unsaved OBS form. Pairing also rechecks ownership around revoke confirmation and completion.

Loading messages and connection results use status regions; setup errors use an alert region. Fields retain native labels, password masking, and the existing `:focus-visible` treatment. Profile names and discovered input names are escaped, and generated panel markup follows the existing sanitizer path. These source features provide implementation evidence, not approval of focus behavior or assistive-technology announcements.

## Evidence and limits

Reviewed `desktop.js`; the changed editor/pairing functions in `app.js`; Windows settings in `hardening.js`; `index.html`; `style.css`; [PC-SETUP.md](PC-SETUP.md); the 1.7 record; the desktop regression source; and the saved desktop/headless logs. The logs report **60 desktop API/controller checks** and **30 headless API/JavaScript checks**. Coverage includes authenticated OBS writes, password modes/redaction, board/profile/pairing preservation, first-run gating, read-only hub behavior, unsaved-state protection, and late-result ownership. This documentation pass did not rerun them. The reviewed source includes the fixes for legacy asynchronous destination replacement and the wrapped PC header's fixed-height track.

No browser, app, computer-use surface, or screenshot was opened for this pass, in accordance with the standing restriction. There are no current rendered comparisons for this extension. Isolated controller/API checks and CSS inspection do not verify visual fit, touch behavior, real keyboard or screen-reader use, live OBS/Soundpad interaction, Windows startup integration, USB hardware, or audio routing. No rendered, touch, focus, or assistive-technology signoff is claimed.
