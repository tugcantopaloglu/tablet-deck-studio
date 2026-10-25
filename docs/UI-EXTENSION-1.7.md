# UI extension record: profiles, backups, and favourites, 1.7

This scoped extension preserves the existing Operate direction. `companion/wwwroot/style.css`, its bundled Manrope typeface, and the incumbent HTML components remain the visual authority. This record adds no product brief, global design system, token sidecar, or replacement identity.

## Existing system used

Profiles and backup views reuse the existing modal panel, form grid, wrapping action rows, confirmation dialog, and quiet/primary/danger buttons. Their surfaces, borders, text, focus outlines, and disabled treatment inherit the existing CSS. Local styles use `--outline` and `--secondary` for row separation and metadata, `--cyan` for the Profiles launcher, and `--amber` for saved favourites. Source declarations remain authoritative; this record does not restate their values as another token specification.

The Profiles launcher extends page navigation in Studio and both Full pad densities. It does not depend on the header controls hidden in Full pad. Profile rows allow long names to wrap while their action buttons do not shrink. At the existing 760px breakpoint, row gaps and button padding reduce. Favourite stars occupy 48px square controls, retain that width on narrow screens, and use a filled star as well as amber when selected.

## Interactions and states

**Profiles.** The launcher identifies the active profile in its accessible name and tooltip. The panel lists page/button counts and marks the current profile with text and a disabled Current button. Both PC and paired tablet can switch. The PC editor additionally creates empty profiles, duplicates the current board, renames, and deletes; the tablet explains where management happens. Names accept 1–30 characters, and the collection allows up to ten profiles. Creation does not switch automatically. Deleting requires another profile to be active and a confirmation.

Switching changes pages and buttons together and resets page navigation. Pairing, OBS settings, audio inputs, and Soundpad favourites remain shared. Unsaved edits, arrangement, pending/running actions, and held keys block board changes with feedback. Board import stays in the current profile with new page/action identities; export contains the current board and excludes credentials, other profiles, and favourites.

**Backups.** The PC-only Backups and restore view lists snapshot date, reason, and profile/button/favourite counts, with an explicit empty state and manual backup action. Changes to boards, profiles, and favourites capture the previous state; profile switches and no-op changes do not consume history. History retains the newest twenty valid snapshots. Preview describes the replacement profiles, the profile that will open, and the favourite count. Restore requires confirmation, backs up the current board first, and preserves current credentials. Copy explains that sound files and applications are referenced rather than copied, and that damaged-config recovery requires re-entering the OBS password.

**Soundpad favourites.** Each identified library sound has a star toggle, and the Favourites filter works with the existing search/category controls and pagination. Starring does not play a sound. Saved identities resolve to the current library index and title. Missing or duplicated identities remain removable in the favourite view, with an explanatory reason and disabled Play; the UI does not choose an ambiguous match. Empty favourites, no matches, library loading, unavailable library, favourite-load failure, and request failures have visible text states.

## Accessibility and asynchronous updates

Native buttons and labelled fields retain the incumbent cyan `:focus-visible` outline. Star toggles have sound-specific Add/Remove labels and `aria-pressed`; the filter also exposes its pressed state. Counts and library feedback use status regions. Profile/backup errors appear inside the modal in an alert region, where they remain visible while the dialog is open. User-provided names are escaped, and inserted markup uses the existing sanitizer.

After a favourite update replaces result rows, focus is restored by stable sound identity and control type: star, Play, or selection checkbox. Removing the focused row falls back to the Favourites filter. Focus that has moved outside the results is preserved. Panel-generation and view-ownership guards prevent late profile, backup, preview, and favourite responses from replacing a newer view or reopening a dismissed panel.

## Evidence and limits

Reviewed the working diff for `app.js`, `features.js`, `style.css`, and `index.html`; the new `boards.js`; the storage implementation and storage/API/UI-logic regression sources. The saved boards result reports 53 passing checks and explicitly records no browser, GUI, or real Soundpad use. Those checks cover authenticated management boundaries, restore confirmation, stable-identity matching, modal ownership, and focus restoration. The release verification records the separate 32 storage checks and existing regression results; see [VERIFICATION.txt](VERIFICATION.txt). This documentation pass did not rerun tests.

No browser, app, or computer-use surface was opened. There are no current screenshots or rendered inspection results for this extension. Source review and isolated UI logic do not establish visual fit, physical touch behavior, assistive-technology announcements, or live Soundpad playback; no visual signoff is claimed.
