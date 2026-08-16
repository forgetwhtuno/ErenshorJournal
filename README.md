# Erenshor Journal

Part of the **Forgotten Roads for Erenshor** mod collection.

**Version:** 0.1.8 retained-UI visual candidate
**Author:** forgetwhtuno  
**Loader:** native Lunaris (BepInEx is no longer required)  
**License:** Apache-2.0

A deliberately small, local notebook for **Erenshor**.

Use the small draggable **Journal** button in the UI to open a journal you can use for whatever you want: quest notes, crafting notes, raid notes, people, places, shopping lists, lore, reminders, or just a normal personal journal.

## Features

- freeform text pages;
- tabs you can add, rename, and delete;
- default `Journal`, `Quest Notes`, and `Crafting` tabs;
- one-click manual **Add Time** helper that adds a clean local timestamp inside the selected player note and focuses the note field;
- copy current page to the clipboard;
- small retained-uGUI `JOURNAL` launcher with Suite-style drag, normalized saved position, fallback visibility, and active/open styling;
- retained-uGUI journal window with a dark translucent/cyan-framed Suite treatment, visible `▾`/`▸` collapse + reset/close header controls, Suite-style drag, retained resize grip, scrolling, saved normalized position, and persisted expanded size;
- automatic local saving;
- atomic local replacement with `.bak`, `.corrupt-*`, and validated interrupted-save `.tmp` recovery;
- structured **Chronicle** view with separate selectable history entries; compatible mods can supply verified events, and optional Crafting Expanded level state can contribute meaningful Foraging/Crafting level-up milestones without logging raw XP ticks.

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

The Journal's normal tabs are player-owned text. Automated history never appends into those note bodies. The **Add Time** button is a manual editing convenience only. Chronicle is a separate structured collection: legacy v1 integrations can append already-verified events, v2 integrations can supply a stable source-owned event ID for durable exactly-once admission, and Journal can optionally observe only public **level** state from Crafting Expanded after establishing a per-character baseline. Raw XP values are not read by that progression path, so ordinary gather/craft XP ticks do not create history spam. Pending integration events are tied to the active character so they cannot spill into another character after a switch.

## Privacy

Your notes remain on your machine.

Journal data is stored per character under:

```text
plugins/config/ErenshorJournal/Characters/<character-key>/journal.dat
```

Older global `plugins/config/ErenshorJournal/journal.dat` data is preserved and may be imported once by the first character that loads after migration; it is never silently copied into every character. The one-time claim marker is committed before that private-data copy, so an interrupted migration fails closed: later characters do not inherit the same legacy notes, and the original legacy file remains available for manual recovery.

Per-character identity uses the strongest currently evidenced local key available to this source: verified save-slot index plus character name. Different slots, or a reused slot with a different character name, stay isolated. A deleted character recreated later with the **same name in the same slot** is not distinguishable without a stronger native save identifier; Journal does not invent one. Back up/remove that old character directory if you intentionally reuse both identity components.

The mod performs **no networking** and never writes journal contents to the log. The data format is local and base64-encodes fields only so multiline/unicode text can be saved safely; base64 is **not encryption**.

A `.bak` copy is maintained during successful replacements. The temporary file is flushed before replacement. If a structurally complete `.tmp` is newer than an older readable main, Journal treats it as an interrupted pre-replace save and recovers it; partial temps never pass validation. If the main file is missing or unreadable, Journal validates both `.bak` and complete `.tmp` recovery candidates and chooses the newest readable one; an unreadable main is preserved as a timestamped `.corrupt-*` copy first. Only when no candidate is readable does it open a fresh default journal.

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

**Status:** 0.1.8 is the retained-UI visual candidate. Deterministic coverage includes manual-note isolation, structured Chronicle persistence, stable-event dedupe across reload, legacy-v1 loading, per-character separation, progression significance filtering, missing optional sibling behavior, CRUD/recovery, typing ownership, launcher policy, collapse behavior, and safe drag/resize ownership. A fresh native compile, plugin-identity audit, and exact-candidate live gameplay/restart pass remain required.

Erenshor Journal now uses retained Unity uGUI for its player-facing panel and launcher, so it no longer needs Harmony patches merely to protect UI clicks. It requests only Lunaris file access and makes no network calls. Current source does reference the verified Erenshor game types used for character readiness/identity and `GameData.PlayerTyping`; it does not patch native gameplay methods.

Typing into the Journal's tab-name field or note text area also sets the real native `GameData.PlayerTyping` flag while a Journal text control has focus, so WASD/hotkeys don't fire while you're writing a note — the same mechanism Erenshor's own chat box and windows (Bank, Auction House, Guild Manager, Raid save, Quest Log) already use. Journal never clears that flag unless it verifiably owns the current assertion and no other native typing owner is still active, so it can't clobber native chat or another window's typing state.

### Opening the Journal

The normal control is the small draggable **JOURNAL** UI button. Drag it by the `◇` grip on its left edge; its position is saved. Clicking the button toggles the Journal open or closed. The button changes visual state while the Journal is open.

Erenshor Journal **does not register a global hotkey**. This is intentional so it cannot compete with Erenshor or other mods for F-keys or other gameplay bindings. Close the window with its `X` button or the same `JOURNAL` toggle.

The main retained-uGUI window can be moved by its title bar. Its normalized position is saved at drag end, invalid/legacy/off-screen values recover safely, and the configured preferred size remains in Lunaris config. If the game temporarily moves to a smaller resolution the visible panel fits that screen without overwriting the preferred size, so it can restore when space returns.

Journal now asserts Erenshor's verified `GameData.DraggingUIElement` ownership **on pointer-down** for its launcher grip, window header, and resize grip—before Unity reaches its drag threshold—so the first drag delta cannot leak into the game camera. Pointer-up, drag end, disable, destroy, close, and unload paths release Journal-owned drag/typing state safely.

## Chronicle integration API

Manual tabs and Chronicle are intentionally different data paths. **Never** append automated history to a selected note body. Existing callers can continue using the v1 API:

```csharp
ErenshorJournal.JournalApi.AddChronicleEntry(
    "My Mod",
    "Milestone",
    "Completed something the source mod already verified."
);
```

New callers that have a durable source-event identity should prefer the v2 structured API:

```csharp
ErenshorJournal.JournalApi.AddChronicleEvent(
    "contract:local:revision17:road-check",
    "My Mod",
    "Contract",
    "Completed Local Contract: Road Check",
    "Returned after an eight-minute road check. Reward: 120 XP."
);
```

`(source, eventId)` is the durable exactly-once key within that character's saved Chronicle. The event ID should identify the actual authoritative source event; do not use a display title or current timestamp as a fake identity. Legacy `AddChronicleEntry` remains contract version 1 for existing integrations, derives a useful row title from the first concise sentence of the supplied body, and retains bounded short-window duplicate suppression.

For an optional integration with no hard DLL dependency, resolve the type/method by reflection and fail closed when Journal is absent. Journal itself follows that same pattern for Crafting Expanded: it has no compile-time sibling reference, baselines current Foraging/Crafting levels after the character settles, and only a later observed **level increase** becomes a structured Chronicle event. Every raw XP tick is ignored because XP is not an input to this observer.

Do **not** use Chronicle text as proof that an event occurred. The source gameplay system/state remains authoritative.

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

Gameplay authority and integration boundaries are intentionally kept deterministic and local.

Erenshor Journal is an unofficial community mod and is not affiliated with or endorsed by Burgee Media.


## Optional Suite Hub integration

Forgotten Roads Hub is **optional**. When it is installed, this mod can expose its normal player-facing controls there through the versioned public `JournalControlApi` surface. The mod remains independently usable without the Hub and does not compile against Hub types or assume Hub load order.

Journal remains the dedicated notebook/editor. A compact standalone launcher is the safety fallback. It hides only when Suite Hub reports Ready with `uiAvailable=true`, this module bridge is registered, and the per-mod **Show launcher** setting is OFF. Missing/unavailable Hub UI forces the launcher visible for recovery; the Hub's manual interaction-validation bit is diagnostic only.

Hub can show only bounded notebook/Chronicle counts/status and open or close Journal; it does not receive character keys, tab names, note text, copied text, or Chronicle text.

The shared control/API and fully-in-world UI policy in this handoff are source-validated but **not yet live-tested under Lunaris hot reload**.

### Content/UI migration candidate

The current Journal player UI uses retained Unity uGUI. The Suite Hub bridge exposes **Show Journal Launcher** and Open/Close/Reset actions without exposing note contents, plus bounded `ui.state` metadata for centralized quick-close ordering. The standalone launcher is forced visible whenever Hub or the Journal bridge is unusable. Native compile and live Lunaris UI/reload verification remain part of the release checklist.
