using System;
using System.Collections.Generic;
using OpenWDS.Runtime;
using UnityEngine;

namespace Sirius.GameResult
{
    /// <summary>
    /// Local counterpart of the ordinary-solo fields in GameResultPanelEntity.
    /// It deliberately contains values that can be produced without the removed
    /// account/server result response.
    /// </summary>
    public sealed class GameResultViewData
    {
        public int MaxCombo { get; }
        public double AchievementRate { get; }
        public double TotalAchievementRate { get; }
        public double BestEverAchievementRate { get; }
        public bool IsNewAchievementRate { get; }
        public double CurrentRecommendationTiming { get; }
        public double RecommendationTiming { get; }
        public double NoteSpeed { get; }
        public bool IsAuto { get; }
        public bool IsFullCombo { get; }
        public bool IsAllPerfect { get; }
        public bool IsPerfectStar { get; }
        public bool ShouldShowPerfectStar { get; }
        public string MusicName { get; }
        public MusicDifficulty Difficulty { get; }
        public Sprite JacketSprite { get; }
        public double BestEverNotationRate { get; }
        public double ThisTimeNotationRate { get; }
        public double BeforePlayerRate { get; }
        public double AfterPlayerRate { get; }
        public bool IsNewNotationRate { get; }
        public bool IsNewPlayerRate { get; }
        public IReadOnlyDictionary<TimingType, int> TimingCounts { get; }
        public IReadOnlyDictionary<int, int> InputDiffSummary { get; }

        public GameResultViewData(
            int maxCombo,
            double achievementRate,
            double totalAchievementRate,
            double currentRecommendationTiming,
            double recommendationTiming,
            double noteSpeed,
            bool isAuto,
            bool isFullCombo,
            bool isAllPerfect,
            bool isPerfectStar,
            IReadOnlyDictionary<TimingType, int> timingCounts,
            IReadOnlyDictionary<int, int> inputDiffSummary,
            string musicName = "",
            MusicDifficulty difficulty = MusicDifficulty.Stella,
            Sprite jacketSprite = null,
            double bestEverNotationRate = 0d,
            double thisTimeNotationRate = 0d,
            double beforePlayerRate = 0d,
            double afterPlayerRate = 0d,
            bool isNewNotationRate = false,
            bool isNewPlayerRate = false,
            double bestEverAchievementRate = 0d,
            bool isNewAchievementRate = false,
            bool shouldShowPerfectStar = true)
        {
            MaxCombo = maxCombo;
            AchievementRate = achievementRate;
            TotalAchievementRate = totalAchievementRate;
            BestEverAchievementRate = bestEverAchievementRate;
            IsNewAchievementRate = isNewAchievementRate;
            CurrentRecommendationTiming = currentRecommendationTiming;
            RecommendationTiming = recommendationTiming;
            NoteSpeed = noteSpeed;
            IsAuto = isAuto;
            IsFullCombo = isFullCombo;
            IsAllPerfect = isAllPerfect;
            IsPerfectStar = isPerfectStar;
            ShouldShowPerfectStar = shouldShowPerfectStar;
            TimingCounts = timingCounts ?? throw new ArgumentNullException(nameof(timingCounts));
            InputDiffSummary = inputDiffSummary ??
                               throw new ArgumentNullException(nameof(inputDiffSummary));
            MusicName = musicName ?? string.Empty;
            Difficulty = difficulty;
            JacketSprite = jacketSprite;
            BestEverNotationRate = bestEverNotationRate;
            ThisTimeNotationRate = thisTimeNotationRate;
            BeforePlayerRate = beforePlayerRate;
            AfterPlayerRate = afterPlayerRate;
            IsNewNotationRate = isNewNotationRate;
            IsNewPlayerRate = isNewPlayerRate;
        }

        public static GameResultViewData FromRuntime(
            GameResultRuntime runtime,
            double currentRecommendationTiming,
            double noteSpeed,
            bool isAuto,
            string musicName = "",
            MusicDifficulty difficulty = MusicDifficulty.Stella,
            Sprite jacketSprite = null,
            double bestEverAchievementRate = 0d,
            bool isNewAchievementRate = false,
            bool shouldShowPerfectStar = true,
            double bestEverNotationRate = 0d,
            double thisTimeNotationRate = 0d,
            double beforePlayerRate = 0d,
            double afterPlayerRate = 0d,
            bool isNewNotationRate = false,
            bool isNewPlayerRate = false,
            bool isCleared = true)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            var counts = new Dictionary<TimingType, int>();
            foreach (var pair in runtime.TimingCounts) counts[pair.Key] = pair.Value;
            var summary = new Dictionary<int, int>();
            foreach (var pair in runtime.InputDiffSummary) summary[pair.Key] = pair.Value;
            var achievement = runtime.AchievementRate;
            return new GameResultViewData(
                runtime.MaxCombo,
                achievement,
                achievement,
                currentRecommendationTiming,
                runtime.CalculateRecommendationTiming(
                    currentRecommendationTiming, noteSpeed),
                noteSpeed,
                isAuto,
                isCleared && runtime.IsFullCombo,
                isCleared && runtime.IsAllPerfect,
                isCleared && runtime.IsPerfectStar,
                counts,
                summary,
                musicName,
                difficulty,
                jacketSprite,
                bestEverNotationRate,
                thisTimeNotationRate,
                beforePlayerRate,
                afterPlayerRate,
                isNewNotationRate,
                isNewPlayerRate,
                bestEverAchievementRate: bestEverAchievementRate,
                isNewAchievementRate: isNewAchievementRate,
                shouldShowPerfectStar: shouldShowPerfectStar);
        }

        public int GetTimingCount(TimingType timingType) =>
            TimingCounts.TryGetValue(timingType, out var value) ? value : 0;

        public int GetDisplayedTimingCount(TimingType timingType)
        {
            if (!ShouldShowPerfectStar &&
                timingType == TimingType.Perfect)
            {
                return GetTimingCount(TimingType.Perfect) +
                       GetTimingCount(TimingType.PerfectStar);
            }
            return GetTimingCount(timingType);
        }
    }
}
