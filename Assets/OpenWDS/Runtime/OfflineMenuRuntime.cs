using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DG.Tweening;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace OpenWDS.Runtime
{
    /// <summary>Offline actions hosted by the APK's shared MenuView/MenuPopView.</summary>
    public sealed class OfflineMenuRuntime : MonoBehaviour
    {
        [Serializable] private sealed class BundleRow { public int id; public string key; public string path; }
        [Serializable] private sealed class AssetRow { public string key; public string internalId; public int bundle; }
        [Serializable] private sealed class Index { public BundleRow[] bundles; public AssetRow[] assets; }
        private readonly Dictionary<int, LocalAssetBundleLease> _bundles = new Dictionary<int, LocalAssetBundleLease>();
        private Transform _parent;
        private Func<Action, GamePauseRuntime> _createSettings;
        private Action _opened, _closed;
        public Action NavigateHome { private get; set; }
        private OfflineMenuDocuments _documents;
        private Func<Action, Transform> _termsHost;
        private GameObject _sideMenuButton;
        private GameObject _view, _popup, _block;
        private GamePauseRuntime _settings;
        private Sequence _transition;
        private IDisposable _hideSubscription;
        private bool _busy;
        private bool _childOpen;
        public bool IsTransitioning => _busy;
        public int CompletedHideCount { get; private set; }
        public bool IsOpen => _block != null;
        public bool IsReady { get; private set; }
        public GameObject Popup => _popup;
        public GameObject View => _view;
        public int BundleCount => _bundles.Count;

        public void Configure(Transform parent, Func<Action, GamePauseRuntime> settings,
            Action opened, Action closed, Func<Action, Transform> termsHost, GameObject sideMenuButton)
        {
            _parent = parent; _createSettings = settings; _opened = opened; _closed = closed;
            _termsHost = termsHost; _sideMenuButton = sideMenuButton;
        }

        public IEnumerator Initialize()
        {
            byte[] bytes = null;
            yield return StreamingAssetsRuntime.ReadBytes("OpenWDS/Menu/index.json", data => bytes = data);
            var index = JsonUtility.FromJson<Index>(Encoding.UTF8.GetString(bytes));
            foreach (var row in index.bundles)
            {
                LocalAssetBundleLease lease = null;
                yield return LocalAssetBundleLease.LoadStreaming(row.key, row.path, value =>
                {
                    lease = value;
                    if (this == null) value.Dispose(); else _bundles.Add(row.id, value);
                });
                if (this == null) yield break;
                if (lease.Bundle == null) throw new InvalidOperationException("Menu bundle failed: " + row.path);
            }
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var row in index.assets)
            {
                var prefab = _bundles[row.bundle].Bundle.LoadAsset<GameObject>(row.internalId);
                if (prefab == null) throw new InvalidOperationException("Menu prefab missing: " + row.key);
                prefabs.Add(row.key, prefab);
                if (!row.key.StartsWith("Feature/Menu/")) continue;
                var instance = Instantiate(prefab, _parent, false);
                instance.name = prefab.name;
                if (instance.GetComponentsInChildren<MonoBehaviour>(true).Any(x => x == null))
                    throw new InvalidOperationException("Menu contains missing scripts: " + row.key);
                foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Badge" || child.name == "AlertBadge" || child.name == "Bell_Badge" || child.name == "Timer")
                        child.gameObject.SetActive(false);
                if (row.key.EndsWith("/MenuView")) _view = instance;
                else _popup = instance;
            }
            _documents = gameObject.AddComponent<OfflineMenuDocuments>();
            _documents.Configure(_parent, prefabs, _sideMenuButton, _termsHost);
            var viewGroup = _view.GetComponent<CanvasGroup>();
            viewGroup.alpha = 1; viewGroup.interactable = true; viewGroup.blocksRaycasts = true;
            Bind(_view.GetComponentsInChildren<Button>(true).Single(), Open);
            foreach (var button in _popup.GetComponentsInChildren<Button>(true))
            {
                button.onClick.RemoveAllListeners();
                string feature = button.transform.parent.name;
                if (button.name == "CloseButton") Bind(button, Close, UiSeRuntime.Cue.ButtonBack);
                else if (feature == "Home")
                {
                    Bind(button, () =>
                    {
                        if (_busy) return;
                        StartCoroutine(HidePopup(() => { if (NavigateHome != null) NavigateHome(); else FrontendNavigation.Home(); }));
                    });
                }
                else if (feature == "Option" && _createSettings != null) Bind(button, OpenSettings);
                else if (feature == "Notification") Bind(button, () => OpenDocument(false));
                else if (feature == "TermOfService" && _termsHost != null) Bind(button, () => OpenDocument(true));
                else
                {
                    button.interactable = false;
                    var unavailable = button.transform.parent.gameObject.AddComponent<CanvasGroup>();
                    unavailable.alpha = 0.35f;
                    unavailable.interactable = false;
                }
            }
            _popup.SetActive(false);
            IsReady = true;
        }

        private static void Bind(Button button, Action action, UiSeRuntime.Cue cue = UiSeRuntime.Cue.ButtonGo)
        {
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { UiSeRuntime.Instance?.Play(cue); action(); });
        }

        public void BringButtonToFront() => _view.transform.SetAsLastSibling();

        public void Open()
        {
            if (!IsReady || IsOpen || _busy) return;
            _opened?.Invoke();
            _block = new GameObject("MenuTouchBlock", typeof(RectTransform), typeof(Image), typeof(Button));
            _block.transform.SetParent(_parent, false);
            var rect = (RectTransform)_block.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _block.GetComponent<Image>().color = new Color(0, 0, 0, 0.7f);
            Bind(_block.GetComponent<Button>(), Close, UiSeRuntime.Cue.ButtonBack);
            ShowPopup();
        }

        private void ShowPopup()
        {
            _childOpen = false;
            _transition?.Kill();
            _popup.SetActive(true);
            _popup.transform.SetAsLastSibling();
            var animator = _popup.GetComponentsInChildren<Animator>(true).Single(a => a.name == "MenuPopPanel");
            animator.Rebind();
            animator.Update(0);
            var group = _popup.GetComponent<CanvasGroup>();
            var body = _popup.GetComponentsInChildren<CanvasGroup>(true).Single(g => g.name == "MenuButtons");
            group.alpha = 0;
            group.interactable = false; body.alpha = 0;
            _busy = true;
            // MenuPopView.ShowAsync 0xB1EDB38: scale (InQuad) + fade,
            // followed by the authored button body fade, each 0.2 seconds.
            _transition = DOTween.Sequence().Append(group.transform.DOScale(1, 0.2f).SetEase(Ease.InQuad))
                .Join(group.DOFade(1, 0.2f)).Append(body.DOFade(1, 0.2f))
                .SetLink(_popup).OnComplete(() => { group.interactable = true; _busy = false; });
        }

        public void Close()
        {
            if (!IsOpen || _busy || _childOpen) return;
            StartCoroutine(HidePopup(() =>
            {
                _block.SetActive(false);
                Destroy(_block);
                _block = null;
                _closed?.Invoke();
            }));
        }

        private IEnumerator HidePopup(Action completed)
        {
            _busy = true;
            _transition?.Kill();
            _popup.GetComponent<CanvasGroup>().interactable = false;
            var body = _popup.GetComponentsInChildren<CanvasGroup>(true).Single(g => g.name == "MenuButtons");
            body.DOFade(0, 0.2f).SetLink(_popup);
            var animator = _popup.GetComponentsInChildren<Animator>(true).Single(a => a.name == "MenuPopPanel");
            // Keep a typed reference as well as the bundle's serialized reference
            // so the original state behaviour survives IL2CPP stripping.
            var trigger = animator.GetBehaviour<UniRx.Triggers.ObservableStateMachineTrigger>();
            if (trigger == null)
                throw new InvalidOperationException("Original menu Animator requires the UniRx state trigger.");
            animator.SetTrigger("FadeOut");
            // AnimatorExtensions.WaitAnimationIsCompletedAsync (0xA5FF750,
            // predicate 0xA600004) observes the original state trigger updates.
            var finished = false;
            _hideSubscription = trigger.OnStateUpdateAsObservable()
                .Where(info => info.StateInfo.normalizedTime >= 1f)
                .Take(1).Subscribe(_ => finished = true);
            var deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (finished)
                {
                    _hideSubscription.Dispose();
                    _hideSubscription = null;
                    _popup.SetActive(false);
                    _busy = false;
                    CompletedHideCount++;
                    completed();
                    yield break;
                }
                yield return null;
            }
            _hideSubscription.Dispose();
            _hideSubscription = null;
            throw new InvalidOperationException("Original menu FadeOut did not complete.");
        }

        private void OpenSettings()
        {
            if (_busy) return;
            _childOpen = true;
            StartCoroutine(HidePopup(() =>
            {
                if (_settings == null) _settings = _createSettings(ShowPopup);
                _settings.OpenSelectionSettings();
            }));
        }

        private void OpenDocument(bool terms)
        {
            if (_busy) return;
            _childOpen = true;
            StartCoroutine(HidePopup(() =>
            {
                if (terms) _documents.ShowTerms(ShowPopup);
                else _documents.ShowNotices(ShowPopup);
            }));
        }

        private void OnDestroy()
        {
            _hideSubscription?.Dispose();
            _transition?.Kill();
            if (_view != null) Destroy(_view);
            if (_popup != null) Destroy(_popup);
            if (_block != null) Destroy(_block);
            // Instances are destroyed at the end of the frame; detach assets
            // from their bundles without invalidating those instances mid-frame.
            foreach (var bundle in _bundles.Values) bundle.Dispose();
            _bundles.Clear();
        }
    }
}
