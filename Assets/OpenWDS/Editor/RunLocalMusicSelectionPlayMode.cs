using System;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenWDS.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenWDS.Editor
{
    [InitializeOnLoad]
    public static class RunLocalMusicSelectionPlayMode
    {
        private const string ScenePath =
            "Assets/OpenWDS/Scenes/LocalMusicSelection.unity";
        private const string ActiveKey = "OpenWDS.MusicSelectionPlayMode.Active";
        private const string HudFadeKey = "OpenWDS.MusicSelectionPlayMode.HudFade";
        private const string PreviewExitKey = "OpenWDS.MusicSelectionPlayMode.PreviewExit";
        private const string ResultCountKey = "OpenWDS.MusicSelectionPlayMode.ResultCount";
        private const string PhaseKey = "OpenWDS.MusicSelectionPlayMode.Phase";
        private const string ErrorKey = "OpenWDS.MusicSelectionPlayMode.Error";
        private const string StartedKey = "OpenWDS.MusicSelectionPlayMode.Started";
        private const string ScrollStartedKey =
            "OpenWDS.MusicSelectionPlayMode.ScrollStarted";
        private const string IntroductionFocusTimeKey =
            "OpenWDS.MusicSelectionPlayMode.IntroductionFocusTime";
        private const string IntroductionFocusValidKey =
            "OpenWDS.MusicSelectionPlayMode.IntroductionFocusValid";
        private const string ResultGameInstanceKey =
            "OpenWDS.MusicSelectionPlayMode.ResultGameInstance";
        private const string SelectionStartCountKey =
            "OpenWDS.MusicSelectionPlayMode.SelectionStartCount";
        private const string ExitPhase = "exit";
        private static string _lastLoggedPhase;
        private static LocalResultStore _storeBeforeLive;
        private static TextAsset _sessionChart, _sessionConfig;

        [Serializable]
        private sealed class Report
        {
            public bool passed;
            public bool introductionMovedDuringCurtain;
            public float introductionDelaySeconds;
            public bool musicCoverTypesValidated;
            public bool olivierStarsValidated;
            public bool hudFadeObserved;
            public bool previewLeftExitObserved;
            public bool resultCountObserved;
            public string failure;
            public double elapsedSeconds;
            public int availableDifficulties;
            public string selectedDifficulty;
            public bool introductionFocusResumeValid;
            public bool gameLoaded;
            public bool pauseRetryContinued;
            public bool settingsRestartReloaded;
            public bool retireReturned;
            public bool resultReturned;
            public bool difficultyPreviewPreserved;
            public bool uncachedRatingJacketLoaded;
            public bool resultsReloaded;
            public bool menuRebuilt;
            public long screenshotBytes;
            public int screenshotNonBackgroundPixels;
        }

        static RunLocalMusicSelectionPlayMode()
        {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        // Compatibility alias for existing batch commands.
        public static void RunPresentation() => Run();

        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException(
                    "MusicSelection Play Mode gate requires batch mode.");
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException(
                    "LocalMusicSelection scene is missing.", ScenePath);
            if (!CreateLocalMusicSelectionScene.ValidateRatingRules())
                throw new InvalidOperationException("Rating or failed-result persistence rules failed.");
            SessionState.SetBool("OpenWDS.MusicCoverTypesValidated", false);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(HudFadeKey, false);
            SessionState.SetBool(PreviewExitKey, false);
            SessionState.SetBool(ResultCountKey, false);
            SessionState.SetBool("OpenWDS.SelectionBugChecks", false);
            SessionState.SetBool("OpenWDS.OlivierStarsValidated", false);
            _storeBeforeLive = null;
            SessionState.SetString(PhaseKey, "presentation-select");
            SessionState.SetString(StartedKey, DateTime.UtcNow.Ticks.ToString());
            SessionState.EraseString(ErrorKey);
            SessionState.SetBool(IntroductionFocusValidKey, false);
            SessionState.SetBool("OpenWDS.IntroDuringCurtain", false);
            SessionState.SetBool("OpenWDS.IntroLightMoved", false);
            SessionState.SetFloat("OpenWDS.IntroNativeDelay", -1f);
            SessionState.EraseInt(SelectionStartCountKey);
            var screenshot = ScreenshotPath();
            if (File.Exists(screenshot)) File.Delete(screenshot);
            var noteSpeedScreenshot = NoteSpeedScreenshotPath();
            if (File.Exists(noteSpeedScreenshot))
                File.Delete(noteSpeedScreenshot);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("OPENWDS_MUSIC_SELECTION_PLAYMODE_START");
            EditorApplication.isPlaying = true;
        }

        private static void Poll()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (SessionState.GetString(PhaseKey, "") == ExitPhase)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    var passed = string.IsNullOrEmpty(
                        SessionState.GetString(ErrorKey, ""));
                    SessionState.SetBool(ActiveKey, false);
                    EditorApplication.Exit(passed ? 0 : 1);
                }
                return;
            }
            var elapsed = Elapsed();
            if (elapsed > 360d)
            {
                Finish(false, "MusicSelection Play Mode timed out.", elapsed);
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            var sampledHud = UnityEngine.Object.FindObjectOfType<GameHudRuntime>();
            if (sampledHud != null && sampledHud.IntroductionAlpha > 0f && sampledHud.IntroductionAlpha < 1f)
                SessionState.SetBool(HudFadeKey, true);
            var sampledPreviewView = GameObject.Find("GameSimulationView");
            if (sampledPreviewView != null)
            {
                var alpha = sampledPreviewView.GetComponent<CanvasGroup>().alpha;
                var rect = sampledPreviewView.transform.Find("RawImage") as RectTransform;
                if (alpha > 0f && alpha < 1f && rect != null && rect.anchoredPosition.x < 0f)
                    SessionState.SetBool(PreviewExitKey, true);
            }
            var sampledResult = UnityEngine.Object.FindObjectOfType<Sirius.GameResult.GameResultPanel>();
            if (sampledResult != null && sampledResult.IsCounting)
                SessionState.SetBool(ResultCountKey, true);
            var firstError = SessionState.GetString(ErrorKey, "");
            if (!string.IsNullOrEmpty(firstError))
            {
                Finish(false, "Play Mode logged an error: " + firstError, elapsed);
                return;
            }

            var openingGame = UnityEngine.Object.FindObjectOfType<GameRuntime>();
            if (openingGame != null && openingGame.IsIntroductionPlaying &&
                CurtainTransitionRuntime.CurrentAnimation == "open")
            {
                var intro = UnityEngine.Object.FindObjectOfType<Sirius.Game.GameIntroductionAnimationController>();
                var light = intro.GetComponentsInChildren<Transform>(true).First(t => t.name == "Light1");
                if (!SessionState.GetBool("OpenWDS.IntroDuringCurtain", false))
                {
                    if (openingGame.IntroductionCurtainPhaseAtStart != "open")
                    {
                        Finish(false, "Introduction started before the initialized scene began opening: " + openingGame.IntroductionCurtainPhaseAtStart, elapsed);
                        return;
                    }
                    SessionState.SetBool("OpenWDS.IntroDuringCurtain", true);
                    SessionState.SetFloat("OpenWDS.IntroNativeDelay", openingGame.IntroductionPlaybackStartedAt - openingGame.IntroductionTransitionStartedAt);
                    SessionState.SetFloat("OpenWDS.IntroLightX", light.localPosition.x);
                    SessionState.SetFloat("OpenWDS.IntroLightY", light.localPosition.y);
                }
                var initial = new Vector2(SessionState.GetFloat("OpenWDS.IntroLightX", 0), SessionState.GetFloat("OpenWDS.IntroLightY", 0));
                if (Vector2.Distance(initial, light.localPosition) > 1f)
                    SessionState.SetBool("OpenWDS.IntroLightMoved", true);
            }
            if (SceneNavigationRuntime.IsLoading || CurtainTransitionRuntime.IsTransitioning) return;
            var phase = SessionState.GetString(PhaseKey, "");
            if (_lastLoggedPhase != phase)
            {
                _lastLoggedPhase = phase;
                Debug.Log($"OPENWDS_PLAYMODE_PHASE {phase}");
            }
            if (phase == "presentation-select")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<LocalMusicSelectionRuntime>();
                if (runtime == null || !runtime.IsInitialized || runtime.IsFocusTransitionActive) return;
                if (runtime.CatalogMusicIds.Contains(9999))
                    throw new InvalidOperationException("Unreleased tutorial entered the ordinary list.");
                if (!SessionState.GetBool("OpenWDS.SelectionBugChecks", false))
                {
                    SessionState.SetString(PhaseKey, "selection-bug-checks");
                    runtime.StartCoroutine(ValidateSelectionBugFixes(runtime));
                    return;
                }
                _storeBeforeLive = (LocalResultStore)typeof(LocalMusicSelectionRuntime)
                    .GetField("_localResults", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);
                var catalog = (LocalMusicCatalog)typeof(LocalMusicSelectionRuntime)
                    .GetField("_catalog", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);
                var index = Array.FindIndex(catalog.Musics, item => item.Id == 10);
                if (index < 0) throw new InvalidOperationException("Presentation fixture Music 10 is missing.");
                // Use the existing snap coroutine with the actual sorted catalog
                // index; no assumption that Music 10 is initially visible.
                var routine = (System.Collections.IEnumerator)typeof(LocalMusicSelectionRuntime)
                    .GetMethod("SnapPhysicalIndex", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(runtime, new object[] { catalog.Musics.Length + index, catalog.Musics[index], false });
                SessionState.SetString(PhaseKey, "selection-music10-final");
                runtime.StartCoroutine(routine);
                return;
            }
            if (phase == "selection-music10-final")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    LocalMusicSelectionRuntime>();
                if (runtime == null || runtime.Selection == null ||
                    runtime.Selection.Music.Id != 10 ||
                    runtime.FocusedMusicId != 10 ||
                    runtime.IsFocusTransitionActive)
                    return;
                var ok = GameObject.Find(
                    "MusicSelectionView/OkButton")?.GetComponent<Button>();
                if (ok == null || !ok.interactable)
                {
                    Finish(false, "Recovered selection start button is unavailable.", elapsed);
                    return;
                }
                var label = ok.GetComponentInChildren<Text>(true);
                if (label == null || label.text != "開演")
                {
                    Finish(false, "Selection button was not changed to 開演.", elapsed);
                    return;
                }
                if (runtime.CachedJacketCount < 2 ||
                    !runtime.HasLoadedJacket(1))
                {
                    Finish(
                        false,
                        "Visible non-selected jackets were not retained in cache.",
                        elapsed);
                    return;
                }
                if (CaptureSelection(ScreenshotPath()) < 1000)
                {
                    Finish(false, "Selection render capture is empty.", elapsed);
                    return;
                }
                SessionState.SetInt(
                    SelectionStartCountKey,
                    runtime.StartInvocationCount);
                var persisted = new SettingsStore().LoadOrDefault();
                persisted.GameSettings.NoteSpeed = 9d;
                persisted.GameSettings.NoteOffsetValue = 0d;
                persisted.GameDetailSettings.IsActiveMirror = false;
                persisted.GameDetailSettings.IsActiveSplitRandom = false;
                persisted.SystemSettings.IsPreLiveOptionConfirmation = true;
                new SettingsStore().Save(persisted);
                if (runtime.ChartReadCount != 0)
                {
                    Finish(false, "Selection browsing read gameplay charts.", elapsed);
                    return;
                }
                SessionState.SetString(PhaseKey, "selection-speed");
                SessionState.SetFloat(
                    ScrollStartedKey,
                    (float)EditorApplication.timeSinceStartup);
                ok.onClick.Invoke();
                return;
            }

            if (phase == "selection-speed")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    LocalMusicSelectionRuntime>();
                if (runtime == null || !runtime.IsNoteSpeedDialogVisible)
                    return;
                if (EditorApplication.timeSinceStartup -
                    SessionState.GetFloat(ScrollStartedKey, 0f) < 0.25d)
                    return;
                if (Mathf.Abs(runtime.NoteSpeed - 9f) > 0.001f)
                {
                    Finish(false, "Note-speed confirmation body is invalid.", elapsed);
                    return;
                }
                var dialog = GameObject.Find("NoteSpeedDialog");
                var dialogRect = dialog != null
                    ? dialog.GetComponent<RectTransform>()
                    : null;
                var dialogOverlay = dialog != null
                    ? FindNamedTransform(
                        dialog.transform, "DialogOverlay")
                        ?.GetComponent<Image>()
                    : null;
                var speedPreview = GameObject.Find("GameSimulation");
                var previewView = GameObject.Find("GameSimulationView");
                var previewNote = speedPreview != null
                    ? FindNamedTransform(speedPreview.transform, "SimulationNote")
                    : null;
                var previewCamera = speedPreview != null
                    ? FindNamedTransform(
                        speedPreview.transform, "GameSimulationCamera")
                        ?.GetComponent<Camera>()
                    : null;
                var previewRawImage = previewView != null
                    ? FindNamedTransform(previewView.transform, "RawImage")
                        ?.GetComponent<RawImage>()
                    : null;
                if (dialogRect == null ||
                    Mathf.Abs(dialogRect.sizeDelta.x - 1032f) > 0.1f ||
                    dialogOverlay == null ||
                    dialogOverlay.color.a < 0.6f ||
                    !dialogOverlay.raycastTarget ||
                    speedPreview == null || previewNote == null ||
                    !speedPreview.activeInHierarchy ||
                    previewCamera == null ||
                    previewCamera.targetTexture == null ||
                    previewRawImage == null ||
                    previewRawImage.texture != previewCamera.targetTexture ||
                    previewRawImage.uvRect !=
                        new Rect(0.412f, 0f, 0.16f, 1f) ||
                    Mathf.Abs(
                        previewRawImage.rectTransform.rect.width - 290f) > 0.1f)
                {
                    Finish(
                        false,
                        "Pre-live dialog width or note-speed preview is invalid.",
                        elapsed);
                    return;
                }
                var dialogBody = GameObject.Find(
                    "NoteSpeedDialog/Body/" +
                    "NoteSpeedSettingsDialogBody");
                var timing = dialogBody != null
                    ? FindNamedTransform(dialogBody.transform, "TimingSettings")
                    : null;
                var timingFinePlus = timing != null
                    ? FindNamedTransform(timing, "DecimalPlus")
                        ?.GetComponent<Button>()
                    : null;
                var mirror = dialogBody != null
                    ? FindNamedTransform(dialogBody.transform, "MirrorSettings")
                    : null;
                var split = dialogBody != null
                    ? FindNamedTransform(
                        dialogBody.transform, "SplitRandomSettings")
                    : null;
                var mirrorOn = mirror != null
                    ? FindNamedTransform(mirror, "On")?.GetComponent<Button>()
                    : null;
                var mirrorOff = mirror != null
                    ? FindNamedTransform(mirror, "Off")?.GetComponent<Button>()
                    : null;
                var splitOn = split != null
                    ? FindNamedTransform(split, "On")?.GetComponent<Button>()
                    : null;
                var splitOff = split != null
                    ? FindNamedTransform(split, "Off")?.GetComponent<Button>()
                    : null;
                var checkButton = dialogBody != null
                    ? FindNamedTransform(
                        FindNamedTransform(dialogBody.transform, "CheckBox"),
                        "CheckButton")?.GetComponent<Button>()
                    : null;
                if (timingFinePlus == null || mirrorOn == null ||
                    mirrorOff == null || splitOn == null || splitOff == null ||
                    checkButton == null)
                {
                    Finish(false, "Performance option controls are incomplete.", elapsed);
                    return;
                }
                var uiSe = UnityEngine.Object.FindObjectOfType<
                    UiSeRuntime>();
                if (uiSe == null)
                {
                    Finish(false, "Performance option SE runtime is unavailable.", elapsed);
                    return;
                }
                timingFinePlus.onClick.Invoke();
                timingFinePlus.onClick.Invoke();
                var seCount = uiSe.CueNamePlayCount;
                mirrorOn.onClick.Invoke();
                if (uiSe.CueNamePlayCount != seCount + 1 ||
                    uiSe.LastCueName != "BUTTON_GO")
                {
                    Finish(false, "Pre-live radio did not play BUTTON_GO.", elapsed);
                    return;
                }
                mirrorOff.onClick.Invoke();
                splitOn.onClick.Invoke();
                splitOff.onClick.Invoke();
                seCount = uiSe.CueNamePlayCount;
                checkButton.onClick.Invoke();
                if (uiSe.CueNamePlayCount != seCount + 1 ||
                    uiSe.LastCueName != "BUTTON_GO")
                {
                    Finish(false, "Pre-live checkbox did not play BUTTON_GO.", elapsed);
                    return;
                }
                checkButton.onClick.Invoke();
                if (Mathf.Abs(runtime.NoteOffset - 0.2f) > 0.001f ||
                    runtime.MirrorEnabled || runtime.SplitRandomEnabled)
                {
                    Finish(false, "Performance option controls did not update.", elapsed);
                    return;
                }
                var noteSpeedScreenshot = NoteSpeedScreenshotPath();
                if (!File.Exists(noteSpeedScreenshot))
                {
                    var visiblePixels = CaptureSelection(noteSpeedScreenshot);
                    if (!File.Exists(noteSpeedScreenshot) ||
                        new FileInfo(noteSpeedScreenshot).Length < 10000 ||
                        visiblePixels < 1000)
                    {
                        Finish(
                            false,
                            "Note-speed confirmation render capture failed.",
                            elapsed);
                        return;
                    }
                }
                var buttonLayer = GameObject.Find(
                    "NoteSpeedDialogButtons");
                var confirm = buttonLayer != null
                    ? FindNamedTransform(buttonLayer.transform, "SecondButton")
                        ?.GetComponent<Button>()
                    : null;
                if (confirm == null)
                {
                    Finish(false, "Note-speed confirm button is unavailable.", elapsed);
                    return;
                }
                SessionState.SetString(PhaseKey, "game");
                confirm.onClick.Invoke();
                return;
            }

            if (phase == "game")
            {
                if (SceneManager.GetActiveScene().name != "OfflineRhythmPreview")
                    return;
                var game = UnityEngine.Object.FindObjectOfType<GameRuntime>();
                if (game == null || !game.IsInitialized) return;
                if (CurtainTransitionRuntime.IsTransitioning ||
                    !CurtainTransitionRuntime.ClosePlayed ||
                    !CurtainTransitionRuntime.OpenPlayed ||
                    !CurtainTransitionRuntime.CloseCompleted ||
                    !CurtainTransitionRuntime.OpenCompleted ||
                    !CurtainTransitionRuntime
                        .DestinationInitializedBeforeOpen ||
                    !CurtainTransitionRuntime
                        .OpenRenderedInDestination)
                    return;
                var nativeDelay = SessionState.GetFloat("OpenWDS.IntroNativeDelay", -1f);
                if (!SessionState.GetBool("OpenWDS.IntroLightMoved", false) || nativeDelay < .9f || nativeDelay > 1.2f)
                {
                    Finish(false, "Introduction must move lights during curtain opening after the native 0.9 s timer; delay=" + nativeDelay, elapsed);
                    return;
                }
                if (UnityEngine.Object.FindObjectsOfType<EventSystem>().Length != 1)
                {
                    Finish(false, "Gameplay must contain exactly one EventSystem.", elapsed);
                    return;
                }
                if (!LocalMusicSelectionSession.HasSelection ||
                    LocalMusicSelectionSession.Selection.Music.Id != 10 ||
                    LocalMusicSelectionSession.Selection.Live.Difficulty !=
                    MusicDifficulty.Stella ||
                    LocalMusicSelectionSession.ChartAsset == null ||
                    LocalMusicSelectionSession.ChartAsset.name !=
                        "Music10Stella" ||
                    game.ChartAsset !=
                    LocalMusicSelectionSession.ChartAsset ||
                    game.MusicDifficulty != MusicDifficulty.Stella)
                {
                    Finish(false, "Selected game parameters were not consumed.", elapsed);
                    return;
                }
                _sessionChart = LocalMusicSelectionSession.ChartAsset;
                _sessionConfig = LocalMusicSelectionSession.MusicConfigAsset;
                if (Resources.FindObjectsOfTypeAll<
                        MusicSelectionPreviewRuntime>()
                    .Any(preview => preview != null && preview.IsPlaying))
                {
                    Finish(
                        false,
                        "Music preview remained active after performance start.",
                        elapsed);
                    return;
                }
                var persisted = new SettingsStore().LoadOrDefault();
                if (Math.Abs(persisted.GameSettings.NoteSpeed - 9d) > 0.001d ||
                    Math.Abs(persisted.GameSettings.NoteOffsetValue - 0.2d) >
                    0.001d ||
                    persisted.GameDetailSettings.IsActiveMirror ||
                    persisted.GameDetailSettings.IsActiveSplitRandom)
                {
                    Finish(false, "Performance options were not persisted.", elapsed);
                    return;
                }
                var introduction = UnityEngine.Object.FindObjectOfType<
                    Sirius.Game.GameIntroductionAnimationController>();
                var pause = UnityEngine.Object.FindObjectOfType<
                    GamePauseRuntime>();
                if (introduction == null || !introduction.IsPlaying)
                {
                    Finish(
                        false,
                        "GameIntroduction was unavailable for focus-loss regression.",
                        elapsed);
                    return;
                }
                if (pause == null)
                {
                    Finish(
                        false,
                        "Pause runtime was unavailable for focus-loss regression.",
                        elapsed);
                    return;
                }
                introduction.SendMessage(
                    "OnApplicationFocus", false,
                    SendMessageOptions.RequireReceiver);
                pause.SendMessage(
                    "OnApplicationFocus", false,
                    SendMessageOptions.RequireReceiver);
                if (!introduction.IsAnimationPaused)
                {
                    Finish(false, "Focus loss did not freeze GameIntroduction.", elapsed);
                    return;
                }
                if (game.IsPaused || pause.IsDialogOpen)
                {
                    Finish(
                        false,
                        "Focus loss opened an unreachable Pause before gameplay started.",
                        elapsed);
                    return;
                }
                SessionState.SetFloat(
                    IntroductionFocusTimeKey, introduction.NormalizedTime);
                SessionState.SetString(PhaseKey, "game-focus-paused");
                return;
            }

            if (phase == "game-focus-paused")
            {
                var game = UnityEngine.Object.FindObjectOfType<GameRuntime>();
                var introduction = UnityEngine.Object.FindObjectOfType<
                    Sirius.Game.GameIntroductionAnimationController>();
                var pause = UnityEngine.Object.FindObjectOfType<
                    GamePauseRuntime>();
                if (game == null || introduction == null || pause == null) return;
                var pausedTime = SessionState.GetFloat(
                    IntroductionFocusTimeKey, -1f);
                if (!introduction.IsPlaying ||
                    !introduction.IsAnimationPaused ||
                    Math.Abs(introduction.NormalizedTime - pausedTime) > 0.001f)
                {
                    Finish(
                        false,
                        "GameIntroduction advanced while application focus was lost.",
                        elapsed);
                    return;
                }
                introduction.SendMessage(
                    "OnApplicationFocus", true,
                    SendMessageOptions.RequireReceiver);
                pause.SendMessage(
                    "OnApplicationFocus", true,
                    SendMessageOptions.RequireReceiver);
                if (introduction.IsAnimationPaused)
                {
                    Finish(
                        false,
                        "Focus regain did not continue GameIntroduction.",
                        elapsed);
                    return;
                }
                if (game.IsPaused || pause.IsDialogOpen)
                {
                    Finish(
                        false,
                        "Focus regain left GameIntroduction behind an invisible Pause.",
                        elapsed);
                    return;
                }
                SessionState.SetString(PhaseKey, "game-focus-resumed");
                return;
            }

            if (phase == "game-focus-resumed")
            {
                var game = UnityEngine.Object.FindObjectOfType<GameRuntime>();
                if (game == null) return;
                var introduction = UnityEngine.Object.FindObjectOfType<
                    Sirius.Game.GameIntroductionAnimationController>();
                var pausedTime = SessionState.GetFloat(
                    IntroductionFocusTimeKey, -1f);
                if (introduction != null && introduction.IsPlaying &&
                    introduction.NormalizedTime <= pausedTime + 0.001f)
                    return;
                if (!game.IsGameplayStarted || game.IsPaused ||
                    game.GameHud == null || !game.GameHud.IsVisible)
                    return;
                if (game.GameResultRuntime.CollectedCount == 0) return;
                SessionState.SetBool(IntroductionFocusValidKey, true);
                SessionState.SetInt("OpenWDS.RetryScene", game.gameObject.scene.handle);
                SessionState.SetInt("OpenWDS.RetryGame", game.GetInstanceID());
                SessionState.SetInt("OpenWDS.RetryTransitions", SceneNavigationRuntime.CompletedTransitions);
                SessionState.SetString(PhaseKey, "game-retried");
                var pauseMenu = game.GetComponent<GamePauseRuntime>();
                pauseMenu.PauseButton.onClick.Invoke();
                FindNamedTransform(pauseMenu.transform, "SecondButton").GetComponent<Button>().onClick.Invoke();
                // Retry confirmation replaces the dialog immediately; select its active button.
                var confirm = pauseMenu.GetComponentsInChildren<Button>(true)
                    .Last(b => b.name == "SecondButton" && b.gameObject.activeInHierarchy);
                confirm.onClick.Invoke();
                SessionState.SetBool("OpenWDS.RetryIntroduction", false);
                if (!pauseMenu.PauseButton.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("Pause button disappeared after Retry UI");
                return;
            }
            if (phase == "game-retried")
            {
                var game = UnityEngine.Object.FindObjectOfType<GameRuntime>();
                if (game != null && game.RetryCount == 1 && game.IsIntroductionPlaying)
                    SessionState.SetBool("OpenWDS.RetryIntroduction", true);
                if (game != null && !game.GetComponent<GamePauseRuntime>().PauseButton.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("Pause button disappeared during retry introduction");
                if (game == null || !game.IsGameplayStarted || game.IsRestartPending) return;
                if (!SessionState.GetBool("OpenWDS.RetryIntroduction", false) || game.RetryCount != 1 || game.GetInstanceID() != SessionState.GetInt("OpenWDS.RetryGame", 0) ||
                    game.gameObject.scene.handle != SessionState.GetInt("OpenWDS.RetryScene", 0) ||
                    SceneNavigationRuntime.CompletedTransitions != SessionState.GetInt("OpenWDS.RetryTransitions", -1) ||
                    game.GameResultRuntime.CollectedCount != 0 || game.GameHud.TotalScore != 0 || game.IsPaused)
                {
                    Finish(false, "Pause retry failed to reset the same Game instance/scene.", elapsed); return;
                }
                SessionState.SetString(PhaseKey, "retry-playing");
                return;
            }
            if (phase == "retry-playing")
            {
                var game = UnityEngine.Object.FindObjectOfType<GameRuntime>();
                if (game == null || game.GameResultRuntime.CollectedCount == 0) return;
                if (game.GetInstanceID() != SessionState.GetInt("OpenWDS.RetryGame", 0) || game.IsPaused)
                {
                    Finish(false, "Retry did not continue judging in the same Game.", elapsed); return;
                }
                SessionState.SetString(PhaseKey, "settings-restarted");
                game.SetPaused(true);
                game.RestartPerformance();
                return;
            }
            if (phase == "settings-restarted")
            {
                var game = UnityEngine.Object.FindObjectOfType<GameRuntime>();
                if (game == null || !game.IsGameplayStarted || game.IsRestartPending) return;
                if (game.GetInstanceID() == SessionState.GetInt("OpenWDS.RetryGame", 0) ||
                    game.gameObject.scene.handle == SessionState.GetInt("OpenWDS.RetryScene", 0) ||
                    SceneNavigationRuntime.CompletedTransitions != SessionState.GetInt("OpenWDS.RetryTransitions", -1) + 1 ||
                    !SceneNavigationRuntime.SourceUnloadedBeforeLoad || SceneNavigationRuntime.TransitionType != 3 ||
                    game.RetryCount != 0 || game.IsPaused)
                {
                    Finish(false, "Settings restart did not replace Game through Curtain.", elapsed); return;
                }
                SessionState.SetString(PhaseKey, "return-retire");
                game.RetireGame();
                return;
            }

            if (phase == "return-retire")
            {
                if (SceneManager.GetActiveScene().name != SceneNavigationRuntime.MainScene)
                    return;
                var runtime = UnityEngine.Object.FindObjectOfType<
                    LocalMusicSelectionRuntime>();
                if (runtime == null || !runtime.IsInitialized || runtime.Selection == null) return;
                if (runtime.StartInvocationCount <= SessionState.GetInt(
                        SelectionStartCountKey, -1))
                {
                    Finish(
                        false,
                        "Retire return failed to rebuild the selection runtime.",
                        elapsed);
                    return;
                }
                if (UnityEngine.Object.FindObjectsOfType<EventSystem>().Length != 1)
                {
                    Finish(
                        false,
                        "Retire return created a duplicate EventSystem.",
                        elapsed);
                    return;
                }
                var ok = GameObject.Find(
                    "MusicSelectionView/OkButton")?.GetComponent<Button>();
                if (ok == null)
                {
                    Finish(false, "Selection did not recover after retire.", elapsed);
                    return;
                }
                if (_sessionChart != null || _sessionConfig != null || LocalMusicSelectionSession.HasSelection)
                {
                    if (SceneManager.GetSceneByName("OfflineRhythmPreview").isLoaded) return;
                    Finish(false, "Retire retained dynamically allocated chart objects.", elapsed);
                    return;
                }
                SessionState.SetString(PhaseKey, "retire-speed");
                ok.onClick.Invoke();
                return;
            }

            if (phase == "retire-speed")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    LocalMusicSelectionRuntime>();
                if (runtime == null || !runtime.IsNoteSpeedDialogVisible)
                    return;
                var buttonLayer = GameObject.Find(
                    "NoteSpeedDialogButtons");
                var confirm = buttonLayer != null
                    ? FindNamedTransform(buttonLayer.transform, "SecondButton")
                        ?.GetComponent<Button>()
                    : null;
                if (confirm == null) return;
                SessionState.SetString(PhaseKey, "game-result");
                confirm.onClick.Invoke();
                return;
            }

            if (phase == "game-result")
            {
                if (SceneManager.GetActiveScene().name != "OfflineRhythmPreview")
                    return;
                var game = UnityEngine.Object.FindObjectOfType<GameRuntime>();
                if (game == null || !game.IsInitialized) return;
                var showResult = typeof(GameRuntime).GetMethod(
                    "ShowGameResult",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (showResult == null)
                {
                    Finish(false, "Game result presenter method is unavailable.", elapsed);
                    return;
                }
                SessionState.SetInt(ResultGameInstanceKey, game.GetInstanceID());
                showResult.Invoke(game, null);
                _sessionChart = LocalMusicSelectionSession.ChartAsset;
                _sessionConfig = LocalMusicSelectionSession.MusicConfigAsset;
                SessionState.SetString(PhaseKey, "result-replay-button");
                return;
            }

            if (phase == "result-replay-button")
            {
                var root = GameObject.Find("RightBotton");
                // Retail GameResultNextPanel remains inactive until the result
                // entrance/count presentation has completed.
                if (root == null) return;
                var next = root != null
                    ? FindNamedTransform(root.transform, "NextButton")
                        ?.GetComponent<Button>()
                    : null;
                var replay = root != null
                    ? FindNamedTransform(root.transform, "InGameButton")
                        ?.GetComponent<Button>()
                    : null;
                var nextText = next != null
                    ? next.GetComponentInChildren<Text>(true)
                    : null;
                var replayText = replay != null
                    ? replay.GetComponentInChildren<Text>(true)
                    : null;
                var nextAnimator = next != null
                    ? next.GetComponent<Animator>()
                    : null;
                var replayAnimator = replay != null
                    ? replay.GetComponent<Animator>()
                    : null;
                if (next == null || replay == null ||
                    FindNamedTransform(root.transform, "BackButton") != null ||
                    nextText?.text != "次へ" ||
                    replayText?.text != "もう一度遊ぶ" ||
                    next.transition != Selectable.Transition.Animation ||
                    replay.transition != Selectable.Transition.Animation ||
                    nextAnimator?.runtimeAnimatorController?.name != "ButtonScale" ||
                    replayAnimator?.runtimeAnimatorController?.name != "ButtonScale")
                {
                    Finish(
                        false,
                        "Original result navigation buttons are invalid: " +
                        $"next={next != null} replay={replay != null} " +
                        $"back={FindNamedTransform(root.transform, "BackButton") != null} " +
                        $"nextText={nextText?.text ?? "null"} " +
                        $"replayText={replayText?.text ?? "null"} " +
                        $"nextTransition={next?.transition.ToString() ?? "null"} " +
                        $"replayTransition={replay?.transition.ToString() ?? "null"} " +
                        $"nextAnimator={nextAnimator?.runtimeAnimatorController?.name ?? "null"} " +
                        $"replayAnimator={replayAnimator?.runtimeAnimatorController?.name ?? "null"}",
                        elapsed);
                    return;
                }
                var game = UnityEngine.Object.FindObjectOfType<GameRuntime>();
                var uiSe = UiSeRuntime.Instance;
                if (game != null || GameResultSceneRuntime.Instance == null || uiSe == null)
                {
                    Finish(false, "Result replay SE runtime is unavailable.", elapsed);
                    return;
                }
                var seCount = uiSe.CueNamePlayCount;
                SessionState.SetString(PhaseKey, "result-replayed");
                replay.onClick.Invoke();
                if (uiSe.CueNamePlayCount != seCount + 1 ||
                    uiSe.LastCueName != "BUTTON_GO")
                    Finish(false, "Result replay did not play BUTTON_GO.", elapsed);
                return;
            }

            if (phase == "result-replayed")
            {
                if (SceneManager.GetActiveScene().name != "OfflineRhythmPreview")
                    return;
                var game = UnityEngine.Object.FindObjectOfType<GameRuntime>();
                if (game == null || !game.IsInitialized ||
                    game.GetInstanceID() == SessionState.GetInt(
                        ResultGameInstanceKey, game.GetInstanceID()) ||
                    !LocalMusicSelectionSession.HasSelection)
                    return;
                var showResult = typeof(GameRuntime).GetMethod(
                    "ShowGameResult",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (showResult == null)
                {
                    Finish(false, "Replayed game result method is unavailable.", elapsed);
                    return;
                }
                if (_sessionChart == null || _sessionConfig == null ||
                    !ReferenceEquals(_sessionChart, LocalMusicSelectionSession.ChartAsset) ||
                    !ReferenceEquals(_sessionConfig, LocalMusicSelectionSession.MusicConfigAsset))
                {
                    Finish(false, "Replay discarded or replaced its chart session.", elapsed);
                    return;
                }
                showResult.Invoke(game, null);
                SessionState.SetString(PhaseKey, "result-next-button");
                return;
            }

            if (phase == "result-next-button")
            {
                var button = GameObject.Find(
                    "NextButton")
                    ?.GetComponent<Button>();
                if (button == null) return;
                var uiSe = UiSeRuntime.Instance;
                if (uiSe == null)
                {
                    Finish(false, "Result next SE runtime is unavailable.", elapsed);
                    return;
                }
                var seCount = uiSe.CueNamePlayCount;
                SessionState.SetString(PhaseKey, "return-result");
                button.onClick.Invoke();
                if (uiSe.CueNamePlayCount != seCount + 1 ||
                    uiSe.LastCueName != "BUTTON_GO")
                    Finish(false, "Result next did not play BUTTON_GO.", elapsed);
                return;
            }

            if (phase == "return-result")
            {
                if (SceneManager.GetActiveScene().name != SceneNavigationRuntime.MainScene)
                    return;
                var runtime = UnityEngine.Object.FindObjectOfType<
                    LocalMusicSelectionRuntime>();
                if (runtime == null || !runtime.IsInitialized || runtime.Selection == null) return;
                if (runtime.StartInvocationCount <= SessionState.GetInt(
                        SelectionStartCountKey, -1))
                {
                    Finish(
                        false,
                        "Result return failed to rebuild the selection runtime.",
                        elapsed);
                    return;
                }
                if (SceneManager.GetSceneByName("OfflineRhythmPreview").isLoaded) return;
                var returned = FrontendNavigation.ReturnedSelection;
                if (LocalMusicSelectionSession.HasSelection || LocalMusicSelectionSession.ChartAsset != null ||
                    LocalMusicSelectionSession.MusicConfigAsset != null || runtime.ChartReadCount != 0 ||
                    _sessionChart != null || _sessionConfig != null)
                {
                    Finish(false, "Returned session retained chart assets or read unexpected charts.", elapsed);
                    return;
                }
                if (returned == null ||
                    runtime.Selection.Music.Id != returned.Music.Id ||
                    runtime.Selection.Live.Difficulty !=
                        returned.Live.Difficulty ||
                    runtime.FocusedMusicId != returned.Music.Id)
                {
                    Finish(
                        false,
                        "Result return did not restore the selected music and difficulty.",
                        elapsed);
                    return;
                }
                var displayedStore = (LocalResultStore)typeof(LocalMusicSelectionRuntime)
                    .GetField("_localResults", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);
                var durableStore = new LocalResultStore();
                if (ReferenceEquals(displayedStore, _storeBeforeLive) ||
                    displayedStore.GetBest(returned.Music.Id, returned.Live.Difficulty) !=
                    durableStore.GetBest(returned.Music.Id, returned.Live.Difficulty) ||
                    displayedStore.GetClearLamp(returned.Music.Id, returned.Live.Difficulty) !=
                    durableStore.GetClearLamp(returned.Music.Id, returned.Live.Difficulty))
                {
                    Finish(false, "Rebuilt selection is not displaying the saved live result.", elapsed);
                    return;
                }
                if (UnityEngine.Object.FindObjectsOfType<EventSystem>().Length != 1)
                {
                    Finish(
                        false,
                        "Result return created a duplicate EventSystem.",
                        elapsed);
                    return;
                }
                if (!SessionState.GetBool(HudFadeKey, false) ||
                    !SessionState.GetBool(PreviewExitKey, false) ||
                    !SessionState.GetBool(ResultCountKey, false))
                {
                    Finish(false, "A presentation animation was not observed: HUD=" +
                        SessionState.GetBool(HudFadeKey, false) + " preview=" +
                        SessionState.GetBool(PreviewExitKey, false) + " result=" +
                        SessionState.GetBool(ResultCountKey, false), elapsed);
                    return;
                }
                var menu = UnityEngine.Object.FindObjectOfType<OfflineMenuRuntime>();
                if (menu == null || !menu.IsReady)
                {
                    Finish(false, "Rebuilt menu was not restored.", elapsed);
                    return;
                }
                menu.Open();
                SessionState.SetString(PhaseKey, "return-menu-open");
                return;
            }
            if (phase == "return-menu-open")
            {
                var menu = UnityEngine.Object.FindObjectOfType<OfflineMenuRuntime>();
                if (menu == null || menu.IsTransitioning ||
                    UnityEngine.Object.FindObjectOfType<GameRuntime>() != null) return;
                if (!menu.IsOpen || menu.Popup.GetComponentsInChildren<Text>().Any(t => t.font == null))
                {
                    Finish(false, "Returning from result invalidated the menu font assets.", elapsed);
                    return;
                }
                CaptureSelection(Path.Combine(Path.GetDirectoryName(ScreenshotPath()), "offline-menu-return.png"));
                menu.Close();
                SessionState.SetString(PhaseKey, "return-menu-closed");
                return;
            }
            if (phase == "return-menu-closed")
            {
                var menu = UnityEngine.Object.FindObjectOfType<OfflineMenuRuntime>();
                if (menu == null || menu.IsTransitioning) return;
                if (menu.IsOpen)
                {
                    Finish(false, "Rebuilt menu could not close after returning from result.", elapsed);
                    return;
                }
                Debug.Log("OPENWDS_PRESENTATION_PLAYMODE hudFade=True previewLeftExit=True resultCount=True returnFocus=True rebuiltMenu=True");
                Finish(true, null, elapsed);
            }
        }

        private static System.Collections.IEnumerator ValidateSelectionBugFixes(
            LocalMusicSelectionRuntime runtime)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(LocalMusicSelectionRuntime);
            var preview = UnityEngine.Object.FindObjectOfType<MusicSelectionPreviewRuntime>();
            while (!preview.IsPlaying) yield return null;
            var playerField = typeof(MusicSelectionPreviewRuntime).GetField("_player", flags);
            var originalPlayer = playerField.GetValue(preview);
            var musicId = runtime.Selection.Music.Id;
            var originalDifficulty = runtime.Selection.Live.Difficulty;
            foreach (var difficulty in new[] { MusicDifficulty.Hard, originalDifficulty })
            {
                type.GetMethod("SelectDifficulty", flags).Invoke(runtime, new object[] { difficulty, true });
                while ((bool)type.GetField("_isRebuildingMusicList", flags).GetValue(runtime)) yield return null;
                if (runtime.Selection.Music.Id != musicId || runtime.Selection.Live.Difficulty != difficulty ||
                    !ReferenceEquals(originalPlayer, playerField.GetValue(preview)) || !preview.IsPlaying)
                    throw new InvalidOperationException("Difficulty change restarted/stopped the same-song preview.");
            }

            var storeField = type.GetField("_localResults", flags);
            var originalStore = storeField.GetValue(runtime);
            var allMusicsField = type.GetField("_allMusics", flags);
            var originalMusics = (LocalMusicEntry[])allMusicsField.GetValue(runtime);
            var temporaryRoot = Path.Combine(Path.GetTempPath(), "OpenWDS-rating-jacket-" + Guid.NewGuid());
            try
            {
                var musics = (LocalMusicEntry[])type.GetField("_allMusics", flags).GetValue(runtime);
                var uncached = musics.Last(m => !runtime.HasLoadedJacket(m.Id) &&
                    m.Lives.Any(l => PlayerRating.IsEligible(m, l)));
                var live = uncached.Lives.First(l => PlayerRating.IsEligible(uncached, l));
                var fixture = new LocalResultStore(temporaryRoot);
                fixture.RecordResult(uncached.Id, live.Difficulty, 101d, out _);
                storeField.SetValue(runtime, fixture);
                type.GetMethod("OpenPlayerRateDialog", flags).Invoke(runtime, null);
                var body = (GameObject)type.GetField("_playerRateDialogBody", flags).GetValue(runtime);
                var row = FindNamedTransform(body.transform, "PlayerRateContent_" + live.Id);
                var jacket = FindNamedTransform(row, "MusicJacketImage").GetComponent<Image>();
                while (jacket.sprite == null || !jacket.enabled) yield return null;
                if (!runtime.HasLoadedJacket(uncached.Id) || jacket.sprite.texture.width < 2 ||
                    jacket.sprite.texture.height < 2)
                    throw new InvalidOperationException("Uncached B30 jacket did not load a valid sprite.");
                // Retail SimpleRateCellView has no jacket; switching back must
                // bind the ready provider value to the new detailed row.
                var loadedSprite = jacket.sprite;
                type.GetField("_playerRateSimpleMode", flags).SetValue(runtime, true);
                type.GetMethod("BindPlayerRatePanel", flags).Invoke(runtime, null);
                yield return null;
                var compact = body.GetComponentsInChildren<Image>()
                    .Where(i => i.name == "MusicJacketImage").ToArray();
                if (compact.Length != 0)
                    throw new InvalidOperationException("Compact B30 layout unexpectedly contains a jacket.");
                type.GetField("_playerRateSimpleMode", flags).SetValue(runtime, false);
                type.GetMethod("BindPlayerRatePanel", flags).Invoke(runtime, null);
                yield return null;
                var cachedImage = body.GetComponentsInChildren<Image>().Single(i => i.name == "MusicJacketImage");
                while (!cachedImage.enabled) yield return null;
                if (cachedImage.sprite != loadedSprite)
                    throw new InvalidOperationException("Reopened detailed B30 row did not bind the cached jacket.");
                var spMusic = musics.First(m => m.Lives.Any(l =>
                    OlivierStars.IsEligible(m, l) && l.Level == 105));
                var spLive = spMusic.Lives.First(l => l.Difficulty == MusicDifficulty.Olivier);
                fixture.RecordOlivierResult(spMusic, spLive, 100.65, ClearLamp.FullCombo, 56, true, false);
                fixture.RecordOlivierResult(spMusic, spLive, 100.5, ClearLamp.AllPerfect, 10, true, false);
                allMusicsField.SetValue(runtime, new[] { spMusic });
                storeField.SetValue(runtime, new LocalResultStore(temporaryRoot));
                type.GetMethod("ShowPlayerRateTab", flags).Invoke(runtime, new object[] { true });
                yield return null;
                var spPanel = FindNamedTransform(body.transform, "SPRatePanel");
                if (FindNamedTransform(spPanel, "RatePercentageText").GetComponent<Text>().text != "92.72%" ||
                    FindNamedTransform(spPanel, "RateFractionText").GetComponent<Text>().text != "102/110" ||
                    FindNamedTransform(spPanel, "RateIcon").GetComponent<Image>().sprite.name != "rainbow")
                    throw new InvalidOperationException("Olivier tab failed persisted component total/percentage/icon binding.");
                CaptureSelection(Path.Combine(Path.GetDirectoryName(ScreenshotPath()), "olivier-stars-selection.png"));
                SessionState.SetBool("OpenWDS.OlivierStarsValidated", true);
                Debug.Log($"OPENWDS_SELECTION_BUG_CHECKS previewPreserved=True jacketMusic={uncached.Id} uncached=True cached=True compactLayout=True");
            }
            finally
            {
                type.GetMethod("ClosePlayerRateDialog", flags).Invoke(runtime, null);
                type.GetField("_playerRateSimpleMode", flags).SetValue(runtime, false);
                storeField.SetValue(runtime, originalStore);
                allMusicsField.SetValue(runtime, originalMusics);
                if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
            }
            yield return ValidateMusicCoverTypes(runtime);
            SessionState.SetBool("OpenWDS.SelectionBugChecks", true);
            SessionState.SetString(PhaseKey, "presentation-select");
        }

        private static System.Collections.IEnumerator ValidateMusicCoverTypes(LocalMusicSelectionRuntime runtime)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(LocalMusicSelectionRuntime);
            var originalMusic = runtime.Selection.Music;
            var originalDifficulty = runtime.Selection.Live.Difficulty;
            var catalog = (LocalMusicCatalog)type.GetField("_catalog", flags).GetValue(runtime);
            var cover = catalog.Musics.First(m => m.MusicCoverType == 2);
            foreach (var music in new[] { originalMusic, cover, originalMusic })
            {
                catalog = (LocalMusicCatalog)type.GetField("_catalog", flags).GetValue(runtime);
                var index = Array.FindIndex(catalog.Musics, m => m.Id == music.Id);
                yield return (System.Collections.IEnumerator)type.GetMethod("SnapPhysicalIndex", flags)
                    .Invoke(runtime, new object[] { catalog.Musics.Length + index, music, false });
                while (runtime.IsFocusTransitionActive) yield return null;
                foreach (var difficulty in new[] { MusicDifficulty.Normal, MusicDifficulty.Hard,
                    MusicDifficulty.Extra, MusicDifficulty.Olivier, originalDifficulty })
                {
                    type.GetMethod("SelectDifficulty", flags).Invoke(runtime, new object[] { difficulty, false });
                    yield return null;
                    var view = (GameObject)type.GetField("_view", flags).GetValue(runtime);
                    var panel = view.transform.Find("TicketMacine/MusicInformationPanel/MusicType");
                    var isOriginal = music.MusicCoverType == 1;
                    var expectedText = isOriginal ? "ORIGINAL" : "COVER";
                    var color = (Color)MusicSelectionPreviewRuntime.DifficultyFrameColor(difficulty);
                    if (runtime.Selection.Music.Id != music.Id ||
                        panel.Find("MusicTypeText").GetComponent<Text>().text != expectedText ||
                        panel.Find("OriginalBackground").GetComponent<Image>().enabled != isOriginal ||
                        panel.Find("CoverBackground").GetComponent<Image>().enabled == isOriginal ||
                        panel.Find("MusicTypeText").GetComponent<Text>().color != (isOriginal ? Color.white : color))
                        throw new InvalidOperationException($"Information panel cover type mismatch: {music.Id}/{difficulty}");
                    var cells = view.GetComponentsInChildren<Transform>()
                        .Where(t => t.name.StartsWith("LocalMusic_")).ToArray();
                    if (cells.Length == 0) throw new InvalidOperationException("No visible music cells to validate.");
                    foreach (var cell in cells)
                    {
                        var entry = catalog.Musics.Single(m => cell.name == "LocalMusic_" + m.Id);
                        var tag = cell.Find("PartsBase/PartsMask/MusicTypePanel");
                        var original = entry.MusicCoverType == 1;
                        if (tag.Find("MusicTypeText").GetComponent<Text>().text != (original ? "ORIGINAL" : "COVER") ||
                            tag.Find("OriginalBackground").GetComponent<Image>().enabled != original ||
                            tag.Find("CoverBackground").GetComponent<Image>().enabled == original ||
                            tag.Find("CoverBackground").GetComponent<Image>().color != color)
                            throw new InvalidOperationException($"Recycled cell cover type mismatch: {entry.Id}/{difficulty}");
                    }
                    if (!isOriginal && difficulty == originalDifficulty)
                        CaptureSelection(Path.Combine(Path.GetDirectoryName(ScreenshotPath()), "music-selection-cover.png"));
                }
            }
            SessionState.SetBool("OpenWDS.MusicCoverTypesValidated", true);
            Debug.Log($"OPENWDS_MUSIC_COVER_TYPES original={originalMusic.Id} cover={cover.Id} difficulties=5 recycledCells=True");
        }

        private static void Finish(bool passed, string failure, double elapsed)
        {
            var report = new Report
            {
                passed = passed,
                introductionMovedDuringCurtain = SessionState.GetBool("OpenWDS.IntroLightMoved", false),
                introductionDelaySeconds = SessionState.GetFloat("OpenWDS.IntroNativeDelay", -1f),
                musicCoverTypesValidated = SessionState.GetBool("OpenWDS.MusicCoverTypesValidated", false),
                hudFadeObserved = SessionState.GetBool(HudFadeKey, false),
                previewLeftExitObserved = SessionState.GetBool(PreviewExitKey, false),
                resultCountObserved = SessionState.GetBool(ResultCountKey, false),
                failure = failure,
                elapsedSeconds = elapsed,
                availableDifficulties = 5,
                selectedDifficulty = MusicDifficulty.Stella.ToString(),
                introductionFocusResumeValid =
                    SessionState.GetBool(IntroductionFocusValidKey, false),
                gameLoaded = passed,
                pauseRetryContinued = passed,
                settingsRestartReloaded = passed,
                retireReturned = passed,
                resultReturned = passed,
                difficultyPreviewPreserved = SessionState.GetBool("OpenWDS.SelectionBugChecks", false),
                uncachedRatingJacketLoaded = SessionState.GetBool("OpenWDS.SelectionBugChecks", false),
                olivierStarsValidated = SessionState.GetBool("OpenWDS.OlivierStarsValidated", false),
                resultsReloaded = passed,
                menuRebuilt = passed,
                screenshotBytes = File.Exists(ScreenshotPath())
                    ? new FileInfo(ScreenshotPath()).Length
                    : 0,
                screenshotNonBackgroundPixels = CountVisiblePixels(
                    ScreenshotPath()),
            };
            var workspace = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", ".."));
            var output = Path.Combine(
                workspace,
                "reverse",
                "reports",
                "unity-music-selection-playmode-validation.json");
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            if (!passed) SessionState.SetString(ErrorKey, failure ?? "unknown failure");
            Debug.Log(
                $"OPENWDS_MUSIC_SELECTION_PLAYMODE_RESULT passed={passed} " +
                $"elapsed={elapsed:F2} failure={failure}");
            SessionState.SetString(PhaseKey, ExitPhase);
            EditorApplication.isPlaying = false;
        }

        private static Transform FindNamedTransform(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (var index = 0; index < root.childCount; index++)
            {
                var found = FindNamedTransform(root.GetChild(index), name);
                if (found != null) return found;
            }
            return null;
        }

        private static void OnLog(
            string condition,
            string stackTrace,
            LogType type)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (type != LogType.Error && type != LogType.Exception &&
                type != LogType.Assert)
                return;
            if (string.IsNullOrEmpty(SessionState.GetString(ErrorKey, "")))
                SessionState.SetString(
                    ErrorKey, condition + "\n" + stackTrace);
        }

        private static double Elapsed()
        {
            return (DateTime.UtcNow - new DateTime(long.Parse(
                SessionState.GetString(StartedKey, "0")), DateTimeKind.Utc))
                .TotalSeconds;
        }

        private static string ScreenshotPath()
        {
            var workspace = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", ".."));
            return Path.Combine(
                workspace,
                "reverse",
                "reports",
                "unity-music-selection.png");
        }

        private static string NoteSpeedScreenshotPath()
        {
            var workspace = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", ".."));
            return Path.Combine(
                workspace,
                "reverse",
                "reports",
                "unity-music-selection-note-speed.png");
        }

        internal static int CaptureSelection(string path)
        {
            var canvas = GameObject.Find("MainCanvas")?.GetComponent<Canvas>();
            var camera = GameObject.Find("UICamera")?.GetComponent<Camera>();
            if (canvas == null || camera == null)
                throw new InvalidOperationException(
                    "MusicSelection capture requires MainCanvas and UICamera.");
            const int width = 1920;
            const int height = 1200;
            var rootCanvases = UnityEngine.Object.FindObjectsOfType<Canvas>()
                .Where(item =>
                    item != null &&
                    item.isRootCanvas &&
                    item.gameObject.activeInHierarchy)
                .ToArray();
            var previousModes = rootCanvases
                .Select(item => item.renderMode)
                .ToArray();
            var previousCameras = rootCanvases
                .Select(item => item.worldCamera)
                .ToArray();
            var previousDistances = rootCanvases.Select(item => item.planeDistance).ToArray();
            var previousAspect = camera.aspect;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = new RenderTexture(width, height, 24);
            var image = new Texture2D(
                width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                foreach (var rootCanvas in rootCanvases)
                {
                    rootCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                    rootCanvas.worldCamera = camera;
                    // Tilted retail UI extends on both sides of the Canvas.
                    // At distance 1, its upper edge crosses the near plane.
                    rootCanvas.planeDistance = (camera.nearClipPlane + camera.farClipPlane) * .5f;
                }
                Canvas.ForceUpdateCanvases();
                foreach (var panel in UnityEngine.Object.FindObjectsOfType<RectTransform>().Where(t => t.name == "MusicInformationPanel"))
                {
                    var corners = new Vector3[4]; panel.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var depth = camera.WorldToViewportPoint(corner).z;
                        if (depth <= camera.nearClipPlane || depth >= camera.farClipPlane)
                            throw new InvalidOperationException("Tilted music panel crosses capture camera clip plane.");
                    }
                }
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                var background = camera.backgroundColor;
                var visible = 0;
                foreach (var pixel in image.GetPixels32())
                {
                    if (Mathf.Abs(pixel.r / 255f - background.r) > 0.01f ||
                        Mathf.Abs(pixel.g / 255f - background.g) > 0.01f ||
                        Mathf.Abs(pixel.b / 255f - background.b) > 0.01f)
                        visible++;
                }
                return visible;
            }
            finally
            {
                for (var index = 0; index < rootCanvases.Length; index++)
                {
                    if (rootCanvases[index] == null) continue;
                    rootCanvases[index].renderMode = previousModes[index];
                    rootCanvases[index].worldCamera = previousCameras[index];
                    rootCanvases[index].planeDistance = previousDistances[index];
                }
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                Canvas.ForceUpdateCanvases();
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static int CountVisiblePixels(string path)
        {
            if (!File.Exists(path)) return 0;
            var data = File.ReadAllBytes(path);
            var image = new Texture2D(2, 2);
            try
            {
                if (!image.LoadImage(data)) return 0;
                var visible = 0;
                foreach (var pixel in image.GetPixels32())
                {
                    if (pixel.r != 6 || pixel.g != 9 || pixel.b != 18)
                        visible++;
                }
                return visible;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
