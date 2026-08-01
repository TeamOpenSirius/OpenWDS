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
    public sealed class RecoveredCurtainTransitionRuntime : MonoBehaviour
    {
        private SkeletonGraphic _skeleton;
        private string _destination;
        private bool _animationComplete;

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

            var root = new GameObject(
                "RecoveredCurtainTransition",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(RecoveredCurtainTransitionRuntime));
            DontDestroyOnLoad(root);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1200f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var runtime = root.GetComponent<RecoveredCurtainTransitionRuntime>();
            runtime._destination = destination;
            runtime._skeleton = SkeletonGraphic.NewSkeletonGraphicGameObject(
                skeletonData, root.transform, graphicMaterial);
            runtime._skeleton.name = "Curtain";
            runtime._skeleton.raycastTarget = true;
            runtime._skeleton.UnscaledTime = true;
            runtime._skeleton.Initialize(true);
            // NewSkeletonGraphicGameObject starts from an arbitrary 100x100
            // RectTransform. Capture the authored Spine bounds first, then use
            // the package's envelope mode so every destination aspect ratio is
            // covered vertically and horizontally during the transition.
            if (runtime._skeleton.MatchRectTransformWithBounds())
            {
                runtime._skeleton.layoutScaleMode =
                    SkeletonGraphic.LayoutMode.EnvelopeParent;
            }
            var rect = runtime._skeleton.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
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

        private IEnumerator Run()
        {
            yield return Play("close");
            SceneManager.LoadScene(_destination);
            // GlobalNavigator keeps the curtain closed until the destination
            // presenter has completed its own initialization. Starting "open"
            // on the first scene-loaded frame makes the reveal run behind the
            // gameplay bootstrap and appear to be skipped.
            RecoveredGameRuntime gameRuntime = null;
            var initializationTimeout = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < initializationTimeout)
            {
                if (SceneManager.GetActiveScene().name == _destination)
                {
                    gameRuntime = FindObjectOfType<RecoveredGameRuntime>();
                    if (gameRuntime != null && gameRuntime.IsInitialized)
                        break;
                }
                yield return null;
            }
            if (gameRuntime == null || !gameRuntime.IsInitialized)
                throw new System.TimeoutException(
                    "Destination gameplay did not initialize before curtain open.");
            DestinationInitializedBeforeOpen = true;
            Canvas.ForceUpdateCanvases();
            // WaitForEndOfFrame is not pumped by Unity batchmode. Two normal
            // frames preserve the destination-presented boundary in both the
            // player and the automated Editor gate.
            yield return null;
            yield return null;
            yield return Play("open");
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
                throw new System.TimeoutException(
                    $"Curtain animation did not complete: {animationName}");
            if (animationName == "close") CloseCompleted = true;
            if (animationName == "open") OpenCompleted = true;
        }

        private void OnAnimationComplete(TrackEntry entry)
        {
            _animationComplete = true;
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
