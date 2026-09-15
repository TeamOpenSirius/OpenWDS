using System;
using System.Collections.Generic;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Solo/default-life subset of Sirius.Game.Life and DefaultLifeCalculator.
    /// Decrement eligibility and values match ARM64 RVAs 0xB986318/0xB986324.
    /// Party-specific guards and skill healing remain outside this boundary.
    /// </summary>
    public sealed class LifeRuntime
    {
        public const int DefaultValue = 1000;

        public int Value { get; private set; }
        public int MaxValue { get; }
        public int GuardCount { get; private set; }

        public LifeRuntime(
            int value = DefaultValue,
            int maxValue = DefaultValue,
            int guardCount = 0)
        {
            if (maxValue <= 0) throw new ArgumentOutOfRangeException(nameof(maxValue));
            Value = Math.Max(0, value);
            MaxValue = maxValue;
            GuardCount = Math.Max(0, guardCount);
        }

        public bool Process(IReadOnlyList<InputResultEntity> results)
        {
            if (results == null) return false;
            var changed = false;
            for (var index = 0; index < results.Count; index++)
            {
                var decrement = CalculateDecrement(
                    results[index].NoteType, results[index].TimingType);
                if (decrement <= 0) continue;
                if (GuardCount > 0)
                {
                    GuardCount--;
                }
                else
                {
                    Value = Math.Max(0, Value - decrement);
                }
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Sirius.Game.Life.Add (ARM64 RVA 0xB986CF0): apply the delta and clamp
        /// only the lower bound. Start effects may therefore exceed MaxValue.
        /// </summary>
        public bool Add(int amount)
        {
            if (amount == 0) return false;
            var next = Math.Max(0, Value + amount);
            if (next == Value) return false;
            Value = next;
            return true;
        }

        public static int CalculateDecrement(
            NoteType noteType,
            TimingType timingType)
        {
            if (timingType != TimingType.Miss &&
                timingType != TimingType.Bad)
                return 0;

            if (timingType == TimingType.Bad)
            {
                if (noteType == NoteType.Sound ||
                    noteType == NoteType.SoundPurple ||
                    noteType == NoteType.Hold ||
                    noteType == NoteType.CriticalHold ||
                    noteType == NoteType.HoldEighth)
                    return 0;
                return 50;
            }

            if (noteType == NoteType.Sound ||
                noteType == NoteType.SoundPurple ||
                noteType == NoteType.HoldEighth)
                return 30;
            if (noteType == NoteType.Hold ||
                noteType == NoteType.CriticalHold ||
                noteType == NoteType.ScratchHold)
                return 50;
            return 80;
        }
    }
}
