using System;
using System.IO;
using System.Linq;
using OpenWDS.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenWDS.Editor
{
    // The focused input gate follows the production Game -> GameResult handoff.
    // It keeps value snapshots, never an unloaded GameRuntime reference.
    [InitializeOnLoad]
    public static class RunAutoplayJudgementPlayMode
    {
        private const string Key = "OpenWDS.JudgementPlayMode";
        [Serializable] private sealed class Catalog { public AnotherNotationSelectionRuntime.Entry[] Entries; }
        [Serializable] private sealed class Report
        {
            public bool passed, musicEndObserved, waitedAfterLastNote, resultShown;
            public long musicId, anotherId, lastNoteMilliseconds, musicEndMilliseconds;
            public int expected, collected, perfectStar, remaining;
            public double achievement, elapsedSeconds;
            public string error;
            public string[] nonPerfect;
        }
        private static Report _report;
        private static double _started;
        private static long _lastMusicTime;
        private static int _lastProgress;
        static RunAutoplayJudgementPlayMode() { EditorApplication.update += Poll; }

        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Run this gate in a dedicated batch-mode Editor.");
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".exit", false);
            SessionState.SetString(Key + ".start", DateTime.UtcNow.ToString("O"));
            EditorSceneManager.OpenScene("Assets/OpenWDS/Scenes/OfflineRhythmPreview.unity");
            EditorApplication.isPlaying = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PrepareSession()
        {
            if (!SessionState.GetBool(Key, false)) return;
            var catalog = LocalMusicCatalog.FromJson(File.ReadAllText(
                "Assets/OpenWDS/OfflineData/LocalMusicCatalog.json"));
            long.TryParse(Environment.GetEnvironmentVariable("OPENWDS_AUTO_JUDGE_ANOTHER_ID"), out var another);
            LocalMusicSelection selection;
            if (another > 0)
            {
                var special = JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(
                    Application.streamingAssetsPath, "OpenWDS/AnotherNotations/catalog.json")));
                var entry = special.Entries.Single(e => e.Id == another);
                selection = new LocalMusicSelection(entry.Music, entry.Music.Lives[0]);
            }
            else
            {
                var id = long.Parse(Environment.GetEnvironmentVariable("OPENWDS_AUTO_JUDGE_MUSIC_ID"));
                var difficulty = (MusicDifficulty)Enum.Parse(typeof(MusicDifficulty),
                    Environment.GetEnvironmentVariable("OPENWDS_AUTO_JUDGE_DIFFICULTY") ?? "Olivier", true);
                selection = catalog.Select(id, difficulty);
            }
            var chart = new TextAsset(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,
                selection.Live.DebugNotationAssetPath)));
            var config = new TextAsset(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,
                selection.Live.DebugMusicConfigAssetPath)));
            LocalMusicSelectionSession.Set(selection, chart, config, null, catalog.Musics,
                isOfficialAuto: true, ownsTextAssets: true);
            _report = new Report { musicId = selection.Music.Id, anotherId = another,
                lastNoteMilliseconds = StandardNotation.Parse(chart.text).Where(n => n.NoteType != 0)
                    .Max(n => Math.Max(n.StartMilliseconds, n.EndMilliseconds)) };
            _started = EditorApplication.timeSinceStartup;
            _lastMusicTime = 0;
            _lastProgress = -1;
            Application.logMessageReceived += OnLog;
        }

        private static void OnLog(string condition, string trace, LogType type)
        {
            if (_report != null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                _report.error = condition;
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (SessionState.GetBool(Key + ".exit", false))
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(SessionState.GetBool(Key + ".passed", false) ? 0 : 1);
                return;
            }
            if (!EditorApplication.isPlaying || _report == null) return;
            try
            {
                _report.elapsedSeconds = EditorApplication.timeSinceStartup - _started;
                if (_report.error != null) throw new InvalidOperationException(_report.error);
                if (_report.elapsedSeconds > 300) throw new TimeoutException("Autoplay judgement gate timed out.");
                var game = UnityEngine.Object.FindObjectOfType<GameRuntime>();
                var music = UnityEngine.Object.FindObjectOfType<CriMusicRuntime>();
                // A batch Editor has no foreground game window. Exercise the
                // normal foreground callback, as the pause smoke gate does.
                if (music != null && music.IsApplicationSuspended)
                {
                    music.SendMessage("OnApplicationFocus", true);
                    music.SendMessage("OnApplicationPause", false);
                }
                var progress = (int)(_report.elapsedSeconds / 30);
                if (progress != _lastProgress)
                {
                    _lastProgress = progress;
                    Debug.Log($"OPENWDS_PLAYMODE_AUTO_JUDGE_PROGRESS elapsed={_report.elapsedSeconds:F0} started={game?.IsGameplayStarted} paused={game?.IsPaused} music={music?.GetChartMilliseconds()} collected={_report.collected}");
                }
                if (game != null && game.IsInitialized && game.IsGameplayStarted && music != null)
                {
                    if (!music.HasPlaybackEnded)
                    {
                        _lastMusicTime = music.GetChartMilliseconds();
                        if (game.InputHandler.IsGameCompleted || game.IsClearPerformancePlaying)
                            throw new InvalidOperationException("Clear started before music playback ended.");
                        if (_lastMusicTime > _report.lastNoteMilliseconds + 1000)
                            _report.waitedAfterLastNote = true;
                    }
                    else
                    {
                        _report.musicEndObserved = true;
                        _report.musicEndMilliseconds = _lastMusicTime;
                    }
                    var score = game.GameResultRuntime;
                    _report.expected = score.AchievementRateNoteCount;
                    _report.collected = score.TimingCounts.Values.Sum();
                    _report.perfectStar = score.TimingCounts[TimingType.PerfectStar];
                    _report.achievement = score.AchievementRate;
                    _report.nonPerfect = score.NonPerfectResults.Select(r => $"{r.NoteId}:{r.NoteType}={r.TimingType}").ToArray();
                    _report.remaining = game.InputHandler.RemainingTapCount + game.InputHandler.RemainingFlickCount +
                        game.InputHandler.RemainingHoldCount + game.InputHandler.RemainingScratchCount +
                        game.InputHandler.RemainingHoldingCount;
                }
                var result = GameResultSceneRuntime.Instance;
                if (result == null || !result.IsInitialized || result.ResultSe.IsCounting) return;
                var panel = result.View.GetComponentInChildren<Sirius.GameResult.GameResultPanel>(true);
                if (panel == null || panel.IsCounting) return;
                _report.resultShown = true;
                _report.passed = _report.musicEndObserved && _report.expected > 0 &&
                    _report.perfectStar == _report.expected && _report.remaining == 0 &&
                    result.Data.Result.IsPerfectStar && Math.Abs(_report.achievement - 101) < 1e-8;
                if (!_report.passed) _report.error = "Autoplay did not produce all PERFECT_STAR results after music end.";
                Finish();
            }
            catch (Exception error) { _report.error = error.ToString(); Finish(); }
        }

        private static void Finish()
        {
            var suffix = _report.anotherId > 0 ? $"another-{_report.anotherId}" :
                $"{_report.musicId}-{(Environment.GetEnvironmentVariable("OPENWDS_AUTO_JUDGE_DIFFICULTY") ?? "Olivier").ToLowerInvariant()}";
            File.WriteAllText($"../../reverse/reports/unity-playmode-auto-judge-validation-{suffix}.json",
                JsonUtility.ToJson(_report, true));
            Application.logMessageReceived -= OnLog;
            Debug.Log($"OPENWDS_PLAYMODE_AUTO_JUDGE_RESULT passed={_report.passed} rate={_report.achievement} error={_report.error}");
            SessionState.SetBool(Key + ".passed", _report.passed);
            SessionState.SetBool(Key + ".exit", true);
            EditorApplication.isPlaying = false;
        }
    }
}
