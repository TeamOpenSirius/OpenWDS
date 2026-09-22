using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace OpenWDS.Runtime
{
    // Offline projection of GameResultPresenterParameter; no source scene objects are retained.
    public sealed class GameResultSceneData
    {
        public GameResultRuntime Result;
        public LocalMusicEntry Music;
        public LocalLiveEntry Live;
        public IReadOnlyList<LocalMusicEntry> Musics;
        public long MusicId, CharacterId;
        public string MusicName;
        public MusicDifficulty Difficulty;
        public Sprite Jacket;
        public GameObject Prefab, Background;
        public double RecommendationTiming;
        public double NoteSpeed;
        public bool IsCleared, ShowPerfectStar, IsOfficialAuto;
    }

    public sealed class GameResultSceneRuntime : MonoBehaviour
    {
        public Action PreviewReplay, PreviewReturn;
        public static GameResultSceneRuntime Instance { get; private set; }
        private GameResultSceneData _data;
        private bool _resultShown, _navigationPending;
        private int _gameResultPresentationCount;
        private GameObject _gameResultInstance, _gameResultBackgroundInstance;
        private Sirius.GameResult.GameResultFontRuntime _gameResultFonts;
        private CharacterPresentationRuntime _characterPresentation;
        private GameResultSeRuntime _resultSe;
        private GameResultBgmRuntime _resultBgm;
        public bool IsInitialized { get; private set; }
        public GameObject View => _gameResultInstance;
        public GameObject Background => _gameResultBackgroundInstance;
        public GameResultSceneData Data => _data;
        public CharacterPresentationRuntime CharacterPresentation => _characterPresentation;
        public GameResultSeRuntime ResultSe => _resultSe;
        public GameResultBgmRuntime ResultBgm => _resultBgm;
        private void Awake() { Instance = this; }
        private IEnumerator Start()
        {
            // Component-only previews initialize explicitly in the same frame.
            if (_data != null) yield break;
            _data = SceneNavigationRuntime.Result ?? throw new InvalidOperationException("GameResult requires navigation parameters.");
            ConfigureSound();
            yield return Sirius.GameResult.GameResultFontRuntime.PrepareStreamingAssets();
            if (_data.CharacterId != 0)
            {
                var settings = new SettingsStore().LoadOrDefault();
                _characterPresentation = gameObject.AddComponent<CharacterPresentationRuntime>();
                yield return _characterPresentation.PrepareResult(_data.CharacterId,
                    GameSettings.CalculateCombinedVolume(settings.SoundVolumeSettings.SystemMaster, settings.SoundVolumeSettings.SystemVoice));
            }
            ShowGameResult();
            IsInitialized = true;
        }
        public void InitializePreview(GameResultSceneData data, CharacterPresentationRuntime character)
        {
            _data = data;
            _characterPresentation = character;
            ConfigureSound();
            ShowGameResult();
            IsInitialized = true;
        }
        private void ConfigureSound()
        {
            var shared = UiSeRuntime.Instance;
            if (shared == null) shared = gameObject.AddComponent<UiSeRuntime>();
            _resultSe = gameObject.AddComponent<GameResultSeRuntime>();
            _resultSe.Configure(shared);
            _resultBgm = gameObject.AddComponent<GameResultBgmRuntime>();
            var settings = new SettingsStore().LoadOrDefault();
            _resultBgm.SetVolume(GameSettings.CalculateCombinedVolume(settings.SoundVolumeSettings.GameMaster, settings.SoundVolumeSettings.GameBGM));
        }
        public void ReturnFromResult()
        {
            if (PreviewReturn != null) { PreviewReturn(); return; }
            if (_navigationPending) return;
            _navigationPending = true;
            StartCoroutine(ReturnAfterSe());
        }
        private IEnumerator ReturnAfterSe()
        {
            UiSeRuntime.Instance?.Play(UiSeRuntime.Cue.ButtonGo);
            yield return new WaitForSecondsRealtime(.12f);
            _resultBgm.Stop();
            if (!SceneNavigationRuntime.ReturnToMain(true)) throw new InvalidOperationException("Result return failed.");
        }
        public void ReplayFromResult()
        {
            if (PreviewReplay != null) { PreviewReplay(); return; }
            if (_navigationPending) return;
            _navigationPending = true;
            StartCoroutine(Replay());
        }
        private IEnumerator Replay()
        {
            UiSeRuntime.Instance?.Play(UiSeRuntime.Cue.ButtonGo);
            yield return new WaitForSecondsRealtime(.12f);
            if (!LocalMusicSelectionSession.HasSelection) throw new InvalidOperationException("Replay requires a chart session.");
            yield return SplitLaneAssetRuntime.PrepareStreamingAssets(LocalMusicSelectionSession.ChartAsset.text);
            SplitLaneAssetRuntime.ThrowIfStreamingAssetPreparationFailed();
            _resultBgm.Stop();
            var nav = SceneNavigationRuntime.EnsureExists();
            nav.ClearResult();
            if (!nav.ReplayGame()) throw new InvalidOperationException("Result replay failed.");
        }
        private void OnDestroy()
        {
            _gameResultFonts?.Dispose();
            if (Instance == this) Instance = null;
        }
        private void ShowGameResult()
        {
            if (_resultShown) return;
            if (_data.Prefab == null)
                throw new InvalidOperationException("Game result prefab is required.");
            _resultShown = true;
            _gameResultPresentationCount++;

            if (_data.Background != null)
            {
                _gameResultBackgroundInstance = Instantiate(_data.Background);
                // GameResultStandbyController.PlayStandbyMove explicitly enables
                // CurtainClose. The extracted subtree preserves its authored
                // inactive root, so reproduce that controller call here.
                _gameResultBackgroundInstance.SetActive(true);
            }
            // GameResultStandbyController.PlayStandbyMove publishes
            // BgmType.GameResult (enum value 2) at this scene boundary.
            _resultBgm?.Play();

            var canvasObject = new GameObject(
                "GameResultCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler),
                typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            // Prepare zero digits and inactive badges before any result effect
            // receives OnEnable. Initialize also serves settled-state fixtures.
            canvasObject.SetActive(false);
            _gameResultInstance = Instantiate(_data.Prefab, canvasObject.transform);
            var rect = _gameResultInstance.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }
            // Font replacement invalidates TMP/layout data. Match the capture and
            // original full-screen root order: establish the final canvas rect
            // before any font-driven layout rebuild.
            if (_gameResultFonts == null)
                _gameResultFonts =
                    new Sirius.GameResult.GameResultFontRuntime();
            _gameResultFonts.Apply(_gameResultInstance);
            var rootCanvasGroup = _gameResultInstance.GetComponent<CanvasGroup>();
            if (rootCanvasGroup != null)
            {
                // Original GameResultView.ShowAsync calls ShowAlphaAsync here.
                rootCanvasGroup.alpha = 1f;
                rootCanvasGroup.interactable = true;
                rootCanvasGroup.blocksRaycasts = true;
            }
            var anotherId = _data.Live?.AnotherNotationId ?? 0;
            var resultKey = anotherId > 0 ? anotherId : _data.MusicId;
            var localResults = new LocalResultStore(anotherNotation: anotherId > 0);
            var isRatingTarget = anotherId == 0 && PlayerRating.IsEligible(
                _data.Music, _data.Live);
            var beforePlayerRate = _data.Musics != null
                ? PlayerRating.CalculatePlayerRate(
                    _data.Musics, anotherId > 0 ? new LocalResultStore() : localResults)
                : 0d;
            var previousNotationRate = isRatingTarget
                ? PlayerRating.CalculateNotationRate(
                    _data.Live.Level,
                    localResults.GetBest(resultKey, _data.Difficulty))
                : 0d;
            var isSpTarget = OlivierStars.IsEligible(_data.Music, _data.Live);
            if (isSpTarget)
            {
                previousNotationRate = localResults.GetSpPoint(_data.MusicId);
                beforePlayerRate = _data.Musics != null
                    ? OlivierStars.GetTotalPoint(_data.Musics, localResults) : previousNotationRate;
            }
            var isCleared = _data.Result.CollectedCount > 0 &&
                _data.IsCleared;
            var thisLamp = !isCleared ? ClearLamp.None : _data.Result.IsAllPerfect ? ClearLamp.AllPerfect :
                _data.Result.IsFullCombo ? ClearLamp.FullCombo : ClearLamp.Clear;
            var nonPerfectStarCount = 0;
            foreach (var pair in _data.Result.TimingCounts)
                if (pair.Key != TimingType.PerfectStar) nonPerfectStarCount += pair.Value;
            var thisTimeSpPoint = localResults.RecordOlivierResult(
                _data.Music, _data.Live, _data.Result.AchievementRate,
                thisLamp, nonPerfectStarCount, isCleared,
                _data.IsOfficialAuto);
            var previousBestAchievementRate = localResults.GetBest(resultKey, _data.Difficulty);
            var isNewAchievementRate = !_data.IsOfficialAuto && localResults.RecordResult(
                resultKey,
                _data.Difficulty,
                _data.Result.AchievementRate,
                thisLamp,
                out previousBestAchievementRate);
            if (_characterPresentation != null)
            {
                var clearLamp = !_data.IsCleared ? 0 :
                    _data.Result.IsAllPerfect ? 6 : _data.Result.IsFullCombo ? 2 : 1;
                StartCoroutine(_characterPresentation.ShowResult(
                    _gameResultBackgroundInstance, clearLamp, isNewAchievementRate));
            }
            var thisTimeNotationRate = isRatingTarget
                ? PlayerRating.CalculateNotationRate(
                    _data.Live.Level,
                    _data.Result.AchievementRate)
                : 0d;
            var bestEverNotationRate = isRatingTarget
                ? PlayerRating.CalculateNotationRate(
                    _data.Live.Level,
                    localResults.GetBest(resultKey, _data.Difficulty))
                : 0d;
            var afterPlayerRate = _data.Musics != null
                ? PlayerRating.CalculatePlayerRate(
                    _data.Musics, anotherId > 0 ? new LocalResultStore() : localResults)
                : 0d;
            if (isSpTarget)
            {
                thisTimeNotationRate = thisTimeSpPoint;
                bestEverNotationRate = localResults.GetSpPoint(_data.MusicId);
                afterPlayerRate = _data.Musics != null
                    ? OlivierStars.GetTotalPoint(_data.Musics, localResults) : bestEverNotationRate;
            }
            var isNewNotationRate = isSpTarget
                ? thisTimeNotationRate > previousNotationRate
                : bestEverNotationRate > previousNotationRate;
            if (isSpTarget)
            {
                var maximum = _data.Musics != null
                    ? OlivierStars.GetTotalObtainablePoint(_data.Musics, DateTime.UtcNow) : 0;
                beforePlayerRate = OlivierStars.GetPercentage((int)beforePlayerRate, maximum);
                afterPlayerRate = OlivierStars.GetPercentage((int)afterPlayerRate, maximum);
            }
            var isNewPlayerRate = afterPlayerRate > beforePlayerRate;
            BindMusicInfo(_gameResultInstance.transform, localResults);
            var panel = _gameResultInstance.GetComponentInChildren<
                Sirius.GameResult.GameResultPanel>(true);
            if (panel == null)
                throw new InvalidOperationException("GameResultPanel is missing.");
            // Only the product's pre-live Autoplay button may set IsAuto.
            // _enableAutoJudge is a debug input injector and must keep the
            // ordinary HUD/result presentation.
            var isOfficialAutoplay = _data.IsOfficialAuto;
            var viewData =
                Sirius.GameResult.GameResultViewData.FromRuntime(
                    _data.Result,
                    _data.RecommendationTiming,
                    _data.NoteSpeed,
                    isOfficialAutoplay,
                    _data.MusicName,
                    _data.Difficulty,
                    _data.Jacket,
                    previousBestAchievementRate,
                    isNewAchievementRate,
                    _data.ShowPerfectStar,
                    // GameResultRate needs the value before this play on the
                    // left of its old -> new presentation.  The store already
                    // contains the new result at this point, so passing
                    // bestEverNotationRate made both numbers identical.
                    previousNotationRate,
                    thisTimeNotationRate,
                    beforePlayerRate,
                    afterPlayerRate,
                    isNewNotationRate,
                    isNewPlayerRate,
                    isCleared: isCleared,
                    isLongVersion: _data.Music?.IsLongVersion ?? false,
                    isAnotherNotation: anotherId > 0);
            panel.Initialize(viewData);
            BindResultNavigation();
            // GameResultView.ShowAsync starts from the serialized root alpha 0
            // and calls AnimationUtility.ShowAlphaAsync (linear 0.2 seconds).
            // Initialize also supports settled-state editor previews.
            if (rootCanvasGroup != null) rootCanvasGroup.alpha = 0f;

            // GameResultView.ShowAsync sets the original root Animator's "Next"
            // trigger before awaiting its entrance state. Without the removed
            // GameResultView component the controller remains in GameResult_in,
            // whose final LeftPanel X is 52; GameResult_left_in ends at the prefab
            // position X=477. Keep the original pivot and drive the missing call.
            var slideAnimator = _gameResultInstance.GetComponent<Animator>();
            StartCoroutine(CompleteResultPresentation(slideAnimator));


        }

        private void BindResultNavigation()
        {
            var root = _gameResultInstance.transform.Find("RightBotton");
            var next = root != null
                ? root.Find("NextButton")?.GetComponent<Button>()
                : null;
            var replay = root != null
                ? root.Find("InGameButton")?.GetComponent<Button>()
                : null;
            if (root == null || next == null || replay == null)
                throw new InvalidOperationException(
                    "Original GameResult NextButton/InGameButton are missing.");
            // GameResultView.Initialize calls GameResultNextPanel.Inactivate.
            // The Presenter activates the whole panel only after the entrance
            // and its numeric result animation have completed.
            next.gameObject.SetActive(true);
            replay.gameObject.SetActive(true);
            root.gameObject.SetActive(false);
            next.onClick.RemoveAllListeners();
            replay.onClick.RemoveAllListeners();
            next.onClick.AddListener(ReturnFromResult);
            replay.onClick.AddListener(ReplayFromResult);
        }

        private IEnumerator CompleteResultPresentation(
            Animator slideAnimator)
        {
            var resultPanel = _gameResultInstance.GetComponentInChildren<
                Sirius.GameResult.GameResultPanel>(true);
            var counts = resultPanel.CreateCountUp(_resultSe);
            _gameResultInstance.transform.parent.gameObject.SetActive(true);
            var canvasGroup = _gameResultInstance.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
                canvasGroup.DOFade(1f, 0.2f).SetEase(Ease.Linear)
                    .SetLink(_gameResultInstance);
            if (slideAnimator != null)
            {
                // The controller must be enabled before receiving Next. Sending
                // it under the inactive preparation Canvas loses the trigger
                // when the Animator initializes and leaves LeftPanel at X=52.
                slideAnimator.SetTrigger(Animator.StringToHash("Next"));
                // GameResultView.ShowAsync activates _StageSuccess (offset
                // 0xA8) immediately after Next; its authored root is inactive.
                var stageSuccess = _gameResultInstance.transform.Find(
                    "SucceseTextImagePosition/GameResultStageSuccess");
                if (stageSuccess == null)
                    throw new InvalidOperationException("Stage Success presentation is missing.");
                stageSuccess.gameObject.SetActive(true);
                // Let the trigger transition be evaluated, then reproduce
                // GameResultView.ShowAsync's normalized-time completion wait.
                yield return null;
                while (slideAnimator != null &&
                       (slideAnimator.IsInTransition(0) ||
                        slideAnimator.GetCurrentAnimatorStateInfo(0)
                            .normalizedTime < 1f))
                {
                    yield return null;
                }
            }

            yield return counts.Play().WaitForCompletion();

            if (_gameResultInstance == null) yield break;
            var navigation = _gameResultInstance.transform.Find("RightBotton");
            if (navigation != null)
                navigation.gameObject.SetActive(true);
        }

        private void BindMusicInfo(
            Transform root,
            LocalResultStore localResults)
        {
            var musicInfo = root.Find("LeftPanel/MusicInfoPanel");
            if (musicInfo == null) return;

            var title = musicInfo.Find("MusicNamelText/BodyRoot/BodyText")
                ?.GetComponent<UnityEngine.UI.Text>();
            if (title != null) title.text = _data.MusicName;
            var difficulty = musicInfo.Find("Difficulty/Text")
                ?.GetComponent<UnityEngine.UI.Text>();
            if (difficulty != null)
                difficulty.text = _data.Difficulty.ToString().ToUpperInvariant();
            var difficultyImage = musicInfo.Find("Difficulty")
                ?.GetComponent<UnityEngine.UI.Image>();
            if (difficultyImage != null)
                difficultyImage.color = GameResultDifficultyColor(
                    _data.Difficulty);
            var jacket = musicInfo.Find("FocusMask/JacketImage")
                ?.GetComponent<UnityEngine.UI.Image>();
            if (jacket != null && _data.Jacket != null)
                jacket.sprite = _data.Jacket;

            var resultKey = (_data.Live?.AnotherNotationId ?? 0) > 0 ? _data.Live.AnotherNotationId : _data.MusicId;
            var lamps = musicInfo.Find("ClearLamps");
            var lampEffects = musicInfo.Find("ClearLampEffects");
            if (lamps != null)
            {
                for (var index = 0; index < lamps.childCount; index++)
                {
                    var child = lamps.GetChild(index);
                    var suffix = child.name.StartsWith("Lamp", StringComparison.Ordinal)
                        ? child.name.Substring(4)
                        : string.Empty;
                    var lampStatus =
                        Enum.TryParse(
                            suffix,
                            true,
                            out MusicDifficulty lampDifficulty) &&
                        localResults != null &&
                        localResults.HasClear(resultKey, lampDifficulty)
                            ? localResults.GetClearLamp(
                                resultKey, lampDifficulty)
                            : ClearLamp.None;
                    // Native GameResultMusicInfoPanel.Initialize: AnotherNotation
                    // retains only the current difficulty's lamp object (B911964).
                    child.gameObject.SetActive((_data.Live?.AnotherNotationId ?? 0) == 0 ||
                        string.Equals(suffix, _data.Difficulty.ToString(), StringComparison.OrdinalIgnoreCase));
                    for (var childIndex = 0;
                         childIndex < child.childCount;
                         childIndex++)
                    {
                        var lamp = child.GetChild(childIndex);
                        lamp.gameObject.SetActive(
                            lamp.name == "ClearLampImage");
                        var image = lamp.GetComponent<UnityEngine.UI.Image>();
                        if (lampStatus != ClearLamp.None)
                        {
                            var source = FindResultLampSource(
                                lampEffects, suffix, lampStatus);
                            if (image != null && source != null)
                                image.sprite = source.sprite;
                        }
                    }
                }
            }
            if (lampEffects != null) lampEffects.gameObject.SetActive(false);
        }

        private static Color32 GameResultDifficultyColor(
            MusicDifficulty difficulty)
        {
            // GameResultMusicInfoPanel.Initialize calls
            // ColorPreset.get_Difficulty_* rather than the darker selection
            // frame palette. Stella is packed as 0xFFE96786 in the original
            // ARM64 method: RGBA (134, 103, 233, 255).
            if (difficulty == MusicDifficulty.Stella)
                return new Color32(134, 103, 233, 255);
            return MusicSelectionPreviewRuntime.DifficultyFrameColor(
                difficulty);
        }

        private static UnityEngine.UI.Image FindResultLampSource(
            Transform effects,
            string difficulty,
            ClearLamp lamp)
        {
            if (effects == null) return null;
            var state = lamp == ClearLamp.AllPerfect
                ? "ClearLampAllParfect"
                : lamp == ClearLamp.FullCombo
                    ? "ClearLampFullCombo"
                    : "ClearLampClear";
            return effects.Find(
                    "ClearLampEffect" + difficulty + "/" + state +
                    "/ClearLampImage")
                ?.GetComponent<UnityEngine.UI.Image>();
        }
    }
}
