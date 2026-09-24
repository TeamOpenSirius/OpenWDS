using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using EnhancedUI.EnhancedScroller;
using UnityEngine;
using UnityEngine.UI;

namespace OpenWDS.Runtime
{
    /// <summary>Local presenters for the original terms body and notification view/cells.</summary>
    public sealed class OfflineMenuDocuments : MonoBehaviour, IEnhancedScrollerDelegate
    {
        private Transform _parent;
        private Dictionary<string, GameObject> _prefabs;
        private GameObject _sideButton;
        private Func<Action, Transform> _termsHost;
        private GameObject _notice, _terms, _cellTemplate;
        private EnhancedScroller _scroller;
        private EnhancedScrollerCellView _cell;
        private Action _noticeClosed;
        private Sequence _animation;
        private int _category;
        private bool _transitioning;
        public bool IsTransitioning => _transitioning;
        public GameObject Notice => _notice;
        public GameObject Terms => _terms;
        private const string StatementTitle = "OpenWDS オフライン版について";

        public void Configure(Transform parent, Dictionary<string, GameObject> prefabs,
            GameObject sideButton, Func<Action, Transform> termsHost)
        {
            _parent = parent; _prefabs = prefabs; _sideButton = sideButton; _termsHost = termsHost;
        }

        private static Transform Find(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
        private static string Content(string name) =>
            (Resources.Load<TextAsset>("TextAsset/OfflineMenu/" + name)
                ?? throw new InvalidOperationException("Missing menu text: " + name)).text;
        private static void Bind(Button button, Action action, bool back = false)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                UiSeRuntime.Instance?.Play(back ? UiSeRuntime.Cue.ButtonBack : UiSeRuntime.Cue.ButtonGo);
                action();
            });
        }
        private static void Visible(GameObject root, bool visible)
        {
            root.SetActive(visible);
            var group = root.GetComponent<CanvasGroup>();
            if (group != null) { group.alpha = visible ? 1 : 0; group.interactable = visible; group.blocksRaycasts = visible; }
        }
        private GameObject Spawn(string key, Transform parent)
        {
            var instance = Instantiate(_prefabs[key], parent, false);
            instance.name = _prefabs[key].name;
            if (instance.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c == null))
                throw new InvalidOperationException("Missing original document component: " + key);
            return instance;
        }

        public void ShowTerms(Action closed)
        {
            var host = _termsHost(() => { _terms = null; closed(); });
            _terms = Spawn("Feature/Title/TermsOfServiceDetailDialogBody", host);
            var texts = Enumerable.Range(1, 4).Select(i => Find(_terms.transform, "ContentText" + i).GetComponent<Text>()).ToArray();
            var scroll = Find(_terms.transform, "Scroll View").GetComponent<ScrollRect>();
            BuildTabs(_terms.transform, new[] { "利用規約", "権利表記", "プライバシーポリシー" }, page =>
            {
                // Original SetText 0xB235F34: four copyright blocks, otherwise
                // one text and three empty strings; replace ASCII spaces with NBSP.
                for (var i = 0; i < texts.Length; i++)
                    texts[i].text = (page == 1 ? Content("Copyright" + (i + 1)) :
                        i == 0 ? Content(page == 0 ? "TermsOfService" : "PrivacyPolicy") : "").Replace(' ', '\u00a0');
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = 1;
            });
        }

        private void BuildTabs(Transform root, string[] labels, Action<int> selected, int disabled = -1)
        {
            var panel = Find(root, "TextSideMenuPanel");
            var menuList = (RectTransform)panel.Find("MenuList");
            // SetEnabledScroller 0xB4BCDD4: dialog with fewer than five
            // categories uses zero stretched insets, not prefab authoring insets.
            menuList.offsetMin = menuList.offsetMax = Vector2.zero;
            var sideScroll = menuList.GetComponent<ScrollRect>();
            sideScroll.movementType = ScrollRect.MovementType.Clamped;
            var content = (RectTransform)menuList.Find("Content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero;
            var selection = Find(content, "SelectedObject").GetComponent<Image>();
            var selectedSprite = selection.sprite;
            selection.gameObject.SetActive(false);
            Find(content, "SideMenuHorizontalStripe").gameObject.SetActive(false);
            var tabs = new List<Button>();
            Action<int> choose = page =>
            {
                for (var i = 0; i < tabs.Count; i++)
                {
                    var graphic = tabs[i].GetComponent<Image>();
                    graphic.sprite = i == page ? selectedSprite : null;
                    graphic.color = i == page ? Color.white : Color.clear;
                    Find(tabs[i].transform, "Label").GetComponent<Text>().color = i == page
                        ? Color.white : new Color32(86, 88, 103, (byte)(i == disabled ? 90 : 255));
                }
                selected(page);
            };
            for (var i = 0; i < labels.Length; i++)
            {
                var tab = Instantiate(_sideButton, content, false);
                tab.name = "DocumentTab" + i;
                var rect = (RectTransform)tab.transform;
                rect.sizeDelta = new Vector2(260, 116);
                Find(tab.transform, "Label").GetComponent<Text>().text = labels[i];
                var button = tab.GetComponent<Button>();
                var page = i;
                Bind(button, () => choose(page));
                button.interactable = i != disabled;
                tabs.Add(button);
            }
            Canvas.ForceUpdateCanvases();
            choose(0);
        }

        public void ShowNotices(Action closed, bool openStatement = false)
        {
            _noticeClosed = closed;
            _notice = Spawn("Feature/Notification/NotificationView", _parent);
            _notice.transform.SetAsLastSibling();
            var list = Find(_notice.transform, "ListPanel").gameObject;
            Visible(list, true);
            Visible(Find(_notice.transform, "BodyPanel").gameObject, false);
            Visible(Find(_notice.transform, "RelatedSitesPanel").gameObject, false);
            _scroller = Find(list.transform, "ListScroller").GetComponent<EnhancedScroller>();
            _cellTemplate = Spawn("OpenWDS/NotificationListCell", transform);
            _cellTemplate.SetActive(false);
            _cell = _cellTemplate.AddComponent<EnhancedScrollerCellView>();
            _cell.cellIdentifier = "OpenWDSStatement";
            _scroller.Delegate = this;
            BuildTabs(list.transform, new[] { "重要", "更新情報", "不具合情報", "関連サイト" }, page =>
            {
                _category = page;
                ReloadNotices();
            }, 3);
            Bind(Find(_notice.transform, "CloseButton").GetComponent<Button>(), CloseNotices, true);
            Bind(Find(_notice.transform, "BackButton").GetComponent<Button>(), ShowNoticeList, true);
            // NotificationView.ShowAsync 0xB1B1D5C: authored popup height 900,
            // OutCubic (9), root fade Linear (1), both 0.2 seconds.
            var popup = (RectTransform)Find(_notice.transform, "NotificationPopup");
            Canvas.ForceUpdateCanvases();
            var bounds = ((RectTransform)_notice.transform).rect;
            // Keep the authored popup and close button inside narrow/tall screens too.
            float scale = Mathf.Min(1f, bounds.width / (popup.sizeDelta.x + 100f), bounds.height / 1000f);
            popup.localScale = Vector3.one * scale;
            var group = _notice.GetComponent<CanvasGroup>();
            group.alpha = 0; group.interactable = false;
            _transitioning = true;
            _animation = DOTween.Sequence().Append(popup.DOSizeDelta(new Vector2(popup.sizeDelta.x, 900), .2f).SetEase(Ease.OutCubic))
                .Join(group.DOFade(1, .2f).SetEase(Ease.Linear)).SetLink(_notice)
                .OnComplete(() => { group.interactable = true; _transitioning = false; if (openStatement) ShowNoticeBody(); });
        }

        private void ReloadNotices()
        {
            // EnhancedScrollerReloadData 0xB1B0640 calls AlignContainerLayout
            // 0xA5FA69C with UpperCenter and disables width control, then reloads.
            var content = _scroller.ScrollRect.content;
            var layout = content.GetComponent<HorizontalOrVerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            content.anchoredPosition = new Vector2(0, content.anchoredPosition.y);
            _scroller.ReloadData();
        }

        public int GetNumberOfCells(EnhancedScroller scroller) => _category == 0 ? 1 : 0;
        public float GetCellViewSize(EnhancedScroller scroller, int dataIndex) => ((RectTransform)_cell.transform).sizeDelta.y;
        public EnhancedScrollerCellView GetCellView(EnhancedScroller scroller, int dataIndex, int cellIndex)
        {
            var cell = scroller.GetCellView(_cell);
            cell.name = "ProjectStatementNotice";
            Visible(cell.gameObject, true);
            FillElement(Find(cell.transform, "NotificationElement"));
            Find(cell.transform, "NewBadge").gameObject.SetActive(false);
            Bind(cell.GetComponentInChildren<Button>(true), ShowNoticeBody);
            return cell;
        }

        private static void FillElement(Transform element)
        {
            Find(element, "Title").GetComponent<Text>().text = StatementTitle;
            Find(element, "PostingTime").GetComponent<Text>().text = "2026/09/24 00:00";
            Find(element, "NotificationCategoryText").GetComponent<Text>().text = "重要";
            // Local text-only notice has no remote banner to wait for.
            Find(element, "Banner").GetComponent<Image>().enabled = false;
            Find(element, "Loading_Icon").gameObject.SetActive(false);
        }

        private void ShowNoticeBody()
        {
            if (_transitioning) return;
            Visible(Find(_notice.transform, "ListPanel").gameObject, false);
            var body = Find(_notice.transform, "BodyPanel");
            Visible(body.gameObject, true);
            FillElement(Find(body, "NotificationElement"));
            var scroll = Find(body, "NotificationContentScroller").GetComponent<ScrollRect>();
            if (scroll.content.childCount == 0)
            {
                var cell = Spawn("OpenWDS/NotificationTextBodyCell", scroll.content);
                var text = Find(cell.transform, "Body").GetComponent<Text>();
                // Noto CJK covers all three statement languages.
                text.font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                if (text.font == null) throw new InvalidOperationException("Missing announcement font.");
                text.text = Content("Statement.zh-Hans") + "\n\n" + Content("Statement.en") + "\n\n" + Content("Statement");
            }
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.StopMovement(); scroll.verticalNormalizedPosition = 1;
        }

        private void ShowNoticeList()
        {
            if (_transitioning) return;
            Visible(Find(_notice.transform, "BodyPanel").gameObject, false);
            Visible(Find(_notice.transform, "ListPanel").gameObject, true);
            ReloadNotices();
        }

        private void CloseNotices()
        {
            if (_transitioning) return;
            _transitioning = true;
            var group = _notice.GetComponent<CanvasGroup>();
            group.interactable = false;
            var popup = (RectTransform)Find(_notice.transform, "NotificationPopup");
            _animation = DOTween.Sequence().Append(popup.DOSizeDelta(new Vector2(popup.sizeDelta.x, 150), .2f).SetEase(Ease.OutCubic))
                .Join(group.DOFade(0, .2f).SetEase(Ease.Linear)).SetLink(_notice).OnComplete(() =>
            {
                Destroy(_notice); _notice = null;
                Destroy(_cellTemplate); _cellTemplate = null;
                _transitioning = false;
                _noticeClosed?.Invoke(); _noticeClosed = null;
            });
        }

        private void OnDestroy()
        {
            _animation?.Kill();
            if (_notice != null) Destroy(_notice);
            if (_cellTemplate != null) Destroy(_cellTemplate);
            if (_terms != null) Destroy(_terms.transform.parent.parent.gameObject);
        }
    }
}
