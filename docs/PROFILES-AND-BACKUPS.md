# Profiles, automatic backups, and Soundpad favourites

Update both the Windows companion and Android APK to 1.7.0 or later. The APK keeps the existing signing certificate, so an update preserves pairing. The first run puts the existing board into a **Default** profile without changing its button IDs, configured targets, or sound routing, and takes a migration backup.

## Board profiles

Choose **Profiles** at the start of the bottom page navigation. It remains available in Studio and both full-pad densities. Select **Switch** beside a configured profile; the PC and all paired tablets share that selection. The old profile's buttons no longer execute after a switch. A connected controller refreshes its board when it observes the changed profile state.

On the PC, the same panel offers:

- **Create empty:** enter a name and create one empty Main page.
- **Duplicate current:** copy the current pages and actions, including Soundpad routing, into a new profile. Page and button IDs are regenerated; sound files and applications stay at their existing paths.
- **Rename:** select a profile, edit its name, and choose Rename.
- **Delete profile:** switch to a different profile first, select the profile to delete, and confirm. A backup is made before deletion.

New profiles are added without switching automatically. There can be up to 10 profiles, 30 pages per profile, and 120 buttons per page. The complete saved configuration must fit within 1 MB. Names have 1–30 characters. If you hit a limit, simplify existing profiles or export a board before removing it.

OBS connection/password, the two mixer input names, pairing, certificate, and favourites are shared across profiles. Switching profiles does not change Windows, OBS, Soundpad, or Discord audio-device settings. Save or discard local edits and release held keys before switching. The server refuses profile changes while an action or macro runs; cancel it first. A stale editor for a different profile cannot overwrite the selected profile.

**Edit → Export** saves only the current board, with its password cleared; other profiles and favourites are excluded. **Import** applies a reviewed board to the current profile when you Save changes. Imported page/button IDs are regenerated. Create an empty profile first if you want to import without replacing an existing board. Absolute PC sound/app paths are references, so transfer the actual files separately when moving PCs.

## Automatic backups and restore

Before a meaningful board save, profile create/rename/delete, favourite change, or restore, the companion saves the previous complete board collection and favourites. It keeps the latest 20 valid snapshots. Profile switches and unchanged saves do not fill the history. **Profiles → Backups and restore → Create backup now** takes a manual snapshot.

Backups live in `%LOCALAPPDATA%\TabletDeck\backups` under the same restricted Windows-user/SYSTEM directory as app data. They contain profile pages, targets, OBS port/input names, and favourites, with the OBS password cleared. They do not contain paired-device credentials or certificates and do not copy audio files, applications, or Soundpad's own library. Board paths and labels may still be private; keep this directory out of public repositories.

To restore on the PC:

1. Open **Profiles → Backups and restore**.
2. Choose **Preview** beside a timestamp. Review the profile names/counts, button counts, favourite count, and which profile will open.
3. Choose **Restore this backup**, then confirm replacement of all current profiles and favourites.

The current board is backed up before replacement. Restore keeps the current OBS password, pairing records, and certificate. It does not roll back changes already made in OBS/Windows or restart playback. A snapshot pruned while you were reviewing it is refused rather than substituted with another one.

If the main configuration is damaged or invalid at startup, its original bytes are copied to a private `config.corrupt-<id>.json` file. The companion tries the newest valid backup; damaged backups are skipped. If none is usable, a default board is loaded. Open Profiles to read the recovery warning. Since backups exclude credentials, **re-enter the OBS password after damaged-file recovery**. Pairing and certificate files remain intact. This differs from a normal confirmed restore, which can preserve the password from a valid current config.

Older snapshots are removed after 20 newer ones. Export important boards separately for longer retention. A downgrade to an older companion that does not understand profiles can discard their metadata on save; preserve your private app-data folder before downgrading.

## Soundpad favourites

Keep Soundpad running and choose **Soundpad → star beside a sound**. Choose **Favourites** to filter to saved sounds. Search and category filters still apply; choose the playback output before Play. Favourites are shared by all controllers and profiles on this PC, survive companion restarts, and are included in private board backups. Up to 200 can be saved.

Favourites record the same stable file identity used by imported Soundpad buttons, rather than relying on the All sounds index. Refresh the library after reordering or renaming entries; favourites use the current title and index. Moving/renaming the actual audio file changes its identity, so remove the old favourite and star the new file.

If a favourite's sound is missing or its identity appears more than once, the favourites view marks it unavailable and disables Play. You can still remove that favourite, including while Soundpad is closed. Adding a favourite requires a uniquely identified sound in the running library. Sounds without a stable identity do not show a star.

Starring does not play a sound. Existing tablet sound buttons keep their own mic/speakers/both selection. Keyboard focus remains on the corresponding result control after an update; if that row disappears it moves to the Favourites filter. Moving focus outside the results while a request runs is respected.

## Verification boundaries

Storage tests use temporary directories and synthetic sound libraries. Authenticated HTTPS tests cover profile management/switching, stale actions/editors, backup previews/restores, credentials, persistence, and missing-library favourite removal. UI-logic checks cover late navigation responses and keyboard-focus preservation. These do not establish rendered fit, native touch ergonomics, or a live Soundpad favourite rehearsal. No app window or browser was opened for this development pass. See [verification](VERIFICATION.txt).
