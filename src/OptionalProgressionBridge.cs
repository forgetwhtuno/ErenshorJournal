using System;
using System.Reflection;

namespace ErenshorJournal
{
    // Failure-closed, reflection-only observation of optional sibling-owned progression STATE.
    // Journal never takes a hard reference on Crafting Expanded and never infers raw XP events.
    // The current sibling source exposes ForagingKnowledge.IsReady/CurrentLevel and
    // CraftingControlApi.GetBasicState().SmithingLevel. We baseline their current values after a
    // character-settle delay, then record only an observed increase as a Chronicle milestone.
    internal sealed class OptionalProgressionBridge
    {
        private const float PollSeconds = 0.75f;
        private const float CharacterSettleSeconds = 1.25f;

        private readonly JournalProgressionLevelTracker _tracker = new JournalProgressionLevelTracker();
        private string _characterKey = string.Empty;
        private float _eligibleAfter;
        private float _nextPoll;
        private int _resolvedAssemblyCount = -1;

        private Type _foragingKnowledgeType;
        private PropertyInfo _foragingReady;
        private PropertyInfo _foragingLevel;
        private Type _craftingControlApiType;
        private MethodInfo _craftingBasicState;
        private FieldInfo _craftingGameplayReady;
        private FieldInfo _craftingRuntimeReady;
        private FieldInfo _craftingEnabled;
        private FieldInfo _craftingLevel;

        internal void ResetCharacter(string characterKey, float now)
        {
            _characterKey = characterKey ?? string.Empty;
            _eligibleAfter = now + CharacterSettleSeconds;
            _nextPoll = _eligibleAfter;
            _tracker.ResetCharacter(_characterKey);
        }

        internal bool Tick(string characterKey, float now, Action<JournalProgressionMilestone> onMilestone)
        {
            if (!string.Equals(characterKey ?? string.Empty, _characterKey, StringComparison.Ordinal))
                ResetCharacter(characterKey, now);
            if (string.IsNullOrWhiteSpace(_characterKey) || now < _eligibleAfter || now < _nextPoll) return false;
            _nextPoll = now + PollSeconds;

            Resolve();
            bool emitted = false;
            int level;
            JournalProgressionMilestone milestone;

            if (TryReadForagingLevel(out level) &&
                _tracker.Observe(_characterKey, "crafting.foraging", "Crafting Expanded", "Foraging", level, out milestone))
            {
                emitted = true;
                if (onMilestone != null) onMilestone(milestone);
            }

            if (TryReadCraftingLevel(out level) &&
                _tracker.Observe(_characterKey, "crafting.crafting", "Crafting Expanded", "Crafting", level, out milestone))
            {
                emitted = true;
                if (onMilestone != null) onMilestone(milestone);
            }

            return emitted;
        }

        internal void ResetForUnload()
        {
            ResetCharacter(string.Empty, 0f);
            Invalidate();
        }

        private bool TryReadForagingLevel(out int level)
        {
            level = 0;
            if (_foragingKnowledgeType == null || _foragingReady == null || _foragingLevel == null) return false;
            try
            {
                object ready = _foragingReady.GetValue(null, null);
                if (!(ready is bool) || !(bool)ready) return false;
                object current = _foragingLevel.GetValue(null, null);
                if (!(current is int)) return false;
                level = (int)current;
                return level >= 1;
            }
            catch
            {
                Invalidate();
                return false;
            }
        }

        private bool TryReadCraftingLevel(out int level)
        {
            level = 0;
            if (_craftingControlApiType == null || _craftingBasicState == null) return false;
            try
            {
                object state = _craftingBasicState.Invoke(null, null);
                if (state == null || _craftingGameplayReady == null || _craftingRuntimeReady == null ||
                    _craftingEnabled == null || _craftingLevel == null) return false;
                if (!(bool)_craftingGameplayReady.GetValue(state)) return false;
                if (!(bool)_craftingRuntimeReady.GetValue(state)) return false;
                if (!(bool)_craftingEnabled.GetValue(state)) return false;
                object current = _craftingLevel.GetValue(state);
                if (!(current is int)) return false;
                level = (int)current;
                return level >= 1;
            }
            catch
            {
                Invalidate();
                return false;
            }
        }

        private void Resolve()
        {
            Assembly[] assemblies;
            try { assemblies = AppDomain.CurrentDomain.GetAssemblies(); }
            catch { return; }
            if (_resolvedAssemblyCount == assemblies.Length) return;

            _resolvedAssemblyCount = assemblies.Length;
            _foragingKnowledgeType = null;
            _foragingReady = null;
            _foragingLevel = null;
            _craftingControlApiType = null;
            _craftingBasicState = null;
            _craftingGameplayReady = null;
            _craftingRuntimeReady = null;
            _craftingEnabled = null;
            _craftingLevel = null;

            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null) continue;
                if (_foragingKnowledgeType == null)
                {
                    try { _foragingKnowledgeType = assembly.GetType("ErenshorCraftingExpanded.ForagingKnowledge", false); }
                    catch { _foragingKnowledgeType = null; }
                }
                if (_craftingControlApiType == null)
                {
                    try { _craftingControlApiType = assembly.GetType("ErenshorCraftingExpanded.CraftingControlApi", false); }
                    catch { _craftingControlApiType = null; }
                }
            }

            const BindingFlags publicStatic = BindingFlags.Public | BindingFlags.Static;
            if (_foragingKnowledgeType != null)
            {
                _foragingReady = _foragingKnowledgeType.GetProperty("IsReady", publicStatic);
                _foragingLevel = _foragingKnowledgeType.GetProperty("CurrentLevel", publicStatic);
                if (_foragingReady == null || _foragingReady.PropertyType != typeof(bool) ||
                    _foragingLevel == null || _foragingLevel.PropertyType != typeof(int))
                {
                    _foragingKnowledgeType = null;
                    _foragingReady = null;
                    _foragingLevel = null;
                }
            }

            if (_craftingControlApiType != null)
            {
                _craftingBasicState = _craftingControlApiType.GetMethod("GetBasicState", publicStatic, null, Type.EmptyTypes, null);
                Type stateType = _craftingBasicState == null ? null : _craftingBasicState.ReturnType;
                if (stateType != null)
                {
                    const BindingFlags publicInstance = BindingFlags.Public | BindingFlags.Instance;
                    _craftingGameplayReady = stateType.GetField("GameplayReady", publicInstance);
                    _craftingRuntimeReady = stateType.GetField("RuntimeReady", publicInstance);
                    _craftingEnabled = stateType.GetField("Enabled", publicInstance);
                    _craftingLevel = stateType.GetField("SmithingLevel", publicInstance);
                }
                if (_craftingBasicState == null || _craftingGameplayReady == null || _craftingRuntimeReady == null ||
                    _craftingEnabled == null || _craftingLevel == null ||
                    _craftingGameplayReady.FieldType != typeof(bool) || _craftingRuntimeReady.FieldType != typeof(bool) ||
                    _craftingEnabled.FieldType != typeof(bool) || _craftingLevel.FieldType != typeof(int))
                {
                    _craftingControlApiType = null;
                    _craftingBasicState = null;
                    _craftingGameplayReady = null;
                    _craftingRuntimeReady = null;
                    _craftingEnabled = null;
                    _craftingLevel = null;
                }
            }
        }

        private void Invalidate()
        {
            _resolvedAssemblyCount = -1;
            _foragingKnowledgeType = null;
            _foragingReady = null;
            _foragingLevel = null;
            _craftingControlApiType = null;
            _craftingBasicState = null;
            _craftingGameplayReady = null;
            _craftingRuntimeReady = null;
            _craftingEnabled = null;
            _craftingLevel = null;
        }
    }
}
