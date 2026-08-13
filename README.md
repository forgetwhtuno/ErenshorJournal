# Erenshor Journal

**Version:** 0.1.2 Preview  
**Author:** forgetwhtuno  
**Loader:** native Lunaris (BepInEx is no longer required)  
**License:** Apache-2.0

A deliberately small, local notebook for **Erenshor**.

Use the small draggable **Journal** button in the UI to open a journal you can use for whatever you want: quest notes, crafting notes, raid notes, people, places, shopping lists, lore, reminders, or just a normal personal journal.

## Features

- freeform text pages;
- tabs you can add, rename, and delete;
- default `Journal`, `Quest Notes`, and `Crafting` tabs;
- one-click local timestamp insertion;
- copy current page to the clipboard;
- small retained-uGUI `JOURNAL` launcher with Suite-style drag, normalized saved position, fallback visibility, and active/open styling;
- retained-uGUI journal window with a visible close/reset header, Suite-style drag, retained resize grip, scrolling, saved normalized position, and persisted size;
- automatic local saving;
- backup/recovery if the journal data file becomes unreadable;
- optional **Chronicle** view for verified events supplied by compatible mods.

## What this mod intentionally does NOT do

Erenshor Journal is not a quest automation mod and is not intended to replace existing quest/adventure guides.

It does **not**:

- read or replace Erenshor's quest log;
- add quest markers or walkthroughs;
- infer that a quest/event happened;
- store SimPlayer personality or memory;
- generate AI text;
- send notes to a server or web service;
- patch any Erenshor gameplay/combat/quest/inventory method;
- require Deep Sims, Crafting, PvP, COOP, Campmaster, Party Tools, or any other gameplay mod.

The Journal is player-owned text. The Chronicle is only an optional integration sink for other mods that already know a verified event occurred.

## Privacy

Your notes remain on your machine.

The journal file is stored under:

```text
plugins/config/ErenshorJournal/journal.dat
```

The mod performs **no networking** and never writes journal contents to the log. The data format is local and base64-encodes fields only so multiline/unicode text can be saved safely; base64 is **not encryption**.

A `.bak` copy is maintained during successful replacements. If the main file becomes unreadable, the mod preserves it as a timestamped `.corrupt-*` copy and opens a fresh default journal rather than destroying it.

## Installation / build

This version requires **native Lunaris** — BepInEx is no longer required. This source package intentionally does not redistribute Erenshor or Lunaris assemblies.

1. Install Lunaris for Erenshor and launch the game modded once.
2. Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\BUILD_AND_INSTALL.ps1
```

The script locates your current Erenshor installation and the Lunaris developer reference, builds against the installed Unity/Lunaris assemblies, then installs:

```text
plugins/ErenshorJournal.dll
```

Lunaris manages enable/disable and config. A legacy BepInEx release remains available in this repository's Git history.

**Status:** the pre-uGUI native baseline compiled and passed its deterministic tests. The retained-uGUI candidate in this handoff is source-verified but could not be recompiled here because the handoff omitted native Erenshor/Lunaris reference DLLs. Live enable/disable/reload verification is still required.

Erenshor Journal now uses retained Unity uGUI for its player-facing panel and launcher, so it no longer needs Harmony patches merely to protect UI clicks. It requests only Lunaris file access for its local notebook storage and makes no network calls.

Typing into the Journal's tab-name field or note text area also sets the real native `GameData.PlayerTyping` flag while a Journal text control has focus, so WASD/hotkeys don't fire while you're writing a note — the same mechanism Erenshor's own chat box and windows (Bank, Auction House, Guild Manager, Raid save, Quest Log) already use. Journal never clears that flag unless it verifiably owns the current assertion and no other native typing owner is still active, so it can't clobber native chat or another window's typing state.

### Opening the Journal

The normal control is the small draggable **JOURNAL** UI button. Drag it by the `◇` grip on its left edge; its position is saved. Clicking the button toggles the Journal open or closed. The button changes visual state while the Journal is open.

Erenshor Journal **does not register a global hotkey**. This is intentional so it cannot compete with Erenshor or other mods for F-keys or other gameplay bindings. Close the window with its `X` button or the same `JOURNAL` toggle.

The main retained-uGUI window can be moved by its title bar. Its normalized position is saved at drag end, invalid/legacy/off-screen values recover safely, and the configured window size remains available through Lunaris config.

## Chronicle integration API

Other mods may append a verified event:

```csharp
ErenshorJournal.JournalApi.AddChronicleEntry(
    "My Mod",
    "Milestone",
    "Completed something the source mod already verified."
);
```

Do **not** use Chronicle text as proof that an event occurred. The source gameplay system remains authoritative.

For an optional integration with no hard DLL dependency, use reflection and only call the API when the type exists:

```csharp
Type api = Type.GetType("ErenshorJournal.JournalApi, ErenshorJournal");
MethodInfo add = api == null ? null : api.GetMethod("AddChronicleEntry");
if (add != null)
{
    add.Invoke(null, new object[] { "My Mod", "Milestone", "Verified event text" });
}
```

## Compatibility philosophy

This mod deliberately avoids owning other mods' responsibilities.

- Quest/adventure mods own quest guidance.
- Deep Sims owns grounded social memory/dialogue.
- Crafting mods own crafting state.
- PvP/Duel mods own their match state.
- Erenshor Journal only owns player notes and its local Chronicle record.

That makes integrations additive instead of required dependencies.

## Uninstall

Remove the `ErenshorJournal.dll` plugin file. The provided uninstall script preserves your journal data by default.

To intentionally erase the saved notes too:

```powershell
.\UNINSTALL.ps1 -GameDir "C:\path\to\Erenshor" -RemoveData
```

## Development note

This project was developed with substantial AI-assisted coding and review. Gameplay authority and integration boundaries are intentionally kept deterministic and local.

Erenshor Journal is an unofficial community mod and is not affiliated with or endorsed by Burgee Media.


## Optional Suite Hub integration

Erenshor Suite Hub is **optional**. When it is installed, this mod can expose its normal player-facing controls there through the versioned public `JournalControlApi` surface. The mod remains independently usable without Suite Hub and does not compile against Hub types or assume Hub load order.

Journal remains the dedicated notebook/editor. A compact standalone launcher is a fallback and is hidden by default while Suite Hub is loaded.

Hub can show the current character and notebook/Chronicle summary and open or close Journal; it does not edit notes.

The shared control/API and fully-in-world UI policy in this handoff are source-validated but **not yet live-tested under Lunaris hot reload**.

### Content/UI migration candidate

The current source migrates Journal player UI from legacy immediate-mode rendering to retained Unity uGUI. The Suite Hub bridge exposes **Show Journal launcher** and Open/Close/Reset actions without exposing note contents. The standalone launcher is forced visible whenever Hub or the Journal bridge is unusable. This source migration still requires a native compile and live Lunaris UI/reload pass before release.
