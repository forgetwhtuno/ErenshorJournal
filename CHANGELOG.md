# Changelog

## Unreleased (native Lunaris migration)

- Converted the plugin host from BepInEx (`BaseUnityPlugin`/`[BepInPlugin]`/`[BepInProcess]`) to
  native Lunaris (`LunarisPlugin`/`[LunarisPlugin]`/`[LunarisPermission(FileAccess | Harmony)]`).
  Still no Reflection or Network permission — this mod uses no reflection and makes no network
  calls. There is no chat-command interception in this mod (UI-button-only, no global hotkey), so
  nothing here changes command syntax.
- Config replaced `ConfigEntry<T>`/`Config.Bind` with native typed Lunaris config
  (`JournalSettings`); all 6 existing settings (section/key/default/description) preserved
  unchanged behind a loader-neutral `JournalConfigEntry<T>` shim.
- Logging replaced `BepInEx.Logging`/`ManualLogSource` with native Lunaris `Logging`.
- Local journal storage moved from `BepInEx/config/ErenshorJournal/` to
  `plugins/config/ErenshorJournal/` (`Paths.ConfigPath` was BepInEx-specific).
- `BUILD_AND_INSTALL.ps1`/`UNINSTALL.ps1` now target `<Erenshor>\plugins` instead of a BepInEx
  profile and no longer require `BepInEx.dll`.
- **Added a narrow Harmony dependency, solely to stop Journal's IMGUI panel from leaking clicks
  through to world target/camera controls.** Erenshor reads `PlayerControl.LeftClick` and
  `csMouseOrbit.LateUpdate` from raw `Input.mousePosition`, bypassing whatever IMGUI already
  consumed, so a click on the Journal window or launcher button could otherwise also drop the
  player's current target or spin the camera. Two prefixes now guard against that:
  `PlayerControl.LeftClick` and `csMouseOrbit.LateUpdate`. This is the only patch surface Journal
  has; no other game method is touched.
- Typing into the tab-name field or note text area now suppresses native movement/hotkeys by
  setting the real native `GameData.PlayerTyping` flag while a Journal text control has focus —
  the same flag Erenshor's own chat box and windows (Bank, Auction House, Guild Manager, Raid
  save, Quest Log) use for the same purpose. Because `PlayerTyping` is a bare shared static bool
  with no ownership encoded in it (confirmed by inspecting every native writer in the currently
  installed `Assembly-CSharp.dll`), Journal never clears it unless it verifiably owns the current
  assertion and no other native typing owner (native chat's input box, or a focused
  `TMP_InputField`/`InputField` from Bank/Auction House/Guild Manager/Raid save/tab-rename) is
  still active. See `src/JournalTypingPolicy.cs` (pure, Unity-independent decision logic) and
  `src/NativeTypingOwnership.cs` (the actual in-game observation).
- Deterministic test suite grew from 5 to 13 assertions (5 journal-core + 8 typing-policy) to
  cover the `PlayerTyping` ownership handling, including the exact scenario that motivated it:
  Journal losing text focus while a native owner is still active must never clear the flag, and
  a later external writer clearing the flag while Journal is still focused must be corrected on
  the next frame.

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


## Unreleased - Suite UI/API coherence handoff

- Added optional, versioned `JournalControlApi` discovery/control surface for Suite Hub without a hard Hub dependency.
- Kept standalone commands and core gameplay authority intact.
- Documented the retained panel/launcher policy and Lunaris live-test requirement.
- Strengthened fully-in-world gating, made the launcher Hub-aware fallback-only, deferred launcher/close state transitions out of `OnGUI`, and added panel Reset Position / whole-drag input capture while preserving native typing ownership.
