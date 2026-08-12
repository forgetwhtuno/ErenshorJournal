# Erenshor Journal - Testing Checklist

## Automated core tests

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\RUN_TESTS.ps1
```

The pure tests cover default state, tab lifecycle, Unicode/multiline persistence, bounded Chronicle history, and corrupt-file recovery without requiring Erenshor assemblies.

## First-run smoke test

1. Launch Erenshor with BepInEx and the plugin installed.
2. Confirm the BepInEx log reports Erenshor Journal 0.1.2 loaded.
3. Confirm the small `Journal` launcher button appears.
4. Click `Journal` and confirm `Journal`, `Quest Notes`, `Crafting`, and `Chronicle` are visible.
5. Type several lines, including punctuation and pasted Unicode text.
6. Close with the UI toggle, reopen, and verify the text remains.
7. Quit the game normally, relaunch, and verify persistence again.

## Tab behavior

- Add several tabs.
- Rename them.
- Confirm tab names are limited to 40 characters and cannot contain newlines/tabs.
- Delete a tab and verify the two-step confirmation.
- Verify the final remaining normal tab cannot be deleted.
- Verify duplicate tab names do not corrupt persistence.

## Editing

- Type and paste multiline text.
- Use Timestamp and verify local clock formatting.
- Use Copy and paste into an external editor to verify contents.
- Leave the journal open long enough for autosave, terminate normally, and verify the latest edit persisted.

## Window / UI

- Drag the window to each screen edge.
- Close and reopen; verify position persists.
- Change resolution/window mode and confirm the panel is clamped back onto the visible screen.
- Drag the small launcher by its `||` grip and verify its position persists after restart.
- Confirm the launcher button opens and closes the Journal without any hotkey.
- Confirm no keyboard shortcut opens or closes Journal.
- Confirm the same `JOURNAL` launcher toggles the window both directions and visibly changes state while open.
- Resize using the lower-right `//` grip; close/reopen and verify the saved size persists.
- Confirm cursor lock/visibility returns to its previous state after closing.

## Chronicle API

From a test plugin or reflection console, call:

```csharp
ErenshorJournal.JournalApi.AddChronicleEntry("Test", "Milestone", "Journal API test");
```

Verify:

- one entry appears in Chronicle;
- timestamp displays in local time;
- normal journal tabs are unchanged;
- Copy includes the entry;
- Clear requires confirmation;
- journal logs do not contain the Chronicle text.

## Persistence recovery

After backing up real notes first:

1. Close Erenshor.
2. Corrupt `BepInEx/config/ErenshorJournal/journal.dat` with arbitrary text.
3. Relaunch.
4. Verify a default journal opens.
5. Verify a `.corrupt-YYYYMMDD-HHMMSS` copy was preserved.
6. Verify the log reports a recovery warning without printing journal contents.

## Compatibility / patch resilience

The plugin intentionally uses:

- BepInEx plugin lifecycle;
- Unity `Input`, IMGUI, cursor, screen, and time APIs;
- standard .NET file APIs.

It intentionally does **not** use Harmony or `Assembly-CSharp.dll`.

After an Erenshor patch, the primary smoke test should therefore be: plugin loads, the UI launcher opens the Journal, typing works, persistence works, and no input/UI regression appears.

## COOP

The Journal has no network authority and does not inspect local or remote actors. Verify it remains a purely local overlay during a COOP session and that no note or Chronicle data is transmitted by this mod.
