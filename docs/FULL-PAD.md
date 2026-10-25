# More buttons with Full pad

Tap **Full pad** in the top bar to give the board the mixer’s space. Choose **24 buttons** or **40 buttons** from the selector at the top. Connection and broadcast state, page tabs, and the exit button stay available.

| View | Wide landscape layout | Buttons per group |
| --- | --- | ---: |
| Studio | Four columns, three rows, mixer alongside | 12 |
| Full pad | Six columns, four rows | 24 |
| Full pad, dense | Eight columns, five rows | 40 |

This changes how many existing actions fit in each group. Add actions using **Edit** on the PC; Full pad does not create extra actions automatically. Pages with more actions use Previous/Next. Switching density keeps the group containing your previous first action visible. The view preference is saved locally on each tablet or browser and does not change other devices.

Use **Exit full pad** to restore the audio mixer, Soundpad library button, and editing controls. Save or cancel button arrangement before changing views. Release a held shortcut first; a layout change cannot interrupt an active hold.

Smaller windows use fewer columns and scrolling to retain usable touch targets. Forty-button mode trades button size for density; choose 24 if labels or targets feel too small. Long names show up to two lines while keeping the full accessible name and desktop hover title. Icons have padded bounds and shrink within their button; an active-scene badge has reserved space above its icon.

The Android wrapper already uses a full-screen app window. Full pad expands the board inside that window; it does not change Android’s display size, resolution, or system settings. On the PC, it expands the board within the browser window.

## Verification

`npm run test:layout --prefix tests` exercises a synthetic 55-action board in an isolated browser. Its synthetic HTTPS origin is intercepted and served from preloaded static assets; it opens no network listener. Twenty-six checks cover 12/24/40 layouts at 1067×667, 1024×600, 1440×900, and 640×360; touch targets; all authored icon paths; label and badge containment; badge/icon separation; pagination; persisted preferences; blocked changes while arranging or holding a shortcut; and reordering across dense groups. Screenshots under `.test-output/layout` are generated fixture captures, not a real stream or user configuration. Set `DECK_CAPTURE_LAYOUT=0` to rerun assertions without taking more screenshots.

On the physical Android 16 tablet, the updated APK preserved pairing, entered full-pad mode, selected 40 buttons, displayed the existing twelve actions in eight columns, and hid the mixer. The 40-action coverage comes from the isolated fixture; no temporary test actions were added to the user's board.
