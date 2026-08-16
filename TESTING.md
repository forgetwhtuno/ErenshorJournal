# Erenshor Journal 0.1.7 - RC camera and player-workflow checklist

## Automated deterministic tests

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\RUN_TESTS.ps1
```

Coverage includes default/manual-note CRUD, selected-tab persistence, Unicode/multiline text, manual timestamp formatting, **manual-note vs Chronicle isolation**, structured Chronicle v2 persistence, stable-event exactly-once admission across reload, legacy-v1 Chronicle loading, per-character separation, progression significance filtering, missing optional Crafting API behavior, malformed/corrupt recovery, character keys, migration, native typing ownership, Suite launcher fallback, pointer gesture cleanup, and toolbar/screen-fit geometry.

## First character / persistence

1. Launch Erenshor with Lunaris and the plugin installed.
2. Before entering the world, confirm no character journal file is created and no Journal launcher is shown.
3. Enter the first character and confirm the `JOURNAL` launcher appears when Hub recovery policy requires it.
4. Open Journal and confirm `Journal`, `Quest Notes`, `Crafting`, and `Chronicle` are present.
5. Create a custom tab, rename it, type multiline/unicode notes, click **Add Time** and type after its timestamp, and Copy into an external editor.
6. Close/reopen the panel, zone once, logout/login, and relaunch the game; verify the latest note and selected tab persist.
7. Verify storage is under `plugins/config/ErenshorJournal/Characters/<character-key>/journal.dat`.

## Character isolation

- On character A, write a unique marker in a normal tab and add a custom tab.
- Logout to character select, enter character B, and confirm A's marker/custom tab contents do not appear.
- Add different content on B, switch back to A, and verify A's data returns unchanged.
- If two slots use the same character name, verify their slot-qualified character directories remain distinct.
- Reuse a slot with a **different** character name and verify the new key is distinct. Same-name + same-slot recreation remains an explicit native-identity limitation until Erenshor exposes a stronger proven save identifier; do not claim isolation for that indistinguishable case.
- Trigger/queue a Chronicle integration event immediately before a character switch and verify it never appears in the newly active character's Chronicle.

## Tab / editing behavior

- Switch repeatedly among Chronicle, default tabs, and custom tabs; selection and text stay matched to the visible tab.
- Rename a tab; names are bounded to 40 characters and cannot contain newlines/tabs.
- Use a 40-character tab name and many custom tabs; the horizontal tab strip remains scrollable and long names receive bounded wider buttons rather than overlapping adjacent tabs.
- Paste a very long multiline Unicode note (at least ~1 MB for torture testing), switch tabs repeatedly, close/reopen, and verify content remains exact and editing focus stays on the selected page.
- Delete a tab and verify the two-step confirmation.
- With one normal tab remaining, Delete is visibly disabled and cannot remove it.
- Clear Chronicle requires confirmation when entries exist and is disabled when empty.
- Click into/out of tab-name and note fields; gameplay keys do not fire while Journal owns text focus, and native chat/window typing ownership is not cleared by Journal.

## Chronicle integration

From a test plugin or reflection console, call:

```csharp
ErenshorJournal.JournalApi.AddChronicleEntry("Test", "Milestone", "Journal API test");
ErenshorJournal.JournalApi.AddChronicleEvent(
    "test:milestone:001",
    "Test",
    "Milestone",
    "Test milestone reached",
    "Structured Journal API test");
```

Verify:

- one **separate selectable Chronicle row** appears with a sane local display timestamp, concise title, source/category provenance, and summary;
- normal tabs are byte-for-byte unchanged by Chronicle admission;
- immediate exact repeats through the legacy v1 API are suppressed;
- `AddChronicleEvent(eventId, source, category, title, text)` creates a separate selectable row and replaying the same `(source,eventId)` is rejected even after save/reload;
- oversized source/category/text inputs are bounded without corrupting the file;
- Copy includes saved Chronicle history;
- reload does not duplicate existing Chronicle entries by itself;
- malformed external payloads cannot make the Journal unreadable;
- Journal logs never contain Chronicle text, note text, character names/keys, or local data paths.

## Meaningful progression / optional sibling behavior

With **Crafting Expanded absent**, launch Journal and verify it loads normally with no error and no phantom Chronicle rows.

With the current Crafting Expanded installed:

1. Load a character whose Foraging/Crafting levels are already above 1. Open Chronicle and wait several seconds. Existing levels are treated as the baseline; Journal must **not** backfill fake historical level-ups.
2. Gain ordinary Foraging XP that does **not** level the skill. Chronicle count must not change and no manual note body may change.
3. Gain enough verified Foraging XP to level once. Within the observer poll interval a new row should appear, e.g. **Foraging reached level 2**, with source `Crafting Expanded`, category `Progression`, and a concise previous→new-level summary.
4. Continue earning XP at the same level. No extra row should appear.
5. If Smithing/Crafting level increases through the current public Crafting state, verify the same one-row milestone behavior.
6. Logout to another character with different existing levels. Its first observed levels are a fresh baseline; no milestone from the previous character may appear. Level the second character once and verify its Chronicle only.
7. Save/reload after a level milestone and keep the same level. The saved milestone must remain exactly once.

The observer is reflection-only and failure-closed. If Crafting Expanded is disabled, hot-unloaded, or its public state shape is unavailable, Journal simply stops observing it and remains a standalone notebook.

## Recovery / old data

After backing up real notes first:

- Add one malformed record to an otherwise valid per-character `journal.dat`; readable tabs/Chronicle remain available and a warning is emitted without note contents.
- Save two versions, truncate the live file to only the header/selection record, and confirm the truncated file is preserved while the previous `.bak` version is recovered.
- Simulate a first-save interruption by leaving only a complete `journal.dat.tmp`; confirm it is recovered with a generic warning.
- Leave an older valid `journal.dat` plus a newer structurally complete `journal.dat.tmp`; confirm the newer temp is recovered. Repeat with a partial/truncated newer temp and confirm it cannot override the readable main.
- Leave a corrupt live file, an older readable `.bak`, and a newer complete `.tmp`; confirm the newest validated recovery candidate wins and the corrupt live file is preserved.
- Replace the whole file header with arbitrary text; if no readable backup/temp exists, the file is preserved as `.corrupt-YYYYMMDD-HHMMSS*` and a default notebook opens.
- Test the old global `plugins/config/ErenshorJournal/journal.dat`: only the first eligible character claims a copy, the legacy file remains untouched, and later characters do not inherit it.
- Simulate an interrupted legacy claim by leaving the claim marker present with no per-character copy; a later character must start fresh and the untouched global legacy file must remain available for manual recovery.

## Window / launcher UI

- Collapse/expand:
  - [ ] Expanded header shows `▾`; clicking it leaves only the compact ~32px draggable header and changes the mark to `▸`.
  - [ ] While collapsed, tabs/page/Chronicle content and the resize grip are not rendered or clickable; Reset and `X` remain reachable.
  - [ ] Drag the collapsed header near each screen edge, expand, and confirm the restored body remains on-screen with the header staying in place.
  - [ ] Repeated collapse/expand does not duplicate Canvas roots, controls, or resize grips.
  - [ ] Ordinary note/footer/selection text changes do not force a structural UI rebuild.

- Press and drag the launcher `◇` grip and Journal header while watching the game camera: **no initial camera twitch** should occur before the UI starts moving. Release outside/at the edge and verify camera control returns normally.
- Drag launcher and panel; positions persist after restart.
- Resize the main panel; size persists. Pointer-up/end-drag must release native `DraggingUIElement`; closing/disabling Journal mid-gesture must also release it.
- Change resolution/window mode, including a small resolution; the panel shrinks/clamps into visible bounds. Return to the larger resolution and confirm the prior preferred configured size restores rather than persisting the temporary shrink.
- Switch tabs with long notes/Chronicle history and confirm scrolling works and controls do not clip.
- Confirm the toolbar has distinct **Add Time / Copy / Delete** hit areas; Add Time is visibly a manual note helper, inserts a clean timestamp separator only into the selected note, focuses the note field, and places the caret at the end.
- Confirm cursor visibility/lock returns to its prior state after closing.

## Suite launcher contract

- Hub absent/unusable: recovery launcher is visible while a character is active.
- Hub healthy + Journal bridge registered + `Show Journal Launcher` OFF: standalone launcher is hidden.
- Toggle `Show Journal Launcher` ON/OFF from MODS and verify state changes immediately.
- `Open Journal` opens the dedicated panel; close/reset actions remain functional.
- Hub status is concise and does not expose character keys, tab names, note contents, copied text, or Chronicle text.

## Lifecycle / unload

- Zone repeatedly with Journal closed and open; no duplicate canvas/launcher appears.
- Append a Chronicle event with the panel closed and immediately logout before autosave; after login/reload the event is still present.
- If duplicate plugin initialization is forced in a development session, the extra instance is ignored and does not create another canvas/launcher.
- Logout/login clears transient selection/focus without losing saved notes.
- Lunaris disable/re-enable/hot reload leaves no stale retained UI, drag ownership, typing ownership, or queued Chronicle event.
- Missing optional Hub/integration sources do not prevent base Journal startup.

## COOP / privacy

Journal has no network authority and does not inspect local or remote actors. During a COOP session verify it remains a local overlay and no note or Chronicle data is transmitted by this mod.
