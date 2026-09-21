using System;
using System.Collections;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Offline equivalent of Sirius.TransitionFades.CurtainTransitionFade.
    /// The retail fade covers the current scene with "close", changes scenes,
    /// then reveals the destination with "open".
    /// </summary>
    public sealed class CurtainTransitionRuntime : MonoBehaviour
    {
        private SkeletonGraphic _skeleton;
        private string _destination;
        private bool _animationComplete;
        private bool _transitionFailed;

        public static bool IsTransitioning { get; private set; }
        public static string CurrentAnimation { get; private set; }
        public static bool ClosePlayed { get; private set; }
        public static bool OpenPlayed { get; private set; }
        public static bool CloseCompleted { get; private set; }
        public static bool OpenCompleted { get; private set; }
        public static bool DestinationInitializedBeforeOpen { get; private set; }
        public static bool OpenRenderedInDestination { get; private set; }

        public static bool Begin(
            SkeletonDataAsset skeletonData,
            Material graphicMaterial,
            string destination)
        {
            if (IsTransitioning || skeletonData == null ||
                graphicMaterial == null || string.IsNullOrEmpty(destination))
                return false;

            var navigator = SceneNavigationRuntime.EnsureExists();
            navigator.CurtainData = skeletonData; navigator.CurtainMaterial = graphicMaterial;
            var root = new GameObject(
                "CurtainTransition",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CurtainTransitionRuntime));
            DontDestroyOnLoad(root);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Original level0/TransitionFadeCanvas (CanvasScaler pathID 555).
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var runtime = root.GetComponent<CurtainTransitionRuntime>();
            runtime._destination = destination;
            runtime._skeleton = SkeletonGraphic.NewSkeletonGraphicGameObject(
                skeletonData, root.transform, graphicMaterial);
            runtime._skeleton.name = "Curtain";
            runtime._skeleton.raycastTarget = true;
            runtime._skeleton.UnscaledTime = true;
            runtime._skeleton.Initialize(true);
            // Retail SkeletonGraphic has no bounds-based layout. Awake replaces
            // the parent scale with ScreenHelper.GetFitScaleForAspectRatio;
            // its child retains the authored 1.1 scale (level0 RectTransform 465).
            runtime._skeleton.layoutScaleMode = SkeletonGraphic.LayoutMode.None;
            var rect = runtime._skeleton.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var fitScale = GetAuthoredFitScale(Screen.width, Screen.height);
            rect.localScale = new Vector3(1.1f * fitScale, 1.1f * fitScale, 0f);
            IsTransitioning = true;
            ClosePlayed = false;
            OpenPlayed = false;
            CloseCompleted = false;
            OpenCompleted = false;
            DestinationInitializedBeforeOpen = false;
            OpenRenderedInDestination = false;
            runtime.StartCoroutine(runtime.Run());
            return true;
        }

        public static float GetAuthoredFitScale(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException("Screen dimensions must be positive.");
            // 2.31.2 ScreenHelper.GetFitScaleForAspectRatio, VA 0xB4BFB34.
            return width / 1.6f <= height
                ? ((float)height / width) / 0.5625f + 0.03f
                : ((float)width / height) / 1.7777778f + 0.03f;
        }

        private IEnumerator Run()
        {
            yield return Play("close");
            if (_transitionFailed)
            {
                FinishFailedTransition();
                yield break;
            }
            yield return SceneNavigationRuntime.EnsureExists().ReplaceScenes(_destination);
            // GlobalNavigator keeps the curtain closed until the destination
            // presenter has completed its own initialization. Starting "open"
            // on the first scene-loaded frame makes the reveal run behind the
            // gameplay bootstrap and appear to be skipped.
            GameRuntime gameRuntime = null;
            var initializationTimeout = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < initializationTimeout)
            {
                if (SceneManager.GetActiveScene().name == _destination)
                {
                    gameRuntime = FindObjectOfType<GameRuntime>();
                    if (gameRuntime != null && gameRuntime.IsInitialized)
                        break;
                }
                yield return null;
            }
            if (gameRuntime == null || !gameRuntime.IsInitialized)
            {
                Debug.LogError(
                    "OPENWDS_CURTAIN_DESTINATION_INIT_FAILED scene=" +
                    _destination);
                FinishFailedTransition();
                yield break;
            }
            DestinationInitializedBeforeOpen = true;
            Canvas.ForceUpdateCanvases();
            // WaitForEndOfFrame is not pumped by Unity batchmode. Two normal
            // frames preserve the destination-presented boundary in both the
            // player and the automated Editor gate.
            yield return null;
            yield return null;
            yield return Play("open");
            if (_transitionFailed)
            {
                FinishFailedTransition();
                yield break;
            }
            IsTransitioning = false;
            CurrentAnimation = null;
            Destroy(gameObject);
        }

        private IEnumerator Play(string animationName)
        {
            CurrentAnimation = animationName;
            if (animationName == "close") ClosePlayed = true;
            if (animationName == "open") OpenPlayed = true;
            _animationComplete = false;
            TrackEntry entry = _skeleton.AnimationState.SetAnimation(
                0, animationName, false);
            entry.Complete += OnAnimationComplete;
            if (animationName == "open")
            {
                // Guarantee that the destination presents at least one rendered
                // frame while the open animation is active.
                yield return null;
                OpenRenderedInDestination =
                    SceneManager.GetActiveScene().name == _destination;
            }
            var timeout = Time.realtimeSinceStartup + 8f;
            while (!_animationComplete && Time.realtimeSinceStartup < timeout)
                yield return null;
            entry.Complete -= OnAnimationComplete;
            if (!_animationComplete)
            {
                _transitionFailed = true;
                Debug.LogError(
                    "OPENWDS_CURTAIN_ANIMATION_FAILED animation=" +
                    animationName);
                yield break;
            }
            if (animationName == "close") CloseCompleted = true;
            if (animationName == "open") OpenCompleted = true;
        }

        private void OnAnimationComplete(TrackEntry entry)
        {
            _animationComplete = true;
        }

        private void FinishFailedTransition()
        {
            IsTransitioning = false;
            CurrentAnimation = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (IsTransitioning && gameObject.scene.IsValid())
            {
                IsTransitioning = false;
                CurrentAnimation = null;
            }
        }
    }
}
