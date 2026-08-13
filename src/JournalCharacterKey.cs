using System.Linq;

namespace ErenshorJournal
{
    // Pure, Unity-free identity composition, deliberately split out from JournalCharacterIdentity.cs
    // (which reads live GameData) so this logic can be exercised by a deterministic test without the
    // game or Lunaris assemblies - same split as JournalTypingPolicy.cs / NativeTypingOwnership.cs.
    //
    // Erenshor does not appear to expose a save identifier stronger than "recorded save-slot index +
    // character name" (checked against the SaveGameData/GameData surface in the installed
    // Assembly-CSharp.dll); this key is therefore a best-effort stable identity, not a
    // guaranteed-unique save id. See JournalCharacterIdentity.ResolveCharacterKey for how the inputs
    // are actually resolved from the live game, mirroring the pattern already live-tested in
    // Erenshor-Nemesis's NemesisDirector (ResolveCharacterKey/ResolveSlotIndex/SafeKey).
    internal static class JournalCharacterKey
    {
        internal static string Compose(string name, int slot)
        {
            return slot >= 0 ? "slot" + slot.ToString() + "_" + SafeKey(name) : SafeKey(name);
        }

        internal static string SafeKey(string value)
        {
            string source = string.IsNullOrEmpty(value) ? "player" : value;
            return new string(source.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').Take(48).ToArray());
        }
    }
}
