using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Confirmed server-side player Rating rules, reproduced for the offline
    /// NORMAL..STELLA flow. Olivier/SP uses the separate OlivierStars calculator.
    /// </summary>
    public static class PlayerRating
    {
        public const int RatedChartCount = 30;

        public static bool IsEligible(
            LocalMusicEntry music,
            LocalLiveEntry live) =>
            music != null &&
            live != null &&
            !music.IsLongVersion && live.AnotherNotationId == 0 &&
            live.Difficulty >= MusicDifficulty.Normal &&
            live.Difficulty <= MusicDifficulty.Stella;

        // SiriusServer LiveResultCalculators.LiveRateCalculator. Clear lamps are
        // independent: failed manual plays may still improve achievement/rating.
        private static readonly (double Rate, double Adjustment)[] Breakpoints =
        {
            (101, 6.05), (100.95, 6), (100.75, 4.5), (100.5, 3),
            (100.25, 2.25), (100, 1.5), (99, .75), (98, 0), (97.5, -1)
        };

        public static double CalculateNotationRate(int level, double achievementRate)
        {
            if (level <= 0 || double.IsNaN(achievementRate) ||
                double.IsInfinity(achievementRate) || achievementRate < 97.5d)
                return 0d;
            if (achievementRate >= 101d) return Math.Round(level + 6.05d, 2);
            for (var index = 0; index < Breakpoints.Length - 1; index++)
            {
                var high = Breakpoints[index];
                var low = Breakpoints[index + 1];
                if (achievementRate >= low.Rate)
                    return Math.Round(level + (low.Adjustment +
                        (achievementRate - low.Rate) / (high.Rate - low.Rate) *
                        (high.Adjustment - low.Adjustment)), 2);
            }
            return 0d;
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
                            results.GetBest(music.Id, live.Difficulty)))));
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
