using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenWDS.Runtime
{
    public static class RecoveredLocalMusicSelectionSession
    {
        public static RecoveredLocalMusicSelection Selection { get; private set; }
        public static TextAsset ChartAsset { get; private set; }
        public static TextAsset MusicConfigAsset { get; private set; }
        public static Sprite JacketSprite { get; private set; }
        public static IReadOnlyList<RecoveredLocalMusicEntry> Musics
        {
            get;
            private set;
        }

        public static bool HasSelection =>
            Selection != null && ChartAsset != null && MusicConfigAsset != null;

        public static void Set(
            RecoveredLocalMusicSelection selection,
            TextAsset chartAsset,
            TextAsset musicConfigAsset,
            Sprite jacketSprite,
            IReadOnlyList<RecoveredLocalMusicEntry> musics)
        {
            Selection = selection ?? throw new ArgumentNullException(nameof(selection));
            ChartAsset = chartAsset != null
                ? chartAsset
                : throw new ArgumentNullException(nameof(chartAsset));
            MusicConfigAsset = musicConfigAsset != null
                ? musicConfigAsset
                : throw new ArgumentNullException(nameof(musicConfigAsset));
            JacketSprite = jacketSprite;
            Musics = musics ?? throw new ArgumentNullException(nameof(musics));
        }
    }

    /// <summary>
    /// Offline host adapter for the original Main-hosted MusicSelectionView prefab.
    /// It consumes RecoveredLocalMusicCatalog and deliberately does not pretend that
    /// LocalMusicSelection was a scene in the retail APK.
    /// </summary>
    public sealed class RecoveredLocalMusicSelectionRuntime : MonoBehaviour
    {
        [Serializable]
        private sealed class MusicJacketAsset
        {
            public long musicId;
            public Sprite jacket;
        }

        [Serializable]
        private sealed class DifficultyMarkerAsset
        {
            public RecoveredMusicDifficulty difficulty;
            public Sprite background;
        }

        private sealed class ListCellBinding
        {
            public RecoveredLocalMusicEntry music;
            public GameObject gameObject;
            public RectTransform rect;
            public int physicalIndex;
            public Sequence focusSequence;
        }

        private sealed class CachedMusicAssets
        {
            public readonly Dictionary<RecoveredMusicDifficulty, TextAsset> Charts =
                new Dictionary<RecoveredMusicDifficulty, TextAsset>();
            public TextAsset MusicConfig;
        }

        private sealed class LocalRateEntry
        {
            public RecoveredLocalMusicEntry music;
            public RecoveredLocalLiveEntry live;
            public double achievementRate;
            public double notationRate;
        }

        [SerializeField] private GameObject _view;
        [SerializeField] private GameObject _listCellPrefab;
        [SerializeField] private TextAsset _catalogJson;
        private TextAsset _musicConfigAsset;
        [SerializeField] private Sprite _jacketSprite;
        [SerializeField] private MusicJacketAsset[] _musicJackets;
        [SerializeField] private DifficultyMarkerAsset[] _difficultyMarkers;
        [SerializeField] private Sprite[] _difficultyButtonOnSprites;
        [SerializeField] private Sprite _difficultyButtonOffSprite;
        [SerializeField] private Sprite _listJacketOffsetMaskSprite;
        [SerializeField] private Sprite _listJacketTargetMaskSprite;
        [SerializeField] private Sprite[] _clearLampSprites;
        [SerializeField] private Sprite[] _rateGradeSprites;
        [SerializeField] private string _gameSceneName = "OfflineRhythmPreview";
        [SerializeField] private GameObject _musicSelectionHeaderPrefab;
        [SerializeField] private GameObject _dialogPrefab;
        [SerializeField] private GameObject _noteSpeedDialogBodyPrefab;
        [SerializeField] private GameObject _musicSortFilterDialogBodyPrefab;
        [SerializeField] private GameObject _textSideMenuButtonPrefab;
        [SerializeField] private GameObject _musicBookmarkSettingDialogBodyPrefab;
        [SerializeField] private GameObject _musicSelectionBookmarkTagPrefab;
        [SerializeField] private GameObject _playerRateDialogBodyPrefab;
        [SerializeField] private GameObject _playerRateContentPrefab;
        [SerializeField] private GameObject _playerRateSimpleContentPrefab;
        [SerializeField] private Sprite _dialogNormalButtonSprite;
        [SerializeField] private Sprite _dialogPositiveButtonSprite;
        [SerializeField] private Sprite _toggleOnSprite;
        [SerializeField] private Sprite _toggleOffSprite;
        [SerializeField] private long[] _characterBaseIconIds;
        [SerializeField] private Sprite[] _characterBaseIconSprites;
        [SerializeField] private GameObject _gameSimulationCameraPrefab;
        [SerializeField] private GameObject _gameSimulationLaneGroupPrefab;
        [SerializeField] private GameObject _gameSimulationPreviewUiPrefab;
        [SerializeField] private GameObject _gameSimulationNotePrefab;
        [SerializeField] private RenderTexture _gameSimulationRenderTexture;
        [SerializeField] private Spine.Unity.SkeletonDataAsset _curtainSkeletonData;
        [SerializeField] private Material _curtainGraphicMaterial;

        private readonly Dictionary<RecoveredMusicDifficulty, TextAsset> _charts =
            new Dictionary<RecoveredMusicDifficulty, TextAsset>();
        private readonly Dictionary<RecoveredMusicDifficulty, Button> _difficultyButtons =
            new Dictionary<RecoveredMusicDifficulty, Button>();
        // The retail MusicSelectionOneTime loader owns one ResourceProvider keyed
        // by MusicMaster.Id for the lifetime of the selection presenter. Keep the
        // same provider boundary across the local gameplay scene round-trip.
        private static readonly Dictionary<long, Sprite> JacketResourceProvider =
            new Dictionary<long, Sprite>();
        private static int SelectionStartInvocationCount;
        private static readonly Dictionary<long, CachedMusicAssets> MusicAssetCache =
            new Dictionary<long, CachedMusicAssets>();
        private readonly HashSet<long> _loadingJackets = new HashSet<long>();
        private bool _jacketBundleLoadActive;
        private bool _stopJacketLoadingRequested;
        private AssetBundle _activeJacketBundle;
        private readonly Dictionary<long, Sprite> _characterBaseIcons =
            new Dictionary<long, Sprite>();
        private readonly Dictionary<long, List<ListCellBinding>> _listCells =
            new Dictionary<long, List<ListCellBinding>>();
        private readonly List<ListCellBinding> _physicalCells =
            new List<ListCellBinding>();
        private readonly Dictionary<RecoveredMusicDifficulty, Sprite> _markerSprites =
            new Dictionary<RecoveredMusicDifficulty, Sprite>();
        private RecoveredLocalMusicCatalog _catalog;
        private RecoveredLocalMusicEntry[] _allMusics;
        private RecoveredLocalMusicSelection _selection;
        private RecoveredLocalResultStore _localResults;
        private RecoveredMusicBookmarkStore _bookmarks;
        private RecoveredUiSeRuntime _se;
        private RecoveredMusicSelectionPreviewRuntime _preview;
        private ScrollRect _listScrollRect;
        private RectTransform _listContent;
        private ListCellBinding _focusedCell;
        private bool _listDragging;
        private bool _snapPending;
        private Coroutine _snapCoroutine;
        private Coroutine _loopVelocityRestoreCoroutine;
        private bool _focusTweenObservedIntermediate;
        private GameObject _musicSelectionHeader;
        private GameObject _noteSpeedDialog;
        private GameObject _noteSpeedPreview;
        private GameObject _noteSpeedPreviewView;
        private RecoveredGameSimulationPreview _noteSpeedSimulation;
        private GameObject _hudDialog;
        private GameObject _noteSpeedDialogButtons;
        private GameObject _hudDialogButtons;
        private GameObject _noteSpeedDialogBody;
        private Text _noteSpeedValueText;
        private Text _noteOffsetValueText;
        private RecoveredSettingsSession.Snapshot _performanceSettings;
        private float _noteSpeed = 9f;
        private float _noteOffset;
        private bool _mirror;
        private bool _splitRandom;
        private bool _skipPreLiveConfirmation;
        private bool _gameStartPending;
        private int _musicSortMode;
        private int _draftMusicSortMode;
        private readonly HashSet<int> _clearLampFilters = new HashSet<int>();
        private readonly HashSet<int> _musicVideoFilters = new HashSet<int>();
        private readonly HashSet<int> _musicSizeFilters = new HashSet<int>();
        private readonly HashSet<long> _vocalFilters = new HashSet<long>();
        private readonly HashSet<int> _draftClearLampFilters = new HashSet<int>();
        private readonly HashSet<int> _draftMusicVideoFilters = new HashSet<int>();
        private readonly HashSet<int> _draftMusicSizeFilters = new HashSet<int>();
        private readonly HashSet<long> _draftVocalFilters = new HashSet<long>();
        private bool _actorAccordionExpanded = true;
        private GameObject _sortFilterDialogBody;
        private Button _sortTabButton;
        private Button _filterTabButton;
        private Sprite _sortFilterSelectedTabSprite;
        private bool _hudDialogShowsFilter;
        private bool _spectrumHiddenForDialog;
        private RecoveredMusicBookmarkFlags _bookmarkFilter;
        private RecoveredMusicBookmarkFlags _draftBookmarkFlags;
        private long _bookmarkDialogMusicId;
        private GameObject _bookmarkDialog;
        private GameObject _bookmarkDialogButtons;
        private GameObject _playerRateDialog;
        private GameObject _playerRateDialogButtons;
        private GameObject _playerRateDialogBody;
        private Button _playerRateTabButton;
        private Button _spRateTabButton;
        private Sprite _playerRateSelectedTabSprite;
        private bool _playerRateDescending = true;
        private bool _playerRateSimpleMode;
        private Sequence _arrowUpSequence;
        private Sequence _arrowDownSequence;
        private bool _isRebuildingMusicList;

        // EnhancedScroller values serialized by the retail MusicSelectionView.
        private const int LoopCopies = 3;
        private const int VisibleCellPoolPadding = 6;
        private const float OffsetCellWidth = 868.5f;
        private const float OffsetCellHeight = 168f;
        private const float TargetCellWidth = 960f;
        private const float TargetCellHeight = 196f;
        private const float OffsetJacketMaskWidth = 333f;
        private const float TargetJacketMaskWidth = 363.41f;
        private const float CellSpacing = 20f;
        private const float CellStride = OffsetCellHeight + CellSpacing;
        private const float ListLeftInset = 24f;
        private const float CellCurveScalar = 0.1042f;
        private const float CellCurveOffset = 20f;
        private const float CellCurveLimit = 100f;
        private const float SnapVelocityThreshold = 400f;
        private const float CellFocusTweenTime = 0.2f;
        // EnhancedScroller contributes the OutCubic snap while the original
        // target-view transition runs for 0.2 seconds. The local host has one
        // combined transition, so use the visible target-view duration.
        private const float SnapTweenTime = 0.2f;
        private static readonly Color32 UnselectedDifficultyTextColor =
            new Color32(83, 84, 92, 255);
        private static readonly FieldInfo ScrollRectPreviousPositionField =
            typeof(ScrollRect).GetField(
                "m_PrevPosition",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ScrollRectContentStartPositionField =
            typeof(ScrollRect).GetField(
                "m_ContentStartPosition",
                BindingFlags.Instance | BindingFlags.NonPublic);

        public RecoveredLocalMusicSelection Selection => _selection;
        public int AvailableDifficultyCount => _charts.Count;
        public int SerializedMusicAssetCount => 0;
        public int SerializedChartAssetCount => 0;
        public int BoundCellCount =>
            _catalog?.Musics != null ? _catalog.Musics.Length : 0;
        public int VisibleBoundCellCount => _listCells.Count;
        public int PhysicalCellCount => _physicalCells.Count;
        public int CachedJacketCount => JacketResourceProvider.Count;
        public int StartInvocationCount => SelectionStartInvocationCount;
        public int CatalogMusicCount =>
            _allMusics != null ? _allMusics.Length : 0;
        public long[] CatalogMusicIds =>
            _allMusics != null
                ? _allMusics.Select(music => music.Id).ToArray()
                : Array.Empty<long>();
        public long[] VisibleMusicIds =>
            _physicalCells
                .Where(cell => cell?.music != null)
                .Select(cell => cell.music.Id)
                .Distinct()
                .ToArray();
        public bool HasLoadedJacket(long musicId) =>
            JacketResourceProvider.TryGetValue(musicId, out var sprite) &&
            sprite != null;
        public int CountCatalogMusicsForActor(long actorId) =>
            _allMusics != null
                ? _allMusics.Count(
                    music => music.ActorIds != null &&
                        Array.IndexOf(music.ActorIds, actorId) >= 0)
                : 0;
        public long FocusedMusicId =>
            _focusedCell != null ? _focusedCell.music.Id : 0;
        public ScrollRect ListScrollRect => _listScrollRect;
        public RecoveredScrollTextRuntime FocusedVocalsScroll =>
            _focusedCell?.gameObject != null
                ? _focusedCell.gameObject.transform.Find(
                        "PartsBase/PartsMask/Vocals")
                    ?.GetComponent<RecoveredScrollTextRuntime>()
                : null;
        public bool IsFocusTransitionActive =>
            _focusedCell?.focusSequence != null &&
            _focusedCell.focusSequence.IsActive() &&
            _focusedCell.focusSequence.IsPlaying();
        public bool FocusTweenObservedIntermediate =>
            _focusTweenObservedIntermediate;
        public bool IsNoteSpeedDialogVisible =>
            _noteSpeedDialog != null;
        public float NoteSpeed => _noteSpeed;
        public float NoteOffset => _noteOffset;
        public bool MirrorEnabled => _mirror;
        public bool SplitRandomEnabled => _splitRandom;
        public bool SkipPreLiveConfirmation => _skipPreLiveConfirmation;
        public RecoveredMusicBookmarkFlags BookmarkFilter => _bookmarkFilter;
        public bool IsBookmarkDialogVisible => _bookmarkDialog != null;
        public bool IsPlayerRateDialogVisible => _playerRateDialog != null;
        public bool AreArrowAnimationsRunning =>
            _arrowUpSequence != null && _arrowUpSequence.IsPlaying() &&
            _arrowDownSequence != null && _arrowDownSequence.IsPlaying();
        public double PlayerRate => CalculatePlayerRate();
        public RecoveredMusicBookmarkFlags GetBookmarkFlags(long musicId) =>
            _bookmarks != null
                ? _bookmarks.Get(musicId)
                : RecoveredMusicBookmarkFlags.None;
        public bool IsMusicListRebuilding => _isRebuildingMusicList;

        public void Configure(
            GameObject view,
            GameObject listCellPrefab,
            TextAsset catalogJson,
            long[] jacketMusicIds,
            Sprite[] jackets,
            Sprite[] difficultyMarkers,
            Sprite[] difficultyButtonOnSprites,
            Sprite difficultyButtonOffSprite,
            Sprite listJacketOffsetMaskSprite,
            Sprite listJacketTargetMaskSprite,
            Sprite[] clearLampSprites,
            Sprite[] rateGradeSprites)
        {
            _view = view;
            _listCellPrefab = listCellPrefab;
            _catalogJson = catalogJson;
            if (jacketMusicIds == null)
                throw new ArgumentNullException(nameof(jacketMusicIds));
            if (jackets == null) throw new ArgumentNullException(nameof(jackets));
            if (jacketMusicIds.Length != jackets.Length)
                throw new ArgumentException(
                    "Jacket music ids and sprites must have the same length.");
            _musicJackets = new MusicJacketAsset[jackets.Length];
            for (var index = 0; index < jackets.Length; index++)
            {
                _musicJackets[index] = new MusicJacketAsset
                {
                    musicId = jacketMusicIds[index],
                    jacket = jackets[index],
                };
            }
            var difficulties = new[]
            {
                RecoveredMusicDifficulty.Normal,
                RecoveredMusicDifficulty.Hard,
                RecoveredMusicDifficulty.Extra,
                RecoveredMusicDifficulty.Stella,
                RecoveredMusicDifficulty.Olivier,
            };
            if (difficultyMarkers == null ||
                difficultyMarkers.Length != difficulties.Length)
                throw new ArgumentException(
                    "Exactly five difficulty marker sprites are required.",
                    nameof(difficultyMarkers));
            _difficultyMarkers = new DifficultyMarkerAsset[difficulties.Length];
            for (var index = 0; index < difficulties.Length; index++)
            {
                _difficultyMarkers[index] = new DifficultyMarkerAsset
                {
                    difficulty = difficulties[index],
                    background = difficultyMarkers[index],
                };
            }
            if (difficultyButtonOnSprites == null ||
                difficultyButtonOnSprites.Length != difficulties.Length)
                throw new ArgumentException(
                    "Exactly five selected difficulty sprites are required.",
                    nameof(difficultyButtonOnSprites));
            _difficultyButtonOnSprites = difficultyButtonOnSprites;
            _difficultyButtonOffSprite = difficultyButtonOffSprite != null
                ? difficultyButtonOffSprite
                : throw new ArgumentNullException(nameof(difficultyButtonOffSprite));
            _listJacketOffsetMaskSprite = listJacketOffsetMaskSprite != null
                ? listJacketOffsetMaskSprite
                : throw new ArgumentNullException(nameof(listJacketOffsetMaskSprite));
            _listJacketTargetMaskSprite = listJacketTargetMaskSprite != null
                ? listJacketTargetMaskSprite
                : throw new ArgumentNullException(nameof(listJacketTargetMaskSprite));
            if (clearLampSprites == null || clearLampSprites.Length != 3)
                throw new ArgumentException(
                    "Clear, Full Combo, and All Perfect sprites are required.",
                    nameof(clearLampSprites));
            if (rateGradeSprites == null || rateGradeSprites.Length != 9)
                throw new ArgumentException(
                    "C through SSS rate-grade sprites are required.",
                    nameof(rateGradeSprites));
            _clearLampSprites = clearLampSprites;
            _rateGradeSprites = rateGradeSprites;
        }

        public void ConfigureHud(
            GameObject musicSelectionHeaderPrefab,
            GameObject dialogPrefab,
            GameObject noteSpeedDialogBodyPrefab,
            GameObject musicSortFilterDialogBodyPrefab,
            GameObject textSideMenuButtonPrefab,
            GameObject musicBookmarkSettingDialogBodyPrefab,
            GameObject musicSelectionBookmarkTagPrefab,
            GameObject playerRateDialogBodyPrefab,
            GameObject playerRateContentPrefab,
            GameObject playerRateSimpleContentPrefab,
            Sprite dialogNormalButtonSprite,
            Sprite dialogPositiveButtonSprite,
            Sprite toggleOnSprite,
            Sprite toggleOffSprite,
            GameObject gameSimulationCameraPrefab,
            GameObject gameSimulationLaneGroupPrefab,
            GameObject gameSimulationPreviewUiPrefab,
            GameObject gameSimulationNotePrefab,
            RenderTexture gameSimulationRenderTexture,
            Spine.Unity.SkeletonDataAsset curtainSkeletonData,
            Material curtainGraphicMaterial)
        {
            _musicSelectionHeaderPrefab = musicSelectionHeaderPrefab;
            _dialogPrefab = dialogPrefab;
            _noteSpeedDialogBodyPrefab = noteSpeedDialogBodyPrefab;
            _musicSortFilterDialogBodyPrefab = musicSortFilterDialogBodyPrefab;
            _textSideMenuButtonPrefab = textSideMenuButtonPrefab;
            _musicBookmarkSettingDialogBodyPrefab =
                musicBookmarkSettingDialogBodyPrefab;
            _musicSelectionBookmarkTagPrefab = musicSelectionBookmarkTagPrefab;
            _playerRateDialogBodyPrefab = playerRateDialogBodyPrefab;
            _playerRateContentPrefab = playerRateContentPrefab;
            _playerRateSimpleContentPrefab = playerRateSimpleContentPrefab;
            _dialogNormalButtonSprite = dialogNormalButtonSprite;
            _dialogPositiveButtonSprite = dialogPositiveButtonSprite;
            _toggleOnSprite = toggleOnSprite;
            _toggleOffSprite = toggleOffSprite;
            _gameSimulationCameraPrefab = gameSimulationCameraPrefab;
            _gameSimulationLaneGroupPrefab = gameSimulationLaneGroupPrefab;
            _gameSimulationPreviewUiPrefab = gameSimulationPreviewUiPrefab;
            _gameSimulationNotePrefab = gameSimulationNotePrefab;
            _gameSimulationRenderTexture = gameSimulationRenderTexture;
            _curtainSkeletonData = curtainSkeletonData;
            _curtainGraphicMaterial = curtainGraphicMaterial;
        }

        public void ConfigureCharacterBaseIcons(long[] ids, Sprite[] sprites)
        {
            if (ids == null || sprites == null || ids.Length != sprites.Length)
                throw new ArgumentException(
                    "Character-base icon IDs and sprites must have equal lengths.");
            _characterBaseIconIds = ids.ToArray();
            _characterBaseIconSprites = sprites.ToArray();
            RebuildCharacterBaseIconLookup();
        }

        private void RebuildCharacterBaseIconLookup()
        {
            if (_characterBaseIconIds == null ||
                _characterBaseIconSprites == null ||
                _characterBaseIconIds.Length != _characterBaseIconSprites.Length)
                throw new InvalidOperationException(
                    "Serialized character-base icon IDs and sprites must have equal lengths.");
            _characterBaseIcons.Clear();
            for (var index = 0; index < _characterBaseIconIds.Length; index++)
            {
                if (_characterBaseIconIds[index] <= 0 ||
                    _characterBaseIconSprites[index] == null)
                    throw new InvalidOperationException(
                        "Every character-base icon requires an ID and Sprite.");
                _characterBaseIcons.Add(
                    _characterBaseIconIds[index],
                    _characterBaseIconSprites[index]);
            }
        }

        public bool IsInitialized { get; private set; }

        private IEnumerator Start()
        {
            SelectionStartInvocationCount++;
            if (_view == null || _listCellPrefab == null || _catalogJson == null)
                throw new InvalidOperationException(
                    "Local MusicSelection host is missing a required serialized asset.");
            RebuildCharacterBaseIconLookup();
            var rootCanvasGroup = _view.GetComponent<CanvasGroup>();
            if (rootCanvasGroup != null)
            {
                // The retail Presenter fades the Addressable view in from its
                // authored alpha=0. The local host owns that Presenter boundary.
                rootCanvasGroup.alpha = 1f;
                rootCanvasGroup.interactable = true;
                rootCanvasGroup.blocksRaycasts = true;
            }
            _se = GetComponent<RecoveredUiSeRuntime>();
            if (_se == null) _se = gameObject.AddComponent<RecoveredUiSeRuntime>();
            BuildHud();
            _catalog = RecoveredLocalMusicCatalog.FromJson(_catalogJson.text);
            _allMusics = (RecoveredLocalMusicEntry[])_catalog.Musics.Clone();
            SelectAllPermanentActors(_vocalFilters);
            _localResults = new RecoveredLocalResultStore();
            _bookmarks = new RecoveredMusicBookmarkStore();
            if (_catalog.Musics.Length == 0)
                throw new InvalidOperationException("Local music catalog contains no music.");
            if (_musicJackets != null)
            {
                foreach (var item in _musicJackets)
                {
                    if (item != null && item.jacket != null)
                        JacketResourceProvider[item.musicId] = item.jacket;
                }
            }
            if (_difficultyMarkers != null)
            {
                foreach (var item in _difficultyMarkers)
                {
                    if (item != null && item.background != null)
                        _markerSprites[item.difficulty] = item.background;
                }
            }
            _preview = gameObject.AddComponent<RecoveredMusicSelectionPreviewRuntime>();
            _preview.Configure(_view.transform);
            var initialMusic = _catalog.Musics[0];
            var initialDifficulty = RecoveredMusicDifficulty.Stella;
            if (RecoveredLocalMusicSelectionSession.HasSelection)
            {
                var returnedSelection =
                    RecoveredLocalMusicSelectionSession.Selection;
                var returnedMusic = _catalog.Musics.FirstOrDefault(
                    item => item.Id == returnedSelection.Music.Id);
                if (returnedMusic != null)
                {
                    initialMusic = returnedMusic;
                    initialDifficulty = returnedSelection.Live.Difficulty;
                }
            }
            yield return LoadMusicAssets(initialMusic);
            BindView(initialMusic);
            if (!_charts.ContainsKey(initialDifficulty) ||
                !TryGetLive(initialMusic, initialDifficulty, out _))
            {
                foreach (var difficulty in _charts.Keys)
                {
                    initialDifficulty = difficulty;
                    break;
                }
            }
            SelectDifficulty(initialDifficulty, false);
            _catalog.Musics = SortMusicsForHud(_catalog.Musics);
            BindMusicList();
            BindSelectionAuxiliaryControls();
            RefreshPlayerRateHeader();
            FocusCell(FindVisibleCell(initialMusic.Id));
            _preview.Play(initialMusic.Id);
            RecoveredLocalGameFlowRouter.EnsureExists();
            IsInitialized = true;
        }

        private void OnEnable()
        {
            _gameStartPending = false;
            _stopJacketLoadingRequested = false;
            if (_view == null) return;
            // The retained scene still owns the snapshot read before the live.
            // Reload the durable result before rebinding any score/lamp/rating UI.
            _localResults = new RecoveredLocalResultStore();
            SetPreLiveUnderlyingControlsVisible(true);
            SetSpectrumVisibleForDialog(true);
            if (_listContent != null)
            {
                // RebindListCell clears _focusedCell, even when the retained
                // cell is rebound to the same song. Restore the focus from the
                // selection entity after refreshing the recycled cells.
                UpdateVisibleCellBindings(true);
                if (_selection != null)
                {
                    FocusCell(FindVisibleCell(_selection.Music.Id));
                    _preview?.Play(_selection.Music.Id);
                }
                RefreshPlayerRateHeader();
                RefreshScorePanel();
            }
        }

        private void OnDisable()
        {
            _preview?.Stop();
            StopAllCoroutines();
            if (_activeJacketBundle != null)
            {
                _activeJacketBundle.Unload(true);
                _activeJacketBundle = null;
            }
            _jacketBundleLoadActive = false;
            _loadingJackets.Clear();
        }

        private void BuildHud()
        {
            if (_musicSelectionHeaderPrefab == null ||
                _dialogPrefab == null ||
                _noteSpeedDialogBodyPrefab == null ||
                _musicSortFilterDialogBodyPrefab == null ||
                _textSideMenuButtonPrefab == null ||
                _musicBookmarkSettingDialogBodyPrefab == null ||
                _musicSelectionBookmarkTagPrefab == null ||
                _playerRateDialogBodyPrefab == null ||
                _playerRateContentPrefab == null ||
                _playerRateSimpleContentPrefab == null ||
                _dialogNormalButtonSprite == null ||
                _dialogPositiveButtonSprite == null ||
                _toggleOnSprite == null ||
                _toggleOffSprite == null ||
                _gameSimulationCameraPrefab == null ||
                _gameSimulationLaneGroupPrefab == null ||
                _gameSimulationPreviewUiPrefab == null ||
                _gameSimulationNotePrefab == null ||
                _gameSimulationRenderTexture == null ||
                _curtainSkeletonData == null ||
                _curtainGraphicMaterial == null)
            {
                throw new InvalidOperationException(
                    "MusicSelection HUD and curtain assets are incomplete.");
            }

            var canvas = _view.transform.parent;
            _musicSelectionHeader = Instantiate(
                _musicSelectionHeaderPrefab, canvas, false);
            _musicSelectionHeader.name = "MusicSelectionHeaderView";
            SetHudStamina(_musicSelectionHeader);
            BindHudControls();
            RefreshHudFilterIcon();
        }

        private static void SetHudStamina(GameObject root)
        {
            SetNamedText(root, "CurrentStaminaText", "-");
            SetNamedText(root, "MaxStaminaText", string.Empty);
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.transform.parent != null &&
                    text.transform.parent.name == "StaminaDataPanel" &&
                    text.name == "Text")
                    text.text = string.Empty;
            }
        }

        private static void SetNamedText(
            GameObject root, string objectName, string value)
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.name == objectName) text.text = value;
            }
        }

        private void BindHudControls()
        {
            SetNamedText(_musicSelectionHeader, "RateText", "0.00");
            BindNamedButton(
                _musicSelectionHeader.transform,
                "FilterSettingButton",
                () => OpenMusicSortFilterDialog(true));
            BindNamedButton(
                _musicSelectionHeader.transform,
                "SortButton",
                () => OpenMusicSortFilterDialog(false));

            var rate = FindDescendant(
                _musicSelectionHeader.transform, "RateDataPanel");
            BindNamedButton(rate, "InformationButton", OpenPlayerRateDialog);
            var stamina = FindDescendant(
                _musicSelectionHeader.transform, "StaminaDataPanel");
            DisableNamedButton(stamina, "PlusButton");
            DisableNamedButton(stamina, "InformationButton");
            RefreshHudSortLabel();
            RefreshHudFilterIcon();
        }

        private static void DisableNamedButton(Transform root, string name)
        {
            if (root == null) return;
            var target = FindDescendant(root, name);
            var button = target != null ? target.GetComponent<Button>() : null;
            if (button != null) button.interactable = false;
        }

        private void OpenMusicSortFilterDialog(bool showFilter)
        {
            if (_hudDialog != null || _noteSpeedDialog != null) return;
            _se.Play(RecoveredUiSeRuntime.Cue.ButtonGo);
            SetSpectrumVisibleForDialog(false);
            _draftMusicSortMode = _musicSortMode;
            CopySet(_clearLampFilters, _draftClearLampFilters);
            CopySet(_musicVideoFilters, _draftMusicVideoFilters);
            CopySet(_musicSizeFilters, _draftMusicSizeFilters);
            CopySet(_vocalFilters, _draftVocalFilters);
            _hudDialog = CreateCommonDialog(
                "ソート/フィルタ", new Vector2(1328f, 996f));
            _hudDialog.name = "RecoveredMusicSortFilterDialog";
            var body = FindDescendant(_hudDialog.transform, "Body");
            _sortFilterDialogBody = Instantiate(
                _musicSortFilterDialogBodyPrefab, body, false);
            _sortFilterDialogBody.name = "MusicSortFilterDialogBody";
            BuildSortFilterSideMenu(showFilter);
            ConfigureCommonDialogButton(
                _hudDialog, "FirstButtonParent", "FirstButton",
                "キャンセル", CloseHudDialog, false);
            ConfigureCommonDialogButton(
                _hudDialog, "SecondButtonParent", "SecondButton",
                "OK", ApplyHudSortFilter, true);
            var third = FindDescendant(_hudDialog.transform, "ThirdButtonParent");
            if (third != null) third.gameObject.SetActive(false);
            _hudDialogButtons = LayoutCommonDialogButtons(_hudDialog);
            _hudDialog.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
        }

        private static void CopySet<T>(HashSet<T> source, HashSet<T> destination)
        {
            destination.Clear();
            destination.UnionWith(source);
        }

        private void BuildSortFilterSideMenu(bool showFilter)
        {
            var sideMenu = FindDescendant(
                _sortFilterDialogBody.transform, "TextSideMenuPanel");
            var content = sideMenu != null
                ? FindDescendant(sideMenu, "Content")
                : null;
            if (sideMenu == null || content == null)
                throw new InvalidOperationException(
                    "Recovered sort/filter side menu is incomplete.");
            sideMenu.gameObject.SetActive(true);
            var selectedObject = FindDescendant(content, "SelectedObject");
            var selectedImage = selectedObject != null
                ? selectedObject.GetComponent<Image>()
                : null;
            _sortFilterSelectedTabSprite =
                selectedImage != null ? selectedImage.sprite : null;
            if (selectedObject != null) selectedObject.gameObject.SetActive(false);
            var stripe = FindDescendant(content, "SideMenuHorizontalStripe");
            if (stripe != null) stripe.gameObject.SetActive(false);
            var contentRect = content.GetComponent<RectTransform>();
            if (contentRect != null) contentRect.sizeDelta = new Vector2(0f, 232f);
            _sortTabButton = CreateSortFilterTab(
                content, "RecoveredSortTab", "ソート");
            _filterTabButton = CreateSortFilterTab(
                content, "RecoveredFilterTab", "フィルタ");
            _sortTabButton.onClick.AddListener(
                () =>
                {
                    _se.Play(RecoveredUiSeRuntime.Cue.ButtonGo);
                    ShowSortFilterTab(false);
                });
            _filterTabButton.onClick.AddListener(
                () =>
                {
                    _se.Play(RecoveredUiSeRuntime.Cue.ButtonGo);
                    ShowSortFilterTab(true);
                });
            ShowSortFilterTab(showFilter);
        }

        private Button CreateSortFilterTab(
            Transform parent, string name, string label)
        {
            var tab = Instantiate(_textSideMenuButtonPrefab, parent, false);
            tab.name = name;
            var rect = tab.GetComponent<RectTransform>();
            var image = tab.GetComponent<Image>();
            var button = tab.GetComponent<Button>();
            var text = FindDescendant(tab.transform, "Label")
                ?.GetComponent<Text>();
            if (rect == null || image == null || button == null || text == null)
                throw new InvalidOperationException(
                    "Original TextSideMenuButton prefab is incomplete.");
            rect.sizeDelta = new Vector2(260f, 116f);
            text.text = label;
            button.targetGraphic = image;
            foreach (var animator in tab.GetComponentsInChildren<Animator>(true))
                animator.enabled = false;
            return button;
        }

        private void ShowSortFilterTab(bool showFilter)
        {
            if (_sortFilterDialogBody == null) return;
            _hudDialogShowsFilter = showFilter;
            var sortPanel = FindDescendant(
                _sortFilterDialogBody.transform, "SortPanel");
            var filterPanel = FindDescendant(
                _sortFilterDialogBody.transform, "FilterPanel");
            if (sortPanel != null) sortPanel.gameObject.SetActive(!showFilter);
            if (filterPanel != null) filterPanel.gameObject.SetActive(showFilter);
            SetSortFilterTabVisual(_sortTabButton, !showFilter);
            SetSortFilterTabVisual(_filterTabButton, showFilter);
            if (showFilter)
                BindLocalFilterPanel(filterPanel);
            else
                BindLocalSortPanel(sortPanel);
            Canvas.ForceUpdateCanvases();
        }

        private void SetSortFilterTabVisual(Button button, bool selected)
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = selected ? _sortFilterSelectedTabSprite : null;
                image.color = selected
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0f);
            }
            var text = FindDescendant(button.transform, "Label")
                ?.GetComponent<Text>();
            if (text != null)
                text.color = selected
                    ? Color.white
                    : new Color32(86, 88, 103, 255);
        }

        private void BindLocalSortPanel(Transform panel)
        {
            if (panel == null) return;
            var names = new[]
            {
                "LevelSortToggleButton",
                "NameSortToggleButton",
                "ReleaseDateSortToggleButton",
                "RateOfSingleScoreSortToggleButton",
                "AchievementRateToggleButton",
                "StaminaConsumptionToggleButton",
            };
            for (var index = 0; index < names.Length; index++)
            {
                var captured = index;
                EnsureButton(
                    FindDescendant(panel, names[index]),
                    () =>
                    {
                        _draftMusicSortMode = captured;
                        RefreshSortSelection(panel, names);
                    });
            }
            BindNamedButton(panel, "ResetButton", () =>
            {
                _draftMusicSortMode = 0;
                RefreshSortSelection(panel, names);
            }, true);
            RefreshSortSelection(panel, names);
        }

        private void RefreshSortSelection(Transform panel, string[] names)
        {
            for (var index = 0; index < names.Length; index++)
            {
                var root = FindDescendant(panel, names[index]);
                var on = root != null ? FindDescendant(root, "On") : null;
                var off = root != null ? FindDescendant(root, "Off") : null;
                if (on != null) on.gameObject.SetActive(index == _draftMusicSortMode);
                if (off != null) off.gameObject.SetActive(index != _draftMusicSortMode);
            }
        }

        private void BindLocalFilterPanel(Transform panel)
        {
            if (panel == null) return;
            var musicType = FindDescendant(panel, "MusicTypeFilterPanel");
            if (musicType != null) musicType.gameObject.SetActive(false);
            BindFilterCategory(
                FindDescendant(panel, "ClearLampFilterPanel"),
                _draftClearLampFilters);
            BindFilterCategory(
                FindDescendant(panel, "MvFilterPanel"),
                _draftMusicVideoFilters);
            BindFilterCategory(
                FindDescendant(panel, "MusicSizeTypeFilterPanel"),
                _draftMusicSizeFilters);
            BindVocalFilterCategory(
                panel, FindDescendant(panel, "ActorFilterCategoryCell"));
            var resetButton = FindDescendant(panel, "ResetButton");
            if (resetButton != null) resetButton.gameObject.SetActive(true);
            EnsureButton(resetButton, () =>
            {
                _draftClearLampFilters.Clear();
                _draftMusicVideoFilters.Clear();
                _draftMusicSizeFilters.Clear();
                SelectAllPermanentActors(_draftVocalFilters);
                RefreshAllFilterCategories(panel);
            });
            RefreshAllFilterCategories(panel);
        }

        private void RefreshAllFilterCategories(Transform panel)
        {
            RefreshBoundFilterCategory(
                FindDescendant(panel, "ClearLampFilterPanel"),
                _draftClearLampFilters);
            RefreshBoundFilterCategory(
                FindDescendant(panel, "MvFilterPanel"),
                _draftMusicVideoFilters);
            RefreshBoundFilterCategory(
                FindDescendant(panel, "MusicSizeTypeFilterPanel"),
                _draftMusicSizeFilters);
            RefreshVocalFilterCategory(
                FindDescendant(panel, "ActorFilterCategoryCell"));
            var resetActive = FindDescendant(
                FindDescendant(panel, "ResetButton"), "ResetOnActive");
            if (resetActive != null)
                resetActive.gameObject.SetActive(
                    _draftClearLampFilters.Count > 0 ||
                    _draftMusicVideoFilters.Count > 0 ||
                    _draftMusicSizeFilters.Count > 0 ||
                    !AreAllPermanentActorsSelected(_draftVocalFilters));
            var content = FindDescendant(panel, "Content") as RectTransform;
            if (content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private static void RefreshBoundFilterCategory(
            Transform category, HashSet<int> selected)
        {
            if (category == null) return;
            var group = FindDescendant(category, "FilterCheckBoxGroup");
            if (group == null) return;
            var options = new List<Transform>();
            for (var index = 0; index < group.childCount; index++)
            {
                var child = group.GetChild(index);
                if (child.name == "FilterAllButton" ||
                    child.name.StartsWith("FilterSideStoryButton",
                        StringComparison.Ordinal) ||
                    child.name.StartsWith("FilterSideStoryButton",
                        StringComparison.Ordinal))
                    options.Add(child);
            }
            RefreshFilterCategory(options, selected);
        }

        private void BindVocalFilterCategory(
            Transform filterPanel, Transform category)
        {
            if (category == null) return;
            category.gameObject.SetActive(true);
            var title = FindDescendant(category, "Title");
            var titleText = title != null
                ? title.GetComponentInChildren<Text>(true)
                : null;
            if (titleText == null || titleText.text != "歌唱アクター")
                throw new InvalidOperationException(
                    "Original ActorCell title must remain 歌唱アクター.");
            var checkboxGroup = FindDescendant(category, "FilterCheckBoxGroup");
            if (checkboxGroup == null || _catalog.CharacterBases == null)
                throw new InvalidOperationException(
                    "Original ActorFilterCategoryCell is incomplete.");

            var companies = new[] { 1, 2, 3, 4 };
            var actorGroups = DirectChildren(checkboxGroup, "ActorGroup");
            var actorLines = DirectChildren(checkboxGroup, "Line");
            if (actorGroups.Count != 5)
                throw new InvalidOperationException(
                    "Original ActorCell must contain five ActorGroup objects.");
            if (actorLines.Count != 5)
                throw new InvalidOperationException(
                    "Original ActorCell must contain five company separator lines.");

            var all = FindDescendant(checkboxGroup, "FilterAllButton");
            EnsureButton(all, () =>
            {
                if (AreAllPermanentActorsSelected(_draftVocalFilters))
                    _draftVocalFilters.Clear();
                else
                    SelectAllPermanentActors(_draftVocalFilters);
                RefreshAllFilterCategories(filterPanel);
            });
            var actorReset = FindDescendant(checkboxGroup, "FilterResetButton");
            if (actorReset != null)
                actorReset.gameObject.SetActive(false);
            var allow = FindDescendant(title, "AllowButton");
            EnsureButton(allow, () =>
            {
                _actorAccordionExpanded = !_actorAccordionExpanded;
                RefreshAllFilterCategories(filterPanel);
            });

            for (var companyIndex = 0;
                 companyIndex < companies.Length;
                 companyIndex++)
            {
                var company = companies[companyIndex];
                var characters = _catalog.CharacterBases
                    .Where(character => character.Company == company)
                    .OrderBy(character => character.Id)
                    .ToArray();
                var group = actorGroups[companyIndex];
                var slots = DirectChildren(group, "FilterAttributeButton");
                if (slots.Count != 6)
                    throw new InvalidOperationException(
                        "Every original ActorGroup must retain six authored slots.");
                for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
                {
                    var slot = slots[slotIndex];
                    if (slotIndex >= characters.Length)
                    {
                        slot.gameObject.SetActive(false);
                        continue;
                    }
                    var character = characters[slotIndex];
                    slot.gameObject.SetActive(true);
                    var name = FindDescendant(slot, "CharacterNameText")
                        ?.GetComponent<Text>();
                    if (name != null) name.text = character.ShortName;
                    var portrait = FindDescendant(
                        FindDescendant(slot, "CharacterIcon"), "Image")
                        ?.GetComponent<Image>();
                    if (portrait == null ||
                        !_characterBaseIcons.TryGetValue(
                            character.Id, out var portraitSprite))
                        throw new InvalidOperationException(
                            $"Original CharacterBases Sprite {character.Id} is missing.");
                    portrait.sprite = portraitSprite;
                    portrait.enabled = true;
                    var actorId = character.Id;
                    EnsureButton(slot, () =>
                    {
                        if (!_draftVocalFilters.Add(actorId))
                            _draftVocalFilters.Remove(actorId);
                        RefreshAllFilterCategories(filterPanel);
                    });
                }
                var companyButton = FindCompanyButton(
                    checkboxGroup, companyIndex);
                if (companyButton != null)
                {
                    companyButton.gameObject.SetActive(characters.Length > 0);
                    var companyActorIds =
                        characters.Select(character => character.Id).ToArray();
                    EnsureButton(companyButton, () =>
                    {
                        var allSelected = companyActorIds.Length > 0 &&
                            companyActorIds.All(
                                actorId => _draftVocalFilters.Contains(actorId));
                        foreach (var actorId in companyActorIds)
                        {
                            if (allSelected)
                                _draftVocalFilters.Remove(actorId);
                            else
                                _draftVocalFilters.Add(actorId);
                        }
                        RefreshAllFilterCategories(filterPanel);
                    });
                }
                group.gameObject.SetActive(
                    _actorAccordionExpanded && characters.Length > 0);
                actorLines[companyIndex].gameObject.SetActive(
                    _actorAccordionExpanded && characters.Length > 0);
            }
            // Company 900 is the collaboration entry authored in the retail
            // prefab. MusicSelection passes only the four permanent companies.
            actorGroups[4].gameObject.SetActive(false);
            actorLines[4].gameObject.SetActive(false);
            var collaborationButton = FindDescendant(
                checkboxGroup, "FilterLoveLiveSunshineButton (4)");
            if (collaborationButton != null)
                collaborationButton.gameObject.SetActive(false);
            RefreshVocalFilterCategory(category);
        }

        private static List<Transform> DirectChildren(
            Transform parent, string namePrefix)
        {
            var result = new List<Transform>();
            if (parent == null) return result;
            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (child.name.StartsWith(namePrefix, StringComparison.Ordinal))
                    result.Add(child);
            }
            return result;
        }

        private static Transform FindCompanyButton(
            Transform checkboxGroup, int companyIndex)
        {
            var prefix = companyIndex == 0
                ? "FilterGekidanButton"
                : $"FilterGekidanButton ({companyIndex})";
            for (var index = 0; index < checkboxGroup.childCount; index++)
            {
                var child = checkboxGroup.GetChild(index);
                if (child.name == prefix) return child;
            }
            return null;
        }

        private void RefreshVocalFilterCategory(Transform category)
        {
            if (category == null || _catalog.CharacterBases == null) return;
            var checkboxGroup = FindDescendant(category, "FilterCheckBoxGroup");
            if (checkboxGroup == null) return;
            var all = FindDescendant(
                FindDescendant(checkboxGroup, "FilterAllButton"), "OnActive");
            if (all != null)
                all.gameObject.SetActive(
                    AreAllPermanentActorsSelected(_draftVocalFilters));
            var actorReset = FindDescendant(
                checkboxGroup, "FilterResetButton");
            if (actorReset != null)
                actorReset.gameObject.SetActive(false);

            var companies = new[] { 1, 2, 3, 4 };
            var actorGroups = DirectChildren(checkboxGroup, "ActorGroup");
            var actorLines = DirectChildren(checkboxGroup, "Line");
            for (var companyIndex = 0;
                 companyIndex < companies.Length;
                 companyIndex++)
            {
                var characters = _catalog.CharacterBases
                    .Where(character =>
                        character.Company == companies[companyIndex])
                    .OrderBy(character => character.Id)
                    .ToArray();
                var slots = DirectChildren(
                    actorGroups[companyIndex], "FilterAttributeButton");
                for (var slotIndex = 0;
                     slotIndex < slots.Count && slotIndex < characters.Length;
                     slotIndex++)
                {
                    var active = FindDescendant(slots[slotIndex], "OnActive");
                    if (active != null)
                        active.gameObject.SetActive(
                            _draftVocalFilters.Contains(
                                characters[slotIndex].Id));
                }
                var companyButton = FindCompanyButton(
                    checkboxGroup, companyIndex);
                if (companyButton != null)
                    companyButton.gameObject.SetActive(
                        _actorAccordionExpanded && characters.Length > 0);
                var companyActive = companyButton != null
                    ? FindDescendant(companyButton, "OnActive")
                    : null;
                if (companyActive != null)
                    companyActive.gameObject.SetActive(
                        characters.Length > 0 &&
                        characters.All(character =>
                            _draftVocalFilters.Contains(character.Id)));
                actorGroups[companyIndex].gameObject.SetActive(
                    _actorAccordionExpanded &&
                    characters.Length > 0);
                if (companyIndex < actorLines.Count)
                    actorLines[companyIndex].gameObject.SetActive(
                        _actorAccordionExpanded &&
                        characters.Length > 0);
            }
            if (actorGroups.Count > companies.Length)
                actorGroups[companies.Length].gameObject.SetActive(false);
            if (actorLines.Count > companies.Length)
                actorLines[companies.Length].gameObject.SetActive(false);
            var collaborationButton = FindDescendant(
                checkboxGroup, "FilterLoveLiveSunshineButton (4)");
            if (collaborationButton != null)
                collaborationButton.gameObject.SetActive(false);
            var arrow = FindDescendant(
                FindDescendant(category, "AllowButton"), "ArrowImage");
            if (arrow != null)
                arrow.localRotation = Quaternion.Euler(
                    0f, 0f, _actorAccordionExpanded ? 180f : 0f);
        }

        private void BindFilterCategory(
            Transform category,
            HashSet<int> selected)
        {
            if (category == null) return;
            category.gameObject.SetActive(true);
            var group = FindDescendant(category, "FilterCheckBoxGroup");
            if (group == null) return;
            var options = new List<Transform>();
            for (var index = 0; index < group.childCount; index++)
            {
                var child = group.GetChild(index);
                if (child.name == "FilterAllButton" ||
                    child.name.StartsWith("FilterSideStoryButton",
                        StringComparison.Ordinal))
                    options.Add(child);
            }
            for (var index = 0; index < options.Count; index++)
            {
                var value = index - 1;
                var captured = value;
                var option = options[index];
                EnsureButton(option, () =>
                {
                    if (captured < 0)
                        selected.Clear();
                    else
                    {
                        selected.Clear();
                        selected.Add(captured);
                    }
                    RefreshFilterCategory(options, selected);
                });
            }
            RefreshFilterCategory(options, selected);
        }

        private static void RefreshFilterCategory(
            List<Transform> options, HashSet<int> selected)
        {
            for (var index = 0; index < options.Count; index++)
            {
                var active = FindDescendant(options[index], "OnActive");
                if (active != null)
                    active.gameObject.SetActive(
                        index == 0 ? selected.Count == 0 : selected.Contains(index - 1));
            }
        }

        private void EnsureButton(
            Transform root, UnityEngine.Events.UnityAction action)
        {
            if (root == null) return;
            var image = root.GetComponent<Image>();
            if (image == null)
            {
                image = root.gameObject.AddComponent<Image>();
                image.color = Color.clear;
            }
            image.raycastTarget = true;
            var button = root.GetComponent<Button>();
            if (button == null) button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                _se.Play(RecoveredUiSeRuntime.Cue.ButtonGo);
                action();
            });
        }

        private void SelectAllPermanentActors(HashSet<long> destination)
        {
            destination.Clear();
            if (_catalog?.CharacterBases == null) return;
            destination.UnionWith(_catalog.CharacterBases
                .Where(character =>
                    character.Company >= 1 && character.Company <= 4)
                .Select(character => character.Id));
        }

        private bool AreAllPermanentActorsSelected(HashSet<long> selected)
        {
            if (_catalog?.CharacterBases == null) return false;
            var permanentActors = _catalog.CharacterBases
                .Where(character =>
                    character.Company >= 1 && character.Company <= 4)
                .Select(character => character.Id)
                .ToArray();
            return permanentActors.Length > 0 &&
                permanentActors.All(selected.Contains);
        }

        private void CloseHudDialog()
        {
            if (_hudDialog == null) return;
            _se.Play(RecoveredUiSeRuntime.Cue.ButtonBack);
            Destroy(_hudDialog);
            _hudDialog = null;
            if (_hudDialogButtons != null) Destroy(_hudDialogButtons);
            _hudDialogButtons = null;
            _sortFilterDialogBody = null;
            _sortTabButton = null;
            _filterTabButton = null;
            SetSpectrumVisibleForDialog(true);
        }

        private void ApplyHudSortFilter()
        {
            if (_hudDialog == null) return;
            _se.Play(RecoveredUiSeRuntime.Cue.ButtonGo);
            _musicSortMode = _draftMusicSortMode;
            RefreshHudSortLabel();
            CopySet(_draftClearLampFilters, _clearLampFilters);
            CopySet(_draftMusicVideoFilters, _musicVideoFilters);
            CopySet(_draftMusicSizeFilters, _musicSizeFilters);
            CopySet(_draftVocalFilters, _vocalFilters);
            Destroy(_hudDialog);
            _hudDialog = null;
            if (_hudDialogButtons != null) Destroy(_hudDialogButtons);
            _hudDialogButtons = null;
            _sortFilterDialogBody = null;
            _sortTabButton = null;
            _filterTabButton = null;
            SetSpectrumVisibleForDialog(true);
            StartCoroutine(RebuildFilteredMusicList());
        }

        private IEnumerator RebuildFilteredMusicList()
        {
            _isRebuildingMusicList = true;
            var selectedId = _selection != null ? _selection.Music.Id : 0;
            var selectedDifficulty = _selection != null
                ? _selection.Live.Difficulty
                : RecoveredMusicDifficulty.Stella;
            var filtered = _allMusics.Where(music =>
            {
                if (_bookmarkFilter != RecoveredMusicBookmarkFlags.None &&
                    !_bookmarks.Contains(music.Id, _bookmarkFilter))
                    return false;
                if (_clearLampFilters.Count > 0 &&
                    !MatchesClearLampFilter(music))
                    return false;
                if (_musicVideoFilters.Count > 0)
                {
                    var hasMusicVideo =
                        music.MusicVideoType != RecoveredMusicVideoType.None;
                    if (!_musicVideoFilters.Contains(hasMusicVideo ? 0 : 1))
                        return false;
                }
                if (_musicSizeFilters.Count > 0 &&
                    !_musicSizeFilters.Contains(music.IsLongVersion ? 1 : 0))
                    return false;
                if (!AreAllPermanentActorsSelected(_vocalFilters))
                {
                    if (music.ActorIds == null ||
                        !_vocalFilters.Overlaps(music.ActorIds))
                        return false;
                }
                return true;
            }).ToArray();
            if (filtered.Length == 0 &&
                _bookmarkFilter == RecoveredMusicBookmarkFlags.None)
                filtered = (RecoveredLocalMusicEntry[])_allMusics.Clone();
            filtered = SortMusicsForHud(filtered);
            _catalog.Musics = filtered;
            if (_listContent != null) Destroy(_listContent.gameObject);
            _listCells.Clear();
            _physicalCells.Clear();
            _focusedCell = null;
            yield return null;
            if (filtered.Length == 0)
            {
                var noMusic = _view.transform.Find("NoMusicPanel");
                if (noMusic != null)
                {
                    noMusic.gameObject.SetActive(true);
                    var noMusicGroup = noMusic.GetComponent<CanvasGroup>();
                    if (noMusicGroup != null) noMusicGroup.alpha = 1f;
                }
                var defaultPanels = _view.transform.Find("DefaultLivePanels");
                if (defaultPanels != null)
                    defaultPanels.gameObject.SetActive(false);
                var noImage = _view.transform.Find(
                    "TicketMacine/MusicInformationPanel/NoImageMask");
                if (noImage != null)
                {
                    noImage.gameObject.SetActive(true);
                    var noImageGroup = noImage.GetComponent<CanvasGroup>();
                    if (noImageGroup != null) noImageGroup.alpha = 1f;
                }
                var musicJackets = _view.transform.Find(
                    "TicketMacine/MusicInformationPanel/MusicJackets");
                if (musicJackets != null)
                    musicJackets.gameObject.SetActive(false);
                var informationPanel = _view.transform.Find(
                    "TicketMacine/MusicInformationPanel");
                var informationFrame = informationPanel != null
                    ? informationPanel.GetComponent<Image>()
                    : null;
                if (informationFrame != null)
                {
                    // MusicSelectionView.SetActiveNoMusic calls
                    // ChangeFrameColor(MusicDifficulties.None).
                    informationFrame.color =
                        RecoveredMusicSelectionPreviewRuntime
                            .DifficultyFrameColor(
                                RecoveredMusicDifficulty.None);
                }
                _preview?.SetDifficultyColor(
                    RecoveredMusicDifficulty.None);
                SetText(
                    "TicketMacine/MusicInformationPanel/ScrollMusicTitle/" +
                    "ScrollMusicTitleText",
                    string.Empty);
                SetText(
                    "TicketMacine/MusicInformationPanel/ScrollCreator/" +
                    "ScrollCreatorText",
                    string.Empty);
                SetSelectionControlsInteractable(false);
                RefreshHudFilterIcon();
                RefreshHudSortLabel();
                _isRebuildingMusicList = false;
                yield break;
            }
            SetSelectionControlsInteractable(true);
            var selected = filtered.FirstOrDefault(music => music.Id == selectedId)
                ?? filtered[0];
            yield return LoadMusicAssets(selected);
            BindView(selected);
            BindMusicList();
            if (!TryGetLive(selected, selectedDifficulty, out _))
                selectedDifficulty = selected.Lives[0].Difficulty;
            SelectDifficulty(selectedDifficulty, false);
            FocusCell(FindVisibleCell(selected.Id));
            _preview.Play(selected.Id);
            RefreshHudFilterIcon();
            RefreshHudSortLabel();
            _isRebuildingMusicList = false;
        }

        private void SetSelectionControlsInteractable(bool interactable)
        {
            foreach (var path in new[]
                     {
                         "ArrowUpButton",
                         "ArrowDownButton",
                         "Random/RandomButton",
                         "OkButton",
                     })
            {
                var button = _view.transform.Find(path)?.GetComponent<Button>();
                if (button == null) continue;
                button.interactable = interactable;
                foreach (var graphic in
                         button.GetComponentsInChildren<Graphic>(true))
                {
                    var color = graphic.color;
                    color.a = interactable ? 1f : 0.5f;
                    graphic.color = color;
                }
            }
        }

        private bool MatchesClearLampFilter(RecoveredLocalMusicEntry music)
        {
            var difficulty = _selection != null
                ? _selection.Live.Difficulty
                : RecoveredMusicDifficulty.Stella;
            var lamp = _localResults.GetClearLamp(music.Id, difficulty);
            foreach (var filter in _clearLampFilters)
            {
                switch (filter)
                {
                    case 0:
                        if (lamp == RecoveredClearLamp.None) return true;
                        break;
                    case 1:
                        if ((int)lamp < (int)RecoveredClearLamp.FullCombo)
                            return true;
                        break;
                    case 2:
                        if ((int)lamp < (int)RecoveredClearLamp.AllPerfect)
                            return true;
                        break;
                    case 3:
                        if (!HasLive(music, RecoveredMusicDifficulty.Stella))
                            return true;
                        break;
                }
            }
            return false;
        }

        private int CompareMusicForHud(
            RecoveredLocalMusicEntry left,
            RecoveredLocalMusicEntry right)
        {
            // Retail Sort: released live first, selected key ascending, then
            // group-main ReleasedAt and group-main MusicMaster ID ascending.
            // Offline catalogs expose every included chart as released.
            TryGetLive(left, _selection.Live.Difficulty, out var leftLive);
            TryGetLive(right, _selection.Live.Difficulty, out var rightLive);
            var compared = (rightLive != null).CompareTo(leftLive != null);
            if (compared != 0) return compared;
            switch (_musicSortMode)
            {
                case 0:
                    compared = (leftLive?.Level ?? 0).CompareTo(rightLive?.Level ?? 0);
                    break;
                case 1:
                    compared = string.Compare(
                        left.PronounceName, right.PronounceName,
                        StringComparison.CurrentCulture);
                    break;
                case 2:
                    break;
                case 3:
                    compared = GetLocalNotationRate(left, leftLive)
                        .CompareTo(GetLocalNotationRate(right, rightLive));
                    break;
                case 4:
                    compared = _localResults.GetBest(
                            left.Id, _selection.Live.Difficulty)
                        .CompareTo(_localResults.GetBest(
                            right.Id, _selection.Live.Difficulty));
                    break;
                case 5:
                    compared = left.StaminaConsumption.CompareTo(
                        right.StaminaConsumption);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(_musicSortMode));
            }
            if (compared != 0) return compared;
            compared = left.HasSortReleasedAt.CompareTo(right.HasSortReleasedAt);
            if (compared != 0) return compared;
            if (left.HasSortReleasedAt)
            {
                compared = left.SortReleasedAtUtcTicks.CompareTo(right.SortReleasedAtUtcTicks);
                if (compared != 0) return compared;
            }
            return left.SortMusicId.CompareTo(right.SortMusicId);
        }

        private RecoveredLocalMusicEntry[] SortMusicsForHud(
            IEnumerable<RecoveredLocalMusicEntry> musics)
        {
            // LINQ retains input order when every authored key ties, as retail
            // OrderBy/ThenBy do. Array.Sort does not guarantee that property.
            return musics.OrderBy(music => music,
                Comparer<RecoveredLocalMusicEntry>.Create(CompareMusicForHud)).ToArray();
        }

        private double GetLocalNotationRate(
            RecoveredLocalMusicEntry music, RecoveredLocalLiveEntry live)
        {
            // Same explicit offline ILive.NotationRate adapter as the score HUD.
            return RecoveredPlayerRating.IsEligible(music, live)
                ? RecoveredPlayerRating.CalculateNotationRate(live.Level,
                    _localResults.GetBest(music.Id, live.Difficulty),
                    _localResults.HasClear(music.Id, live.Difficulty))
                : 0d;
        }

        private void RefreshHudFilterIcon()
        {
            var active = FindDescendant(
                _musicSelectionHeader.transform, "Active");
            if (active != null)
                active.gameObject.SetActive(
                    _clearLampFilters.Count > 0 ||
                    _musicVideoFilters.Count > 0 ||
                    _musicSizeFilters.Count > 0 ||
                    !AreAllPermanentActorsSelected(_vocalFilters));
        }

        private void RefreshHudSortLabel()
        {
            var sortButton = FindDescendant(
                _musicSelectionHeader.transform, "SortButton");
            var text = sortButton != null
                ? sortButton.GetComponentInChildren<Text>(true)
                : null;
            if (text == null) return;
            var labels = new[]
            {
                "Lv", "楽曲名", "配信日", "レート", "達成率", "スタミナ消費量",
            };
            text.text = labels[Mathf.Clamp(_musicSortMode, 0, labels.Length - 1)];
        }

        private GameObject CreateOverlay(
            Transform parent, string name, Color color)
        {
            var overlay = CreateRect(
                parent, name, Vector2.zero, Vector2.zero).gameObject;
            var rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = overlay.AddComponent<Image>();
            image.color = color;
            return overlay;
        }

        private static RectTransform CreateRect(
            Transform parent, string name, Vector2 position, Vector2 size)
        {
            var result = new GameObject(name, typeof(RectTransform))
                .GetComponent<RectTransform>();
            result.SetParent(parent, false);
            result.anchorMin = new Vector2(0.5f, 0.5f);
            result.anchorMax = new Vector2(0.5f, 0.5f);
            result.pivot = new Vector2(0.5f, 0.5f);
            result.anchoredPosition = position;
            result.sizeDelta = size;
            return result;
        }

        private Text CreateText(
            Transform parent,
            string name,
            string value,
            Vector2 position,
            Vector2 size,
            int fontSize)
        {
            var rect = CreateRect(parent, name, position, size);
            var text = rect.gameObject.AddComponent<Text>();
            var source = _view.GetComponentInChildren<Text>(true);
            text.font = source != null && source.font != null
                ? source.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return text;
        }

        private Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            var rect = CreateRect(parent, name, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            CreateText(
                rect, "Text", label, Vector2.zero, size, 38);
            return button;
        }

        private void BindNoteSpeedControls()
        {
            _performanceSettings = new RecoveredSettingsStore().LoadOrDefault();
            _noteSpeed = (float)_performanceSettings.GameSettings.NoteSpeed;
            _noteOffset = (float)_performanceSettings.GameSettings.NoteOffsetValue;
            _mirror = _performanceSettings.GameDetailSettings.IsActiveMirror;
            _splitRandom =
                _performanceSettings.GameDetailSettings.IsActiveSplitRandom;
            _skipPreLiveConfirmation =
                !_performanceSettings.SystemSettings.IsPreLiveOptionConfirmation;

            var noteSpeed = FindDescendant(
                _noteSpeedDialogBody.transform, "NoteSpeedSettings");
            var timing = FindDescendant(
                _noteSpeedDialogBody.transform, "TimingSettings");
            if (noteSpeed == null || timing == null)
                throw new InvalidOperationException(
                    "Recovered performance settings body is incomplete.");
            var current = FindDescendant(noteSpeed, "CurrentValueText");
            _noteSpeedValueText = current != null
                ? current.GetComponent<Text>()
                : null;
            var timingCurrent = FindDescendant(timing, "CurrentValueText");
            _noteOffsetValueText = timingCurrent != null
                ? timingCurrent.GetComponent<Text>()
                : null;
            BindNamedButton(noteSpeed, "Minus", () => ChangeNoteSpeed(-1f));
            BindNamedButton(noteSpeed, "DecimalMinus", () => ChangeNoteSpeed(-0.1f));
            BindNamedButton(noteSpeed, "DecimalPlus", () => ChangeNoteSpeed(0.1f));
            BindNamedButton(noteSpeed, "Plus", () => ChangeNoteSpeed(1f));
            BindNamedButton(timing, "Minus", () => ChangeNoteOffset(-0.5f));
            BindNamedButton(timing, "DecimalMinus", () => ChangeNoteOffset(-0.1f));
            BindNamedButton(timing, "DecimalPlus", () => ChangeNoteOffset(0.1f));
            BindNamedButton(timing, "Plus", () => ChangeNoteOffset(0.5f));
            BindBinarySetting(
                FindDescendant(_noteSpeedDialogBody.transform, "MirrorSettings"),
                () => _mirror,
                value => _mirror = value);
            BindBinarySetting(
                FindDescendant(
                    _noteSpeedDialogBody.transform, "SplitRandomSettings"),
                () => _splitRandom,
                value => _splitRandom = value);
            var checkBox = FindDescendant(
                _noteSpeedDialogBody.transform, "CheckBox");
            BindNamedButton(checkBox, "CheckButton", () =>
            {
                _skipPreLiveConfirmation = !_skipPreLiveConfirmation;
                RefreshPreLiveCheckBox(checkBox);
            }, true);
            RefreshPerformanceSettings();
        }

        private void BindBinarySetting(
            Transform setting, Func<bool> getValue, Action<bool> setValue)
        {
            if (setting == null) return;
            BindNamedButton(setting, "On", () =>
            {
                setValue(true);
                RefreshBinarySetting(setting, getValue());
            }, true);
            BindNamedButton(setting, "Off", () =>
            {
                setValue(false);
                RefreshBinarySetting(setting, getValue());
            }, true);
            RefreshBinarySetting(setting, getValue());
        }

        private void RefreshBinarySetting(Transform setting, bool value)
        {
            foreach (var optionName in new[] { "On", "Off" })
            {
                var option = FindDescendant(setting, optionName);
                if (option == null) continue;
                var selected = optionName == "On" ? value : !value;
                var active = FindDescendant(option, "InnerImage");
                var image = active != null ? active.GetComponent<Image>() : null;
                if (image == null)
                    throw new InvalidOperationException(
                        $"{setting.name}/{optionName} has no ToggleImage InnerImage.");
                // Sirius.ToggleImage keeps both InnerImage objects active and
                // swaps the two exact common-button sprites.
                active.gameObject.SetActive(true);
                image.sprite = selected ? _toggleOnSprite : _toggleOffSprite;
                image.color = Color.white;
            }
        }

        private void RefreshPreLiveCheckBox(Transform checkBox)
        {
            if (checkBox == null) return;
            var checkMark = FindDescendant(checkBox, "CheckMark");
            if (checkMark != null)
                checkMark.gameObject.SetActive(_skipPreLiveConfirmation);
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }
            return null;
        }

        private void BindNamedButton(
            Transform root,
            string name,
            UnityEngine.Events.UnityAction action,
            bool playButtonGo = false)
        {
            var target = FindDescendant(root, name);
            var button = target != null ? target.GetComponent<Button>() : null;
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (playButtonGo)
                    _se.Play(RecoveredUiSeRuntime.Cue.ButtonGo);
                action();
            });
        }

        private void ChangeNoteSpeed(float delta)
        {
            _noteSpeed = Mathf.Clamp(
                Mathf.Round((_noteSpeed + delta) * 10f) / 10f,
                (float)RecoveredGameSettings.MinimumNoteSpeed,
                (float)RecoveredGameSettings.MaximumNoteSpeed);
            RefreshNoteSpeedText();
        }

        private void ChangeNoteOffset(float delta)
        {
            _noteOffset = Mathf.Clamp(
                Mathf.Round((_noteOffset + delta) * 10f) / 10f,
                (float)RecoveredGameSettings.MinimumNoteOffsetValue,
                (float)RecoveredGameSettings.MaximumNoteOffsetValue);
            RefreshPerformanceSettings();
        }

        private void RefreshPerformanceSettings()
        {
            RefreshNoteSpeedText();
            if (_noteOffsetValueText != null)
                _noteOffsetValueText.text = _noteOffset.ToString("0.0");
            RefreshBinarySetting(
                FindDescendant(_noteSpeedDialogBody.transform, "MirrorSettings"),
                _mirror);
            RefreshBinarySetting(
                FindDescendant(
                    _noteSpeedDialogBody.transform, "SplitRandomSettings"),
                _splitRandom);
            RefreshPreLiveCheckBox(
                FindDescendant(_noteSpeedDialogBody.transform, "CheckBox"));
            RefreshNoteSpeedSimulation();
        }

        private void RefreshNoteSpeedText()
        {
            if (_noteSpeedValueText != null)
                _noteSpeedValueText.text = _noteSpeed.ToString("0.0");
            RefreshNoteSpeedSimulation();
        }

        private void RefreshNoteSpeedSimulation()
        {
            if (_noteSpeedSimulation == null || _performanceSettings == null)
                return;
            _noteSpeedSimulation.SetSettings(
                _noteSpeed,
                _noteOffset,
                _performanceSettings.GameDetailSettings.NoteStartOffset,
                _performanceSettings.GameDetailSettings.TimingEffectOffset,
                _performanceSettings.GameDetailSettings.TimingEffectScaleType,
                _performanceSettings.GameSettings.IsActiveSenseDisplay,
                _performanceSettings.GameDetailSettings.LaneWidth);
        }

        private void BindView(RecoveredLocalMusicEntry music)
        {
            _jacketSprite = JacketResourceProvider.TryGetValue(music.Id, out var jacket)
                ? jacket
                : null;
            SetText("TicketMacine/MusicInformationPanel/ScrollMusicTitle/ScrollMusicTitleText",
                music.Name);
            SetText("TicketMacine/MusicInformationPanel/ScrollCreator/ScrollCreatorText",
                string.Format("作詞 {0} / 作曲 {1}", music.LyricWriter, music.Composer));
            SetText("TicketMacine/MusicInformationPanel/MusicType/MusicTypeText", "ORIGINAL");
            ApplyInformationPanelLayout();
            BindJacket(
                _view.transform.Find(
                    "TicketMacine/MusicInformationPanel/MusicJackets/MusicJacketBack"));
            BindJacket(
                _view.transform.Find(
                    "TicketMacine/MusicInformationPanel/MusicJackets/MusicJacketFront"));
            var noImage = _view.transform.Find(
                "TicketMacine/MusicInformationPanel/NoImageMask");
            if (noImage != null)
            {
                noImage.gameObject.SetActive(false);
                var noImageGroup = noImage.GetComponent<CanvasGroup>();
                if (noImageGroup != null) noImageGroup.alpha = 0f;
            }
            var musicJackets = _view.transform.Find(
                "TicketMacine/MusicInformationPanel/MusicJackets");
            if (musicJackets != null)
                musicJackets.gameObject.SetActive(true);
            var noMusic = _view.transform.Find("NoMusicPanel");
            if (noMusic != null) noMusic.gameObject.SetActive(false);
            var defaultPanels = _view.transform.Find("DefaultLivePanels");
            if (defaultPanels != null) defaultPanels.gameObject.SetActive(true);
            var ratePanel = _view.transform.Find(
                "DefaultLivePanels/ScorePanel/RatePanel");
            var rateCanvasGroup =
                ratePanel != null ? ratePanel.GetComponent<CanvasGroup>() : null;
            if (rateCanvasGroup != null)
            {
                // RatePanel.Show/ShowAsync restores the prefab's authored
                // alpha=0 after MusicSelectionView.SetMusicInformation.
                rateCanvasGroup.alpha = 1f;
                rateCanvasGroup.interactable = true;
                rateCanvasGroup.blocksRaycasts = true;
            }
            SetText(
                "DefaultLivePanels/ScorePanel/RatePanel/" +
                "AchievementRateFrame/AchievementRate",
                FormatAchievementRate(0d));
            SetText(
                "DefaultLivePanels/ScorePanel/RatePanel/" +
                "MusicRateFrame/MusicRate",
                "0.00");
            var rewardButton = _view.transform.Find(
                "DefaultLivePanels/ScorePanel/RatePanel/" +
                "AchievementRateFrame/ConfirmRewardButton");
            if (rewardButton != null) rewardButton.gameObject.SetActive(false);

            foreach (RecoveredMusicDifficulty difficulty in Enum.GetValues(
                         typeof(RecoveredMusicDifficulty)))
            {
                if (difficulty == RecoveredMusicDifficulty.None) continue;
                var panel = _view.transform.Find(
                    "DefaultLivePanels/DifficultyPanel/" + difficulty);
                if (panel == null) continue;
                var button = panel.GetComponent<Button>();
                if (button == null) continue;
                _difficultyButtons[difficulty] = button;
                var buttonImage = panel.GetComponent<Image>();
                var difficultyIndex = (int)difficulty - 1;
                if (buttonImage != null &&
                    difficultyIndex >= 0 &&
                    difficultyIndex < _difficultyButtonOnSprites.Length)
                    buttonImage.sprite = _difficultyButtonOnSprites[difficultyIndex];
                var captured = difficulty;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectDifficulty(captured, true));
                button.interactable = _charts.ContainsKey(difficulty) &&
                    HasLive(music, difficulty);
                var level = panel.Find("DifficultyLevel")?.GetComponent<Text>();
                if (level != null)
                    level.text = TryGetLive(music, difficulty, out var live)
                        ? FormatLevel(difficulty, live.Level)
                        : "-";
            }

            var ok = _view.transform.Find("OkButton")?.GetComponent<Button>();
            if (ok == null)
                throw new InvalidOperationException("MusicSelectionView/OkButton is missing.");
            ok.onClick.RemoveAllListeners();
            var okText = ok.GetComponentInChildren<Text>(true);
            if (okText == null)
                throw new InvalidOperationException(
                    "MusicSelectionView/OkButtonText is missing.");
            okText.text = "開演";
            ok.onClick.AddListener(RequestPerformanceStart);
        }

        private void BindMusicList()
        {
            _listScrollRect = _view.GetComponentInChildren<ScrollRect>(true);
            if (_listScrollRect == null)
                throw new InvalidOperationException("MusicSelection ScrollRect is missing.");
            var viewport = _listScrollRect.GetComponent<RectTransform>();
            if (viewport == null)
                throw new InvalidOperationException("MusicSelection list content is missing.");
            var contentObject = new GameObject(
                "LocalMusicContent", typeof(RectTransform));
            _listContent = contentObject.GetComponent<RectTransform>();
            _listContent.SetParent(viewport, false);
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = new Vector2(1f, 1f);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.anchoredPosition = Vector2.zero;
            _listContent.sizeDelta = new Vector2(
                0f, _catalog.Musics.Length * LoopCopies * CellStride);
            _listScrollRect.viewport = viewport;
            _listScrollRect.content = _listContent;
            _listScrollRect.horizontal = false;
            _listScrollRect.vertical = true;
            _listScrollRect.movementType = ScrollRect.MovementType.Unrestricted;
            _listScrollRect.inertia = true;
            _listScrollRect.decelerationRate = 0.01f;
            _listScrollRect.scrollSensitivity = 20f;
            var input = _listScrollRect.GetComponent<
                RecoveredMusicSelectionLoopInput>();
            if (input == null)
                input = _listScrollRect.gameObject.AddComponent<
                    RecoveredMusicSelectionLoopInput>();
            input.Configure(this);
            Canvas.ForceUpdateCanvases();
            var visibleCount = Mathf.CeilToInt(
                _listScrollRect.viewport.rect.height / CellStride);
            var poolCount = Mathf.Min(
                _catalog.Musics.Length * LoopCopies,
                Mathf.Max(3, visibleCount + VisibleCellPoolPadding * 2 + 1));
            for (var index = 0; index < poolCount; index++)
                CreateListCell(_listContent);
            var selectedIndex = _selection != null
                ? Array.FindIndex(
                    _catalog.Musics,
                    music => music.Id == _selection.Music.Id)
                : 0;
            if (selectedIndex < 0) selectedIndex = 0;
            SetContentForPhysicalIndex(_catalog.Musics.Length + selectedIndex);
            UpdateVisibleCellBindings(true);
        }

        private void BindSelectionAuxiliaryControls()
        {
            var arrowUp = _view.transform.Find("ArrowUpButton");
            var arrowDown = _view.transform.Find("ArrowDownButton");
            EnsureButton(arrowUp, () => SelectAdjacentMusicGroup(-1));
            EnsureButton(arrowDown, () => SelectAdjacentMusicGroup(1));
            StartArrowAnimation(arrowUp as RectTransform, true);
            StartArrowAnimation(arrowDown as RectTransform, false);
            EnsureButton(
                _view.transform.Find("Random/RandomButton"),
                SelectRandomMusic);
            EnsureButton(
                _view.transform.Find("Bookmark/BookmarkChangeButton"),
                CycleBookmarkFilter);
            RefreshBookmarkFilterButton();
        }

        private void StartArrowAnimation(RectTransform arrow, bool upward)
        {
            if (arrow == null) return;
            var targetY = arrow.anchoredPosition.y + (upward ? 12f : -12f);
            var sequence = DOTween.Sequence().SetLink(arrow.gameObject);
            sequence.Append(
                arrow.DOAnchorPosY(targetY, 1f)
                    .SetEase(Ease.InSine));
            sequence.SetLoops(-1, LoopType.Yoyo);
            if (upward)
                _arrowUpSequence = sequence;
            else
                _arrowDownSequence = sequence;
        }

        private void SelectAdjacentMusicGroup(int direction)
        {
            if (_focusedCell == null || _catalog?.Musics == null ||
                _catalog.Musics.Length == 0)
                return;

            // MusicSelectionPresenter.OnGroupMusicAsync delegates the arrow
            // value to MusicSelectionModel.AddGroupingMusicIndex. The model's
            // GroupingMusicEntityEnumerable groups the already-sorted list by
            // the active SortType, advances cyclically, and returns the first
            // entity of the target group. It is not an adjacent-cell control.
            var ordered = SortMusicsForHud(_catalog.Musics);
            var groups = ordered
                .GroupBy(GetCurrentMusicGroupKey)
                .Select(group => group.ToArray())
                .ToArray();
            if (groups.Length == 0) return;
            var current = Array.FindIndex(
                groups,
                group => group.Any(music => music.Id == _focusedCell.music.Id));
            if (current < 0) current = 0;
            var next = (current + Math.Sign(direction)) % groups.Length;
            if (next < 0) next += groups.Length;
            var target = groups[next][0];
            SnapToMusic(target, false);
        }

        private string GetCurrentMusicGroupKey(RecoveredLocalMusicEntry music)
        {
            var difficulty = _selection != null
                ? _selection.Live.Difficulty
                : RecoveredMusicDifficulty.Stella;
            switch (_musicSortMode)
            {
                case 0:
                    return TryGetLive(music, difficulty, out var live)
                        ? "level:" + live.Level
                        : "level:none";
                case 1:
                    return "name:" + GetMusicNameGroupKey(music.PronounceName);
                case 2:
                    // The local catalog preserves the retail release order as
                    // MusicMaster ID order. Each recovered item is therefore a
                    // release group until ReleaseAt is added to the adapter.
                    return "release:" + music.Id;
                case 3:
                    var rate = GetBestRateEntryForMusic(music, difficulty);
                    return "rate:" +
                        Math.Floor(rate != null ? rate.notationRate : 0d);
                case 4:
                    return "achievement:" +
                        Sirius.GameResult.GameResultPanel.GetAchievementGrade(
                            _localResults.GetBest(music.Id, difficulty));
                case 5:
                    return "stamina:" + music.StaminaConsumption;
                default:
                    return "music:" + music.Id;
            }
        }

        private static int GetMusicNameGroupKey(string pronounceName)
        {
            if (string.IsNullOrEmpty(pronounceName)) return -1;
            var value = pronounceName[0];
            var rows = new[]
            {
                "ぁあぃいぅうぇえぉおゔ",
                "かがきぎくぐけげこご",
                "さざしじすずせぜそぞ",
                "ただちぢっつづてでとど",
                "なにぬねの",
                "はばぱひびぴふぶぷへべぺほぼぽ",
                "まみむめも",
                "ゃやゅゆょよ",
                "らりるれろ",
                "ゎわをん",
            };
            for (var index = 0; index < rows.Length; index++)
                if (rows[index].IndexOf(value) >= 0)
                    return index;
            return 1000 + value;
        }

        private void SelectRandomMusic()
        {
            if (_selection == null || _catalog?.Musics == null) return;
            var candidates = _catalog.Musics
                .Where(music =>
                    music.Id != _selection.Music.Id &&
                    HasLive(music, _selection.Live.Difficulty))
                .ToArray();
            if (candidates.Length == 0) return;
            var selected = candidates[UnityEngine.Random.Range(
                0, candidates.Length)];
            SnapToMusic(selected, false);
        }

        private void CycleBookmarkFilter()
        {
            switch (_bookmarkFilter)
            {
                case RecoveredMusicBookmarkFlags.None:
                    _bookmarkFilter = RecoveredMusicBookmarkFlags.Bookmark1;
                    break;
                case RecoveredMusicBookmarkFlags.Bookmark1:
                    _bookmarkFilter = RecoveredMusicBookmarkFlags.Bookmark2;
                    break;
                case RecoveredMusicBookmarkFlags.Bookmark2:
                    _bookmarkFilter = RecoveredMusicBookmarkFlags.Bookmark3;
                    break;
                default:
                    _bookmarkFilter = RecoveredMusicBookmarkFlags.None;
                    break;
            }
            RefreshBookmarkFilterButton();
            StartCoroutine(RebuildFilteredMusicList());
        }

        private void RefreshBookmarkFilterButton()
        {
            var root = _view != null
                ? _view.transform.Find("Bookmark/BookmarkChangeButton")
                : null;
            if (root == null || _bookmarks == null) return;
            var on = root.Find("On");
            var off = root.Find("Off");
            var active = _bookmarkFilter != RecoveredMusicBookmarkFlags.None;
            if (on != null) on.gameObject.SetActive(active);
            if (off != null) off.gameObject.SetActive(!active);
            var visibleState = active ? on : off;
            if (visibleState != null)
            {
                foreach (var graphic in
                         visibleState.GetComponentsInChildren<Graphic>(true))
                {
                    var color = graphic.color;
                    color.a = 1f;
                    graphic.color = color;
                    graphic.enabled = true;
                    var image = graphic as Image;
                    if (image != null && image.sprite != null)
                        image.overrideSprite = image.sprite;
                }
            }
            var balloon = _view.transform.Find("Bookmark/BalloonBadge");
            if (balloon != null) balloon.gameObject.SetActive(active);
            var number = _view.transform.Find(
                    "Bookmark/BalloonBadge/FavoriteNumberText")
                ?.GetComponent<Text>();
            if (number != null)
                number.text = active
                    ? "お気に入り" + BookmarkNumber(_bookmarkFilter)
                    : string.Empty;
        }

        private static string BookmarkNumber(
            RecoveredMusicBookmarkFlags flag)
        {
            switch (flag)
            {
                case RecoveredMusicBookmarkFlags.Bookmark1: return "１";
                case RecoveredMusicBookmarkFlags.Bookmark2: return "２";
                case RecoveredMusicBookmarkFlags.Bookmark3: return "３";
                default: return string.Empty;
            }
        }

        private void RefreshCellBookmarkTags(Transform cell, long musicId)
        {
            var group = cell != null
                ? cell.Find("PartsBase/BookmarkGroup/BookmarkTagGroup")
                : null;
            if (group == null || _bookmarks == null) return;
            for (var index = group.childCount - 1; index >= 0; index--)
                Destroy(group.GetChild(index).gameObject);
            var flags = _bookmarks.Get(musicId);
            var visibleIndex = 0;
            for (var index = 0; index < 3; index++)
            {
                var flag = (RecoveredMusicBookmarkFlags)(1 << index);
                if ((flags & flag) == 0) continue;
                var tag = Instantiate(
                    _musicSelectionBookmarkTagPrefab, group, false);
                tag.name = "MusicSelectionBookmarkTag" + (index + 1);
                var rect = tag.GetComponent<RectTransform>();
                if (rect != null)
                    rect.anchoredPosition = new Vector2(visibleIndex * 36f, 0f);
                var number = FindDescendant(tag.transform, "NumberText")
                    ?.GetComponent<Text>();
                if (number != null) number.text = (index + 1).ToString();
                visibleIndex++;
            }
        }

        private void RefreshAllCellBookmarkTags()
        {
            foreach (var pair in _listCells)
                foreach (var cell in pair.Value)
                    RefreshCellBookmarkTags(cell.gameObject.transform, pair.Key);
        }

        private void OpenBookmarkDialog(RecoveredLocalMusicEntry music)
        {
            if (music == null || _bookmarkDialog != null ||
                _playerRateDialog != null || _hudDialog != null ||
                _noteSpeedDialog != null)
                return;
            SetSpectrumVisibleForDialog(false);
            _bookmarkDialogMusicId = music.Id;
            _draftBookmarkFlags = _bookmarks.Get(music.Id);
            _bookmarkDialog = CreateCommonDialog(
                "お気に入り設定", new Vector2(1032f, 720f));
            _bookmarkDialog.name = "RecoveredMusicBookmarkSettingDialog";
            var body = FindDescendant(_bookmarkDialog.transform, "Body");
            var bodyInstance = Instantiate(
                _musicBookmarkSettingDialogBodyPrefab, body, false);
            bodyInstance.name = "MusicBookmarkSettingDialogBody";
            var group = FindDescendant(
                bodyInstance.transform, "FavoriteCheckBoxGroup");
            if (group != null) group.gameObject.SetActive(true);
            var jacket = FindDescendant(
                bodyInstance.transform, "MusicJacketImage")
                ?.GetComponent<Image>();
            if (jacket != null && JacketResourceProvider.TryGetValue(music.Id, out var sprite))
            {
                jacket.sprite = sprite;
                jacket.overrideSprite = sprite;
                // The retail async jacket callback fades this placeholder
                // Image from authored alpha=0 to visible.
                jacket.color = Color.white;
                jacket.enabled = true;
            }
            var musicName = FindDescendant(
                bodyInstance.transform, "MusicNameText")
                ?.GetComponent<Text>();
            if (musicName != null) musicName.text = music.Name;
            var toggles = DirectChildren(group, "FavoriteToggle");
            for (var index = 0; index < toggles.Count && index < 3; index++)
            {
                var captured = index;
                EnsureButton(toggles[index], () =>
                {
                    var flag =
                        (RecoveredMusicBookmarkFlags)(1 << captured);
                    _draftBookmarkFlags ^= flag;
                    RefreshBookmarkDialogToggles(toggles);
                });
            }
            RefreshBookmarkDialogToggles(toggles);
            ConfigureCommonDialogButton(
                _bookmarkDialog, "FirstButtonParent", "FirstButton",
                "キャンセル", CloseBookmarkDialog, false);
            ConfigureCommonDialogButton(
                _bookmarkDialog, "SecondButtonParent", "SecondButton",
                "OK", ConfirmBookmarkDialog, true);
            var third = FindDescendant(
                _bookmarkDialog.transform, "ThirdButtonParent");
            if (third != null) third.gameObject.SetActive(false);
            _bookmarkDialogButtons =
                LayoutCommonDialogButtons(_bookmarkDialog);
            _bookmarkDialog.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
        }

        private void RefreshBookmarkDialogToggles(List<Transform> toggles)
        {
            for (var index = 0; index < toggles.Count && index < 3; index++)
            {
                var active = FindDescendant(toggles[index], "OnActive");
                if (active != null)
                    active.gameObject.SetActive(
                        (_draftBookmarkFlags &
                         (RecoveredMusicBookmarkFlags)(1 << index)) != 0);
            }
        }

        private void CloseBookmarkDialog()
        {
            if (_bookmarkDialog == null) return;
            _se.Play(RecoveredUiSeRuntime.Cue.ButtonBack);
            Destroy(_bookmarkDialog);
            if (_bookmarkDialogButtons != null)
                Destroy(_bookmarkDialogButtons);
            _bookmarkDialog = null;
            _bookmarkDialogButtons = null;
            _bookmarkDialogMusicId = 0;
            SetSpectrumVisibleForDialog(true);
        }

        private void ConfirmBookmarkDialog()
        {
            if (_bookmarkDialog == null) return;
            _se.Play(RecoveredUiSeRuntime.Cue.ButtonGo);
            _bookmarks.Set(_bookmarkDialogMusicId, _draftBookmarkFlags);
            RefreshAllCellBookmarkTags();
            RefreshBookmarkFilterButton();
            Destroy(_bookmarkDialog);
            if (_bookmarkDialogButtons != null)
                Destroy(_bookmarkDialogButtons);
            _bookmarkDialog = null;
            _bookmarkDialogButtons = null;
            _bookmarkDialogMusicId = 0;
            SetSpectrumVisibleForDialog(true);
            if (_bookmarkFilter != RecoveredMusicBookmarkFlags.None)
                StartCoroutine(RebuildFilteredMusicList());
        }

        private IEnumerable<LocalRateEntry> GetLocalRatedLiveEntries()
        {
            if (_allMusics == null || _localResults == null)
                yield break;
            foreach (var music in _allMusics)
            {
                if (music?.Lives == null) continue;
                foreach (var live in music.Lives)
                {
                    if (!RecoveredPlayerRating.IsEligible(music, live))
                        continue;
                    var achievement =
                        _localResults.GetBest(music.Id, live.Difficulty);
                    var notationRate =
                        RecoveredPlayerRating.CalculateNotationRate(
                            live.Level,
                            achievement,
                            _localResults.HasClear(
                                music.Id, live.Difficulty));
                    if (notationRate <= 0d) continue;
                    yield return new LocalRateEntry
                    {
                        music = music,
                        live = live,
                        achievementRate = achievement,
                        notationRate = notationRate,
                    };
                }
            }
        }

        private IEnumerable<LocalRateEntry> GetLocalRateEntries()
        {
            // PlayerRateSubPresenter groups the rated ILives by Music and shows
            // the maximum NotationRate chart for each song in this dialog.
            return GetLocalRatedLiveEntries()
                .GroupBy(entry => entry.music.Id)
                .Select(group => group
                    .OrderByDescending(entry => entry.notationRate)
                    .First());
        }

        private LocalRateEntry GetBestRateEntryForMusic(
            RecoveredLocalMusicEntry music,
            RecoveredMusicDifficulty fallbackDifficulty)
        {
            if (music?.Lives == null) return null;
            LocalRateEntry bestEntry = null;
            foreach (var live in music.Lives)
            {
                if (!RecoveredPlayerRating.IsEligible(music, live))
                    continue;
                var achievement =
                    _localResults.GetBest(music.Id, live.Difficulty);
                if (achievement <= 0d && live.Difficulty != fallbackDifficulty)
                    continue;
                var entry = new LocalRateEntry
                {
                    music = music,
                    live = live,
                    achievementRate = achievement,
                    notationRate = RecoveredPlayerRating.CalculateNotationRate(
                        live.Level,
                        achievement,
                        _localResults.HasClear(
                            music.Id, live.Difficulty)),
                };
                if (bestEntry == null ||
                    entry.notationRate > bestEntry.notationRate)
                    bestEntry = entry;
            }
            return bestEntry;
        }

        private double CalculatePlayerRate() =>
            RecoveredPlayerRating.CalculatePlayerRate(
                GetLocalRatedLiveEntries()
                    .Select(entry => entry.notationRate));

        private void RefreshPlayerRateHeader()
        {
            if (_musicSelectionHeader == null) return;
            var playerRate = CalculatePlayerRate();
            SetNamedText(
                _musicSelectionHeader,
                "RateText",
                playerRate.ToString(
                    "0.00", System.Globalization.CultureInfo.InvariantCulture));
            // MusicSelectionHeaderView authors this value white.  Its header
            // presentation is not the GameResult footer color path.
        }

        private void OpenPlayerRateDialog()
        {
            if (_playerRateDialog != null || _bookmarkDialog != null ||
                _hudDialog != null || _noteSpeedDialog != null)
                return;
            _se.Play(RecoveredUiSeRuntime.Cue.ButtonGo);
            SetSpectrumVisibleForDialog(false);
            _playerRateDialog = CreateCommonDialog(
                "レート対象楽曲", new Vector2(1328f, 996f));
            _playerRateDialog.name = "RecoveredPlayerRateDialog";
            var body = FindDescendant(_playerRateDialog.transform, "Body");
            _playerRateDialogBody = Instantiate(
                _playerRateDialogBodyPrefab, body, false);
            _playerRateDialogBody.name = "PlayerRateDialogBody";
            CreatePlayerRateBodyBackground();
            BuildPlayerRateSideMenu();
            BindPlayerRatePanel();
            ConfigureCommonDialogButton(
                _playerRateDialog, "FirstButtonParent", "FirstButton",
                "閉じる", ClosePlayerRateDialog, false);
            var second = FindDescendant(
                _playerRateDialog.transform, "SecondButtonParent");
            var third = FindDescendant(
                _playerRateDialog.transform, "ThirdButtonParent");
            if (second != null) second.gameObject.SetActive(false);
            if (third != null) third.gameObject.SetActive(false);
            _playerRateDialogButtons =
                LayoutSingleCommonDialogButton(
                    _playerRateDialog, "閉じる", false);
            _playerRateDialog.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
        }

        private void CreatePlayerRateBodyBackground()
        {
            var source = FindDescendant(
                _playerRateDialog.transform, "BodyBackground")
                ?.GetComponent<Image>();
            if (source == null || _playerRateDialogBody == null) return;
            var background = new GameObject(
                "RecoveredPlayerRateBodyBackground",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            background.transform.SetParent(
                _playerRateDialogBody.transform, false);
            background.transform.SetSiblingIndex(0);
            var rect = background.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = background.GetComponent<Image>();
            image.sprite = source.sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private void BuildPlayerRateSideMenu()
        {
            var sideMenu = FindDescendant(
                _playerRateDialogBody.transform, "TextSideMenuPanel");
            var content = sideMenu != null
                ? FindDescendant(sideMenu, "Content")
                : null;
            if (content == null)
                throw new InvalidOperationException(
                    "Recovered player-rate side menu is incomplete.");
            var selected = FindDescendant(content, "SelectedObject");
            _playerRateSelectedTabSprite =
                selected != null ? selected.GetComponent<Image>()?.sprite : null;
            if (selected != null) selected.gameObject.SetActive(false);
            var stripe = FindDescendant(content, "SideMenuHorizontalStripe");
            if (stripe != null) stripe.gameObject.SetActive(false);
            _playerRateTabButton = CreateSortFilterTab(
                content, "RecoveredPlayerRateTab", "プレイヤーレート");
            _spRateTabButton = CreateSortFilterTab(
                content, "RecoveredSpRateTab", "星章");
            _playerRateTabButton.onClick.AddListener(() =>
            {
                _se.Play(RecoveredUiSeRuntime.Cue.ButtonGo);
                ShowPlayerRateTab(false);
            });
            _spRateTabButton.onClick.AddListener(() =>
            {
                _se.Play(RecoveredUiSeRuntime.Cue.ButtonGo);
                ShowPlayerRateTab(true);
            });
            ShowPlayerRateTab(false);
        }

        private void ShowPlayerRateTab(bool showSp)
        {
            if (_playerRateDialogBody == null) return;
            var player = FindDescendant(
                _playerRateDialogBody.transform, "PlayerRatePanel");
            var sp = FindDescendant(
                _playerRateDialogBody.transform, "SPRatePanel");
            if (player != null) player.gameObject.SetActive(!showSp);
            if (sp != null) sp.gameObject.SetActive(showSp);
            SetPlayerRateTabVisual(_playerRateTabButton, !showSp);
            SetPlayerRateTabVisual(_spRateTabButton, showSp);
            if (showSp) BindLocalSpRatePanel(sp);
        }

        private void SetPlayerRateTabVisual(Button button, bool selected)
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = selected ? _playerRateSelectedTabSprite : null;
                image.color = selected
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0f);
            }
            var label = FindDescendant(button.transform, "Label")
                ?.GetComponent<Text>();
            if (label != null)
                label.color = selected
                    ? Color.white
                    : new Color32(86, 88, 103, 255);
        }

        private void BindPlayerRatePanel()
        {
            var panel = FindDescendant(
                _playerRateDialogBody.transform, "PlayerRatePanel");
            if (panel == null) return;
            var playerRateText = FindDescendant(panel, "PlayerRateNumberText")
                ?.GetComponent<Text>();
            if (playerRateText != null)
                playerRateText.text = CalculatePlayerRate().ToString(
                    "0.00", System.Globalization.CultureInfo.InvariantCulture);
            EnsureButton(
                FindDescendant(panel, "DisplayOrderSwitchButton"),
                () =>
                {
                    _playerRateDescending = !_playerRateDescending;
                    PopulatePlayerRateRows(panel);
                    RefreshPlayerRateOrderIcon(panel);
                });
            var displayType = FindDescendant(panel, "DsiplayTypeSwitchButton");
            EnsureButton(displayType, () =>
            {
                _playerRateSimpleMode = !_playerRateSimpleMode;
                PopulatePlayerRateRows(panel);
            });
            if (displayType != null) displayType.gameObject.SetActive(true);
            RefreshPlayerRateOrderIcon(panel);
            PopulatePlayerRateRows(panel);
        }

        private void RefreshPlayerRateOrderIcon(Transform panel)
        {
            var icon = FindDescendant(
                FindDescendant(panel, "DisplayOrderSwitchButton"), "Image");
            if (icon != null)
                icon.localScale = new Vector3(
                    1f, _playerRateDescending ? 1f : -1f, 1f);
        }

        private void PopulatePlayerRateRows(Transform panel)
        {
            var rateScroller = FindDescendant(panel, "RateScroller");
            var simpleScroller = FindDescendant(panel, "SimpleRateScroller");
            if (rateScroller != null)
                rateScroller.gameObject.SetActive(!_playerRateSimpleMode);
            if (simpleScroller != null)
                simpleScroller.gameObject.SetActive(_playerRateSimpleMode);
            var activeScroller =
                _playerRateSimpleMode ? simpleScroller : rateScroller;
            var content = activeScroller != null
                ? FindDescendant(
                    FindDescendant(activeScroller, "Scroller"), "Content")
                : null;
            if (content == null) return;
            for (var index = content.childCount - 1; index >= 0; index--)
                Destroy(content.GetChild(index).gameObject);
            var ordered = _playerRateDescending
                ? GetLocalRateEntries()
                    .OrderByDescending(entry => entry.notationRate).ToArray()
                : GetLocalRateEntries()
                    .OrderBy(entry => entry.notationRate).ToArray();
            var rowHeight = _playerRateSimpleMode ? 128f : 184f;
            var contentRect = content as RectTransform;
            if (contentRect != null)
            {
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(0f, 1f);
                contentRect.pivot = new Vector2(0f, 1f);
                contentRect.anchoredPosition = Vector2.zero;
                contentRect.sizeDelta = new Vector2(
                    932f, Mathf.Max(624f, ordered.Length * rowHeight));
            }
            for (var index = 0; index < ordered.Length; index++)
            {
                var entry = ordered[index];
                var row = Instantiate(
                    _playerRateSimpleMode
                        ? _playerRateSimpleContentPrefab
                        : _playerRateContentPrefab,
                    content,
                    false);
                row.name = "PlayerRateContent_" + entry.live.Id;
                var rect = row.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(
                        _playerRateSimpleMode ? 0.5f : 0f,
                        _playerRateSimpleMode ? 0.5f : 1f);
                    rect.sizeDelta = new Vector2(932f, rowHeight);
                    rect.anchoredPosition = new Vector2(
                        _playerRateSimpleMode ? 466f : 0f,
                        -index * rowHeight);
                }
                var jacket = FindDescendant(row.transform, "MusicJacketImage")
                    ?.GetComponent<Image>();
                if (jacket != null)
                    StartCoroutine(BindPlayerRateJacket(entry.music, jacket));
                SetDescendantText(
                    row.transform, "DifficultLabelText",
                    entry.live.Difficulty.ToString().ToUpperInvariant());
                var difficultyLabel = FindDescendant(
                        row.transform, "DifficultyLabelImage")
                    ?.GetComponent<Image>();
                if (difficultyLabel != null)
                    difficultyLabel.color =
                        DifficultyLabelColor(entry.live.Difficulty);
                SetDescendantText(
                    row.transform, "MusicName", entry.music.Name);
                SetDescendantText(
                    row.transform, "NameText", entry.music.Name);
                SetDescendantText(
                    row.transform, "LevelNumberText",
                    FormatLevel(entry.live.Difficulty, entry.live.Level));
                SetDescendantText(
                    row.transform, "CompletePercentageText",
                    entry.achievementRate.ToString(
                        "0.0000",
                        System.Globalization.CultureInfo.InvariantCulture) + "%");
                SetDescendantText(
                    row.transform, "RateNumberText",
                    entry.notationRate.ToString(
                        "0.00",
                        System.Globalization.CultureInfo.InvariantCulture));
                if (_playerRateSimpleMode)
                    SetDescendantText(
                        row.transform, "RateText",
                        entry.notationRate.ToString(
                            "0.00",
                            System.Globalization.CultureInfo.InvariantCulture));
            }
            var empty = FindDescendant(panel, "EmptyText");
            if (empty != null) empty.gameObject.SetActive(ordered.Length == 0);
            if (activeScroller != null)
                activeScroller.gameObject.SetActive(ordered.Length > 0);
        }

        private static void SetDescendantText(
            Transform root, string name, string value)
        {
            var text = FindDescendant(root, name)?.GetComponent<Text>();
            if (text != null) text.text = value ?? string.Empty;
        }

        private void BindLocalSpRatePanel(Transform panel)
        {
            if (panel == null) return;
            var rateIcon = FindDescendant(panel, "RateIcon")
                ?.GetComponent<Image>();
            if (rateIcon != null)
            {
                // PlayerRateDialogSPRatePanel.Initialize always assigns the
                // SpRateNoteType sprite. AssetStudio recovered the authored
                // note sprite; keep it visible while the offline numeric
                // adapter remains a separate concern.
                rateIcon.gameObject.SetActive(true);
                rateIcon.enabled = true;
                if (rateIcon.sprite != null)
                    rateIcon.overrideSprite = rateIcon.sprite;
                var color = rateIcon.color;
                color.a = 1f;
                rateIcon.color = color;
            }
            var olivierEntries = _allMusics
                .Where(music => music?.Lives != null)
                .SelectMany(music => music.Lives
                    .Where(live => live != null &&
                        live.Difficulty ==
                        RecoveredMusicDifficulty.Olivier)
                    .Select(live => new LocalRateEntry
                    {
                        music = music,
                        live = live,
                        achievementRate = _localResults.GetBest(
                            music.Id, live.Difficulty),
                    }))
                .Where(entry => entry.achievementRate > 0d)
                .ToArray();
            var obtained = olivierEntries.Count(entry =>
                entry.achievementRate >= 100d);
            var total = _allMusics.Sum(music =>
                music.Lives.Count(live =>
                    live.Difficulty == RecoveredMusicDifficulty.Olivier));
            var percentage = total > 0 ? obtained * 100d / total : 0d;
            SetDescendantText(
                panel, "RatePercentageText",
                percentage.ToString(
                    "0.00", System.Globalization.CultureInfo.InvariantCulture) +
                "%");
            SetDescendantText(
                panel, "RateFractionText", obtained + "/" + total);
        }

        private void ClosePlayerRateDialog()
        {
            if (_playerRateDialog == null) return;
            _se.Play(RecoveredUiSeRuntime.Cue.ButtonGo);
            Destroy(_playerRateDialog);
            if (_playerRateDialogButtons != null)
                Destroy(_playerRateDialogButtons);
            _playerRateDialog = null;
            _playerRateDialogButtons = null;
            _playerRateDialogBody = null;
            _playerRateTabButton = null;
            _spRateTabButton = null;
            _playerRateSimpleMode = false;
            SetSpectrumVisibleForDialog(true);
        }

        private void CreateListCell(Transform parent)
        {
            var cell = Instantiate(_listCellPrefab, parent, false);
            cell.name = "LocalMusicCellPool";
            var rect = cell.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                // The authored cell pivot is centered. Keeping a top pivot made
                // the 28 px focus growth extend entirely into the next cell.
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(OffsetCellWidth, OffsetCellHeight);
                rect.localScale = Vector3.one;
            }
            var binding = new ListCellBinding
            {
                gameObject = cell,
                rect = rect,
                physicalIndex = -1,
            };
            _physicalCells.Add(binding);
            ConfigureScrollText(
                cell.transform.Find("PartsBase/PartsMask/MusicTitle"));
            ConfigureScrollText(
                cell.transform.Find("PartsBase/PartsMask/Vocals"));
            var focusMask = cell.transform.Find(
                "PartsBase/PartsMask/FocusMask")?.GetComponent<Mask>();
            if (focusMask != null) focusMask.showMaskGraphic = false;
            var bookmarkButton = cell.transform.Find(
                "PartsBase/BookmarkGroup/BookmarkButton");
            if (bookmarkButton != null)
            {
                // MusicSelectionListCell only reveals the registration '+'
                // control on the target cell. Keep its original Button while
                // restoring a raycast surface lost with ButtonController.
                bookmarkButton.gameObject.SetActive(false);
                var surface = bookmarkButton.GetComponent<Image>();
                if (surface == null)
                {
                    surface = bookmarkButton.gameObject.AddComponent<Image>();
                    surface.color = Color.clear;
                }
                surface.raycastTarget = true;
                var button = bookmarkButton.GetComponent<Button>();
                if (button != null) button.targetGraphic = surface;
            }
            var emptyDifficultyBackground = cell.transform.Find(
                "PartsBase/PartsMask/DifficultyLevel/" +
                "DifficultyLevelBackgroundImage")?.GetComponent<Image>();
            if (emptyDifficultyBackground != null)
                emptyDifficultyBackground.enabled = true;
            foreach (var button in cell.GetComponentsInChildren<Button>(true))
            {
                button.onClick.RemoveAllListeners();
                var captured = binding;
                if (button.name == "BookmarkButton")
                {
                    button.onClick.AddListener(() =>
                    {
                        _se.Play(RecoveredUiSeRuntime.Cue.ButtonGo);
                        OpenBookmarkDialog(captured.music);
                    });
                }
                else
                    button.onClick.AddListener(() => SnapToCell(captured, true));
            }
            cell.SetActive(false);
        }

        private void UpdateVisibleCellBindings(bool force = false)
        {
            if (_physicalCells.Count == 0 || _catalog?.Musics == null ||
                _catalog.Musics.Length == 0)
                return;
            var totalCount = _catalog.Musics.Length * LoopCopies;
            var center = Mathf.Clamp(NearestPhysicalIndex(), 0, totalCount - 1);
            var start = Mathf.Clamp(
                center - _physicalCells.Count / 2,
                0,
                Mathf.Max(0, totalCount - _physicalCells.Count));
            for (var poolIndex = 0; poolIndex < _physicalCells.Count; poolIndex++)
            {
                var physicalIndex = start + poolIndex;
                var binding = _physicalCells[poolIndex];
                if (!force && binding.physicalIndex == physicalIndex) continue;
                RebindListCell(
                    binding,
                    _catalog.Musics[physicalIndex % _catalog.Musics.Length],
                    physicalIndex);
            }
            UpdateListCellVerticalPositions();
        }

        private void RebindListCell(
            ListCellBinding binding,
            RecoveredLocalMusicEntry music,
            int physicalIndex)
        {
            if (binding.music != null &&
                _listCells.TryGetValue(binding.music.Id, out var previousCells))
            {
                previousCells.Remove(binding);
                if (previousCells.Count == 0) _listCells.Remove(binding.music.Id);
            }
            if (_focusedCell == binding)
            {
                binding.focusSequence?.Kill(false);
                _focusedCell = null;
            }
            binding.music = music;
            binding.physicalIndex = physicalIndex;
            binding.gameObject.name = "LocalMusic_" + music.Id;
            binding.gameObject.SetActive(true);
            if (!_listCells.TryGetValue(music.Id, out var cells))
            {
                cells = new List<ListCellBinding>();
                _listCells[music.Id] = cells;
            }
            cells.Add(binding);
            var root = binding.gameObject.transform;
            SetCellText(root, "PartsBase/PartsMask/MusicTitle/BodyText", music.Name);
            SetCellText(root, "PartsBase/PartsMask/Vocals/BodyText", music.Vocals);
            RefreshCellBookmarkTags(root, music.Id);
            var difficulty = _selection != null
                ? _selection.Live.Difficulty
                : RecoveredMusicDifficulty.Stella;
            ApplyCellDifficultyColor(root, difficulty);
            var hasLive = TryGetLive(music, difficulty, out var live);
            SetCellText(
                root,
                "PartsBase/PartsMask/DifficultyLevel/DifficultyLevelText",
                hasLive ? FormatLevel(difficulty, live.Level) : string.Empty);
            var difficultyBackground = root.Find(
                "PartsBase/PartsMask/DifficultyLevel/" +
                "DifficultyLevelBackgroundImage")?.GetComponent<Image>();
            if (difficultyBackground != null)
            {
                difficultyBackground.enabled = hasLive;
                if (hasLive && _markerSprites.TryGetValue(difficulty, out var marker))
                {
                    difficultyBackground.sprite = marker;
                    difficultyBackground.overrideSprite = marker;
                    difficultyBackground.color = Color.white;
                    difficultyBackground.preserveAspect = true;
                }
            }
            ApplyCellLocalResults(root, music);
            ApplyCellRateGrade(binding, difficulty);
            JacketResourceProvider.TryGetValue(music.Id, out var jacket);
            ApplyJacketToCell(binding, jacket);
            if (jacket == null) EnsureJacketLoaded(music);
            ApplyCellFocus(binding, false);
        }

        private void SelectDifficulty(
            RecoveredMusicDifficulty difficulty,
            bool playSound)
        {
            if (!_charts.ContainsKey(difficulty) ||
                _selection == null ||
                !TryGetLive(_selection.Music, difficulty, out _))
                return;
            _selection = _catalog.Select(_selection.Music.Id, difficulty);
            var difficultyColor =
                RecoveredMusicSelectionPreviewRuntime.DifficultyFrameColor(difficulty);
            foreach (var pair in _difficultyButtons)
            {
                var image = pair.Value.GetComponent<Image>();
                if (image == null) continue;
                var index = (int)pair.Key - 1;
                image.sprite = pair.Key == difficulty &&
                    index >= 0 &&
                    index < _difficultyButtonOnSprites.Length
                    ? _difficultyButtonOnSprites[index]
                    : _difficultyButtonOffSprite;
                image.overrideSprite = image.sprite;
                image.color = Color.white;
                var buttonLevelText = pair.Value.transform.Find(
                    "DifficultyLevel")?.GetComponent<Text>();
                Color textColor = pair.Key == difficulty
                    ? Color.white
                    : UnselectedDifficultyTextColor;
                if (buttonLevelText != null) buttonLevelText.color = textColor;
                var buttonLabel = pair.Value.transform.Find(
                    "DifficultyLabel")?.GetComponent<Graphic>();
                if (buttonLabel != null) buttonLabel.color = textColor;
            }
            var informationPanel = _view.transform.Find(
                "TicketMacine/MusicInformationPanel");
            var informationFrame = informationPanel != null
                ? informationPanel.GetComponent<Image>()
                : null;
            if (informationFrame != null) informationFrame.color = difficultyColor;
            _preview?.SetDifficultyColor(difficulty);
            var levelText = FindText(
                "DefaultLivePanels/DifficultyPanel/" + difficulty + "/DifficultyLevel");
            if (levelText != null)
                levelText.text = FormatLevel(difficulty, _selection.Live.Level);
            RefreshListDifficulty(difficulty);
            RefreshScorePanel();
            RefreshListRateGrades(difficulty);
            if (playSound) _se.Play(RecoveredUiSeRuntime.Cue.ButtonGo);
            if (playSound && !_isRebuildingMusicList)
                StartCoroutine(RebuildFilteredMusicList());
        }

        private void ApplyCellLocalResults(
            Transform root,
            RecoveredLocalMusicEntry music)
        {
            if (root == null || music == null || _localResults == null) return;
            foreach (RecoveredMusicDifficulty difficulty in Enum.GetValues(
                         typeof(RecoveredMusicDifficulty)))
            {
                if (difficulty == RecoveredMusicDifficulty.None) continue;
                var lampRoot = root.Find(
                    "PartsBase/PartsMask/ClearLamps/ClearLamp" + difficulty);
                if (lampRoot == null) continue;
                var status = _localResults.GetClearLamp(music.Id, difficulty);
                var normal = lampRoot.Find("NormalClearLamp")
                    ?.GetComponent<Image>();
                if (normal != null)
                {
                    normal.enabled = status != RecoveredClearLamp.None;
                    var spriteIndex = (int)status - 1;
                    if (spriteIndex >= 0 &&
                        spriteIndex < _clearLampSprites.Length)
                    {
                        normal.sprite = _clearLampSprites[spriteIndex];
                        normal.overrideSprite = normal.sprite;
                    }
                }
                for (var index = 1; index <= 3; index++)
                {
                    var multi = lampRoot.Find("MultiClearLamp" + index)
                        ?.GetComponent<Image>();
                    if (multi != null) multi.enabled = false;
                }
            }
        }

        private void RefreshListRateGrades(
            RecoveredMusicDifficulty difficulty)
        {
            if (_localResults == null) return;
            foreach (var music in _catalog.Musics)
            {
                if (!_listCells.TryGetValue(music.Id, out var listCells))
                    continue;
                var grade = Sirius.GameResult.GameResultPanel
                    .GetAchievementGrade(
                        _localResults.GetBest(music.Id, difficulty));
                foreach (var listCell in listCells)
                    ApplyCellRateGrade(listCell, difficulty, grade);
            }
        }

        private void ApplyCellRateGrade(
            ListCellBinding listCell,
            RecoveredMusicDifficulty difficulty,
            int grade = -1)
        {
            if (listCell?.music == null || _localResults == null) return;
            if (grade < 0)
            {
                grade = Sirius.GameResult.GameResultPanel.GetAchievementGrade(
                    _localResults.GetBest(listCell.music.Id, difficulty));
            }
            var badge = listCell.gameObject.transform.Find(
                    "PartsBase/PartsMask/RateGradeBadge")
                ?.GetComponent<Image>();
            if (badge == null) return;
            badge.enabled = grade > 0;
            if (grade <= 0 || grade > _rateGradeSprites.Length) return;
            badge.sprite = _rateGradeSprites[grade - 1];
            badge.overrideSprite = badge.sprite;
            // Retail cells keep the badge inside a fixed box.
            badge.preserveAspect = true;
        }

        private void RefreshScorePanel()
        {
            if (_selection == null || _localResults == null) return;
            var best = _localResults.GetBest(
                _selection.Music.Id, _selection.Live.Difficulty);
            var rateRoot = _view.transform.Find(
                "DefaultLivePanels/ScorePanel/RatePanel");
            var musicRateFrame = rateRoot != null
                ? rateRoot.Find("MusicRateFrame")
                : null;
            var rateTarget = musicRateFrame != null
                ? musicRateFrame.Find("MusicRateTarget")
                : null;
            var rateNotSubject = musicRateFrame != null
                ? musicRateFrame.Find("RateNotSubject")
                : null;
            var isOlivier =
                _selection.Live.Difficulty == RecoveredMusicDifficulty.Olivier;
            var notationRate = RecoveredPlayerRating.IsEligible(
                    _selection.Music, _selection.Live)
                ? RecoveredPlayerRating.CalculateNotationRate(
                    _selection.Live.Level,
                    best,
                    _localResults.HasClear(
                        _selection.Music.Id,
                        _selection.Live.Difficulty))
                : 0d;
            var isRateTarget = notationRate > 0d;
            if (rateTarget != null)
                rateTarget.gameObject.SetActive(isOlivier || isRateTarget);
            if (rateNotSubject != null)
                rateNotSubject.gameObject.SetActive(!isOlivier && !isRateTarget);
            var rateFrameImage =
                musicRateFrame != null
                    ? musicRateFrame.GetComponent<Image>()
                    : null;
            if (rateFrameImage != null)
                rateFrameImage.color = isOlivier
                    ? new Color32(65, 66, 77, 255)
                    : new Color32(48, 166, 244, 255);
            SetText(
                "DefaultLivePanels/ScorePanel/RatePanel/" +
                "AchievementRateFrame/AchievementRate",
                FormatAchievementRate(best));
            SetText(
                "DefaultLivePanels/ScorePanel/RatePanel/" +
                "MusicRateFrame/MusicRateLabel",
                isOlivier ? "星章" : "レート");
            SetText(
                "DefaultLivePanels/ScorePanel/RatePanel/" +
                "MusicRateFrame/MusicRate",
                isOlivier
                    ? CalculateLocalOlivierStars(_selection.Live, best).ToString()
                    : notationRate.ToString(
                        "0.00",
                        System.Globalization.CultureInfo.InvariantCulture));
            RefreshPlayerRateHeader();
        }

        private static int CalculateLocalOlivierStars(
            RecoveredLocalLiveEntry live,
            double achievementRate)
        {
            // The retail SetOlivierRate receives the server-calculated SP point
            // rather than deriving it in the client. Keep that API boundary:
            // the offline adapter maps its locally owned achievement result to
            // the 0..75 display range used by the recovered Olivier charts.
            return Mathf.Clamp(
                Mathf.FloorToInt((float)(achievementRate * 0.75d)),
                0,
                75);
        }

        private static string FormatAchievementRate(double value)
        {
            // FloatExtensions.ToStringForAchievementRate is called with the
            // retail default fontSize=26. The first two fractional digits use
            // the Text component's authored 32 px; the final two and unit are
            // smaller.
            var formatted = Math.Max(0d, value).ToString(
                "0.0000",
                System.Globalization.CultureInfo.InvariantCulture);
            var split = Math.Max(0, formatted.Length - 2);
            return formatted.Substring(0, split) +
                   "<size=26>" + formatted.Substring(split) + "%</size>";
        }

        private static Color32 DifficultyLabelColor(
            RecoveredMusicDifficulty difficulty)
        {
            if (difficulty == RecoveredMusicDifficulty.Stella)
            {
                // ColorPreset.get_Difficulty_Stella, RVA 0xA59F4BC:
                // packed Color32 0xFFE96786 -> RGBA (134,103,233,255).
                return new Color32(134, 103, 233, 255);
            }
            return RecoveredMusicSelectionPreviewRuntime
                .DifficultyFrameColor(difficulty);
        }

        private void RefreshListDifficulty(RecoveredMusicDifficulty difficulty)
        {
            _markerSprites.TryGetValue(difficulty, out var markerSprite);
            foreach (var music in _catalog.Musics)
            {
                if (!_listCells.TryGetValue(music.Id, out var listCells))
                    continue;
                foreach (var listCell in listCells)
                {
                    ApplyCellDifficultyColor(listCell.gameObject.transform, difficulty);
                    var hasLive = TryGetLive(music, difficulty, out var live);
                    SetCellText(
                        listCell.gameObject.transform,
                        "PartsBase/PartsMask/DifficultyLevel/DifficultyLevelText",
                        hasLive ? FormatLevel(difficulty, live.Level) : string.Empty);
                    var background = listCell.gameObject.transform.Find(
                        "PartsBase/PartsMask/DifficultyLevel/" +
                        "DifficultyLevelBackgroundImage")?.GetComponent<Image>();
                    if (background == null) continue;
                    background.enabled = hasLive;
                    if (!hasLive) continue;
                    background.sprite = markerSprite;
                    background.overrideSprite = markerSprite;
                    background.color = Color.white;
                    background.preserveAspect = true;
                }
            }
        }

        private void Update()
        {
            if (_listContent == null || _catalog == null ||
                _catalog.Musics == null || _catalog.Musics.Length == 0)
                return;
            if (_snapCoroutine == null) NormalizeLoopPosition();
            UpdateVisibleCellBindings();
            UpdateListCellHorizontalPositions();
            if (_snapPending &&
                !_listDragging &&
                Mathf.Abs(_listScrollRect.velocity.y) <= SnapVelocityThreshold)
            {
                _snapPending = false;
                SnapToNearestCell();
            }
        }

        public void OnListBeginDrag()
        {
            _listDragging = true;
            _snapPending = false;
            if (_snapCoroutine != null)
            {
                StopCoroutine(_snapCoroutine);
                _snapCoroutine = null;
            }
        }

        public void OnListEndDrag()
        {
            _listDragging = false;
            _snapPending = true;
        }

        private void NormalizeLoopPosition()
        {
            var logicalCount = _catalog.Musics.Length;
            var index = NearestPhysicalIndex();
            var cycle = logicalCount * CellStride;
            var crossedCycles =
                Mathf.FloorToInt(index / (float)logicalCount) - 1;
            if (crossedCycles == 0) return;

            // EnhancedScroller's retail loop keeps its logical scroll position
            // continuous while recycling cells. Moving a ScrollRect Content
            // alone makes ScrollRect interpret the recycle jump as a huge input
            // delta on LateUpdate; its smoothed velocity can consequently flip
            // sign. Rebase all three pieces of ScrollRect state by the same
            // amount and preserve the authored drag/inertia velocity.
            var deltaY = -crossedCycles * cycle;
            var velocity = _listScrollRect.velocity;
            var position = _listContent.anchoredPosition;
            position.y += deltaY;
            _listContent.anchoredPosition = position;
            SetScrollRectVector2(
                ScrollRectPreviousPositionField,
                _listContent.anchoredPosition);
            ShiftScrollRectVector2(
                ScrollRectContentStartPositionField,
                new Vector2(0f, deltaY));
            _listScrollRect.velocity = velocity;
            if (_loopVelocityRestoreCoroutine != null)
                StopCoroutine(_loopVelocityRestoreCoroutine);
            _loopVelocityRestoreCoroutine =
                StartCoroutine(RestoreLoopVelocityAfterLateUpdate(velocity));
            UpdateVisibleCellBindings(true);
        }

        private IEnumerator RestoreLoopVelocityAfterLateUpdate(Vector2 velocity)
        {
            yield return new WaitForEndOfFrame();
            if (_listScrollRect != null &&
                Mathf.Abs(velocity.y) > 0.01f &&
                Mathf.Sign(_listScrollRect.velocity.y) != Mathf.Sign(velocity.y))
                _listScrollRect.velocity = velocity;
            _loopVelocityRestoreCoroutine = null;
        }

        private void ShiftScrollRectVector2(FieldInfo field, Vector2 delta)
        {
            if (field == null)
                throw new MissingFieldException(
                    typeof(ScrollRect).FullName,
                    "m_PrevPosition/m_ContentStartPosition");
            var value = (Vector2)field.GetValue(_listScrollRect);
            field.SetValue(_listScrollRect, value + delta);
        }

        private void SetScrollRectVector2(FieldInfo field, Vector2 value)
        {
            if (field == null)
                throw new MissingFieldException(
                    typeof(ScrollRect).FullName,
                    "m_PrevPosition");
            field.SetValue(_listScrollRect, value);
        }

        private void UpdateListCellHorizontalPositions()
        {
            var viewportCenter = _listScrollRect.viewport.TransformPoint(
                _listScrollRect.viewport.rect.center);
            foreach (var binding in _physicalCells)
            {
                if (binding == null || binding.rect == null) continue;
                var partsBase = binding.gameObject.transform.Find(
                    "PartsBase") as RectTransform;
                if (partsBase == null) continue;
                if (binding == _focusedCell)
                {
                    SetAnchoredPositionX(partsBase, 0f);
                    continue;
                }
                var cellCenter = binding.rect.TransformPoint(
                    binding.rect.rect.center);
                var distance = Mathf.Abs(
                    _listScrollRect.viewport.InverseTransformPoint(cellCenter).y -
                    _listScrollRect.viewport.InverseTransformPoint(viewportCenter).y);
                var x = Mathf.Min(
                    CellCurveLimit,
                    distance * CellCurveScalar - CellCurveOffset);
                SetAnchoredPositionX(partsBase, x);
            }
        }

        private int NearestPhysicalIndex()
        {
            var viewportHeight = _listScrollRect.viewport.rect.height;
            var centerFromTop =
                _listContent.anchoredPosition.y + viewportHeight * 0.5f;
            return Mathf.RoundToInt(
                (centerFromTop - OffsetCellHeight * 0.5f) / CellStride);
        }

        private void SetContentForPhysicalIndex(int physicalIndex)
        {
            var position = _listContent.anchoredPosition;
            position.y =
                physicalIndex * CellStride +
                OffsetCellHeight * 0.5f -
                _listScrollRect.viewport.rect.height * 0.5f;
            _listContent.anchoredPosition = position;
        }

        private void SnapToNearestCell()
        {
            var index = Mathf.Clamp(
                NearestPhysicalIndex(),
                0,
                _catalog.Musics.Length * LoopCopies - 1);
            UpdateVisibleCellBindings();
            SnapToCell(FindCellAtPhysicalIndex(index), false);
        }

        private ListCellBinding FindCellAtPhysicalIndex(int physicalIndex) =>
            _physicalCells.FirstOrDefault(
                binding => binding.physicalIndex == physicalIndex);

        private ListCellBinding FindVisibleCell(long musicId) =>
            _listCells.TryGetValue(musicId, out var cells)
                ? cells.OrderBy(cell => Math.Abs(
                        cell.physicalIndex - NearestPhysicalIndex()))
                    .FirstOrDefault()
                : null;

        private void SnapToMusic(RecoveredLocalMusicEntry music, bool playSound)
        {
            if (music == null || _catalog?.Musics == null) return;
            var logicalIndex = Array.FindIndex(
                _catalog.Musics, item => item.Id == music.Id);
            if (logicalIndex < 0) return;
            var current = NearestPhysicalIndex();
            var logicalCount = _catalog.Musics.Length;
            var candidates = new[]
            {
                logicalIndex,
                logicalCount + logicalIndex,
                logicalCount * 2 + logicalIndex,
            };
            var target = candidates.OrderBy(index => Math.Abs(index - current)).First();
            if (_snapCoroutine != null) StopCoroutine(_snapCoroutine);
            SetSelectionControlsInteractable(false);
            _snapCoroutine = StartCoroutine(
                SnapPhysicalIndex(target, music, playSound));
        }

        private void SnapToCell(ListCellBinding binding, bool playSound)
        {
            if (binding == null) return;
            if (_snapCoroutine != null) StopCoroutine(_snapCoroutine);
            SetSelectionControlsInteractable(false);
            _snapCoroutine = StartCoroutine(SnapCell(binding, playSound));
        }

        private IEnumerator SnapCell(ListCellBinding binding, bool playSound)
        {
            if (binding == null) yield break;
            yield return SnapPhysicalIndex(
                binding.physicalIndex, binding.music, playSound);
        }

        private IEnumerator SnapPhysicalIndex(
            int physicalIndex,
            RecoveredLocalMusicEntry music,
            bool playSound)
        {
            _listScrollRect.StopMovement();
            var start = _listContent.anchoredPosition;
            SetContentForPhysicalIndex(physicalIndex);
            var target = _listContent.anchoredPosition;
            _listContent.anchoredPosition = start;
            for (var elapsed = 0f; elapsed < SnapTweenTime;
                 elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / SnapTweenTime);
                // EnhancedScroller snapTweenType=15 (OutCubic).
                t = 1f - Mathf.Pow(1f - t, 3f);
                _listContent.anchoredPosition = Vector2.LerpUnclamped(start, target, t);
                yield return null;
            }
            _listContent.anchoredPosition = target;
            UpdateVisibleCellBindings();
            var binding = RebaseSnappedBinding(
                FindCellAtPhysicalIndex(physicalIndex));
            FocusCell(binding);
            yield return SelectMusic(music, playSound);
            _snapCoroutine = null;
            SetSelectionControlsInteractable(true);
        }

        private ListCellBinding RebaseSnappedBinding(ListCellBinding binding)
        {
            if (binding == null || _catalog == null) return binding;
            var logicalCount = _catalog.Musics.Length;
            var logicalIndex = binding.physicalIndex % logicalCount;
            var targetIndex = logicalCount + logicalIndex;
            if (binding.physicalIndex == targetIndex) return binding;

            // Recycle the identical loop copy before applying focus geometry.
            // Focusing the edge copy first and then transferring focus made all
            // cell heights/positions change twice in one frame at Music 1↔10.
            var deltaY =
                (targetIndex - binding.physicalIndex) * CellStride;
            var position = _listContent.anchoredPosition;
            position.y += deltaY;
            _listContent.anchoredPosition = position;
            SetScrollRectVector2(
                ScrollRectPreviousPositionField,
                _listContent.anchoredPosition);
            UpdateVisibleCellBindings(true);
            return FindCellAtPhysicalIndex(targetIndex) ?? binding;
        }

        private void FocusCell(ListCellBinding binding)
        {
            if (binding == null) return;
            if (_focusedCell == binding) return;
            if (_focusedCell != null) ApplyCellFocus(_focusedCell, false, true);
            _focusedCell = binding;
            ApplyCellFocus(binding, true, true);
            UpdateListCellVerticalPositions();
            UpdateListCellHorizontalPositions();
        }

        private void UpdateListCellVerticalPositions()
        {
            var focusIndex = _focusedCell != null
                ? _focusedCell.physicalIndex
                : -1;
            var focusGrowth = (TargetCellHeight - OffsetCellHeight) * 0.5f;
            foreach (var binding in _physicalCells)
            {
                if (binding == null || binding.rect == null) continue;
                var y = -binding.physicalIndex * CellStride -
                    OffsetCellHeight * 0.5f;
                if (focusIndex >= 0 && binding.physicalIndex < focusIndex)
                    y += focusGrowth;
                else if (focusIndex >= 0 && binding.physicalIndex > focusIndex)
                    y -= focusGrowth;
                binding.rect.anchoredPosition = new Vector2(ListLeftInset, y);
            }
        }

        private void ApplyCellFocus(
            ListCellBinding binding,
            bool focused,
            bool animate = false)
        {
            if (!animate)
            {
                ApplyCellFocusImmediate(binding, focused, false);
                return;
            }
            if (binding == null || binding.rect == null) return;

            var animatedRects = FocusTweenRects(binding.gameObject.transform);
            var startSizes = new Vector2[animatedRects.Length];
            var startPositions = new Vector2[animatedRects.Length];
            for (var index = 0; index < animatedRects.Length; index++)
            {
                startSizes[index] = animatedRects[index].sizeDelta;
                startPositions[index] = animatedRects[index].anchoredPosition;
            }

            ApplyCellFocusImmediate(binding, focused, focused);

            binding.focusSequence?.Kill(false);
            var sequence = DOTween.Sequence().SetLink(binding.gameObject);
            for (var index = 0; index < animatedRects.Length; index++)
            {
                var rect = animatedRects[index];
                var targetSize = rect.sizeDelta;
                var targetPosition = rect.anchoredPosition;
                rect.sizeDelta = startSizes[index];
                rect.anchoredPosition = startPositions[index];
                if ((targetSize - startSizes[index]).sqrMagnitude > 0.0001f)
                    sequence.Join(rect.DOSizeDelta(
                        targetSize, CellFocusTweenTime));
                if ((targetPosition - startPositions[index]).sqrMagnitude >
                    0.0001f)
                    sequence.Join(rect.DOAnchorPos(
                        targetPosition, CellFocusTweenTime));
            }
            if (focused)
            {
                sequence.OnComplete(() =>
                {
                    SetScrollAnimation(
                        binding.gameObject.transform.Find(
                            "PartsBase/PartsMask/MusicTitle"),
                        true);
                    SetScrollAnimation(
                        binding.gameObject.transform.Find(
                            "PartsBase/PartsMask/Vocals"),
                        true);
                });
            }
            sequence.OnUpdate(() =>
            {
                var height = binding.rect.sizeDelta.y;
                if (height > OffsetCellHeight + 0.1f &&
                    height < TargetCellHeight - 0.1f)
                    _focusTweenObservedIntermediate = true;
            });
            binding.focusSequence = sequence;
            sequence.Play();
        }

        private static RectTransform[] FocusTweenRects(Transform root)
        {
            var result = new List<RectTransform>();
            AddFocusTweenRect(result, root as RectTransform);
            AddFocusTweenRect(result, root.Find("PartsBase") as RectTransform);
            AddFocusTweenRect(
                result,
                root.Find("PartsBase/PartsMask/BackgroundMusicJacket")
                    as RectTransform);
            AddFocusTweenRect(
                result,
                root.Find("PartsBase/PartsMask/FocusMask") as RectTransform);
            AddFocusTweenRect(
                result,
                root.Find(
                    "PartsBase/PartsMask/FocusMask/FocusTargetMusicJacket")
                    as RectTransform);
            AddFocusTweenRect(
                result,
                root.Find("PartsBase/PartsMask/MusicTitle") as RectTransform);
            AddFocusTweenRect(
                result,
                root.Find("PartsBase/PartsMask/BorderPoints") as RectTransform);
            AddFocusTweenRect(
                result,
                root.Find("PartsBase/PartsMask/RateGradeBadge")
                    as RectTransform);
            return result.ToArray();
        }

        private static void AddFocusTweenRect(
            ICollection<RectTransform> result,
            RectTransform rect)
        {
            if (rect != null) result.Add(rect);
        }

        private void ApplyCellFocusImmediate(
            ListCellBinding binding,
            bool focused,
            bool deferFocusedScroll)
        {
            if (binding == null || binding.rect == null) return;
            binding.rect.sizeDelta = focused
                ? new Vector2(TargetCellWidth, TargetCellHeight)
                : new Vector2(OffsetCellWidth, OffsetCellHeight);
            var root = binding.gameObject.transform;
            var layout = binding.gameObject.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredWidth = focused ? TargetCellWidth : -1f;
                layout.preferredHeight = focused ? TargetCellHeight : -1f;
            }
            var partsBase = root.Find("PartsBase") as RectTransform;
            if (partsBase != null)
            {
                partsBase.sizeDelta = focused
                    ? new Vector2(TargetCellWidth, TargetCellHeight)
                    : new Vector2(OffsetCellWidth, OffsetCellHeight);
                if (focused) SetAnchoredPositionX(partsBase, 0f);
            }
            var background = root.Find(
                "PartsBase/PartsMask/BackgroundMusicJacket") as RectTransform;
            if (background != null)
                background.sizeDelta = focused
                    ? new Vector2(960f, 960f)
                    : new Vector2(OffsetCellWidth, OffsetCellWidth);
            SetGraphicEnabled(
                root, "PartsBase/PartsMask/JacketOverlayImage", focused);
            SetGraphicEnabled(
                root,
                "PartsBase/PartsMask/FocusMask/DifficultyFrameImage",
                !focused);
            SetGraphicEnabled(
                root, "PartsBase/PartsMask/DropShadow", focused);
            ApplyCellDifficultyColor(
                root,
                _selection != null
                    ? _selection.Live.Difficulty
                    : RecoveredMusicDifficulty.Stella);
            var focusMaskImage = root.Find(
                "PartsBase/PartsMask/FocusMask")?.GetComponent<Image>();
            if (focusMaskImage != null)
            {
                focusMaskImage.sprite = focused
                    ? _listJacketTargetMaskSprite
                    : _listJacketOffsetMaskSprite;
                focusMaskImage.overrideSprite = focusMaskImage.sprite;
            }
            var focusMaskRect = root.Find(
                "PartsBase/PartsMask/FocusMask") as RectTransform;
            if (focusMaskRect != null)
                focusMaskRect.sizeDelta = focused
                    ? new Vector2(TargetJacketMaskWidth, TargetCellHeight)
                    : new Vector2(OffsetJacketMaskWidth, OffsetCellHeight);
            var focusJacket = root.Find(
                "PartsBase/PartsMask/FocusMask/FocusTargetMusicJacket")
                as RectTransform;
            if (focusJacket != null)
                focusJacket.sizeDelta = focused
                    ? new Vector2(TargetJacketMaskWidth, TargetJacketMaskWidth)
                    : new Vector2(OffsetJacketMaskWidth, OffsetJacketMaskWidth);
            var difficulty = root.Find(
                "PartsBase/PartsMask/DifficultyLevel") as RectTransform;
            if (difficulty != null)
            {
                difficulty.sizeDelta = focused
                    ? new Vector2(88f, 88f)
                    : new Vector2(76f, 76f);
            }
            var levelTransform = root.Find(
                "PartsBase/PartsMask/DifficultyLevel/DifficultyLevelText")
                as RectTransform;
            if (levelTransform != null)
                levelTransform.anchoredPosition = focused
                    ? new Vector2(14f, 19f)
                    : new Vector2(6f, 14f);
            var level = levelTransform != null
                ? levelTransform.GetComponent<Text>()
                : null;
            if (level != null) level.fontSize = focused ? 48 : 40;
            var title = root.Find(
                "PartsBase/PartsMask/MusicTitle/BodyText")?.GetComponent<Text>();
            if (title != null)
            {
                title.fontSize = focused ? 52 : 40;
                // MusicTitleParts.ChangeAppearanceToTarget sets RGBA to
                // (1,1,1,1); ResetView restores ColorPreset.DarkGray.
                title.color = focused
                    ? Color.white
                    : new Color32(86, 88, 103, 255);
            }
            var vocals = root.Find(
                "PartsBase/PartsMask/Vocals/BodyText")?.GetComponent<Text>();
            if (vocals != null)
            {
                // VocalsParts is intentionally visible only on the target cell.
                vocals.enabled = focused;
                vocals.color = Color.white;
            }
            if (!focused || !deferFocusedScroll)
            {
                SetScrollAnimation(
                    root.Find("PartsBase/PartsMask/MusicTitle"), focused);
                SetScrollAnimation(
                    root.Find("PartsBase/PartsMask/Vocals"), focused);
            }
            SetRect(
                root,
                "PartsBase/PartsMask/MusicTitle",
                focused ? new Vector2(180f, 56.5f) : new Vector2(168.55f, 35.5f),
                focused ? new Vector2(569f, 69f) : new Vector2(510.25f, 53f));
            SetRectPosition(
                root,
                "PartsBase/PartsMask/MusicTypePanel",
                focused ? new Vector2(242.5f, -4f) : new Vector2(211f, -4f));
            SetRectPosition(
                root,
                "PartsBase/PartsMask/ClearLamps",
                focused ? new Vector2(336.5f, 11f) : new Vector2(327.5f, 9f));
            var lamps = root.Find("PartsBase/PartsMask/ClearLamps");
            if (lamps != null)
            {
                // ClearLampsParts._lampRectTransforms contains the five direct
                // difficulty roots only. Their inset foreground children keep
                // the authored stretch anchors and -12 sizeDelta.
                for (var index = 0; index < lamps.childCount; index++)
                {
                    var lamp = lamps.GetChild(index) as RectTransform;
                    if (lamp == null) continue;
                    lamp.sizeDelta = focused
                        ? new Vector2(52f, 52f)
                        : new Vector2(46f, 46f);
                }
            }
            SetRect(
                root,
                "PartsBase/PartsMask/BorderPoints",
                focused ? new Vector2(361f, -114f) : new Vector2(341.5f, -98f),
                focused ? new Vector2(572f, 6f) : new Vector2(502f, 6f));
            SetRect(
                root,
                "PartsBase/PartsMask/RateGradeBadge",
                focused ? new Vector2(824f, -136f) : new Vector2(744.5f, -116f),
                focused ? new Vector2(108f, 44f) : new Vector2(98f, 40f));
            var rateGradeBadge = root.Find(
                    "PartsBase/PartsMask/RateGradeBadge")
                ?.GetComponent<Image>();
            if (rateGradeBadge != null)
                rateGradeBadge.preserveAspect = true;
            var animator = root.Find(
                "PartsBase/PartsMask/BackgroundMusicJacket")
                ?.GetComponent<Animator>();
            if (animator != null) animator.SetBool("IsTarget", focused);
            var bookmarkButton = root.Find(
                "PartsBase/BookmarkGroup/BookmarkButton");
            if (bookmarkButton != null)
                bookmarkButton.gameObject.SetActive(focused);
        }

        private void ApplyInformationPanelLayout()
        {
            var informationPanel = _view.transform.Find(
                "TicketMacine/MusicInformationPanel");
            if (informationPanel == null) return;

            // The two ScrollText children use HorizontalFit only. Stretching
            // their Text rects to the authored parent height keeps the retail
            // MiddleLeft alignment vertically centered after the local host
            // replaces the placeholder strings.
            CenterInformationText(
                informationPanel.Find("ScrollMusicTitle/ScrollMusicTitleText"));
            CenterInformationText(
                informationPanel.Find("ScrollCreator/ScrollCreatorText"));
            ConfigureScrollText(
                informationPanel.Find("ScrollMusicTitle"), true, true);
            ConfigureScrollText(
                informationPanel.Find("ScrollCreator"), true, true);

            // MusicCoverTypes.Original == 1. ARM64
            // ChangeMusicTypePanelBackground enables OriginalBackground only
            // for that exact value; the recovered prefab defaults to Cover.
            var musicType = informationPanel.Find("MusicType");
            if (musicType == null) return;
            var cover = musicType.Find("CoverBackground")?.GetComponent<Image>();
            var original =
                musicType.Find("OriginalBackground")?.GetComponent<Image>();
            var musicTypeText =
                musicType.Find("MusicTypeText")?.GetComponent<Text>();
            if (cover != null) cover.enabled = false;
            if (original != null) original.enabled = true;
            if (musicTypeText != null) musicTypeText.color = Color.white;
        }

        private static void CenterInformationText(Transform target)
        {
            var rect = target as RectTransform;
            var text = target != null ? target.GetComponent<Text>() : null;
            if (rect == null || text == null) return;
            var viewport = rect.parent as RectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(
                rect.sizeDelta.x,
                viewport != null ? viewport.rect.height : rect.sizeDelta.y);
            text.alignment = TextAnchor.MiddleLeft;
        }

        private static void ConfigureScrollText(
            Transform viewport,
            bool animate = false,
            bool contentChanged = false)
        {
            if (viewport == null) return;
            var body = viewport.Find("BodyText")?.GetComponent<Text>();
            if (body == null)
                body = viewport.GetComponentInChildren<Text>(true);
            if (body == null) return;
            var scrollText =
                viewport.GetComponent<RecoveredScrollTextRuntime>();
            if (scrollText == null)
                scrollText = viewport.gameObject.AddComponent<
                    RecoveredScrollTextRuntime>();
            var cubicAppearance =
                viewport.name != "Vocals";
            scrollText.Configure(body, cubicAppearance);
            scrollText.SetAnimation(animate);
            if (contentChanged) scrollText.RestartForNewContent();
        }

        private static void SetScrollAnimation(Transform viewport, bool animate)
        {
            var scrollText =
                viewport != null
                    ? viewport.GetComponent<RecoveredScrollTextRuntime>()
                    : null;
            if (scrollText != null) scrollText.SetAnimation(animate);
        }

        private static void ApplyCellDifficultyColor(
            Transform root,
            RecoveredMusicDifficulty difficulty)
        {
            var color =
                RecoveredMusicSelectionPreviewRuntime.DifficultyFrameColor(difficulty);
            SetImageColor(root, "PartsBase/PartsMask/JacketOverlayImage", color);
            SetImageColor(
                root,
                "PartsBase/PartsMask/FocusMask/DifficultyFrameImage",
                color);
            SetImageColor(root, "PartsBase/PartsMask/DropShadow", color);
        }

        private static void SetImageColor(
            Transform root,
            string path,
            Color color)
        {
            var image = root.Find(path)?.GetComponent<Image>();
            if (image != null) image.color = color;
        }

        private static void SetRect(
            Transform root,
            string path,
            Vector2 position,
            Vector2 size)
        {
            var rect = root.Find(path) as RectTransform;
            if (rect == null) return;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetRectPosition(
            Transform root,
            string path,
            Vector2 position)
        {
            var rect = root.Find(path) as RectTransform;
            if (rect != null) rect.anchoredPosition = position;
        }

        private static void SetAnchoredPositionX(RectTransform rect, float x)
        {
            var position = rect.anchoredPosition;
            position.x = x;
            rect.anchoredPosition = position;
        }

        private static void SetActive(Transform root, string path, bool active)
        {
            var target = root.Find(path);
            if (target != null) target.gameObject.SetActive(active);
        }

        private static void SetGraphicEnabled(
            Transform root,
            string path,
            bool enabled)
        {
            var graphic = root.Find(path)?.GetComponent<Graphic>();
            if (graphic != null) graphic.enabled = enabled;
        }

        private IEnumerator SelectMusic(
            RecoveredLocalMusicEntry music, bool playSound)
        {
            if (music == null || (_selection != null && _selection.Music.Id == music.Id))
                yield break;
            var previousDifficulty = _selection != null
                ? _selection.Live.Difficulty
                : RecoveredMusicDifficulty.Stella;
            yield return LoadMusicAssets(music);
            BindView(music);
            if (!_charts.ContainsKey(previousDifficulty) ||
                !TryGetLive(music, previousDifficulty, out _))
                previousDifficulty = RecoveredMusicDifficulty.Stella;
            _selection = _catalog.Select(music.Id, previousDifficulty);
            SelectDifficulty(previousDifficulty, false);
            _preview.Play(music.Id);
            if (playSound) _se.Play(RecoveredUiSeRuntime.Cue.ButtonGo);
        }

        private IEnumerator LoadMusicAssets(RecoveredLocalMusicEntry music)
        {
            if (music == null || music.Lives == null)
                throw new ArgumentNullException(nameof(music));
            var hasCompleteCache =
                MusicAssetCache.TryGetValue(music.Id, out var cached) &&
                cached.MusicConfig != null &&
                music.Lives.All(live =>
                    cached.Charts.TryGetValue(live.Difficulty, out var chart) &&
                    chart != null);
            if (!hasCompleteCache)
            {
                var loaded = new CachedMusicAssets();
                foreach (var live in music.Lives)
                {
                    byte[] bytes = null;
                    yield return RecoveredStreamingAssetsRuntime.ReadBytes(
                        live.DebugNotationAssetPath, value => bytes = value);
                    loaded.Charts[live.Difficulty] = new TextAsset(
                        Encoding.UTF8.GetString(bytes))
                    {
                        name = $"Music{music.Id}{live.Difficulty}",
                    };
                }
                byte[] configBytes = null;
                yield return RecoveredStreamingAssetsRuntime.ReadBytes(
                    music.Lives[0].DebugMusicConfigAssetPath,
                    value => configBytes = value);
                loaded.MusicConfig = new TextAsset(
                    Encoding.UTF8.GetString(configBytes))
                {
                    name = $"Music{music.Id}Config",
                };
                cached = loaded;
                MusicAssetCache[music.Id] = loaded;
            }

            // Commit the complete music snapshot atomically. A superseded snap
            // coroutine may be stopped during an Android UnityWebRequest, but it
            // must never expose a half-filled chart dictionary to the start path.
            _charts.Clear();
            foreach (var pair in cached.Charts) _charts[pair.Key] = pair.Value;
            _musicConfigAsset = cached.MusicConfig;
            SetLoadedMusicSelection(music);
            EnsureJacketLoaded(music);
        }

        private IEnumerator BindPlayerRateJacket(RecoveredLocalMusicEntry music, Image jacket)
        {
            // PlayerRateDialogRateCellView.SetJacketImageAsync waits for the
            // provider's ready notification, then enables and binds the image.
            jacket.enabled = false;
            yield return LoadJacket(music);
            if (jacket == null) yield break; // Dialog/row may have been destroyed.
            JacketResourceProvider.TryGetValue(music.Id, out var sprite);
            jacket.sprite = sprite;
            jacket.overrideSprite = sprite;
            jacket.enabled = sprite != null;
        }

        private IEnumerator LoadJacket(RecoveredLocalMusicEntry music)
        {
            if (JacketResourceProvider.TryGetValue(music.Id, out var cached) &&
                cached != null)
            {
                BindJacketToListCells(music.Id, cached);
                yield break;
            }
            if (_loadingJackets.Contains(music.Id))
            {
                while (_loadingJackets.Contains(music.Id)) yield return null;
                if (JacketResourceProvider.TryGetValue(music.Id, out cached))
                    BindJacketToListCells(music.Id, cached);
                yield break;
            }
            if (string.IsNullOrEmpty(music.JacketAssetPath))
                throw new InvalidOperationException(
                    $"Music {music.Id} has no Jacket StreamingAssets path.");
            _loadingJackets.Add(music.Id);
            while (_jacketBundleLoadActive && !_stopJacketLoadingRequested)
                yield return null;
            if (_stopJacketLoadingRequested)
            {
                _loadingJackets.Remove(music.Id);
                yield break;
            }
            _jacketBundleLoadActive = true;
            AssetBundle bundle = null;
            try
            {
                byte[] jacketBytes = null;
                yield return RecoveredStreamingAssetsRuntime.ReadBytes(
                    music.JacketAssetPath, value => jacketBytes = value);
                if (_stopJacketLoadingRequested) yield break;
                var bundleRequest = AssetBundle.LoadFromMemoryAsync(jacketBytes);
                yield return bundleRequest;
                bundle = bundleRequest.assetBundle;
                _activeJacketBundle = bundle;
                if (_stopJacketLoadingRequested) yield break;
                if (bundle == null)
                    throw new InvalidOperationException(
                        $"Music {music.Id} Jacket bundle failed to load.");
                var spriteRequest = bundle.LoadAllAssetsAsync<Sprite>();
                yield return spriteRequest;
                if (_stopJacketLoadingRequested) yield break;
                var sprites = spriteRequest.allAssets.OfType<Sprite>().ToArray();
                if (sprites.Length != 1)
                {
                    bundle.Unload(true);
                    bundle = null;
                    _activeJacketBundle = null;
                    throw new InvalidOperationException(
                        $"Music {music.Id} Jacket bundle contains " +
                        $"{sprites.Length} sprites instead of one.");
                }
                JacketResourceProvider[music.Id] = sprites[0];
                bundle.Unload(false);
                bundle = null;
                _activeJacketBundle = null;
                BindJacketToListCells(music.Id, sprites[0]);
            }
            finally
            {
                if (_activeJacketBundle == bundle) _activeJacketBundle = null;
                if (bundle != null) bundle.Unload(true);
                _jacketBundleLoadActive = false;
                _loadingJackets.Remove(music.Id);
            }
        }

        private void EnsureJacketLoaded(RecoveredLocalMusicEntry music)
        {
            if (_stopJacketLoadingRequested || music == null ||
                _loadingJackets.Contains(music.Id))
                return;
            if (JacketResourceProvider.TryGetValue(music.Id, out var sprite) &&
                sprite != null)
            {
                BindJacketToListCells(music.Id, sprite);
                return;
            }
            StartCoroutine(LoadJacket(music));
        }

        private IEnumerator StopJacketLoading()
        {
            _stopJacketLoadingRequested = true;
            while (_jacketBundleLoadActive) yield return null;
            _loadingJackets.Clear();
        }

        private void SetLoadedMusicSelection(RecoveredLocalMusicEntry music)
        {
            var initial = _charts.ContainsKey(RecoveredMusicDifficulty.Stella)
                ? RecoveredMusicDifficulty.Stella
                : music.Lives[0].Difficulty;
            _selection = _catalog.Select(music.Id, initial);
        }

        private void BindJacketToListCells(long musicId, Sprite sprite)
        {
            if (_listCells.TryGetValue(musicId, out var cells))
            {
                foreach (var cell in cells)
                    ApplyJacketToCell(cell, sprite);
            }
            if (_selection?.Music != null &&
                _selection.Music.Id == musicId)
            {
                _jacketSprite = sprite;
                BindJacket(_view.transform.Find(
                    "TicketMacine/MusicInformationPanel/MusicJackets/" +
                    "MusicJacketBack"));
                BindJacket(_view.transform.Find(
                    "TicketMacine/MusicInformationPanel/MusicJackets/" +
                    "MusicJacketFront"));
            }
        }

        private static void ApplyJacketToCell(
            ListCellBinding cell,
            Sprite sprite)
        {
            if (cell?.gameObject == null) return;
            var root = cell.gameObject.transform;
            foreach (var path in new[]
                     {
                         "PartsBase/PartsMask/BackgroundMusicJacket",
                         "PartsBase/PartsMask/FocusMask/FocusTargetMusicJacket",
                     })
            {
                var image = root.Find(path)?.GetComponent<Image>();
                if (image == null) continue;
                image.enabled = sprite != null;
                image.sprite = sprite;
                image.overrideSprite = sprite;
            }
        }

        private void RequestPerformanceStart()
        {
            var settings = new RecoveredSettingsStore().LoadOrDefault();
            if (!settings.SystemSettings.IsPreLiveOptionConfirmation)
            {
                _performanceSettings = settings;
                _noteSpeed = (float)settings.GameSettings.NoteSpeed;
                _noteOffset = (float)settings.GameSettings.NoteOffsetValue;
                _mirror = settings.GameDetailSettings.IsActiveMirror;
                _splitRandom = settings.GameDetailSettings.IsActiveSplitRandom;
                _skipPreLiveConfirmation = true;
                BeginSelectedGame();
                return;
            }
            OpenNoteSpeedDialog();
        }

        private void OpenNoteSpeedDialog()
        {
            if (_noteSpeedDialog != null)
                return;
            _se.Play(RecoveredUiSeRuntime.Cue.ButtonGo);
            SetSpectrumVisibleForDialog(false);
            _noteSpeedDialog = CreateCommonDialog(
                "公演前オプション確認", new Vector2(1032f, 996f));
            _noteSpeedDialog.name = "RecoveredNoteSpeedDialog";
            var shellRect = _noteSpeedDialog.GetComponent<RectTransform>();
            if (shellRect == null)
                throw new InvalidOperationException(
                    "Original Dialog root RectTransform is missing.");
            // Retail leaves the note-speed preview visible to the right.
            shellRect.sizeDelta = new Vector2(1032f, 996f);
            var shellGroup = _noteSpeedDialog.GetComponent<CanvasGroup>();
            var body = FindDescendant(_noteSpeedDialog.transform, "Body");
            var bodyGroup = body != null ? body.GetComponent<CanvasGroup>() : null;
            var title = FindDescendant(_noteSpeedDialog.transform, "TitleText")
                ?.GetComponent<Text>();
            if (shellGroup == null || bodyGroup == null || title == null)
                throw new InvalidOperationException(
                    "Recovered common Dialog hierarchy is incomplete.");
            shellGroup.alpha = 1f;
            bodyGroup.alpha = 1f;
            title.text = "公演前オプション確認";

            SetPreLiveUnderlyingControlsVisible(false);
            _noteSpeedDialogBody = Instantiate(
                _noteSpeedDialogBodyPrefab, body, false);
            _noteSpeedDialogBody.name = "NoteSpeedSettingsDialogBody";
            BindNoteSpeedControls();
            CreateNoteSpeedPreview();
            ConfigureDialogButton(
                "FirstButtonParent", "FirstButton", "キャンセル",
                CloseNoteSpeedDialog, false);
            ConfigureDialogButton(
                "SecondButtonParent", "SecondButton", "OK",
                ConfirmGameStart, true);
            var third = FindDescendant(
                _noteSpeedDialog.transform, "ThirdButtonParent");
            if (third != null) third.gameObject.SetActive(false);
            _noteSpeedDialogButtons =
                LayoutCommonDialogButtons(_noteSpeedDialog);
            _noteSpeedDialog.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
        }

        private void CloseNoteSpeedDialog()
        {
            if (_noteSpeedDialog == null) return;
            _se.Play(RecoveredUiSeRuntime.Cue.ButtonBack);
            Destroy(_noteSpeedDialog);
            _noteSpeedDialog = null;
            if (_noteSpeedDialogButtons != null)
                Destroy(_noteSpeedDialogButtons);
            _noteSpeedDialogButtons = null;
            _noteSpeedDialogBody = null;
            _noteSpeedValueText = null;
            _noteOffsetValueText = null;
            _performanceSettings = null;
            DestroyNoteSpeedPreview();
            SetPreLiveUnderlyingControlsVisible(true);
            SetSpectrumVisibleForDialog(true);
        }

        private void ConfirmGameStart()
        {
            if (_gameStartPending) return;
            if (_performanceSettings == null)
                _performanceSettings =
                    new RecoveredSettingsStore().LoadOrDefault();
            _performanceSettings.GameSettings.NoteSpeed = _noteSpeed;
            _performanceSettings.GameSettings.NoteOffsetValue = _noteOffset;
            _performanceSettings.GameDetailSettings.IsActiveMirror = _mirror;
            _performanceSettings.GameDetailSettings.IsActiveSplitRandom =
                _splitRandom;
            _performanceSettings.SystemSettings.IsPreLiveOptionConfirmation =
                !_skipPreLiveConfirmation;
            new RecoveredSettingsStore().Save(_performanceSettings);
            BeginSelectedGame();
        }

        private void BeginSelectedGame()
        {
            if (_gameStartPending) return;
            _gameStartPending = true;
            StartCoroutine(BeginSelectedGameAfterAssetPreparation());
        }

        private IEnumerator BeginSelectedGameAfterAssetPreparation()
        {
            if (_selection == null ||
                !_charts.TryGetValue(_selection.Live.Difficulty, out var chart))
            {
                _gameStartPending = false;
                yield break;
            }
            if (_noteSpeedDialog != null)
            {
                yield return HidePreLiveDialog();
                yield return HideNoteSpeedPreview();
            }
            yield return RecoveredSplitLaneAssetRuntime
                .PrepareStreamingAssets(chart.text);
            try
            {
                RecoveredSplitLaneAssetRuntime
                    .ThrowIfStreamingAssetPreparationFailed();
            }
            catch
            {
                _gameStartPending = false;
                throw;
            }
            yield return StopJacketLoading();
            _preview?.Stop();
            _se.Play(RecoveredUiSeRuntime.Cue.ButtonGo);
            RecoveredLocalMusicSelectionSession.Set(
                _selection,
                chart,
                _musicConfigAsset,
                _jacketSprite,
                _allMusics);
            if (_noteSpeedDialog != null)
            {
                Destroy(_noteSpeedDialog);
                _noteSpeedDialog = null;
                if (_noteSpeedDialogButtons != null)
                    Destroy(_noteSpeedDialogButtons);
                _noteSpeedDialogButtons = null;
                _noteSpeedDialogBody = null;
                _noteSpeedValueText = null;
                _noteOffsetValueText = null;
            }
            DestroyNoteSpeedPreview();
            if (!RecoveredCurtainTransitionRuntime.Begin(
                    _curtainSkeletonData,
                    _curtainGraphicMaterial,
                    _gameSceneName,
                    true))
            {
                throw new InvalidOperationException(
                    "Curtain transition could not start.");
            }
        }

        private IEnumerator HidePreLiveDialog()
        {
            // DialogMonoBehaviour.HideAsync (2.31.2 VA 0x753EF90): fade the
            // body over 0.2s, trigger FadeOut, await Animator completion.
            // The original Dialog_fadeOut_anim scales 1 -> 0.8 and fades the
            // root over 10/60s. It contains no horizontal translation.
            var dialog = _noteSpeedDialog;
            var animator = dialog.GetComponent<Animator>();
            var group = dialog.GetComponent<CanvasGroup>();
            var body = FindDescendant(dialog.transform, "Body")?.GetComponent<CanvasGroup>();
            if (animator == null || group == null || body == null ||
                animator.runtimeAnimatorController == null)
                throw new InvalidOperationException("Pre-live Dialog exit components are missing.");
            group.interactable = false;
            body.DOFade(0f, 0.2f).SetLink(dialog).Play();
            animator.SetTrigger("FadeOut");
            var deadline = Time.realtimeSinceStartup + 2f;
            do
            {
                yield return null;
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.IsName("Dialog_fadeOut_anim") && state.normalizedTime >= 1f)
                    yield break;
            } while (Time.realtimeSinceStartup < deadline);
            throw new InvalidOperationException("Pre-live Dialog FadeOut did not complete.");
        }

        private IEnumerator HideNoteSpeedPreview()
        {
            if (_noteSpeedPreviewView == null) yield break;
            // PartyReadyGameSettingUseCase -> GameSimulationPresenter.HideAsync:
            // stop ticking/rendering, then IView.HideAsync -> HideViewFromLeftAsync.
            if (_noteSpeedPreview != null)
            {
                var preview = _noteSpeedPreview.GetComponent<RecoveredGameSimulationPreview>();
                if (preview != null) preview.enabled = false;
                foreach (var camera in _noteSpeedPreview.GetComponentsInChildren<Camera>(true))
                    camera.enabled = false;
            }
            var view = _noteSpeedPreviewView;
            // This host owns a root overlay Canvas (whose rect Unity drives).
            // Translate its sole visible child to reproduce the retail view move.
            var rect = (RectTransform)view.transform.Find("RawImage");
            var group = view.GetComponent<CanvasGroup>();
            rect.anchoredPosition = Vector2.zero;
            group.alpha = 1f;
            var sequence = DOTween.Sequence().SetLink(view);
            sequence.Append(rect.DOAnchorPosX(-500f, 0.3f).SetEase(Ease.OutCubic));
            sequence.Join(group.DOFade(0f, 0.2f).SetEase(Ease.Linear));
            sequence.OnComplete(() => view.SetActive(false));
            yield return sequence.WaitForCompletion();
        }

        private void CreateNoteSpeedPreview()
        {
            DestroyNoteSpeedPreview();
            if (_performanceSettings == null)
                throw new InvalidOperationException(
                    "Performance settings must be loaded before GameSimulation.");

            _noteSpeedPreview = new GameObject("GameSimulation");
            var cameraObject = Instantiate(
                _gameSimulationCameraPrefab,
                _noteSpeedPreview.transform,
                false);
            cameraObject.name = "GameSimulationCamera";
            var simulationCamera = cameraObject.GetComponent<Camera>();
            if (simulationCamera == null)
                throw new InvalidOperationException(
                    "Original GameSimulationCamera prefab has no Camera.");
            _noteSpeedSimulation =
                _noteSpeedPreview.AddComponent<RecoveredGameSimulationPreview>();
            _noteSpeedSimulation.Configure(
                simulationCamera,
                _gameSimulationLaneGroupPrefab,
                _gameSimulationPreviewUiPrefab,
                _gameSimulationNotePrefab,
                _noteSpeed,
                _noteOffset,
                _performanceSettings.GameDetailSettings.NoteStartOffset,
                _performanceSettings.GameDetailSettings.TimingEffectOffset,
                _performanceSettings.GameDetailSettings.TimingEffectScaleType,
                _performanceSettings.GameSettings.IsActiveSenseDisplay,
                _performanceSettings.GameDetailSettings.LaneWidth,
                _performanceSettings.GameSettings.LaneAlphaValue,
                _performanceSettings.GameDetailSettings.NoteHeight);

            // GameSimulationView.prefab: full-screen Canvas order 61, RawImage
            // right anchored at width 290, UV (0.412,0,0.16,1). The local
            // dialog uses a higher recovered sort band, preserving the retail
            // relative order while retaining the authored view values.
            _noteSpeedPreviewView = new GameObject(
                "GameSimulationView",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup));
            var canvas = _noteSpeedPreviewView.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 109;
            var rootRect = (RectTransform)_noteSpeedPreviewView.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var imageObject = new GameObject(
                "RawImage",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            imageObject.transform.SetParent(
                _noteSpeedPreviewView.transform, false);
            var imageRect = (RectTransform)imageObject.transform;
            imageRect.anchorMin = new Vector2(1f, 0f);
            imageRect.anchorMax = Vector2.one;
            imageRect.pivot = new Vector2(1f, 0.5f);
            imageRect.anchoredPosition = Vector2.zero;
            imageRect.sizeDelta = new Vector2(290f, 0f);
            var rawImage = imageObject.GetComponent<RawImage>();
            rawImage.texture = _gameSimulationRenderTexture;
            rawImage.uvRect = new Rect(0.412f, 0f, 0.16f, 1f);
            rawImage.raycastTarget = false;
        }

        private void DestroyNoteSpeedPreview()
        {
            if (_noteSpeedPreview != null)
            {
                _noteSpeedPreview.SetActive(false);
                Destroy(_noteSpeedPreview);
            }
            if (_noteSpeedPreviewView != null)
            {
                _noteSpeedPreviewView.SetActive(false);
                Destroy(_noteSpeedPreviewView);
            }
            _noteSpeedPreview = null;
            _noteSpeedPreviewView = null;
            _noteSpeedSimulation = null;
        }

        private void SetPreLiveUnderlyingControlsVisible(bool visible)
        {
            var ok = _view.transform.Find("OkButton");
            if (ok != null) ok.gameObject.SetActive(visible);
            var difficulty = _view.transform.Find(
                "DefaultLivePanels/DifficultyPanel");
            if (difficulty != null) difficulty.gameObject.SetActive(visible);
        }

        private void ConfigureDialogButton(
            string parentName,
            string buttonName,
            string label,
            UnityEngine.Events.UnityAction action,
            bool positive)
        {
            ConfigureCommonDialogButton(
                _noteSpeedDialog, parentName, buttonName,
                label, action, positive);
        }

        private GameObject CreateCommonDialog(string titleValue, Vector2 size)
        {
            var shell = Instantiate(
                _dialogPrefab, _view.transform.parent, false);
            var rect = shell.GetComponent<RectTransform>();
            if (rect == null)
                throw new InvalidOperationException(
                    "Original Dialog root RectTransform is missing.");
            rect.sizeDelta = size;
            var overlay = new GameObject(
                "RecoveredDialogOverlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            overlay.transform.SetParent(shell.transform, false);
            overlay.transform.SetSiblingIndex(0);
            var overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
            overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
            overlayRect.pivot = new Vector2(0.5f, 0.5f);
            overlayRect.anchoredPosition = Vector2.zero;
            overlayRect.sizeDelta = new Vector2(2200f, 1400f);
            var overlayImage = overlay.GetComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.62f);
            overlayImage.raycastTarget = true;
            var shellGroup = shell.GetComponent<CanvasGroup>();
            var body = FindDescendant(shell.transform, "Body");
            var bodyGroup = body != null ? body.GetComponent<CanvasGroup>() : null;
            var bodyBackground = FindDescendant(
                shell.transform, "BodyBackground")?.GetComponent<Image>();
            var title = FindDescendant(shell.transform, "TitleText")
                ?.GetComponent<Text>();
            if (shellGroup == null || bodyGroup == null || title == null)
                throw new InvalidOperationException(
                    "Recovered common Dialog hierarchy is incomplete.");
            shellGroup.alpha = 1f;
            bodyGroup.alpha = 1f;
            if (bodyBackground != null)
            {
                var color = bodyBackground.color;
                color.a = 1f;
                bodyBackground.color = color;
            }
            title.text = titleValue;
            return shell;
        }

        private void ConfigureCommonDialogButton(
            GameObject dialog,
            string parentName,
            string buttonName,
            string label,
            UnityEngine.Events.UnityAction action,
            bool positive)
        {
            var parent = FindDescendant(dialog.transform, parentName);
            var buttonRoot = parent != null
                ? FindDescendant(parent, buttonName)
                : null;
            var button = buttonRoot != null
                ? buttonRoot.GetComponent<Button>()
                : null;
            var text = buttonRoot != null
                ? buttonRoot.GetComponentInChildren<Text>(true)
                : null;
            var image = buttonRoot != null
                ? buttonRoot.GetComponent<Image>()
                : null;
            if (parent == null || button == null || text == null || image == null)
                throw new InvalidOperationException(
                    $"Recovered common Dialog button is incomplete: {buttonName}");
            parent.gameObject.SetActive(true);
            text.text = label;
            image.sprite = positive
                ? _dialogPositiveButtonSprite
                : _dialogNormalButtonSprite;
            image.enabled = true;
            image.color = Color.white;
            if (positive) text.color = Color.white;
            foreach (var animator in buttonRoot.GetComponents<Animator>())
                animator.enabled = false;
            var group = buttonRoot.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            var visibleSurface = CreateDialogButtonVisual(
                buttonRoot, text, label,
                positive ? _dialogPositiveButtonSprite
                    : _dialogNormalButtonSprite,
                positive);
            image.enabled = false;
            text.enabled = false;
            button.targetGraphic = visibleSurface;
            foreach (var buttonText in
                     buttonRoot.GetComponentsInChildren<Text>(true))
                buttonText.text = label;
        }

        private static Image CreateDialogButtonVisual(
            Transform buttonRoot,
            Text sourceText,
            string label,
            Sprite sprite,
            bool positive)
        {
            var old = buttonRoot.Find("RecoveredVisibleSurface");
            if (old != null) Destroy(old.gameObject);
            var surface = new GameObject(
                "RecoveredVisibleSurface",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            surface.transform.SetParent(buttonRoot, false);
            var surfaceRect = surface.GetComponent<RectTransform>();
            surfaceRect.anchorMin = Vector2.zero;
            surfaceRect.anchorMax = Vector2.one;
            surfaceRect.offsetMin = Vector2.zero;
            surfaceRect.offsetMax = Vector2.zero;
            var surfaceImage = surface.GetComponent<Image>();
            surfaceImage.sprite = sprite;
            surfaceImage.type = Image.Type.Sliced;
            surfaceImage.color = Color.white;
            surfaceImage.raycastTarget = true;
            var labelObject = new GameObject(
                "Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            labelObject.transform.SetParent(surface.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var labelText = labelObject.GetComponent<Text>();
            labelText.text = label;
            labelText.font = sourceText.font;
            labelText.fontSize = sourceText.fontSize;
            labelText.fontStyle = sourceText.fontStyle;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = positive
                ? Color.white
                : new Color32(82, 84, 94, 255);
            labelText.raycastTarget = false;
            return surfaceImage;
        }

        private GameObject LayoutSingleCommonDialogButton(
            GameObject dialog,
            string label,
            bool positive)
        {
            var buttons = FindDescendant(dialog.transform, "Buttons")
                ?.GetComponent<RectTransform>();
            var firstParent = FindDescendant(
                dialog.transform, "FirstButtonParent") as RectTransform;
            var original = FindDescendant(dialog.transform, "FirstButton")
                ?.GetComponent<Button>();
            if (buttons == null || firstParent == null) return null;
            var layer = new GameObject(
                dialog.name + "Buttons", typeof(RectTransform));
            layer.transform.SetParent(_view.transform.parent, false);
            var layerRect = (RectTransform)layer.transform;
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = Vector2.zero;
            layerRect.offsetMax = Vector2.zero;
            buttons.SetParent(layer.transform, false);
            var hidden = layer.AddComponent<CanvasGroup>();
            hidden.alpha = 0f;
            hidden.interactable = false;
            hidden.blocksRaycasts = false;
            var dialogRect = dialog.transform as RectTransform;
            var footerButtonY = dialogRect != null
                ? -dialogRect.rect.height * 0.5f + 66f
                : -432f;
            CreateCommonFooterButton(
                dialog.transform,
                "CloseButton",
                label,
                new Vector2(0f, footerButtonY),
                positive
                    ? _dialogPositiveButtonSprite
                    : _dialogNormalButtonSprite,
                original,
                positive);
            return layer;
        }

        private GameObject LayoutCommonDialogButtons(GameObject dialog)
        {
            var buttons = FindDescendant(dialog.transform, "Buttons")
                ?.GetComponent<RectTransform>();
            if (buttons == null) return null;
            var names = new[] { "FirstButtonParent", "SecondButtonParent" };
            var buttonParents = names.Select(name =>
                    FindDescendant(dialog.transform, name)
                        ?.GetComponent<RectTransform>())
                .ToArray();
            var layer = new GameObject(
                dialog.name + "Buttons",
                typeof(RectTransform));
            layer.transform.SetParent(_view.transform.parent, false);
            var layerRect = layer.GetComponent<RectTransform>();
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = Vector2.zero;
            layerRect.offsetMax = Vector2.zero;
            buttons.SetParent(layer.transform, false);
            buttons.name = "Buttons";
            layer.transform.SetAsLastSibling();
            var layout = buttons.GetComponent<HorizontalLayoutGroup>();
            if (layout != null) layout.enabled = false;
            buttons.anchorMin = new Vector2(0.5f, 0f);
            buttons.anchorMax = new Vector2(0.5f, 0f);
            buttons.pivot = new Vector2(0.5f, 0f);
            // The recovered host Canvas keeps the retail 1920-wide safe-area
            // origin at its left edge even though this overlay RectTransform
            // is center-anchored. Compensate that host-space offset so the
            // original common buttons land inside the visible dialog footer.
            buttons.anchoredPosition = new Vector2(960f, 180f);
            buttons.sizeDelta = new Vector2(668f, 108f);
            for (var index = 0; index < names.Length; index++)
            {
                var rect = buttonParents[index];
                if (rect == null) continue;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(
                    index == 0 ? -174f : 174f, 0f);
                rect.sizeDelta = new Vector2(320f, 108f);
                var button = rect.childCount > 0
                    ? rect.GetChild(0).GetComponent<RectTransform>()
                    : null;
                if (button == null) continue;
                button.anchorMin = Vector2.zero;
                button.anchorMax = Vector2.one;
                button.offsetMin = Vector2.zero;
                button.offsetMax = Vector2.zero;
                button.pivot = new Vector2(0.5f, 0.5f);
            }
            var originalCancel = FindDescendant(layer.transform, "FirstButton")
                ?.GetComponent<Button>();
            var originalConfirm = FindDescendant(layer.transform, "SecondButton")
                ?.GetComponent<Button>();
            var cancelLabel = originalCancel != null
                ? originalCancel.GetComponentInChildren<Text>(true)?.text
                : null;
            var confirmLabel = originalConfirm != null
                ? originalConfirm.GetComponentInChildren<Text>(true)?.text
                : null;
            var dialogRect = dialog.transform as RectTransform;
            // Retail Dialog/Buttons stretches across the dialog at the bottom:
            // bottom pivot, anchoredPosition.y=12, height=108. Its button
            // centers are therefore 12 + 54 pixels above the dialog bottom.
            var footerButtonY = dialogRect != null
                ? -dialogRect.rect.height * 0.5f + 66f
                : -432f;
            CreateCommonFooterButton(
                dialog.transform,
                "CancelButton",
                string.IsNullOrEmpty(cancelLabel) ? "キャンセル" : cancelLabel,
                new Vector2(-174f, footerButtonY),
                _dialogNormalButtonSprite,
                originalCancel,
                false);
            CreateCommonFooterButton(
                dialog.transform,
                "ConfirmButton",
                string.IsNullOrEmpty(confirmLabel) ? "OK" : confirmLabel,
                new Vector2(174f, footerButtonY),
                _dialogPositiveButtonSprite,
                originalConfirm,
                true);
            var hiddenGroup = layer.AddComponent<CanvasGroup>();
            hiddenGroup.alpha = 0f;
            hiddenGroup.interactable = false;
            hiddenGroup.blocksRaycasts = false;
            return layer;
        }

        private void CreateCommonFooterButton(
            Transform parent,
            string name,
            string label,
            Vector2 position,
            Sprite sprite,
            Button original,
            bool positive)
        {
            var rect = CreateRect(
                parent, name, position, new Vector2(320f, 108f));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (original != null)
                button.onClick.AddListener(original.onClick.Invoke);
            var text = CreateText(
                rect, "Text", label, new Vector2(0f, 1f),
                new Vector2(310f, 106f), 40);
            text.fontStyle = FontStyle.Normal;
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.anchoredPosition = new Vector2(0f, 1f);
            textRect.sizeDelta = new Vector2(-10f, -2f);
            text.alignment = TextAnchor.MiddleCenter;
            text.color = positive
                ? Color.white
                : new Color32(82, 84, 94, 255);
        }

        private void SetSpectrumVisibleForDialog(bool visible)
        {
            var spectrum = FindDescendant(
                _view.transform, "UIParticleSpectrumViewer");
            if (spectrum == null) return;
            if (!visible)
            {
                _spectrumHiddenForDialog = spectrum.gameObject.activeSelf;
                spectrum.gameObject.SetActive(false);
            }
            else if (_spectrumHiddenForDialog)
            {
                spectrum.gameObject.SetActive(true);
                _spectrumHiddenForDialog = false;
            }
        }

        private Text FindText(string relativePath)
        {
            return _view.transform.Find(relativePath)?.GetComponent<Text>();
        }

        private void SetText(string relativePath, string value)
        {
            var text = FindText(relativePath);
            if (text != null) text.text = value ?? string.Empty;
        }

        private static void SetCellText(Transform root, string path, string value)
        {
            var text = root.Find(path)?.GetComponent<Text>();
            if (text != null) text.text = value ?? string.Empty;
        }

        private void BindJacket(Transform target)
        {
            var image = target != null ? target.GetComponent<Image>() : null;
            if (image == null) return;
            image.enabled = _jacketSprite != null;
            image.sprite = _jacketSprite;
            image.overrideSprite = _jacketSprite;
            if (_jacketSprite == null) return;
            // The retail prefab stretches the square Jacket into its authored
            // 568x616 rect. preserveAspect would leave 24-unit gaps above and
            // below, placing the spectrum outside the visible artwork.
            image.preserveAspect = false;
            image.color = Color.white;
        }

        private static bool HasLive(
            RecoveredLocalMusicEntry music,
            RecoveredMusicDifficulty difficulty)
        {
            return TryGetLive(music, difficulty, out _);
        }

        private static bool TryGetLive(
            RecoveredLocalMusicEntry music,
            RecoveredMusicDifficulty difficulty,
            out RecoveredLocalLiveEntry result)
        {
            if (music.Lives != null)
            {
                foreach (var live in music.Lives)
                {
                    if (live != null && live.Difficulty == difficulty)
                    {
                        result = live;
                        return true;
                    }
                }
            }
            result = null;
            return false;
        }

        private static readonly string[] OlivierLevelNames =
        {
            string.Empty, "I", "II", "III", "IV", "V",
            "VI", "VII", "VIII", "IX", "X",
        };

        public static string FormatMusicLevel(int level)
        {
            return level >= 101 && level <= 110
                ? OlivierLevelNames[level - 100]
                : level.ToString();
        }

        private static string FormatLevel(
            RecoveredMusicDifficulty difficulty,
            int level)
        {
            return FormatMusicLevel(level);
        }

        private void OnDestroy()
        {
            if (_activeJacketBundle != null)
            {
                _activeJacketBundle.Unload(true);
                _activeJacketBundle = null;
            }
        }
    }

    public sealed class RecoveredLocalGameFlowRouter : MonoBehaviour
    {
        private static RecoveredLocalGameFlowRouter _instance;
        private RecoveredGameRuntime _game;
        private bool _returning;

        public static RecoveredLocalGameFlowRouter EnsureExists()
        {
            if (_instance != null) return _instance;
            var host = new GameObject("RecoveredLocalGameFlowRouter");
            return host.AddComponent<RecoveredLocalGameFlowRouter>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            BindGameRuntime();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _returning = false;
            BindGameRuntime();
        }

        private void BindGameRuntime()
        {
            if (_game != null) _game.ExitRouteRequested -= OnExitRouteRequested;
            _game = FindObjectOfType<RecoveredGameRuntime>();
            if (_game != null) _game.ExitRouteRequested += OnExitRouteRequested;
        }

        private void OnExitRouteRequested(RecoveredGameExitRoute route)
        {
            if (route == RecoveredGameExitRoute.MusicSelection)
                StartReturn(_game != null && _game.IsResultShown);
        }

        private void StartReturn(bool positiveAction = false)
        {
            if (_returning) return;
            _returning = true;
            StartCoroutine(ReturnAfterSe(positiveAction));
        }

        private IEnumerator ReturnAfterSe(bool positiveAction)
        {
            if (RecoveredUiSeRuntime.Instance != null)
                RecoveredUiSeRuntime.Instance.Play(
                    positiveAction
                        ? RecoveredUiSeRuntime.Cue.ButtonGo
                        : RecoveredUiSeRuntime.Cue.ButtonBack);
            yield return new WaitForSecondsRealtime(0.12f);
            if (!RecoveredCurtainTransitionRuntime.ReturnToRetainedScene())
                SceneManager.LoadScene("LocalMusicSelection");
        }

        private void OnDestroy()
        {
            if (_game != null) _game.ExitRouteRequested -= OnExitRouteRequested;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_instance == this) _instance = null;
        }
    }
}
