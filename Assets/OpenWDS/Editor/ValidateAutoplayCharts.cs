using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenWDS.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenWDS.Editor
{
    public static class ValidateAutoplayCharts
    {
        [Serializable] public sealed class ChartResult
        {
            public string path;
            public int step;
            public int expected, collected, remaining;
            public double achievement;
            public bool passed;
            public List<string> failures = new List<string>();
        }
        [Serializable] public sealed class Report
        {
            public bool passed;
            public List<ChartResult> charts = new List<ChartResult>();
        }

        public static void Run()
        {
            ValidateHoldPulseTiming.ValidateSharedHoldRelease();
            var report = new Report { passed = true };
            var root = SongResourceStore.Root;
            EditorSceneManager.OpenScene("Assets/OpenWDS/Scenes/OfflineRhythmPreview.unity");
            var game = UnityEngine.Object.FindObjectOfType<GameRuntime>();
            var serialized = new SerializedObject(game);
            var camera = (Camera)serialized.FindProperty("_gameCamera").objectReferenceValue;
            var lanes = (Sirius.Game.LaneGroup)serialized.FindProperty("_laneGroup").objectReferenceValue;
            var managers = lanes.ColliderManagers;
            managers.InitializeMappings();
            Physics.SyncTransforms();
            var hits = new Dictionary<int, HitLaneEntity>();
            using (var raycaster = new LaneRaycaster(camera, managers.MainColliders,
                       managers.SubLeftInnerColliders, managers.SubRightInnerColliders,
                       managers.SubLeftOuterColliders, managers.SubRightOuterColliders))
                for (var lane = 1; lane <= 12; lane++)
                    hits[lane] = raycaster.RaycastPoint(camera.WorldToScreenPoint(lanes.GetLaneCollider(lane).position));
            var paths = new List<string>
            {
                "OpenWDS/StandardCharts/237/1/5.csv",
                "OpenWDS/AnotherNotations/52/5.csv",
                "OpenWDS/StandardCharts/1/1/4.csv",
                "OpenWDS/StandardCharts/61/1/4.csv"
            };
            var catalog = JsonUtility.FromJson<LocalMusicCatalog>(File.ReadAllText(
                "SongResources/catalog.json"));
            paths.AddRange(catalog.Musics.SelectMany(m => m.Lives)
                .Where(l => l.Difficulty == MusicDifficulty.Olivier)
                .OrderByDescending(l => l.Level).Take(12)
                .Select(l => l.DebugNotationAssetPath));
            foreach (var path in paths.Distinct())
            foreach (var step in new[] { 8, 16, 33 })
            {
                var notes = StandardNotation.Parse(File.ReadAllText(Path.Combine(root, path)));
                var byId = notes.ToDictionary(n => n.Id);
                var score = new GameResultRuntime(notes);
                var row = new ChartResult { path = path, step = step,
                    expected = score.AchievementRateNoteCount };
                using (var runtime = new InputHandlerRuntime(notes, 0f,
                           n => new Vector2(n.Lane, 0),
                           p => hits[(int)p.x]))
                {
                    var end = notes.Max(n => Math.Max(n.StartMilliseconds, n.EndMilliseconds)) + 1500;
                    for (long t = -100; t <= end; t += step)
                    {
                        runtime.Tick(t, t + 100);
                        foreach (var result in runtime.InputResults)
                        {
                            score.Collect(result);
                            if (result.TimingType != TimingType.PerfectStar)
                            {
                                var n = byId[result.NoteId];
                                row.failures.Add($"{n.Id}:{n.NoteType}@{n.StartMilliseconds}-{n.EndMilliseconds} lane={n.Lane}/{n.Width} {result.TimingType} frame={t}");
                            }
                        }
                    }
                    row.remaining = runtime.RemainingTapCount + runtime.RemainingFlickCount +
                        runtime.RemainingHoldCount + runtime.RemainingScratchCount + runtime.RemainingHoldingCount;
                }
                row.collected = score.CollectedCount;
                row.achievement = score.AchievementRate;
                row.passed = score.IsPerfectStar && row.remaining == 0 &&
                    score.TimingCounts.Values.Sum() == row.expected;
                report.charts.Add(row);
                report.passed &= row.passed;
                Debug.Log($"OPENWDS_AUTOPLAY_CHART {path} step={step} passed={row.passed} rate={row.achievement} failures={row.failures.Count}");
            }
            Directory.CreateDirectory("../../reverse/reports");
            File.WriteAllText("../../reverse/reports/autoplay-charts.json", JsonUtility.ToJson(report, true));
            EditorApplication.Exit(report.passed ? 0 : 1);
        }
    }
}
