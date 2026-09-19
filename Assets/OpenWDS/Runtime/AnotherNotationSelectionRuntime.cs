using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EnhancedUI.EnhancedScroller;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;

namespace OpenWDS.Runtime
{
    /// <summary>Offline presenter for the APK's independent AnotherNotationEventView.</summary>
    public sealed class AnotherNotationSelectionRuntime : MonoBehaviour, IEnhancedScrollerDelegate
    {
        [Serializable] public sealed class Entry
        {
            public long Id, MusicId, StartDateTicks, MusicReleasedAtTicks;
            public int VocalVersion, Difficulty, Level, NotationType;
            public string NotationPath;
            public LocalMusicEntry Music;
        }
        [Serializable] private sealed class Catalog { public Entry[] Entries; }
        [Serializable] private sealed class BundleRow { public int id; public string key, path; }
        [Serializable] private sealed class AssetRow { public string key, internalId; public int bundle; }
        [Serializable] private sealed class SpriteRow { public string key, name; public int bundle; }
        [Serializable] private sealed class Index { public BundleRow[] bundles; public AssetRow[] assets; public SpriteRow[] sprites; }
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private static readonly string[] TypeWords = { "", "蔵", "不", "替", "謎", "狂", "再", "裏", "易" };
        private readonly Dictionary<int, LocalAssetBundleLease> _bundles = new Dictionary<int, LocalAssetBundleLease>();
        private readonly Dictionary<long, AssetBundle> _jacketBundles = new Dictionary<long, AssetBundle>();
        private readonly HashSet<long> _loadingJackets = new HashSet<long>();
        private readonly Dictionary<long, Sprite> _jackets = new Dictionary<long, Sprite>();
        private readonly Dictionary<EnhancedScrollerCellView, Entry> _cells = new Dictionary<EnhancedScrollerCellView, Entry>();
        private readonly Dictionary<Transform, Dictionary<string, Transform>> _nodes = new Dictionary<Transform, Dictionary<string, Transform>>();
        private readonly Dictionary<EnhancedScrollerCellView, bool> _focused = new Dictionary<EnhancedScrollerCellView, bool>();
        private Entry _preparedEntry;
        public int SelectionPreparationCount { get; private set; }
        public int ChartReadCount { get; private set; }
        private LocalMusicSelectionRuntime _host;
        private GameObject _background;
        private readonly List<Material> _backgroundMaterials = new List<Material>();
        private GameObject _view, _template, _toolbar, _header, _filterPrefab, _titlePrefab;
        private readonly AnotherNotationFilters _filter = new AnotherNotationFilters();
        private Entry[] _allEntries;
        private EnhancedScrollerCellView _cellTemplate;
        private EnhancedScroller _scroller;
        private MusicSelectionPreviewRuntime _preview;
        private GamePauseRuntime _settings;
        private Action _closed;
        private Entry[] _entries;
        private Entry _selected;
        private float _cellSize;
        private bool _busy, _launching, _auto;
        private TextAsset _chart, _config;
        private LocalResultStore _results;
        public bool IsOpen => _view != null;
        public bool IsReady { get; private set; }
        public long SelectedId => _selected?.Id ?? 0;
        public int EntryCount => _entries?.Length ?? 0;
        public IReadOnlyList<Entry> VisibleEntries => _entries;
        public GameObject View => _view;
        public bool IsPreparing => _busy;

        private Transform Find(Transform root, string name)
        {
            if (!_nodes.TryGetValue(root, out var nodes))
            {
                nodes = new Dictionary<string, Transform>();
                foreach (var node in root.GetComponentsInChildren<Transform>(true))
                    if (!nodes.ContainsKey(node.name)) nodes.Add(node.name, node);
                _nodes.Add(root, nodes);
            }
            return nodes[name];
        }
        private void TextAt(Transform root, string name, string text) => Find(root, name).GetComponent<Text>().text = text;
        private static void Bind(Button button, Action action)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { UiSeRuntime.Instance?.Play(UiSeRuntime.Cue.ButtonGo); action(); });
        }
        private static void Visible(GameObject go, bool visible)
        {
            go.SetActive(visible);
            var group = go.GetComponent<CanvasGroup>();
            if (group != null) { group.alpha = visible ? 1 : 0; group.interactable = visible; group.blocksRaycasts = visible; }
        }

        public IEnumerator Initialize(LocalMusicSelectionRuntime host, Transform parent, Action closed)
        {
            _host = host; _closed = closed; _busy = true;
            byte[] bytes = null;
            yield return StreamingAssetsRuntime.ReadBytes("OpenWDS/AnotherNotations/catalog.json", b => bytes = b);
            _entries = JsonUtility.FromJson<Catalog>(Encoding.UTF8.GetString(bytes)).Entries;
            if (_entries == null || _entries.Length == 0 || _entries.Select(e => e.Id).Distinct().Count() != _entries.Length)
                throw new InvalidOperationException("Invalid AnotherNotation catalog.");
            yield return StreamingAssetsRuntime.ReadBytes("OpenWDS/AnotherNotationView/index.json", b => bytes = b);
            var index = JsonUtility.FromJson<Index>(Encoding.UTF8.GetString(bytes));
            foreach (var row in index.bundles)
            {
                yield return StreamingAssetsRuntime.ReadBytes(row.path, b => bytes = b);
                var lease = LocalAssetBundleLease.FromMemory(row.key, bytes);
                _bundles.Add(row.id, lease);
                if (lease.LoadOperation != null) yield return lease.LoadOperation;
                if (lease.Bundle == null) throw new InvalidOperationException("AnotherNotation bundle missing: " + row.path);
            }
            foreach (var row in index.sprites)
            {
                var atlases = _bundles[row.bundle].Bundle.LoadAllAssets<SpriteAtlas>();
                var sprite = atlases.Select(a => a.GetSprite(row.name)).FirstOrDefault(s => s != null);
                if (sprite == null) throw new InvalidOperationException("Original config sprite missing: " + row.key + "/" + row.name);
                _sprites.Add(row.key, sprite);
            }
            foreach (var row in index.assets)
            {
                var prefab = _bundles[row.bundle].Bundle.LoadAsset<GameObject>(row.internalId);
                if (prefab == null) throw new InvalidOperationException("AnotherNotation prefab missing: " + row.key);
                if (row.key.EndsWith("TitleHeaderView")) { _titlePrefab = prefab; continue; }
                if (row.key.EndsWith("MusicSortFilterDialogBody")) { _filterPrefab = prefab; continue; }
                var instance = Instantiate(prefab, parent, false);
                instance.name = prefab.name;
                if (instance.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c == null))
                    throw new InvalidOperationException("Missing script in AnotherNotation prefab: " + row.key);
                if (row.key == "Background/JugonBackground") _background = instance;
                else if (row.key == "OpenWDS/AnotherNotationCell") _template = instance;
                else _view = instance;
            }
            RestoreBackground();
            RestoreSharedTicketMachine();
            _host.HideStandardSelectionForAnother();
            _view.transform.SetAsLastSibling();
            Visible(_view, true);
            _template.SetActive(false);
            _cellSize = MusicSelectionListGeometry.OffsetHeight;
            _cellTemplate = _template.AddComponent<EnhancedScrollerCellView>();
            _cellTemplate.cellIdentifier = "AnotherNotation";
            _scroller = _view.GetComponentInChildren<EnhancedScroller>(true);
            _view.transform.Find("MusicListPanel/Arrow")?.gameObject.SetActive(false);
            _scroller.Delegate = this;
            _scroller.scrollerSnapped = (scroller, cellIndex, dataIndex, cell) => SelectIndex(dataIndex, false);
            _scroller.spacing = MusicSelectionListGeometry.Spacing;
            _scroller.snapTweenType = EnhancedScroller.TweenType.easeOutCubic;
            _scroller.snapTweenTime = .2f;
            _scroller.ScrollRect.decelerationRate = .01f;
            var input = _scroller.gameObject.AddComponent<MusicSelectionLoopInput>();
            input.Configure(() => _scroller.snapping = false, () =>
            {
                _scroller.snapping = true;
                if (Mathf.Abs(_scroller.LinearVelocity) <= _scroller.snapVelocityThreshold) _scroller.Snap();
            });
            var layout = _scroller.GetComponent<ScrollRect>().content.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            var contentPosition = _scroller.GetComponent<ScrollRect>().content.anchoredPosition;
            contentPosition.x = 0; _scroller.GetComponent<ScrollRect>().content.anchoredPosition = contentPosition;
            Visible(Find(_view.transform, "NoMusicPanel").gameObject, false);
            _preview = _view.AddComponent<MusicSelectionPreviewRuntime>();
            _preview.Configure(_view.transform);
            Bind(Find(_view.transform, "RandomButton").GetComponent<Button>(), () => SelectIndex(UnityEngine.Random.Range(0, _entries.Length), true));
            Bind(Find(_view.transform, "OkButton").GetComponent<Button>(), Launch);
            Bind(Find(_view.transform, "SettingsButton").GetComponent<Button>(), OpenSettings);
            // SwitchTextToggle is a Sirius component; use its original button and labels.
            var auto = Find(_view.transform, "AutoSettingToggle");
            var autoButton = auto.GetComponent<Button>() ?? auto.gameObject.AddComponent<Button>();
            Bind(autoButton, ToggleAuto);
            RefreshAuto();
            CreateBackButton(parent);
            _header = _host.CreateAnotherHeader(parent);
            _header.name = "AnotherNotationHeader";
            _host.GetComponent<OfflineMenuRuntime>().BringButtonToFront();
            Find(_header.transform, "RateDataPanel").gameObject.SetActive(false);
            Find(_header.transform, "StaminaDataPanel").gameObject.SetActive(false);
            Bind(Find(_header.transform, "FilterSettingButton").GetComponent<Button>(), () => OpenFilter(true));
            Bind(Find(_header.transform, "SortButton").GetComponent<Button>(), () => OpenFilter(false));
            _filter.Applied = ApplyFilter;
            _allEntries = _entries;
            _results = new LocalResultStore(anotherNotation: true);
            _entries = SortEntries(_allEntries);
            _selected = _entries.First(e => e.Id == 1);
            _scroller.ReloadData();
            yield return null;
            _scroller.JumpToDataIndex(Array.IndexOf(_entries, _selected), .5f, .5f, false);
            _busy = false;
            IsReady = true;
            SelectIndex(Array.IndexOf(_entries, _selected), false);
            RefreshHeader();
            Debug.Log("OPENWDS_ANOTHER_SELECTION_READY entries=" + _entries.Length);
        }

        public void SelectId(long id) => SelectIndex(Array.FindIndex(_entries, e => e.Id == id));
        public void OpenFilter(bool showFilter)
        {
            if (_busy || _launching) return;
            _host.OpenAnotherFilterDialog(_filterPrefab, _filter, showFilter);
        }
        private Entry[] SortEntries(IEnumerable<Entry> source)
        {
            // Native Sort 0xB3338A0: selected key, then Music released-at and ID.
            IOrderedEnumerable<Entry> sorted;
            switch (_filter.SortMode)
            {
                case 0: sorted = source.OrderBy(e => e.Level).ThenBy(e => e.Difficulty); break;
                case 1: sorted = source.OrderBy(e => e.Music.PronounceName, StringComparer.CurrentCulture); break;
                case 2: sorted = source.OrderBy(e => _results.GetBest(e.Id, (MusicDifficulty)e.Difficulty)); break;
                case 3: sorted = source.OrderBy(e => e.StartDateTicks); break;
                default: throw new ArgumentOutOfRangeException("SortMode");
            }
            return sorted.ThenBy(e => e.MusicReleasedAtTicks).ThenBy(e => e.MusicId).ToArray();
        }
        private void ApplyFilter()
        {
            var previous = _selected;
            var entries = _allEntries.Where(e =>
                (_filter.Difficulty.Count == 0 || _filter.Difficulty.Contains(e.Difficulty - 1)) &&
                (_filter.MusicVideo.Count == 0 || _filter.MusicVideo.Contains(e.Music.MusicVideoType == MusicVideoType.None ? 1 : 0)) &&
                (_filter.MusicType.Count == 0 || _filter.MusicType.Contains(e.Music.MusicCoverType - 1)) &&
                (_filter.AllActors || _filter.Actors.Overlaps(e.Music.ActorIds)) &&
                (_filter.ClearLamp.Count == 0 || _filter.ClearLamp.Any(f =>
                    (int)_results.GetClearLamp(e.Id, (MusicDifficulty)e.Difficulty) < (f == 0 ? 1 : f == 1 ? 2 : 3))));
            _entries = SortEntries(entries);
            _selected = _entries.Contains(previous) ? previous : _entries.FirstOrDefault();
            _scroller.ReloadData();
            Visible(Find(_view.transform, "NoMusicPanel").gameObject, _entries.Length == 0);
            Find(_view.transform, "OkButton").GetComponent<Button>().interactable = _entries.Length > 0;
            Find(_view.transform, "RandomButton").GetComponent<Button>().interactable = _entries.Length > 0;
            if (_selected != null) SelectIndex(Array.IndexOf(_entries, _selected));
            else { _preview.Stop(); _preparedEntry = null; }
            RefreshHeader();
        }
        private void RefreshHeader()
        {
            foreach (var text in Find(_header.transform, "SortButton").GetComponentsInChildren<Text>(true))
                text.text = new[] { "Lv", "曲名", "達成率", "追加日時" }[_filter.SortMode];
            var active = Find(_header.transform, "FilterSettingButton").GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Active");
            if (active != null) active.gameObject.SetActive(_filter.Difficulty.Count + _filter.ClearLamp.Count + _filter.MusicVideo.Count + _filter.MusicType.Count > 0 || !_filter.AllActors);
        }

        private void CreateBackButton(Transform parent)
        {
            _toolbar = Instantiate(_titlePrefab, parent, false);
            _toolbar.name = "AnotherNotationTitleHeader";
            Visible(_toolbar, true);
            TextAt(_toolbar.transform, "PageNameText", "楽曲選択");
            Find(_toolbar.transform, "HelpButton").gameObject.SetActive(false);
            Find(_toolbar.transform, "Timer").gameObject.SetActive(false);
            var button = Find(_toolbar.transform, "BackButton").GetComponent<Button>();
            button.gameObject.name = "AnotherNotationBack";
            Bind(button, Close);
        }

        public int GetNumberOfCells(EnhancedScroller scroller) => _entries.Length;
        public float GetCellViewSize(EnhancedScroller scroller, int dataIndex) => _cellSize;
        public EnhancedScrollerCellView GetCellView(EnhancedScroller scroller, int dataIndex, int cellIndex)
        {
            var cell = scroller.GetCellView(_cellTemplate);
            cell.gameObject.SetActive(true);
            var entry = _entries[dataIndex];
            _cells[cell] = entry;
            _focused.Remove(cell);
            Bind(cell.GetComponentInChildren<Button>(true), () => SelectIndex(dataIndex, true));
            RefreshCell(cell, entry);
            if (!_jackets.ContainsKey(entry.MusicId) && !_loadingJackets.Contains(entry.MusicId)) StartCoroutine(LoadJacket(entry));
            return cell;
        }

        private void RefreshCell(EnhancedScrollerCellView cell, Entry entry)
        {
            var root = cell.transform;
            UpdateFocus(cell, entry);
            TextAt(Find(root, "MusicTitle"), "BodyText", entry.Music.Name);
            TextAt(Find(root, "Vocals"), "BodyText", entry.Music.Vocals);
            TextAt(root, "DifficultyLevelText", LocalMusicSelectionRuntime.FormatMusicLevel(entry.Level));
            var marker = Find(root, "DifficultyLevelBackgroundImage").GetComponent<Image>();
            marker.sprite = _host.AnotherDifficultyMarker((MusicDifficulty)entry.Difficulty);
            TextAt(root, "NotationTypeLabel", TypeWords[entry.NotationType]);
            var typeIcon = Find(root, "NotationTypeIcon").GetComponent<Image>();
            typeIcon.sprite = _sprites["type" + entry.NotationType]; typeIcon.enabled = true;
            var gradeBadge = Find(root, "RateGradeBadge").GetComponent<Image>();
            gradeBadge.sprite = _host.AnotherRateGrade(_results.GetBest(entry.Id, (MusicDifficulty)entry.Difficulty));
            gradeBadge.enabled = gradeBadge.sprite != null;
            gradeBadge.preserveAspect = true;
            var playType = Find(root, "PlayTypeBadge");
            playType.gameObject.SetActive(entry.Music.IsLongVersion || entry.Music.MusicVideoType != MusicVideoType.None);
            // SetBadge 0xADB7860 prioritizes LONG over MV. ColorPreset getters
            // 0xA5A10BC/0xA5A10DC contain the original RGB constants.
            playType.GetComponent<Image>().color = entry.Music.IsLongVersion
                ? new Color32(174, 172, 208, 255) : new Color32(154, 181, 226, 255);
            TextAt(playType, "PlayTypeBadgeLabel", entry.Music.IsLongVersion ? "LONG" :
                entry.Music.MusicVideoType == MusicVideoType.RealTimeRendering ? "3DMV" :
                entry.Music.MusicVideoType == MusicVideoType.Movie ? "2DMV" : "MV");
            SetMusicType(root, entry.Music);
            var lamp = Find(root, "NormalClearLamp").GetComponent<Image>();
            var lampSprite = _host.AnotherClearLamp(_results.GetClearLamp(entry.Id, (MusicDifficulty)entry.Difficulty));
            lamp.enabled = lampSprite != null; lamp.sprite = lampSprite;
            _jackets.TryGetValue(entry.MusicId, out var jacket);
            foreach (var name in new[] { "BackgroundMusicJacket", "FocusTargetMusicJacket" })
            {
                var image = Find(root, name).GetComponent<Image>();
                image.sprite = jacket; image.enabled = jacket != null;
            }
        }

        private static void SetMusicType(Transform root, LocalMusicEntry music)
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true)) if (text.name == "MusicTypeText") text.text = music.MusicTypeName;
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "OriginalBackground") image.enabled = music.MusicCoverType == 1;
                if (image.name == "CoverBackground") image.enabled = music.MusicCoverType != 1;
            }
        }

        public void SelectIndex(int index, bool jump = true)
        {
            if (!IsReady || _launching || index < 0 || index >= _entries.Length) return;
            var entry = _entries[index];
            if (_busy) { _requestedIndex = index; return; }
            _selected = entry;
            if (jump) _scroller.JumpToDataIndex(index, .5f, .5f, false, EnhancedScroller.TweenType.easeOutCubic, .2f);
            foreach (var pair in _cells) if (pair.Key != null && pair.Key.gameObject.activeInHierarchy) UpdateFocus(pair.Key, pair.Value);
            if (_preparedEntry != entry) StartCoroutine(PrepareSelection(entry));
        }
        private int _requestedIndex = -1;

        private IEnumerator PrepareSelection(Entry entry)
        {
            _busy = true;
            Find(_view.transform, "OkButton").GetComponent<Button>().interactable = false;
            SelectionPreparationCount++;
            yield return LoadJacket(entry);
            var root = _view.transform;
            TextAt(root, "ScrollMusicTitleText", entry.Music.Name);
            TextAt(root, "ScrollCreatorText", "作詞 " + entry.Music.LyricWriter + " / 作曲 " + entry.Music.Composer);
            var icon = Find(root, "DifficultyIcon");
            foreach (var text in icon.GetComponentsInChildren<Text>(true)) text.text = LocalMusicSelectionRuntime.FormatMusicLevel(entry.Level);
            icon.GetComponent<Image>().sprite = _sprites["difficulty" + entry.Difficulty];
            Find(root, "DifficultyLabel").GetComponent<Image>().sprite = _sprites["difficultyLabel" + entry.Difficulty];
            var stamp = Find(root, "Stamp").GetComponent<Image>();
            stamp.sprite = _sprites["stamp" + entry.NotationType]; stamp.gameObject.SetActive(true); stamp.enabled = true;
            TextAt(root, "AchievementRateValue", LocalMusicSelectionRuntime.FormatAchievementRate(_results.GetBest(entry.Id, (MusicDifficulty)entry.Difficulty)));
            SetMusicType(Find(root, "MusicInformationPanel"), entry.Music);
            LocalMusicSelectionRuntime.ApplyInformationPanelLayout(Find(root, "MusicInformationPanel"), entry.Music, (MusicDifficulty)entry.Difficulty);
            Find(root, "MusicInformationPanel").GetComponent<Image>().color = MusicSelectionPreviewRuntime.DifficultyFrameColor((MusicDifficulty)entry.Difficulty);
            foreach (var name in new[] { "MusicJacketBack", "MusicJacketFront" })
            {
                var image = Find(root, name).GetComponent<Image>(); image.sprite = _jackets[entry.MusicId]; image.color = Color.white;
            }
            Find(root, "NoImageMask").gameObject.SetActive(false);
            Find(root, "StaminaQuantity").GetComponent<Text>().text = "0";
            _preparedEntry = entry;
            _preview.SetDifficultyColor((MusicDifficulty)entry.Difficulty);
            _preview.Play(entry.MusicId, entry.Music.PreviewAcbPath, entry.Music.MusicCue);
            _busy = false;
            Find(root, "OkButton").GetComponent<Button>().interactable = true;
            if (_requestedIndex >= 0)
            {
                var next = _requestedIndex; _requestedIndex = -1;
                if (_entries[next] != entry) SelectIndex(next);
            }
        }

        private IEnumerator LoadJacket(Entry entry)
        {
            while (_loadingJackets.Contains(entry.MusicId)) yield return null;
            if (_jackets.ContainsKey(entry.MusicId)) yield break;
            _loadingJackets.Add(entry.MusicId);
            byte[] bytes = null;
            yield return StreamingAssetsRuntime.ReadBytes(entry.Music.JacketAssetPath, b => bytes = b);
            var request = AssetBundle.LoadFromMemoryAsync(bytes);
            yield return request;
            var bundle = request.assetBundle;
            if (bundle == null) throw new InvalidOperationException("AnotherNotation jacket failed: " + entry.MusicId);
            var sprites = bundle.LoadAllAssetsAsync<Sprite>();
            yield return sprites;
            var sprite = sprites.allAssets.OfType<Sprite>().FirstOrDefault();
            if (sprite == null) throw new InvalidOperationException("Empty AnotherNotation jacket: " + entry.MusicId);
            _jackets.Add(entry.MusicId, sprite);
            _jacketBundles.Add(entry.MusicId, bundle);
            _loadingJackets.Remove(entry.MusicId);
            foreach (var pair in _cells)
                if (pair.Key != null && pair.Key.gameObject.activeInHierarchy && pair.Value.MusicId == entry.MusicId)
                    ApplyCellJacket(pair.Key, sprite);
        }

        private void ToggleAuto()
        {
            _auto = !_auto;
            RefreshAuto();
        }
        private void RefreshAuto()
        {
            var auto = Find(_view.transform, "AutoSettingToggle");
            // SwitchTextToggle._onImage is the inner red circle; the outer
            // grey circle and authored label color stay visible in both states.
            auto.Find("Image").gameObject.SetActive(true);
            auto.Find("Image/Image").gameObject.SetActive(_auto);
        }
        private void RestoreBackground()
        {
            // Keep the original aspect fitter, mask, animated pattern and particles.
            _background.transform.SetParent(_view.transform, false);
            _background.transform.SetAsFirstSibling();
            foreach (var graphic in _background.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
                if (graphic.material == null || graphic.material.name != "JugonBackGround") continue;
                var material = new Material(graphic.material) { shader = Resources.Load<Shader>("Shader/UI_CommonBackGround") };
                if (material.shader == null) throw new InvalidOperationException("Missing recovered Jugon background shader");
                _backgroundMaterials.Add(material);
                graphic.material = material;
            }
            foreach (var renderer in _background.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var material = new Material(renderer.sharedMaterial) { shader = Resources.Load<Shader>("Shader/UI_JugonAlpha") };
                _backgroundMaterials.Add(material);
                renderer.sharedMaterial = material;
                renderer.enabled = false;
            }
            foreach (var particle in _background.GetComponentsInChildren<Coffee.UIExtensions.UIParticle>(true))
            {
                particle.autoScalingMode = Coffee.UIExtensions.UIParticle.AutoScalingMode.None;
                particle.transform.localScale = Vector3.one;
                particle.RefreshParticles();
            }
        }

        private void RestoreSharedTicketMachine()
        {
            // Both retail views use the same MusicSelectionInformationPanel. Use
            // the recovered ordinary prefab, including Coffee/material repairs.
            var source = Resources.Load<GameObject>("Prefabs/MusicSelectionView");
            var old = _view.transform.Find("TicketMacine");
            var replacement = Instantiate(source.transform.Find("TicketMacine").gameObject, old.parent, false);
            replacement.name = "TicketMacine";
            replacement.transform.SetSiblingIndex(old.GetSiblingIndex());
            var panel = replacement.transform.Find("MusicInformationPanel");
            var stamp = Instantiate(old.Find("MusicInformationPanel/Stamp").gameObject, panel, false);
            stamp.name = "Stamp";
            old.SetParent(null, false); old.gameObject.SetActive(false); Destroy(old.gameObject);
        }
        private void ApplyCellJacket(EnhancedScrollerCellView cell, Sprite sprite)
        {
            foreach (var name in new[] { "BackgroundMusicJacket", "FocusTargetMusicJacket" })
            {
                var image = Find(cell.transform, name).GetComponent<Image>();
                image.sprite = sprite; image.enabled = sprite != null;
            }
        }
        private void UpdateFocus(EnhancedScrollerCellView cell, Entry entry)
        {
            var focused = entry == _selected;
            var hadFocus = _focused.TryGetValue(cell, out var previous);
            if (hadFocus && previous == focused) return;
            _focused[cell] = focused;
            LocalMusicSelectionRuntime.ConfigureScrollText(Find(cell.transform, "MusicTitle"));
            LocalMusicSelectionRuntime.ConfigureScrollText(Find(cell.transform, "Vocals"));
            _host.ApplyAnotherCellLayout(cell.gameObject, focused, (MusicDifficulty)entry.Difficulty, hadFocus && IsReady);
            // EnhancedScroller owns fixed slots. Only the visual PartsBase grows;
            // neighbors receive the same +/-14 compensation as ordinary selection.
            var rect = (RectTransform)cell.transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, _cellSize);
            var layout = cell.GetComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = _cellSize;
            var mask = Find(cell.transform, "FocusMask").GetComponent<Mask>();
            if (mask != null) mask.showMaskGraphic = false;
        }
        private void LateUpdate()
        {
            if (!IsReady) return;
            var viewport = (RectTransform)_scroller.transform;
            var center = viewport.TransformPoint(viewport.rect.center);
            EnhancedScrollerCellView focus = null;
            var nearest = float.MaxValue;
            foreach (var pair in _cells)
            {
                if (!pair.Key.gameObject.activeInHierarchy || pair.Value != _selected) continue;
                var distance = Mathf.Abs(viewport.InverseTransformPoint(pair.Key.transform.TransformPoint(((RectTransform)pair.Key.transform).rect.center)).y - viewport.rect.center.y);
                if (distance < nearest) { nearest = distance; focus = pair.Key; }
            }
            foreach (var pair in _cells)
            {
                if (!pair.Key.gameObject.activeInHierarchy) continue;
                var rect = (RectTransform)pair.Key.transform;
                var distance = viewport.InverseTransformPoint(rect.TransformPoint(rect.rect.center)).y - viewport.InverseTransformPoint(center).y;
                var parts = (RectTransform)Find(rect, "PartsBase");
                var displacement = focus == null ? 0 : MusicSelectionListGeometry.FocusDisplacement(pair.Key.cellIndex, focus.cellIndex, ((RectTransform)Find(focus.transform, "PartsBase")).rect.height);
                parts.anchoredPosition = new Vector2(pair.Key == focus ? 0 : MusicSelectionListGeometry.Curve(distance), displacement);
            }
        }
        public void SetPreLiveControlsVisible(bool visible)
        {
            Find(_view.transform, "OkButton").gameObject.SetActive(visible);
            Find(_view.transform, "DifficultyIcon").gameObject.SetActive(visible);
        }
        public void SetSpectrumVisible(bool visible)
        {
            if (_view != null) Find(_view.transform, "UIParticleSpectrumViewer").gameObject.SetActive(visible);
        }
        private void OpenSettings()
        {
            if (_busy || _launching) return;
            if (_settings == null) _settings = _host.CreateAnotherSettings(() => { SetSpectrumVisible(true); RefreshAuto(); });
            SetSpectrumVisible(false);
            _settings.OpenSelectionSettings();
        }
        public void Launch()
        {
            if (_busy || _launching || _selected == null) return;
            _host.ConfirmAnotherStart(() => { _launching = true; StartCoroutine(LaunchPrepared()); });
        }
        private IEnumerator LaunchPrepared()
        {
            byte[] bytes = null;
            ChartReadCount++;
            yield return StreamingAssetsRuntime.ReadBytes(_selected.Music.Lives[0].DebugNotationAssetPath, b => bytes = b);
            if (_chart != null) Destroy(_chart);
            _chart = new TextAsset(Encoding.UTF8.GetString(bytes));
            yield return StreamingAssetsRuntime.ReadBytes(_selected.Music.Lives[0].DebugMusicConfigAssetPath, b => bytes = b);
            if (_config != null) Destroy(_config);
            _config = new TextAsset(Encoding.UTF8.GetString(bytes));
            yield return SplitLaneAssetRuntime.PrepareStreamingAssets(_chart.text);
            SplitLaneAssetRuntime.ThrowIfStreamingAssetPreparationFailed();
            _preview.Stop();
            _host.LaunchAnotherGame(new LocalMusicSelection(_selected.Music, _selected.Music.Lives[0]), _chart, _config, _jackets[_selected.MusicId], _auto);
        }
        private void OnEnable()
        {
            if (!IsReady) return;
            _launching = false; _busy = false;
            _results = new LocalResultStore(anotherNotation: true);
            _preparedEntry = null;
            if (_selected != null) SelectIndex(Array.IndexOf(_entries, _selected), false);
        }
        public void Close()
        {
            if (_busy || _launching) return;
            _preview.Stop(); IsReady = false;
            foreach (var cell in _cells.Keys) if (cell != null) _host.ReleaseAnotherCellLayout(cell.gameObject);
            Destroy(_view); _view = null;
            _closed?.Invoke();
        }
        private void OnDestroy()
        {
            if (_host != null) foreach (var cell in _cells.Keys) if (cell != null) _host.ReleaseAnotherCellLayout(cell.gameObject);
            if (_view != null) Destroy(_view);
            if (_template != null) Destroy(_template);
            if (_toolbar != null) Destroy(_toolbar);
            if (_header != null) Destroy(_header);
            if (_settings != null) Destroy(_settings);
            if (_chart != null) Destroy(_chart);
            if (_config != null) Destroy(_config);
            foreach (var material in _backgroundMaterials) Destroy(material);
            foreach (var bundle in _jacketBundles.Values) bundle.Unload(true);
            foreach (var sprite in _sprites.Values) Destroy(sprite);
            foreach (var bundle in _bundles.Values) bundle.Dispose();
        }
    }
}
