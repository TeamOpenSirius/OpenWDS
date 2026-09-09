using System;
using System.Collections.Generic;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Runtime bridge for the original TimingEffect and Combo prefabs. Selection,
    /// scale, position and digit style come from original ARM64/GameConfig data.
    /// </summary>
    public sealed class RecoveredGameHudRuntime : MonoBehaviour
    {
        [SerializeField] private GameObject _timingEffectPrefab;
        [SerializeField] private GameObject _timingAssistEffectPrefab;
        [SerializeField] private GameObject _comboPrefab;
        [SerializeField] private GameObject _lifePrefab;
        [SerializeField] private GameObject _principalPrefab;
        [SerializeField] private GameObject _scorePrefab;
        [SerializeField] private GameObject _achievementRatePrefab;
        [SerializeField] private GameObject _senseLightPrefab;
        [SerializeField] private Sprite[] _senseLightSprites;
        [SerializeField] private GameObject _additionalScoreCutInPrefab;
        [SerializeField] private Sprite[] _senseCutInSprites;
        [SerializeField] private Sprite[] _timingSprites;
        [SerializeField] private Sprite[] _timingAssistSprites;
        [SerializeField] private Sprite[] _normalComboCounts;
        [SerializeField] private Sprite[] _fullComboCounts;
        [SerializeField] private Sprite[] _allPerfectComboCounts;
        [SerializeField] private Sprite[] _comboLabels;
        [SerializeField] private Sprite[] _noComboRateCounts;
        [SerializeField] private Sprite[] _ratePoints;
        [SerializeField] private Sprite[] _ratePercents;
        [SerializeField] private Camera _gameplayCamera;
        [SerializeField] private Transform _timingParent;
        [SerializeField] private float _timingScale = 1f;
        [SerializeField] private float _timingPositionY = 0.4f;

        private readonly List<Sirius.Game.TimingEffect> _timingPool =
            new List<Sirius.Game.TimingEffect>();
        private readonly List<Sirius.Game.TimingAssistEffect> _timingAssistPool =
            new List<Sirius.Game.TimingAssistEffect>();
        private GameObject _canvasObject;
        private CanvasGroup _introductionCanvasGroup;
        private Sirius.Game.UI.ComboPanel _comboPanel;
        private Sirius.Game.UI.LifeGauge _lifeGauge;
        private Sirius.Game.UI.PrincipalGauge _principalGauge;
        private Sirius.Game.UI.ScorePanel _scorePanel;
        private Sirius.Game.UI.AchievementRatePanel _achievementRatePanel;
        private GameObject _senseLightPanel;
        private Sirius.Game.UI.AdditionalScoreCutInPanel _additionalScoreCutInPanel;
        private Transform[] _senseLightContents;
        private int _totalSenseLightCount;
        private RecoveredLifeRuntime _life;
        private RecoveredPrincipalRuntime _principal;
        private RecoveredSoloScoreContext _scoreContext;
        private RecoveredSoloScoreRuntime _score;
        private RecoveredSenseScoreRuntime _senseScore;
        private RecoveredStarActScoreRuntime _starActScore;
        private RecoveredPlayerUnitFixture _currentPlayerUnit;
        private Dictionary<long, int> _principalAddingMap;
        private int _principalOrder;
        private int _achievementRateSettingType;
        private int _timingAssistSettingType;
        private bool _isActiveComboEffect = true;
        private bool _isPerfectContinuous = true;
        private bool _isActiveSenseDisplay = true;
        private bool _shouldShowPerfectStar = true;
        private bool _initialized;
        private bool _visible;

        public Sirius.Game.UI.ComboPanel ComboPanel => _comboPanel;
        public Sirius.Game.UI.LifeGauge LifeGauge => _lifeGauge;
        public RecoveredLifeRuntime Life => _life;
        public Sirius.Game.UI.PrincipalGauge PrincipalGauge => _principalGauge;
        public RecoveredPrincipalRuntime Principal => _principal;
        public Sirius.Game.UI.ScorePanel ScorePanel => _scorePanel;
        public RecoveredSoloScoreRuntime Score => _score;
        public RecoveredSenseScoreRuntime SenseScore => _senseScore;
        public RecoveredStarActScoreRuntime StarActScore => _starActScore;
        public long TotalScore => (_score?.Count ?? 0L) +
                                  (_senseScore?.Count ?? 0L) +
                                  (_starActScore?.Count ?? 0L);
        public Sirius.Game.UI.AchievementRatePanel AchievementRatePanel =>
            _achievementRatePanel;
        public GameObject SenseLightPanel => _senseLightPanel;
        public Sirius.Game.UI.AdditionalScoreCutInPanel AdditionalScoreCutInPanel =>
            _additionalScoreCutInPanel;
        public Transform CanvasTransform =>
            _canvasObject != null ? _canvasObject.transform : null;
        public int TimingPoolCount => _timingPool.Count;
        public int TimingAssistPoolCount => _timingAssistPool.Count;
        public bool IsVisible => _initialized && _visible;
        public float IntroductionAlpha =>
            _introductionCanvasGroup != null ? _introductionCanvasGroup.alpha : 0f;
        public Sirius.Game.TimingEffect LastTimingEffect { get; private set; }
        public Sirius.Game.TimingAssistEffect LastTimingAssistEffect
        {
            get;
            private set;
        }
        public Transform TimingParent => _timingParent;
        public float TimingPositionY => _timingPositionY;
        public float TimingScale => _timingScale;

        public void Configure(
            Camera gameplayCamera,
            Transform timingParent,
            GameObject timingEffectPrefab,
            GameObject timingAssistEffectPrefab,
            GameObject comboPrefab,
            GameObject lifePrefab,
            GameObject principalPrefab,
            GameObject scorePrefab,
            GameObject achievementRatePrefab,
            GameObject senseLightPrefab,
            Sprite[] senseLightSprites,
            GameObject additionalScoreCutInPrefab,
            Sprite[] senseCutInSprites,
            Sprite[] timingSprites,
            Sprite[] timingAssistSprites,
            Sprite[] normalComboCounts,
            Sprite[] fullComboCounts,
            Sprite[] allPerfectComboCounts,
            Sprite[] comboLabels,
            Sprite[] noComboRateCounts,
            Sprite[] ratePoints,
            Sprite[] ratePercents,
            int achievementRateSettingType,
            int timingAssistSettingType,
            bool isActiveComboEffect,
            bool isPerfectContinuous,
            int timingEffectOffset = 60,
            int timingEffectScaleType = 1)
        {
            _gameplayCamera = gameplayCamera;
            _timingParent = timingParent;
            _timingEffectPrefab = timingEffectPrefab;
            _timingAssistEffectPrefab = timingAssistEffectPrefab;
            _comboPrefab = comboPrefab;
            _lifePrefab = lifePrefab;
            _principalPrefab = principalPrefab;
            _scorePrefab = scorePrefab;
            _achievementRatePrefab = achievementRatePrefab;
            _senseLightPrefab = senseLightPrefab;
            _senseLightSprites = senseLightSprites;
            _additionalScoreCutInPrefab = additionalScoreCutInPrefab;
            _senseCutInSprites = senseCutInSprites;
            _timingSprites = timingSprites;
            _timingAssistSprites = timingAssistSprites;
            _normalComboCounts = normalComboCounts;
            _fullComboCounts = fullComboCounts;
            _allPerfectComboCounts = allPerfectComboCounts;
            _comboLabels = comboLabels;
            _noComboRateCounts = noComboRateCounts;
            _ratePoints = ratePoints;
            _ratePercents = ratePercents;
            _achievementRateSettingType = achievementRateSettingType;
            _timingAssistSettingType = timingAssistSettingType;
            _isActiveComboEffect = isActiveComboEffect;
            _isPerfectContinuous = isPerfectContinuous;
            _timingPositionY = CalculatePositionY(timingEffectOffset);
            _timingScale = GetTimingEffectScale(timingEffectScaleType);
        }

        public void Initialize()
        {
            if (_initialized) return;
            if (_timingParent == null || _timingEffectPrefab == null ||
                _timingAssistEffectPrefab == null ||
                _comboPrefab == null || _lifePrefab == null)
                throw new InvalidOperationException("Game HUD prefabs and parent are required.");
            if (_gameplayCamera == null)
                throw new InvalidOperationException(
                    "The gameplay Camera is required for HUD screen projection.");
            if (_principalPrefab == null || _scorePrefab == null ||
                _achievementRatePrefab == null || _senseLightPrefab == null)
                throw new InvalidOperationException("Game HUD prefabs and parent are required.");
            if (_senseLightSprites == null || _senseLightSprites.Length != 5)
                throw new InvalidOperationException(
                    "SenseLightPanel requires the original five light sprites.");
            ValidateSprites();
            ApplyTimingParentPosition();
            CreateTimingEffect(); // Original TimingEffectSpawner.PreloadCount = 1.
            CreateTimingAssistEffect(); // Same preload count for assist.

            _canvasObject = new GameObject(
                "RecoveredGameHudCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            _introductionCanvasGroup = _canvasObject.AddComponent<CanvasGroup>();
            _canvasObject.transform.SetParent(transform, false);
            var canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var comboObject = Instantiate(_comboPrefab, _canvasObject.transform, false);
            comboObject.name = "Combo";
            _comboPanel = comboObject.GetComponent<Sirius.Game.UI.ComboPanel>();
            _comboPanel.Initialize(
                new[] { _normalComboCounts, _fullComboCounts, _allPerfectComboCounts },
                _comboLabels);
            var lifeObject = Instantiate(_lifePrefab, _canvasObject.transform, false);
            lifeObject.name = "Life";
            _lifeGauge = lifeObject.GetComponent<Sirius.Game.UI.LifeGauge>();
            if (_lifeGauge == null)
                throw new InvalidOperationException("Life prefab has no LifeGauge component.");
            _life = new RecoveredLifeRuntime();
            _lifeGauge.SetLifeValue(_life.Value, _life.MaxValue, _life.GuardCount);
            var principalObject = Instantiate(
                _principalPrefab, _canvasObject.transform, false);
            principalObject.name = "PrincipalGauge";
            _principalGauge = principalObject.GetComponent<
                Sirius.Game.UI.PrincipalGauge>();
            if (_principalGauge == null)
                throw new InvalidOperationException(
                    "Principal prefab has no PrincipalGauge component.");
            _principal = new RecoveredPrincipalRuntime();
            _principal.DefaultValueChanged += OnDefaultPrincipalChanged;
            // Principal.Initialize reads the real LiveUnitWithOrder array. The
            // public offline slice has no player unit, so do not invent a limit.
            _principalGauge.Hide();

            _senseLightPanel = Instantiate(
                _senseLightPrefab, _canvasObject.transform, false);
            _senseLightPanel.name = "SenseLightPanel";
            var contents = FindDescendant(
                _senseLightPanel.transform, "SenseLightContents");
            if (contents == null || contents.childCount != 10)
                throw new InvalidOperationException(
                    "Original SenseLightPanel must contain ten light slots.");
            _senseLightContents = new Transform[contents.childCount];
            for (var index = 0; index < contents.childCount; index++)
                _senseLightContents[index] = contents.GetChild(index);
            RefreshSenseLights(null);

            if (_additionalScoreCutInPrefab == null)
                throw new InvalidOperationException(
                    "AdditionalScoreCutInPanel prefab is required.");
            var cutInObject = Instantiate(
                _additionalScoreCutInPrefab, _canvasObject.transform, false);
            cutInObject.name = "AdditionalScoreCutInPanel";
            _additionalScoreCutInPanel = cutInObject.GetComponent<
                Sirius.Game.UI.AdditionalScoreCutInPanel>();
            if (_additionalScoreCutInPanel == null)
                throw new InvalidOperationException(
                    "AdditionalScoreCutInPanel has no recovered component.");
            _additionalScoreCutInPanel.Configure(
                _senseCutInSprites,
                _isActiveSenseDisplay,
                _gameplayCamera,
                _timingParent);

            var achievementObject = Instantiate(
                _achievementRatePrefab, _canvasObject.transform, false);
            achievementObject.name = "AchievementRate";
            _achievementRatePanel = achievementObject.GetComponent<
                Sirius.Game.UI.AchievementRatePanel>();
            if (_achievementRatePanel == null)
                throw new InvalidOperationException(
                    "AchievementRate prefab has no recovered panel component.");
            _achievementRatePanel.Initialize(
                new[]
                {
                    _normalComboCounts,
                    _fullComboCounts,
                    _allPerfectComboCounts,
                    _noComboRateCounts,
                },
                _ratePoints,
                _ratePercents,
                (Sirius.Game.UI.RecoveredAchievementRateSettingType)
                    _achievementRateSettingType,
                false,
                false,
                false,
                _isPerfectContinuous);

            var scoreObject = Instantiate(_scorePrefab, _canvasObject.transform, false);
            scoreObject.name = "Score";
            _scorePanel = scoreObject.GetComponent<Sirius.Game.UI.ScorePanel>();
            if (_scorePanel == null)
                throw new InvalidOperationException("Score prefab has no recovered panel component.");
            // AutoTouch is only the offline test/input driver. The recovered
            // scene itself is an ordinary solo live, so showing the original
            // game's AutoPlay replacement over the live score is incorrect.
            // Score.Initialize requires a real LiveUnitWithOrder-derived context.
            // Keep the view absent until that explicit boundary is supplied.
            _scorePanel.Hide();
            _initialized = true;
            _visible = true;
        }

        public void ApplyRecoveredSettings(
            RecoveredGameSettings.Basic basic,
            RecoveredGameSettings.Detail settings)
        {
            if (basic == null) throw new ArgumentNullException(nameof(basic));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (_initialized)
                throw new InvalidOperationException(
                    "HUD settings must be applied before initialization.");
            _achievementRateSettingType = settings.AchievementRateSettingType;
            _timingAssistSettingType = settings.TimingAssistSettingType;
            _isActiveComboEffect = settings.IsActiveComboEffect;
            _isPerfectContinuous = settings.IsPerfectContinuous;
            _isActiveSenseDisplay = basic.IsActiveSenseDisplay;
            _shouldShowPerfectStar = settings.ShouldShowPerfectStar;
            _timingPositionY = CalculatePositionY(settings.TimingEffectOffset);
            _timingScale = GetTimingEffectScale(settings.TimingEffectScaleType);
        }

        public void InitializeScore(RecoveredSoloScoreContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!_initialized) Initialize();
            _scoreContext = context;
            _score = new RecoveredSoloScoreRuntime(
                context.DifficultyAutoCoefficient,
                context.TotalStatus,
                context.BaseScorePercentage,
                context.TotalNotesCount);
            _scorePanel.Show();
            _scorePanel.Initialize(false, false, false);
            _scorePanel.CompleteInitializationAnimations();
            _scorePanel.SetScoreCount(0L, false);
        }

        public void ApplyStartEffects(RecoveredPlayerUnitFixture fixture)
        {
            if (fixture == null) throw new ArgumentNullException(nameof(fixture));
            if (!_initialized) Initialize();
            if (_life.Add(fixture.GetInitialLifeAddition()))
                _lifeGauge.SetLifeValue(
                    _life.Value, _life.MaxValue, _life.GuardCount);
        }

        public void InitializeSenseScore(RecoveredPlayerUnitFixture fixture)
        {
            if (fixture == null) throw new ArgumentNullException(nameof(fixture));
            if (!_initialized) Initialize();
            _currentPlayerUnit = fixture;
            _senseScore = new RecoveredSenseScoreRuntime(fixture);
            _starActScore = new RecoveredStarActScoreRuntime(fixture);
            _totalSenseLightCount = fixture.GetStarActSenseLightCount();
            RefreshSenseLights(null);
            _principalAddingMap = new Dictionary<long, int>();
            foreach (var senseEvent in fixture.senseEvents)
            {
                if (_principalAddingMap.TryGetValue(
                        senseEvent.senseMasterId, out var existing) &&
                    existing != senseEvent.acquirableGauge)
                    throw new InvalidOperationException(
                        "Sense Principal amount is inconsistent.");
                _principalAddingMap[senseEvent.senseMasterId] =
                    senseEvent.acquirableGauge;
            }
            _principalOrder = fixture.order;
            InitializePrincipal(new[]
            {
                new RecoveredPrincipalUnit(
                    fixture.order, fixture.initialMaxPrincipal),
            });
        }

        public void TickSenseScore(long chartMilliseconds)
        {
            if (!_visible || _senseScore == null) return;
            var added = _senseScore.Tick(
                chartMilliseconds,
                (senseEvent, addedScore, actorIndex) =>
                {
                    _scorePanel.SetSenseScoreCount(addedScore, actorIndex);
                    var card = Array.Find(
                        _currentPlayerUnit.cards,
                        candidate =>
                            candidate.position == senseEvent.activatingPosition);
                    if (card != null)
                        _additionalScoreCutInPanel?.OnSenseScoreAdded(
                            card.senseType, addedScore);
                    var starActAdded = _starActScore.OnSenseActivated(senseEvent);
                    RefreshSenseLights(_starActScore.HoldingLights);
                    if (starActAdded > 0L)
                        _scorePanel.SetStarActScoreCount(starActAdded);
                    _principal.ActivateSense(
                        senseEvent.senseMasterId,
                        _principalAddingMap,
                        _principalOrder);
                });
            if (added > 0L) _scorePanel.SetScoreCount(TotalScore, false);
        }

        private void RefreshSenseLights(IReadOnlyList<int> holdingLights)
        {
            if (_senseLightContents == null) return;
            var holdingCount = holdingLights?.Count ?? 0;
            for (var index = 0; index < _senseLightContents.Length; index++)
            {
                var content = _senseLightContents[index];
                var isRequired = index < _totalSenseLightCount;
                if (content.gameObject.activeSelf != isRequired)
                    content.gameObject.SetActive(isRequired);
                if (!isRequired) continue;
                var empty = FindDescendant(content, "EmptySenseLight")
                    ?.GetComponent<Image>();
                var light = FindDescendant(content, "SenseLight")
                    ?.GetComponent<Image>();
                var addition = FindDescendant(content, "SenseLightAdd")
                    ?.GetComponent<Image>();
                if (empty == null || light == null)
                    throw new InvalidOperationException(
                        "Original SenseLightContent images are incomplete.");
                var occupied = index < holdingCount;
                empty.enabled = !occupied;
                light.enabled = occupied;
                if (addition != null) addition.enabled = false;
                if (occupied)
                {
                    var type = holdingLights[index];
                    if (type < 0 || type >= _senseLightSprites.Length)
                        throw new InvalidOperationException(
                            "Unsupported Sense light visual type.");
                    light.sprite = _senseLightSprites[type];
                    light.color = Color.white;
                }
            }
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (var index = 0; index < root.childCount; index++)
            {
                var found = FindDescendant(root.GetChild(index), name);
                if (found != null) return found;
            }
            return null;
        }

        public void InitializePrincipal(IReadOnlyList<RecoveredPrincipalUnit> units)
        {
            if (!_initialized) Initialize();
            _principal.Initialize(units);
            if (!_principal.IsInitialized)
            {
                _principalGauge.Hide();
                return;
            }
            _principalGauge.gameObject.SetActive(true);
            OnDefaultPrincipalChanged(
                _principal.GetCurrentPrincipal(_principal.DefaultOrder),
                _principal.GetMaxPrincipal(_principal.DefaultOrder));
        }

        public int ActivateSensePrincipal(
            long senseId,
            IReadOnlyDictionary<long, int> addingPrincipalMap,
            int order)
        {
            return _principal.ActivateSense(senseId, addingPrincipalMap, order);
        }

        public void ProcessFrame(
            IReadOnlyList<RecoveredInputResultEntity> results,
            RecoveredGameResultRuntime combo)
        {
            if (!_visible || results == null || results.Count == 0) return;
            var selected = RecoveredTimingType.None;
            var selectedAssist = RecoveredTimingAssistType.None;
            for (var index = 0; index < results.Count; index++)
            {
                if (selected > RecoveredTimingType.Perfect) break;
                if (selected < results[index].TimingType)
                {
                    selected = results[index].TimingType;
                    selectedAssist = results[index].TimingAssistType;
                }
            }
            if (selected != RecoveredTimingType.None)
            {
                selected = ResolveDisplayedTiming(
                    selected, _shouldShowPerfectStar);
                var effect = RentTimingEffect();
                LastTimingEffect = effect;
                effect.Initialize(_timingScale, _timingSprites[(int)selected]);
                effect.SetLocalPosition(Vector3.zero);
                effect.Play();
                if (ShouldShowTimingAssist(
                        _timingAssistSettingType, selected, selectedAssist))
                {
                    var assistEffect = RentTimingAssistEffect();
                    if (assistEffect != null)
                    {
                        LastTimingAssistEffect = assistEffect;
                        assistEffect.Initialize(
                            _timingScale,
                            _timingAssistSprites[(int)selectedAssist]);
                        assistEffect.SetLocalPosition(Vector3.zero);
                        assistEffect.Play();
                    }
                }
            }

            var comboType = combo.IsAllPerfect
                ? Sirius.Game.UI.RecoveredComboType.AllPerfect
                : combo.IsFullCombo
                    ? Sirius.Game.UI.RecoveredComboType.FullCombo
                    : Sirius.Game.UI.RecoveredComboType.None;
            comboType = ResolveComboEffectType(_isActiveComboEffect, comboType);
            _comboPanel.SetComboCount(combo.Count, comboType);
            var hasMiss = false;
            for (var index = 0; index < results.Count; index++)
            {
                var result = results[index];
                if (result.TimingType == RecoveredTimingType.Miss) hasMiss = true;
                if (_score != null && _scoreContext.ContainsNote(result.NoteId))
                    _score.Collect(result.TimingType);
            }
            if (_score != null) _scorePanel.SetScoreCount(TotalScore, true);
            if (_achievementRatePanel != null)
                _achievementRatePanel.SetCount(
                    combo.Count,
                    comboType,
                    combo.GetDisplayedAchievementRate(
                        _achievementRateSettingType),
                    hasMiss,
                    combo.IsAllPerfect);
            if (_life.Process(results))
                _lifeGauge.SetLifeValue(_life.Value, _life.MaxValue, _life.GuardCount);
        }

        public void Hide()
        {
            if (!_initialized || !_visible) return;
            _visible = false;
            if (_canvasObject != null) _canvasObject.SetActive(false);
            foreach (var effect in _timingPool)
                if (effect != null) effect.gameObject.SetActive(false);
            foreach (var effect in _timingAssistPool)
                if (effect != null) effect.gameObject.SetActive(false);
        }

        public void Show()
        {
            if (!_initialized || _visible) return;
            _visible = true;
            if (_canvasObject != null) _canvasObject.SetActive(true);
            // Hide runs during the introduction and disables the preloaded
            // spawner instances. They must be active again before Play()
            // restarts the shared animation.
            foreach (var effect in _timingPool)
                if (effect != null) effect.gameObject.SetActive(true);
            foreach (var effect in _timingAssistPool)
                if (effect != null) effect.gameObject.SetActive(true);
        }

        public IEnumerator ShowIntroduction()
        {
            // GameIntroductionUIAnimationController ctor/ShowAsync and
            // GameStartAnimationManager: await alpha 0 -> 1 before starting play.
            if (!_initialized) yield break;
            _introductionCanvasGroup.alpha = 0f;
            Show();
            yield return _introductionCanvasGroup.DOFade(1f, 0.2f)
                .SetEase(Ease.InQuad).SetLink(_canvasObject).WaitForCompletion();
        }

        public static float CalculatePositionY(int timingEffectOffset)
        {
            return ((timingEffectOffset / -100f) + 1f) * 6.5f - 2.2f;
        }

        public static float GetTimingEffectScale(int type)
        {
            switch (type)
            {
                case 0: return 0.8f;
                case 1: return 1f;
                case 2: return 1.3f;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public static RecoveredTimingType ResolveDisplayedTiming(
            RecoveredTimingType timingType,
            bool shouldShowPerfectStar)
        {
            return !shouldShowPerfectStar &&
                   timingType == RecoveredTimingType.PerfectStar
                ? RecoveredTimingType.Perfect
                : timingType;
        }

        public static bool ShouldShowTimingAssist(
            int settingType,
            RecoveredTimingType timingType,
            RecoveredTimingAssistType assistType)
        {
            if (assistType == RecoveredTimingAssistType.None) return false;
            switch (settingType)
            {
                case 0:
                    return false;
                case 1:
                    return timingType > RecoveredTimingType.Miss &&
                           timingType < RecoveredTimingType.Perfect;
                case 2:
                    return timingType > RecoveredTimingType.Miss &&
                           timingType <= RecoveredTimingType.Perfect;
                default:
                    throw new ArgumentOutOfRangeException(nameof(settingType));
            }
        }

        public static Sirius.Game.UI.RecoveredComboType ResolveComboEffectType(
            bool isActiveComboEffect,
            Sirius.Game.UI.RecoveredComboType comboType)
        {
            // Retail ComboPanel.SetComboCount only replaces ComboTypes with None
            // here. Its OnCount and per-100 OnFire triggers still run.
            return isActiveComboEffect
                ? comboType
                : Sirius.Game.UI.RecoveredComboType.None;
        }

        private void ApplyTimingParentPosition()
        {
            // TimingEffectManager.InitializeAsync moves the injected
            // Game/Effects parent. Spawned TimingEffect instances stay at
            // local zero beneath it.
            var position = _timingParent.localPosition;
            position.y = _timingPositionY;
            _timingParent.localPosition = position;
        }

        private Sirius.Game.TimingEffect RentTimingEffect()
        {
            // TimingEffectSpawner owns exactly one preloaded instance. A newer
            // judgment restarts it instead of allocating an overlapping label.
            return _timingPool.Count > 0
                ? _timingPool[0]
                : CreateTimingEffect();
        }

        private Sirius.Game.TimingEffect CreateTimingEffect()
        {
            var instance = Instantiate(_timingEffectPrefab, _timingParent, false);
            instance.name = "TimingEffect";
            var effect = instance.GetComponent<Sirius.Game.TimingEffect>();
            _timingPool.Add(effect);
            return effect;
        }

        private Sirius.Game.TimingAssistEffect RentTimingAssistEffect()
        {
            foreach (var effect in _timingAssistPool)
                if (effect.IsUnused) return effect;
            // The retail spawner owns one preloaded assist instance and returns
            // null while it is still playing.
            return null;
        }

        private Sirius.Game.TimingAssistEffect CreateTimingAssistEffect()
        {
            var instance = Instantiate(
                _timingAssistEffectPrefab, _timingParent, false);
            instance.name = "TimingAssistEffect";
            var effect = instance.GetComponent<Sirius.Game.TimingAssistEffect>();
            if (effect == null)
                throw new InvalidOperationException(
                    "TimingAssistEffect prefab has no recovered component.");
            _timingAssistPool.Add(effect);
            return effect;
        }

        private void ValidateSprites()
        {
            if (_timingSprites == null || _timingSprites.Length != 7 ||
                _timingAssistSprites == null ||
                _timingAssistSprites.Length != 3 ||
                _timingAssistSprites[1] == null ||
                _timingAssistSprites[2] == null ||
                _senseCutInSprites == null || _senseCutInSprites.Length != 12 ||
                _normalComboCounts == null || _normalComboCounts.Length != 10 ||
                _fullComboCounts == null || _fullComboCounts.Length != 10 ||
                _allPerfectComboCounts == null || _allPerfectComboCounts.Length != 10 ||
                _comboLabels == null || _comboLabels.Length != 3 ||
                _noComboRateCounts == null || _noComboRateCounts.Length != 10 ||
                _ratePoints == null || _ratePoints.Length != 4 ||
                _ratePercents == null || _ratePercents.Length != 4)
                throw new InvalidOperationException("GameConfig HUD sprite tables are incomplete.");
        }

        private void OnDefaultPrincipalChanged(int value, int maxValue)
        {
            if (_principalGauge != null)
                _principalGauge.SetPrincipalValue(value, maxValue);
        }
    }
}
