using System;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenWDS.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace OpenWDS.Editor
{
    [InitializeOnLoad]
    public static class RunOfflineAutoplayPlayMode
    {
        private const string ScenePath =
            "Assets/OpenWDS/Scenes/OfflineRhythmPreview.unity";
        private const string ActiveKey = "OpenWDS.AutoplayPlayMode.Active";
        private const string PhaseKey = "OpenWDS.AutoplayPlayMode.Phase";
        private const string StartedKey = "OpenWDS.AutoplayPlayMode.StartedUtcTicks";
        private const string ErrorKey = "OpenWDS.AutoplayPlayMode.FirstError";
        private const string PauseSmokeKey =
            "OpenWDS.AutoplayPlayMode.PauseSmokePassed";
        private const string SenseCutInVisualKey =
            "OpenWDS.AutoplayPlayMode.SenseCutInVisualObserved";
        private const string IntroductionVisualKey =
            "OpenWDS.AutoplayPlayMode.IntroductionVisualObserved";
        private const string IntroductionFrameEarlyKey =
            "OpenWDS.AutoplayPlayMode.IntroductionFrameEarly";
        private const string IntroductionFrameLateKey =
            "OpenWDS.AutoplayPlayMode.IntroductionFrameLate";
        private const string ClearVisualKey =
            "OpenWDS.AutoplayPlayMode.ClearVisualObserved";
        private const string ClearFrameKey =
            "OpenWDS.AutoplayPlayMode.ClearFrame";
        private const string CaptureOnlyKey =
            "OpenWDS.AutoplayPlayMode.CaptureOnly";
        private const string ClearCaptureOnlyKey =
            "OpenWDS.AutoplayPlayMode.ClearCaptureOnly";
        private const string ClearCaptureStartedKey =
            "OpenWDS.AutoplayPlayMode.ClearCaptureStarted";
        private const string TargetedChartKey =
            "OpenWDS.AutoplayPlayMode.TargetedChart";
        private const string MusicTimeSecondsKey =
            "OpenWDS.AutoplayPlayMode.MusicTimeSeconds";
        private const string MusicIdKey =
            "OpenWDS.AutoplayPlayMode.MusicId";
        private const string DifficultyKey =
            "OpenWDS.AutoplayPlayMode.Difficulty";
        private const string ExitPhase = "exit";

        [Serializable]
        private sealed class AutoplayReport
        {
            public string generatedAtUtc;
            public bool passed;
            public string failure;
            public long musicId;
            public string difficulty;
            public bool autoJudgeEnabled;
            public bool detailedResultVisible;
            public bool autoplayResultUiHidden;
            public bool resultRatingsVisible;
            public double resultNotationRating;
            public double resultPlayerRating;
            public string resultRatingDiagnostic;
            public bool resultDifficultyColorValid;
            public int resultLampCount;
            public double elapsedSeconds;
            public int collectedCount;
            public int maxCombo;
            public int perfectStar;
            public int perfect;
            public int great;
            public int good;
            public int bad;
            public int miss;
            public string nonPerfectResults;
            public int remainingTap;
            public int remainingFlick;
            public string remainingFlickNotes;
            public string remainingFlickInputDiagnostics;
            public int remainingHold;
            public int remainingScratch;
            public int remainingHolding;
            public int activeTouchHolds;
            public long score;
            public long baseScore;
            public long senseScore;
            public int senseActivationCount;
            public long starActScore;
            public int starActActivationCount;
            public long maxScore;
            public int life;
            public int maxLife;
            public int principal;
            public int maxPrincipal;
            public int gameResultPresentationCount;
            public bool pauseSmokePassed;
            public bool senseCutInVisualObserved;
            public bool introductionVisualObserved;
            public bool clearVisualObserved;
        }

        static RunOfflineAutoplayPlayMode()
        {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException(
                    "The autoplay gate must run in a dedicated batch-mode Editor process.");
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("Offline preview scene is missing.", ScenePath);

            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(PhaseKey, "play");
            SessionState.SetString(StartedKey, DateTime.UtcNow.Ticks.ToString());
            SessionState.EraseString(ErrorKey);
            SessionState.SetBool(PauseSmokeKey, false);
            SessionState.SetBool(SenseCutInVisualKey, false);
            SessionState.SetBool(IntroductionVisualKey, false);
            SessionState.SetBool(IntroductionFrameEarlyKey, false);
            SessionState.SetBool(IntroductionFrameLateKey, false);
            SessionState.SetBool(ClearVisualKey, false);
            SessionState.SetBool(ClearFrameKey, false);
            SessionState.SetBool(
                CaptureOnlyKey,
                Environment.GetEnvironmentVariable(
                    "OPENWDS_INTRODUCTION_CAPTURE_ONLY") == "1");
            SessionState.SetBool(
                ClearCaptureOnlyKey,
                Environment.GetEnvironmentVariable(
                    "OPENWDS_CLEAR_CAPTURE_ONLY") == "1");
            SessionState.SetBool(ClearCaptureStartedKey, false);
            SessionState.SetInt(MusicTimeSecondsKey, 0);
            for (var index = 1; index <= 9; index++)
                SessionState.SetBool(
                    "OpenWDS.AutoplayPlayMode.IntroductionFrame" + index,
                    false);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ConfigureRequestedChart();
            if (Environment.GetEnvironmentVariable(
                    "OPENWDS_INTRODUCTION_CAPTURE_UNLOCK") == "1")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredGameRuntime>();
                if (runtime == null)
                    throw new InvalidOperationException(
                        "Offline preview runtime is missing.");
                var serializedRuntime = new SerializedObject(runtime);
                serializedRuntime.FindProperty("_isUnlockOlivier").boolValue =
                    true;
                serializedRuntime.ApplyModifiedPropertiesWithoutUndo();
            }
            Debug.Log("OPENWDS_PLAYMODE_AUTO_JUDGE_START scene=" + ScenePath);
            EditorApplication.isPlaying = true;
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;

            if (SessionState.GetString(PhaseKey, string.Empty) == ExitPhase)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    var passed = string.IsNullOrEmpty(
                        SessionState.GetString(ErrorKey, string.Empty));
                    SessionState.SetBool(ActiveKey, false);
                    EditorApplication.Exit(passed ? 0 : 1);
                }
                return;
            }

            var elapsed = GetElapsedSeconds();
            if (elapsed > GetTimeoutSeconds())
            {
                Fail("Play Mode autoplay timed out after " +
                     elapsed.ToString("F1") + " seconds.", null, elapsed);
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;

            var runtime = UnityEngine.Object.FindObjectOfType<RecoveredGameRuntime>();
            var firstError = SessionState.GetString(ErrorKey, string.Empty);
            if (!string.IsNullOrEmpty(firstError))
            {
                Fail("Play Mode logged an error: " + firstError, runtime, elapsed);
                return;
            }
            if (runtime == null || !runtime.IsInitialized || runtime.InputHandler == null)
                return;
            if (SessionState.GetBool(ClearCaptureOnlyKey, false))
            {
                RunClearCaptureOnly(runtime);
                return;
            }
            ObserveBoundaryPerformances(runtime);
            if (!runtime.IsGameplayStarted) return;
            if (SessionState.GetBool(CaptureOnlyKey, false))
            {
                var introduction = Resources.FindObjectsOfTypeAll<
                        Sirius.Game.GameIntroductionAnimationController>()
                    .FirstOrDefault(value =>
                        value != null && value.gameObject.scene.IsValid());
                var expectedUnlock =
                    Environment.GetEnvironmentVariable(
                        "OPENWDS_INTRODUCTION_CAPTURE_UNLOCK") == "1";
                if (introduction == null ||
                    introduction.IsUnlockOlivier != expectedUnlock ||
                    (expectedUnlock &&
                     introduction.SelectedDifficultyIndex !=
                     (int)RecoveredMusicDifficulty.Olivier - 1))
                {
                    Fail(
                        "GameIntroduction selected the wrong difficulty branch.",
                        runtime, elapsed, false);
                    return;
                }
                var sharedSe = UnityEngine.Object.FindObjectOfType<
                    RecoveredUiSeRuntime>();
                if (expectedUnlock &&
                    (sharedSe == null ||
                     sharedSe.LastCueName != "Olivier_glass" ||
                     sharedSe.CueNamePlayCount != 1 ||
                     sharedSe.LastPlayback.id ==
                     CriWare.CriAtomExPlayback.invalidId))
                {
                    Fail(
                        "Olivier introduction did not play its authored shared SE.",
                        runtime, elapsed, false);
                    return;
                }
                Debug.Log(
                    "OPENWDS_GAME_INTRODUCTION_CAPTURE_PASS " +
                    $"sharedSeCue={sharedSe?.LastCueName ?? "none"} " +
                    $"sharedSeCount={sharedSe?.CueNamePlayCount ?? 0}");
                Finish(true);
                return;
            }
            if (!SessionState.GetBool(PauseSmokeKey, false) &&
                !RunPauseSmoke(runtime, elapsed))
                return;
            ObserveSenseCutInVisual(runtime);
            if (!runtime.InputHandler.IsGameCompleted ||
                !runtime.IsResultShown) return;
            if (runtime.ResultSe == null ||
                runtime.ResultSe.PresentationCount == 0 ||
                runtime.ResultSe.IsCounting) return;
            // GameResult_left_in keeps the entire LeftPanel transparent until
            // 1.67 s into its authored entrance clip. Sampling immediately
            // after result SE counting therefore reports every child (including
            // ratings) as invisible even though the clip has not finished yet.
            var liveResultPanel = UnityEngine.Object.FindObjectOfType<
                Sirius.GameResult.GameResultPanel>(true);
            var leftPanelGroup = liveResultPanel != null
                ? liveResultPanel.transform.parent?.GetComponent<CanvasGroup>()
                : null;
            if (leftPanelGroup == null || leftPanelGroup.alpha < 0.99f) return;

            var report = BuildReport(runtime, elapsed);
            var inputPassed = report.collectedCount > 0 &&
                            report.bad == 0 && report.miss == 0 &&
                            report.remainingTap == 0 && report.remainingFlick == 0 &&
                            report.remainingHold == 0 && report.remainingScratch == 0 &&
                            report.remainingHolding == 0 && report.activeTouchHolds == 0 &&
                            report.autoJudgeEnabled &&
                            report.detailedResultVisible &&
                            report.autoplayResultUiHidden &&
                            report.resultRatingsVisible &&
                            report.resultNotationRating > 0d &&
                            report.resultPlayerRating > 0d &&
                            report.resultDifficultyColorValid &&
                            report.resultLampCount == 5;
            report.passed = SessionState.GetBool(TargetedChartKey, false)
                ? inputPassed
                : inputPassed &&
                            report.score == 12322L && report.baseScore == 5596L &&
                            report.senseScore == 1556L &&
                            report.senseActivationCount == 8 &&
                            runtime.GameHud.AdditionalScoreCutInPanel != null &&
                            runtime.GameHud.AdditionalScoreCutInPanel.ActivationCount == 8 &&
                            report.senseCutInVisualObserved &&
                            report.introductionVisualObserved &&
                            report.clearVisualObserved &&
                            report.starActScore == 5170L &&
                            report.starActActivationCount == 1 &&
                            report.maxScore == 5596L &&
                            report.life == 1000 && report.maxLife == 1000 &&
                            report.principal == 840 &&
                            report.maxPrincipal == 1000 &&
                            report.pauseSmokePassed &&
                            runtime.GameResultPresentationCount == 1 &&
                            runtime.ClearSe != null &&
                            runtime.ClearSe.LastCueName ==
                                RecoveredGameRuntime.GetBoundaryClearType(
                                    runtime.GameResultRuntime).ToString() &&
                            runtime.ClearSe.PlayCount == 1 &&
                            runtime.ClearSe.LastPlayback.id !=
                            CriWare.CriAtomExPlayback.invalidId &&
                            runtime.ResultSe.PresentationCount == 1 &&
                            runtime.ResultBgm != null &&
                            runtime.ResultBgm.PlayCount == 1 &&
                            runtime.ResultBgm.LastPlayback.id !=
                                CriWare.CriAtomExPlayback.invalidId &&
                            runtime.GameHud.AchievementRatePanel != null &&
                            !runtime.GameHud.AchievementRatePanel
                                .IsAnimationEnabled &&
                            Math.Abs(runtime.GameResultRuntime
                                .GetDisplayedAchievementRate(1) - 101d) <
                                0.000001d &&
                            Math.Abs(runtime.GameResultRuntime
                                .GetDisplayedAchievementRate(2) - 101d) <
                                0.000001d;
            if (!report.passed)
            {
                report.failure = "Faithful automatic judging did not finish " +
                                 "with normal result UI and empty input queues.";
                WriteReport(report);
                Fail(report.failure, runtime, elapsed, false);
                return;
            }

            WriteReport(report);
            Debug.Log(
                "OPENWDS_PLAYMODE_AUTO_JUDGE_PASS " +
                $"perfectStar={report.perfectStar} perfect={report.perfect} " +
                $"great={report.great} good={report.good} bad={report.bad} " +
                $"miss={report.miss} maxCombo={report.maxCombo} " +
                $"life={report.life}/{report.maxLife} " +
                $"elapsedSeconds={report.elapsedSeconds:F1}");
            Finish(true);
        }

        private static void RunClearCaptureOnly(RecoveredGameRuntime runtime)
        {
            var clear = Resources.FindObjectsOfTypeAll<
                    Sirius.Game.GameResultPanel>()
                .FirstOrDefault(value =>
                    value != null && value.gameObject.scene.IsValid());
            if (clear == null) return;

            if (!SessionState.GetBool(ClearCaptureStartedKey, false))
            {
                var introduction = Resources.FindObjectsOfTypeAll<
                        Sirius.Game.GameIntroductionAnimationController>()
                    .FirstOrDefault(value =>
                        value != null && value.gameObject.scene.IsValid());
                if (introduction != null)
                    introduction.gameObject.SetActive(false);
                runtime.SetPaused(true);
                var requestedType = Environment.GetEnvironmentVariable(
                    "OPENWDS_CLEAR_CAPTURE_TYPE");
                var clearType =
                    Enum.TryParse<Sirius.Game.RecoveredBoundaryClearType>(
                        requestedType, true, out var parsedType)
                        ? parsedType
                        : Sirius.Game.RecoveredBoundaryClearType.Clear;
                clear.Show(clearType);
                SessionState.SetBool(ClearCaptureStartedKey, true);
                Debug.Log(
                    "OPENWDS_GAME_CLEAR_CAPTURE_START type=" + clearType);
                return;
            }

            var typeName = clear.ClearType.ToString().ToLowerInvariant();
            CaptureClearFrame(
                runtime, clear, ClearFrameKey, 0.5f,
                $"game-clear-{typeName}-middle.png");
            if (!SessionState.GetBool(ClearFrameKey, false)) return;
            Debug.Log("OPENWDS_GAME_CLEAR_CAPTURE_PASS");
            Finish(true);
        }

        private static AutoplayReport BuildReport(
            RecoveredGameRuntime runtime, double elapsed)
        {
            var input = runtime.InputHandler;
            var result = runtime.GameResultRuntime;
            var report = new AutoplayReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                musicId = SessionState.GetInt(MusicIdKey, 1),
                difficulty = SessionState.GetString(
                    DifficultyKey, RecoveredMusicDifficulty.Stella.ToString()),
                autoJudgeEnabled = runtime.IsAutoJudgeEnabled,
                elapsedSeconds = elapsed,
                collectedCount = result.Count,
                maxCombo = result.MaxCombo,
                perfectStar = GetCount(result, RecoveredTimingType.PerfectStar),
                perfect = GetCount(result, RecoveredTimingType.Perfect),
                great = GetCount(result, RecoveredTimingType.Great),
                good = GetCount(result, RecoveredTimingType.Good),
                bad = GetCount(result, RecoveredTimingType.Bad),
                miss = GetCount(result, RecoveredTimingType.Miss),
                nonPerfectResults = FormatNonPerfectResults(result),
                remainingTap = input.RemainingTapCount,
                remainingFlick = input.RemainingFlickCount,
                remainingFlickNotes = FormatRemainingFlickNotes(input),
                remainingFlickInputDiagnostics =
                    FormatRemainingFlickInputDiagnostics(input),
                remainingHold = input.RemainingHoldCount,
                remainingScratch = input.RemainingScratchCount,
                remainingHolding = input.RemainingHoldingCount,
                activeTouchHolds = input.ActiveTouchHoldCount,
                score = runtime.GameHud != null && runtime.GameHud.Score != null
                    ? runtime.GameHud.TotalScore
                    : -1L,
                baseScore = runtime.GameHud != null && runtime.GameHud.Score != null
                    ? runtime.GameHud.Score.Count
                    : -1L,
                senseScore = runtime.GameHud != null &&
                             runtime.GameHud.SenseScore != null
                    ? runtime.GameHud.SenseScore.Count
                    : -1L,
                senseActivationCount = runtime.GameHud != null &&
                                       runtime.GameHud.SenseScore != null
                    ? runtime.GameHud.SenseScore.ActivationCount
                    : -1,
                starActScore = runtime.GameHud != null &&
                               runtime.GameHud.StarActScore != null
                    ? runtime.GameHud.StarActScore.Count
                    : -1L,
                starActActivationCount = runtime.GameHud != null &&
                                         runtime.GameHud.StarActScore != null
                    ? runtime.GameHud.StarActScore.ActivationCount
                    : -1,
                maxScore = runtime.GameHud != null && runtime.GameHud.Score != null
                    ? runtime.GameHud.Score.MaxScore
                    : -1L,
                life = runtime.GameHud != null && runtime.GameHud.Life != null
                    ? runtime.GameHud.Life.Value
                    : -1,
                maxLife = runtime.GameHud != null && runtime.GameHud.Life != null
                    ? runtime.GameHud.Life.MaxValue
                    : -1,
                principal = runtime.GameHud != null &&
                            runtime.GameHud.Principal != null &&
                            runtime.GameHud.Principal.IsInitialized
                    ? runtime.GameHud.Principal.GetCurrentPrincipal(
                        runtime.GameHud.Principal.DefaultOrder)
                    : -1,
                maxPrincipal = runtime.GameHud != null &&
                               runtime.GameHud.Principal != null &&
                               runtime.GameHud.Principal.IsInitialized
                    ? runtime.GameHud.Principal.GetMaxPrincipal(
                        runtime.GameHud.Principal.DefaultOrder)
                    : -1,
                gameResultPresentationCount =
                    runtime.GameResultPresentationCount,
                pauseSmokePassed = SessionState.GetBool(PauseSmokeKey, false),
                senseCutInVisualObserved =
                    SessionState.GetBool(SenseCutInVisualKey, false),
                introductionVisualObserved =
                    SessionState.GetBool(IntroductionVisualKey, false),
                clearVisualObserved =
                    SessionState.GetBool(ClearVisualKey, false),
            };
            var panel = UnityEngine.Object.FindObjectOfType<
                Sirius.GameResult.GameResultPanel>(true);
            if (panel != null)
            {
                var serializedPanel = new SerializedObject(panel);
                var detailed = serializedPanel
                    .FindProperty("_resultPanelCanvasGroup")
                    ?.objectReferenceValue as CanvasGroup;
                var autoHidden = serializedPanel
                    .FindProperty("_autoHiddenPanelCanvasGroup")
                    ?.objectReferenceValue as CanvasGroup;
                report.detailedResultVisible =
                    detailed != null && detailed.alpha > 0.99f;
                report.autoplayResultUiHidden =
                    autoHidden != null && autoHidden.alpha < 0.01f;

                var ratePanel = panel.RatePanel != null
                    ? new SerializedObject(panel.RatePanel)
                    : null;
                var notationLabel = ratePanel?.FindProperty("_notationRateText")
                    ?.objectReferenceValue as Text;
                var playerLabel = ratePanel?.FindProperty("_playerRateText")
                    ?.objectReferenceValue as Text;
                var notationValue = notationLabel != null
                    ? notationLabel.transform.parent?.parent
                        ?.Find("RatePanel/ThisTimeRate")
                        ?.GetComponent<Text>()
                    : null;
                var playerValue = playerLabel != null
                    ? playerLabel.transform.parent?.parent
                        ?.Find("RatePanel/ThisTimeRate")
                        ?.GetComponent<Text>()
                    : null;
                report.resultRatingsVisible =
                    IsVisibleRateText(notationValue) &&
                    IsVisibleRateText(playerValue);
                report.resultNotationRating = ParseRateText(notationValue);
                report.resultPlayerRating = ParseRateText(playerValue);
                report.resultRatingDiagnostic =
                    DescribeRateText("notation", notationValue) + ";" +
                    DescribeRateText("player", playerValue);

                var musicInfo = panel.transform.parent?.Find("MusicInfoPanel");
                var difficultyImage = musicInfo?.Find("Difficulty")
                    ?.GetComponent<Image>();
                var requestedDifficulty = Enum.TryParse(
                    report.difficulty, true,
                    out RecoveredMusicDifficulty parsedDifficulty)
                    ? parsedDifficulty
                    : RecoveredMusicDifficulty.Stella;
                report.resultDifficultyColorValid = difficultyImage != null &&
                    (requestedDifficulty != RecoveredMusicDifficulty.Stella ||
                     ((Color32)difficultyImage.color).Equals(
                         new Color32(134, 103, 233, 255)));
                var lamps = musicInfo?.Find("ClearLamps");
                report.resultLampCount = 0;
                if (lamps != null)
                {
                    for (var index = 0; index < lamps.childCount; index++)
                    {
                        if (lamps.GetChild(index).gameObject.activeSelf)
                            report.resultLampCount++;
                    }
                }
            }
            report.collectedCount = report.perfectStar + report.perfect +
                                    report.great + report.good +
                                    report.bad + report.miss;
            return report;
        }

        private static bool IsVisibleRateText(Text text)
        {
            if (text == null || !text.gameObject.activeInHierarchy ||
                string.IsNullOrEmpty(text.text))
                return false;
            foreach (var group in text.GetComponentsInParent<CanvasGroup>(true))
            {
                if (group.alpha < 0.01f) return false;
            }
            return true;
        }

        private static double ParseRateText(Text text)
        {
            return text != null && double.TryParse(
                text.text,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value)
                ? value
                : 0d;
        }

        private static string DescribeRateText(string name, Text text)
        {
            if (text == null) return name + "=null";
            var groups = text.GetComponentsInParent<CanvasGroup>(true)
                .Select(group => group.name + ":" +
                                 group.alpha.ToString("0.00"));
            return name + "=" + text.text +
                   ",active=" + text.gameObject.activeInHierarchy +
                   ",enabled=" + text.enabled +
                   ",colorA=" + text.color.a.ToString("0.00") +
                   ",groups=[" + string.Join(",", groups) + "]";
        }

        private static void ConfigureRequestedChart()
        {
            var runtime = UnityEngine.Object.FindObjectOfType<RecoveredGameRuntime>();
            if (runtime == null)
                throw new InvalidOperationException(
                    "Offline preview runtime is missing.");
            var runtimeObject = new SerializedObject(runtime);
            runtimeObject.FindProperty("_enableAutoJudge").boolValue = true;

            var rawMusicId = Environment.GetEnvironmentVariable(
                "OPENWDS_AUTO_JUDGE_MUSIC_ID");
            if (string.IsNullOrWhiteSpace(rawMusicId))
            {
                runtimeObject.ApplyModifiedPropertiesWithoutUndo();
                SessionState.SetBool(TargetedChartKey, false);
                SessionState.SetInt(MusicIdKey, 1);
                SessionState.SetString(
                    DifficultyKey, RecoveredMusicDifficulty.Stella.ToString());
                return;
            }
            if (!long.TryParse(rawMusicId, out var musicId) || musicId <= 0)
                throw new ArgumentException(
                    "OPENWDS_AUTO_JUDGE_MUSIC_ID must be a positive integer.");

            var rawDifficulty = Environment.GetEnvironmentVariable(
                "OPENWDS_AUTO_JUDGE_DIFFICULTY");
            if (string.IsNullOrWhiteSpace(rawDifficulty))
                rawDifficulty = RecoveredMusicDifficulty.Stella.ToString();
            if (!Enum.TryParse(
                    rawDifficulty, true,
                    out RecoveredMusicDifficulty difficulty) ||
                difficulty == RecoveredMusicDifficulty.None)
                throw new ArgumentException(
                    "OPENWDS_AUTO_JUDGE_DIFFICULTY is invalid: " + rawDifficulty);

            const string catalogPath =
                "Assets/OpenWDS/OfflineData/LocalMusicCatalog.json";
            var catalogAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(catalogPath);
            if (catalogAsset == null)
                throw new FileNotFoundException(
                    "Local music catalog is missing.", catalogPath);
            var selection = RecoveredLocalMusicCatalog
                .FromJson(catalogAsset.text)
                .Select(musicId, difficulty);
            var chartPath = "Assets/StreamingAssets/" +
                            selection.Live.DebugNotationAssetPath;
            var configPath = "Assets/StreamingAssets/" +
                             selection.Live.DebugMusicConfigAssetPath;
            if (!File.Exists(chartPath) || !File.Exists(configPath))
                throw new FileNotFoundException(
                    $"Requested chart assets are missing: {chartPath}, {configPath}");

            var criMusic = UnityEngine.Object.FindObjectOfType<
                RecoveredCriMusicRuntime>();
            if (criMusic == null)
                throw new InvalidOperationException(
                    "Offline preview CRI music runtime is missing.");

            runtimeObject.FindProperty("_chartAsset").objectReferenceValue = null;
            runtimeObject.FindProperty("_musicConfigAsset").objectReferenceValue = null;
            runtimeObject.FindProperty("_chartRelativePath").stringValue =
                selection.Live.DebugNotationAssetPath;
            runtimeObject.FindProperty("_musicConfigRelativePath").stringValue =
                selection.Live.DebugMusicConfigAssetPath;
            runtimeObject.FindProperty("_musicName").stringValue =
                selection.Music.Name;
            runtimeObject.FindProperty("_musicInfo").stringValue = string.Format(
                "作詞：{0}　作曲：{1}　編曲：{2}",
                selection.Music.LyricWriter,
                selection.Music.Composer,
                selection.Music.Arranger);
            runtimeObject.FindProperty("_musicDifficulty").enumValueIndex =
                (int)difficulty;
            var jacket = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"Assets/OpenWDS/OfflineData/Music{musicId}Jacket.png");
            runtimeObject.FindProperty("_musicJacketSprite").objectReferenceValue =
                jacket;
            runtimeObject.ApplyModifiedPropertiesWithoutUndo();

            var criObject = new SerializedObject(criMusic);
            criObject.FindProperty("_musicConfigAsset").objectReferenceValue = null;
            criObject.FindProperty("_musicConfigRelativePath").stringValue =
                selection.Live.DebugMusicConfigAssetPath;
            criObject.FindProperty("_acbRelativePath").stringValue =
                $"OpenWDS/StandardCharts/{musicId}/cri/music_{musicId}.acb.bundle";
            criObject.FindProperty("_cueName").stringValue = musicId.ToString();
            criObject.ApplyModifiedPropertiesWithoutUndo();

            SessionState.SetBool(TargetedChartKey, true);
            SessionState.SetInt(MusicIdKey, checked((int)musicId));
            SessionState.SetInt(
                MusicTimeSecondsKey, selection.Music.MusicTimeSecond);
            SessionState.SetString(DifficultyKey, difficulty.ToString());
            Debug.Log(
                "OPENWDS_PLAYMODE_AUTO_JUDGE_TARGET " +
                $"musicId={musicId} difficulty={difficulty} chart={chartPath}");
        }

        private static void ObserveBoundaryPerformances(
            RecoveredGameRuntime runtime)
        {
            if (!SessionState.GetBool(IntroductionVisualKey, false) &&
                runtime.IsIntroductionPlaying)
            {
                var introduction = UnityEngine.Object.FindObjectOfType<
                    Sirius.Game.GameIntroductionAnimationController>();
                if (introduction != null && introduction.gameObject.activeInHierarchy &&
                    !introduction.IsUnlockOlivier &&
                    introduction.Jacket != null &&
                    introduction.MusicName == "ワナビスタ！" &&
                    introduction.MusicInfo.Contains("松井洋平"))
                    SessionState.SetBool(IntroductionVisualKey, true);
            }
            var activeIntroduction = UnityEngine.Object.FindObjectOfType<
                Sirius.Game.GameIntroductionAnimationController>();
            if (activeIntroduction != null &&
                (activeIntroduction.IsNormalAnimationPlaying ||
                 activeIntroduction.IsUnlockAnimationPlaying))
            {
                var capturePrefix = activeIntroduction.IsUnlockOlivier
                    ? "game-introduction-olivier"
                    : "game-introduction-normal";
                for (var index = 1; index <= 9; index++)
                    CaptureIntroductionFrame(
                        runtime, activeIntroduction,
                        "OpenWDS.AutoplayPlayMode.IntroductionFrame" + index,
                        index / 10f,
                        $"{capturePrefix}-{index:00}.png");
                CaptureIntroductionFrame(
                    runtime, activeIntroduction, IntroductionFrameEarlyKey,
                    0.30f, $"{capturePrefix}-early.png");
                CaptureIntroductionFrame(
                    runtime, activeIntroduction, IntroductionFrameLateKey,
                    0.68f, $"{capturePrefix}-late.png");
            }
            if (runtime.IsClearPerformancePlaying)
            {
                var clear = UnityEngine.Object.FindObjectOfType<
                    Sirius.Game.GameResultPanel>();
                if (clear != null && clear.gameObject.activeInHierarchy &&
                    clear.IsPlaying &&
                    clear.ClearType ==
                    Sirius.Game.RecoveredBoundaryClearType.AllPerfect)
                {
                    if (!SessionState.GetBool(ClearVisualKey, false))
                        SessionState.SetBool(ClearVisualKey, true);
                    CaptureClearFrame(
                        runtime, clear, ClearFrameKey, 0.5f,
                        "game-clear-allperfect-middle.png");
                }
            }
        }

        private static void CaptureClearFrame(
            RecoveredGameRuntime runtime,
            Sirius.Game.GameResultPanel clear,
            string sessionKey,
            float normalizedTime,
            string fileName)
        {
            if (SessionState.GetBool(sessionKey, false) ||
                clear.NormalizedTime < normalizedTime) return;
            var output = Path.GetFullPath(Path.Combine(
                Application.dataPath, "../../../reverse/reports", fileName));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            const int width = 1920;
            const int height = 1200;
            var camera = runtime.GameCamera;
            var canvas = clear.GetComponentInParent<Canvas>();
            if (camera == null || canvas == null)
                throw new InvalidOperationException(
                    "ClearAnimation capture requires the game camera and canvas.");
            var target = new RenderTexture(
                width, height, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(
                width, height, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture;
            var previousAspect = camera.aspect;
            var previousActive = RenderTexture.active;
            var previousRenderMode = canvas.renderMode;
            var previousWorldCamera = canvas.worldCamera;
            var previousPlaneDistance = canvas.planeDistance;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(output, image.EncodeToPNG());
                var details = clear.GetComponentsInChildren<Image>(true)
                    .Where(value =>
                        value.gameObject.activeInHierarchy && value.enabled)
                    .Select(value =>
                        $"{GetTransformPath(clear.transform, value.transform)}" +
                        $"[sprite={(value.sprite != null ? value.sprite.name : "null")}," +
                        $"rgba={value.color.r:F2}/{value.color.g:F2}/" +
                        $"{value.color.b:F2}/{value.color.a:F2}," +
                        $"material={(value.material != null ? value.material.name : "null")}," +
                        $"shader={(value.material != null && value.material.shader != null ? value.material.shader.name : "null")}," +
                        $"culled={value.canvasRenderer.cull}]");
                Debug.Log(
                    "OPENWDS_GAME_CLEAR_IMAGE_DETAILS " +
                    string.Join(";", details));
            }
            finally
            {
                canvas.renderMode = previousRenderMode;
                canvas.worldCamera = previousWorldCamera;
                canvas.planeDistance = previousPlaneDistance;
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
                Canvas.ForceUpdateCanvases();
            }
            SessionState.SetBool(sessionKey, true);
            Debug.Log(
                "OPENWDS_GAME_CLEAR_CAPTURE " +
                $"type={clear.ClearType} normalizedTime={clear.NormalizedTime:F3} " +
                $"output={output}");
        }

        private static void CaptureIntroductionFrame(
            RecoveredGameRuntime runtime,
            Sirius.Game.GameIntroductionAnimationController introduction,
            string sessionKey, float normalizedTime, string fileName)
        {
            if (SessionState.GetBool(sessionKey, false) ||
                introduction.NormalizedTime < normalizedTime) return;
            var output = Path.GetFullPath(Path.Combine(
                Application.dataPath, "../../../reverse/reports", fileName));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            const int width = 1920;
            const int height = 1200;
            var camera = runtime.GameCamera;
            var canvas = introduction.GetComponentInParent<Canvas>();
            if (camera == null || canvas == null)
                throw new InvalidOperationException(
                    "Introduction capture requires the game camera and canvas.");
            var target = new RenderTexture(
                width, height, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(
                width, height, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture;
            var previousAspect = camera.aspect;
            var previousActive = RenderTexture.active;
            var previousRenderMode = canvas.renderMode;
            var previousWorldCamera = canvas.worldCamera;
            var previousPlaneDistance = canvas.planeDistance;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(output, image.EncodeToPNG());
                var images = introduction.GetComponentsInChildren<Image>(true);
                var activeImages = images.Count(value =>
                    value.gameObject.activeInHierarchy && value.enabled);
                var visibleImages = images.Count(value =>
                    value.gameObject.activeInHierarchy && value.enabled &&
                    value.sprite != null && value.color.a > 0.001f);
                Debug.Log(
                    "OPENWDS_GAME_INTRODUCTION_IMAGES " +
                    $"normalTime={introduction.NormalizedTime:F3} " +
                    $"total={images.Length} active={activeImages} " +
                    $"visible={visibleImages}");
                if (Mathf.Abs(introduction.NormalizedTime - 0.5f) < 0.01f)
                {
                    var details = images
                        .Where(value =>
                            value.gameObject.activeInHierarchy && value.enabled)
                        .Select(value =>
                            $"{GetTransformPath(introduction.transform, value.transform)}" +
                            $"[sprite={(value.sprite != null ? value.sprite.name : "null")}," +
                            $"rgba={value.color.r:F2}/{value.color.g:F2}/" +
                            $"{value.color.b:F2}/{value.color.a:F2}," +
                            $"material={(value.material != null ? value.material.name : "null")}," +
                            $"shader={(value.material != null && value.material.shader != null ? value.material.shader.name : "null")}," +
                            $"culled={value.canvasRenderer.cull}]");
                    Debug.Log(
                        "OPENWDS_GAME_INTRODUCTION_IMAGE_DETAILS " +
                        string.Join(";", details));
                }
            }
            finally
            {
                canvas.renderMode = previousRenderMode;
                canvas.worldCamera = previousWorldCamera;
                canvas.planeDistance = previousPlaneDistance;
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
                Canvas.ForceUpdateCanvases();
            }
            SessionState.SetBool(sessionKey, true);
            Debug.Log(
                "OPENWDS_GAME_INTRODUCTION_CAPTURE " +
                $"normalTime={introduction.NormalizedTime:F3} output={output}");
        }

        private static string GetTransformPath(Transform root, Transform value)
        {
            var path = value.name;
            while (value.parent != null && value.parent != root)
            {
                value = value.parent;
                path = value.name + "/" + path;
            }
            return path;
        }

        private static void ObserveSenseCutInVisual(RecoveredGameRuntime runtime)
        {
            if (SessionState.GetBool(SenseCutInVisualKey, false) ||
                runtime.GameHud == null)
                return;
            var panel = runtime.GameHud.AdditionalScoreCutInPanel;
            if (panel == null || !panel.HasVisibleCutInArtwork) return;
            var expectedPosition = panel.ConfiguredScreenPosition;
            if (Vector3.Distance(
                    panel.ParentScreenPosition, expectedPosition) > 0.01f)
                throw new InvalidOperationException(
                    "Formal Sense cut-in does not use the projected " +
                    "Game/Effects position: actual=" +
                    panel.ParentScreenPosition + ", expected=" +
                    expectedPosition + ".");
            // The Animate trigger transitions on the Animator's following
            // update, so do not reject the same frame that emitted Sense.
            if (!panel.LastAnimationUsedOriginalStates) return;
            SessionState.SetBool(SenseCutInVisualKey, true);
            Debug.Log(
                "OPENWDS_PLAYMODE_SENSE_CUTIN_VISIBLE " +
                $"activations={panel.ActivationCount} " +
                $"screen={panel.ParentScreenPosition} " +
                "animations=CutIn_anim/SenceCutIn_anim");
        }

        private static bool RunPauseSmoke(
            RecoveredGameRuntime runtime, double elapsed)
        {
            var pause = UnityEngine.Object.FindObjectOfType<
                RecoveredGamePauseRuntime>();
            if (pause == null || pause.CurrentSettings == null) return false;

            try
            {
                var before = pause.CurrentSettings.GameSettings.NoteSpeed;
                var volumeBefore =
                    pause.CurrentSettings.SoundVolumeSettings.GameMaster;
                var textSpeedBefore =
                    pause.CurrentSettings.SystemSettings.TextDisplaySpeed;
                var bluetoothTimingBefore =
                    pause.CurrentSettings.BluetoothSettings.NoteTimingValue;
                var senseDisplayBefore =
                    pause.CurrentSettings.GameSettings.IsActiveSenseDisplay;
                var laneWidthBefore =
                    pause.CurrentSettings.GameDetailSettings.LaneWidth;
                var laneAlphaBefore =
                    pause.CurrentSettings.GameSettings.LaneAlphaValue;
                var noteHeightBefore =
                    pause.CurrentSettings.GameDetailSettings.NoteHeight;
                var expectedTimingY =
                    RecoveredGameHudRuntime.CalculatePositionY(
                        pause.CurrentSettings.GameDetailSettings
                            .TimingEffectOffset);
                var expectedTimingScale =
                    RecoveredGameHudRuntime.GetTimingEffectScale(
                        pause.CurrentSettings.GameDetailSettings
                            .TimingEffectScaleType);
                var formalLaneGroup =
                    UnityEngine.Object.FindObjectOfType<Sirius.Game.LaneGroup>();
                if (runtime.GameHud == null ||
                    runtime.GameHud.TimingParent == null ||
                    runtime.GameHud.TimingParent.name != "Effects" ||
                    formalLaneGroup == null ||
                    runtime.NoteVisuals == null ||
                    Math.Abs(
                        runtime.NoteVisuals.OffsetValue -
                        pause.CurrentSettings.GameSettings.NoteOffsetValue) >
                        0.000001d ||
                    Math.Abs(
                        formalLaneGroup.LaneBorderAlpha -
                        Sirius.Game.LaneGroup.CalculateLaneBorderAlpha(
                            laneAlphaBefore)) > 0.0001f ||
                    Math.Abs(
                        formalLaneGroup.LaneDarknessAlpha -
                        0.8f * laneAlphaBefore / 100f) > 0.0001f ||
                    Math.Abs(
                        formalLaneGroup.LaneScaleX -
                        RecoveredGameSettings.CalculateLaneScale(
                            laneWidthBefore)) > 0.0001f ||
                    runtime.GameHud.TimingParent ==
                        formalLaneGroup.LaneEffectParent ||
                    Vector3.Distance(
                        runtime.GameHud.TimingParent.localPosition,
                        new Vector3(0f, expectedTimingY, 10f)) > 0.0001f ||
                    Math.Abs(runtime.GameHud.TimingPositionY -
                             expectedTimingY) > 0.0001f ||
                    Math.Abs(runtime.GameHud.TimingScale -
                             expectedTimingScale) > 0.0001f ||
                    (runtime.GameHud.LastTimingEffect != null &&
                     Math.Abs(
                         runtime.GameHud.LastTimingEffect.transform.localPosition.y -
                         0f) > 0.0001f))
                    throw new InvalidOperationException(
                        "Formal gameplay TimingEffect does not apply the " +
                        "persisted machine-code position/scale calculation.");
                // Retail gameplay automatically opens Pause on focus loss.
                pause.SendMessage(
                    "OnApplicationFocus", false,
                    SendMessageOptions.RequireReceiver);
                if (!pause.IsDialogOpen || !pause.IsPauseMenuOpen ||
                    !runtime.IsPaused)
                    throw new InvalidOperationException(
                        "Focus loss did not open Pause and pause the game.");
                var gameSe = UnityEngine.Object.FindObjectOfType<
                    RecoveredGameSeRuntime>();
                var criMusic = UnityEngine.Object.FindObjectOfType<
                    RecoveredCriMusicRuntime>();
                if (gameSe == null || gameSe.HoldingCount != 0 ||
                    gameSe.IsHoldingPlaybackActive)
                    throw new InvalidOperationException(
                        "Pause did not stop the shared hold-note playback.");
                if (criMusic == null || !criMusic.IsGamePaused ||
                    !criMusic.IsPaused)
                    throw new InvalidOperationException(
                        "Pause did not retain the CRI game-pause reason.");
                criMusic.SendMessage(
                    "OnApplicationFocus", false,
                    SendMessageOptions.RequireReceiver);
                criMusic.SendMessage(
                    "OnApplicationFocus", true,
                    SendMessageOptions.RequireReceiver);
                if (!criMusic.IsGamePaused || !criMusic.IsPaused ||
                    criMusic.IsPlaying)
                    throw new InvalidOperationException(
                        "Focus regain resumed music through an active game pause.");
                var timingAssistSettingField =
                    typeof(RecoveredGameHudRuntime).GetField(
                        "_timingAssistSettingType",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                if (runtime.GameHud == null ||
                    timingAssistSettingField == null)
                    throw new InvalidOperationException(
                        "Timing assist HUD setting field is unavailable.");
                var persistedTimingAssist =
                    pause.CurrentSettings.GameDetailSettings
                        .TimingAssistSettingType;
                timingAssistSettingField.SetValue(runtime.GameHud, 2);
                var assistNote = new RecoveredNotationNote
                {
                    Id = -100,
                    StartTickCount = 1f,
                    NoteType = (int)RecoveredNoteType.Normal,
                    Lane = 6,
                    Width = 1,
                };
                var assistDecision = new RecoveredTimingDecision(
                    RecoveredTimingType.Great,
                    RecoveredTimingAssistType.Fast,
                    -60);
                var assistResult = RecoveredInputResultEntity.Create(
                    assistNote, assistDecision);
                var assistCombo = new RecoveredGameResultRuntime(
                    new[] { assistNote });
                assistCombo.Collect(assistResult);
                runtime.GameHud.ProcessFrame(
                    new[] { assistResult }, assistCombo);
                timingAssistSettingField.SetValue(
                    runtime.GameHud, persistedTimingAssist);
                var assistEffect = runtime.GameHud.LastTimingAssistEffect;
                if (assistEffect == null ||
                    assistEffect.CurrentSprite == null ||
                    assistEffect.CurrentSprite.name !=
                        "txt_game_common_judgment_fast" ||
                    Math.Abs(
                        assistEffect.transform.localPosition.y -
                        0f) > 0.0001f ||
                    Math.Abs(
                        assistEffect.transform.localScale.x -
                        expectedTimingScale) > 0.0001f)
                    throw new InvalidOperationException(
                        "Timing assist did not play the original Fast visual " +
                        "with persisted timing position/scale.");
                Debug.Log(
                    "OPENWDS_PLAYMODE_TIMING_ASSIST_VISIBLE " +
                    $"sprite={assistEffect.CurrentSprite.name} " +
                    $"parentY={runtime.GameHud.TimingParent.localPosition.y:F3} " +
                    $"localY={assistEffect.transform.localPosition.y:F3} " +
                    $"scale={assistEffect.transform.localScale.x:F3}");
                var comboEffectSettingField =
                    typeof(RecoveredGameHudRuntime).GetField(
                        "_isActiveComboEffect",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                if (comboEffectSettingField == null)
                    throw new InvalidOperationException(
                        "Combo effect HUD setting field is unavailable.");
                var persistedComboEffect =
                    pause.CurrentSettings.GameDetailSettings.IsActiveComboEffect;
                var perfectStarDecision = new RecoveredTimingDecision(
                    RecoveredTimingType.PerfectStar,
                    RecoveredTimingAssistType.None,
                    0);
                var perfectStarResult = RecoveredInputResultEntity.Create(
                    assistNote, perfectStarDecision);
                var perfectStarCombo = new RecoveredGameResultRuntime(
                    new[] { assistNote });
                perfectStarCombo.Collect(perfectStarResult);
                comboEffectSettingField.SetValue(runtime.GameHud, false);
                runtime.GameHud.ProcessFrame(
                    new[] { perfectStarResult }, perfectStarCombo);
                if (runtime.GameHud.ComboPanel.ComboType !=
                    Sirius.Game.UI.RecoveredComboType.None)
                    throw new InvalidOperationException(
                        "Disabled combo effect did not force the retail normal style.");
                comboEffectSettingField.SetValue(runtime.GameHud, true);
                runtime.GameHud.ProcessFrame(
                    new[] { perfectStarResult }, perfectStarCombo);
                if (runtime.GameHud.ComboPanel.ComboType !=
                    Sirius.Game.UI.RecoveredComboType.AllPerfect)
                    throw new InvalidOperationException(
                        "Enabled combo effect did not retain the All Perfect style.");
                comboEffectSettingField.SetValue(
                    runtime.GameHud, persistedComboEffect);
                Debug.Log(
                    "OPENWDS_PLAYMODE_COMBO_EFFECT_PASS " +
                    "disabled=None enabled=AllPerfect animations=retained");
                var touchBlock = GameObject.Find("TouchBlock");
                var pauseDialog = GameObject.Find("Dialog");
                var pauseDialogCanvas = pauseDialog != null
                    ? pauseDialog.GetComponent<Canvas>()
                    : null;
                var hudParent = runtime.GameHud != null
                    ? runtime.GameHud.CanvasTransform
                    : null;
                if (touchBlock == null || hudParent == null ||
                    touchBlock.transform.parent != hudParent ||
                    pauseDialogCanvas == null ||
                    !pauseDialogCanvas.overrideSorting ||
                    pauseDialogCanvas.sortingOrder != 110 ||
                    pauseDialog.GetComponent<GraphicRaycaster>() == null)
                    throw new InvalidOperationException(
                        "TouchBlock/Dialog Canvas layering does not match the " +
                        "shared HUD parent and front sorting order 110.");
                var resumeButton = FindNamedDescendant(
                    pauseDialog.transform, "ThirdButton");
                var resumeImage = resumeButton != null
                    ? resumeButton.GetComponent<Image>()
                    : null;
                var resumeText = resumeButton != null
                    ? resumeButton.GetComponentInChildren<Text>(true)
                    : null;
                if (resumeImage == null || resumeImage.sprite == null ||
                    resumeImage.sprite.name != "btn_common_btn_large_red" ||
                    resumeText == null || resumeText.color != Color.white)
                    throw new InvalidOperationException(
                        "Pause resume button is not the original positive red button.");
                pause.OpenSettings();
                if (!pause.IsSettingsOpen || pause.IsPauseMenuOpen)
                    throw new InvalidOperationException(
                        "Pause SettingsButton did not open OptionDialogBody.");
                // Destroy() keeps the previous pause Dialog alive until the end
                // of this frame. Resolve the new shell through its unique body
                // instead of relying on GameObject.Find("Dialog") ordering.
                var settingsBodyObject = GameObject.Find("OptionDialogBody");
                var settingsDialog =
                    settingsBodyObject != null &&
                    settingsBodyObject.transform.parent != null &&
                    settingsBodyObject.transform.parent.parent != null
                        ? settingsBodyObject.transform.parent.parent.gameObject
                        : null;
                var settingsDialogRect = settingsDialog != null
                    ? settingsDialog.GetComponent<RectTransform>()
                    : null;
                if (settingsDialogRect == null ||
                    Vector2.Distance(
                        settingsDialogRect.sizeDelta,
                        new Vector2(1328f, 996f)) > 0.01f)
                    throw new InvalidOperationException(
                        "Settings did not use DialogType Height_960_Medium.");
                var settingsBody = settingsBodyObject != null
                    ? settingsBodyObject.transform
                    : null;
                var okButton = FindNamedDescendant(
                    settingsDialog.transform, "SecondButton");
                var okImage = okButton != null
                    ? okButton.GetComponent<UnityEngine.UI.Image>()
                    : null;
                var okText = okButton != null
                    ? okButton.GetComponentInChildren<Text>(true)
                    : null;
                if (settingsBody == null || settingsBody.parent == null ||
                    settingsBody.parent.name != "Body" ||
                    okImage == null ||
                    okImage.sprite == null ||
                    okImage.sprite.name != "btn_common_btn_large_red" ||
                    okText == null ||
                    okText.color != Color.white)
                    throw new InvalidOperationException(
                        "Settings Dialog hierarchy or white positive OK button is invalid.");
                var animatedButtonCount = 0;
                foreach (var animator in settingsBody.GetComponentsInChildren<
                    Animator>(true))
                {
                    if (animator.runtimeAnimatorController == null ||
                        animator.runtimeAnimatorController.name != "ButtonScale")
                        continue;
                    var animatedButton = animator.GetComponent<Button>();
                    if (animatedButton != null &&
                        animatedButton.transition ==
                            Selectable.Transition.Animation)
                        animatedButtonCount++;
                }
                if (animatedButtonCount == 0)
                    throw new InvalidOperationException(
                        "Settings ButtonScale Animator controllers were not rebound.");
                foreach (var label in settingsBody.GetComponentsInChildren<
                    Text>(true))
                {
                    if (label.font == null)
                        throw new InvalidOperationException(
                            $"Settings label {label.name} has no recovered font.");
                }
                var detailPanel = FindNamedDescendant(
                    settingsBody, "GameDetailSettingsPanel");
                var timingScaleRoot = FindNamedDescendant(
                    detailPanel, "TimingScaleSettings");
                var timingScaleValue = FindNamedDescendant(
                    timingScaleRoot, "CurrentValueText")?.GetComponent<Text>();
                var timingScaleLabels = new[] { "小", "中", "大" };
                var timingScaleIndex = Mathf.Clamp(
                    pause.CurrentSettings.GameDetailSettings.TimingEffectScaleType,
                    0, timingScaleLabels.Length - 1);
                if (timingScaleValue == null ||
                    timingScaleValue.text != timingScaleLabels[timingScaleIndex])
                    throw new InvalidOperationException(
                        "TimingEffectScaleType is not displayed as 小/中/大.");
                var sideMenuButtonCount = 0;
                foreach (var child in settingsBody.GetComponentsInChildren<
                    Transform>(true))
                {
                    if (child.name.StartsWith(
                        "TextSideMenuButton_", StringComparison.Ordinal))
                        sideMenuButtonCount++;
                }
                if (sideMenuButtonCount != 4)
                    throw new InvalidOperationException(
                        $"Pause settings expected four side-menu buttons, " +
                        $"found {sideMenuButtonCount}.");
                foreach (var image in settingsBody.GetComponentsInChildren<
                    UnityEngine.UI.Image>(true))
                {
                    var sprite = image.sprite;
                    if (sprite == null ||
                        !sprite.name.StartsWith(
                            "btn_common", StringComparison.Ordinal))
                        continue;
                    var spritePath = AssetDatabase.GetAssetPath(sprite);
                    if (sprite.packed ||
                        !spritePath.StartsWith(
                            "Assets/Resources/Sprite/",
                            StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            $"Settings button {sprite.name} is still using a " +
                            $"packed/non-standalone Sprite export ({spritePath}).");
                }
                var informationButtonCount = 0;
                foreach (var child in settingsBody.GetComponentsInChildren<
                    Transform>(true))
                {
                    if (child.name != "InformationButton") continue;
                    informationButtonCount++;
                    var circle = child.GetComponent<Image>();
                    if (circle == null || circle.sprite == null ||
                        circle.sprite.name != "btn_common_btn_circle_72px")
                        throw new InvalidOperationException(
                            "InformationButton circle Sprite is missing.");
                }
                if (informationButtonCount == 0)
                    throw new InvalidOperationException(
                        "Settings has no InformationButton to validate.");
                var gameInformationButton =
                    FindNamedDescendant(settingsBody, "GameSettingsPanel")
                        ?.GetComponentsInChildren<Button>(true)
                        .FirstOrDefault(candidate =>
                            candidate.name == "InformationButton");
                if (gameInformationButton == null)
                    throw new InvalidOperationException(
                        "GameSettings InformationButton is not interactive.");
                gameInformationButton.onClick.Invoke();
                var informationDialog =
                    GameObject.Find("OptionInformationDialog");
                var informationBody = informationDialog != null
                    ? FindNamedDescendant(
                        informationDialog.transform,
                        "OptionInformationDialogBody")
                    : null;
                var gameInformation = informationBody != null
                    ? FindNamedDescendant(informationBody, "GameSettings")
                    : null;
                var laneInformation = informationBody != null
                    ? FindNamedDescendant(informationBody, "LaneSettings")
                    : null;
                if (informationDialog == null || informationBody == null ||
                    gameInformation == null || !gameInformation.gameObject.activeSelf ||
                    laneInformation == null || laneInformation.gameObject.activeSelf)
                    throw new InvalidOperationException(
                        "InformationButton did not open the original filtered " +
                        "OptionInformationDialogBody.");
                var informationOk = FindNamedDescendant(
                    informationDialog.transform, "SecondButton")
                    ?.GetComponent<Button>();
                if (informationOk == null)
                    throw new InvalidOperationException(
                        "OptionInformation dialog OK button is missing.");
                informationOk.onClick.Invoke();

                var senseSettings = FindNamedDescendant(
                    settingsBody, "SenseDisplaySettings");
                var senseOn = senseSettings != null
                    ? FindNamedDescendant(senseSettings, "On")?.GetComponent<Button>()
                    : null;
                var senseOff = senseSettings != null
                    ? FindNamedDescendant(senseSettings, "Off")?.GetComponent<Button>()
                    : null;
                var selectedSense = senseDisplayBefore ? senseOff : senseOn;
                if (senseOn == null || senseOff == null || selectedSense == null)
                    throw new InvalidOperationException(
                        "SenseDisplay Radio buttons are missing.");
                selectedSense.onClick.Invoke();
                var expectedSense = !senseDisplayBefore;
                var onInner = FindNamedDescendant(senseOn.transform, "InnerImage");
                var offInner = FindNamedDescendant(senseOff.transform, "InnerImage");
                var onSprite = onInner != null
                    ? onInner.GetComponent<Image>()?.sprite
                    : null;
                var offSprite = offInner != null
                    ? offInner.GetComponent<Image>()?.sprite
                    : null;
                if (pause.CurrentSettings.GameSettings.IsActiveSenseDisplay !=
                        expectedSense ||
                    onInner == null || offInner == null ||
                    !onInner.gameObject.activeSelf ||
                    !offInner.gameObject.activeSelf ||
                    onSprite == null || offSprite == null ||
                    onSprite.name != (expectedSense
                        ? "btn_common_btn_switch_filter_on"
                        : "btn_common_btn_switch_filter_off") ||
                    offSprite.name != (expectedSense
                        ? "btn_common_btn_switch_filter_off"
                        : "btn_common_btn_switch_filter_on"))
                    throw new InvalidOperationException(
                        "Radio did not swap its original ToggleImage on/off Sprites.");

                var displayTimeRoot = FindNamedDescendant(
                    settingsBody, "NoteDisplayTimeSettings");
                var displayTimeObject = displayTimeRoot != null
                    ? FindNamedDescendant(displayTimeRoot, "NoteDisplayTime")
                    : null;
                var displayTimeText = displayTimeObject != null
                    ? displayTimeObject.GetComponent<Text>()
                    : null;
                var expectedDisplayTime =
                    RecoveredGameSettings.CalculateNoteDisplayTime(
                        pause.CurrentSettings.GameDetailSettings.NoteStartOffset,
                        before).ToString();
                if (displayTimeText == null ||
                    displayTimeText.text != expectedDisplayTime)
                    throw new InvalidOperationException(
                        "NoteDisplayTimeSettings did not receive the recovered " +
                        $"value {expectedDisplayTime}.");

                var simulationView = GameObject.Find("GameSimulationView");
                var simulationRawImage = simulationView != null
                    ? simulationView.GetComponentInChildren<RawImage>(true)
                    : null;
                var simulationRoot = GameObject.Find("GameSimulation");
                var simulationNote = simulationRoot != null
                    ? FindNamedDescendant(
                        simulationRoot.transform, "SimulationNote")
                    : null;
                var simulationCamera = simulationRoot != null
                    ? simulationRoot.GetComponentInChildren<Camera>(true)
                    : null;
                var simulationCameraController = simulationCamera != null
                    ? simulationCamera.GetComponent<
                        Sirius.GameSimulation.GameSimulationCamera>()
                    : null;
                var additionalCameraData = simulationCamera != null
                    ? simulationCamera.GetComponent<
                        UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()
                    : null;
                var previewUi = simulationRoot != null
                    ? simulationRoot.GetComponentInChildren<
                        Sirius.GameSimulation.PreviewUI>(true)
                    : null;
                var previewTimingPositionTransform =
                    previewUi != null && previewUi.TimingAnimator != null
                        ? previewUi.TimingAnimator.transform
                        : null;
                var previewTimingScaleTransform = previewUi != null
                    ? previewUi.TimingTransform
                    : null;
                var previewSenseAnimator = previewUi != null
                    ? previewUi.SenseAnimator
                    : null;
                var previewBomb = simulationRoot != null
                    ? simulationRoot.GetComponentInChildren<
                        Sirius.Game.BombController>(true)
                    : null;
                var simulationLaneGroup = simulationRoot != null
                    ? simulationRoot.GetComponentInChildren<
                        Sirius.Game.LaneGroup>(true)
                    : null;
                var simulationEffectParent = simulationLaneGroup != null
                    ? simulationLaneGroup.LaneEffectParent
                    : null;
                var previewStartLine = simulationRoot != null
                    ? simulationRoot.GetComponentInChildren<
                        Sirius.Game.LaneNoteStartLine>(true)
                    : null;
                var simulationImageRect = simulationRawImage != null
                    ? simulationRawImage.rectTransform
                    : null;
                const int simulationLane = 5;
                const int simulationWidth = 2;
                var simulationNoteWidth =
                    RecoveredNotePositionCalculator.GetNoteWidth(
                        simulationWidth,
                        RecoveredGameConfigValues.NoteWidthPerLane,
                        RecoveredGameConfigValues.LaneBorderWidth);
                var expectedSimulationNoteX =
                    RecoveredNotePositionCalculator.GetNotePositionX(
                        simulationLane,
                        simulationNoteWidth,
                        RecoveredGameConfigValues.NoteWidthPerLane,
                        RecoveredGameConfigValues.LaneBorderWidth);
                var simulationNotes1 = simulationNote != null
                    ? FindNamedDescendant(simulationNote, "Notes1")
                    : null;
                var simulationNoteRenderer = simulationNotes1 != null
                    ? simulationNotes1
                        .GetComponentsInChildren<SpriteRenderer>(true)
                        .FirstOrDefault(renderer =>
                            renderer.drawMode != SpriteDrawMode.Simple)
                    : null;
                var previewMask =
                    1 << RecoveredGameSimulationPreview.IsolatedRenderLayer;
                Debug.Log(
                    "OPENWDS_SIMULATION_GEOMETRY " +
                    $"raw={simulationRawImage != null} " +
                    $"texture={simulationRawImage?.texture != null} " +
                    $"note={simulationNote != null} " +
                    $"camera={simulationCamera != null}/{simulationCamera?.enabled} " +
                    $"cameraController={simulationCameraController != null} " +
                    $"additionalCamera={additionalCameraData != null} " +
                    $"preview={previewUi != null} " +
                    $"parent={previewUi?.transform.parent?.name}/" +
                    $"{simulationEffectParent?.name} " +
                    $"previewLocal={previewUi?.transform.localPosition} " +
                    $"timing={previewTimingPositionTransform?.localPosition}/" +
                    $"{previewTimingScaleTransform?.localScale} " +
                    $"bomb={previewBomb?.transform.localPosition}/" +
                    $"{previewBomb?.transform.position} " +
                    $"noteX={simulationNote?.localPosition.x}/" +
                    $"{expectedSimulationNoteX} " +
                    $"noteScale={simulationNote?.localScale} " +
                    $"renderer={simulationNoteRenderer?.size.x}/" +
                    $"{RecoveredNoteVisualRuntime.GetTapVisualWidth(simulationNoteWidth)} " +
                    $"start={previewStartLine?.transform.localPosition.y}/" +
                    $"{RecoveredOriginalGameConfig.GetNoteVisiblePositionY(pause.CurrentSettings.GameDetailSettings.NoteStartOffset)} " +
                    $"view={simulationImageRect?.sizeDelta} " +
                    $"uv={simulationRawImage?.uvRect} " +
                    $"mask={simulationCamera?.cullingMask}/{previewMask} " +
                    $"layer={simulationNote?.gameObject.layer}");
                if (simulationRawImage == null ||
                    simulationRawImage.texture == null ||
                    simulationNote == null ||
                    simulationCamera == null ||
                    !simulationCamera.enabled ||
                    simulationCameraController == null ||
                    additionalCameraData == null ||
                    previewUi == null ||
                    simulationEffectParent == null ||
                    previewUi.transform.parent != simulationRoot.transform ||
                    previewUi.transform.localPosition !=
                        new Vector3(0f, 0f, 10f) ||
                    previewUi.transform.localRotation != Quaternion.identity ||
                    previewUi.transform.localScale != Vector3.one ||
                    previewTimingPositionTransform == null ||
                    Math.Abs(
                        previewTimingPositionTransform.localPosition.x -
                        (-0.87f)) > 0.0001f ||
                    Math.Abs(previewTimingPositionTransform.localPosition.y -
                             expectedTimingY) > 0.0001f ||
                    Math.Abs(
                        previewTimingPositionTransform.localScale.x -
                        0.8f) > 0.0001f ||
                    previewTimingScaleTransform == null ||
                    Math.Abs(
                        previewTimingScaleTransform.localPosition.y -
                        (-0.1f)) > 0.0001f ||
                    Math.Abs(previewTimingScaleTransform.localScale.x -
                             expectedTimingScale) > 0.0001f ||
                    previewBomb == null ||
                    previewBomb.transform.parent != simulationEffectParent ||
                    Math.Abs(
                        previewBomb.transform.localPosition.x -
                        (-0.9f)) > 0.0001f ||
                    Math.Abs(previewBomb.transform.localPosition.y) > 0.0001f ||
                    Math.Abs(previewBomb.transform.position.x -
                             simulationEffectParent.TransformPoint(
                                 new Vector3(-0.9f, 0f, 0f)).x) > 0.0001f ||
                    previewSenseAnimator == null ||
                    previewSenseAnimator.transform.localPosition != Vector3.zero ||
                    Vector3.Distance(
                        previewSenseAnimator.transform.localScale,
                        new Vector3(0.58f, 0.58f, 1f)) > 0.0001f ||
                    previewSenseAnimator.runtimeAnimatorController == null ||
                    previewSenseAnimator.runtimeAnimatorController.name !=
                        "SenseAnimator Controller" ||
                    Math.Abs(simulationNote.localPosition.x -
                             expectedSimulationNoteX) > 0.0001f ||
                    Math.Abs(
                        Mathf.DeltaAngle(
                            simulationNote.localEulerAngles.x,
                            RecoveredOriginalGameConfig.GetNoteHeightRotationX(
                                noteHeightBefore))) > 0.0001f ||
                    simulationNote.localScale != Vector3.one ||
                    simulationNoteRenderer == null ||
                    Math.Abs(
                        simulationNoteRenderer.size.x -
                        RecoveredNoteVisualRuntime.GetTapVisualWidth(
                            simulationNoteWidth)) > 0.0001f ||
                    previewStartLine == null ||
                    !Mathf.Approximately(
                        previewStartLine.transform.localPosition.y,
                        RecoveredOriginalGameConfig.GetNoteVisiblePositionY(
                            pause.CurrentSettings.GameDetailSettings.NoteStartOffset)) ||
                    simulationImageRect == null ||
                    Math.Abs(simulationImageRect.sizeDelta.x - 290f) > 0.01f ||
                    Math.Abs(simulationRawImage.uvRect.x - 0.412f) > 0.0001f ||
                    Math.Abs(simulationRawImage.uvRect.width - 0.16f) > 0.0001f ||
                    simulationCamera.cullingMask != previewMask ||
                    simulationNote.gameObject.layer !=
                        RecoveredGameSimulationPreview.IsolatedRenderLayer)
                    throw new InvalidOperationException(
                        "Original GameSimulation RenderTexture preview geometry " +
                        "or recovered render isolation is invalid.");
                foreach (var otherCamera in Camera.allCameras)
                {
                    if (otherCamera != simulationCamera &&
                        (otherCamera.cullingMask & previewMask) != 0)
                        throw new InvalidOperationException(
                            $"Camera {otherCamera.name} still renders the " +
                            "isolated GameSimulation layer.");
                }
                var previewRuntime = simulationRoot != null
                    ? simulationRoot.GetComponent<RecoveredGameSimulationPreview>()
                    : null;
                previewRuntime?.SendMessage(
                    "PlayHitEffect", SendMessageOptions.RequireReceiver);
                var previewBombParticle = previewBomb != null
                    ? previewBomb.GetComponentInChildren<ParticleSystem>(true)
                    : null;
                Debug.Log(
                    "OPENWDS_SIMULATION_EFFECT " +
                    $"bombCount={previewRuntime?.BombPlayCount} " +
                    $"bombActive={previewRuntime?.IsBombActive} " +
                    $"particle={previewBombParticle?.isPlaying} " +
                    $"sense={previewSenseAnimator?.gameObject.activeSelf}/" +
                    $"{expectedSense} " +
                    $"senseState={previewSenseAnimator?.GetCurrentAnimatorStateInfo(0).shortNameHash} " +
                    $"timingState={previewUi?.TimingAnimator?.GetCurrentAnimatorStateInfo(0).shortNameHash}");
                if (previewRuntime == null ||
                    previewRuntime.BombPlayCount == 0 ||
                    !previewRuntime.IsBombActive ||
                    previewBombParticle == null ||
                    !previewBombParticle.isPlaying ||
                    previewSenseAnimator.gameObject.activeSelf != expectedSense ||
                    (expectedSense &&
                     !previewSenseAnimator.GetCurrentAnimatorStateInfo(0)
                         .IsName("SenseMoveUp_anim")) ||
                    previewUi.TimingAnimator == null ||
                    !previewUi.TimingAnimator.GetCurrentAnimatorStateInfo(0)
                        .IsName("TimingEffect_anime"))
                    throw new InvalidOperationException(
                        "GameSimulation Bomb/Sense effects did not use the " +
                        "recovered PreviewUI hit branch.");

                var increaseLaneWidth =
                    laneWidthBefore < RecoveredGameSettings.MaximumLaneWidth;
                var laneWidthButton = FindSettingsButton(
                    "LaneWidthSettings",
                    increaseLaneWidth ? "Plus" : "Minus");
                if (laneWidthButton == null)
                    throw new InvalidOperationException(
                        "Lane width step button is missing.");
                laneWidthButton.onClick.Invoke();
                var laneWidthChanged = laneWidthBefore +
                    (increaseLaneWidth
                        ? RecoveredGameSettings.LaneWidthStep
                        : -RecoveredGameSettings.LaneWidthStep);
                if (pause.CurrentSettings.GameDetailSettings.LaneWidth !=
                        laneWidthChanged ||
                    simulationLaneGroup == null ||
                    Math.Abs(
                        simulationLaneGroup.LaneScaleX -
                        RecoveredGameSettings.CalculateLaneScale(
                            laneWidthChanged)) > 0.0001f)
                    throw new InvalidOperationException(
                        "Lane width did not update both settings and the " +
                        "original GameSimulation LaneGroup.");

                var previewNoteRotationBefore =
                    simulationNote.localEulerAngles.x;
                var previewLaneAlphaBefore =
                    simulationLaneGroup.LaneDarknessAlpha;
                var increaseNoteHeight =
                    noteHeightBefore <
                    RecoveredGameSettings.MaximumNoteHeight;
                var noteHeightButton = FindSettingsButton(
                    "NotesHeightSettings",
                    increaseNoteHeight ? "Plus" : "Minus");
                if (noteHeightButton == null)
                    throw new InvalidOperationException(
                        "Note height step button is missing.");
                noteHeightButton.onClick.Invoke();
                var noteHeightChanged = noteHeightBefore +
                    (increaseNoteHeight
                        ? RecoveredGameSettings.NoteHeightStep
                        : -RecoveredGameSettings.NoteHeightStep);
                if (pause.CurrentSettings.GameDetailSettings.NoteHeight !=
                        noteHeightChanged ||
                    Math.Abs(Mathf.DeltaAngle(
                        simulationNote.localEulerAngles.x,
                        previewNoteRotationBefore)) > 0.0001f)
                    throw new InvalidOperationException(
                        "Note height did not change the setting, or was " +
                        "incorrectly applied to the existing preview note.");

                var increaseLaneAlpha =
                    laneAlphaBefore <
                    RecoveredGameSettings.MaximumLaneAlpha;
                var laneAlphaButton = FindSettingsButton(
                    "LaneAlphaSettings",
                    increaseLaneAlpha ? "Plus" : "Minus");
                if (laneAlphaButton == null)
                    throw new InvalidOperationException(
                        "Lane darkness step button is missing.");
                laneAlphaButton.onClick.Invoke();
                var laneAlphaChanged = laneAlphaBefore +
                    (increaseLaneAlpha
                        ? RecoveredGameSettings.LaneAlphaStep
                        : -RecoveredGameSettings.LaneAlphaStep);
                if (pause.CurrentSettings.GameSettings.LaneAlphaValue !=
                        laneAlphaChanged ||
                    Math.Abs(
                        simulationLaneGroup.LaneDarknessAlpha -
                        previewLaneAlphaBefore) > 0.0001f)
                    throw new InvalidOperationException(
                        "Lane darkness did not change the setting, or was " +
                        "incorrectly applied to the existing preview lane.");

                var increase = before < RecoveredGameSettings.MaximumNoteSpeed;
                var button = FindSettingsButton(
                    "NoteSpeedSettings",
                    increase ? "DecimalPlus" : "DecimalMinus");
                if (button == null)
                    throw new InvalidOperationException(
                        "NoteSpeed decimal step button is missing.");
                button.onClick.Invoke();
                var expected = Math.Round(
                    before + (increase
                        ? RecoveredGameSettings.NoteSpeedFineStep
                        : -RecoveredGameSettings.NoteSpeedFineStep),
                    2);
                var changed = pause.CurrentSettings.GameSettings.NoteSpeed;
                if (Math.Abs(changed - expected) > 0.000001d ||
                    !pause.HasPendingChanges)
                    throw new InvalidOperationException(
                        $"NoteSpeed binding failed: {before:F1} -> {changed:F1}, " +
                        $"expected {expected:F1}.");
                expectedDisplayTime =
                    RecoveredGameSettings.CalculateNoteDisplayTime(
                        pause.CurrentSettings.GameDetailSettings.NoteStartOffset,
                        changed).ToString();
                if (displayTimeText.text != expectedDisplayTime)
                    throw new InvalidOperationException(
                        "NoteDisplayTime did not refresh after NoteSpeed changed.");

                var volumeSlider = FindVolumeSlider("SliderSettings (1)");
                if (volumeSlider == null)
                    throw new InvalidOperationException(
                        "Game master volume Slider is missing.");
                var volumeExpected = volumeBefore == 73 ? 72 : 73;
                volumeSlider.value = volumeExpected / 100f;
                var volumeChanged =
                    pause.CurrentSettings.SoundVolumeSettings.GameMaster;
                if (volumeChanged != volumeExpected)
                    throw new InvalidOperationException(
                        $"Game master volume binding failed: {volumeBefore} -> " +
                        $"{volumeChanged}, expected {volumeExpected}.");

                var increaseTextSpeed =
                    textSpeedBefore < RecoveredGameSettings.MaximumTextSpeed;
                var textSpeedButton = FindSettingsButton(
                    "TextDisplaySpeedSettings",
                    increaseTextSpeed ? "Plus" : "Minus");
                if (textSpeedButton == null)
                    throw new InvalidOperationException(
                        "System text speed step button is missing.");
                textSpeedButton.onClick.Invoke();
                var textSpeedExpected =
                    textSpeedBefore + (increaseTextSpeed ? 1 : -1);
                if (pause.CurrentSettings.SystemSettings.TextDisplaySpeed !=
                    textSpeedExpected)
                    throw new InvalidOperationException(
                        "System text speed binding failed.");

                var increaseBluetooth =
                    bluetoothTimingBefore <
                    RecoveredGameSettings.MaximumNoteTimingValue;
                var bluetoothButton = FindSettingsButton(
                    "BluetoothSoundTimingOffsetSettings",
                    increaseBluetooth ? "Plus" : "Minus");
                if (bluetoothButton == null)
                    throw new InvalidOperationException(
                        "Bluetooth timing step button is missing.");
                bluetoothButton.onClick.Invoke();
                var bluetoothExpected = bluetoothTimingBefore +
                    (increaseBluetooth
                        ? RecoveredGameSettings.NoteTimingValueStep
                        : -RecoveredGameSettings.NoteTimingValueStep);
                if (Math.Abs(
                    pause.CurrentSettings.BluetoothSettings.NoteTimingValue -
                    bluetoothExpected) > 0.000001d)
                    throw new InvalidOperationException(
                        "Bluetooth timing binding failed.");

                var settingsOk = okButton != null
                    ? okButton.GetComponent<Button>()
                    : null;
                settingsOk?.onClick.Invoke();
                var restartConfirmation =
                    GameObject.Find("RestartConfirmationDialog");
                var stackedSettingsBody = GameObject.Find("OptionDialogBody");
                var confirmationTitle = restartConfirmation != null
                    ? FindNamedDescendant(
                        restartConfirmation.transform, "TitleText")
                        ?.GetComponent<Text>()
                    : null;
                var confirmationMessage = restartConfirmation != null
                    ? FindNamedDescendant(
                        restartConfirmation.transform, "ConfirmationMessage")
                        ?.GetComponent<Text>()
                    : null;
                var returnToSettings = restartConfirmation != null
                    ? FindNamedDescendant(
                        restartConfirmation.transform, "FirstButton")
                        ?.GetComponent<Button>()
                    : null;
                var discardAndReturn = restartConfirmation != null
                    ? FindNamedDescendant(
                        restartConfirmation.transform, "SecondButton")
                        ?.GetComponent<Button>()
                    : null;
                var applyAndRetry = restartConfirmation != null
                    ? FindNamedDescendant(
                        restartConfirmation.transform, "ThirdButton")
                        ?.GetComponent<Button>()
                    : null;
                var applyAndRetryImage = applyAndRetry != null
                    ? applyAndRetry.GetComponent<Image>()
                    : null;
                var applyAndRetryText = applyAndRetry != null
                    ? applyAndRetry.GetComponentInChildren<Text>(true)
                    : null;
                if (!pause.IsSettingsConfirmationOpen ||
                    restartConfirmation == null ||
                    confirmationTitle == null ||
                    confirmationTitle.text != "リトライ" ||
                    confirmationMessage == null ||
                    confirmationMessage.text !=
                        "設定を反映して最初から公演を始めますか？" ||
                    returnToSettings == null ||
                    returnToSettings.GetComponentInChildren<Text>(true)?.text !=
                        "キャンセル" ||
                    discardAndReturn == null ||
                    discardAndReturn.GetComponentInChildren<Text>(true)?.text !=
                        "反映せずに戻る" ||
                    applyAndRetry == null ||
                    applyAndRetryText == null ||
                    applyAndRetryText.text != "反映してリトライ" ||
                    applyAndRetryText.color != Color.white ||
                    applyAndRetryImage == null ||
                    applyAndRetryImage.sprite == null ||
                    applyAndRetryImage.sprite.name !=
                        "btn_common_btn_large_red" ||
                    stackedSettingsBody != null)
                    throw new InvalidOperationException(
                        "Settings OK did not open the original retry " +
                        "confirmation after closing OptionDialogBody.");
                returnToSettings.onClick.Invoke();
                if (pause.IsSettingsConfirmationOpen || !pause.IsSettingsOpen)
                    throw new InvalidOperationException(
                        "Restart confirmation did not return to settings.");

                var reopenedSettings = GameObject.Find("OptionDialogBody");
                var reopenedDialog = GameObject.Find("Dialog");
                var reopenedOk = reopenedDialog != null
                    ? FindNamedDescendant(
                        reopenedDialog.transform, "SecondButton")
                        ?.GetComponent<Button>()
                    : null;
                if (reopenedSettings == null || reopenedOk == null)
                    throw new InvalidOperationException(
                        "Returned settings dialog is incomplete.");
                reopenedOk.onClick.Invoke();
                var discardConfirmation =
                    GameObject.Find("RestartConfirmationDialog");
                var discardButton = discardConfirmation != null
                    ? FindNamedDescendant(
                        discardConfirmation.transform, "SecondButton")
                        ?.GetComponent<Button>()
                    : null;
                if (discardButton == null)
                    throw new InvalidOperationException(
                        "Restart confirmation discard branch is missing.");
                discardButton.onClick.Invoke();
                if (!pause.IsDialogOpen || !pause.IsPauseMenuOpen ||
                    pause.IsSettingsOpen || !runtime.IsPaused ||
                    pause.HasPendingChanges ||
                    Math.Abs(
                        pause.CurrentSettings.GameSettings.NoteSpeed - before
                    ) > 0.000001d ||
                    pause.CurrentSettings.SoundVolumeSettings.GameMaster !=
                    volumeBefore ||
                    pause.CurrentSettings.SystemSettings.TextDisplaySpeed !=
                    textSpeedBefore ||
                    pause.CurrentSettings.GameSettings.IsActiveSenseDisplay !=
                        senseDisplayBefore ||
                        pause.CurrentSettings.GameDetailSettings.LaneWidth !=
                        laneWidthBefore ||
                    pause.CurrentSettings.GameSettings.LaneAlphaValue !=
                        laneAlphaBefore ||
                    pause.CurrentSettings.GameDetailSettings.NoteHeight !=
                        noteHeightBefore ||
                    Math.Abs(
                        pause.CurrentSettings.BluetoothSettings.NoteTimingValue -
                        bluetoothTimingBefore) > 0.000001d)
                    throw new InvalidOperationException(
                        "Discard did not restore settings and return to pause menu.");

                pause.OpenSettings();
                var unchangedDialog = GameObject.Find("Dialog");
                var unchangedOk = unchangedDialog != null
                    ? FindNamedDescendant(
                        unchangedDialog.transform, "SecondButton")
                        ?.GetComponent<Button>()
                    : null;
                if (unchangedOk == null)
                    throw new InvalidOperationException(
                        "Unchanged settings OK button is missing.");
                unchangedOk.onClick.Invoke();
                var unchangedConfirmation =
                    GameObject.Find("RestartConfirmationDialog");
                var acceptButton = unchangedConfirmation != null
                    ? FindNamedDescendant(
                        unchangedConfirmation.transform, "ThirdButton")
                        ?.GetComponent<Button>()
                    : null;
                if (acceptButton == null)
                    throw new InvalidOperationException(
                        "Restart confirmation accept branch is missing.");
                acceptButton.onClick.Invoke();
                if (!pause.IsPauseMenuOpen || pause.IsSettingsOpen ||
                    pause.IsSettingsConfirmationOpen || !runtime.IsPaused ||
                    runtime.IsResultShown || runtime.IsRetired)
                    throw new InvalidOperationException(
                        "Accepting unchanged settings did not return to Pause.");
                pause.Resume();
                if (pause.IsDialogOpen || !runtime.IsPaused ||
                    !pause.IsResumeCountdownActive)
                    throw new InvalidOperationException(
                        "Continue did not enter the original three-second countdown.");

                SessionState.SetBool(PauseSmokeKey, true);
                Debug.Log(
                    "OPENWDS_PLAYMODE_PAUSE_PASS " +
                    "resumeCountdown=3s " +
                    $"senseDisplay={senseDisplayBefore}->{expectedSense}->" +
                    $"{senseDisplayBefore} " +
                    $"laneWidth={laneWidthBefore}->{laneWidthChanged}->" +
                    $"{laneWidthBefore} " +
                    $"laneDarkness={laneAlphaBefore}->{laneAlphaChanged}->" +
                    $"{laneAlphaBefore}(preview unchanged) " +
                    $"noteHeight={noteHeightBefore}->{noteHeightChanged}->" +
                    $"{noteHeightBefore}(preview unchanged) " +
                    $"noteSpeed={before:F1}->{changed:F1}->{before:F1} " +
                    $"gameMaster={volumeBefore}->{volumeChanged}->{volumeBefore}");
                return true;
            }
            catch (Exception exception)
            {
                Fail("Pause/settings smoke failed: " + exception.Message,
                    runtime, elapsed);
                return false;
            }
        }

        private static Button FindSettingsButton(
            string settingsName, string buttonName)
        {
            foreach (var button in UnityEngine.Object.FindObjectsOfType<Button>(true))
            {
                if (button.name != buttonName) continue;
                for (var parent = button.transform.parent;
                     parent != null;
                     parent = parent.parent)
                {
                    if (parent.name == settingsName &&
                        parent.gameObject.activeSelf)
                        return button;
                }
            }
            return null;
        }

        private static Transform FindNamedDescendant(
            Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            for (var index = 0; index < root.childCount; index++)
            {
                var found = FindNamedDescendant(root.GetChild(index), objectName);
                if (found != null) return found;
            }
            return null;
        }

        private static Slider FindVolumeSlider(string groupName)
        {
            foreach (var slider in
                     UnityEngine.Object.FindObjectsOfType<Slider>(true))
            {
                var hasMainSettings = false;
                var hasGroup = false;
                for (var parent = slider.transform.parent;
                     parent != null;
                     parent = parent.parent)
                {
                    if (parent.name == "MainSettings") hasMainSettings = true;
                    if (parent.name == groupName) hasGroup = true;
                }
                if (hasMainSettings && hasGroup) return slider;
            }
            return null;
        }

        private static int GetCount(
            RecoveredGameResultRuntime result, RecoveredTimingType timing)
        {
            return result.TimingCounts.TryGetValue(timing, out var count) ? count : 0;
        }

        private static string FormatRemainingFlickNotes(
            RecoveredInputHandlerRuntime input)
        {
            var values = new string[input.RemainingFlickNotes.Count];
            for (var index = 0; index < input.RemainingFlickNotes.Count; index++)
            {
                var note = input.RemainingFlickNotes[index];
                values[index] = $"{note.Id}@{note.StartMilliseconds}:" +
                                $"lane={note.Lane},width={note.Width}";
            }
            return string.Join(",", values);
        }

        private static string FormatNonPerfectResults(
            RecoveredGameResultRuntime result)
        {
            return string.Join(
                ",",
                result.NonPerfectResults.Select(value =>
                    value.NoteId + ":" + (int)value.NoteType + "@" +
                    value.StartMilliseconds + "=" + value.TimingType));
        }

        private static string FormatRemainingFlickInputDiagnostics(
            RecoveredInputHandlerRuntime input)
        {
            var values = new string[input.RemainingFlickNotes.Count];
            for (var index = 0; index < input.RemainingFlickNotes.Count; index++)
            {
                var note = input.RemainingFlickNotes[index];
                values[index] = note.Id + "[" +
                                input.GetFlickInputDiagnostic(note.Id) + "]";
            }
            return string.Join(",", values);
        }

        private static void Fail(
            string message,
            RecoveredGameRuntime runtime,
            double elapsed,
            bool writeReport = true)
        {
            SessionState.SetString(ErrorKey, message);
            if (writeReport)
            {
                var report = runtime != null && runtime.GameResultRuntime != null
                    ? BuildReport(runtime, elapsed)
                    : new AutoplayReport
                    {
                        generatedAtUtc = DateTime.UtcNow.ToString("O"),
                        elapsedSeconds = elapsed,
                    };
                report.passed = false;
                report.failure = message;
                WriteReport(report);
            }
            Debug.LogError("OPENWDS_PLAYMODE_AUTO_JUDGE_FAIL " + message);
            Finish(false);
        }

        private static void Finish(bool passed)
        {
            if (passed) SessionState.EraseString(ErrorKey);
            SessionState.SetString(PhaseKey, ExitPhase);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.isPlaying = false;
        }

        private static void WriteReport(AutoplayReport report)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var repositoryRoot = Directory.GetParent(
                Directory.GetParent(projectRoot).FullName).FullName;
            var reportName = SessionState.GetBool(TargetedChartKey, false)
                ? string.Format(
                    "unity-playmode-auto-judge-validation-{0}-{1}.json",
                    SessionState.GetInt(MusicIdKey, 1),
                    SessionState.GetString(
                            DifficultyKey,
                            RecoveredMusicDifficulty.Stella.ToString())
                        .ToLowerInvariant())
                : "unity-playmode-auto-judge-validation.json";
            var reportPath = Path.Combine(
                repositoryRoot, "reverse", "reports",
                reportName);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true) + Environment.NewLine);
        }

        private static double GetElapsedSeconds()
        {
            var raw = SessionState.GetString(StartedKey, "0");
            return long.TryParse(raw, out var ticks) && ticks > 0
                ? (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalSeconds
                : 0d;
        }

        private static double GetTimeoutSeconds()
        {
            var raw = Environment.GetEnvironmentVariable(
                "OPENWDS_PLAYMODE_TIMEOUT_SECONDS");
            if (double.TryParse(raw, out var seconds) && seconds > 0d)
                return seconds;
            var musicTimeSeconds = SessionState.GetInt(MusicTimeSecondsKey, 0);
            return Math.Max(180d, musicTimeSeconds + 60d);
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (!SessionState.GetBool(ActiveKey, false) ||
                SessionState.GetString(PhaseKey, string.Empty) == ExitPhase ||
                (type != LogType.Error && type != LogType.Exception &&
                 type != LogType.Assert))
                return;
            if (condition.StartsWith("OPENWDS_PLAYMODE_AUTO_JUDGE_FAIL", StringComparison.Ordinal))
                return;
            if (string.IsNullOrEmpty(SessionState.GetString(ErrorKey, string.Empty)))
                SessionState.SetString(ErrorKey, condition);
        }
    }
}
