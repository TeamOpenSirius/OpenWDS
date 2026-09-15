using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Confirmed server-side player Rating rules, reproduced for the offline
    /// NORMAL..STELLA flow. Olivier/SP remains a separate server-owned system.
    /// </summary>
    public static class PlayerRating
    {
        public const int RatedChartCount = 30;

        public static bool IsEligible(
            LocalMusicEntry music,
            LocalLiveEntry live) =>
            music != null &&
            live != null &&
            !music.IsLongVersion &&
            live.Difficulty >= MusicDifficulty.Normal &&
            live.Difficulty <= MusicDifficulty.Stella;

        public static double CalculateNotationRate(
            int level,
            double achievementRate,
            bool isCleared = true)
        {
            if (!isCleared || level <= 0 ||
                double.IsNaN(achievementRate) ||
                double.IsInfinity(achievementRate) ||
                achievementRate < 80d)
                return 0d;

            var rate = Math.Min(101d, achievementRate);
            double raw;
            if (rate >= 100.95d)
                raw = level + 6d + (rate - 100.95d);
            else if (rate >= 100.75d)
                raw = level + 4.5d + 7.5d * (rate - 100.75d);
            else if (rate >= 100.5d)
                raw = level + 3d + 6d * (rate - 100.5d);
            else if (rate >= 100d)
                raw = level + 1.5d + 3d * (rate - 100d);
            else if (rate >= 98d)
                raw = level + 0.75d * (rate - 98d);
            else if (rate >= 95d)
                raw = level * (0.75d + (rate - 95d) / 12d);
            else if (rate >= 90d)
                raw = level * (0.5d + (rate - 90d) / 20d);
            else
                raw = level * rate / 180d;

            // The server stores/display-sums the per-chart value after dropping
            // digits below 0.01 (for example Lv29 at 100.80% is 33.87).
            return Math.Floor(raw * 100d + 0.000000001d) / 100d;
        }

        public static double CalculatePlayerRate(IEnumerable<double> chartRates)
        {
            if (chartRates == null) throw new ArgumentNullException(nameof(chartRates));
            return chartRates
                .Where(rate => rate > 0d && !double.IsNaN(rate))
                .OrderByDescending(rate => rate)
                .Take(RatedChartCount)
                .Sum();
        }

        public static double CalculatePlayerRate(
            IEnumerable<LocalMusicEntry> musics,
            LocalResultStore results)
        {
            if (musics == null) throw new ArgumentNullException(nameof(musics));
            if (results == null) throw new ArgumentNullException(nameof(results));
            return CalculatePlayerRate(
                musics
                    .Where(music => music?.Lives != null)
                    .SelectMany(music => music.Lives
                        .Where(live => IsEligible(music, live))
                        .Select(live => CalculateNotationRate(
                            live.Level,
                            results.GetBest(music.Id, live.Difficulty),
                            results.HasClear(music.Id, live.Difficulty)))));
        }

        /// <summary>
        /// Top text color selected by the retail ColorHelper.RateColor tiers.
        /// GameResultRate reapplies it while the old-to-new number tween runs,
        /// so crossing a tier changes color during the count-up.
        /// </summary>
        public static Color32 GetHudTopColor(double playerRate)
        {
            if (playerRate >= 1000d) return Hex(0xFF83EC);
            if (playerRate >= 950d) return Hex(0xFEB519);
            if (playerRate >= 900d) return Hex(0xECCD6D);
            if (playerRate >= 850d) return Hex(0xAEB0C0);
            if (playerRate >= 800d) return Hex(0xDE7446);
            if (playerRate >= 750d) return Hex(0xEE5F5F);
            if (playerRate >= 700d) return Hex(0xEE5F5F);
            if (playerRate >= 650d) return Hex(0xFF5498);
            if (playerRate >= 600d) return Hex(0xFF5498);
            if (playerRate >= 550d) return Hex(0x9567E9);
            if (playerRate >= 500d) return Hex(0x9567E9);
            if (playerRate >= 450d) return Hex(0x698CFE);
            if (playerRate >= 400d) return Hex(0x698CFE);
            if (playerRate >= 350d) return Hex(0x1BB5D3);
            if (playerRate >= 300d) return Hex(0x1BB5D3);
            if (playerRate >= 250d) return Hex(0xAAE21B);
            if (playerRate >= 200d) return Hex(0xAAE21B);
            return Hex(0xFFFFFF);
        }

        /// <summary>
        /// Settled GameResult footer text uses its own low-rate presentation.
        /// MusicSelectionHeader uses ColorPreset's separate gradient palette.
        /// </summary>
        public static Color32 GetGameResultTextColor(double rate)
        {
            return rate < 200d ? Hex(0x53545E) : GetHudTopColor(rate);
        }

        private static Color32 Hex(int rgb) =>
            new Color32(
                (byte)((rgb >> 16) & 0xFF),
                (byte)((rgb >> 8) & 0xFF),
                (byte)(rgb & 0xFF),
                0xFF);
    }
}
