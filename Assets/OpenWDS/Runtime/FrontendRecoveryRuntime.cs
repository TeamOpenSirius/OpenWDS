using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenWDS.Runtime
{
    /// <summary>Original title UI and Home hosted over the additive Theatre recovery scope.</summary>
    public sealed class FrontendRecoveryRuntime : MonoBehaviour
    {
        [SerializeField] private Shader _spriteShader;
        [SerializeField] private Shader _fontShader;
        public void Configure(Shader spriteShader, Shader fontShader) { _spriteShader = spriteShader; _fontShader = fontShader; }
        private FrontendViewLibrary _view;
        private FrontendViewLibrary _background;
        private FrontendViewLibrary _artwork;
        private FrontendViewLibrary _footer;
        private readonly List<FrontendViewLibrary> _headers = new List<FrontendViewLibrary>();
        private FrontendBgmRuntime _bgm;
        public HomeTheatreRuntime Theatre { get; private set; }
        public OfflineMenuRuntime Menu { get; private set; }
        public Button FullScreenButton { get; private set; }
        public Button ExitFullScreenButton { get; private set; }
        public bool IsFullScreen { get; private set; }
        private Coroutine _titleMusic;
        public FrontendBgmRuntime Bgm => _bgm;
        public Button AnotherButton { get; private set; }
        public IReadOnlyList<FrontendViewLibrary> Headers => _headers;
        public GameObject Background => _background?.Instance;
        public GameObject CharacterCard => Page == "Home" ? _artwork?.Instance : null;
        public GameObject Footer => _footer?.Instance;
        private bool _busy;
        private bool _homeSuspended;
        public TitleTransitionRuntime HomeTransition { get; private set; }
        public double LastHomePreparationSeconds { get; private set; }
        private IDisposable _titleExit;
        public bool TitleAnimationEnded { get; private set; }
        public string Page { get; private set; }
        public bool IsReady => !_busy && _view?.Instance != null;
        public GameObject View => _view?.Instance;
        public int BundleCount => (_view?.BundleCount ?? 0) + (_background?.BundleCount ?? 0) + (_artwork?.BundleCount ?? 0) + (_footer?.BundleCount ?? 0) + HeaderBundleCount + (Theatre != null ? Theatre.BundleCount : 0) + (Menu != null ? Menu.BundleCount : 0);
        private int HeaderBundleCount { get { int count = 0; foreach (var header in _headers) count += header.BundleCount; return count; } }
        public Button StartButton { get; private set; }
        public Image StartPrompt => Page == "Title" ? _view.Field("TitleView", "_startButtonImage").GetComponent<Image>() : null;
        public Button LiveButton { get; private set; }
        private IEnumerator Start()
        {
            _bgm = gameObject.AddComponent<FrontendBgmRuntime>();
            if (FrontendNavigation.ConsumeSelection()) MainPageNavigationRuntime.Instance.OpenSelection();
            else if (FrontendNavigation.ConsumeHome()) yield return ShowHome();
            else yield return ShowTitle();
        }
        private IEnumerator TitleMusic()
        {
            yield return new WaitForSeconds(0.75f);
            _bgm.Play(true);
            _titleMusic = null;
        }

        public IEnumerator LeaveForSelection()
        {
            _bgm.Stop();
            if (Page == "Home" && _view?.Instance != null && Theatre != null && Theatre.IsReady)
            {
                // Page navigation keeps the prepared Home scope until Main is unloaded.
                IsFullScreen = false;
                _view.Field("HomeView", "_uIPanelsObject").gameObject.SetActive(true);
                ExitFullScreenButton.interactable = false;
                ExitFullScreenButton.targetGraphic.raycastTarget = false;
                SetHomeActive(false);
                Theatre.Suspend();
                _homeSuspended = true;
            }
            else
            {
                if (Theatre != null) { yield return Theatre.Release(); Theatre = null; }
                ReleaseViews();
            }
            Page = "Selection";
            _busy = false;
        }

        public IEnumerator ShowTitle()
        {
            if (_busy) yield break;
            _busy = true;
            if (Theatre != null) { yield return Theatre.Release(); Theatre = null; }
            ReleaseViews();
            _background = new FrontendViewLibrary(_spriteShader, _fontShader);
            yield return _background.Load("Feature/Title/TitleBackgroundView", transform);
            // TitleBackgroundView.Initialize -> HideObjects(false), 0xB2491DC.
            foreach (var field in new[] { "_darkBackTex", "_backTex", "_middleTex", "_blackOut", "_smoke" })
                _background.Field("TitleBackgroundView", field).gameObject.SetActive(false);
            foreach (var particle in _background.Instance.GetComponentsInChildren<ParticleSystem>(true))
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var backgroundImage = _background.Field("TitleBackgroundView", "_backgroundImage").GetComponent<Image>();
            backgroundImage.gameObject.SetActive(true);
            // Frozen TitleBackgroundMaster detail 1001 is this scene's explicit artwork fixture.
            _artwork = new FrontendViewLibrary(_spriteShader, _fontShader);
            yield return _artwork.LoadSprite("Title/Title_event_01");
            backgroundImage.sprite = _artwork.Sprite;
            backgroundImage.color = Color.white;
            // ChangeBackground(sprite, false): cover the original rect with a centered native-size image.
            Canvas.ForceUpdateCanvases();
            var rect = backgroundImage.rectTransform;
            var size = rect.rect.size;
            var texture = _artwork.Sprite.texture;
            float scale = Mathf.Max(size.x / texture.width, size.y / texture.height);
            backgroundImage.SetNativeSize();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = new Vector3(scale, scale, 0);
            _view = new FrontendViewLibrary(_spriteShader, _fontShader);
            yield return _view.Load("Feature/Title/TitleView", transform);
            DisableButtons(_view.Instance);
            _view.Field("TitleView", "_startButtonImage").gameObject.SetActive(true);
            _view.Field("TitleView", "_clientVersionText").gameObject.SetActive(true);
            SetText(_view.Field("TitleView", "_clientVersionText"), "ver.2.31.2");
            _view.Field("TitleView", "_commitShortHashText").gameObject.SetActive(false);
            _view.Field("TitleView", "_showUserIdTapArea").gameObject.SetActive(false);
            _view.Field("TitleView", "_anniversaryDecoratioImage").gameObject.SetActive(false);
            StartButton = _view.Field("TitleView", "_startButtonController").GetComponent<Button>();
            StartButton.interactable = true;
            var animator = _view.Field("TitleView", "_titleViewAnimator").GetComponent<Animator>();
            var trigger = animator.GetBehaviour<ObservableStateMachineTrigger>();
            if (trigger == null) throw new InvalidOperationException("Title Animator state callback is missing");
            TitleAnimationEnded = false;
            // ChangeStateAsync (0xA5FF0FC) awaits the first OnStateExit; an early tap only skips the intro.
            _titleExit = trigger.OnStateExitAsObservable().Take(1).Subscribe(_ => TitleAnimationEnded = true);
            StartButton.onClick.AddListener(() =>
            {
                if (_busy) return;
                if (!TitleAnimationEnded) animator.SetTrigger("TapSkipTrigger");
                else StartCoroutine(ShowHome());
            });
            _titleMusic = StartCoroutine(TitleMusic());
            Page = "Title";
            _busy = false;
        }

        public IEnumerator ShowHome()
        {
            if (_busy) yield break;
            _busy = true;
            if (_homeSuspended && _view?.Instance != null && Theatre != null && Theatre.IsReady)
            {
                Theatre.Resume();
                RefreshHomeHeaders();
                SetHomeActive(true);
                _homeSuspended = false;
                _bgm.Play(false);
                var retainedGroup = _view.Field("HomeView", "_canvasGroup").GetComponent<CanvasGroup>();
                retainedGroup.alpha = 0;
                yield return retainedGroup.DOFade(1, 0.2f).SetUpdate(true).WaitForCompletion();
                Page = "Home";
                _busy = false;
                yield break;
            }
            double preparingAt = Time.realtimeSinceStartupAsDouble;
            if (Page == "Title")
            {
                var transition = new GameObject("HomeLoadingTransition");
                HomeTransition = transition.AddComponent<TitleTransitionRuntime>();
                yield return HomeTransition.Show();
            }
            if (Theatre != null) { yield return Theatre.Release(); Theatre = null; }
            ReleaseViews();
            Theatre = new GameObject("TheatreRuntime").AddComponent<HomeTheatreRuntime>();
            yield return Theatre.Prepare(FindObjectOfType<Camera>());
            _view = new FrontendViewLibrary(_spriteShader, _fontShader);
            yield return _view.Load("Feature/Home/HomeView", transform);
            DisableButtons(_view.Instance);
            // Original HomeView.Initialize: hide speech balloon, show UI panels.
            _view.Field("HomeView", "_speechBalloonPanel").gameObject.SetActive(false);
            _view.Field("HomeView", "_uIPanelsObject").gameObject.SetActive(true);
            // These are populated by account/event repositories, not prefab demo values.
            foreach (var field in new[] { "_bannerPanel", "_eventLogoBannerPanel", "_liveNotificationBadge", "_campaignBadge" })
                _view.Field("HomeView", field).gameObject.SetActive(false);
            foreach (var field in new[] { "_presentBoxBadge", "_missionCountBadge", "_missionBadge",
                "_theaterCompanyMissionBadge", "_beginnerMissionBadge", "_beginnerMissionTutorialTips",
                "_actorMissionBadge", "_flashSaleButton" })
                _view.Field("HomeSidePanel", field).gameObject.SetActive(false);
            LiveButton = _view.Field("HomeView", "_liveButton").GetComponent<Button>();
            LiveButton.interactable = true;
            LiveButton.onClick.AddListener(() =>
            {
                if (_busy) return;
                _busy = true;
                _bgm.Stop();
                FrontendNavigation.Selection(false);
            });
            _footer = new FrontendViewLibrary(_spriteShader, _fontShader);
            yield return _footer.Load("GlobalFooterView", transform);
            _footer.Instance.SetActive(true);
            DisableButtons(_footer.Instance);
            // Footer selection and destination presenters remain pending; account-driven hints have no offline source.
            foreach (var field in new[] { "_newBadge", "_balloonBadge", "_tutorialBalloon", "_exclamation", "_lockParts" })
                foreach (var target in _footer.Fields("GlobalFooterButton", field)) target.gameObject.SetActive(false);
            foreach (var target in _footer.Fields("GlobalFooterButton", "_backgroundImage"))
                target.GetComponent<Image>().fillAmount = 0;
            var footerGroup = _footer.Field("GlobalFooterView", "_canvasGroup").GetComponent<CanvasGroup>();
            footerGroup.alpha = 1;
            // HomeView.SetActiveConcertButton indexes the original array by AsideLiveButtonTypes - 1.
            // Frozen archive entry uses type 3 (Yaneura), not the prefab's concert demo label.
            _view.Field("HomeView", "_concertButtonImage").GetComponent<Image>().sprite =
                _view.ArraySprite("HomeView", "_concertButtonSprites", 2);
            AnotherButton = _view.Field("HomeView", "_concertButton").GetComponent<Button>();
            AnotherButton.interactable = true;
            AnotherButton.onClick.AddListener(() =>
            {
                if (_busy) return;
                _busy = true; _bgm.Stop(); FrontendNavigation.Selection(true);
            });
            yield return LoadHomeHeaders();
            // Shared retail MenuPopView: local announcement is available; settings/terms
            // require the reusable common-dialog host which is still selection-owned.
            var menuHost = new GameObject("HomeMenuHost"); menuHost.transform.SetParent(transform, false);
            Menu = menuHost.AddComponent<OfflineMenuRuntime>();
            Menu.Configure(transform, null, () => { }, () => { }, null,
                Resources.Load<GameObject>("Prefabs/Common/Panels/SideMenuPanel/TextSideMenuButton"));
            Menu.NavigateHome = () => { };
            yield return Menu.Initialize();
            if (GetComponent<UiSeRuntime>() == null) gameObject.AddComponent<UiSeRuntime>();
            FullScreenButton = _view.Field("HomeSidePanel", "_fullScreenButton").GetComponent<Button>();
            FullScreenButton.gameObject.SetActive(true); FullScreenButton.interactable = true;
            FullScreenButton.onClick.AddListener(() => SetFullScreen(true));
            // Transparent input adapter on the original full-page root, no replacement artwork.
            var hit = _view.Instance.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = false;
            ExitFullScreenButton = _view.Instance.AddComponent<Button>();
            ExitFullScreenButton.targetGraphic = hit; ExitFullScreenButton.interactable = false;
            ExitFullScreenButton.onClick.AddListener(() => SetFullScreen(false));
            _bgm.Play(false);
            var group = _view.Field("HomeView", "_canvasGroup").GetComponent<CanvasGroup>();
            group.interactable = true;
            group.blocksRaycasts = true;
            group.alpha = 0;
            // Original first HomeView.ShowAsync uses a 0.2 second fade, 0xADB285C.
            yield return group.DOFade(1, 0.2f).SetUpdate(true).WaitForCompletion();
            LastHomePreparationSeconds = Time.realtimeSinceStartupAsDouble - preparingAt;
            if (HomeTransition != null)
            {
                yield return HomeTransition.Hide();
                Destroy(HomeTransition.gameObject);
                HomeTransition = null;
                yield return null;
            }
            Menu.ShowStartupNotice();
            Debug.Log($"OPENWDS_HOME_READY preparationSeconds={LastHomePreparationSeconds:F3}");
            Page = "Home";
            _busy = false;
        }

        private IEnumerator LoadHomeHeaders()
        {
            foreach (var key in new[] { "Feature/Headers/UserDataHeaderView", "Feature/Headers/SpotHeaderView" })
            {
                var header = new FrontendViewLibrary(_spriteShader, _fontShader);
                _headers.Add(header);
                yield return header.Load(key, transform);
                header.Instance.SetActive(true);
                foreach (var group in header.Instance.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1;
                DisableButtons(header.Instance);
            }
            RefreshHomeHeaders();
        }

        private void SetHomeActive(bool active)
        {
            _view.Instance.SetActive(active);
            _footer.Instance.SetActive(active);
            foreach (var header in _headers) header.Instance.SetActive(active);
            Menu.View.SetActive(active);
            Menu.gameObject.SetActive(active);
        }

        private void RefreshHomeHeaders()
        {
            var user = _headers[0];
            // No account economy repository exists; never display authored demonstration balances.
            SetText(user.Field("UserRankDataPanel", "_rankText"), "—");
            user.Field("UserRankDataPanel", "_rankSlider").GetComponent<Slider>().value = 0;
            user.Field("UserRankDataPanel", "_cautionMark").gameObject.SetActive(false);
            var musicCatalog = LocalMusicCatalog.FromJson(SongResourceStore.CatalogJson);
            var rating = PlayerRating.CalculatePlayerRate(musicCatalog.Musics, new LocalResultStore());
            var rateText = user.Field("RateDataPanel", "_rateText");
            SetText(rateText, rating.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            (rateText.GetComponent<PlayerRateGradient>() ?? rateText.gameObject.AddComponent<PlayerRateGradient>()).SetRate(rating);
            SetText(user.Field("GemDataPanel", "_gemText"), "0");
            SetText(user.Field("StaminaDataPanel", "_currentStaminaText"), "—");
            SetText(user.Field("StaminaDataPanel", "_maxStaminaText"), "/—");
            user.Field("UserDataHeaderView", "_getGemPanel").gameObject.SetActive(false);
            user.Field("StaminaDataPanel", "_staminaRecoveryTimePanel").gameObject.SetActive(false);
            SetText(_headers[1].Field("SpotHeaderView", "_pageNameText"), "劇場ロビー");

        }

        public void SetFullScreen(bool visible)
        {
            if (_busy || Page != "Home") return;
            IsFullScreen = visible;
            // HomeView.HidePanelObject/ShowPanelObject (0xADB2B88/0xADB2AF4).
            _view.Field("HomeView", "_uIPanelsObject").gameObject.SetActive(!visible);
            foreach (var header in _headers) header.Instance.SetActive(!visible);
            _footer.Instance.SetActive(!visible);
            Menu.View.SetActive(!visible);
            ExitFullScreenButton.interactable = visible;
            ExitFullScreenButton.targetGraphic.raycastTarget = visible;
        }

        private static void DisableButtons(GameObject root)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                button.onClick.RemoveAllListeners();
                button.interactable = false;
            }
        }
        private static void SetText(Transform target, string value)
        {
            var tmp = target.GetComponent<TMP_Text>();
            if (tmp != null) tmp.text = value;
            else if (target.TryGetComponent<Text>(out var text)) text.text = value;
            else throw new InvalidOperationException("Missing frontend text component: " + target.name);
        }
        private void ReleaseViews()
        {
            _homeSuspended = false;
            if (_titleMusic != null) { StopCoroutine(_titleMusic); _titleMusic = null; }
            _bgm?.Stop();
            IsFullScreen = false; FullScreenButton = null; ExitFullScreenButton = null;
            if (Menu != null) { Menu.gameObject.SetActive(false); Destroy(Menu.gameObject); Menu = null; }
            foreach (var header in _headers) header.Dispose();
            _headers.Clear();
            AnotherButton = null;
            _titleExit?.Dispose(); _titleExit = null;
            TitleAnimationEnded = false;
            if (_view?.Instance != null) _view.Instance.GetComponent<CanvasGroup>()?.DOKill();
            _view?.Dispose(); _view = null;
            _artwork?.Dispose(); _artwork = null;
            _background?.Dispose(); _background = null;
            _footer?.Dispose(); _footer = null;
            StartButton = null; LiveButton = null;
        }
        private void OnDestroy()
        {
            if (HomeTransition != null) Destroy(HomeTransition.gameObject);
            ReleaseViews();
        }
    }
}
