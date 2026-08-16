# Mod overlap / scope check

Research basis: project Erenshor ecosystem reference snapshot dated **2026-08-10**, plus a live Thunderstore/web check on **2026-08-12**.

## Result

No standalone Erenshor freeform notebook/journal/chronicle mod surfaced in targeted searches for `journal`, `notes`, or `chronicle`.

Thunderstore's current Erenshor category showed **57 packages** during the check. The closest adjacent public projects are different in purpose:

- **AdventureGuide / quest helpers:** quest guidance, GPS navigation, walkthroughs, item sources, and world markers. Journal does not duplicate these features.
- **ErenshorLLM / Deep Sims:** Sim dialogue, lore/memory, and social context. Journal does not own Sim memory or generate dialogue.
- **General QoL/UI mods:** add commands, information, or overlays, but no standalone freeform player notebook was identified in the current catalog/search.

## Deliberate non-overlap boundary

Erenshor Journal owns only:

1. player-written local notes;
2. tab organization;
3. local persistence;
4. a tiny optional append API for structured verified Chronicle events;
5. failure-closed observation of narrowly evidenced optional **level milestones** (not raw XP/gameplay control).

It does not automatically read quests, inventory, Sims, combat, PvP, guild state, navigation, or COOP state. It may observe only the current public Foraging/Crafting **level** fields from Crafting Expanded to detect a meaningful level increase after a per-character baseline; it does not read raw XP or own that progression state.

The Chronicle does not infer that an event happened. A source mod that already verified an event may append a record; that source remains authoritative.

This scope makes Journal a complementary utility rather than a replacement for an existing gameplay/guide mod.
