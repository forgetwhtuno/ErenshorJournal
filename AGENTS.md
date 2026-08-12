# AGENTS.md — Erenshor Journal

Instructions for AI/coding agents working in this repository. Read this before making changes.

## What this mod is

A small local notebook for Erenshor (BepInEx 5 plugin, .NET Framework 4.8, C# 5 effective language level via `csc`). Freeform, player-owned, tabbed text pages with local autosave, plus an optional Chronicle view that other mods can append verified events to.

## Core design boundary

- The Journal is player-owned text. It is not a quest log, not a walkthrough, and does not infer that a quest or event happened.
- **No networking.** Notes never leave the machine, and journal contents are never written to the BepInEx log.
- No Harmony patches, no `Assembly-CSharp.dll` reference. Keep the patch-maintenance surface at zero — that's the entire point of this mod being small.
- The Chronicle only records what a caller already verified via `JournalApi.AddChronicleEntry(...)`. It must never be used, or documented, as proof that something happened — the source mod remains authoritative.
- Data format base64-encodes fields so multiline/unicode text survives round-tripping. Base64 is **not encryption** — do not describe it as one, and do not add real encryption without the user explicitly asking (that's a scope change, not a bug fix).

## Forbidden

- Do not add networking, telemetry, or any outbound call of any kind.
- Do not add a Harmony patch or an `Assembly-CSharp.dll` reference.
- Do not add a global hotkey by default — this mod deliberately ships with `ToggleKey = None` so it never competes for F-keys. UI-only open/close is intentional.
- Do not invent Erenshor APIs that haven't been verified.
- Do not commit `bin/`, `obj/`, `refs/`, compiled DLLs, game assemblies, or anything under a live BepInEx/Erenshor install path. `.gitignore` already covers the standard cases.
- No secrets, personal file paths, tokens, or real names in source, docs, or commit messages.
- Do not commit or push changes unrelated to the task at hand.

## Important source files

- `src/JournalStore.cs` — local persistence under `BepInEx/config/ErenshorJournal/journal.dat`, atomic replace, `.bak` and `.corrupt-*` recovery. Be careful here: never destroy an unreadable file, preserve it as a timestamped corrupt copy.
- `src/JournalWindow.cs`, `src/JournalLauncher.cs` — UI (draggable/resizable window, draggable launcher button, saved position/size).
- `src/JournalApi.cs` — the public `AddChronicleEntry(source, category, text)` surface for other mods.
- `src/JournalModels.cs` — data shapes.
- `src/ErenshorJournalPlugin.cs` — BepInEx plugin entry point.

## Build / test procedure

- Deterministic core tests: `powershell -ExecutionPolicy Bypass -File .\RUN_TESTS.ps1` (standalone `csc` compile + run, no game/BepInEx dependency).
- Full plugin build: `powershell -ExecutionPolicy Bypass -File .\BUILD_AND_INSTALL.ps1` — locates the current Erenshor/BepInEx install and **installs over the live plugin folder**. Don't use it as a plain compile check.
- The shipped build compiles with the legacy .NET Framework `csc.exe` (effectively C# 5) despite the `.csproj` claiming `LangVersion 7.3`. Avoid string interpolation, `nameof`, null-conditional operators, expression-bodied members, and inline `out` variables.
- Compile and run the deterministic tests before claiming a change works.

## Compatibility boundaries

- Requires nothing else — Journal must keep working standalone with no other mod installed.
- Other mods (Contracts, Guild Life, Deep Sims, etc.) may optionally call the Chronicle API through reflection with no hard DLL dependency; never flip that to a required reference.
- Does not own quest guidance, Sim memory/dialogue, crafting state, or PvP/duel match state — those belong to other mods.
