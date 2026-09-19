using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine.UI;
using DG.Tweening;
using Sirius.GameResult;
using OpenWDS.Runtime;
using UnityEngine;

namespace OpenWDS.Editor
{
    public static class ValidateOlivierStars
    {
        public static void Run()
        {
            Require(OlivierStars.CalculateAchievementStar(105, 100.65) == 92 &&
                OlivierStars.CalculateAchievementStar(105, 100.78) == 96 &&
                OlivierStars.CalculateAchievementStar(106, 99.2142) == 78,
                "server examples");
            Require(OlivierStars.CalculateAchievementStar(105, 100.9499) == 99 &&
                OlivierStars.CalculateAchievementStar(105, 100.95) == 100 &&
                OlivierStars.CalculateAchievementStar(110, 97.9999) == 89 &&
                OlivierStars.CalculateAchievementStar(110, 98) == 103,
                "discrete thresholds");
            for (var level = 101; level <= 110; level++)
                Require(OlivierStars.CalculateAchievementStar(level, 101) + 10 ==
                    OlivierStars.GetMaxPoint(level), "I..X maximum");
            var counts = new[] { 101, 100, 76, 75, 51, 50, 31, 30, 11, 10, 0 };
            var bonuses = new[] { 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5 };
            for (var i = 0; i < counts.Length; i++)
                Require(OlivierStars.CalculateAccuracyStar(counts[i]) == bonuses[i], "B boundary");
            Require(OlivierStars.CalculateAccuracyStar(41 + 14 + 1) == 2,
                "all non-PERFECT_STAR judgments counted");
            var root = Path.Combine(Path.GetTempPath(), "OpenWDS-Sp-" + Guid.NewGuid().ToString("N"));
            try
            {
                var live = new LocalLiveEntry { Id = 105, Difficulty = MusicDifficulty.Olivier, Level = 105 };
                var music = new LocalMusicEntry { Id = 1, HasReleasedAt = true,
                    ReleasedAtUtcTicks = DateTime.UtcNow.AddDays(-1).Ticks, Lives = new[] { live } };
                var store = new LocalResultStore(root);
                // Existing saves never retained B or eligibility; do not invent either.
                store.RecordResult(1, MusicDifficulty.Olivier, 101, ClearLamp.AllPerfect, out _);
                Require(new LocalResultStore(root).GetSpPoint(1) == 0, "legacy result preserved without fabricated stars");
                Require(store.RecordOlivierResult(music, live, 100.65, ClearLamp.FullCombo, 56, true, false) == 96,
                    "first attempt A92+B2+C2");
                Require(store.RecordOlivierResult(music, live, 100.5, ClearLamp.AllPerfect, 10, true, false) == 100,
                    "second attempt A90+B5+C5");
                store = new LocalResultStore(root);
                Require(store.GetSpPoint(1) == 102, "independent maxima survive reload (92+5+5)");
                var path = Path.Combine(root, SettingsPersistence.CurrentDirectory, "OpenWDSLocalResults");
                var before = File.ReadAllBytes(path);
                Require(store.RecordOlivierResult(music, live, 101, ClearLamp.AllPerfect, 0, false, false) == 0 &&
                    store.RecordOlivierResult(music, live, 101, ClearLamp.AllPerfect, 0, true, true) == 0 &&
                    store.RecordOlivierResult(music, live, 101, ClearLamp.AllPerfect, 0, true, false, LiveType.Audition) == 0,
                    "failure/auto/audition excluded");
                live.AnotherNotationId = 9;
                store.RecordOlivierResult(music, live, 101, ClearLamp.AllPerfect, 0, true, false);
                live.AnotherNotationId = 0;
                music.IsLongVersion = true;
                store.RecordOlivierResult(music, live, 101, ClearLamp.AllPerfect, 0, true, false);
                music.IsLongVersion = false;
                Require(Convert.ToBase64String(before) == Convert.ToBase64String(File.ReadAllBytes(path)),
                    "ineligible attempts do not write save");
                var future = new LocalMusicEntry { Id = 2, HasReleasedAt = true,
                    ReleasedAtUtcTicks = DateTime.UtcNow.AddDays(1).Ticks,
                    Lives = new[] { new LocalLiveEntry { Id = 205, Difficulty = MusicDifficulty.Olivier, Level = 110 } } };
                var catalog = new[] { music, music, future };
                Require(OlivierStars.GetTotalPoint(catalog, store) == 102 &&
                    OlivierStars.GetTotalObtainablePoint(catalog, DateTime.UtcNow) == 110 &&
                    OlivierStars.GetPercentage(102, 110) == 92.72 &&
                    OlivierStars.GetPercentage(0, 0) == 0, "catalog total/date/deduplication/truncation");

            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            ValidatePresentation();
            Debug.Log("OPENWDS_OLIVIER_STARS_VALIDATED server fixtures, independent best, persistence, exclusions, total percentage");
        }

        private static void ValidatePresentation()
        {
            var percentages = new[] { 0d, 20, 30, 40, 50, 60, 70, 75, 80, 85, 90, 95, 98 };
            foreach (var value in percentages)
            {
                var sprite = Resources.Load<Sprite>("OlivierStarIcons/" + OlivierStars.GetIconName(value));
                Require(sprite != null && sprite.rect.width == 256 && sprite.rect.height == 256,
                    "original icon imports and logical canvas");
            }
            Require(OlivierStars.GetIconName(19.99) == "red" &&
                OlivierStars.GetIconName(97.99) == "rainbow2" &&
                OlivierStars.GetIconName(100) == "rainbow3", "icon boundaries");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/OrdinarySoloGameResult.prefab");
            var instance = UnityEngine.Object.Instantiate(prefab);
            instance.SetActive(false);
            Sequence sequence = null;
            try
            {
                var panel = instance.GetComponentInChildren<GameResultRatePanel>(true);
                var data = new GameResultViewData(100, 100.65, 100.65, 0, 0, 10,
                    false, true, false, false, new Dictionary<TimingType, int>(),
                    new Dictionary<int, int>(), difficulty: MusicDifficulty.Olivier,
                    bestEverNotationRate: 96, thisTimeNotationRate: 100,
                    beforePlayerRate: 87.27, afterPlayerRate: 92.72,
                    isNewNotationRate: true, isNewPlayerRate: true);
                panel.Initialize(data);
                var texts = panel.GetComponentsInChildren<Text>(true);
                Require(texts.Any(t => t.text == "星章") && texts.Any(t => t.text == "星章達成率") &&
                    texts.Any(t => t.name == "ThisTimeRate" && t.text == "100") &&
                    texts.Any(t => t.name == "ThisTimeRate" && t.text == "92.72%"),
                    "result labels, points and percentage");
                sequence = (Sequence)typeof(GameResultRatePanel).GetMethod("CreateCountUp",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel, new object[] { data, null });
                sequence.Complete(true);
                Require(texts.Any(t => t.name == "ThisTimeRate" && t.text == "100") &&
                    texts.Any(t => t.name == "ThisTimeRate" && t.text == "92.72%"),
                    "count-up retains SP formatting");
            }
            finally
            {
                sequence?.Kill();
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Olivier stars: " + message);
        }
    }
}
