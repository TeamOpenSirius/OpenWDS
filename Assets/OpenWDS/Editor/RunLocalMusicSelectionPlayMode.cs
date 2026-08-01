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
        private const string PhaseKey = "OpenWDS.MusicSelectionPlayMode.Phase";
        private const string ErrorKey = "OpenWDS.MusicSelectionPlayMode.Error";
        private const string StartedKey = "OpenWDS.MusicSelectionPlayMode.Started";
        private const string CaptureKey = "OpenWDS.MusicSelectionPlayMode.Captured";
        private const string ScrollStartedKey =
            "OpenWDS.MusicSelectionPlayMode.ScrollStarted";
        private const string BookmarkFlagsKey =
            "OpenWDS.MusicSelectionPlayMode.BookmarkFlags";
        private const string IntroductionFocusTimeKey =
            "OpenWDS.MusicSelectionPlayMode.IntroductionFocusTime";
        private const string IntroductionFocusValidKey =
            "OpenWDS.MusicSelectionPlayMode.IntroductionFocusValid";
        private const string ExitPhase = "exit";
        private static string _lastLoggedPhase;

        [Serializable]
        private sealed class Report
        {
            public bool passed;
            public string failure;
            public double elapsedSeconds;
            public int availableDifficulties;
            public string selectedDifficulty;
            public bool titleBound;
            public bool listCellBound;
            public bool scrollingValid;
            public bool arrowNavigationValid;
            public bool randomSelectionValid;
            public bool bookmarkFlowValid;
            public bool playerRateDialogValid;
            public bool introductionFocusResumeValid;
            public bool allDifficultyMarkersRefreshed;
            public bool olivierRomanValid;
            public bool roundedMarkerSpriteValid;
            public bool gameLoaded;
            public bool retireReturned;
            public bool resultReturned;
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

        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException(
                    "MusicSelection Play Mode gate requires batch mode.");
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException(
                    "LocalMusicSelection scene is missing.", ScenePath);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(PhaseKey, "selection");
            SessionState.SetString(StartedKey, DateTime.UtcNow.Ticks.ToString());
            SessionState.EraseString(ErrorKey);
            SessionState.SetBool(CaptureKey, false);
            SessionState.SetBool(IntroductionFocusValidKey, false);
            var screenshot = ScreenshotPath();
            if (File.Exists(screenshot)) File.Delete(screenshot);
            var noteSpeedScreenshot = NoteSpeedScreenshotPath();
            if (File.Exists(noteSpeedScreenshot))
                File.Delete(noteSpeedScreenshot);
            var filterScreenshot = FilterScreenshotPath();
            if (File.Exists(filterScreenshot))
                File.Delete(filterScreenshot);
            var vocalFilterScreenshot = VocalFilterScreenshotPath();
            if (File.Exists(vocalFilterScreenshot))
                File.Delete(vocalFilterScreenshot);
            var sortScreenshot = SortScreenshotPath();
            if (File.Exists(sortScreenshot))
                File.Delete(sortScreenshot);
            var spRateScreenshot = SpRateScreenshotPath();
            if (File.Exists(spRateScreenshot))
                File.Delete(spRateScreenshot);
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
            if (elapsed > 240d)
            {
                Finish(false, "MusicSelection Play Mode timed out.", elapsed);
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            var firstError = SessionState.GetString(ErrorKey, "");
            if (!string.IsNullOrEmpty(firstError))
            {
                Finish(false, "Play Mode logged an error: " + firstError, elapsed);
                return;
            }

            var phase = SessionState.GetString(PhaseKey, "");
            if (_lastLoggedPhase != phase)
            {
                _lastLoggedPhase = phase;
                Debug.Log($"OPENWDS_PLAYMODE_PHASE {phase}");
            }
            if (phase == "selection")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null || runtime.Selection == null) return;
                var title = GameObject.Find(
                    "MusicSelectionView/TicketMacine/MusicInformationPanel/" +
                    "ScrollMusicTitle/ScrollMusicTitleText")?.GetComponent<Text>();
                var cell = GameObject.Find("LocalMusic_1");
                var tenthCell = GameObject.Find("LocalMusic_10");
                var preview = UnityEngine.Object.FindObjectOfType<
                    RecoveredMusicSelectionPreviewRuntime>();
                var ratePanel = GameObject.Find(
                    "MusicSelectionView/DefaultLivePanels/ScorePanel/RatePanel")
                    ?.GetComponent<RectTransform>();
                var achievementFrame = ratePanel != null
                    ? ratePanel.Find("AchievementRateFrame") as RectTransform
                    : null;
                var musicRateFrame = ratePanel != null
                    ? ratePanel.Find("MusicRateFrame") as RectTransform
                    : null;
                var achievementRate = achievementFrame != null
                    ? achievementFrame.Find("AchievementRate")
                        ?.GetComponent<Text>()
                    : null;
                if (runtime.AvailableDifficultyCount != 5 ||
                    runtime.BoundCellCount != 10 ||
                    runtime.PhysicalCellCount != 30 ||
                    runtime.FocusedMusicId != 1 ||
                    runtime.Selection.Live.Difficulty !=
                    RecoveredMusicDifficulty.Stella ||
                    title == null || title.text != "ワナビスタ！" ||
                    cell == null || tenthCell == null ||
                    preview == null || preview.SpectrumBarCount != 76 ||
                    preview.MusicId != 1 ||
                    ratePanel == null || achievementFrame == null ||
                    musicRateFrame == null || achievementRate == null ||
                    Mathf.Abs(ratePanel.rect.width - 588f) > 0.1f ||
                    Mathf.Abs(achievementFrame.rect.width - 316f) > 0.1f ||
                    Mathf.Abs(musicRateFrame.rect.width - 236f) > 0.1f ||
                    achievementFrame.anchoredPosition.x +
                        achievementFrame.rect.xMax >
                        ratePanel.rect.xMax + 0.1f ||
                    musicRateFrame.anchoredPosition.x +
                        musicRateFrame.rect.xMax >
                        ratePanel.rect.xMax + 0.1f ||
                    !achievementRate.text.Contains("<size=26>00%</size>"))
                {
                    Finish(false, "Recovered selection view binding is invalid.", elapsed);
                    return;
                }
                if (runtime.IsFocusTransitionActive) return;
                var focusedRect = cell.GetComponent<RectTransform>();
                var focusedPartsBase =
                    cell.transform.Find("PartsBase") as RectTransform;
                var focusedOverlay = cell.transform.Find(
                    "PartsBase/PartsMask/JacketOverlayImage")?.GetComponent<Image>();
                var focusedTitle = cell.transform.Find(
                    "PartsBase/PartsMask/MusicTitle/BodyText")?.GetComponent<Text>();
                var focusedVocals = cell.transform.Find(
                    "PartsBase/PartsMask/Vocals/BodyText")?.GetComponent<Text>();
                var focusedVocalsScroll = cell.transform.Find(
                    "PartsBase/PartsMask/Vocals")
                    ?.GetComponent<RecoveredScrollTextRuntime>();
                var focusedMask = cell.transform.Find(
                    "PartsBase/PartsMask/FocusMask")?.GetComponent<Image>();
                var focusedPartsMask = cell.transform.Find(
                    "PartsBase/PartsMask") as RectTransform;
                var focusedDifficultyFrame = cell.transform.Find(
                    "PartsBase/PartsMask/FocusMask/DifficultyFrameImage")
                    ?.GetComponent<Image>();
                var focusedDifficultyLevel = cell.transform.Find(
                    "PartsBase/PartsMask/DifficultyLevel") as RectTransform;
                var focusedDifficultyLevelText = cell.transform.Find(
                    "PartsBase/PartsMask/DifficultyLevel/DifficultyLevelText")
                    as RectTransform;
                var focusedMaskSprite = focusedMask != null
                    ? focusedMask.overrideSprite ?? focusedMask.sprite
                    : null;
                var informationTitle = GameObject.Find(
                    "MusicSelectionView/TicketMacine/MusicInformationPanel/" +
                    "ScrollMusicTitle/ScrollMusicTitleText")?.GetComponent<Text>();
                var informationCreator = GameObject.Find(
                    "MusicSelectionView/TicketMacine/MusicInformationPanel/" +
                    "ScrollCreator/ScrollCreatorText")?.GetComponent<Text>();
                var informationOriginal = GameObject.Find(
                    "MusicSelectionView/TicketMacine/MusicInformationPanel/" +
                    "MusicType/OriginalBackground")?.GetComponent<Image>();
                var informationCover = GameObject.Find(
                    "MusicSelectionView/TicketMacine/MusicInformationPanel/" +
                    "MusicType/CoverBackground")?.GetComponent<Image>();
                var fourthPartsBase = GameObject.Find("LocalMusic_4")
                    ?.transform.Find("PartsBase") as RectTransform;
                var secondRect = GameObject.Find("LocalMusic_2")
                    ?.GetComponent<RectTransform>();
                var secondVocals = secondRect != null
                    ? secondRect.transform.Find(
                        "PartsBase/PartsMask/Vocals/BodyText")?.GetComponent<Text>()
                    : null;
                var secondDifficultyFrame = secondRect != null
                    ? secondRect.transform.Find(
                        "PartsBase/PartsMask/FocusMask/DifficultyFrameImage")
                        ?.GetComponent<Image>()
                    : null;
                var focusedRateGrade = cell.transform.Find(
                    "PartsBase/PartsMask/RateGradeBadge")?.GetComponent<Image>();
                var secondRateGrade = secondRect != null
                    ? secondRect.transform.Find(
                        "PartsBase/PartsMask/RateGradeBadge")?.GetComponent<Image>()
                    : null;
                var focusedLamp = cell.transform.Find(
                    "PartsBase/PartsMask/ClearLamps/ClearLampStella")
                    as RectTransform;
                var focusedLampImage = focusedLamp != null
                    ? focusedLamp.Find("NormalClearLamp") as RectTransform
                    : null;
                var secondLamp = secondRect != null
                    ? secondRect.transform.Find(
                        "PartsBase/PartsMask/ClearLamps/ClearLampStella")
                        as RectTransform
                    : null;
                var secondLampImage = secondLamp != null
                    ? secondLamp.Find("NormalClearLamp") as RectTransform
                    : null;
                var focusedBottom = focusedRect != null
                    ? focusedRect.anchoredPosition.y - focusedRect.rect.height * 0.5f
                    : 0f;
                var secondTop = secondRect != null
                    ? secondRect.anchoredPosition.y + secondRect.rect.height * 0.5f
                    : 0f;
                Debug.Log(
                    "OPENWDS_SELECTION_STYLE " +
                    $"partsMask={focusedPartsMask?.sizeDelta} " +
                    $"frameEnabled={focusedDifficultyFrame?.enabled} " +
                    $"frameColor={focusedDifficultyFrame?.color} " +
                    $"framePos={((RectTransform)focusedDifficultyFrame?.transform)?.anchoredPosition} " +
                    $"labelStella={DifficultyLabelUses(RecoveredMusicDifficulty.Stella, Color.white)} " +
                    $"labelNormal={DifficultyLabelUses(RecoveredMusicDifficulty.Normal, new Color32(83, 84, 92, 255))} " +
                    $"titleAlign={informationTitle?.alignment} " +
                    $"creatorAlign={informationCreator?.alignment} " +
                    $"original={informationOriginal?.enabled} cover={informationCover?.enabled}");
                Debug.Log(
                    "OPENWDS_SELECTION_RESULT_BADGES " +
                    $"focusedGrade={focusedRateGrade?.rectTransform.sizeDelta}/" +
                    $"{focusedRateGrade?.preserveAspect} " +
                    $"offsetGrade={secondRateGrade?.rectTransform.sizeDelta}/" +
                    $"{secondRateGrade?.preserveAspect} " +
                    $"focusedLamp={focusedLamp?.sizeDelta}/" +
                    $"{focusedLampImage?.sizeDelta}/{focusedLampImage?.rect.size} " +
                    $"offsetLamp={secondLamp?.sizeDelta}/{secondLampImage?.sizeDelta}/" +
                    $"{secondLampImage?.rect.size}");
                if (focusedRect == null ||
                    Mathf.Abs(focusedRect.pivot.y - 0.5f) > 0.01f ||
                    Mathf.Abs(focusedRect.sizeDelta.x - 960f) > 0.1f ||
                    Mathf.Abs(focusedRect.sizeDelta.y - 196f) > 0.1f ||
                    Mathf.Abs(focusedRect.anchoredPosition.x - 24f) > 0.1f ||
                    secondRect == null ||
                    Mathf.Abs(Mathf.Abs(focusedBottom - secondTop) - 20f) > 0.1f ||
                    focusedPartsBase == null ||
                    Mathf.Abs(focusedPartsBase.sizeDelta.x - 960f) > 0.1f ||
                    Mathf.Abs(focusedPartsBase.anchoredPosition.x) > 0.1f ||
                    focusedPartsMask == null ||
                    Mathf.Abs(focusedPartsMask.sizeDelta.x) > 0.1f ||
                    Mathf.Abs(focusedPartsMask.rect.width - 960f) > 0.1f ||
                    focusedMask == null ||
                    Mathf.Abs(focusedMask.rectTransform.rect.width - 363.41f) > 0.1f ||
                    focusedDifficultyFrame == null ||
                    focusedDifficultyFrame.enabled ||
                    !ColorsMatch(
                        focusedDifficultyFrame.color,
                        RecoveredMusicSelectionPreviewRuntime.DifficultyFrameColor(
                            RecoveredMusicDifficulty.Stella)) ||
                    Mathf.Abs(
                        ((RectTransform)focusedDifficultyFrame.transform)
                            .anchoredPosition.x - 5f) > 0.1f ||
                    focusedDifficultyLevel == null ||
                    Vector2.Distance(
                        focusedDifficultyLevel.sizeDelta,
                        new Vector2(88f, 88f)) > 0.1f ||
                    focusedDifficultyLevelText == null ||
                    Vector2.Distance(
                        focusedDifficultyLevelText.anchoredPosition,
                        new Vector2(14f, 19f)) > 0.1f ||
                    focusedRateGrade == null ||
                    !focusedRateGrade.preserveAspect ||
                    Vector2.Distance(
                        focusedRateGrade.rectTransform.sizeDelta,
                        new Vector2(108f, 44f)) > 0.1f ||
                    secondRateGrade == null ||
                    !secondRateGrade.preserveAspect ||
                    Vector2.Distance(
                        secondRateGrade.rectTransform.sizeDelta,
                        new Vector2(98f, 40f)) > 0.1f ||
                    focusedLamp == null ||
                    Vector2.Distance(
                        focusedLamp.sizeDelta,
                        new Vector2(52f, 52f)) > 0.1f ||
                    focusedLampImage == null ||
                    Vector2.Distance(
                        focusedLampImage.rect.size,
                        new Vector2(40f, 40f)) > 0.1f ||
                    secondLamp == null ||
                    Vector2.Distance(
                        secondLamp.sizeDelta,
                        new Vector2(46f, 46f)) > 0.1f ||
                    secondLampImage == null ||
                    Vector2.Distance(
                        secondLampImage.rect.size,
                        new Vector2(34f, 34f)) > 0.1f ||
                    secondDifficultyFrame == null ||
                    !secondDifficultyFrame.enabled ||
                    fourthPartsBase == null ||
                    fourthPartsBase.anchoredPosition.x < 30f ||
                    focusedOverlay == null ||
                    !focusedOverlay.enabled ||
                    focusedTitle == null ||
                    focusedTitle.color != Color.white ||
                    focusedVocals == null ||
                    !focusedVocals.enabled ||
                    focusedVocals.text !=
                    "鳳ここな、静香、カトリナ・グリーベル、新妻八恵、柳場ぱんだ、流石知冴" ||
                    focusedVocalsScroll == null ||
                    !focusedVocalsScroll.IsScrollable ||
                    !focusedVocalsScroll.IsAnimating ||
                    secondVocals == null ||
                    secondVocals.enabled ||
                    focusedMaskSprite == null ||
                    focusedMaskSprite.name !=
                    "img_live_common_list_jacket_mask" ||
                    !DifficultyButtonUses(
                        RecoveredMusicDifficulty.Stella,
                        "img_live_common_level_stella") ||
                    !DifficultyButtonUses(
                        RecoveredMusicDifficulty.Normal,
                        "img_live_common_level_off") ||
                    !DifficultyTextUses(
                        RecoveredMusicDifficulty.Stella,
                        Color.white) ||
                    !DifficultyTextUses(
                        RecoveredMusicDifficulty.Normal,
                        new Color32(83, 84, 92, 255)) ||
                    !DifficultyLabelUses(
                        RecoveredMusicDifficulty.Stella,
                        Color.white) ||
                    !DifficultyLabelUses(
                        RecoveredMusicDifficulty.Normal,
                        new Color32(83, 84, 92, 255)) ||
                    informationTitle == null ||
                    informationTitle.alignment != TextAnchor.MiddleLeft ||
                    informationCreator == null ||
                    informationCreator.alignment != TextAnchor.MiddleLeft ||
                    informationOriginal == null ||
                    !informationOriginal.enabled ||
                    informationCover == null ||
                    informationCover.enabled ||
                    UnityEngine.Object.FindObjectsOfType<EventSystem>().Length != 1)
                {
                    Finish(false, "Selected cell or difficulty focus graphic is invalid.", elapsed);
                    return;
                }
                var spectrumBar = GameObject.Find(
                    "MusicSelectionView/TicketMacine/MusicInformationPanel/" +
                    "UIParticleSpectrumViewer/Top/Mask/SpectrumNoteTop")
                    ?.GetComponent<RectTransform>();
                var spectrumLastBar = GameObject.Find(
                    "MusicSelectionView/TicketMacine/MusicInformationPanel/" +
                    "UIParticleSpectrumViewer/Top/Mask/SpectrumNoteTop (37)")
                    ?.GetComponent<RectTransform>();
                var spectrumSecondBar = GameObject.Find(
                    "MusicSelectionView/TicketMacine/MusicInformationPanel/" +
                    "UIParticleSpectrumViewer/Top/Mask/SpectrumNoteTop (1)")
                    ?.GetComponent<RectTransform>();
                var spectrumBottomBar = GameObject.Find(
                    "MusicSelectionView/TicketMacine/MusicInformationPanel/" +
                    "UIParticleSpectrumViewer/Bottom/Mask/SpectrumNoteBottom (1)")
                    ?.GetComponent<RectTransform>();
                var jacketFront = GameObject.Find(
                    "MusicSelectionView/TicketMacine/MusicInformationPanel/" +
                    "MusicJackets/MusicJacketFront")?.GetComponent<Image>();
                var spectrumTop = spectrumBar != null
                    ? spectrumBar.transform.parent?.parent as RectTransform
                    : null;
                var spectrumBottom = spectrumBottomBar != null
                    ? spectrumBottomBar.transform.parent?.parent as RectTransform
                    : null;
                var spectrumViewer = spectrumTop != null
                    ? spectrumTop.parent as RectTransform
                    : null;
                var jacketAnimation = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Assets/Resources/AnimationClip/" +
                    "BackgroundMusicJacket_onFocus_anim.anim");
                var jacketAlphaCurve = false;
                var jacketAlphaFadeValid = false;
                if (jacketAnimation != null)
                {
                    foreach (var curve in
                             AnimationUtility.GetCurveBindings(jacketAnimation))
                    {
                        if (curve.propertyName == "m_Color.a" &&
                            curve.type == typeof(Image))
                        {
                            jacketAlphaCurve = true;
                            var alpha =
                                AnimationUtility.GetEditorCurve(
                                    jacketAnimation, curve);
                            jacketAlphaFadeValid =
                                alpha != null &&
                                alpha.Evaluate(0.5f) > 0.001f &&
                                alpha.Evaluate(0.5f) < 0.155f &&
                                alpha.Evaluate(4.5f) > 0.001f &&
                                alpha.Evaluate(4.5f) < 0.155f &&
                                Mathf.Abs(alpha.Evaluate(5f)) < 0.001f &&
                                Mathf.Abs(alpha.Evaluate(6f)) < 0.001f &&
                                alpha.Evaluate(6.5f) > 0.001f &&
                                alpha.Evaluate(6.5f) < 0.155f;
                            break;
                        }
                    }
                }
                Debug.Log(
                    "OPENWDS_SELECTION_SCAN " +
                    $"topY={spectrumTop?.anchoredPosition.y} " +
                    $"bottomY={spectrumBottom?.anchoredPosition.y} " +
                    $"alphaBinding={jacketAlphaCurve} " +
                    $"alphaFade={jacketAlphaFadeValid} " +
                    $"focusTween={runtime.FocusTweenObservedIntermediate}");
                var jacketCornersInViewer = new Vector3[4];
                if (jacketFront != null && spectrumViewer != null)
                {
                    var worldCorners = new Vector3[4];
                    jacketFront.rectTransform.GetWorldCorners(worldCorners);
                    for (var index = 0; index < worldCorners.Length; index++)
                        jacketCornersInViewer[index] =
                            spectrumViewer.InverseTransformPoint(worldCorners[index]);
                }
                var topBaseInViewer =
                    spectrumSecondBar != null && spectrumViewer != null
                        ? spectrumViewer.InverseTransformPoint(
                            spectrumSecondBar.TransformPoint(Vector3.zero))
                        : Vector3.zero;
                var bottomBaseInViewer =
                    spectrumBottomBar != null && spectrumViewer != null
                        ? spectrumViewer.InverseTransformPoint(
                            spectrumBottomBar.TransformPoint(Vector3.zero))
                        : Vector3.zero;
                Debug.Log(
                    "OPENWDS_SPECTRUM_REFERENCE " +
                    $"viewerRect={spectrumViewer?.rect} " +
                    $"topMaskScale={spectrumBar?.parent.localScale} " +
                    $"bottomMaskScale={spectrumBottomBar?.parent.localScale} " +
                    $"jacketBL={jacketCornersInViewer[0]} " +
                    $"jacketTL={jacketCornersInViewer[1]} " +
                    $"jacketTR={jacketCornersInViewer[2]} " +
                    $"jacketBR={jacketCornersInViewer[3]} " +
                    $"topBase={topBaseInViewer} " +
                    $"bottomBase={bottomBaseInViewer} " +
                    $"canvas={spectrumViewer?.GetComponent<Canvas>() != null}");
                if (spectrumBar == null ||
                    spectrumLastBar == null ||
                    spectrumSecondBar == null ||
                    spectrumBottomBar == null ||
                    jacketFront == null ||
                    jacketFront.preserveAspect ||
                    !spectrumBar.gameObject.activeInHierarchy ||
                    Mathf.Abs(spectrumBar.sizeDelta.x - 10f) > 0.1f ||
                    spectrumBar.sizeDelta.y < 9.9f ||
                    Mathf.Abs(spectrumBar.anchoredPosition.x - 740f) > 0.1f ||
                    Mathf.Abs(spectrumSecondBar.anchoredPosition.x - 20f) > 0.1f ||
                    Mathf.Abs(spectrumLastBar.anchoredPosition.x - 740f) > 0.1f ||
                    spectrumLastBar.GetComponent<Image>()?.enabled != false ||
                    Mathf.Abs(spectrumLastBar.sizeDelta.y) > 0.1f ||
                    Mathf.Abs(spectrumSecondBar.pivot.y - 1f) > 0.01f ||
                    Mathf.Abs(spectrumBottomBar.pivot.y) > 0.01f ||
                    spectrumTop == null ||
                    Mathf.Abs(spectrumTop.anchoredPosition.y) > 0.1f ||
                    spectrumBottom == null ||
                    Mathf.Abs(spectrumBottom.anchoredPosition.y) > 0.1f ||
                    spectrumViewer == null ||
                    spectrumViewer.GetComponent<Canvas>() == null ||
                    Vector3.Distance(
                        spectrumBar.parent.localScale,
                        Vector3.one) > 0.01f ||
                    Vector3.Distance(
                        spectrumBottomBar.parent.localScale,
                        Vector3.one) > 0.01f ||
                    Vector3.Distance(
                        jacketCornersInViewer[0],
                        new Vector3(
                            spectrumViewer.rect.xMin,
                            spectrumViewer.rect.yMin,
                            0f)) > 0.1f ||
                    Vector3.Distance(
                        jacketCornersInViewer[1],
                        new Vector3(
                            spectrumViewer.rect.xMin,
                            spectrumViewer.rect.yMax,
                            0f)) > 0.1f ||
                    Vector3.Distance(
                        jacketCornersInViewer[2],
                        new Vector3(
                            spectrumViewer.rect.xMax,
                            spectrumViewer.rect.yMax,
                            0f)) > 0.1f ||
                    Vector3.Distance(
                        jacketCornersInViewer[3],
                        new Vector3(
                            spectrumViewer.rect.xMax,
                            spectrumViewer.rect.yMin,
                            0f)) > 0.1f ||
                    Mathf.Abs(topBaseInViewer.y - spectrumViewer.rect.yMax) >
                    0.1f ||
                    Mathf.Abs(
                        bottomBaseInViewer.y - spectrumViewer.rect.yMin) >
                    0.1f ||
                    !jacketAlphaCurve ||
                    !jacketAlphaFadeValid ||
                    !runtime.FocusTweenObservedIntermediate ||
                    !ColorsMatch(
                        RecoveredMusicSelectionPreviewRuntime.DifficultyFrameColor(
                            RecoveredMusicDifficulty.Normal),
                        new Color32(0, 195, 220, 255)) ||
                    Vector2.Distance(
                        jacketFront.rectTransform.rect.size,
                        ((RectTransform)spectrumBar.transform.parent.parent.parent)
                            .rect.size) > 0.1f)
                {
                    Finish(false, "Recovered jacket spectrum geometry is invalid.", elapsed);
                    return;
                }
                var spectrumParticle = spectrumBar.GetComponentInChildren<
                    ParticleSystem>(true);
                var spectrumBottomParticle =
                    spectrumBottomBar.GetComponentInChildren<
                        ParticleSystem>(true);
                var spectrumImage = spectrumBar.GetComponent<Image>();
                var spectrumRenderer = spectrumParticle != null
                    ? spectrumParticle.GetComponent<ParticleSystemRenderer>()
                    : null;
                Graphic spectrumParticleGraphic = null;
                var spectrumMask = spectrumBar.transform.parent;
                var spectrumBoundsValid = true;
                if (spectrumViewer != null)
                {
                    foreach (var rect in
                             spectrumViewer.GetComponentsInChildren<
                                 RectTransform>(true))
                    {
                        if (!rect.name.StartsWith(
                                "SpectrumNote",
                                StringComparison.Ordinal))
                            continue;
                        var rectImage = rect.GetComponent<Image>();
                        if (rectImage != null && !rectImage.enabled) continue;
                        if (rect.sizeDelta.y < 9.99f ||
                            rect.sizeDelta.y > 60.01f)
                        {
                            spectrumBoundsValid = false;
                            break;
                        }
                    }
                }
                if (spectrumMask != null)
                {
                    foreach (var graphic in
                             spectrumMask.GetComponents<Graphic>())
                    {
                        if (graphic is Image) continue;
                        spectrumParticleGraphic = graphic;
                        break;
                    }
                }
                if (spectrumParticle == null ||
                    spectrumBottomParticle == null ||
                    !spectrumParticle.gameObject.activeInHierarchy ||
                    spectrumImage == null ||
                    Mathf.Abs(spectrumImage.color.a - 0.5f) > 0.01f ||
                    !spectrumBoundsValid ||
                    spectrumRenderer == null ||
                    spectrumRenderer.enabled ||
                    spectrumParticleGraphic == null ||
                    !spectrumParticleGraphic.enabled ||
                    Mathf.Abs(
                        spectrumParticle.shape.position.y +
                        spectrumBar.sizeDelta.y / 100f) > 0.01f ||
                    Mathf.Abs(
                        spectrumBottomParticle.shape.position.y -
                        spectrumBottomBar.sizeDelta.y / 100f) > 0.01f ||
                    Mathf.Abs(
                        spectrumParticle.main.startColor.color.a - 1f) > 0.01f ||
                    !ColorsMatch(
                        new Color(
                            spectrumImage.color.r,
                            spectrumImage.color.g,
                            spectrumImage.color.b,
                            1f),
                        spectrumParticle.main.startColor.color) ||
                    Mathf.Abs(
                        RecoveredMusicSelectionPreviewRuntime.SpectrumHeight(0f) -
                        10f) > 0.01f ||
                    Mathf.Abs(
                        RecoveredMusicSelectionPreviewRuntime.SpectrumHeight(10f) -
                        60f) > 0.01f)
                {
                    Finish(false, "Spectrum UIParticle render ownership is invalid.", elapsed);
                    return;
                }
                var spectrumCorner = RectTransformUtility.WorldToScreenPoint(
                    null,
                    spectrumSecondBar.TransformPoint(spectrumSecondBar.rect.center));
                Debug.Log(
                    $"OPENWDS_SPECTRUM_GEOMETRY screen={spectrumCorner} " +
                    $"size={spectrumBar.rect.size} " +
                    $"imageColor={spectrumImage.color} " +
                    $"particleColor={spectrumParticle.main.startColor.color} " +
                    $"zeroHeight={RecoveredMusicSelectionPreviewRuntime.SpectrumHeight(0f):F2} " +
                    $"active={spectrumBar.gameObject.activeInHierarchy}");
                var scroll = runtime.ListScrollRect;
                if (scroll == null || scroll.content == null ||
                    scroll.viewport == null ||
                    scroll.content.rect.height <= scroll.viewport.rect.height)
                {
                    Finish(false, "Recovered music list is not scrollable.", elapsed);
                    return;
                }
                var filterButton = GameObject.Find(
                    "MusicSelectionHeaderView/FilterButtonPanel/" +
                    "FilterSettingButton")?.GetComponent<Button>();
                if (filterButton == null || !filterButton.interactable)
                {
                    Finish(false, "MusicSelection HUD filter is unavailable.", elapsed);
                    return;
                }
                if (!runtime.AreArrowAnimationsRunning)
                {
                    Finish(false, "MusicSelection arrow animation is not running.", elapsed);
                    return;
                }
                var arrowDown = GameObject.Find(
                    "MusicSelectionView/ArrowDownButton")?.GetComponent<Button>();
                if (arrowDown == null)
                {
                    Finish(false, "MusicSelection down arrow is unavailable.", elapsed);
                    return;
                }
                SessionState.SetString(PhaseKey, "selection-arrow-down");
                SessionState.SetFloat(
                    ScrollStartedKey,
                    (float)EditorApplication.timeSinceStartup);
                arrowDown.onClick.Invoke();
                return;
            }

            if (phase == "selection-arrow-down")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null || runtime.IsFocusTransitionActive ||
                    runtime.FocusedMusicId != 2)
                    return;
                var arrowDown = GameObject.Find(
                    "MusicSelectionView/ArrowDownButton")?.GetComponent<Button>();
                if (arrowDown == null)
                {
                    Finish(false, "MusicSelection down arrow disappeared.", elapsed);
                    return;
                }
                SessionState.SetString(
                    PhaseKey, "selection-arrow-down-next-group");
                arrowDown.onClick.Invoke();
                return;
            }

            if (phase == "selection-arrow-down-next-group")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                // Stella Lv 26 (Music 2) must jump to the next level group,
                // Lv 28 (Music 7), instead of adjacent Music 3 at Lv 25.
                if (runtime == null || runtime.IsFocusTransitionActive ||
                    runtime.FocusedMusicId != 7)
                    return;
                var arrowUp = GameObject.Find(
                    "MusicSelectionView/ArrowUpButton")?.GetComponent<Button>();
                if (arrowUp == null)
                {
                    Finish(false, "MusicSelection up arrow is unavailable.", elapsed);
                    return;
                }
                SessionState.SetString(PhaseKey, "selection-arrow-up");
                arrowUp.onClick.Invoke();
                return;
            }

            if (phase == "selection-arrow-up")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null || runtime.IsFocusTransitionActive ||
                    runtime.FocusedMusicId != 2)
                    return;
                var arrowUp = GameObject.Find(
                    "MusicSelectionView/ArrowUpButton")?.GetComponent<Button>();
                if (arrowUp == null)
                {
                    Finish(false, "MusicSelection up arrow disappeared.", elapsed);
                    return;
                }
                SessionState.SetString(
                    PhaseKey, "selection-arrow-up-return");
                arrowUp.onClick.Invoke();
                return;
            }

            if (phase == "selection-arrow-up-return")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null || runtime.IsFocusTransitionActive ||
                    runtime.FocusedMusicId != 1)
                    return;
                var random = GameObject.Find(
                    "MusicSelectionView/Random/RandomButton")
                    ?.GetComponent<Button>();
                if (random == null)
                {
                    Finish(false, "MusicSelection random button is unavailable.", elapsed);
                    return;
                }
                SessionState.SetString(PhaseKey, "selection-random");
                random.onClick.Invoke();
                return;
            }

            if (phase == "selection-random")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null || runtime.IsFocusTransitionActive ||
                    runtime.FocusedMusicId == 0 ||
                    runtime.FocusedMusicId == 1)
                    return;
                var filterButton = GameObject.Find(
                    "MusicSelectionHeaderView/FilterButtonPanel/" +
                    "FilterSettingButton")?.GetComponent<Button>();
                if (filterButton == null)
                {
                    Finish(false, "MusicSelection HUD filter disappeared.", elapsed);
                    return;
                }
                SessionState.SetString(PhaseKey, "selection-hud-filter");
                SessionState.SetFloat(
                    ScrollStartedKey,
                    (float)EditorApplication.timeSinceStartup);
                filterButton.onClick.Invoke();
                return;
            }

            if (phase == "selection-hud-filter")
            {
                var body = GameObject.Find(
                    "RecoveredMusicSortFilterDialog/Body/" +
                    "MusicSortFilterDialogBody");
                var filterPanel = body != null
                    ? FindNamedTransform(body.transform, "FilterPanel")
                    : null;
                if (body == null || filterPanel == null ||
                    !filterPanel.gameObject.activeSelf)
                    return;
                if (EditorApplication.timeSinceStartup -
                    SessionState.GetFloat(ScrollStartedKey, 0f) < 0.25d)
                    return;
                var sideMenu = FindNamedTransform(
                    body.transform, "TextSideMenuPanel");
                var clearLamp = FindNamedTransform(
                    filterPanel, "ClearLampFilterPanel");
                var mv = FindNamedTransform(filterPanel, "MvFilterPanel");
                var size = FindNamedTransform(
                    filterPanel, "MusicSizeTypeFilterPanel");
                var actor = FindNamedTransform(
                    filterPanel, "ActorFilterCategoryCell");
                var vocalGroup = actor != null
                    ? FindNamedTransform(actor, "FilterCheckBoxGroup")
                    : null;
                var actorGroups = vocalGroup != null
                    ? vocalGroup.GetComponentsInChildren<Transform>(true)
                        .Where(item => item.name == "ActorGroup")
                        .ToArray()
                    : Array.Empty<Transform>();
                var actorTitle = actor != null
                    ? FindNamedTransform(
                        FindNamedTransform(actor, "Title"), "Text")
                        ?.GetComponent<Text>()
                    : null;
                var expectedActorCounts = new[] { 6, 5, 5, 5 };
                var actorCountsValid = actorGroups.Length == 5;
                for (var groupIndex = 0;
                     actorCountsValid &&
                     groupIndex < expectedActorCounts.Length;
                     groupIndex++)
                {
                    var activeSlots = 0;
                    for (var childIndex = 0;
                         childIndex < actorGroups[groupIndex].childCount;
                         childIndex++)
                    {
                        var child = actorGroups[groupIndex].GetChild(childIndex);
                        if (child.name.StartsWith(
                                "FilterAttributeButton",
                                StringComparison.Ordinal) &&
                            child.gameObject.activeSelf)
                            activeSlots++;
                    }
                    actorCountsValid =
                        actorGroups[groupIndex].gameObject.activeSelf &&
                        activeSlots == expectedActorCounts[groupIndex];
                }
                actorCountsValid = actorCountsValid &&
                    !actorGroups[4].gameObject.activeSelf;
                var sortTabRoot = sideMenu != null
                    ? FindNamedTransform(sideMenu, "RecoveredSortTab")
                    : null;
                var filterTabRoot = sideMenu != null
                    ? FindNamedTransform(sideMenu, "RecoveredFilterTab")
                    : null;
                var sortTabLabel = sortTabRoot != null
                    ? FindNamedTransform(sortTabRoot, "Label")
                        ?.GetComponent<Text>()
                    : null;
                var filterTabLabel = filterTabRoot != null
                    ? FindNamedTransform(filterTabRoot, "Label")
                        ?.GetComponent<Text>()
                    : null;
                if (sideMenu == null || !sideMenu.gameObject.activeSelf ||
                    clearLamp == null || !clearLamp.gameObject.activeSelf ||
                    mv == null || !mv.gameObject.activeSelf ||
                    size == null || !size.gameObject.activeSelf ||
                    actor == null || !actor.gameObject.activeSelf ||
                    vocalGroup == null || !actorCountsValid ||
                    actorTitle == null ||
                    actorTitle.text != "歌唱アクター" ||
                    actorTitle.fontSize != 30 ||
                    actorTitle.fontStyle != FontStyle.Normal ||
                    actorTitle.alignment != TextAnchor.MiddleCenter ||
                    sortTabRoot == null || filterTabRoot == null ||
                    Mathf.Abs(
                        ((RectTransform)sortTabRoot).rect.height - 116f) > 0.1f ||
                    Mathf.Abs(
                        ((RectTransform)filterTabRoot).rect.height - 116f) > 0.1f ||
                    sortTabLabel == null || filterTabLabel == null ||
                    sortTabLabel.fontSize != 32 ||
                    filterTabLabel.fontSize != 32 ||
                    sortTabLabel.fontStyle != FontStyle.Normal ||
                    filterTabLabel.fontStyle != FontStyle.Normal ||
                    sortTabLabel.alignment != TextAnchor.MiddleCenter ||
                    filterTabLabel.alignment != TextAnchor.MiddleCenter ||
                    Mathf.Abs(
                        sortTabLabel.rectTransform.anchoredPosition.y - 2f) >
                        0.1f ||
                    Mathf.Abs(
                        sortTabLabel.rectTransform.sizeDelta.y + 4f) > 0.1f)
                {
                    Finish(false, "HUD filter categories/side menu are incomplete.", elapsed);
                    return;
                }
                var firstClearFilter = FindNamedTransform(
                    FindNamedTransform(clearLamp, "FilterCheckBoxGroup"),
                    "FilterSideStoryButton")?.GetComponent<Button>();
                var secondClearFilter = FindNamedTransform(
                    FindNamedTransform(clearLamp, "FilterCheckBoxGroup"),
                    "FilterSideStoryButton (1)")?.GetComponent<Button>();
                var filterReset = FindNamedTransform(
                    filterPanel, "ResetButton")?.GetComponent<Button>();
                var firstVocalFilter = FindNamedTransform(
                    actorGroups[0], "FilterAttributeButton (0)")
                    ?.GetComponent<Button>();
                var companyButton = FindNamedTransform(
                    vocalGroup, "FilterGekidanButton")?.GetComponent<Button>();
                var actorAllButton = FindNamedTransform(
                    vocalGroup, "FilterAllButton")?.GetComponent<Button>();
                var actorResetButton = FindNamedTransform(
                    vocalGroup, "FilterResetButton")?.GetComponent<Button>();
                var firstCharacterIcon = firstVocalFilter != null
                    ? FindNamedTransform(
                        firstVocalFilter.transform, "CharacterIcon")
                    : null;
                var firstCharacterPortrait = firstCharacterIcon != null
                    ? FindNamedTransform(firstCharacterIcon, "Image")
                        ?.GetComponent<Image>()
                    : null;
                if (firstClearFilter == null || firstVocalFilter == null ||
                    secondClearFilter == null || filterReset == null ||
                    companyButton == null ||
                    actorAllButton == null || actorResetButton == null ||
                    !firstVocalFilter.interactable ||
                    !companyButton.interactable ||
                    !actorAllButton.interactable ||
                    actorResetButton.gameObject.activeSelf ||
                    firstCharacterPortrait?.sprite?.name != "101" ||
                    FindNamedTransform(
                        firstVocalFilter.transform, "CharacterNameText")
                        ?.GetComponent<Text>()?.text != "ここな" ||
                    FindNamedTransform(
                        actorGroups[0], "FilterAttributeButton (2)")
                        ?.GetComponentInChildren<Text>(true)?.text != "カトリナ" ||
                    FindNamedTransform(
                        filterPanel, "RecoveredVocalFilterGroup") != null)
                {
                    Finish(false, "HUD filter controls are incomplete.", elapsed);
                    return;
                }
                var uiSe = UnityEngine.Object.FindObjectOfType<
                    RecoveredUiSeRuntime>();
                if (uiSe == null)
                {
                    Finish(false, "HUD shared button SE runtime is unavailable.", elapsed);
                    return;
                }
                var clearAll = FindNamedTransform(
                    FindNamedTransform(clearLamp, "FilterCheckBoxGroup"),
                    "FilterAllButton");
                var clearAllActive = clearAll != null
                    ? FindNamedTransform(clearAll, "OnActive")
                    : null;
                var firstClearActive = FindNamedTransform(
                    firstClearFilter.transform, "OnActive");
                var secondClearActive = FindNamedTransform(
                    secondClearFilter.transform, "OnActive");
                var seCount = uiSe.CueNamePlayCount;
                firstClearFilter.onClick.Invoke();
                if (uiSe.CueNamePlayCount != seCount + 1 ||
                    uiSe.LastCueName != "BUTTON_GO")
                {
                    Finish(false, "HUD radio selection did not play BUTTON_GO.", elapsed);
                    return;
                }
                secondClearFilter.onClick.Invoke();
                if (firstClearActive == null ||
                    firstClearActive.gameObject.activeSelf ||
                    secondClearActive == null ||
                    !secondClearActive.gameObject.activeSelf ||
                    clearAllActive == null ||
                    clearAllActive.gameObject.activeSelf)
                {
                    Finish(false, "HUD filter category is not single-selection.", elapsed);
                    return;
                }
                secondClearFilter.onClick.Invoke();
                if (!secondClearActive.gameObject.activeSelf ||
                    firstClearActive.gameObject.activeSelf)
                {
                    Finish(false, "HUD selected radio could be toggled off.", elapsed);
                    return;
                }
                var vocalSelected = FindNamedTransform(
                    firstVocalFilter.transform, "OnActive");
                var companySelected = FindNamedTransform(
                    companyButton.transform, "OnActive");
                var vocalAll = FindNamedTransform(vocalGroup, "FilterAllButton");
                var vocalAllActive = vocalAll != null
                    ? FindNamedTransform(vocalAll, "OnActive")
                    : null;
                var allActorsInitiallySelected = actorGroups
                    .Take(4)
                    .SelectMany(group => group.GetComponentsInChildren<Transform>(true))
                    .Where(item =>
                        item.name.StartsWith(
                            "FilterAttributeButton",
                            StringComparison.Ordinal) &&
                        item.gameObject.activeSelf)
                    .All(item =>
                        FindNamedTransform(item, "OnActive")
                            ?.gameObject.activeSelf == true);
                if (vocalSelected == null || !vocalSelected.gameObject.activeSelf ||
                    companySelected == null ||
                    !companySelected.gameObject.activeSelf ||
                    vocalAllActive == null ||
                    !vocalAllActive.gameObject.activeSelf ||
                    !allActorsInitiallySelected)
                {
                    Finish(false, "HUD actor all state did not select all 21 actors.", elapsed);
                    return;
                }
                seCount = uiSe.CueNamePlayCount;
                companyButton.onClick.Invoke();
                if (uiSe.CueNamePlayCount != seCount + 1 ||
                    uiSe.LastCueName != "BUTTON_GO" ||
                    vocalSelected.gameObject.activeSelf ||
                    companySelected.gameObject.activeSelf)
                {
                    Finish(false, "HUD company checkbox did not clear its actors.", elapsed);
                    return;
                }
                firstVocalFilter.onClick.Invoke();
                if (!vocalSelected.gameObject.activeSelf ||
                    companySelected.gameObject.activeSelf)
                {
                    Finish(false, "HUD actor checkbox/company linkage is invalid.", elapsed);
                    return;
                }
                actorAllButton.onClick.Invoke();
                if (vocalAllActive == null ||
                    !vocalAllActive.gameObject.activeSelf ||
                    !vocalSelected.gameObject.activeSelf ||
                    !companySelected.gameObject.activeSelf)
                {
                    Finish(false, "HUD actor all checkbox did not select every actor.", elapsed);
                    return;
                }
                actorAllButton.onClick.Invoke();
                if (vocalAllActive.gameObject.activeSelf ||
                    vocalSelected.gameObject.activeSelf ||
                    companySelected.gameObject.activeSelf)
                {
                    Finish(false, "HUD actor all checkbox could not clear all actors.", elapsed);
                    return;
                }
                firstVocalFilter.onClick.Invoke();
                seCount = uiSe.CueNamePlayCount;
                filterReset.onClick.Invoke();
                if (firstClearActive.gameObject.activeSelf ||
                    secondClearActive.gameObject.activeSelf ||
                    clearAllActive == null || !clearAllActive.gameObject.activeSelf ||
                    vocalAllActive == null || !vocalAllActive.gameObject.activeSelf ||
                    !vocalSelected.gameObject.activeSelf ||
                    !companySelected.gameObject.activeSelf ||
                    uiSe.LastCueName != "BUTTON_GO" ||
                    uiSe.CueNamePlayCount != seCount + 1)
                {
                    Finish(false, "HUD filter reset did not restore all categories.", elapsed);
                    return;
                }
                var filterScreenshot = FilterScreenshotPath();
                CaptureSelection(filterScreenshot);
                var scroll = FindNamedTransform(
                    filterPanel, "Scroll View")?.GetComponent<ScrollRect>();
                if (scroll != null)
                {
                    Canvas.ForceUpdateCanvases();
                    var actorTitleRect = actorTitle != null
                        ? actorTitle.rectTransform
                        : null;
                    if (actorTitleRect != null &&
                        scroll.content != null &&
                        scroll.viewport != null)
                    {
                        var scrollableHeight = Mathf.Max(
                            0f,
                            scroll.content.rect.height -
                            scroll.viewport.rect.height);
                        var titleBounds =
                            RectTransformUtility.CalculateRelativeRectTransformBounds(
                                scroll.content,
                                actorTitleRect);
                        var actorOffset = Mathf.Clamp(
                            scroll.content.rect.yMax - titleBounds.max.y,
                            0f,
                            scrollableHeight);
                        scroll.verticalNormalizedPosition =
                            scrollableHeight > 0f
                                ? 1f - actorOffset / scrollableHeight
                                : 1f;
                    }
                    Canvas.ForceUpdateCanvases();
                    CaptureSelection(VocalFilterScreenshotPath());
                    scroll.verticalNormalizedPosition = 1f;
                }
                var sortTab = FindNamedTransform(
                    sideMenu, "RecoveredSortTab")?.GetComponent<Button>();
                var filterTab = FindNamedTransform(
                    sideMenu, "RecoveredFilterTab")?.GetComponent<Button>();
                if (sortTab == null || filterTab == null)
                {
                    Finish(false, "HUD sort/filter tab is unavailable.", elapsed);
                    return;
                }
                seCount = uiSe.CueNamePlayCount;
                sortTab.onClick.Invoke();
                if (uiSe.CueNamePlayCount != seCount + 1 ||
                    uiSe.LastCueName != "BUTTON_GO")
                {
                    Finish(false, "HUD side-menu navigation did not play BUTTON_GO.", elapsed);
                    return;
                }
                seCount = uiSe.CueNamePlayCount;
                filterTab.onClick.Invoke();
                if (uiSe.CueNamePlayCount != seCount + 1 ||
                    uiSe.LastCueName != "BUTTON_GO" ||
                    !filterPanel.gameObject.activeSelf)
                {
                    Finish(false, "HUD filter navigation did not play BUTTON_GO.", elapsed);
                    return;
                }
                sortTab.onClick.Invoke();
                var sortPanel = FindNamedTransform(body.transform, "SortPanel");
                if (sortPanel == null || !sortPanel.gameObject.activeSelf ||
                    filterPanel.gameObject.activeSelf)
                {
                    Finish(false, "HUD sort/filter tab switch failed.", elapsed);
                    return;
                }
                var buttonLayer = GameObject.Find(
                    "RecoveredMusicSortFilterDialogButtons");
                var cancel = buttonLayer != null
                    ? FindNamedTransform(buttonLayer.transform, "FirstButton")
                        ?.GetComponent<Button>()
                    : null;
                var visibleCancel = FindNamedTransform(
                    GameObject.Find("RecoveredMusicSortFilterDialog")?.transform,
                    "CancelButton") as RectTransform;
                var visibleConfirm = FindNamedTransform(
                    GameObject.Find("RecoveredMusicSortFilterDialog")?.transform,
                    "ConfirmButton") as RectTransform;
                if (cancel == null || visibleCancel == null ||
                    visibleConfirm == null ||
                    Mathf.Abs(visibleCancel.anchoredPosition.x + 174f) > 0.1f ||
                    Mathf.Abs(visibleConfirm.anchoredPosition.x - 174f) > 0.1f ||
                    Mathf.Abs(visibleCancel.anchoredPosition.y + 432f) > 0.1f ||
                    Mathf.Abs(visibleConfirm.anchoredPosition.y + 432f) > 0.1f ||
                    Mathf.Abs(visibleCancel.rect.height - 108f) > 0.1f ||
                    Mathf.Abs(visibleConfirm.rect.height - 108f) > 0.1f)
                {
                    Finish(false, "HUD filter common Dialog footer geometry is invalid.", elapsed);
                    return;
                }
                // Cancel must discard this draft actor selection.
                firstVocalFilter.onClick.Invoke();
                cancel.onClick.Invoke();
                SessionState.SetString(
                    PhaseKey, "selection-hud-filter-cancelled");
                return;
            }

            if (phase == "selection-hud-filter-cancelled")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null ||
                    GameObject.Find("RecoveredMusicSortFilterDialog") != null)
                    return;
                if (runtime.BoundCellCount != 10)
                {
                    Finish(false, "HUD filter Cancel leaked draft state.", elapsed);
                    return;
                }
                var filterButton = GameObject.Find(
                    "MusicSelectionHeaderView/FilterButtonPanel/" +
                    "FilterSettingButton")?.GetComponent<Button>();
                if (filterButton == null)
                {
                    Finish(false, "HUD filter button disappeared after Cancel.", elapsed);
                    return;
                }
                filterButton.onClick.Invoke();
                SessionState.SetString(
                    PhaseKey, "selection-hud-filter-confirm-dialog");
                return;
            }

            if (phase == "selection-hud-filter-confirm-dialog")
            {
                var dialog = GameObject.Find("RecoveredMusicSortFilterDialog");
                var body = GameObject.Find(
                    "RecoveredMusicSortFilterDialog/Body/" +
                    "MusicSortFilterDialogBody");
                var filterPanel = body != null
                    ? FindNamedTransform(body.transform, "FilterPanel")
                    : null;
                var actor = filterPanel != null
                    ? FindNamedTransform(filterPanel, "ActorFilterCategoryCell")
                    : null;
                var vocalGroup = actor != null
                    ? FindNamedTransform(actor, "FilterCheckBoxGroup")
                    : null;
                var actorGroups = vocalGroup != null
                    ? vocalGroup.GetComponentsInChildren<Transform>(true)
                        .Where(item => item.name == "ActorGroup")
                        .ToArray()
                    : Array.Empty<Transform>();
                if (dialog == null || actorGroups.Length != 5) return;
                var firstVocalFilter = FindNamedTransform(
                    actorGroups[0], "FilterAttributeButton (0)")
                    ?.GetComponent<Button>();
                var actorAll = FindNamedTransform(vocalGroup, "FilterAllButton");
                var actorAllActive = actorAll != null
                    ? FindNamedTransform(actorAll, "OnActive")
                    : null;
                var confirm = FindNamedTransform(
                    dialog.transform, "ConfirmButton")?.GetComponent<Button>();
                if (firstVocalFilter == null || confirm == null ||
                    actorAllActive == null || !actorAllActive.gameObject.activeSelf)
                {
                    Finish(false, "HUD filter Cancel did not restore committed state.", elapsed);
                    return;
                }
                var companyButtons = new[]
                {
                    "FilterGekidanButton",
                    "FilterGekidanButton (1)",
                    "FilterGekidanButton (2)",
                    "FilterGekidanButton (3)",
                }.Select(name =>
                    FindNamedTransform(vocalGroup, name)?.GetComponent<Button>())
                    .ToArray();
                if (companyButtons.Any(button => button == null))
                {
                    Finish(false, "HUD permanent actor company controls are incomplete.", elapsed);
                    return;
                }
                foreach (var companyButton in companyButtons)
                    companyButton.onClick.Invoke();
                firstVocalFilter.onClick.Invoke();
                confirm.onClick.Invoke();
                SessionState.SetString(
                    PhaseKey, "selection-hud-filter-confirmed");
                return;
            }

            if (phase == "selection-hud-filter-confirmed")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null ||
                    GameObject.Find("RecoveredMusicSortFilterDialog") != null)
                    return;
                if (runtime.BoundCellCount != 5)
                {
                    Finish(false, "HUD filter OK did not apply actor matching.", elapsed);
                    return;
                }
                var filterButton = GameObject.Find(
                    "MusicSelectionHeaderView/FilterButtonPanel/" +
                    "FilterSettingButton")?.GetComponent<Button>();
                if (filterButton == null)
                {
                    Finish(false, "HUD filter button disappeared after OK.", elapsed);
                    return;
                }
                filterButton.onClick.Invoke();
                SessionState.SetString(
                    PhaseKey, "selection-hud-filter-restore-dialog");
                return;
            }

            if (phase == "selection-hud-filter-restore-dialog")
            {
                var dialog = GameObject.Find("RecoveredMusicSortFilterDialog");
                var body = GameObject.Find(
                    "RecoveredMusicSortFilterDialog/Body/" +
                    "MusicSortFilterDialogBody");
                var filterPanel = body != null
                    ? FindNamedTransform(body.transform, "FilterPanel")
                    : null;
                var actor = filterPanel != null
                    ? FindNamedTransform(filterPanel, "ActorFilterCategoryCell")
                    : null;
                var vocalGroup = actor != null
                    ? FindNamedTransform(actor, "FilterCheckBoxGroup")
                    : null;
                var actorAll = vocalGroup != null
                    ? FindNamedTransform(vocalGroup, "FilterAllButton")
                        ?.GetComponent<Button>()
                    : null;
                var confirm = dialog != null
                    ? FindNamedTransform(dialog.transform, "ConfirmButton")
                        ?.GetComponent<Button>()
                    : null;
                if (actorAll == null || confirm == null) return;
                actorAll.onClick.Invoke();
                confirm.onClick.Invoke();
                SessionState.SetString(
                    PhaseKey, "selection-hud-filter-restored");
                return;
            }

            if (phase == "selection-hud-filter-restored")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null ||
                    GameObject.Find("RecoveredMusicSortFilterDialog") != null)
                    return;
                if (runtime.BoundCellCount != 10)
                {
                    Finish(false, "HUD actor all/OK did not restore the list.", elapsed);
                    return;
                }
                SessionState.SetString(PhaseKey, "selection-hud-sort");
                return;
            }

            if (phase == "selection-hud-sort")
            {
                if (GameObject.Find("RecoveredMusicSortFilterDialog") != null)
                    return;
                var sortButton = GameObject.Find(
                    "MusicSelectionHeaderView/SortButtonParent/SortButton")
                    ?.GetComponent<Button>();
                if (sortButton == null || !sortButton.interactable)
                {
                    Finish(false, "MusicSelection HUD sort is unavailable.", elapsed);
                    return;
                }
                sortButton.onClick.Invoke();
                SessionState.SetString(PhaseKey, "selection-hud-sort-dialog");
                SessionState.SetFloat(
                    ScrollStartedKey,
                    (float)EditorApplication.timeSinceStartup);
                return;
            }

            if (phase == "selection-hud-sort-dialog")
            {
                var body = GameObject.Find(
                    "RecoveredMusicSortFilterDialog/Body/" +
                    "MusicSortFilterDialogBody");
                var sortPanel = body != null
                    ? FindNamedTransform(body.transform, "SortPanel")
                    : null;
                if (body == null || sortPanel == null ||
                    !sortPanel.gameObject.activeSelf)
                    return;
                if (EditorApplication.timeSinceStartup -
                    SessionState.GetFloat(ScrollStartedKey, 0f) < 0.25d)
                    return;
                var nameSort = FindNamedTransform(
                    sortPanel, "NameSortToggleButton")?.GetComponent<Button>();
                var reset = FindNamedTransform(
                    sortPanel, "ResetButton")?.GetComponent<Button>();
                if (nameSort == null || reset == null)
                {
                    Finish(false, "HUD sort controls are incomplete.", elapsed);
                    return;
                }
                nameSort.onClick.Invoke();
                var selectedName = FindNamedTransform(
                    nameSort.transform, "On");
                if (selectedName == null || !selectedName.gameObject.activeSelf)
                {
                    Finish(false, "HUD name sort did not select.", elapsed);
                    return;
                }
                reset.onClick.Invoke();
                var levelSort = FindNamedTransform(
                    sortPanel, "LevelSortToggleButton");
                var selectedLevel = levelSort != null
                    ? FindNamedTransform(levelSort, "On")
                    : null;
                if (selectedLevel == null || !selectedLevel.gameObject.activeSelf ||
                    selectedName.gameObject.activeSelf)
                {
                    Finish(false, "HUD sort reset did not restore level order.", elapsed);
                    return;
                }
                nameSort.onClick.Invoke();
                CaptureSelection(SortScreenshotPath());
                var buttonLayer = GameObject.Find(
                    "RecoveredMusicSortFilterDialogButtons");
                var confirm = buttonLayer != null
                    ? FindNamedTransform(buttonLayer.transform, "SecondButton")
                        ?.GetComponent<Button>()
                    : null;
                if (confirm == null)
                {
                    Finish(false, "HUD sort original confirm button is incomplete.", elapsed);
                    return;
                }
                confirm.onClick.Invoke();
                SessionState.SetString(
                    PhaseKey, "selection-hud-sort-applied");
                return;
            }

            if (phase == "selection-hud-sort-applied")
            {
                var remainingDialog =
                    GameObject.Find("RecoveredMusicSortFilterDialog");
                if (remainingDialog != null)
                    return;
                var sortButton = GameObject.Find(
                    "MusicSelectionHeaderView/SortButtonParent/SortButton");
                var label = sortButton != null
                    ? sortButton.GetComponentInChildren<Text>(true)
                    : null;
                if (label == null)
                {
                    Finish(false, "HUD sort label is missing.", elapsed);
                    return;
                }
                if (label.text != "楽曲名") return;
                var sortButtonComponent = sortButton.GetComponent<Button>();
                if (sortButtonComponent == null)
                {
                    Finish(false, "HUD sort button disappeared after apply.", elapsed);
                    return;
                }
                sortButtonComponent.onClick.Invoke();
                SessionState.SetString(
                    PhaseKey, "selection-hud-sort-restore-dialog");
                return;
            }

            if (phase == "selection-hud-sort-restore-dialog")
            {
                var body = GameObject.Find(
                    "RecoveredMusicSortFilterDialog/Body/" +
                    "MusicSortFilterDialogBody");
                var sortPanel = body != null
                    ? FindNamedTransform(body.transform, "SortPanel")
                    : null;
                if (sortPanel == null || !sortPanel.gameObject.activeSelf)
                    return;
                var reset = FindNamedTransform(
                    sortPanel, "ResetButton")?.GetComponent<Button>();
                var buttonLayer = GameObject.Find(
                    "RecoveredMusicSortFilterDialogButtons");
                var confirm = buttonLayer != null
                    ? FindNamedTransform(buttonLayer.transform, "SecondButton")
                        ?.GetComponent<Button>()
                    : null;
                if (reset == null || confirm == null)
                {
                    Finish(false, "HUD sort restore controls are incomplete.", elapsed);
                    return;
                }
                reset.onClick.Invoke();
                confirm.onClick.Invoke();
                SessionState.SetString(
                    PhaseKey, "selection-hud-sort-restored");
                return;
            }

            if (phase == "selection-hud-sort-restored")
            {
                if (GameObject.Find("RecoveredMusicSortFilterDialog") != null)
                    return;
                var sortButton = GameObject.Find(
                    "MusicSelectionHeaderView/SortButtonParent/SortButton");
                var label = sortButton != null
                    ? sortButton.GetComponentInChildren<Text>(true)
                    : null;
                if (label == null)
                {
                    Finish(false, "HUD restored sort label is missing.", elapsed);
                    return;
                }
                if (label.text != "楽曲Lv") return;
                SceneManager.LoadScene("LocalMusicSelection");
                SessionState.SetString(
                    PhaseKey, "selection-hud-reloaded");
                return;
            }

            if (phase == "selection-hud-reloaded")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null || runtime.Selection == null ||
                    runtime.FocusedMusicId != 1 ||
                    runtime.IsFocusTransitionActive)
                    return;
                SessionState.SetInt(
                    BookmarkFlagsKey, (int)runtime.GetBookmarkFlags(1));
                var bookmark = GameObject.Find("LocalMusic_1")
                    ?.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(button => button.name == "BookmarkButton");
                if (bookmark == null)
                {
                    Finish(false, "Music bookmark button is unavailable.", elapsed);
                    return;
                }
                SessionState.SetString(PhaseKey, "selection-bookmark-open");
                bookmark.onClick.Invoke();
                return;
            }

            if (phase == "selection-bookmark-open")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                var dialog = GameObject.Find(
                    "RecoveredMusicBookmarkSettingDialog");
                if (runtime == null || !runtime.IsBookmarkDialogVisible ||
                    dialog == null)
                    return;
                CaptureSelection(BookmarkDialogScreenshotPath());
                var toggle = FindNamedTransform(
                    dialog.transform, "FavoriteToggle")?.GetComponent<Button>();
                var confirm = FindNamedTransform(
                    dialog.transform, "ConfirmButton")?.GetComponent<Button>();
                if (toggle == null || confirm == null)
                {
                    Finish(false, "Music bookmark dialog controls are incomplete.", elapsed);
                    return;
                }
                var initiallyOn =
                    (SessionState.GetInt(BookmarkFlagsKey, 0) & 1) != 0;
                toggle.onClick.Invoke();
                if (initiallyOn) toggle.onClick.Invoke();
                SessionState.SetString(PhaseKey, "selection-bookmark-saved");
                confirm.onClick.Invoke();
                return;
            }

            if (phase == "selection-bookmark-saved")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null || runtime.IsBookmarkDialogVisible)
                    return;
                if ((runtime.GetBookmarkFlags(1) &
                     RecoveredMusicBookmarkFlags.Bookmark1) == 0)
                {
                    Finish(false, "Music bookmark did not persist.", elapsed);
                    return;
                }
                var bookmarkFilter = GameObject.Find(
                    "MusicSelectionView/Bookmark/BookmarkChangeButton")
                    ?.GetComponent<Button>();
                if (bookmarkFilter == null)
                {
                    Finish(false, "Music bookmark filter is unavailable.", elapsed);
                    return;
                }
                SessionState.SetString(PhaseKey, "selection-bookmark-filter1");
                bookmarkFilter.onClick.Invoke();
                return;
            }

            if (phase == "selection-bookmark-filter1")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null ||
                    runtime.IsMusicListRebuilding ||
                    runtime.BookmarkFilter !=
                    RecoveredMusicBookmarkFlags.Bookmark1 ||
                    runtime.BoundCellCount == 0 ||
                    GameObject.Find("LocalMusic_1") == null)
                    return;
                GameObject.Find(
                    "MusicSelectionView/Bookmark/BookmarkChangeButton")
                    ?.GetComponent<Button>()?.onClick.Invoke();
                SessionState.SetString(PhaseKey, "selection-bookmark-filter2");
                return;
            }

            if (phase == "selection-bookmark-filter2")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null ||
                    runtime.IsMusicListRebuilding ||
                    runtime.BookmarkFilter !=
                    RecoveredMusicBookmarkFlags.Bookmark2)
                    return;
                var view = GameObject.Find("MusicSelectionView");
                var noMusic = view != null
                    ? FindNamedTransform(view.transform, "NoMusicPanel")
                    : null;
                var noImage = view != null
                    ? FindNamedTransform(view.transform, "NoImageMask")
                    : null;
                var ok = GameObject.Find("MusicSelectionView/OkButton")
                    ?.GetComponent<Button>();
                if (runtime.BoundCellCount != 0 ||
                    noMusic == null || !noMusic.gameObject.activeSelf ||
                    noImage == null || !noImage.gameObject.activeSelf ||
                    ok == null || ok.interactable)
                {
                    Finish(false, "Empty bookmark filter state is incorrect.", elapsed);
                    return;
                }
                CaptureSelection(EmptyBookmarkScreenshotPath());
                GameObject.Find(
                    "MusicSelectionView/Bookmark/BookmarkChangeButton")
                    ?.GetComponent<Button>()?.onClick.Invoke();
                SessionState.SetString(PhaseKey, "selection-bookmark-filter3");
                return;
            }

            if (phase == "selection-bookmark-filter3")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null ||
                    runtime.IsMusicListRebuilding ||
                    runtime.BookmarkFilter !=
                    RecoveredMusicBookmarkFlags.Bookmark3)
                    return;
                GameObject.Find(
                    "MusicSelectionView/Bookmark/BookmarkChangeButton")
                    ?.GetComponent<Button>()?.onClick.Invoke();
                SessionState.SetString(PhaseKey, "selection-bookmark-restored");
                return;
            }

            if (phase == "selection-bookmark-restored")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null ||
                    runtime.IsMusicListRebuilding ||
                    runtime.BookmarkFilter != RecoveredMusicBookmarkFlags.None ||
                    runtime.BoundCellCount != 10)
                    return;
                var initiallyOn =
                    (SessionState.GetInt(BookmarkFlagsKey, 0) & 1) != 0;
                if (!initiallyOn)
                {
                    var bookmark = GameObject.Find("LocalMusic_1")
                        ?.GetComponentsInChildren<Button>(true)
                        .FirstOrDefault(
                            button => button.name == "BookmarkButton");
                    if (bookmark == null) return;
                    bookmark.onClick.Invoke();
                    SessionState.SetString(
                        PhaseKey, "selection-bookmark-cleanup-open");
                    return;
                }
                OpenRatePanelOrFail(elapsed);
                return;
            }

            if (phase == "selection-bookmark-cleanup-open")
            {
                var dialog = GameObject.Find(
                    "RecoveredMusicBookmarkSettingDialog");
                if (dialog == null) return;
                var toggle = FindNamedTransform(
                    dialog.transform, "FavoriteToggle")?.GetComponent<Button>();
                var confirm = FindNamedTransform(
                    dialog.transform, "ConfirmButton")?.GetComponent<Button>();
                if (toggle == null || confirm == null) return;
                toggle.onClick.Invoke();
                confirm.onClick.Invoke();
                SessionState.SetString(
                    PhaseKey, "selection-bookmark-cleanup-done");
                return;
            }

            if (phase == "selection-bookmark-cleanup-done")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null || runtime.IsBookmarkDialogVisible)
                    return;
                if ((runtime.GetBookmarkFlags(1) &
                     RecoveredMusicBookmarkFlags.Bookmark1) != 0)
                {
                    Finish(false, "Music bookmark cleanup failed.", elapsed);
                    return;
                }
                OpenRatePanelOrFail(elapsed);
                return;
            }

            if (phase == "selection-rate-open")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                var dialog = GameObject.Find("RecoveredPlayerRateDialog");
                var body = dialog != null
                    ? FindNamedTransform(dialog.transform, "PlayerRateDialogBody")
                    : null;
                var rateNumber = body != null
                    ? FindNamedTransform(body, "PlayerRateNumberText")
                        ?.GetComponent<Text>()
                    : null;
                var playerTab = body != null
                    ? FindNamedTransform(body, "RecoveredPlayerRateTab")
                        ?.GetComponent<Button>()
                    : null;
                var spTab = body != null
                    ? FindNamedTransform(body, "RecoveredSpRateTab")
                        ?.GetComponent<Button>()
                    : null;
                if (runtime == null || !runtime.IsPlayerRateDialogVisible ||
                    body == null || rateNumber == null ||
                    playerTab == null || spTab == null)
                    return;
                CaptureSelection(PlayerRateScreenshotPath());
                if (!NamedTextIs(
                        "RecoveredPlayerRateDialog",
                        "TitleText",
                        "レート対象楽曲"))
                {
                    Finish(false, "Player rate dialog title is incorrect.", elapsed);
                    return;
                }
                if (!double.TryParse(
                        rateNumber.text,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out _))
                {
                    Finish(false, "Player rate text is invalid.", elapsed);
                    return;
                }
                spTab.onClick.Invoke();
                var spPanel = FindNamedTransform(body, "SPRatePanel");
                if (spPanel == null || !spPanel.gameObject.activeSelf)
                {
                    Finish(false, "SP rate tab did not switch panels.", elapsed);
                    return;
                }
                var spRateIcon = FindNamedTransform(spPanel, "RateIcon")
                    ?.GetComponent<Image>();
                if (spRateIcon == null || !spRateIcon.enabled ||
                    spRateIcon.sprite == null ||
                    spRateIcon.color.a <= 0.99f)
                {
                    Finish(false, "SP rate note icon is not visible.", elapsed);
                    return;
                }
                CaptureSelection(SpRateScreenshotPath());
                playerTab.onClick.Invoke();
                var displayType = FindNamedTransform(
                    body, "DsiplayTypeSwitchButton")?.GetComponent<Button>();
                if (displayType == null || !displayType.gameObject.activeSelf)
                {
                    Finish(false, "Player rate display switch is unavailable.", elapsed);
                    return;
                }
                displayType.onClick.Invoke();
                var simpleScroller = FindNamedTransform(
                    body, "SimpleRateScroller");
                if (simpleScroller == null ||
                    !simpleScroller.gameObject.activeSelf)
                {
                    Finish(false, "Player rate simple list did not activate.", elapsed);
                    return;
                }
                var close = FindNamedTransform(
                    dialog.transform, "CloseButton")?.GetComponent<Button>();
                if (close == null)
                {
                    Finish(false, "Player rate close button is unavailable.", elapsed);
                    return;
                }
                close.onClick.Invoke();
                SessionState.SetString(
                    PhaseKey, "selection-rate-closed");
                return;
            }

            if (phase == "selection-rate-closed")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null || runtime.IsPlayerRateDialogVisible)
                    return;
                SceneManager.LoadScene("LocalMusicSelection");
                SessionState.SetString(
                    PhaseKey, "selection-auxiliary-reloaded");
                return;
            }

            if (phase == "selection-auxiliary-reloaded")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null || runtime.Selection == null ||
                    runtime.FocusedMusicId != 1 ||
                    runtime.IsFocusTransitionActive)
                    return;
                SessionState.SetString(
                    PhaseKey, "selection-scroll-text-restart");
                return;
            }

            if (phase == "selection-scroll-text-restart")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                var focusedVocalsScroll = runtime != null
                    ? runtime.FocusedVocalsScroll
                    : null;
                if (focusedVocalsScroll == null)
                {
                    Finish(false, "Focused scroll text disappeared after HUD.", elapsed);
                    return;
                }
                focusedVocalsScroll.RestartForNewContent();
                SessionState.SetString(
                    PhaseKey, "selection-scroll-text-reset");
                return;
            }

            if (phase == "selection-scroll-text-reset")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                var vocalsScroll = runtime != null
                    ? runtime.FocusedVocalsScroll
                    : null;
                if (vocalsScroll == null || vocalsScroll.BodyText == null)
                    return;
                if (Mathf.Abs(
                        vocalsScroll.BodyText.rectTransform.anchoredPosition.x) >
                    1f)
                    return;
                SessionState.SetFloat(
                    ScrollStartedKey,
                    (float)EditorApplication.timeSinceStartup);
                SessionState.SetString(PhaseKey, "selection-scroll-text");
                return;
            }

            if (phase == "selection-scroll-text")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                var vocalsScroll = runtime != null
                    ? runtime.FocusedVocalsScroll
                    : null;
                if (runtime == null || vocalsScroll == null ||
                    vocalsScroll.BodyText == null)
                    return;
                var sinceRestart = EditorApplication.timeSinceStartup -
                    SessionState.GetFloat(
                        ScrollStartedKey,
                        (float)EditorApplication.timeSinceStartup);
                var textX =
                    vocalsScroll.BodyText.rectTransform.anchoredPosition.x;
                if (sinceRestart < 0.8d)
                {
                    if (Mathf.Abs(textX) > 1f)
                    {
                        Finish(
                            false,
                            "Selected long text ignored the retail one-second delay.",
                            elapsed);
                    }
                    return;
                }
                if (textX >= -1f)
                {
                    if (sinceRestart > 1.35d)
                    {
                        Finish(
                            false,
                            "Selected long text did not begin its 120 px/s scroll.",
                            elapsed);
                    }
                    return;
                }
                var scroll = runtime.ListScrollRect;
                if (scroll == null) return;
                var screenshot = ScreenshotPath();
                if (!SessionState.GetBool(CaptureKey, false))
                {
                    var visiblePixels = CaptureSelection(screenshot);
                    SessionState.SetBool(CaptureKey, true);
                    if (!File.Exists(screenshot) ||
                        new FileInfo(screenshot).Length < 10000 ||
                        visiblePixels < 1000)
                    {
                        Finish(false, "MusicSelection render capture failed.", elapsed);
                        return;
                    }
                }
                runtime.OnListBeginDrag();
                scroll.velocity = Vector2.up * 600f;
                scroll.content.anchoredPosition +=
                    Vector2.up * (188f * 21f);
                SessionState.SetString(PhaseKey, "selection-large-loop-rebase");
                return;
            }

            if (phase == "selection-large-loop-rebase")
            {
                SessionState.SetString(
                    PhaseKey,
                    "selection-large-loop-verify");
                return;
            }

            if (phase == "selection-large-loop-verify")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                var scroll = runtime != null ? runtime.ListScrollRect : null;
                if (runtime == null || scroll == null || scroll.velocity.y <= 0f)
                {
                    Finish(
                        false,
                        "Large loop rebase reversed the ScrollRect velocity.",
                        elapsed);
                    return;
                }
                scroll.StopMovement();
                runtime.OnListEndDrag();
                SessionState.SetFloat(
                    ScrollStartedKey,
                    (float)EditorApplication.timeSinceStartup);
                SessionState.SetString(PhaseKey, "selection-auto-snap");
                return;
            }

            if (phase == "selection-auto-snap")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null || runtime.Selection == null ||
                    runtime.Selection.Music.Id != 2 ||
                    runtime.FocusedMusicId != 2 ||
                    runtime.IsFocusTransitionActive)
                {
                    if (EditorApplication.timeSinceStartup -
                        SessionState.GetFloat(ScrollStartedKey, 0f) > 5d)
                    {
                        Finish(
                            false,
                            "Scroll snapping stalled: selection=" +
                            runtime?.Selection?.Music?.Id +
                            " focus=" + runtime?.FocusedMusicId +
                            " transition=" +
                            runtime?.IsFocusTransitionActive +
                            " contentY=" +
                            runtime?.ListScrollRect?.content?.anchoredPosition.y,
                            elapsed);
                    }
                    return;
                }
                var first = GameObject.Find("LocalMusic_1")?.GetComponent<RectTransform>();
                var second = GameObject.Find("LocalMusic_2")?.GetComponent<RectTransform>();
                if (first == null || second == null ||
                    Mathf.Abs(first.sizeDelta.y - 168f) > 0.1f ||
                    Mathf.Abs(second.sizeDelta.y - 196f) > 0.1f)
                {
                    Finish(false, "Scroll snapping did not update selected cell focus.", elapsed);
                    return;
                }
                var olivierButton = GameObject.Find(
                    "MusicSelectionView/DefaultLivePanels/DifficultyPanel/Olivier")
                    ?.GetComponent<Button>();
                if (olivierButton == null || !olivierButton.interactable)
                {
                    Finish(false, "Olivier difficulty button is unavailable.", elapsed);
                    return;
                }
                SessionState.SetString(PhaseKey, "selection-olivier");
                olivierButton.onClick.Invoke();
                return;
            }

            if (phase == "selection-olivier")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null || runtime.Selection == null ||
                    runtime.Selection.Live.Difficulty !=
                    RecoveredMusicDifficulty.Olivier)
                    return;
                if (runtime.ListScrollRect == null ||
                    runtime.FocusedMusicId != 2)
                {
                    Finish(false, "Recovered music list did not preserve snapped focus.", elapsed);
                    return;
                }
                if (!DifficultyButtonUses(
                        RecoveredMusicDifficulty.Olivier,
                        "img_live_common_level_olivier") ||
                    !DifficultyButtonUses(
                        RecoveredMusicDifficulty.Stella,
                        "img_live_common_level_off"))
                {
                    Finish(false, "Difficulty button selected/off graphics are invalid.", elapsed);
                    return;
                }
                if (!MarkerIs(1, "V", "img_live_common_list_level_olivier") ||
                    !MarkerIs(2, "V", "img_live_common_list_level_olivier") ||
                    !MarkerIs(3, "IV", "img_live_common_list_level_olivier") ||
                    !MarkerIs(10, "II", "img_live_common_list_level_olivier"))
                {
                    Finish(
                        false,
                        "Olivier Roman levels or rounded marker sprites are invalid.",
                        elapsed);
                    return;
                }
                for (var musicId = 1; musicId <= 10; musicId++)
                {
                    if (!MarkerUses(
                            musicId, "img_live_common_list_level_olivier"))
                    {
                        Finish(
                            false,
                            "Olivier difficulty did not refresh every list cell.",
                            elapsed);
                        return;
                    }
                }
                if (RecoveredLocalMusicSelectionRuntime.FormatMusicLevel(100) != "100" ||
                    RecoveredLocalMusicSelectionRuntime.FormatMusicLevel(101) != "I" ||
                    RecoveredLocalMusicSelectionRuntime.FormatMusicLevel(110) != "X" ||
                    RecoveredLocalMusicSelectionRuntime.FormatMusicLevel(111) != "111")
                {
                    Finish(false, "Olivier level conversion boundary is invalid.", elapsed);
                    return;
                }
                var stellaButton = GameObject.Find(
                    "MusicSelectionView/DefaultLivePanels/DifficultyPanel/Stella")
                    ?.GetComponent<Button>();
                if (stellaButton == null || !stellaButton.interactable)
                {
                    Finish(false, "Stella difficulty button is unavailable.", elapsed);
                    return;
                }
                SessionState.SetString(PhaseKey, "selection-stella");
                stellaButton.onClick.Invoke();
                return;
            }

            if (phase == "selection-stella")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null || runtime.Selection == null ||
                    runtime.Selection.Live.Difficulty !=
                    RecoveredMusicDifficulty.Stella)
                    return;
                if (!MarkerIs(1, "25", "img_live_common_list_level_stella") ||
                    !MarkerIs(2, "26", "img_live_common_list_level_stella"))
                {
                    Finish(
                        false,
                        "Stella difficulty did not refresh the list markers.",
                        elapsed);
                    return;
                }
                var tenthCell = GameObject.Find("LocalMusic_10");
                var switchButton =
                    tenthCell?.GetComponentInChildren<Button>(true);
                if (switchButton == null)
                {
                    Finish(false, "Music 10 selection button is unavailable.", elapsed);
                    return;
                }
                SessionState.SetString(PhaseKey, "selection-music10");
                switchButton.onClick.Invoke();
                return;
            }

            if (phase == "selection-music10")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                var preview = UnityEngine.Object.FindObjectOfType<
                    RecoveredMusicSelectionPreviewRuntime>();
                var title = GameObject.Find(
                    "MusicSelectionView/TicketMacine/MusicInformationPanel/" +
                    "ScrollMusicTitle/ScrollMusicTitleText")?.GetComponent<Text>();
                var jacket = GameObject.Find(
                    "MusicSelectionView/TicketMacine/MusicInformationPanel/" +
                    "MusicJackets/MusicJacketFront")?.GetComponent<Image>();
                if (runtime == null || runtime.Selection == null ||
                    runtime.Selection.Music.Id != 10 ||
                    runtime.Selection.Live.Difficulty != RecoveredMusicDifficulty.Stella ||
                    runtime.IsFocusTransitionActive ||
                    preview == null || preview.MusicId != 10 ||
                    title == null || title.text != "名もなき恋人よ" ||
                    jacket == null || jacket.overrideSprite == null)
                    return;
                runtime.OnListBeginDrag();
                runtime.ListScrollRect.content.anchoredPosition += Vector2.up * 188f;
                runtime.OnListEndDrag();
                SessionState.SetString(PhaseKey, "selection-loop-wrap");
                return;
            }

            if (phase == "selection-loop-wrap")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null || runtime.Selection == null ||
                    runtime.Selection.Music.Id != 1 ||
                    runtime.FocusedMusicId != 1 ||
                    runtime.IsFocusTransitionActive)
                    return;
                SessionState.SetFloat(
                    ScrollStartedKey,
                    (float)EditorApplication.timeSinceStartup);
                SessionState.SetString(
                    PhaseKey,
                    "selection-loop-wrap-scroll");
                return;
            }

            if (phase == "selection-loop-wrap-scroll")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                var vocalsScroll = runtime != null
                    ? runtime.FocusedVocalsScroll
                    : null;
                if (runtime == null || runtime.Selection == null ||
                    runtime.Selection.Music.Id != 1 ||
                    vocalsScroll == null || vocalsScroll.BodyText == null)
                    return;
                var sinceFocus = EditorApplication.timeSinceStartup -
                    SessionState.GetFloat(
                        ScrollStartedKey,
                        (float)EditorApplication.timeSinceStartup);
                var textX =
                    vocalsScroll.BodyText.rectTransform.anchoredPosition.x;
                if (sinceFocus < 0.8d)
                {
                    if (Mathf.Abs(textX) > 1f)
                    {
                        Finish(
                            false,
                            "Reselected long text ignored the retail delay.",
                            elapsed);
                    }
                    return;
                }
                if (textX >= -1f)
                {
                    if (sinceFocus > 1.35d)
                    {
                        Finish(
                            false,
                            "Reselected long text remained stuck after focus.",
                            elapsed);
                    }
                    return;
                }
                var tenthCell = GameObject.Find("LocalMusic_10");
                var switchButton = tenthCell?.GetComponentInChildren<Button>(true);
                if (switchButton == null)
                {
                    Finish(false, "Loop wrap recovery cell is unavailable.", elapsed);
                    return;
                }
                SessionState.SetString(PhaseKey, "selection-music10-final");
                switchButton.onClick.Invoke();
                return;
            }

            if (phase == "selection-music10-final")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
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
                var persisted = new RecoveredSettingsStore().LoadOrDefault();
                persisted.GameSettings.NoteSpeed = 9d;
                persisted.GameSettings.NoteOffsetValue = 0d;
                persisted.GameDetailSettings.IsActiveMirror = false;
                persisted.GameDetailSettings.IsActiveSplitRandom = false;
                persisted.SystemSettings.IsPreLiveOptionConfirmation = true;
                new RecoveredSettingsStore().Save(persisted);
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
                    RecoveredLocalMusicSelectionRuntime>();
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
                var dialog = GameObject.Find("RecoveredNoteSpeedDialog");
                var dialogRect = dialog != null
                    ? dialog.GetComponent<RectTransform>()
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
                    "RecoveredNoteSpeedDialog/Body/" +
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
                    RecoveredUiSeRuntime>();
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
                var mirrorOnInner = FindNamedTransform(
                    FindNamedTransform(mirror, "On"), "InnerImage")
                    ?.GetComponent<Image>();
                var mirrorOffInner = FindNamedTransform(
                    FindNamedTransform(mirror, "Off"), "InnerImage")
                    ?.GetComponent<Image>();
                if (mirrorOnInner == null || mirrorOffInner == null ||
                    !mirrorOnInner.gameObject.activeSelf ||
                    !mirrorOffInner.gameObject.activeSelf ||
                    mirrorOnInner.sprite == null ||
                    mirrorOffInner.sprite == null ||
                    mirrorOnInner.sprite.name !=
                        "btn_common_btn_switch_filter_off" ||
                    mirrorOffInner.sprite.name !=
                        "btn_common_btn_switch_filter_on")
                {
                    Finish(
                        false,
                        "Performance radio buttons do not use HUD On/Off sprites.",
                        elapsed);
                    return;
                }
                var visibleCancel = dialog != null
                    ? FindNamedTransform(dialog.transform, "CancelButton")
                    : null;
                var visibleConfirm = dialog != null
                    ? FindNamedTransform(dialog.transform, "ConfirmButton")
                    : null;
                var visibleCancelRect = visibleCancel as RectTransform;
                var visibleConfirmRect = visibleConfirm as RectTransform;
                var visibleCancelText = visibleCancel != null
                    ? visibleCancel.GetComponentInChildren<Text>(true)
                    : null;
                if (visibleCancelRect == null || visibleConfirmRect == null ||
                    Mathf.Abs(visibleCancelRect.rect.height - 108f) > 0.1f ||
                    Mathf.Abs(visibleConfirmRect.rect.height - 108f) > 0.1f ||
                    Mathf.Abs(
                        visibleCancelRect.anchoredPosition.x + 174f) > 0.1f ||
                    Mathf.Abs(
                        visibleConfirmRect.anchoredPosition.x - 174f) > 0.1f ||
                    Mathf.Abs(
                        visibleCancelRect.anchoredPosition.y + 432f) > 0.1f ||
                    Mathf.Abs(
                        visibleConfirmRect.anchoredPosition.y + 432f) > 0.1f ||
                    visibleCancelText == null ||
                    visibleCancelText.fontSize != 40 ||
                    visibleCancelText.fontStyle != FontStyle.Normal ||
                    visibleCancelText.alignment != TextAnchor.MiddleCenter ||
                    Mathf.Abs(
                        visibleCancelText.rectTransform.anchoredPosition.y - 1f) >
                        0.1f ||
                    Mathf.Abs(
                        visibleCancelText.rectTransform.sizeDelta.y + 2f) > 0.1f)
                {
                    Finish(false, "Common dialog footer geometry/font is invalid.", elapsed);
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
                    "RecoveredNoteSpeedDialogButtons");
                var confirm = buttonLayer != null
                    ? FindNamedTransform(buttonLayer.transform, "SecondButton")
                        ?.GetComponent<Button>()
                    : null;
                if (confirm == null)
                {
                    Finish(false, "Note-speed confirm button is unavailable.", elapsed);
                    return;
                }
                var confirmRect = confirm.GetComponent<RectTransform>();
                var confirmImage = confirm.GetComponent<Image>();
                Debug.Log(
                    "OPENWDS_NOTE_SPEED_DIALOG " +
                    $"buttonPos={confirmRect?.position} " +
                    $"buttonRect={confirmRect?.rect} " +
                    $"active={confirm.gameObject.activeInHierarchy} " +
                    $"image={confirmImage != null}/{confirmImage?.enabled}/" +
                    $"{confirmImage?.color} cull={confirmImage?.canvasRenderer.cull}");
                SessionState.SetString(PhaseKey, "game");
                confirm.onClick.Invoke();
                return;
            }

            if (phase == "game")
            {
                if (SceneManager.GetActiveScene().name != "OfflineRhythmPreview")
                    return;
                var game = UnityEngine.Object.FindObjectOfType<RecoveredGameRuntime>();
                if (game == null || !game.IsInitialized) return;
                if (RecoveredCurtainTransitionRuntime.IsTransitioning ||
                    !RecoveredCurtainTransitionRuntime.ClosePlayed ||
                    !RecoveredCurtainTransitionRuntime.OpenPlayed ||
                    !RecoveredCurtainTransitionRuntime.CloseCompleted ||
                    !RecoveredCurtainTransitionRuntime.OpenCompleted ||
                    !RecoveredCurtainTransitionRuntime
                        .DestinationInitializedBeforeOpen ||
                    !RecoveredCurtainTransitionRuntime
                        .OpenRenderedInDestination)
                    return;
                if (UnityEngine.Object.FindObjectsOfType<EventSystem>().Length != 1)
                {
                    Finish(false, "Gameplay must contain exactly one EventSystem.", elapsed);
                    return;
                }
                if (!RecoveredLocalMusicSelectionSession.HasSelection ||
                    RecoveredLocalMusicSelectionSession.Selection.Music.Id != 10 ||
                    RecoveredLocalMusicSelectionSession.Selection.Live.Difficulty !=
                    RecoveredMusicDifficulty.Stella ||
                    RecoveredLocalMusicSelectionSession.ChartAsset == null ||
                    game.ChartAsset !=
                    RecoveredLocalMusicSelectionSession.ChartAsset ||
                    game.MusicDifficulty != RecoveredMusicDifficulty.Stella)
                {
                    Finish(false, "Selected game parameters were not consumed.", elapsed);
                    return;
                }
                var persisted = new RecoveredSettingsStore().LoadOrDefault();
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
                if (introduction == null || !introduction.IsPlaying)
                {
                    Finish(
                        false,
                        "GameIntroduction was unavailable for focus-loss regression.",
                        elapsed);
                    return;
                }
                introduction.SendMessage(
                    "OnApplicationFocus", false,
                    SendMessageOptions.RequireReceiver);
                game.SetPaused(true);
                if (!introduction.IsAnimationPaused)
                {
                    Finish(false, "Focus loss did not freeze GameIntroduction.", elapsed);
                    return;
                }
                SessionState.SetFloat(
                    IntroductionFocusTimeKey, introduction.NormalizedTime);
                SessionState.SetString(PhaseKey, "game-focus-paused");
                return;
            }

            if (phase == "game-focus-paused")
            {
                var game = UnityEngine.Object.FindObjectOfType<RecoveredGameRuntime>();
                var introduction = UnityEngine.Object.FindObjectOfType<
                    Sirius.Game.GameIntroductionAnimationController>();
                if (game == null || introduction == null) return;
                var pausedTime = SessionState.GetFloat(
                    IntroductionFocusTimeKey, -1f);
                if (!introduction.IsPlaying ||
                    !introduction.IsAnimationPaused ||
                    Math.Abs(introduction.NormalizedTime - pausedTime) > 0.001f)
                {
                    Finish(
                        false,
                        "GameIntroduction advanced while focus/game pause was active.",
                        elapsed);
                    return;
                }
                introduction.SendMessage(
                    "OnApplicationFocus", true,
                    SendMessageOptions.RequireReceiver);
                if (introduction.IsAnimationPaused)
                {
                    Finish(
                        false,
                        "Focus regain did not continue GameIntroduction while the game pause remained active.",
                        elapsed);
                    return;
                }
                SessionState.SetString(PhaseKey, "game-focus-resumed");
                return;
            }

            if (phase == "game-focus-resumed")
            {
                var game = UnityEngine.Object.FindObjectOfType<RecoveredGameRuntime>();
                if (game == null) return;
                var introduction = UnityEngine.Object.FindObjectOfType<
                    Sirius.Game.GameIntroductionAnimationController>();
                var pausedTime = SessionState.GetFloat(
                    IntroductionFocusTimeKey, -1f);
                if (introduction != null && introduction.IsPlaying &&
                    introduction.NormalizedTime <= pausedTime + 0.001f)
                    return;
                game.SetPaused(false);
                SessionState.SetBool(IntroductionFocusValidKey, true);
                SessionState.SetString(PhaseKey, "return-retire");
                game.RetireGame();
                return;
            }

            if (phase == "return-retire")
            {
                if (SceneManager.GetActiveScene().name != "LocalMusicSelection")
                    return;
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null || runtime.Selection == null) return;
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
                SessionState.SetString(PhaseKey, "retire-speed");
                ok.onClick.Invoke();
                return;
            }

            if (phase == "retire-speed")
            {
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null || !runtime.IsNoteSpeedDialogVisible)
                    return;
                var buttonLayer = GameObject.Find(
                    "RecoveredNoteSpeedDialogButtons");
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
                var game = UnityEngine.Object.FindObjectOfType<RecoveredGameRuntime>();
                if (game == null || !game.IsInitialized) return;
                var resultField = typeof(RecoveredGameRuntime).GetField(
                    "_resultShown",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (resultField == null)
                {
                    Finish(false, "Game result state field is unavailable.", elapsed);
                    return;
                }
                resultField.SetValue(game, true);
                SessionState.SetString(PhaseKey, "result-button");
                return;
            }

            if (phase == "result-button")
            {
                var button = GameObject.Find(
                    "RecoveredResultReturnCanvas/ReturnToMusicSelection")
                    ?.GetComponent<Button>();
                if (button == null) return;
                SessionState.SetString(PhaseKey, "return-result");
                button.onClick.Invoke();
                return;
            }

            if (phase == "return-result")
            {
                if (SceneManager.GetActiveScene().name != "LocalMusicSelection")
                    return;
                var runtime = UnityEngine.Object.FindObjectOfType<
                    RecoveredLocalMusicSelectionRuntime>();
                if (runtime == null || runtime.Selection == null) return;
                if (UnityEngine.Object.FindObjectsOfType<EventSystem>().Length != 1)
                {
                    Finish(
                        false,
                        "Result return created a duplicate EventSystem.",
                        elapsed);
                    return;
                }
                Finish(true, null, elapsed);
            }
        }

        private static void Finish(bool passed, string failure, double elapsed)
        {
            var report = new Report
            {
                passed = passed,
                failure = failure,
                elapsedSeconds = elapsed,
                availableDifficulties = 5,
                selectedDifficulty = RecoveredMusicDifficulty.Stella.ToString(),
                titleBound = passed,
                listCellBound = passed,
                scrollingValid = passed,
                arrowNavigationValid = passed,
                randomSelectionValid = passed,
                bookmarkFlowValid = passed,
                playerRateDialogValid = passed,
                introductionFocusResumeValid =
                    SessionState.GetBool(IntroductionFocusValidKey, false),
                allDifficultyMarkersRefreshed = passed,
                olivierRomanValid = passed,
                roundedMarkerSpriteValid = passed,
                gameLoaded = passed,
                retireReturned = passed,
                resultReturned = passed,
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

        private static bool NamedTextIs(
            string rootName, string textName, string expected)
        {
            foreach (var transform in
                     UnityEngine.Object.FindObjectsOfType<Transform>(true))
            {
                if (transform.name != rootName) continue;
                foreach (var text in transform.GetComponentsInChildren<Text>(true))
                {
                    if (text.name == textName && text.text == expected)
                        return true;
                }
            }
            return false;
        }

        private static void OpenRatePanelOrFail(double elapsed)
        {
            var header = GameObject.Find("MusicSelectionHeaderView");
            var ratePanel = header != null
                ? FindNamedTransform(header.transform, "RateDataPanel")
                : null;
            var information = ratePanel != null
                ? FindNamedTransform(ratePanel, "InformationButton")
                    ?.GetComponent<Button>()
                : null;
            if (information == null)
            {
                Finish(false, "Player rate information button is unavailable.", elapsed);
                return;
            }
            SessionState.SetString(PhaseKey, "selection-rate-open");
            information.onClick.Invoke();
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

        private static bool MarkerIs(
            int musicId,
            string expectedText,
            string expectedSprite)
        {
            var cell = GameObject.Find("LocalMusic_" + musicId);
            if (cell == null) return false;
            var text = cell.transform.Find(
                "PartsBase/PartsMask/DifficultyLevel/DifficultyLevelText")
                ?.GetComponent<Text>();
            return text != null && text.text == expectedText &&
                MarkerUses(musicId, expectedSprite);
        }

        private static bool MarkerUses(int musicId, string expectedSprite)
        {
            var cell = GameObject.Find("LocalMusic_" + musicId);
            if (cell == null) return false;
            var image = cell.transform.Find(
                "PartsBase/PartsMask/DifficultyLevel/" +
                "DifficultyLevelBackgroundImage")?.GetComponent<Image>();
            var sprite = image != null
                ? image.overrideSprite ?? image.sprite
                : null;
            return image != null && image.enabled && sprite != null &&
                sprite.name == expectedSprite;
        }

        private static bool DifficultyButtonUses(
            RecoveredMusicDifficulty difficulty,
            string expectedSprite)
        {
            var image = GameObject.Find(
                "MusicSelectionView/DefaultLivePanels/DifficultyPanel/" +
                difficulty)?.GetComponent<Image>();
            var sprite = image != null
                ? image.overrideSprite ?? image.sprite
                : null;
            return sprite != null && sprite.name == expectedSprite;
        }

        private static bool DifficultyTextUses(
            RecoveredMusicDifficulty difficulty,
            Color expected)
        {
            var text = GameObject.Find(
                "MusicSelectionView/DefaultLivePanels/DifficultyPanel/" +
                difficulty + "/DifficultyLevel")?.GetComponent<Text>();
            if (text == null) return false;
            return ColorsMatch(text.color, expected);
        }

        private static bool DifficultyLabelUses(
            RecoveredMusicDifficulty difficulty,
            Color expected)
        {
            var graphic = GameObject.Find(
                "MusicSelectionView/DefaultLivePanels/DifficultyPanel/" +
                difficulty + "/DifficultyLabel")?.GetComponent<Graphic>();
            return graphic != null && ColorsMatch(graphic.color, expected);
        }

        private static bool ColorsMatch(Color actual, Color expected)
        {
            return Mathf.Abs(actual.r - expected.r) < 0.01f &&
                Mathf.Abs(actual.g - expected.g) < 0.01f &&
                Mathf.Abs(actual.b - expected.b) < 0.01f &&
                Mathf.Abs(actual.a - expected.a) < 0.01f;
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

        private static string FilterScreenshotPath()
        {
            var workspace = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", ".."));
            return Path.Combine(
                workspace,
                "reverse",
                "reports",
                "unity-music-selection-filter.png");
        }

        private static string SortScreenshotPath()
        {
            var workspace = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", ".."));
            return Path.Combine(
                workspace,
                "reverse",
                "reports",
                "unity-music-selection-sort.png");
        }

        private static string VocalFilterScreenshotPath()
        {
            var workspace = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", ".."));
            return Path.Combine(
                workspace,
                "reverse",
                "reports",
                "unity-music-selection-filter-vocals.png");
        }

        private static string BookmarkDialogScreenshotPath()
        {
            var workspace = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", ".."));
            return Path.Combine(
                workspace,
                "reverse",
                "reports",
                "unity-music-selection-bookmark-dialog.png");
        }

        private static string PlayerRateScreenshotPath()
        {
            var workspace = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", ".."));
            return Path.Combine(
                workspace,
                "reverse",
                "reports",
                "unity-music-selection-player-rate.png");
        }

        private static string SpRateScreenshotPath()
        {
            var workspace = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", ".."));
            return Path.Combine(
                workspace,
                "reverse",
                "reports",
                "unity-music-selection-sp-rate.png");
        }

        private static string EmptyBookmarkScreenshotPath()
        {
            var workspace = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "..", ".."));
            return Path.Combine(
                workspace,
                "reverse",
                "reports",
                "unity-music-selection-bookmark-empty.png");
        }

        private static int CaptureSelection(string path)
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
                    rootCanvas.planeDistance = 1f;
                }
                Canvas.ForceUpdateCanvases();
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
                }
                camera.targetTexture = previousTarget;
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
