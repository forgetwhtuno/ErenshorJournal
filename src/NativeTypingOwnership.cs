using UnityEngine;
using UnityEngine.EventSystems;

namespace ErenshorJournal
{
    // Positively observes whether a native (non-Journal) system currently owns
    // GameData.PlayerTyping, so JournalTypingPolicy can avoid clearing a suppression another
    // system still needs.
    //
    // Evidence (from the currently installed Assembly-CSharp.dll, inspected via Mono.Cecil):
    // every native writer of GameData.PlayerTyping also independently toggles its own
    // observable state at the same time it writes the flag:
    //   - TypeText (native chat): OpenInputBox/CloseInputBox both call
    //     InputBox.SetActive(...) immediately before writing PlayerTyping, so InputBox.activeSelf
    //     is a real, independent signal for "is chat currently accepting text".
    //   - AuctionHouseUI (ListPrice/Searchbox), BankUI (TabEdit), GuildManagerUI
    //     (NewGuildName/DELETE), RaidManager (RaidSaveName), AdjustWindowFilters (TabName) all
    //     drive real TMP_InputField/InputField components, whose focus Unity's EventSystem
    //     already tracks via currentSelectedGameObject - no invented state required.
    // This does not claim completeness beyond these verified writers: PlayerTyping remains an
    // inherently ownerless flag at the game level (these native systems don't guard against
    // clobbering each other either), so this only defends against every currently-known writer.
    internal static class NativeTypingOwnership
    {
        private static TypeText _cachedChat;

        internal static bool IsAnyNativeOwnerActive()
        {
            return IsNativeChatOpen() || IsNativeInputFieldFocused();
        }

        private static bool IsNativeChatOpen()
        {
            try
            {
                if (_cachedChat == null) _cachedChat = Object.FindObjectOfType<TypeText>();
                return _cachedChat != null && _cachedChat.InputBox != null && _cachedChat.InputBox.activeSelf;
            }
            catch { return false; }
        }

        private static bool IsNativeInputFieldFocused()
        {
            try
            {
                EventSystem events = EventSystem.current;
                GameObject selected = events == null ? null : events.currentSelectedGameObject;
                if (selected == null) return false;
                return selected.GetComponent<TMPro.TMP_InputField>() != null ||
                       selected.GetComponent<UnityEngine.UI.InputField>() != null;
            }
            catch { return false; }
        }

        // Called when Journal is destroyed/reloaded so a stale reference from a previous load
        // is never retained across a Lunaris hot reload.
        internal static void Reset()
        {
            _cachedChat = null;
        }
    }
}
