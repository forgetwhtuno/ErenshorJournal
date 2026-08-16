using System;
using System.Collections.Generic;

namespace ErenshorJournal
{
    internal sealed class JournalProgressionMilestone
    {
        internal string EventId = string.Empty;
        internal string Source = string.Empty;
        internal string Category = string.Empty;
        internal string Title = string.Empty;
        internal string Text = string.Empty;
    }

    // Pure, Unity-free significance policy. It deliberately observes LEVELS only. Raw XP values are
    // not inputs, so ordinary +XP ticks cannot create Chronicle spam through this path.
    internal sealed class JournalProgressionLevelTracker
    {
        private string _characterKey = string.Empty;
        private readonly Dictionary<string, int> _levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        internal void ResetCharacter(string characterKey)
        {
            _characterKey = characterKey ?? string.Empty;
            _levels.Clear();
        }

        internal bool Observe(string characterKey, string sourceId, string sourceLabel, string skillLabel, int level,
            out JournalProgressionMilestone milestone)
        {
            milestone = null;
            if (string.IsNullOrWhiteSpace(characterKey) || !string.Equals(characterKey, _characterKey, StringComparison.Ordinal))
            {
                ResetCharacter(characterKey);
            }
            if (string.IsNullOrWhiteSpace(_characterKey) || string.IsNullOrWhiteSpace(sourceId) || level < 1) return false;

            int previous;
            if (!_levels.TryGetValue(sourceId, out previous))
            {
                // First sight of an already-existing level is a baseline, not proof that a level-up
                // occurred now. This avoids manufacturing history at plugin startup/login.
                _levels[sourceId] = level;
                return false;
            }

            if (level <= previous)
            {
                // A lower value is a provider/character/reset boundary, not a negative progression
                // event. Re-baseline silently and fail closed.
                if (level < previous) _levels[sourceId] = level;
                return false;
            }

            _levels[sourceId] = level;
            string cleanSkill = string.IsNullOrWhiteSpace(skillLabel) ? "Progression" : skillLabel.Trim();
            string cleanSource = string.IsNullOrWhiteSpace(sourceLabel) ? "Progression" : sourceLabel.Trim();

            milestone = new JournalProgressionMilestone();
            milestone.EventId = sourceId + ".level." + level.ToString();
            milestone.Source = cleanSource;
            milestone.Category = "Progression";
            milestone.Title = cleanSkill + " reached level " + level.ToString();
            milestone.Text = previous + 1 == level
                ? cleanSkill + " increased from level " + previous.ToString() + " to level " + level.ToString() + "."
                : cleanSkill + " reached level " + level.ToString() + " (previous observed level " + previous.ToString() + ").";
            return true;
        }
    }
}
