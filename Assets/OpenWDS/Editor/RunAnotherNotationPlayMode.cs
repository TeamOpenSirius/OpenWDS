using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using OpenWDS.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenWDS.Editor
{
    [InitializeOnLoad]
    public static class RunAnotherNotationPlayMode
    {
        private const string Key = "OpenWDS.AnotherGate.";
        private static int _phase, _filterPhase, _scrollPhase, _dragFrames;
        private static PointerEventData _drag;
        private static float _maxGapError;
        private static double _resultObservedAt;
        private static bool _stageObserved, _completedNotes;
        private static int _initialSelectionId;
        private static int _zeroHudSamples;
        private static bool Direct => SessionState.GetBool(Key + "direct", false);
        private static bool UiOnly => SessionState.GetBool(Key + "uiOnly", false);
        private static double _next;
        private static string _error;
        private static string Report => Path.GetFullPath(Path.Combine(Application.dataPath, UiOnly ? "../../../reverse/reports/another-notation-ui.json" : "../../../reverse/reports/another-notation-playmode.json"));
        static RunAnotherNotationPlayMode() { EditorApplication.update += Poll; Application.logMessageReceived += Log; }
        public static void Run()
        {
            SessionState.SetBool(Key + "uiOnly", Environment.GetCommandLineArgs().Contains("-openwdsAnotherUiOnly"));
            SessionState.SetBool(Key + "direct", Environment.GetCommandLineArgs().Contains("-openwdsAnotherDirect"));
            ValidateStoreIsolation();
            var files = new[] { "OpenWDSLocalResults", "OpenWDSAnotherNotationResults" }
                .Select(n => Path.Combine(Application.persistentDataPath, SettingsPersistence.CurrentDirectory, n))
                .ToDictionary(p => p, p => File.Exists(p) ? File.ReadAllBytes(p) : null);
            SessionState.SetString(Key + "resultsBefore", JsonConvert.SerializeObject(files));
            SessionState.SetBool(Key + "active", true);
            SessionState.SetBool(Key + "exit", false);
            SessionState.SetString(Key + "start", DateTime.UtcNow.ToString("O"));
            if (Direct) CreateFrontendRecoveryScene.Run();
            else EditorSceneManager.OpenScene("Assets/OpenWDS/Scenes/LocalMusicSelection.unity");
            EditorApplication.isPlaying = true;
        }
        private static void Log(string message, string stack, LogType type)
        {
            if (SessionState.GetBool(Key + "active", false) &&
                (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || message.Contains("referenced script") && message.Contains("missing")))
                _error = _error ?? message + "\n" + stack;
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
            if ((DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Key + "start", ""))).TotalSeconds > 420) { Finish("Timed out"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (_error != null) { Finish(_error); return; }
            if (EditorApplication.timeSinceStartup < _next) return;
            try
            {
                var host = UnityEngine.Object.FindObjectOfType<LocalMusicSelectionRuntime>();
                var menu = UnityEngine.Object.FindObjectOfType<OfflineMenuRuntime>();
                var another = UnityEngine.Object.FindObjectOfType<AnotherNotationSelectionRuntime>();
                if (Direct && host == null && _phase == 0)
                {
                    var frontend = UnityEngine.Object.FindObjectOfType<FrontendRecoveryRuntime>();
                    if (frontend == null || !frontend.IsReady) return;
                    if (frontend.Page == "Title") { frontend.StartCoroutine(frontend.ShowHome()); return; }
                    if (frontend.Page == "Home") { Click(frontend.AnotherButton); _phase = 1; return; }
                }
                if (Direct && host != null)
                    Require(host.IsDirectAnotherEntry && host.StandardListInitializationCount == 0 && host.Selection == null,
                        "Direct archive route initialized ordinary list");
                if (_phase == 13)
                {
                    var frontend = UnityEngine.Object.FindObjectOfType<FrontendRecoveryRuntime>();
                    if (frontend == null || !frontend.IsReady || frontend.Page != "Home" || !frontend.Bgm.IsPlaying) return;
                    Require(another == null, "Direct archive remained after Home return");
                    Finish(null); return;
                }
                if (SceneNavigationRuntime.IsLoading || CurtainTransitionRuntime.IsTransitioning) return;
                if (MainPageNavigationRuntime.Instance != null && MainPageNavigationRuntime.Instance.IsTransitioning) return;
                var game = UnityEngine.Object.FindObjectOfType<GameRuntime>();
                var resultScene = GameResultSceneRuntime.Instance;
                if ((_phase <= 8 || _phase >= 12) && (host == null || !host.IsInitialized || menu == null || menu.IsTransitioning)) return;
                if (another != null && (!another.IsReady || another.IsPreparing)) return;
                if (_phase == 2 && _scrollPhase < 7)
                {
                    ValidateScrolling(another);
                    return;
                }
                if (_phase == 2 && _filterPhase < 6)
                {
                    ValidateFilter(another);
                    _next = EditorApplication.timeSinceStartup + 1;
                    return;
                }
                switch (_phase)
                {
                    case 0: host.OpenAnotherNotationSelection(); break;
                    case 1:
                        Require(another != null, "Special presenter did not open"); _initialSelectionId = host.GetInstanceID(); break;
                    case 2:
                        Require(another != null && another.EntryCount == 112, "Expected all 112 special charts");
                        Capture("another-notation-selection.png"); another.SelectId(14); break;
                    case 3:
                        Require(another.SelectedId == 14, "Wrong alternate vocal selection");
                        Require(another.View.GetComponent<MusicSelectionPreviewRuntime>().IsPlaying, "Alternate vocal preview is not playing");
                        Capture("another-notation-vocal.png"); another.SelectId(1); break;
                    case 4:
                        Require(another.SelectedId == 1, "Wrong return selection");
                        if (Direct) { _phase = 6; break; }
                        Click(GameObject.Find("AnotherNotationBack").GetComponent<Button>()); break;
                    case 5:
                        Require(another == null, "Special view did not close");
                        var cachedLayouts = (System.Collections.IDictionary)typeof(LocalMusicSelectionRuntime).GetField("_anotherCellLayouts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(host);
                        Require(cachedLayouts.Count == 0, "Closed special cells retained in shared focus cache");
                        Require(GameObject.Find("MusicSelectionView") != null, "Ordinary selection did not return");
                        host.OpenAnotherNotationSelection(); break;
                    case 6: Require(another != null, "Special presenter did not open"); break;
                    case 7:
                        Require(another != null && another.SelectedId == 1, "Second special entry failed");
                        Capture("another-notation-reopen.png");
                        var auto = another.View.transform.Find("AutoSettingToggle");
                        var labelColor = auto.GetComponentInChildren<Text>(true).color;
                        ValidateAuto(auto, false, labelColor);
                        Click(auto.GetComponent<Button>());
                        ValidateAuto(auto, true, labelColor);
                        Capture("another-notation-auto.png");
                        Click(auto.GetComponent<Button>());
                        ValidateAuto(auto, false, labelColor);
                        if (UiOnly) { Finish(null); return; }
                        Click(auto.GetComponent<Button>()); break;
                    case 8:
                        Click(another.View.GetComponentsInChildren<Button>().First(b => b.name == "OkButton")); break;
                    case 9:
                        var confirmation = GameObject.Find("NoteSpeedDialog");
                        if (confirmation != null)
                        {
                            Capture("another-notation-prelive.png");
                            Click(GameObject.Find("ConfirmButton").GetComponent<Button>());
                            _next = EditorApplication.timeSinceStartup + 1.5;
                            return;
                        }
                        if (resultScene != null && resultScene.IsInitialized)
                        {
                            Require(game == null && SceneNavigationRuntime.SourceUnloadedBeforeLoad, "Game survived result navigation");
                            Require(_completedNotes, "Unconsumed special notes");
                            Require(LocalMusicSelectionSession.Selection.Live.AnotherNotationId == 1 && LocalMusicSelectionSession.IsOfficialAuto,
                                "Lost special chart replay parameters");
                            _resultObservedAt = EditorApplication.timeSinceStartup; _phase++; return;
                        }
                        if (game == null || !game.IsInitialized) return;
                        Require(!UnityEngine.SceneManagement.SceneManager.GetSceneByName(SceneNavigationRuntime.MainScene).isLoaded, "Main remained loaded during Game");
                        var hud = game.GetComponent<GameHudRuntime>();
                        Require(hud.ScorePanel.gameObject.activeSelf && hud.PrincipalGauge.gameObject.activeSelf, "Special zero HUD hidden");
                        Require(hud.TotalScore == 0 && hud.ScorePanel.Score == 0 && hud.Score == null && hud.SenseScore == null && hud.StarActScore == null, "Special chart accumulated score");
                        Require(hud.PrincipalGauge.CurrentPrincipalCount.text == "0" && hud.PrincipalGauge.MaxPrincipalCount.text == "0", "Special principal is not 0/0");
                        _zeroHudSamples++;
                        _completedNotes |= game.InputHandler.IsGameCompleted && game.InputHandler.RemainingTapCount + game.InputHandler.RemainingFlickCount + game.InputHandler.RemainingHoldCount + game.InputHandler.RemainingScratchCount == 0;
                        return;

                    case 10:
                        var stage = GameObject.Find("GameResultStageSuccess");
                        Require(stage != null, "Shared StageSuccess was not activated");
                        if (!_stageObserved && stage.transform.Find("position").GetComponent<CanvasGroup>().alpha > .5f)
                        {
                            _stageObserved = true;
                            Capture("another-notation-stage-success.png");
                        }
                        if (EditorApplication.timeSinceStartup - _resultObservedAt < 8) return;
                        Require(_stageObserved, "StageSuccess never became visible");
                        Require(_zeroHudSamples > 100, "Insufficient zero-score gameplay samples");
                        Require(resultScene.CharacterPresentation != null && resultScene.CharacterPresentation.IsReady && resultScene.CharacterPresentation.ResultPlayCount == 1, "Shared result character did not play");
                        Require(GameObject.Find("CharacterParent") != null, "Result character is inactive");
                        Capture("another-notation-result.png");
                        resultScene.ReplayFromResult(); break;
                    case 11:
                        if (game == null || game.IsResultShown || game.InputHandler == null || game.IsRestartPending) return;
                        game.RetireGame(); break;
                    case 12:
                        Require(another != null && another.SelectedId == 1, "Special selection not rebuilt after result/replay/retire");
                        Require(host.GetInstanceID() != _initialSelectionId && SceneNavigationRuntime.CompletedTransitions >= 4, "Source presenter was retained");
                        Capture("another-notation-return.png");
                        Click(GameObject.Find("AnotherNotationBack").GetComponent<Button>()); break;
                    case 13:
                        Require(another == null && GameObject.Find("MusicSelectionView") != null, "Final ordinary selection return failed");
                        Finish(null); return;
                }
                _phase++; _next = EditorApplication.timeSinceStartup + 1.5;
            }
            catch (Exception e) { Finish(e.ToString()); }
        }
        private static void ValidateScrolling(AnotherNotationSelectionRuntime another)
        {
            var scroller = another.View.GetComponentInChildren<EnhancedUI.EnhancedScroller.EnhancedScroller>();
            switch (_scrollPhase)
            {
                case 0:
                    ValidatePresentation(another);
                    var reads = another.SelectionPreparationCount;
                    for (var i = 0; i < 100; i++) another.SelectIndex(another.VisibleEntries.ToList().FindIndex(e => e.Id == another.SelectedId), false);
                    Require(another.SelectionPreparationCount == reads && another.ChartReadCount == 0, "Repeated snap reloaded selected music/chart");
                    var rect = (RectTransform)scroller.transform;
                    _drag = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
                    _drag.pressPosition = _drag.position;
                    ExecuteEvents.Execute(scroller.gameObject, _drag, ExecuteEvents.initializePotentialDrag);
                    ExecuteEvents.Execute(scroller.gameObject, _drag, ExecuteEvents.beginDragHandler);
                    Require(!scroller.snapping, "Snap was left enabled during drag");
                    _scrollPhase++; _next = EditorApplication.timeSinceStartup + .03; return;
                case 1:
                    _drag.delta = new Vector2(0, 35); _drag.position += _drag.delta;
                    ExecuteEvents.Execute(scroller.gameObject, _drag, ExecuteEvents.dragHandler);
                    Require(!scroller.snapping, "Scroller snapped before pointer release");
                    if (++_dragFrames < 12) { _next = EditorApplication.timeSinceStartup + .03; return; }
                    ExecuteEvents.Execute(scroller.gameObject, _drag, ExecuteEvents.endDragHandler);
                    Require(scroller.snapping, "Snap not restored at drag end");
                    _scrollPhase++; _next = EditorApplication.timeSinceStartup + 3; return;
                case 2:
                    Require(another.SelectedId != 1, "Drag did not select another chart");
                    ValidatePresentation(another);
                    Capture("another-notation-scrolled.png");
                    another.SelectId(88); _scrollPhase++; _next = EditorApplication.timeSinceStartup + 2; return;
                case 3:
                    ValidatePresentation(another);
                    Capture("another-notation-reference.png");
                    another.SelectId(1); _scrollPhase++; _next = EditorApplication.timeSinceStartup + 1; return;
                case 4:
                    // Model integration: the frozen catalog has no NORMAL MV.
                    // Exercise empty -> restored selection independently of UI scrolling.
                    var empty = (AnotherNotationFilters)typeof(AnotherNotationSelectionRuntime).GetField("_filter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(another);
                    empty.Difficulty.Add(0); empty.MusicVideo.Add(0); empty.Applied();
                    _scrollPhase++; _next = EditorApplication.timeSinceStartup + 1; return;
                case 5:
                    Require(another.EntryCount == 0 && !another.View.GetComponent<MusicSelectionPreviewRuntime>().IsPlaying, "Empty filter did not stop preview");
                    var reset = (AnotherNotationFilters)typeof(AnotherNotationSelectionRuntime).GetField("_filter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(another);
                    reset.Difficulty.Clear(); reset.MusicVideo.Clear(); reset.Applied();
                    _scrollPhase++; _next = EditorApplication.timeSinceStartup + 1; return;
                case 6:
                    Require(another.EntryCount == 112 && another.View.GetComponent<MusicSelectionPreviewRuntime>().IsPlaying, "Preview did not resume after empty filter reset");
                    another.SelectId(1); _scrollPhase++; _next = EditorApplication.timeSinceStartup + 1; return;
            }
        }
        private static void ValidatePresentation(AnotherNotationSelectionRuntime another)
        {
            var scroller = another.View.GetComponentInChildren<EnhancedUI.EnhancedScroller.EnhancedScroller>();
            var cells = scroller.GetComponentsInChildren<EnhancedUI.EnhancedScroller.EnhancedScrollerCellView>()
                .OrderBy(c => c.cellIndex).ToArray();
            Require(cells.Length >= 3, "Too few visible cells");
            var bounds = cells.Select(c =>
            {
                var rect = (RectTransform)c.transform.Find("PartsBase");
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                return corners.Select(v => scroller.transform.InverseTransformPoint(v).y).ToArray();
            }).ToArray();
            for (var i = 1; i < bounds.Length; i++)
            {
                if (cells[i].cellIndex != cells[i-1].cellIndex + 1) continue;
                var gap = bounds[i-1].Min() - bounds[i].Max();
                _maxGapError = Mathf.Max(_maxGapError, Mathf.Abs(gap - 20));
                Require(Mathf.Abs(gap - 20) < .2f, "Nonuniform cell gap: " + gap);
            }
            foreach (var cell in cells)
            {
                var focused = ((RectTransform)cell.transform.Find("PartsBase")).sizeDelta.y > 180;
                var badge = (RectTransform)cell.transform.Find("PartsBase/PartsMask/NotationTypeBadge");
                Require(Mathf.Abs(badge.anchoredPosition.y - (focused ? -136 : -116)) < .01f, "Notation badge is not vertically centered");
            }
            var background = another.View.transform.Find("JugonBackground");
            Require(background != null && background.gameObject.activeInHierarchy && background.GetSiblingIndex() == 0, "Independent Jugon background missing/above content");
            var backgroundImage = background.Find("Image").GetComponent<Image>();
            Require(backgroundImage.sprite != null && backgroundImage.material.shader.isSupported && backgroundImage.material.shader.name == "UI/CommonBackGround", "Jugon background material is not restored");
            var menu = GameObject.Find("MenuView");
            Require(menu.transform.GetSiblingIndex() > another.View.transform.GetSiblingIndex(), "Menu is behind TicketMachine");
            var panel = another.View.transform.Find("TicketMacine/MusicInformationPanel");
            Require(Find(panel, "Stamp").GetComponent<Image>().isActiveAndEnabled, "Special stamp hidden");
            Require(panel.GetComponentsInChildren<ScrollTextRuntime>().Length >= 2, "Information scrolling text missing");
            foreach (var particle in panel.GetComponentsInChildren<Coffee.UIExtensions.UIParticle>(true))
            {
                Require(particle.autoScalingMode == Coffee.UIExtensions.UIParticle.AutoScalingMode.None && (particle.transform.localScale-Vector3.one).sqrMagnitude < .0001f, "Coffee 4.x scaling regression");
                Require(particle.isActiveAndEnabled, "Coffee Canvas renderer disabled");
            }
            Require(another.ChartReadCount == 0, "Selection eagerly reads notation payloads");
        }

        private static void ValidateAuto(Transform auto, bool selected, Color labelColor)
        {
            Require(auto.Find("Image").gameObject.activeSelf, "Auto grey circle disappeared");
            Require(auto.Find("Image/Image").gameObject.activeSelf == selected, "Auto red circle state is wrong");
            Require(auto.GetComponentInChildren<Text>(true).color == labelColor, "Auto label changed color");
        }

        private static void ValidateStoreIsolation()
        {
            var path = Path.Combine(Application.temporaryCachePath, "AnotherNotationStoreGate-" + Guid.NewGuid());
            try
            {
                new LocalResultStore(path).RecordResult(1, MusicDifficulty.Normal, 98, ClearLamp.FullCombo, out _);
                new LocalResultStore(path, true).RecordResult(1, MusicDifficulty.Normal, 76, ClearLamp.Clear, out _);
                Require(new LocalResultStore(path).GetBest(1, MusicDifficulty.Normal) == 98, "Special chart overwrote ordinary score");
                Require(new LocalResultStore(path, true).GetBest(1, MusicDifficulty.Normal) == 76, "Special score identity did not persist");
            }
            finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
        }
        private static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
        private static void ValidateFilter(AnotherNotationSelectionRuntime another)
        {
            Require(another != null, "Special filter host missing");
            var dialog = GameObject.Find("MusicSortFilterDialog");
            switch (_filterPhase)
            {
                case 0:
                    Click(Find(GameObject.Find("AnotherNotationHeader").transform, "FilterSettingButton").GetComponent<Button>()); break;
                case 1:
                    Require(dialog != null, "Original special filter did not open");
                    Require(!Find(another.View.transform, "UIParticleSpectrumViewer").gameObject.activeSelf, "Spectrum overlays filter");
                    var group = Find(Find(dialog.transform, "DifficultyFilterPanel"), "FilterCheckBoxGroup");
                    var option = Enumerable.Range(0, group.childCount).Select(i => group.GetChild(i)).First(t => t.name.StartsWith("FilterSideStoryButton"));
                    Click(option.GetComponent<Button>());
                    Capture("another-notation-filter.png");
                    Click(GameObject.Find("ConfirmButton").GetComponent<Button>()); break;
                case 2:
                    Require(another.EntryCount > 0 && another.EntryCount < 112 && another.VisibleEntries.All(e => e.Difficulty == 1), "Special difficulty filter failed");
                    Click(Find(GameObject.Find("AnotherNotationHeader").transform, "SortButton").GetComponent<Button>()); break;
                case 3:
                    Click(Find(dialog.transform, "NameSortToggleButton").GetComponent<Button>());
                    Capture("another-notation-sort.png");
                    Click(GameObject.Find("ConfirmButton").GetComponent<Button>()); break;
                case 4:
                    Require(another.VisibleEntries.Zip(another.VisibleEntries.Skip(1), (a, b) => StringComparer.CurrentCulture.Compare(a.Music.PronounceName, b.Music.PronounceName) <= 0).All(x => x), "Special name ordering failed");
                    Click(Find(GameObject.Find("AnotherNotationHeader").transform, "FilterSettingButton").GetComponent<Button>()); break;
                case 5:
                    var filterPanel = Find(dialog.transform, "FilterPanel");
                    Click(Find(filterPanel, "ResetButton").GetComponent<Button>());
                    Click(GameObject.Find("ConfirmButton").GetComponent<Button>());
                    Require(another.EntryCount == 112, "Special filter reset lost entries");
                    another.SelectId(1); break;
            }
            _filterPhase++;
        }

        private static void Capture(string name)
        {
            var path = Path.Combine(Path.GetDirectoryName(Report), name);
            var game = UnityEngine.Object.FindObjectOfType<GameRuntime>();
            if (GameResultSceneRuntime.Instance != null && GameResultSceneRuntime.Instance.IsInitialized) CaptureResult(GameResultSceneRuntime.Instance, path);
            else Require(RunLocalMusicSelectionPlayMode.CaptureSelection(path) > 100000, "Empty screenshot");
        }
        private static void CaptureResult(GameResultSceneRuntime game, string path)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var _background = game.Background;
            var _root = game.View;
            var camera = _background.GetComponentInChildren<Camera>();
            var canvas = _root.GetComponentInParent<Canvas>();
            var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera;
            var oldDistance = canvas.planeDistance;
            var oldTarget = camera.targetTexture;
            var oldAspect = camera.aspect;
            var oldActive = RenderTexture.active;
            var target = new RenderTexture(1014, 633, 24);
            var texture = new Texture2D(1014, 633, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.aspect = 1014f / 633f;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1014, 633), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCamera;
                canvas.planeDistance = oldDistance;
                camera.targetTexture = oldTarget;
                camera.aspect = oldAspect;
                RenderTexture.active = oldActive;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(target);
                Canvas.ForceUpdateCanvases();
            }
        }

        private static void Click(Button button)
        {
            Require(button != null && button.IsActive() && button.IsInteractable(), "Inactive button");
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var data = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
                "Button blocked: " + button.name + " first=" + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Finish(string error)
        {
            var before = JsonConvert.DeserializeObject<Dictionary<string, byte[]>>(SessionState.GetString(Key + "resultsBefore", "{}"));
            var resultsUnchanged = before.All(pair => pair.Value == null ? !File.Exists(pair.Key) : File.Exists(pair.Key) && File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value));
            if (!resultsUnchanged && error == null) error = "Auto or selection wrote a score file";
            File.WriteAllText(Report, JsonConvert.SerializeObject(new { passed = error == null, directFromHome = Direct, sceneTransitions = SceneNavigationRuntime.CompletedTransitions, phase = _phase, filterPhase = _filterPhase, scrollPhase = _scrollPhase, maxGapError = _maxGapError, uiOnly = UiOnly, stageObserved = _stageObserved, zeroHudSamples = _zeroHudSamples, resultsUnchanged, storeIsolationValidated = true, error }, Formatting.Indented));
            SessionState.SetBool(Key + "passed", error == null);
            SessionState.SetBool(Key + "exit", true);
            EditorApplication.isPlaying = false;
        }
    }
}
