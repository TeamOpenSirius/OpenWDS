using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace OpenWDS.Runtime
{
    /// <summary>Offline Main presenter boundary: one camera, event system and CRI host for Home and selection.</summary>
    public sealed class MainPageNavigationRuntime : MonoBehaviour
    {
        [SerializeField] private FrontendRecoveryRuntime _frontend;
        [SerializeField] private GameObject _selectionPagePrefab;
        private GameObject _selectionPage;
        private bool _transitioning;
        public static MainPageNavigationRuntime Instance { get; private set; }
        public bool IsTransitioning => _transitioning;
        public double LastTransitionSeconds { get; private set; }

        public void Configure(FrontendRecoveryRuntime frontend, GameObject selectionPagePrefab)
        {
            _frontend = frontend; _selectionPagePrefab = selectionPagePrefab;
        }
        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void OpenSelection()
        {
            if (_transitioning || _selectionPage != null) return;
            StartCoroutine(ShowSelection());
        }
        private IEnumerator ShowSelection()
        {
            _transitioning = true;
            double started = Time.realtimeSinceStartupAsDouble;
            var homeInput = _frontend.GetComponent<CanvasGroup>();
            if (homeInput == null) homeInput = _frontend.gameObject.AddComponent<CanvasGroup>();
            homeInput.interactable = false; homeInput.blocksRaycasts = false;
            // Prepare the destination while the outgoing page remains visible. Hide both input surfaces.
            _selectionPage = Instantiate(_selectionPagePrefab);
            _selectionPage.name = "SelectionPage";
            var canvas = _selectionPage.GetComponentInChildren<Canvas>();
            canvas.name = "SelectionCanvas";
            var selectionGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            selectionGroup.alpha = 0; selectionGroup.interactable = false; selectionGroup.blocksRaycasts = false;
            var selection = _selectionPage.GetComponentInChildren<LocalMusicSelectionRuntime>();
            while (!selection.IsInitialized) yield return null;
            var another = selection.GetComponent<AnotherNotationSelectionRuntime>();
            while (another != null && (!another.IsReady || another.IsPreparing)) yield return null;
            yield return _frontend.LeaveForSelection();
            var camera = GameObject.Find("UICamera").GetComponent<Camera>();
            camera.orthographic = true;
            selectionGroup.alpha = 1; selectionGroup.interactable = true; selectionGroup.blocksRaycasts = true;
            _transitioning = false;
            LastTransitionSeconds = Time.realtimeSinceStartupAsDouble - started;
            Debug.Log($"OPENWDS_MAIN_PAGE page=selection seconds={LastTransitionSeconds:F3}");
        }
        public void OpenHome()
        {
            if (_transitioning || _selectionPage == null) return;
            StartCoroutine(ShowHome());
        }
        private IEnumerator ShowHome()
        {
            _transitioning = true;
            double started = Time.realtimeSinceStartupAsDouble;
            // ReturnHome already waited for jacket reads; destroy the outgoing page before releasing its scope.
            _selectionPage.SetActive(false);
            Destroy(_selectionPage); _selectionPage = null;
            yield return null;
            var input = _frontend.GetComponent<CanvasGroup>();
            input.interactable = true; input.blocksRaycasts = true;
            yield return _frontend.ShowHome();
            _transitioning = false;
            LastTransitionSeconds = Time.realtimeSinceStartupAsDouble - started;
            Debug.Log($"OPENWDS_MAIN_PAGE page=home seconds={LastTransitionSeconds:F3}");
        }
    }
}
