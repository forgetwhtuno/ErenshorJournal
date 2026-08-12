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
- small draggable `JOURNAL` UI launcher with saved position and active/open styling;
- draggable, resizable Erenshor-like journal window with saved position and size;
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
- patch Erenshor gameplay methods;
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

**Status:** this native build compiles cleanly against the installed Lunaris/Assembly-CSharp and passes its deterministic test suite. It has not yet been live-tested in-game under Lunaris (enable/disable/reload behavior). Do not assume hot-reload safety until that pass is done.

Unlike many gameplay mods, Erenshor Journal does **not** reference `Assembly-CSharp.dll` and uses no Harmony patches. That is intentional: a notebook should have a very small patch-maintenance surface.

### Opening the Journal

The normal control is the small draggable **JOURNAL** UI button. Drag it by the `||` grip on its left edge; its position is saved. Clicking the button toggles the Journal open or closed. The button changes visual state while the Journal is open.

Erenshor Journal **does not register a global hotkey**. This is intentional so it cannot compete with Erenshor or other mods for F-keys or other gameplay bindings. Close the window with its `X` button or the same `JOURNAL` toggle.

The main window can be moved by its title bar and resized from the `//` grip in the lower-right corner. Its position and size are saved and clamped back on-screen after resolution changes.

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
