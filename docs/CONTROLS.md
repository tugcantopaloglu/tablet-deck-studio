# Library, app volumes, and button ordering

These controls were added in version 1.2.0. Update both the Windows companion and Android APK. Existing boards, pairing records, and OBS settings stay in the companion's data directory.

## Browse Soundpad

1. Start Soundpad on the Windows PC under the same account as the companion.
2. Choose **Soundpad** in the board toolbar. On the tablet, it is also available under **Edit → Soundpad library**.
3. Search by sound name, artist, or category. Choose a category to narrow the results. **Show more sounds** reveals the next group of results.
4. Choose **Playback output**: Microphone, Speakers, Speakers + microphone, or Soundpad default.
5. Press a sound's **Play** button. Browsing and selecting sounds do not start playback.

The microphone output uses Soundpad's device configuration. For Discord, select the same microphone in Soundpad and Discord and test transmission. See [integration setup](INTEGRATIONS.md).

### Add sounds to your board

Importing is available in the Windows browser panel:

1. Save or discard any unsaved editor changes.
2. Open **Soundpad** and check the sounds you want.
3. Choose a destination page under **Add selected sounds to**, then select the playback output.
4. Choose **Add selected buttons**. The new buttons are saved immediately and sync to the tablet. Reimporting the same index and output into that page skips duplicates.
5. Use **Edit** to adjust their names, icons, colors, or position.

The tablet can browse and play sounds but cannot import new actions or change PC targets. Up to 120 sounds can be selected for one import, within the page's 120-button limit. Create another page on PC if needed.

Soundpad indices follow its **All sounds** list. Moving or removing sounds there can change which sound an existing board button plays. Choose **Refresh library** after library changes, and update affected saved buttons in Edit. In 1.3, new imports record a file-path hash and resolve the current index before playback. Older/manual buttons need verified linking. Names remain user-editable. See [remapping](DESKTOP-AND-HOLD.md#soundpad-remapping).

Library metadata is cached for 30 seconds; Refresh library bypasses the cache. Up to 5,000 sounds are supported. If the installed Soundpad version cannot provide categories, all sounds remain searchable and the library displays a warning. A closed/unresponsive Soundpad produces an error and can be retried after starting it. Sound files remain on the PC; the tablet receives names and metadata, not the library's file-path fields or sound files.

## Windows app volumes

Choose **App volumes** below the two OBS faders. The window lists Windows playback sessions by app and output device. Search by either name, drag a volume slider from 0 to 100%, or use **Mute / Unmute**. Each change applies immediately and also appears in Windows Volume Mixer.

An app needs a playback session to appear. Start playing audio in the game, browser, Discord, or music player, then choose **Refresh apps**. The list also refreshes while open. Apps can have several sessions or output devices; adjust the row for the intended output. Restarting an app or changing devices can end a session; refresh if a control reports that it ended.

These controls affect the app's playback volume on Windows, including audio a streaming tool may capture. They are separate from the board's two OBS input faders and do not change microphone gain, Discord input sensitivity, or Windows master volume. Mute does not move the slider to zero; unmuting restores that session's chosen level.

## Arrange buttons

### Windows editor

Open **Edit**, select a page, then drag a button row using its right-side handle. The selected button stays selected. **Move earlier / Move later** remain available as keyboard-friendly alternatives. Choose **Save changes** to persist the new order with any other edits.

### Tablet or board view

1. On the tablet choose **Edit → Arrange buttons**. On PC choose **Edit → Arrange board** after saving or discarding editor changes.
2. Drag a key over another key to move it. Buttons move within their current page. Page tabs and Previous/Next controls let you navigate larger boards.
3. Choose **Save order** to sync the layout, or **Cancel** to discard the ordering changes.

Actions are disabled while arranging. A cancelled touch gesture restores its previous order. With a keyboard, focus a key and use **Alt + Left/Right** to move one position or **Alt + Up/Down** to move four positions. Save order is still required.

Dragging moves keys within the currently visible group of twelve. To move across groups using touch, tap a key, choose its new position from the full page list, and choose **Move button**. Keyboard movement can also cross groups. To move a button between pages, edit the board on PC. Changing labels, action targets, or adding new buttons remains a PC task.

Avoid editing the same board on two devices at once. If the PC adds/removes actions while a tablet is arranging, the server rejects an outdated layout rather than dropping actions. Cancel and reopen Arrange buttons to load the current board.

## Verification

Library parsing, categories, fragmented responses, import limits, and unsafe XML rejection are covered by automated fixtures. Browser tests cover search, playback/import payloads, app volume/mute, mouse/touch/keyboard ordering, save/cancel, and layout bounds. A separate smoke test changes and restores only the test process's real Windows audio session.

No physical Android tablet or live Soundpad/Discord audio setup was available for this release. Rehearse those paths on your devices before broadcasting. See [verification details](VERIFICATION.txt).
