# Changelog

## 0.1.2 - UI consistency pass

- removed the global keyboard-toggle path entirely; Journal now opens/closes from UI only;
- restyled the launcher and main window using the same framed cyan/teal visual language as Erenshor Follow / Party Tools;
- added explicit hover/active states and an open-state launcher style;
- made the Journal window resizable from a lower-right grip and retained saved size/position;
- moved the default launcher toward the upper-right HUD area rather than screen center;
- kept launcher and window clamped on-screen across resolution changes;
- removed the now-unused `UnityEngine.InputLegacyModule` build reference;
- retained local-only persistence, Chronicle API, and zero Harmony / `Assembly-CSharp.dll` dependency.

## 0.1.1 - 2026-08-12

- Replaced the default F8 launcher with a persistent draggable `Journal` UI button.
- The launcher toggles between `Journal` and `Close Journal` and remembers its screen position.
- Global `ToggleKey` now defaults to `None`; keyboard control is opt-in only.
- Escape still closes the Journal only while the Journal is already open.
- No new Harmony or `Assembly-CSharp.dll` dependency was introduced.


## 0.1.0 - 2026-08-12

Initial preview release.

- Standalone F8 journal window.
- Player-owned freeform tabs with add, rename, delete, timestamp, and copy controls.
- Local autosave under `BepInEx/config/ErenshorJournal/` with atomic replacement and backup recovery.
- Read-only-style Chronicle view for verified entries appended by compatible mods.
- Tiny reflection-friendly `JournalApi.AddChronicleEntry(source, category, text)` integration surface.
- No Harmony patches, no `Assembly-CSharp.dll` dependency, no networking, no AI, and no automatic quest inference.
