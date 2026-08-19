using System;

namespace ErenshorJournal
{
    // Unity-free geometry helpers for the retained Journal UI.
    internal static class JournalUiLayoutPolicy
    {
        internal const float ToolbarGap = 4f;
        internal const float RightMargin = 6f;

        internal const float DeleteWidth = 64f;
        internal const float DeleteRight = -RightMargin;

        internal const float CopyWidth = 50f;
        internal const float CopyRight = DeleteRight - DeleteWidth - ToolbarGap;

        internal const float NewEntryWidth = 76f;
        internal const float NewEntryRight = CopyRight - CopyWidth - ToolbarGap;

        internal const float NameInputRightInset = 210f;

        internal static bool MainToolbarDoesNotOverlap()
        {
            float deleteLeft = DeleteRight - DeleteWidth;
            float copyLeft = CopyRight - CopyWidth;
            float newEntryLeft = NewEntryRight - NewEntryWidth;
            float nameRight = -NameInputRightInset;
            return CopyRight <= deleteLeft
                && NewEntryRight <= copyLeft
                && nameRight <= newEntryLeft;
        }

        internal static float TabWidthForName(string value)
        {
            string clean = string.IsNullOrEmpty(value) ? "Untitled" : value;
            float width = 34f + (clean.Length * 7f);
            return Math.Max(92f, Math.Min(168f, width));
        }

        internal static float ResolvePanelExtent(float preferred, float screenExtent, float minimum, float margin)
        {
            if (float.IsNaN(preferred) || float.IsInfinity(preferred)) preferred = minimum;
            if (float.IsNaN(screenExtent) || float.IsInfinity(screenExtent)) screenExtent = minimum + (margin * 2f);
            float safePreferred = Math.Max(minimum, preferred);
            float available = Math.Max(1f, screenExtent - (Math.Max(0f, margin) * 2f));
            return Math.Min(safePreferred, available);
        }

        // Pure point-anchor rect math (anchorMin == anchorMax on both axes - no stretch). This is
        // the launcher and drag-grip anchor mode since 0.1.9: sizeDelta is the element's absolute
        // size, and anchoredPosition places its pivot point at that offset from the parent's
        // bottom-left corner. Kept Unity-free so the launcher's drag-target geometry contract (a
        // dedicated grip/accent area must fit entirely inside the launcher bounds) can be proven by
        // computation instead of only by source-text matching, since UnityEngine.RectTransform is
        // not available to this standalone test binary.
        internal static bool RectFitsWithinParent(float parentWidth, float parentHeight,
            float childX, float childY, float childWidth, float childHeight, float pivotX, float pivotY)
        {
            if (childWidth < 0f || childHeight < 0f) return false;
            float left = childX - (childWidth * pivotX);
            float right = left + childWidth;
            float bottom = childY - (childHeight * pivotY);
            float top = bottom + childHeight;
            const float epsilon = 0.01f;
            return left >= -epsilon && bottom >= -epsilon && right <= parentWidth + epsilon && top <= parentHeight + epsilon;
        }
    }
}
