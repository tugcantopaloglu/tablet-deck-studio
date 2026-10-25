# UI extension record: running actions, 1.6

This is a scoped record of the running-action ribbon. The incumbent HTML and CSS remain the visual authority; this extension preserves the existing Operate direction. It introduces no global design system or replacement identity.

## Existing system used

`companion/wwwroot/style.css` supplies the bundled Manrope typeface and the dark surface hierarchy. The ribbon uses the existing `--surface`, `--outline`, `--ink`, and `--secondary` properties. Cancel inherits the existing danger button's red text, tinted surface, border, rounded corners, disabled treatment, and cyan keyboard-focus outline. Those source declarations remain authoritative; this record does not duplicate them as a second token specification.

The ribbon adds a local danger-button hover surface and local type sizes. Its desktop label/status sizes are 15/13px, reduced to 13/12px below the existing 760px breakpoint. Status numbers use tabular figures. Long labels wrap, the copy column can shrink, and Cancel retains a minimum height of 48px without shrinking.

## Component behavior

The ribbon sits in document flow immediately above page navigation in Studio and both Full pad densities. It reserves its own grid row during a local pending request or an active run and returns that space when idle. Existing key grids, density preferences, and navigation remain in use. No new dialog is introduced.

The ribbon shows the action label and current step out of the total, or a waiting message before the PC provides the run identity. Progress describes the step started; it is not elapsed-time progress. Cancel is disabled before an identity is available, while its request is pending, and while cancellation is in progress. Cancelling copy explains that completed steps are kept. Cancellation and command semantics are documented in [MACROS.md](MACROS.md).

A failed status request replaces step copy with a connection-loss warning that the action may still be running. Cached state at the same revision cannot clear that warning; a fresh successful poll can. Action failures and cancellation feedback use the existing notice component. While the ribbon is visible, notices move below the header so they do not cover Cancel. The step text is a polite live status; labels and status content are assigned through `textContent`.

## Evidence and limits

Reviewed the working diff for `companion/wwwroot/index.html`, `style.css`, and `app.js`, their incumbent component styles, [MACROS.md](MACROS.md), [FULL-PAD.md](FULL-PAD.md), and the execution regression source. The execution suite exercises authenticated cancellation and isolated UI logic; its result artifact explicitly records whether a browser, GUI, or real OBS was used. See [VERIFICATION.txt](VERIFICATION.txt) for the current release's results and totals. This documentation pass did not rerun implementation tests.

No browser, app, or computer-use surface was opened. There are no current ribbon screenshots or rendered inspection results from this pass. Visual fit, physical touch interaction, and live-region behavior with assistive technology remain unverified; earlier Full pad captures do not establish those properties for the new ribbon.
