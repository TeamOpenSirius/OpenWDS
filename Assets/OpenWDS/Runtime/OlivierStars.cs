using System;
using System.Collections.Generic;

namespace OpenWDS.Runtime
{
    // Port of SiriusServer/Lives/Finish/OlivierSpRateCalculator and settlement.
    public static class OlivierStars
    {
        private static readonly (double Threshold, int[] Points)[] AchievementRows =
        {
            (100.90d, new[] {59, 69, 79, 89, 99, 109, 118, 128, 138, 148}),
            (100.85d, new[] {58, 68, 78, 88, 98, 108, 116, 126, 136, 146}),
            (100.80d, new[] {57, 67, 77, 87, 97, 106, 114, 124, 134, 144}),
            (100.75d, new[] {56, 66, 76, 86, 96, 104, 112, 122, 132, 142}),
            (100.70d, new[] {55, 65, 75, 85, 94, 102, 110, 120, 130, 140}),
            (100.60d, new[] {54, 64, 74, 84, 92, 100, 108, 118, 128, 138}),
            (100.50d, new[] {53, 63, 73, 82, 90, 98, 106, 116, 126, 135}),
            (100.40d, new[] {52, 62, 72, 80, 88, 96, 104, 114, 124, 132}),
            (100.30d, new[] {51, 61, 70, 78, 86, 94, 102, 112, 121, 129}),
            (100.20d, new[] {50, 60, 68, 76, 84, 92, 100, 110, 118, 126}),
            (100.10d, new[] {49, 58, 66, 74, 82, 90, 98, 107, 115, 123}),
            (100.00d, new[] {48, 56, 64, 72, 80, 88, 96, 104, 112, 120})
        };

        private static readonly double[] IconThresholds =
            { 20, 30, 40, 50, 60, 70, 75, 80, 85, 90, 95, 98 };
        private static readonly string[] IconNames =
            { "red", "copper", "copper2", "copper3", "silver", "silver2", "silver3",
              "gold", "gold2", "gold3", "rainbow", "rainbow2", "rainbow3" };

        public static string GetIconName(double percentage)
        {
            if (percentage < 0 || percentage > 100 || double.IsNaN(percentage)) return IconNames[0];
            var index = 0;
            while (index < IconThresholds.Length && percentage >= IconThresholds[index]) index++;
            return IconNames[index];
        }

        public static int GetMaxPoint(int level)
        {
            if (level < 101 || level > 110)
                throw new ArgumentOutOfRangeException(nameof(level));
            return (level - 100) * 10 + 60;
        }

        public static int CalculateAchievementStar(int level, double rate)
        {
            var maximum = GetMaxPoint(level) - 10;
            if (double.IsNaN(rate) || double.IsInfinity(rate))
                throw new ArgumentOutOfRangeException(nameof(rate));
            var rank = level - 100;
            if (rate >= 100.95d) return maximum;
            foreach (var row in AchievementRows)
                if (rate >= row.Threshold) return row.Points[rank - 1];
            var whole = (int)Math.Floor(Math.Max(0d, rate));
            var point = whole >= 99 ? 7 * rank + 36 :
                whole >= 98 ? 7 * rank + 33 :
                whole >= 95 ? 6 * rank + 23 + 3 * (whole - 95) :
                whole >= 90 ? 5 * rank + 7 + 3 * (whole - 90) :
                whole >= 80 ? 4 * rank - 24 + 3 * (whole - 80) : 0;
            return Math.Max(0, Math.Min(maximum, point));
        }

        public static int CalculateAccuracyStar(int nonPerfectStarCount)
        {
            if (nonPerfectStarCount < 0)
                throw new ArgumentOutOfRangeException(nameof(nonPerfectStarCount));
            return (nonPerfectStarCount <= 100 ? 1 : 0) +
                (nonPerfectStarCount <= 75 ? 1 : 0) +
                (nonPerfectStarCount <= 50 ? 1 : 0) +
                (nonPerfectStarCount <= 30 ? 1 : 0) +
                (nonPerfectStarCount <= 10 ? 1 : 0);
        }

        public static int CalculateLampStar(ClearLamp lamp) =>
            lamp == ClearLamp.AllPerfect ? 5 : lamp == ClearLamp.FullCombo ? 2 : 0;

        public static bool IsEligible(LocalMusicEntry music, LocalLiveEntry live) =>
            music != null && music.Id > 0 && !music.IsLongVersion &&
            live != null && live.Id > 0 && live.AnotherNotationId == 0 &&
            live.Difficulty == MusicDifficulty.Olivier && live.Level >= 101 && live.Level <= 110;

        public static int GetTotalPoint(IEnumerable<LocalMusicEntry> musics, LocalResultStore store)
        {
            var total = 0;
            var seen = new HashSet<long>();
            foreach (var music in musics)
                if (music?.Lives != null)
                    foreach (var live in music.Lives)
                        if (IsEligible(music, live) && seen.Add(live.Id))
                            total += store.GetSpPoint(music.Id);
            return total;
        }

        public static int GetTotalObtainablePoint(IEnumerable<LocalMusicEntry> musics, DateTime utcNow)
        {
            var total = 0;
            var seen = new HashSet<long>();
            foreach (var music in musics)
                if (music?.Lives != null && music.HasReleasedAt &&
                    music.ReleasedAtUtcTicks <= utcNow.Ticks)
                    foreach (var live in music.Lives)
                        if (IsEligible(music, live) && seen.Add(live.Id))
                            total += GetMaxPoint(live.Level);
            return total;
        }

        public static double GetPercentage(int point, int maximum) => maximum <= 0 ? 0d :
            Math.Truncate(point * 100d / maximum * 100d) / 100d;
    }
}
