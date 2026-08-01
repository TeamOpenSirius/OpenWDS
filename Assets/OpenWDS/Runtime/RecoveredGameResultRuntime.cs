using System;
using System.Collections.Generic;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Offline subset of Combo and InputCollector recovered from WDS 2.30.3
    /// ARM64. This is the stable data source for the normal solo result panels.
    /// Score is intentionally added separately because it depends on the live
    /// unit/status chain. Achievement rate and result timing are local input
    /// aggregates and are recovered here.
    /// </summary>
    public sealed class RecoveredGameResultRuntime
    {
        private readonly HashSet<int> _duplicatedSoundNoteIds;
        private readonly Dictionary<RecoveredTimingType, int> _timingCounts =
            new Dictionary<RecoveredTimingType, int>();
        private readonly Dictionary<int, int> _inputDiffSummary =
            new Dictionary<int, int>();
        private const int InputDiffBucketMilliseconds = 5;
        private const int InputDiffMaxBucket = 25;
        private readonly int _achievementRateNoteCount;
        private int _count;
        private int _failedCount;
        private int _perfectCount;
        private int _collectedCount;

        public int Count => _count;
        public int MaxCombo { get; private set; }
        public int FailedCount => _failedCount;
        public int PerfectCount => _perfectCount;
        public int CollectedCount => _collectedCount;
        public int AchievementRateNoteCount => _achievementRateNoteCount;
        public int DuplicatedSoundNoteCount => _duplicatedSoundNoteIds.Count;
        public IReadOnlyDictionary<RecoveredTimingType, int> TimingCounts =>
            _timingCounts;
        public IReadOnlyDictionary<int, int> InputDiffSummary =>
            _inputDiffSummary;

        // Combo.get_IsFullCombo/get_IsAllPerfect compare the successful perfect
        // counter with the current combo after first rejecting any failed input.
        public bool IsFullCombo =>
            _collectedCount > 0 && _failedCount <= 0 && _perfectCount != _count;
        public bool IsAllPerfect =>
            _collectedCount > 0 && _failedCount <= 0 && _perfectCount == _count;

        // InputCollector.IsAllPerfect is stricter than Combo.IsAllPerfect: it
        // means that every collected result is PERFECT_STAR.
        public bool IsPerfectStar =>
            GetTimingCount(RecoveredTimingType.Miss) == 0 &&
            GetTimingCount(RecoveredTimingType.Bad) == 0 &&
            GetTimingCount(RecoveredTimingType.Good) == 0 &&
            GetTimingCount(RecoveredTimingType.Great) == 0 &&
            GetTimingCount(RecoveredTimingType.Perfect) == 0 &&
            GetTimingCount(RecoveredTimingType.PerfectStar) != 0;

        public double AchievementRate => CalculateAchievementRate(_timingCounts);

        public RecoveredGameResultRuntime(
            IReadOnlyList<RecoveredNotationNote> notation)
        {
            if (notation == null) throw new ArgumentNullException(nameof(notation));
            _duplicatedSoundNoteIds = FindDuplicatedSoundNoteIds(notation);
            var achievementRateNoteCount = 0;
            foreach (var note in notation)
            {
                if (note.NoteType != (int)RecoveredNoteType.None &&
                    !_duplicatedSoundNoteIds.Contains(note.Id))
                {
                    achievementRateNoteCount++;
                }
            }
            _achievementRateNoteCount = achievementRateNoteCount;
            for (var timing = RecoveredTimingType.Miss;
                 timing <= RecoveredTimingType.PerfectStar;
                 timing++)
            {
                _timingCounts[timing] = 0;
            }
            ResetInputDiffSummary();
        }

        public void Collect(in RecoveredInputResultEntity result)
        {
            if (_duplicatedSoundNoteIds.Contains(result.NoteId)) return;

            _collectedCount++;
            if (!_timingCounts.ContainsKey(result.TimingType))
                _timingCounts[result.TimingType] = 0;
            _timingCounts[result.TimingType]++;
            CollectCombo(result.TimingType);

            if (result.TimingType != RecoveredTimingType.Miss &&
                UsesInputDiffSummary(result.NoteType))
            {
                var bucket = DivideAwayFromZero(
                    result.DiffMilliseconds,
                    InputDiffBucketMilliseconds);
                if (_inputDiffSummary.ContainsKey(bucket))
                    _inputDiffSummary[bucket]++;
            }
        }

        public bool IsIgnoredDuplicateNote(int noteId) =>
            _duplicatedSoundNoteIds.Contains(noteId);

        public int[] GetScoreNoteIds(IReadOnlyList<RecoveredNotationNote> notation)
        {
            if (notation == null) throw new ArgumentNullException(nameof(notation));
            var result = new List<int>(notation.Count);
            foreach (var note in notation)
            {
                // Score.<Initialize>b__16_1 first rejects NoteType.None. Its
                // per-context predicate then rejects the duplicated Sound ID.
                if (note.NoteType == (int)RecoveredNoteType.None ||
                    _duplicatedSoundNoteIds.Contains(note.Id))
                    continue;
                result.Add(note.Id);
            }
            return result.ToArray();
        }

        public double CalculateRecommendationTiming(
            double currentRecommendationTiming,
            double noteSpeed)
        {
            return CalculateRecommendationTiming(
                _inputDiffSummary,
                currentRecommendationTiming,
                noteSpeed);
        }

        public static double CalculateAchievementRate(
            IReadOnlyDictionary<RecoveredTimingType, int> timingCounts)
        {
            if (timingCounts == null)
                throw new ArgumentNullException(nameof(timingCounts));

            long total = 0;
            long weighted = 0;
            foreach (var pair in timingCounts)
            {
                total += pair.Value;
                switch (pair.Key)
                {
                    case RecoveredTimingType.Good:
                        weighted += pair.Value * 50L;
                        break;
                    case RecoveredTimingType.Great:
                        weighted += pair.Value * 80L;
                        break;
                    case RecoveredTimingType.Perfect:
                        weighted += pair.Value * 100L;
                        break;
                    case RecoveredTimingType.PerfectStar:
                        weighted += pair.Value * 101L;
                        break;
                }
            }
            return total == 0 ? 0d : (double)weighted / total;
        }

        /// <summary>
        /// Recovers InputCollector.AchievementRate, whose denominator is the
        /// complete chart's achievement-rate note count rather than the number
        /// of results collected so far. Setting 1 is Subtract; settings 0/2
        /// use the additive accumulator (Off is hidden by the panel).
        /// </summary>
        public double GetDisplayedAchievementRate(int settingType)
        {
            return CalculateDisplayedAchievementRate(
                _timingCounts, _achievementRateNoteCount, settingType);
        }

        public static double CalculateDisplayedAchievementRate(
            IReadOnlyDictionary<RecoveredTimingType, int> timingCounts,
            int achievementRateNoteCount,
            int settingType)
        {
            if (timingCounts == null)
                throw new ArgumentNullException(nameof(timingCounts));
            if (achievementRateNoteCount < 1) return 0d;

            // InputCollector.Initialize uses a 1.01-per-note baseline in
            // Subtract mode. Each result then removes the gap from
            // PERFECT_STAR: MISS/BAD 1.01, GOOD .51, GREAT .21, PERFECT .01.
            // Add mode starts at zero and adds .50/.80/1.00/1.01.
            long hundredths;
            if (settingType == 1)
            {
                hundredths = achievementRateNoteCount * 101L;
                hundredths -= GetTimingCount(timingCounts, RecoveredTimingType.Miss) * 101L;
                hundredths -= GetTimingCount(timingCounts, RecoveredTimingType.Bad) * 101L;
                hundredths -= GetTimingCount(timingCounts, RecoveredTimingType.Good) * 51L;
                hundredths -= GetTimingCount(timingCounts, RecoveredTimingType.Great) * 21L;
                hundredths -= GetTimingCount(timingCounts, RecoveredTimingType.Perfect);
            }
            else
            {
                hundredths =
                    GetTimingCount(timingCounts, RecoveredTimingType.Good) * 50L +
                    GetTimingCount(timingCounts, RecoveredTimingType.Great) * 80L +
                    GetTimingCount(timingCounts, RecoveredTimingType.Perfect) * 100L +
                    GetTimingCount(timingCounts, RecoveredTimingType.PerfectStar) * 101L;
            }

            // Decimal.Floor((timingCount / noteCount) * 1,000,000) / 10,000.
            // The integer hundredths representation makes the same four-place
            // truncation exact and avoids binary floating-point rounding.
            var tenThousandths =
                hundredths * 10000L / achievementRateNoteCount;
            return tenThousandths / 10000d;
        }

        public static double CalculateRecommendationTiming(
            IReadOnlyDictionary<int, int> inputDiffSummary,
            double currentRecommendationTiming,
            double noteSpeed)
        {
            if (inputDiffSummary == null)
                throw new ArgumentNullException(nameof(inputDiffSummary));

            var total = 0;
            foreach (var pair in inputDiffSummary) total += pair.Value;

            var medianOptionValue = 0d;
            if (total > 0)
            {
                var orderedKeys = new List<int>(inputDiffSummary.Keys);
                orderedKeys.Sort();
                var half = total * 0.5d;
                var cumulative = 0;
                foreach (var key in orderedKeys)
                {
                    cumulative += inputDiffSummary[key];
                    if (cumulative < half) continue;

                    // The original result panel moves the selected 5 ms bucket
                    // one step toward zero, then converts milliseconds to
                    // seconds and rounds to three digits (ToEven).
                    var innerBucket = key + (key >= 1 ? -1 : 1);
                    medianOptionValue = Math.Round(
                        innerBucket * 5d / 1000d,
                        3,
                        MidpointRounding.ToEven);
                    break;
                }
            }

            var recommendation = currentRecommendationTiming +
                                 (-6d * noteSpeed * medianOptionValue);
            return Math.Max(-2d, Math.Min(2d, recommendation));
        }

        public void Reset()
        {
            _count = 0;
            MaxCombo = 0;
            _failedCount = 0;
            _perfectCount = 0;
            _collectedCount = 0;
            var keys = new List<RecoveredTimingType>(_timingCounts.Keys);
            foreach (var key in keys) _timingCounts[key] = 0;
            ResetInputDiffSummary();
        }

        private void CollectCombo(RecoveredTimingType timingType)
        {
            if (timingType == RecoveredTimingType.Great)
            {
                Increment();
                return;
            }
            if (timingType == RecoveredTimingType.Perfect ||
                timingType == RecoveredTimingType.PerfectStar)
            {
                _perfectCount++;
                Increment();
                return;
            }

            // Original Combo treats None/Miss/Bad/Good as failures.
            _failedCount++;
            _count = 0;
        }

        private void Increment()
        {
            _count++;
            MaxCombo = Math.Max(MaxCombo, _count);
        }

        private int GetTimingCount(RecoveredTimingType timingType)
        {
            return GetTimingCount(_timingCounts, timingType);
        }

        private static int GetTimingCount(
            IReadOnlyDictionary<RecoveredTimingType, int> timingCounts,
            RecoveredTimingType timingType)
        {
            return timingCounts.TryGetValue(timingType, out var count)
                ? count
                : 0;
        }

        private static int DivideAwayFromZero(long value, int divisor)
        {
            if (value == 0) return 0;
            var absolute = value < 0 ? -value : value;
            var quotient = (absolute + divisor - 1) / divisor;
            return value < 0 ? -(int)quotient : (int)quotient;
        }

        private void ResetInputDiffSummary()
        {
            _inputDiffSummary.Clear();
            // InputCollector.ctor creates Range(1, maxTapTiming / 5) twice,
            // retaining every empty positive and negative bucket. There is no
            // zero bucket, and OnCollect only increments an existing key.
            for (var bucket = 1; bucket <= InputDiffMaxBucket; bucket++)
            {
                _inputDiffSummary[bucket] = 0;
                _inputDiffSummary[-bucket] = 0;
            }
        }

        private static bool UsesInputDiffSummary(RecoveredNoteType noteType)
        {
            return noteType == RecoveredNoteType.Normal ||
                   noteType == RecoveredNoteType.Critical ||
                   noteType == RecoveredNoteType.HoldStart ||
                   noteType == RecoveredNoteType.CriticalHoldStart ||
                   noteType == RecoveredNoteType.ScratchHoldStart ||
                   noteType == RecoveredNoteType.ScratchCriticalHoldStart ||
                   noteType == RecoveredNoteType.BlueTap;
        }

        private static HashSet<int> FindDuplicatedSoundNoteIds(
            IReadOnlyList<RecoveredNotationNote> notation)
        {
            var groups = new Dictionary<(long, int), List<RecoveredNotationNote>>();
            foreach (var note in notation)
            {
                var type = (RecoveredNoteType)note.NoteType;
                if (type != RecoveredNoteType.Sound &&
                    type != RecoveredNoteType.SoundPurple &&
                    type != RecoveredNoteType.Scratch &&
                    type != RecoveredNoteType.HoldEighth)
                {
                    continue;
                }

                var key = (note.StartMilliseconds, note.Lane);
                if (!groups.TryGetValue(key, out var group))
                {
                    group = new List<RecoveredNotationNote>();
                    groups.Add(key, group);
                }
                group.Add(note);
            }

            var duplicated = new HashSet<int>();
            foreach (var group in groups.Values)
            {
                if (group.Count <= 1) continue;
                foreach (var note in group)
                {
                    var type = (RecoveredNoteType)note.NoteType;
                    if (type == RecoveredNoteType.Sound ||
                        type == RecoveredNoteType.SoundPurple ||
                        type == RecoveredNoteType.Scratch)
                    {
                        duplicated.Add(note.Id);
                        break;
                    }
                }
            }
            return duplicated;
        }
    }
}
