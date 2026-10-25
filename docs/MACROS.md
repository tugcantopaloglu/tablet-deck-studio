# Running and cancelling macros

Update both the Windows companion and Android APK to 1.6.0 or later. Pairing and existing buttons are preserved when updating with the same Android signing key.

In the PC editor, choose a button, select **Run multiple actions**, and add steps. Each macro supports up to 30 immediate steps and three nesting levels. The full run has a 45-second budget. Delay steps accept 0–10000 milliseconds. Streaming and recording steps still require confirmation before the macro starts.

When a button runs, a ribbon above the page tabs shows the action's label, current step, and total step count. Nested macros contribute their individual steps to that count. The ribbon remains available in Studio and both full-pad densities; when idle it returns its space to the keys. Progress reports which step has started, not a percentage of elapsed time.

Choose **Cancel action** to cancel that particular run. A delay stops promptly. For OBS, Windows, or another tool, a command already started may finish before cancellation takes effect; later macro steps are skipped. The ribbon shows **Cancelling…** until the run releases its execution slot. Other configured actions can then run again.

Completed steps stay applied. Cancellation does not roll back a scene, stop a broadcast or recording, close an app, release a separate hold shortcut, or silence a sound already playing. Use the dedicated Soundpad or deck stop control to stop playback. Stopping deck playback and stopping Soundpad are separate controls.

Cancellation works through the authenticated persistent TLS link and HTTPS fallback over Wi-Fi or USB. It bypasses the action execution slot, so a waiting macro cannot block its own cancellation. A specific run ID prevents a late or repeated tap from cancelling a subsequent action. All paired controllers share the running-action view and can cancel the current run. Configured file paths, hotkeys, and OBS passwords are not exposed by the progress endpoint.

If the connection is lost, the ribbon warns that the action may still be running. A network disconnect does not cancel a macro automatically. Reconnect to check its status and cancel it, or wait for its execution budget to expire. Commands are never replayed automatically.

## Verification

`npm run test:execution --prefix tests` covers authentication, HTTPS cancellation, nested step counts, stale run IDs, repeated cancellation, failure cleanup, confirmation, and UI status logic without opening a browser. `npm run test:fast --prefix tests` additionally checks cancellation while a persistent-TLS action request is pending, and uses a mock OBS service to prove the pre-cancellation scene is kept and a later scene never runs.

These tests do not verify the ribbon's visual fit or touch interaction on a physical tablet. The existing layout tests remain available for a later browser-enabled session.
