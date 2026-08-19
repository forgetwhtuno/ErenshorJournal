# Changelog

## 0.1.11 - UI workspace normalization

- Removed the text-only "[OPEN]" suffix from the JOURNAL launcher label (it now always reads
  "JOURNAL"). Open/active state is instead a small structural cue shared with the Follow/Duel
  standalone launchers: a filled accent bar along the launcher's top edge, present only while the
  panel is open - not a color-only signal.
- New default (and Reset) window position: the window's top-right corner lines up with the shared
  right-side workspace anchor below the launcher column, instead of the old dead-center 0.5/0.5
  default. An existing saved position and size are never touched by this - only the default used
  when no valid saved position exists, and the Reset target, changed. Window size (720x560 default)
  is unchanged; it is a legitimate content-appropriate size for a full note/chronicle editor, not
  something this pass judged as oversized.
- Fixed the shared launcher-column right margin: the previous 0.006 normalized value was smaller
  than the launcher's own 154px width at every realistic screen resolution, so the position-resolve
  clamp silently erased it, leaving the launcher flush against the very edge of the screen. It now
  resolves to a real, small, safe margin instead.
- Reordered the shared launcher column to Journal(0) / Duel(1) / Follow(2), top to bottom.
- Added `StandaloneLauncherColumnPolicy.DefaultPanelTopNormalized()`/`DefaultPanelRightNormalized()`
  (mirrored across Journal/Follow/Duel's copies) so all three modules' default utility-panel
  workspace shares one coherent anchor below the launcher rail instead of three unrelated screen
  locations.

## 0.1.10 - shared standalone-launcher visual/placement pass

- New default launcher position: a vertical right-side column beneath the native minimap area,
  replacing the old `(0.86, 0.82)` default so Journal, Follow, and Duel launchers occupy fixed,
  non-overlapping slots (Journal is slot 0, the topmost) instead of drifting toward the same
  lower-right area independently. No stable minimap `RectTransform` exists in the installed assembly
  to derive an exact lower edge from, so the column uses a resolution-independent top-right anchor
  with a conservative fixed inset.
- Any existing saved launcher position is preserved exactly - only installs that have never moved the
  launcher (still at the `Unset` sentinel) pick up the new default.
- Added `src/StandaloneLauncherColumnPolicy.cs`, the canonical per-module copied placement policy also
  used by Follow and Duel's shared fallback launcher, which now matches Journal's launcher chrome.

## 0.1.9 - standalone launcher grip repair

**Fixed**
- The standalone launcher's left drag grip used a vertically stretched anchor (`anchorMin.y=0`,
  `anchorMax.y=1`) together with `StandaloneLauncherVisual.StyleGrip`, which sets `sizeDelta =
  (GripWidth, Height)` as an absolute size. On a stretched anchor, `sizeDelta.y` is an *additive*
  offset on top of the already-parent-matched height instead, so the grip's real height doubled to
  64px and, with `pivot.y = 0`, the extra 32px rendered entirely above the visible launcher as an
  oversized cyan strip with an equally oversized invisible pointer/raycast target. Dragging from
  that region behaved inconsistently because the effective hit area no longer matched the visible
  launcher bounds.
- `JournalLauncher` now builds the grip with fixed (non-stretch) anchors via
  `RetainedUiKit.AnchorBottomLeft`, matching the sizing contract `StyleGrip` already assumed. The
  grip is now exactly `20x32`, fully inside the launcher's own `154x32` bounds, with no strip
  projecting above or below it. Drag behavior, pointer ownership, position clamping/persistence,
  and Journal note/Chronicle behavior are unchanged.

## 0.1.8 - first public release

**Added**
- Standalone retained-uGUI launcher with programmatic grip marks and the shared Forgotten Roads hover/pressed colors.
- Collapsible Journal header: collapsed Journal keeps only its draggable header plus Reset and Close, with the body and resize grip disabled until expanded.

**Changed**
- Standardized compact header naming while preserving drag, collapse, reset, close, position persistence, and Suite fallback behavior.
- Chronicle persistence, per-character separation, and camera-gesture ownership are unchanged.

**Fixed**
- A persistent per-frame update failure now reports one bounded summary at most every 30 seconds instead of one log line per frame.
- The project file now references `UnityEngine.UIModule` and `UnityEngine.IMGUIModule`, so `ErenshorJournal.csproj` builds from a clean checkout.

**Compatibility**
- Built against the currently installed Erenshor `Assembly-CSharp.dll` and Lunaris 0.1.0.
- Fully standalone. Crafting Expanded, Suite Hub, and every other Forgotten Roads mod are optional; the progression bridge is reflection-only and fails closed when the sibling is absent.
- Notes are stored per character under `plugins/config/ErenshorJournal/Characters/<character>/`. Existing notes and legacy global journals are preserved and migrated once, never deleted.
- Supports Lunaris runtime enable/disable/reload.

**Known limitations**
- Lunaris lists the plugin under its stable identifier `forgetwhtuno.erenshor.journal` rather than a friendly display name. That identifier also names the config file, so it is deliberately left unchanged.

## 0.1.7 - RC camera containment

- Added a fail-closed, monotonic postfix for the verified current `CameraController.UsingUI()` boundary. Journal raises native UI state only during an owned left-button move/resize gesture.
- Unified move/resize ownership through the suite process registry, preserving the native pre-gesture value and never clearing a sibling owner.
- Added physical-button, focus, pause, disable, destroy, readiness, close, zone, and unload cleanup plus deterministic source-contract guards.

## Pre-release development - bounded Suite UI polish

- Aligned the retained Journal/launcher palette mechanically with the canonical dark/translucent/cyan Sim Actions tokens and added a thin cyan frame.
- Added a consistent `▾` / `▸` header collapse control. Collapsed Journal keeps only the draggable 32px header plus Reset/Close; body and resize grip are disabled until expanded.
- Collapse/expand preserves the header's screen position and clamps both states on-screen; expanded dragging keeps the existing normalized position persistence.
- Kept normal note/Chronicle dynamic updates retained in place; collapse does not introduce a per-frame UI rebuild.
- Extended Unity-free Suite UI policy tests for compact geometry, collapsed/expanded heights, top-edge preservation, containment clamp, and structural-vs-dynamic rebuild behavior.

## 0.1.6 - structured Chronicle / progression workflow

- Separated automated history from manual note bodies as an explicit invariant: normal tabs are mutated only by player editing/manual **Add Time**; Chronicle integrations only append structured Chronicle records.
- Renamed the ambiguous manual `New Entry` button to **Add Time** so its real behavior—adding a timestamp marker inside the selected player note—is clear.
- Added structured Chronicle v2 fields (`EventId`, `Title`) and backward-compatible V1/V2 persistence. Existing Chronicle rows load unchanged and derive a useful display title from the first concise sentence of their existing body, with category/source retained as provenance.
- Added `JournalApi.AddChronicleEvent(eventId, source, category, title, text)` while preserving v1 `ContractVersion = 1` / `AddChronicleEntry(...)` for current sibling integrations. Stable `(source,eventId)` keys dedupe across callbacks and save/reload.
- Added a failure-closed, reflection-only optional Crafting Expanded progression observer. It baselines current Foraging/Crafting levels per character and creates Chronicle entries only for later level increases; raw XP values are never read, preventing XP-tick spam.
- Reworked Chronicle rows into distinct selectable retained-uGUI entries with readable local timestamp, title, source/category, summary, selected-row styling, and structured Copy output.
- Fixed Journal-owned drag containment to assert `GameData.DraggingUIElement` on pointer-down (before Unity drag threshold) for window/launcher/resize surfaces; pointer-up/end/disable/destroy/close/unload release ownership safely.
- Closing Journal now releases Journal-owned native `PlayerTyping` immediately instead of waiting for the next Update tick.
- Expanded deterministic tests for manual-note isolation, stable-event dedupe, reload/persistence, V1 compatibility, character separation, progression significance, missing optional sibling behavior, and gesture cleanup/source invariants.

## 0.1.5 - deep playable-state pass

- Hardened local persistence: temp data is flushed before replacement, header-only/truncated files fail closed, and recovery chooses the newest validated `.bak`/complete `.tmp` candidate when the live file is missing or unreadable; a structurally complete interrupted-save `.tmp` can also recover the pre-replace window where an older readable main still exists.
- Legacy global-note migration now acquires and durably flushes its one-time claim marker **before** copying private notes; an interrupted migration therefore fails closed instead of allowing a later character to inherit the same legacy data.
- Normalization now repairs blank/duplicate tab IDs without losing readable note content, keeping retained tab binding unambiguous.
- Exact duplicate Chronicle records already present on disk are collapsed on load while the existing short-window admission dedupe remains intact for new integrations.
- Chronicle integration application is capped per frame and remains character-bound, preventing a burst of optional events from monopolizing one Unity update.
- Added **New Entry** as a fast timestamped note separator that returns focus to the note body; renamed the old Timestamp button accordingly.
- Long tab names receive bounded adaptive widths in the horizontal tab strip instead of being forced into one narrow slot.
- Temporary low resolutions fit the visible panel without persisting the shrunken size, allowing the configured preferred dimensions to restore when the screen grows again.
- Local control state no longer populates character keys or selected tab names; Suite status remains count-only.
- Expanded deterministic persistence coverage for missing/empty/truncated data, backup recovery, interrupted-save temp recovery, duplicate IDs/Chronicle rows, large Unicode notes, and atomic previous-version backups.
- Search was deliberately not added: the current product is one editable freeform page per tab, and introducing a second results/selection mode would add focus/lifecycle complexity disproportionate to this small notebook.

## 0.1.4 - playable-state / release-readiness

- Bound queued Chronicle integration entries to the active character so a logout or character switch cannot leak pending history into another notebook.
- Save dirty Journal/Chronicle state before a not-ready/logout transition drops the active character store, including when the panel is already closed.
- Reject duplicate plugin initialization so an abnormal double-start cannot create duplicate retained UI or lifecycle ownership.
- Bounded Chronicle source/category/text payloads and added short-window exact duplicate suppression.
- Made individual malformed tab/Chronicle records recoverable without discarding otherwise readable local notes; fully unreadable files are still preserved as `.corrupt-*`.
- Removed character keys, local paths, and exception-message detail from normal runtime logging; Journal contents are never logged.
- Disabled Delete when only one tab remains and disabled Clear when Chronicle is empty; both destructive actions keep visible confirmation when applicable.
- Reworded Chronicle empty/help text for players instead of exposing integration API terminology.
- Added retained-panel screen fitting for small resolutions and lowered the normal minimum panel size to 440x320 while keeping resize/drag persistence.
- Added tests for Chronicle dedupe/bounds, selected-tab persistence, CRUD editing, and partial malformed-record recovery.

## Pre-release development - retained uGUI / content-utility polish

- Fixed the top action row so Timestamp / Copy / Delete no longer overlap; Copy has a dedicated non-clipped slot.
- Empty editable tabs now show the understated placeholder `No entries.` instead of an unexplained blank region.
- Launcher suppression now keys off Hub `Ready + uiAvailable` capability rather than the manual interaction-validation bit, while still failing safe to visible when Hub/bridge access is unavailable.
- Current plugin host is native Lunaris with `LunarisPermission.FileAccess`; the retained-uGUI/TMP panel and launcher require no Harmony click/camera patches.
- Current source references verified Erenshor types only for character readiness/identity and native `GameData.PlayerTyping` ownership; it makes no network calls and does not patch gameplay methods.
- Tightened tab/Chronicle spacing while preserving editing, tabs, Copy, Chronicle, resize/drag, typing ownership, and persistence behavior.
- Corrected stale testing/migration text that still described the superseded IMGUI/Harmony intermediate build.
- `closePanel` remains available through the Suite Aura action and `ui.state` now reports open/closeable/sort-order/activation state for the centralized quick-close contract; no independent Escape handler was added.

## Pre-release development (native Lunaris migration history)

- Converted the plugin host from BepInEx to native Lunaris and moved local data under `plugins/config/ErenshorJournal/`.
- Replaced BepInEx config/logging with native Lunaris config/logging.
- The migration briefly used IMGUI/Harmony containment; that intermediate implementation is superseded by the retained-uGUI source described above.
- Preserved the verified `GameData.PlayerTyping` ownership policy for Journal text inputs.

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


## Pre-release development - Suite UI/API coherence handoff

- Added optional, versioned `JournalControlApi` discovery/control surface for Suite Hub without a hard Hub dependency.
- Kept standalone commands and core gameplay authority intact.
- Documented the retained panel/launcher policy and Lunaris live-test requirement.
- Strengthened fully-in-world gating, made the launcher Hub-aware fallback-only, deferred launcher/close state transitions out of `OnGUI`, and added panel Reset Position / whole-drag input capture while preserving native typing ownership.
