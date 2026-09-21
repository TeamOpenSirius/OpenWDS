using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

namespace OpenWDS.Runtime
{
    // Local scene names map to the original Main/Game/GameResult boundaries.
    // Launcher remains loaded so Unity can unload the sole feature scene first.
    public sealed class SceneNavigationRuntime : MonoBehaviour
    {
        public const string MainScene = "FrontendRecovery";
        public const string GameScene = "OfflineRhythmPreview";
        public const string ResultScene = "GameResult";
        public static SceneNavigationRuntime Instance { get; private set; }
        private static bool _loading;
        private bool _coveredTransition;
        public static bool IsLoading => _loading || (Instance != null && Instance._coveredTransition);
        public static int TransitionType { get; private set; }
        public static int CompletedTransitions { get; private set; }
        public static string LastUnloadedScene { get; private set; }
        public static bool SourceUnloadedBeforeLoad { get; private set; }
        public static GameResultSceneData Result { get; set; }
        private Scene _launcher;
        // Explicit Unity references protect the small cross-scene payload during UnloadUnusedAssets.
        public TextAsset Chart, MusicConfig;
        public Sprite Jacket;
        public GameObject ResultPrefab, ResultBackground;
        public Spine.Unity.SkeletonDataAsset CurtainData;
        public Material CurtainMaterial;

        public static SceneNavigationRuntime EnsureExists()
        {
            if (Instance != null) return Instance;
            var host = new GameObject("GlobalSceneNavigation");
            DontDestroyOnLoad(host);
            return host.AddComponent<SceneNavigationRuntime>();
        }
        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) { Instance = null; _loading = false; Result = null; } }

        public void PinSession()
        {
            Chart = LocalMusicSelectionSession.ChartAsset;
            MusicConfig = LocalMusicSelectionSession.MusicConfigAsset;
            Jacket = LocalMusicSelectionSession.JacketSprite;
        }
        public bool ReplayGame()
        {
            if (CurtainData == null || CurtainMaterial == null)
            {
                var assets = Resources.Load<SceneTransitionAssets>("ScriptableObject/SceneTransitionAssets");
                if (assets == null) throw new InvalidOperationException("Generate scene transition assets first.");
                CurtainData = assets.Curtain; CurtainMaterial = assets.CurtainMaterial;
            }
            TransitionType = 3;
            return CurtainTransitionRuntime.Begin(CurtainData, CurtainMaterial, GameScene);
        }
        public void ClearResult()
        {
            Result = null; ResultPrefab = null; ResultBackground = null;
        }
        public bool Navigate(string destination, int transitionType = 1)
        {
            if (IsLoading || CurtainTransitionRuntime.IsTransitioning) return false;
            TransitionType = transitionType;
            _coveredTransition = true;
            StartCoroutine(NavigateCovered(destination));
            return true;
        }
        private IEnumerator NavigateCovered(string destination)
        {
            var cover = new GameObject("SceneTransitionCover", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            cover.transform.SetParent(transform, false);
            var canvas = cover.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 31999;
            var graphic = new GameObject("Cover", typeof(RectTransform), typeof(Image));
            graphic.transform.SetParent(cover.transform, false);
            var rect = (RectTransform)graphic.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            graphic.GetComponent<Image>().color = Color.black;
            var alpha = cover.AddComponent<CanvasGroup>();
            alpha.alpha = 0;
            yield return alpha.DOFade(1, .2f).SetUpdate(true).WaitForCompletion();
            yield return ReplaceScenes(destination);
            yield return alpha.DOFade(0, .15f).SetUpdate(true).WaitForCompletion();
            Destroy(cover);
            _coveredTransition = false;
        }
        public IEnumerator ReplaceScenes(string destination)
        {
            if (_loading) throw new InvalidOperationException("Overlapping scene navigation.");
            _loading = true;
            PinSession();
            if (!_launcher.IsValid() || !_launcher.isLoaded)
            {
                _launcher = SceneManager.GetSceneByName("Launcher");
                if (!_launcher.IsValid()) _launcher = SceneManager.CreateScene("Launcher");
                var cri = FindObjectOfType<CriWare.CriWareInitializer>();
                if (cri != null) SceneManager.MoveGameObjectToScene(cri.transform.root.gameObject, _launcher);
            }
            var source = SceneManager.GetActiveScene();
            LastUnloadedScene = source.name;
            SceneManager.SetActiveScene(_launcher);
            var outgoing = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != _launcher) outgoing.Add(scene);
            }
            var operations = new List<AsyncOperation>();
            foreach (var scene in outgoing)
            {
                var operation = SceneManager.UnloadSceneAsync(scene);
                if (operation != null) operations.Add(operation);
            }
            foreach (var operation in operations) yield return operation;
            SourceUnloadedBeforeLoad = !source.IsValid() || !source.isLoaded;
            if (!SourceUnloadedBeforeLoad) throw new InvalidOperationException("Source scene survived navigation.");
            yield return Resources.UnloadUnusedAssets();
            var load = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Additive);
            if (load == null) throw new InvalidOperationException("Cannot load scene: " + destination);
            yield return load;
            var target = SceneManager.GetSceneByName(destination);
            if (!target.IsValid() || !target.isLoaded || !SceneManager.SetActiveScene(target))
                throw new InvalidOperationException("Cannot activate scene: " + destination);
            var timeout = Time.realtimeSinceStartup + 60f;
            while (!DestinationReady(destination))
            {
                if (Time.realtimeSinceStartup > timeout) throw new TimeoutException("Scene initialization: " + destination);
                yield return null;
            }
            yield return null;
            CompletedTransitions++;
            _loading = false;
            Debug.Log($"OPENWDS_SCENE_NAVIGATION from={LastUnloadedScene} to={destination} sourceUnloaded={SourceUnloadedBeforeLoad}");
        }
        private static bool DestinationReady(string destination)
        {
            if (destination == GameScene)
            {
                var game = FindObjectOfType<GameRuntime>();
                return game != null && game.IsInitialized && (game.CharacterPresentation == null || game.CharacterPresentation.IsReady);
            }
            if (destination == ResultScene) return GameResultSceneRuntime.Instance != null && GameResultSceneRuntime.Instance.IsInitialized;
            if (destination == MainScene)
            {
                var selection = FindObjectOfType<LocalMusicSelectionRuntime>();
                if (selection != null) return selection.IsInitialized && !MainPageNavigationRuntime.Instance.IsTransitioning;
                var home = FindObjectOfType<FrontendRecoveryRuntime>();
                return home != null && home.IsReady;
            }
            throw new InvalidOperationException("No initializer registered for scene: " + destination);
        }
        public static bool ReturnToMain(bool fromResult)
        {
            var selection = LocalMusicSelectionSession.Selection;
            FrontendNavigation.PrepareSelectionReturn(selection, fromResult);
            LocalMusicSelectionSession.Clear();
            var navigator = EnsureExists();
            navigator.PinSession(); navigator.ClearResult();
            return navigator.Navigate(MainScene, fromResult ? 4 : 1);
        }
    }
}
