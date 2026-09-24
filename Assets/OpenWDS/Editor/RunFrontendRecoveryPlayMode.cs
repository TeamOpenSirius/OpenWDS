using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using OpenWDS.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenWDS.Editor
{
    [InitializeOnLoad]
    public static class RunFrontendRecoveryPlayMode
    {
        private const string Key = "OpenWDS.FrontendGate.";
        private static string Report => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../reverse/reports/frontend-playmode.json"));
        private static bool _startupNoticeValidated, _startupNoticeCaptured;
        private static int _phase;
        private static int _homeUiPhase;
        private static float _actorTime;
        private static bool _actorAnimationAdvanced, _sceneLifetimeValidated, _selectionBackValidated;
        private static int _mainSceneHandle, _criInstance;
        private static double _next;
        private static readonly System.Collections.Generic.List<double> _directEntrySeconds = new System.Collections.Generic.List<double>();
        private static string _error;
        private static int _titleBundles, _homeBundles;
        private static int _homeViewId, _homeButtonId, _theatreId;
        private static readonly System.Collections.Generic.List<double> _homeReturnSeconds = new System.Collections.Generic.List<double>();
        private static string[] _titleLoaded, _homeLoaded;
        static RunFrontendRecoveryPlayMode() { EditorApplication.update += Poll; Application.logMessageReceived += Log; }
        public static void Run()
        {
            CreateFrontendRecoveryScene.Run();
            SessionState.SetBool(Key + "active", true);
            SessionState.SetBool(Key + "exit", false);
            SessionState.SetBool(Key + "homeCover", false);
            SessionState.SetString(Key + "start", DateTime.UtcNow.ToString("O"));
            EditorApplication.isPlaying = true;
        }
        private static void Log(string message, string stack, LogType type)
        {
            if (SessionState.GetBool(Key + "active", false) &&
                (type == LogType.Error || type == LogType.Exception || type == LogType.Assert ||
                 message.Contains("referenced script") && message.Contains("missing")))
                _error = _error ?? message;
        }
        private static void Poll()
        {
            if (!SessionState.GetBool(Key + "active", false)) return;
            if (SessionState.GetBool(Key + "exit", false))
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetBool(Key + "active", false);
                    EditorApplication.Exit(SessionState.GetBool(Key + "passed", false) ? 0 : 1);
                }
                return;
            }
            if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Key + "start", ""))).TotalSeconds > 240) { Finish("Timed out"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (_error != null) { Finish(_error); return; }
            if (EditorApplication.timeSinceStartup < _next) return;
            try
            {
                var host = UnityEngine.Object.FindObjectOfType<FrontendRecoveryRuntime>();
                if (_phase == 2 && host != null && host.HomeTransition != null &&
                    host.HomeTransition.Phase == "loading")
                {
                    Require(host.HomeTransition.IsCovering, "Home preparation exposed an uncovered frame");
                    if (!SessionState.GetBool(Key + "homeCover", false))
                    {
                        Validate(host.HomeTransition.View);
                        var transitionAnimator = host.HomeTransition.View.GetComponentInChildren<Animator>(true);
                        Debug.Log("OPENWDS_TITLE_STATE " + transitionAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash +
                            " time=" + transitionAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime);
                        foreach (var graphic in host.HomeTransition.View.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                            Debug.Log($"OPENWDS_TITLE_VISIBLE {graphic.name} active={graphic.gameObject.activeInHierarchy} enabled={graphic.enabled} color={graphic.color} alpha={graphic.canvasRenderer.GetInheritedAlpha()} rect={graphic.rectTransform.rect} scale={graphic.transform.lossyScale} material={graphic.material.name}");
                        CaptureTheatre(Path.Combine(Path.GetDirectoryName(Report), "frontend-home-loading.png"));
                        SessionState.SetBool(Key + "homeCover", true);
                    }
                }
                if ((_phase < 5 || _phase == 7 || _phase == 9 || _phase == 12 || _phase == 14) && (host == null || !host.IsReady)) return;
                if ((_phase == 7 || _phase == 9 || _phase == 12 || _phase == 14) &&
                    (host.Page != "Home" || !host.Bgm.IsPlaying)) return;
                if (_phase == 2 && host != null && host.Page == "Home" && !_startupNoticeValidated)
                {
                    var documents = host.Menu.Documents;
                    if (documents.IsTransitioning) return;
                    Require(documents.Notice != null, "First Home announcement missing");
                    var body = documents.Notice.GetComponentsInChildren<Text>().Single(t => t.name == "Body");
                    Require(body.text.Contains("非盈利") && body.text.Contains("non-profit"), "Bilingual statement missing");
                    body.font.RequestCharactersInTexture(body.text, body.fontSize, body.fontStyle);
                    Require(body.text.Where(c => !char.IsWhiteSpace(c)).All(c => body.font.HasCharacter(c)), "Statement font missing glyphs");
                    if (!_startupNoticeCaptured)
                    {
                        Capture("frontend-startup-notice.png");
                        _startupNoticeCaptured = true;
                        _next = EditorApplication.timeSinceStartup + 0.5;
                        return;
                    }
                    Click(documents.Notice.GetComponentsInChildren<Button>().Single(b => b.name == "CloseButton"));
                    _startupNoticeValidated = true;
                    _next = EditorApplication.timeSinceStartup + 0.5;
                    return;
                }
                var directHost = UnityEngine.Object.FindObjectOfType<LocalMusicSelectionRuntime>();
                if (directHost != null && directHost.IsDirectAnotherEntry)
                {
                    Require(directHost.StandardListInitializationCount == 0 && directHost.Selection == null,
                        "Direct Another entry initialized ordinary selection");
                    Require((GameObject.Find("MusicSelectionView") == null) && GameObject.Find("LiveBackground") == null && directHost.BackButton == null,
                        "Direct Another entry exposed ordinary page assets");
                    Require(directHost.GetComponent<MusicSelectionPreviewRuntime>() == null,
                        "Direct Another entry started ordinary preview");
                }
                if (MainPageNavigationRuntime.Instance != null && MainPageNavigationRuntime.Instance.IsTransitioning) return;
                if (_phase == 7 || _phase == 9 || _phase == 12 || _phase == 14)
                {
                    Require(host.Menu.Documents.Notice == null, "Announcement repeated on Home return");
                    Require(host.View.GetInstanceID() == _homeViewId && host.LiveButton.GetInstanceID() == _homeButtonId &&
                        host.Theatre.GetInstanceID() == _theatreId, "Home return rebuilt the view/buttons/theatre");
                    Require(!host.Theatre.IsSuspended && host.Theatre.HomeCamera.isActiveAndEnabled &&
                        !GameObject.Find("UICamera").GetComponent<Camera>().orthographic, "Home camera was not restored");
                    Require(LoadedBundles().SequenceEqual(_homeLoaded), "Home return retained selection bundles");
                    _homeReturnSeconds.Add(MainPageNavigationRuntime.Instance.LastTransitionSeconds);
                }
                switch (_phase)
                {
                    case 0:
                        Require(host.Page == "Title", "Title not initialized");
                        _mainSceneHandle = host.gameObject.scene.handle;
                        _criInstance = UnityEngine.Object.FindObjectOfType<CriWare.CriWareInitializer>().GetInstanceID();
                        _titleBundles = host.BundleCount;
                        _titleLoaded = LoadedBundles();
                        CaptureInstallDialog();
                        Validate(host.View);
                        Require(!host.TitleAnimationEnded, "Intro was already complete before the early tap test");
                        Click(host.StartButton);
                        Require(host.Page == "Title", "Early tap entered Home instead of skipping the intro");
                        break;
                    case 1:
                        Require(host.TitleAnimationEnded, "Title state exit callback did not complete");
                        Require(host.StartPrompt != null && host.StartPrompt.sprite != null, "Original TAP TO START sprite missing");
                        if (host.StartPrompt.color.a < 0.25f) return;
                        Require(host.Bgm != null && host.Bgm.Cue == "TITLE" && host.Bgm.IsPlaying && host.Bgm.Playback.GetTime() > 0,
                            "Title CRI cue is not advancing");
                        Capture("frontend-title.png");
                        foreach (var text in host.View.GetComponentsInChildren<TMPro.TMP_Text>())
                        {
                            text.ForceMeshUpdate();
                            Require(string.IsNullOrWhiteSpace(text.text) || text.textInfo.meshInfo.Any(m => m.vertexCount > 0), "Empty title text geometry: " + text.name);
                        }
                        Click(host.StartButton);
                        break;
                    case 2:
                        Require(host.Page == "Home", "Start did not open Home");
                        Require(SessionState.GetBool(Key + "homeCover", false), "Title loading transition was not observed");
                        Require(host.HomeTransition == null, "Home input restored before transition finished");
                        _homeBundles = host.BundleCount;
                        _homeLoaded = LoadedBundles();
                        Validate(host.View);
                        Require(host.Theatre != null && host.Theatre.IsReady && host.Theatre.HasScene, "Additive theatre missing");
                        Require(host.Theatre.Character != null && host.Theatre.Character.Animator.runtimeAnimatorController != null, "Home actor missing");
                        Require(host.Theatre.HomeCamera.isActiveAndEnabled, "Original Home virtual camera inactive");
                        Require(host.Theatre.Environment.GetComponentsInChildren<Renderer>().Count(r => r.enabled) > 100, "Theatre geometry missing");
                        Validate(host.Footer);
                        Require(host.Footer.GetComponentsInChildren<Button>(true).All(b => !b.interactable), "Unrestored footer destinations enabled");
                        Require(host.Bgm.Cue == "inst_bgm" && host.Bgm.IsPlaying && host.Bgm.Playback.GetTime() > 0, "Home CRI cue is not advancing");
                        Require(host.Headers.Count == 2, "Home headers missing");
                        var rate = PlayerRating.CalculatePlayerRate(LocalMusicCatalog.FromJson(
                            SongResourceStore.CatalogJson).Musics, new LocalResultStore());
                        Require(host.Headers[0].Field("RateDataPanel", "_rateText").GetComponent<Text>().text ==
                            rate.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) &&
                            host.Headers[0].Field("GemDataPanel", "_gemText").GetComponent<Text>().text == "0", "Home rating/economy");
                        foreach (var header in host.Headers) Validate(header.Instance);
                        Require(host.AnotherButton.IsInteractable(), "Home archive entry disabled");
                        if (_homeUiPhase == 0)
                        {
                            Require(UnityEngine.SceneManagement.SceneManager.sceneCount == 2, "Home must own one additive environment");
                            _actorTime = host.Theatre.Character.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                            Capture("frontend-home.png"); Click(host.FullScreenButton);
                            _homeUiPhase = 1; _next = EditorApplication.timeSinceStartup + 1; return;
                        }
                        if (_homeUiPhase == 1)
                        {
                            _actorAnimationAdvanced = host.Theatre.Character.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime > _actorTime;
                            Require(_actorAnimationAdvanced, "Original Home character animation did not advance");
                            Require(host.IsFullScreen && !host.Footer.activeSelf && !host.Menu.View.activeSelf, "Fullscreen did not hide UI");
                            Capture("frontend-home-fullscreen.png"); Click(host.ExitFullScreenButton);
                            _homeUiPhase = 2; _next = EditorApplication.timeSinceStartup + 1; return;
                        }
                        if (_homeUiPhase == 2)
                        {
                            Require(!host.IsFullScreen && host.Footer.activeSelf, "Fullscreen return failed");
                            Click(host.Menu.View.GetComponentInChildren<Button>());
                            _homeUiPhase = 3; _next = EditorApplication.timeSinceStartup + 1; return;
                        }
                        if (host.Menu.IsTransitioning) return;
                        if (_homeUiPhase == 3)
                        {
                            Capture("frontend-home-menu.png");
                            Click(host.Menu.Popup.GetComponentsInChildren<Button>().Single(b => b.transform.parent.name == "Notification"));
                            _homeUiPhase = 4; _next = EditorApplication.timeSinceStartup + 1; return;
                        }
                        var documents = host.Menu.GetComponent<OfflineMenuDocuments>();
                        if (_homeUiPhase == 4)
                        {
                            if (documents.Notice == null || documents.IsTransitioning) return;
                            Require(GameObject.Find("ProjectStatementNotice") != null, "Home notice entry missing");
                            Capture("frontend-home-notices.png");
                            _homeUiPhase = 40; _next = EditorApplication.timeSinceStartup + 1; return;
                        }
                        if (_homeUiPhase == 40)
                        {
                            Click(documents.Notice.GetComponentsInChildren<Button>().Single(b => b.name == "CloseButton"));
                            _homeUiPhase = 5; _next = EditorApplication.timeSinceStartup + 1; return;
                        }
                        if (_homeUiPhase == 5)
                        {
                            Require(documents.Notice == null, "Home notice remained after closing");
                            Click(host.Menu.Popup.GetComponentsInChildren<Button>().Single(b => b.name == "CloseButton"));
                            _homeUiPhase = 6; _next = EditorApplication.timeSinceStartup + 1; return;
                        }
                        Require(!host.Menu.IsOpen, "Home menu touch blocker remained");
                        host.StartCoroutine(host.ShowTitle());
                        break;
                    case 3:
                        if (!host.TitleAnimationEnded) return;
                        Require(host.Page == "Title" && host.BundleCount == _titleBundles, "Title reload changed its scope");
                        Require(LoadedBundles().SequenceEqual(_titleLoaded), "Title retained stale native bundles");
                        Require(UnityEngine.SceneManagement.SceneManager.sceneCount == 1 && UnityEngine.Object.FindObjectOfType<HomeTheatreRuntime>() == null, "Title retained Theatre scene");
                        Validate(host.View);
                        Click(host.StartButton);
                        break;
                    case 4:
                        Require(host.Page == "Home" && host.BundleCount == _homeBundles, "Home reload changed its scope");
                        Require(LoadedBundles().SequenceEqual(_homeLoaded), "Home retained stale native bundles");
                        _homeViewId = host.View.GetInstanceID();
                        _homeButtonId = host.LiveButton.GetInstanceID();
                        _theatreId = host.Theatre.GetInstanceID();
                        Click(host.LiveButton);
                        break;
                    case 5:
                        var selection = UnityEngine.Object.FindObjectOfType<LocalMusicSelectionRuntime>();
                        if (selection == null || !selection.IsInitialized) return;
                        if (!selection.HasLoadedJacket(selection.Selection.Music.Id)) return;
                        Require(host != null && host.Page == "Selection" && selection.gameObject.scene.handle == _mainSceneHandle,
                            "Home and selection did not share the same Main scene");
                        Require(UnityEngine.Object.FindObjectOfType<CriWare.CriWareInitializer>().GetInstanceID() == _criInstance,
                            "Page navigation recreated CRI host");
                        _sceneLifetimeValidated = UnityEngine.SceneManagement.SceneManager.sceneCount == 2 &&
                            host.Theatre.GetInstanceID() == _theatreId && host.Theatre.IsSuspended &&
                            !host.Theatre.Environment.activeInHierarchy && !host.View.activeInHierarchy &&
                            !GameObject.Find("UICamera").GetComponent<Cinemachine.CinemachineBrain>().enabled;
                        Require(_sceneLifetimeValidated, "Selection did not suspend the retained Home scope");
                        Require(GameObject.Find("LiveBackground") != null, "Original regular selection background missing");
                        Capture("frontend-selection.png");
                        UnityEngine.Object.FindObjectOfType<OfflineMenuRuntime>().Open();
                        break;
                    case 6:
                        var menu = UnityEngine.Object.FindObjectOfType<OfflineMenuRuntime>();
                        if (menu == null || menu.IsTransitioning) return;
                        var home = menu.Popup.GetComponentsInChildren<Button>().Single(b => b.transform.parent.name == "Home");
                        Require(!menu.Popup.GetComponentsInChildren<Transform>(true).Any(t => t.name == "AprilFool"), "Temporary menu entry remains");
                        Click(home);
                        break;
                    case 7:
                        Require(host.Page == "Home", "Menu Home did not restore Home");
                        Click(host.AnotherButton);
                        break;
                    case 8:
                        var another = UnityEngine.Object.FindObjectOfType<AnotherNotationSelectionRuntime>();
                        if (another == null || !another.IsReady || another.IsPreparing) return;
                        Require(another.EntryCount == 112, "Home archive entry did not load all special charts");
                        _directEntrySeconds.Add(directHost.InitializationSeconds);
                        Capture("frontend-another.png");
                        Click(GameObject.Find("AnotherNotationBack").GetComponent<Button>());
                        break;
                    case 9:
                        Require(host.Page == "Home" && host.Bgm.IsPlaying, "Archive return did not restore Home/music");
                        Click(host.AnotherButton);
                        break;
                    case 10:
                        var archive = UnityEngine.Object.FindObjectOfType<AnotherNotationSelectionRuntime>();
                        if (archive == null || !archive.IsReady || archive.IsPreparing) return;
                        UnityEngine.Object.FindObjectOfType<OfflineMenuRuntime>().Open();
                        break;
                    case 11:
                        var archiveMenu = UnityEngine.Object.FindObjectOfType<OfflineMenuRuntime>();
                        if (archiveMenu == null || archiveMenu.IsTransitioning) return;
                        Click(archiveMenu.Popup.GetComponentsInChildren<Button>().Single(b => b.transform.parent.name == "Home"));
                        break;
                    case 12:
                        Require(host.Page == "Home" && host.Bgm.IsPlaying, "Archive menu return did not restore Home/music");
                        Click(host.LiveButton);
                        break;
                    case 13:
                        var backSelection = UnityEngine.Object.FindObjectOfType<LocalMusicSelectionRuntime>();
                        if (backSelection == null || !backSelection.IsInitialized || !backSelection.HasLoadedJacket(backSelection.Selection.Music.Id)) return;
                        var backButton = backSelection.BackButton;
                        Require(backButton != null, "Original selection header BackButton missing");
                        Validate(backButton.transform.parent.gameObject);
                        var backPoint = RectTransformUtility.WorldToScreenPoint(null, backButton.transform.position);
                        Require(backPoint.x > 0 && backPoint.x < Screen.width * .15f && backPoint.y > Screen.height * .8f && backPoint.y < Screen.height,
                            "Selection BackButton is outside the top-left corner: " + backPoint);
                        Capture("frontend-selection.png");
                        Click(backButton);
                        break;
                    case 14:
                        Require(host.Page == "Home" && host.Bgm.IsPlaying && host.Theatre.IsReady, "Selection BackButton did not restore Home/theatre/music");
                        Require(LoadedBundles().SequenceEqual(_homeLoaded), "Selection header retained bundles after BackButton return");
                        _selectionBackValidated = true;
                        Finish(null);
                        return;
                }
                _phase++;
                _next = EditorApplication.timeSinceStartup + (_phase == 1 ? 6 : 2);
            }
            catch (Exception ex) { Finish(ex.ToString()); }
        }
        private static void CaptureInstallDialog()
        {
            var root = new GameObject("InstallDialogVisualValidation");
            bool background = Application.runInBackground;
            int sleep = Screen.sleepTimeout;
            try
            {
                var bootstrap = root.AddComponent<OfflineSongResourcesBootstrap>();
                bootstrap.enabled = false;
                typeof(OfflineSongResourcesBootstrap).GetMethod("CreateDialog",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(bootstrap, null);
                var shell = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "SongResourceDialog");
                Require(shell.GetComponentsInChildren<Image>(true).Count(i => i.sprite != null) >= 5,
                    "Installation dialog did not use original frame/buttons");
                Require(shell.GetComponentsInChildren<Text>(true).All(t => t.font != null), "Installation font missing");
                RunLocalMusicSelectionPlayMode.CaptureSelection(Path.Combine(Path.GetDirectoryName(Report), "offline-install-dialog.png"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                Application.runInBackground = background;
                Screen.sleepTimeout = sleep;
            }
        }

        private static string[] LoadedBundles() => AssetBundle.GetAllLoadedAssetBundles().Select(b => b.name).OrderBy(n => n).ToArray();
        private static void Validate(GameObject view)
        {
            Require(view.GetComponentsInChildren<MonoBehaviour>(true).All(c => c != null), "Missing frontend script");
            foreach (var graphic in view.GetComponentsInChildren<Graphic>())
            {
                string shaderPath = AssetDatabase.GetAssetPath(graphic.material?.shader);
                Require(string.IsNullOrEmpty(shaderPath) || !File.Exists(shaderPath) ||
                    !File.ReadAllText(shaderPath).Contains("DummyShaderTextExporter"), "Placeholder shader: " + graphic.name);
                Require(graphic.material != null && graphic.material.shader != null && graphic.material.shader.isSupported,
                    "Unsupported graphic shader: " + graphic.name);
            }
        }
        private static void Click(Button button)
        {
            Require(button != null && button.IsActive() && button.IsInteractable(), "Button unavailable");
            Canvas.ForceUpdateCanvases();
            var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
            var eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var point = RectTransformUtility.WorldToScreenPoint(eventCamera, button.GetComponent<RectTransform>().TransformPoint(button.GetComponent<RectTransform>().rect.center));
            var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
                "Original button " + button.name + " blocked by " + (hits.Count == 0 ? "no target" : hits[0].gameObject.name) + " point=" + point + " screen=" + Screen.width + "x" + Screen.height + " canvas=" + canvas.name + "/" + canvas.renderMode);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        }
        private static void Capture(string name)
        {
            var path = Path.Combine(Path.GetDirectoryName(Report), name);
            var theatre = UnityEngine.Object.FindObjectOfType<HomeTheatreRuntime>();
            if (theatre != null && theatre.IsReady) { CaptureTheatre(path); return; }
            Require(RunLocalMusicSelectionPlayMode.CaptureSelection(path) > 100000 && File.Exists(path), "Frontend capture is empty or missing");
        }
        private static void CaptureTheatre(string path)
        {
            var canvas = GameObject.Find("MainCanvas").GetComponent<Canvas>();
            var camera = GameObject.Find("UICamera").GetComponent<Camera>();
            var overlay = new GameObject("CaptureOverlayCamera").AddComponent<Camera>();
            overlay.enabled = false;
            overlay.orthographic = true;
            overlay.orthographicSize = 5;
            overlay.nearClipPlane = 0.1f; overlay.farClipPlane = 1000;
            overlay.cullingMask = 1 << 27;
            var overlayData = overlay.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            overlayData.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Base;
            overlay.clearFlags = CameraClearFlags.Nothing;
            overlay.allowMSAA = false;
            var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas).ToArray();
            var nodes = canvases.SelectMany(c => c.GetComponentsInChildren<Transform>(true)).Distinct().ToArray();
            var layers = nodes.Select(t => t.gameObject.layer).ToArray();
            var modes = canvases.Select(c => c.renderMode).ToArray();
            var cameras = canvases.Select(c => c.worldCamera).ToArray();
            var distances = canvases.Select(c => c.planeDistance).ToArray();
            var mask = camera.cullingMask; var aspect = camera.aspect;
            var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            var target = new RenderTexture(1920, 1200, 24);
            var image = new Texture2D(1920, 1200, TextureFormat.RGB24, false);
            try
            {
                foreach (var node in nodes) node.gameObject.layer = 27;
                camera.cullingMask &= ~(1 << 27);
                camera.targetTexture = target; camera.aspect = 1.6f;
                overlay.targetTexture = target; overlay.aspect = 1.6f;
                foreach (var root in canvases)
                { root.renderMode = RenderMode.ScreenSpaceCamera; root.worldCamera = overlay; root.planeDistance = 500; }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1920, 1200), 0, 0); image.Apply();
                var worldPixels = image.GetPixels32();
                // Desktop capture only: URP Nothing + non-MSAA clears depth, retaining the world color.
                overlay.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1920, 1200), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                int uiPixels = image.GetPixels32().Where((pixel, i) => !pixel.Equals(worldPixels[i])).Count();
                if (!UnityEngine.Object.FindObjectOfType<FrontendRecoveryRuntime>().IsFullScreen)
                    Require(uiPixels > 10000, "Home UI was not rendered over the theatre: pixels=" + uiPixels);
                Require(image.GetPixels32().Count(p => p.r > 220 && p.b > 220 && p.g < 30) < 500, "Theatre contains magenta pixels");
            }
            finally
            {
                overlay.targetTexture = null;
                for (int i = 0; i < canvases.Length; i++)
                { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i]; }
                for (int i = 0; i < nodes.Length; i++) nodes[i].gameObject.layer = layers[i];
                camera.cullingMask = mask; camera.targetTexture = previousTarget; camera.aspect = aspect;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(overlay.gameObject);
                Canvas.ForceUpdateCanvases();
            }
        }
        private static void Require(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); }
        private static void Finish(string error)
        {
            File.WriteAllText(Report, JsonConvert.SerializeObject(new { passed = error == null, error, phase = _phase,
                titleBundles = _titleBundles, homeBundles = _homeBundles, utc = DateTime.UtcNow,
                titleLoadingCoverValidated = SessionState.GetBool(Key + "homeCover", false),
                actorAnimationAdvanced = _actorAnimationAdvanced, homeSuspendedDuringSelection = _sceneLifetimeValidated, homeReturnSeconds = _homeReturnSeconds, homeInstancesReused = _homeReturnSeconds.Count == 4,
                startupNoticeValidated = _startupNoticeValidated,
                fullScreenAndHomeMenuValidated = _homeUiPhase == 6, selectionBackButtonValidated = _selectionBackValidated, directAnotherEntrySeconds = _directEntrySeconds, sharedMainSceneValidated = _selectionBackValidated,
                scope = "Original UI, repeated Title/Home navigation and entry to selection; title artwork uses frozen detail 1001; Home uses original additive Theatre, character 101/default costume 11 and footer; Title/Home original CRI playback, Home headers, regular background, original selection BackButton and Home archive/menu return tested; Other character preferences, awakening, account actions and online login excluded." }, Formatting.Indented));
            Debug.Log("OPENWDS_FRONTEND " + (error ?? "passed"));
            SessionState.SetBool(Key + "passed", error == null);
            SessionState.SetBool(Key + "exit", true);
            EditorApplication.isPlaying = false;
        }
    }
}
