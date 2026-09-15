using System;
using System.Collections.Generic;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Explicit player-unit boundary required by Score.Initialize. The offline
    /// music fixture deliberately does not manufacture these values.
    /// </summary>
    public sealed class SoloScoreContext
    {
        private readonly HashSet<int> _noteIds;

        public int Order { get; }
        public double DifficultyAutoCoefficient { get; }
        public int TotalStatus { get; }
        public int BaseScorePercentage { get; }
        public int TotalNotesCount { get; }

        public SoloScoreContext(
            int order,
            double difficultyAutoCoefficient,
            int totalStatus,
            int baseScorePercentage,
            IReadOnlyCollection<int> noteIds)
        {
            if (difficultyAutoCoefficient < 0d)
                throw new ArgumentOutOfRangeException(nameof(difficultyAutoCoefficient));
            if (totalStatus < 0)
                throw new ArgumentOutOfRangeException(nameof(totalStatus));
            if (noteIds == null || noteIds.Count == 0)
                throw new ArgumentException("ScoreContext requires its filtered notation IDs.", nameof(noteIds));
            Order = order;
            DifficultyAutoCoefficient = difficultyAutoCoefficient;
            TotalStatus = totalStatus;
            BaseScorePercentage = baseScorePercentage;
            _noteIds = new HashSet<int>(noteIds);
            TotalNotesCount = _noteIds.Count;
        }

        public bool ContainsNote(int noteId) => _noteIds.Contains(noteId);
    }

    /// <summary>
    /// Parameterized ordinary-solo ScoreContext core recovered from WDS 2.30.3.
    /// Live-unit routing and skill additions remain outside this class.
    /// </summary>
    public sealed class SoloScoreRuntime
    {
        private readonly int _totalNotesCount;

        public long Count { get; private set; }
        public long MaxScore { get; }
        public decimal TotalAchievementRate { get; private set; }
        public double DifficultyAutoCoefficient { get; }
        public int BaseRate { get; }
        public double BuffRate { get; }

        public SoloScoreRuntime(
            double difficultyAutoCoefficient,
            int totalStatus,
            int baseScorePercentage,
            int totalNotesCount)
        {
            if (totalNotesCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(totalNotesCount));

            DifficultyAutoCoefficient = difficultyAutoCoefficient;
            BaseRate = totalStatus * 10;
            BuffRate = (baseScorePercentage + 100) / 100d;
            MaxScore = (long)(DifficultyAutoCoefficient * BaseRate * BuffRate);
            _totalNotesCount = totalNotesCount;
        }

        public long Collect(TimingType timingType)
        {
            TotalAchievementRate += GetAddingAchievementRate(
                timingType,
                _totalNotesCount);

            long targetScore;
            if (TotalAchievementRate >= decimal.One)
            {
                targetScore = MaxScore;
            }
            else
            {
                var truncatedRate = decimal.Floor(
                    TotalAchievementRate * 10000m) / 10000m;
                targetScore = (long)decimal.Floor(MaxScore * truncatedRate);
            }

            var addingScore = targetScore - Count;
            Count += addingScore;
            return addingScore;
        }

        public void Reset()
        {
            Count = 0;
            TotalAchievementRate = decimal.Zero;
        }

        public static decimal GetAddingAchievementRate(
            TimingType timingType,
            int totalNotesCount)
        {
            if (totalNotesCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(totalNotesCount));

            decimal coefficient;
            switch (timingType)
            {
                case TimingType.PerfectStar:
                    coefficient = 1.01m;
                    break;
                case TimingType.Perfect:
                    coefficient = 1m;
                    break;
                case TimingType.Great:
                    coefficient = 0.8m;
                    break;
                case TimingType.Good:
                    coefficient = 0.5m;
                    break;
                default:
                    coefficient = 0m;
                    break;
            }
            return coefficient / totalNotesCount;
        }
    }
}
