using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Runtime adapter for the original Pause, Dialog, PauseGameDialogBody, and
    /// OptionDialogBody prefabs.
    /// The original hierarchy and graphics are retained; this class replaces
    /// stripped Sirius presenters with the recovered settings transaction.
    /// </summary>
    public sealed class GamePauseRuntime : MonoBehaviour
    {
        [SerializeField] private GameRuntime _gameRuntime;
        [SerializeField] private GameObject _pausePrefab;
        [SerializeField] private GameObject _dialogPrefab;
        [SerializeField] private GameObject _pauseDialogBodyPrefab;
        [SerializeField] private GameObject _optionDialogPrefab;
        [SerializeField] private GameObject _optionInformationDialogPrefab;
        [SerializeField] private GameObject _textSideMenuButtonPrefab;
        [SerializeField] private GameObject _resumeCountDownPrefab;
        [SerializeField] private Sprite _dialogNormalButtonSprite;
        [SerializeField] private Sprite _dialogPositiveButtonSprite;
        [SerializeField] private Sprite _toggleOnSprite;
        [SerializeField] private Sprite _toggleOffSprite;
        [SerializeField] private GameObject _gameSimulationCameraPrefab;
        [SerializeField] private GameObject _gameSimulationLaneGroupPrefab;
        [SerializeField] private GameObject _gameSimulationPreviewUiPrefab;
        [SerializeField] private GameObject _gameSimulationNotePrefab;
        [SerializeField] private RenderTexture _gameSimulationRenderTexture;

        private bool _selectionHost;
        private Action _selectionSettingsClosed;
        private SettingsStore _store;
        private SettingsSession _session;
        private UiSeRuntime _uiSe;
        private GameObject _canvasObject;
        private GameObject _pauseInstance;
        private GameObject _dialogInstance;
        private GameObject _pauseBodyInstance;
        private GameObject _settingsInstance;
        private GameObject _informationDialogInstance;
        private bool _isRestartConfirmationOpen;
        private GameObject _touchBlockInstance;
        private GameObject _resumeCountDownInstance;
        private Button _pauseButton;
        private Coroutine _resumeCoroutine;
        private GameObject _systemPanel;
        private GameObject _gamePanel;
        private GameObject _detailPanel;
        private GameObject _customPanel;
        private GameObject _soundPanel;
        private GameObject _bluetoothPanel;
        private RectTransform _settingsSelectedObject;
        private Text[] _settingsMenuLabels;
        private RectTransform[] _settingsMenuButtons;
        private int[] _settingsMenuPanelTypes;
        private ScrollRect _settingsScrollRect;
        private GameObject _gameSimulationRoot;
        private GameObject _gameSimulationView;
        private GameSimulationPreview _gameSimulationPreview;

        private static readonly Vector2 PauseDialogSize = new Vector2(1228f, 696f);
        private static readonly Vector2 SettingsDialogSize = new Vector2(1328f, 996f);

        public bool IsDialogOpen => _dialogInstance != null;
        public bool IsPauseMenuOpen => _pauseBodyInstance != null;
        public bool IsSettingsOpen => _settingsInstance != null;
        public bool IsSettingsConfirmationOpen => _isRestartConfirmationOpen;
        public bool IsResumeCountdownActive => _resumeCountDownInstance != null;
        public SettingsSession.Snapshot CurrentSettings =>
            _session?.Current;
        public bool HasPendingChanges => _session != null && _session.HasChanges;

        public void Configure(
            GameRuntime gameRuntime,
            GameObject pausePrefab,
            GameObject dialogPrefab,
            GameObject pauseDialogBodyPrefab,
            GameObject optionDialogPrefab,
            GameObject optionInformationDialogPrefab,
            GameObject textSideMenuButtonPrefab,
            GameObject resumeCountDownPrefab,
            Sprite dialogNormalButtonSprite,
            Sprite dialogPositiveButtonSprite,
            Sprite toggleOnSprite,
            Sprite toggleOffSprite,
            GameObject gameSimulationCameraPrefab,
            GameObject gameSimulationLaneGroupPrefab,
            GameObject gameSimulationPreviewUiPrefab,
            GameObject gameSimulationNotePrefab,
            RenderTexture gameSimulationRenderTexture)
        {
            _gameRuntime = gameRuntime;
            _pausePrefab = pausePrefab;
            _dialogPrefab = dialogPrefab;
            _pauseDialogBodyPrefab = pauseDialogBodyPrefab;
            _optionDialogPrefab = optionDialogPrefab;
            _optionInformationDialogPrefab = optionInformationDialogPrefab;
            _textSideMenuButtonPrefab = textSideMenuButtonPrefab;
            _resumeCountDownPrefab = resumeCountDownPrefab;
            _dialogNormalButtonSprite = dialogNormalButtonSprite;
            _dialogPositiveButtonSprite = dialogPositiveButtonSprite;
            _toggleOnSprite = toggleOnSprite;
            _toggleOffSprite = toggleOffSprite;
            _gameSimulationCameraPrefab = gameSimulationCameraPrefab;
            _gameSimulationLaneGroupPrefab = gameSimulationLaneGroupPrefab;
            _gameSimulationPreviewUiPrefab = gameSimulationPreviewUiPrefab;
            _gameSimulationNotePrefab = gameSimulationNotePrefab;
            _gameSimulationRenderTexture = gameSimulationRenderTexture;
        }

        // Shared OptionDialog host: selection saves immediately; gameplay retains
        // the original restart-confirmation transaction.
        public void ConfigureSelectionHost(Transform parent, Action closed)
        {
            _selectionHost = true;
            _canvasObject = parent.gameObject;
            _selectionSettingsClosed = closed;
            _store = new SettingsStore();
            _uiSe = UiSeRuntime.Instance;
        }

        public void OpenSelectionSettings()
        {
            if (!_selectionHost || IsDialogOpen) return;
            _session = new SettingsSession(_store.LoadOrDefault());
            CreateTouchBlock();
            OpenSettings();
        }

        private void CloseSelectionSettings(bool save)
        {
            if (save)
            {
                _store.Save(_session.Current);
                _session.AcceptSavedValues();
            }
            else _session.Cancel();
            DestroyGameSimulationPreview();
            DestroyDialog();
            DestroyTouchBlock();
            _selectionSettingsClosed?.Invoke();
        }

        private void OnDestroy()
        {
            DestroyGameSimulationPreview();
            DestroyDialog();
            DestroyTouchBlock();
        }

        private void Start()
        {
            if (_selectionHost) return;
            if (_gameRuntime == null || _pausePrefab == null ||
                _dialogPrefab == null || _pauseDialogBodyPrefab == null ||
                _optionDialogPrefab == null || _textSideMenuButtonPrefab == null ||
                _optionInformationDialogPrefab == null ||
                _resumeCountDownPrefab == null ||
                _dialogNormalButtonSprite == null ||
                _dialogPositiveButtonSprite == null ||
                _toggleOnSprite == null ||
                _toggleOffSprite == null ||
                _gameSimulationCameraPrefab == null ||
                _gameSimulationLaneGroupPrefab == null ||
                _gameSimulationPreviewUiPrefab == null ||
                _gameSimulationNotePrefab == null ||
                _gameSimulationRenderTexture == null)
                throw new InvalidOperationException(
                    "Recovered pause runtime requires GameRuntime, Pause, Dialog, " +
                    "PauseGameDialogBody, OptionDialogBody, TextSideMenuButton, " +
                    "and ResumeCountDown.");

            if (FindObjectOfType<EventSystem>() == null)
            {
                var eventSystem = new GameObject(
                    "EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            _store = new SettingsStore();
            _session = new SettingsSession(_store.LoadOrDefault());
            _uiSe = UiSeRuntime.Instance;
            if (_uiSe == null) _uiSe = GetComponent<UiSeRuntime>();
            if (_uiSe == null) _uiSe = gameObject.AddComponent<UiSeRuntime>();

            // TouchBlockFactory.Initialize receives the shared UI parent in the
            // original scene.  Its TouchBlock is therefore ordered against the
            // HUD by sibling index, rather than living in an unrelated root
            // Canvas. DialogMonoBehaviour optionally adds its own front Canvas.
            _gameRuntime.Initialize();
            var hudParent = _gameRuntime.GameHud?.CanvasTransform;
            if (hudParent == null)
                throw new InvalidOperationException(
                    "Recovered gameplay HUD Canvas is required by Pause.");
            _canvasObject = hudParent.gameObject;
            _pauseInstance = Instantiate(_pausePrefab, hudParent, false);
            _pauseInstance.name = "Pause";
            _pauseInstance.transform.SetAsLastSibling();
            _pauseButton = _pauseInstance.GetComponentInChildren<Button>(true);
            if (_pauseButton == null)
                throw new InvalidOperationException("Original Pause Button is missing.");
            _pauseButton.onClick.RemoveAllListeners();
            _pauseButton.onClick.AddListener(() =>
            {
                PlayUiSe(UiSeRuntime.Cue.ButtonGo);
                Open();
            });
        }

        public void Open()
        {
            if (_dialogInstance != null || _resumeCountDownInstance != null) return;
            _gameRuntime.SetPaused(true);
            CreateTouchBlock();
            OpenPauseMenu();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) PauseForApplicationSuspension();
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused) PauseForApplicationSuspension();
        }

        private void PauseForApplicationSuspension()
        {
            // The retail gameplay presenter opens Pause when the application
            // leaves the foreground only after play has entered the gameplay
            // state. During GameIntroduction the HUD canvas is intentionally
            // hidden and the introduction controller owns focus suspension;
            // opening Pause there would create an unreachable dialog and keep
            // GameRuntime from ever calling BeginGameplay.
            // Regaining focus never resumes an active gameplay Pause implicitly;
            // the player must use the normal three-second resume path.
            if (_gameRuntime == null || !_gameRuntime.IsInitialized ||
                !_gameRuntime.IsGameplayStarted ||
                _gameRuntime.IsPaused || _dialogInstance != null ||
                _resumeCountDownInstance != null)
                return;
            Open();
        }

        private void OpenPauseMenu()
        {
            DestroyGameSimulationPreview();
            DestroyDialog();
            _dialogInstance = CreateDialogShell("一時停止", PauseDialogSize);
            var body = RequireDescendant(_dialogInstance.transform, "Body");
            _pauseBodyInstance = Instantiate(
                _pauseDialogBodyPrefab, body, false);
            _pauseBodyInstance.name = "PauseGameDialogBody";
            var settings = RequireDescendant(
                _pauseBodyInstance.transform, "SettingsButton")
                .GetComponent<Button>();
            if (settings == null)
                throw new InvalidOperationException(
                    "Original PauseGameDialogBody SettingsButton is missing.");
            settings.onClick.RemoveAllListeners();
            settings.onClick.AddListener(() =>
            {
                PlayUiSe(UiSeRuntime.Cue.ButtonGo);
                OpenSettings();
            });

            ConfigureDialogButton(
                _dialogInstance.transform, "FirstButtonParent", "FirstButton",
                "リタイア", () => ShowConfirmation(
                    "ライブを終了しますか？",
                    "ライブを終了してリザルトへ進みます。",
                    Retire));
            ConfigureDialogButton(
                _dialogInstance.transform, "SecondButtonParent", "SecondButton",
                "リトライ", () => ShowConfirmation(
                    "リトライしますか？",
                    "ライブを最初からやり直します。",
                    Retry));
            ConfigureDialogButton(
                _dialogInstance.transform, "ThirdButtonParent", "ThirdButton",
                "ライブに戻る", Resume, true);
        }

        public void OpenSettings()
        {
            if (!_selectionHost && !_gameRuntime.IsPaused) return;
            DestroyDialog();
            // DialogHelper.OnSettingsAsync passes Height_960_Medium (120) and
            // YesNo (1), then initializes OptionDialogBody inside Dialog.Body.
            _dialogInstance = CreateDialogShell("設定", SettingsDialogSize);
            var body = RequireDescendant(_dialogInstance.transform, "Body");
            _settingsInstance = Instantiate(_optionDialogPrefab, body, false);
            _settingsInstance.name = "OptionDialogBody";
            _systemPanel =
                FindDescendant(_settingsInstance.transform, "SystemSettingsPanel")?.gameObject;
            _gamePanel =
                FindDescendant(_settingsInstance.transform, "GameSettingsPanel")?.gameObject;
            _detailPanel =
                FindDescendant(_settingsInstance.transform, "GameDetailSettingsPanel")?.gameObject;
            _customPanel =
                FindDescendant(_settingsInstance.transform, "GameCustomSettingsPanel")?.gameObject;
            _soundPanel =
                FindDescendant(_settingsInstance.transform, "SoundVolumeSettingsPanel")?.gameObject;
            _bluetoothPanel =
                FindDescendant(_settingsInstance.transform, "BluetoothSettingsPanel")?.gameObject;
            _settingsScrollRect =
                FindDescendant(_settingsInstance.transform, "ScrollView")
                    ?.GetComponent<ScrollRect>();
            if (_systemPanel == null || _gamePanel == null || _detailPanel == null ||
                _customPanel == null || _soundPanel == null ||
                _bluetoothPanel == null || _settingsScrollRect == null)
                throw new InvalidOperationException(
                    "Original System/Game/Detail/Custom/Sound/Bluetooth settings " +
                    "panels are missing.");

            ShowPanel(1);
            BindValues();
            BindInformationButtons();
            CreateNavigation();
            ConfigureDialogButton(
                _dialogInstance.transform, "FirstButtonParent", "FirstButton",
                "キャンセル", Cancel);
            ConfigureDialogButton(
                _dialogInstance.transform, "SecondButtonParent", "SecondButton",
                "OK", Save, true);
            RequireDescendant(
                _dialogInstance.transform, "ThirdButtonParent").gameObject.SetActive(false);
            CreateGameSimulationPreview();
        }

        private void BindInformationButtons()
        {
            BindInformationButton(_gamePanel, 0);
            if (_detailPanel == null) return;
            var informationButtons = new System.Collections.Generic.List<Button>();
            foreach (var button in _detailPanel.GetComponentsInChildren<Button>(true))
            {
                if (button.name == "InformationButton")
                    informationButtons.Add(button);
            }
            if (informationButtons.Count != 4)
                throw new InvalidOperationException(
                    "Original GameDetailSettingsPanel must contain four " +
                    $"InformationButtons, found {informationButtons.Count}.");
            for (var index = 0; index < informationButtons.Count; index++)
            {
                var informationType = index + 1;
                var button = informationButtons[index];
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    PlayUiSe(UiSeRuntime.Cue.ButtonGo);
                    OpenInformation(informationType);
                });
            }
        }

        private void BindInformationButton(GameObject panel, int informationType)
        {
            var root = panel != null
                ? FindDescendant(panel.transform, "InformationButton")
                : null;
            var button = root != null ? root.GetComponent<Button>() : null;
            if (button == null)
                throw new InvalidOperationException(
                    $"InformationButton for type {informationType} is missing.");
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                PlayUiSe(UiSeRuntime.Cue.ButtonGo);
                OpenInformation(informationType);
            });
        }

        private void OpenInformation(int informationType)
        {
            if (_settingsInstance == null || _informationDialogInstance != null)
                return;
            _informationDialogInstance =
                CreateDialogShell("設定について", SettingsDialogSize, 111);
            _informationDialogInstance.name = "OptionInformationDialog";
            var body = RequireDescendant(
                _informationDialogInstance.transform, "Body");
            var informationBody = Instantiate(
                _optionInformationDialogPrefab, body, false);
            informationBody.name = "OptionInformationDialogBody";
            var typeNames = new[]
            {
                "GameSettings",
                "LaneSettings",
                "PositionSettings",
                "DisplaySettings",
                "MusicScoreSettings",
            };
            for (var index = 0; index < typeNames.Length; index++)
            {
                var typeRoot = FindDescendant(informationBody.transform, typeNames[index]);
                if (typeRoot == null)
                    throw new InvalidOperationException(
                        $"Information body section {typeNames[index]} is missing.");
                typeRoot.gameObject.SetActive(index == informationType);
            }
            var scrollRect = informationBody.GetComponentInChildren<ScrollRect>(true);
            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 1f;
            }
            RequireDescendant(
                _informationDialogInstance.transform, "FirstButtonParent")
                .gameObject.SetActive(false);
            ConfigureDialogButton(
                _informationDialogInstance.transform,
                "SecondButtonParent", "SecondButton",
                "OK", CloseInformation, true);
            RequireDescendant(
                _informationDialogInstance.transform, "ThirdButtonParent")
                .gameObject.SetActive(false);
        }

        private void CloseInformation()
        {
            if (_informationDialogInstance != null)
                Destroy(_informationDialogInstance);
            _informationDialogInstance = null;
        }

        private void BindValues()
        {
            var system = _session.Current.SystemSettings;
            var basic = _session.Current.GameSettings;
            var detail = _session.Current.GameDetailSettings;
            var custom = _session.Current.GameCustomSettings;
            var sound = _session.Current.SoundVolumeSettings;
            var bluetooth = _session.Current.BluetoothSettings;
            BindStepper(_systemPanel.transform, "LetterDisplaySpeedSettings",
                () => system.TextSpeed,
                value => system.TextSpeed = (int)value,
                GameSettings.MinimumTextSpeed,
                GameSettings.MaximumTextSpeed,
                GameSettings.TextSpeedStep, "F0");
            BindStepper(_systemPanel.transform, "TextDisplaySpeedSettings",
                () => system.TextDisplaySpeed,
                value => system.TextDisplaySpeed = (int)value,
                GameSettings.MinimumTextSpeed,
                GameSettings.MaximumTextSpeed,
                GameSettings.TextSpeedStep, "F0");
            BindChoice(
                FindChoiceRoot(_systemPanel.transform, "GraphicQualitySettings", 4),
                () => system.QualitySetting,
                value => system.QualitySetting = value,
                4);
            BindToggle(_systemPanel.transform, "HomeFpsSettings",
                () => system.Is60Fps,
                value => system.Is60Fps = value);
            BindToggle(_systemPanel.transform, "PreGameOptionDialogSettings",
                () => system.IsPreLiveOptionConfirmation,
                value => system.IsPreLiveOptionConfirmation = value);
            BindToggle(_systemPanel.transform, "TitleBackgroundSettings",
                () => system.IsDefaultTitleBackground,
                value => system.IsDefaultTitleBackground = value);
            BindToggle(_systemPanel.transform, "FriendNoticeSettings",
                () => system.IsFriendInviteOptionConfirmation,
                value => system.IsFriendInviteOptionConfirmation = value);
            BindToggle(_systemPanel.transform, "CircleNoticeSettings",
                () => system.IsCircleInviteOptionConfirmation,
                value => system.IsCircleInviteOptionConfirmation = value);
            BindToggle(_systemPanel.transform, "StaminaMaxNoticeSettings",
                () => system.IsStaminaMaxOptionConfirmation,
                value => system.IsStaminaMaxOptionConfirmation = value);
            BindToggle(_systemPanel.transform, "PracticeNoticeSettings",
                () => system.IsPracticeOptionConfirmation,
                value => system.IsPracticeOptionConfirmation = value);
            BindToggle(_systemPanel.transform, "EventNoticeSettings",
                () => system.IsEventNoticeOptionConfirmation,
                value => system.IsEventNoticeOptionConfirmation = value);
            BindToggle(_systemPanel.transform, "GachaNoticeSettings",
                () => system.IsGachaNoticeOptionConfirmation,
                value => system.IsGachaNoticeOptionConfirmation = value);
            BindToggle(_systemPanel.transform, "NightTimeNoticeSettings",
                () => system.IsNightTimeNoticeOptionConfirmation,
                value => system.IsNightTimeNoticeOptionConfirmation = value);
            BindToggle(_systemPanel.transform, "ChatNoticeSettings",
                () => system.IsChatNoticeOptionConfirmation,
                value => system.IsChatNoticeOptionConfirmation = value);
            BindToggle(_systemPanel.transform, "MessageNoticeSettings",
                () => system.IsMessageNoticeOptionConfirmation,
                value => system.IsMessageNoticeOptionConfirmation = value);
            BindToggle(_systemPanel.transform, "CircleStaminaMaxNoticeSettings",
                () => system.IsCircleStaminaMaxOptionConfirmation,
                value => system.IsCircleStaminaMaxOptionConfirmation = value);
            BindToggle(_systemPanel.transform, "LeagueNoticeSettings",
                () => system.IsLeagueNoticeOptionConfirmation,
                value => system.IsLeagueNoticeOptionConfirmation = value);
            BindStepper(_gamePanel.transform, "NoteSpeedSettings",
                () => basic.NoteSpeed,
                value => basic.NoteSpeed = value,
                GameSettings.MinimumNoteSpeed,
                GameSettings.MaximumNoteSpeed,
                GameSettings.NoteSpeedStep, "F1", "",
                GameSettings.NoteSpeedFineStep,
                RefreshNoteDisplayTimes);
            BindStepper(_gamePanel.transform, "TimingSettings",
                () => basic.NoteTimingValue,
                value => basic.NoteTimingValue = value,
                GameSettings.MinimumNoteTimingValue,
                GameSettings.MaximumNoteTimingValue,
                GameSettings.NoteTimingValueStep, "F1", "",
                GameSettings.NoteTimingValueFineStep);
            BindStepper(_gamePanel.transform, "OffsetSettings",
                () => basic.NoteOffsetValue,
                value => basic.NoteOffsetValue = value,
                GameSettings.MinimumNoteOffsetValue,
                GameSettings.MaximumNoteOffsetValue,
                GameSettings.NoteOffsetValueStep, "F1", "",
                GameSettings.NoteOffsetValueFineStep,
                RefreshGameSimulationSettings);
            BindStepper(_gamePanel.transform, "LaneAlphaSettings",
                () => basic.LaneAlphaValue,
                value => basic.LaneAlphaValue = (int)value,
                GameSettings.MinimumLaneAlpha,
                GameSettings.MaximumLaneAlpha,
                GameSettings.LaneAlphaStep, "F0", "%");
            BindToggle(_gamePanel.transform, "SenseDisplaySettings",
                () => basic.IsActiveSenseDisplay,
                value => basic.IsActiveSenseDisplay = value,
                RefreshGameSimulationSettings);
            BindToggle(_gamePanel.transform, "CutInSettings",
                () => basic.IsActiveSenseCutIn,
                value => basic.IsActiveSenseCutIn = value);
            BindStepper(_gamePanel.transform, "SplitEffectLineOpacitySettings",
                () => basic.SplitEffectLineOpacity,
                value => basic.SplitEffectLineOpacity = (int)value,
                GameSettings.MinimumSplitEffectLineOpacity,
                GameSettings.MaximumSplitEffectLineOpacity,
                GameSettings.SplitEffectLineOpacityStep, "F0", "%");
            BindChoice(
                FindChoiceRoot(_gamePanel.transform, "SplitEffectSettings", 3),
                () => basic.SpritEffectSettingType,
                value => basic.SpritEffectSettingType = value,
                3);
            BindToggle(_gamePanel.transform, "VibrationSettings",
                () => basic.IsActiveVibration,
                value => basic.IsActiveVibration = value);
            BindToggle(_gamePanel.transform, "MusicVideoSettings",
                () => basic.IsActiveMusicVideo,
                value => basic.IsActiveMusicVideo = value);
            BindToggle(_gamePanel.transform, "MusicVideoTypeSettings",
                () => basic.IsRealTimeRenderingMusicVideo,
                value => basic.IsRealTimeRenderingMusicVideo = value);
            BindChoice(
                FindChoiceRoot(_gamePanel.transform, "GameEndVoiceSettings", 2),
                () => basic.GameEndVoiceSettingType,
                value => basic.GameEndVoiceSettingType = value,
                2);
            BindStepper(_detailPanel.transform, "LaneWidthSettings",
                () => detail.LaneWidth,
                value => detail.LaneWidth = (int)value,
                GameSettings.MinimumLaneWidth,
                GameSettings.MaximumLaneWidth,
                GameSettings.LaneWidthStep, "F0", "%", 0d,
                RefreshGameSimulationSettings);
            BindStepper(_detailPanel.transform, "NoteStartSettings",
                () => detail.NoteStartOffset,
                value => detail.NoteStartOffset = (int)value,
                GameSettings.MinimumNoteStartOffset,
                GameSettings.MaximumNoteStartOffset,
                GameSettings.NoteStartOffsetStep, "F0", "", 0d,
                RefreshNoteDisplayTimes);
            BindStepper(_detailPanel.transform, "TimingOffsetSettings",
                () => detail.TimingEffectOffset,
                value => detail.TimingEffectOffset = (int)value,
                GameSettings.MinimumTimingEffectOffset,
                GameSettings.MaximumTimingEffectOffset,
                GameSettings.TimingEffectOffsetStep, "F0", "", 0d,
                RefreshGameSimulationSettings);
            BindStepper(_detailPanel.transform, "NotesHeightSettings",
                () => detail.NoteHeight,
                value => detail.NoteHeight = (int)value,
                GameSettings.MinimumNoteHeight,
                GameSettings.MaximumNoteHeight,
                GameSettings.NoteHeightStep, "F0");
            BindEnumStepper(
                _detailPanel.transform, "TimingScaleSettings",
                () => detail.TimingEffectScaleType,
                value => detail.TimingEffectScaleType = value,
                new[] { "小", "中", "大" },
                RefreshGameSimulationSettings);
            BindChoice(
                FindChoiceRoot(_detailPanel.transform, "TimingAssisteSettings", 3),
                () => GameSettings.ConvertThreeChoiceToggleIndex(
                    detail.TimingAssistSettingType),
                value => detail.TimingAssistSettingType =
                    GameSettings.ConvertThreeChoiceToggleIndex(value),
                3);
            BindChoice(
                FindChoiceRoot(_detailPanel.transform, "AchievementRateSettings", 3),
                () => GameSettings.ConvertThreeChoiceToggleIndex(
                    detail.AchievementRateSettingType),
                value => detail.AchievementRateSettingType =
                    GameSettings.ConvertThreeChoiceToggleIndex(value),
                3);
            BindToggle(_detailPanel.transform, "ConcurrentLineSettings",
                () => detail.IsActiveConcurrentLine,
                value => detail.IsActiveConcurrentLine = value);
            BindToggle(_detailPanel.transform, "LaneAssistLineSettings",
                () => detail.IsActiveLaneAssistLine,
                value => detail.IsActiveLaneAssistLine = value);
            BindToggle(_detailPanel.transform, "KeyBeamSettings",
                () => detail.IsActiveKeyBeam,
                value => detail.IsActiveKeyBeam = value);
            BindChoice(
                FindChoiceRoot(_detailPanel.transform, "TapEffectSettings", 2),
                () => detail.TapEffectType,
                value => detail.TapEffectType = value,
                2);
            BindToggle(_detailPanel.transform, "PerfectStarSettings",
                () => detail.ShouldShowPerfectStar,
                value => detail.ShouldShowPerfectStar = value);
            BindToggle(_detailPanel.transform, "ComboColorSettings",
                () => detail.IsActiveComboEffect,
                value => detail.IsActiveComboEffect = value);
            BindToggle(_detailPanel.transform, "PerfectContinuousSettings",
                () => detail.IsPerfectContinuous,
                value => detail.IsPerfectContinuous = value);
            BindToggle(_detailPanel.transform, "FrameRateSettings",
                () => detail.IsLowFrameRate,
                value => detail.IsLowFrameRate = value);
            BindToggle(_detailPanel.transform, "MirrorSettings",
                () => detail.IsActiveMirror,
                value => detail.IsActiveMirror = value);
            BindToggle(_detailPanel.transform, "SplitRandomSettings",
                () => detail.IsActiveSplitRandom,
                value => detail.IsActiveSplitRandom = value);
            BindStepper(_customPanel.transform, "NormalTapSESettings",
                () => custom.NormalSeId,
                value => custom.NormalSeId = (int)value,
                GameSettings.MinimumGameSeId,
                GameSettings.MaximumGameSeId,
                GameSettings.GameSeIdStep, "F0");
            BindStepper(_customPanel.transform, "CriticalTapSoundSettings",
                () => custom.CriticalSeId,
                value => custom.CriticalSeId = (int)value,
                GameSettings.MinimumGameSeId,
                GameSettings.MaximumGameSeId,
                GameSettings.GameSeIdStep, "F0");
            BindStepper(_customPanel.transform, "BombTypeSettings",
                () => custom.BombType,
                value => custom.BombType = (int)value,
                GameSettings.MinimumBombType,
                GameSettings.MaximumBombType,
                GameSettings.BombTypeStep, "F0");

            BindVolumeGroup(
                FindDirectChild(_soundPanel.transform, "SliderSettings"),
                () => sound.SystemMaster,
                value => sound.SystemMaster = value,
                new Func<int>[]
                {
                    () => sound.SystemBGM,
                    () => sound.SystemSE,
                    () => sound.SystemVoice,
                    () => sound.SystemStampVoice,
                },
                new Action<int>[]
                {
                    value => sound.SystemBGM = value,
                    value => sound.SystemSE = value,
                    value => sound.SystemVoice = value,
                    value => sound.SystemStampVoice = value,
                });
            BindVolumeGroup(
                FindDirectChild(_soundPanel.transform, "SliderSettings (1)"),
                () => sound.GameMaster,
                value => sound.GameMaster = value,
                new Func<int>[]
                {
                    () => sound.GameBGM,
                    () => sound.GameSE,
                    () => sound.GameNotesTap,
                    () => sound.GameVoice,
                },
                new Action<int>[]
                {
                    value => sound.GameBGM = value,
                    value => sound.GameSE = value,
                    value => sound.GameNotesTap = value,
                    value => sound.GameVoice = value,
                });
            BindVolumeGroup(
                FindDirectChild(_soundPanel.transform, "SliderSettings (2)"),
                () => sound.StoryMaster,
                value => sound.StoryMaster = value,
                new Func<int>[]
                {
                    () => sound.StoryBGM,
                    () => sound.StorySE,
                    () => sound.StoryVoice,
                },
                new Action<int>[]
                {
                    value => sound.StoryBGM = value,
                    value => sound.StorySE = value,
                    value => sound.StoryVoice = value,
                });
            BindStepper(_bluetoothPanel.transform,
                "BluetoothSoundTimingOffsetSettings",
                () => bluetooth.NoteTimingValue,
                value => bluetooth.NoteTimingValue = value,
                GameSettings.MinimumNoteTimingValue,
                GameSettings.MaximumNoteTimingValue,
                GameSettings.NoteTimingValueStep, "F1", "",
                GameSettings.NoteTimingValueFineStep);
            BindStepper(_bluetoothPanel.transform,
                "BluetoothTapTimingOffsetSettings",
                () => bluetooth.NoteOffsetValue,
                value => bluetooth.NoteOffsetValue = value,
                GameSettings.MinimumNoteTimingValue,
                GameSettings.MaximumNoteTimingValue,
                GameSettings.NoteTimingValueStep, "F1", "",
                GameSettings.NoteTimingValueFineStep);
            BindVolumeSlider(
                FindDescendant(_bluetoothPanel.transform, "BluetoothSEVolumeSetting"),
                () => bluetooth.GameSeVolume,
                value => bluetooth.GameSeVolume = value);
            BindVolumeSlider(
                FindDescendant(
                    _bluetoothPanel.transform, "BluetoothNotesTapVolumeSetting"),
                () => bluetooth.GameNotesTapVolume,
                value => bluetooth.GameNotesTapVolume = value);
            RefreshNoteDisplayTimes();

            BindReset(_systemPanel.transform, () =>
            {
                _session.Current.SystemSettings =
                    GameSettings.System.Default();
                BindValues();
            });
            BindReset(_gamePanel.transform, () =>
            {
                _session.Current.GameSettings = GameSettings.Basic.Default();
                BindValues();
            });
            BindReset(_detailPanel.transform, () =>
            {
                _session.Current.GameDetailSettings = GameSettings.Detail.Default();
                BindValues();
            });
            BindReset(_customPanel.transform, () =>
            {
                _session.Current.GameCustomSettings =
                    GameSettings.Custom.Default();
                BindValues();
            });
            BindReset(_soundPanel.transform, () =>
            {
                _session.Current.SoundVolumeSettings =
                    GameSettings.SoundVolume.Default();
                BindValues();
            });
            BindReset(_bluetoothPanel.transform, () =>
            {
                _session.Current.BluetoothSettings =
                    GameSettings.Bluetooth.Default();
                BindValues();
            });
        }

        private static void BindVolumeGroup(
            Transform group,
            Func<int> readMaster,
            Action<int> writeMaster,
            Func<int>[] readSub,
            Action<int>[] writeSub)
        {
            if (group == null)
                throw new InvalidOperationException("Sound volume group is missing.");
            var main = FindDirectChild(group, "MainSettings");
            var mainSetting = main != null
                ? FindDirectChild(main, "SliderSettings")
                : null;
            BindVolumeSlider(mainSetting, readMaster, writeMaster);

            var sub = FindDirectChild(group, "SubSettings");
            if (sub == null || sub.childCount < readSub.Length ||
                readSub.Length != writeSub.Length)
                throw new InvalidOperationException(
                    "Sound volume sub-slider count does not match machine code.");
            for (var index = 0; index < readSub.Length; index++)
                BindVolumeSlider(sub.GetChild(index), readSub[index], writeSub[index]);
        }

        private static void BindVolumeSlider(
            Transform setting,
            Func<int> read,
            Action<int> write)
        {
            var sliderTransform = setting != null
                ? FindDescendant(setting, "VolumeSlider")
                : null;
            var slider = sliderTransform != null
                ? sliderTransform.GetComponent<Slider>()
                : null;
            var valueTextTransform = setting != null
                ? FindDescendant(setting, "CurrentValueText")
                : null;
            var valueText = valueTextTransform != null
                ? valueTextTransform.GetComponent<Text>()
                : null;
            if (slider == null)
                throw new InvalidOperationException("Original volume Slider is missing.");
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.onValueChanged.RemoveAllListeners();
            slider.SetValueWithoutNotify(Mathf.Clamp(read(), 0, 100) / 100f);
            if (valueText != null) valueText.text = Mathf.Clamp(read(), 0, 100) + "%";
            slider.onValueChanged.AddListener(value =>
            {
                var percentage = Mathf.Clamp(
                    Mathf.RoundToInt(value * 100f),
                    GameSettings.MinimumVolume,
                    GameSettings.MaximumVolume);
                write(percentage);
                if (valueText != null) valueText.text = percentage + "%";
            });
        }

        private void BindStepper(
            Transform panel,
            string name,
            Func<double> read,
            Action<double> write,
            double minimum,
            double maximum,
            double step,
            string format,
            string unit = "",
            double fineStep = 0d,
            Action afterWrite = null)
        {
            var root = FindActiveNamedDescendant(panel, name);
            if (root == null) return;
            var textTransform = FindDescendant(root, "CurrentValueText");
            var valueText = textTransform != null ? textTransform.GetComponent<Text>() : null;
            Action refresh = () =>
            {
                if (valueText != null) valueText.text = read().ToString(format) + unit;
            };
            var minus = FindDescendant(root, "Minus")?.GetComponent<Button>();
            var plus = FindDescendant(root, "Plus")?.GetComponent<Button>();
            if (minus != null)
            {
                minus.onClick.RemoveAllListeners();
                minus.onClick.AddListener(() =>
                {
                    PlayUiSe(UiSeRuntime.Cue.Minus);
                    write(Math.Max(minimum, read() - step));
                    refresh();
                    afterWrite?.Invoke();
                });
            }
            if (plus != null)
            {
                plus.onClick.RemoveAllListeners();
                plus.onClick.AddListener(() =>
                {
                    PlayUiSe(UiSeRuntime.Cue.Plus);
                    write(Math.Min(maximum, read() + step));
                    refresh();
                    afterWrite?.Invoke();
                });
            }
            if (fineStep > 0d)
            {
                var decimalMinus =
                    FindDescendant(root, "DecimalMinus")?.GetComponent<Button>();
                var decimalPlus =
                    FindDescendant(root, "DecimalPlus")?.GetComponent<Button>();
                if (decimalMinus != null)
                {
                    decimalMinus.onClick.RemoveAllListeners();
                    decimalMinus.onClick.AddListener(() =>
                    {
                        PlayUiSe(UiSeRuntime.Cue.Minus);
                        write(Math.Max(minimum, Math.Round(read() - fineStep, 2)));
                        refresh();
                        afterWrite?.Invoke();
                    });
                }
                if (decimalPlus != null)
                {
                    decimalPlus.onClick.RemoveAllListeners();
                    decimalPlus.onClick.AddListener(() =>
                    {
                        PlayUiSe(UiSeRuntime.Cue.Plus);
                        write(Math.Min(maximum, Math.Round(read() + fineStep, 2)));
                        refresh();
                        afterWrite?.Invoke();
                    });
                }
            }
            refresh();
        }

        private void BindEnumStepper(
            Transform panel,
            string name,
            Func<int> read,
            Action<int> write,
            string[] labels,
            Action afterWrite = null)
        {
            var root = FindActiveNamedDescendant(panel, name);
            if (root == null || labels == null || labels.Length == 0) return;
            var valueText = FindDescendant(root, "CurrentValueText")
                ?.GetComponent<Text>();
            Action refresh = () =>
            {
                var index = Mathf.Clamp(read(), 0, labels.Length - 1);
                if (valueText != null) valueText.text = labels[index];
            };
            var minus = FindDescendant(root, "Minus")?.GetComponent<Button>();
            var plus = FindDescendant(root, "Plus")?.GetComponent<Button>();
            if (minus != null)
            {
                minus.onClick.RemoveAllListeners();
                minus.onClick.AddListener(() =>
                {
                    PlayUiSe(UiSeRuntime.Cue.Minus);
                    write(Mathf.Max(0, read() - 1));
                    refresh();
                    afterWrite?.Invoke();
                });
            }
            if (plus != null)
            {
                plus.onClick.RemoveAllListeners();
                plus.onClick.AddListener(() =>
                {
                    PlayUiSe(UiSeRuntime.Cue.Plus);
                    write(Mathf.Min(labels.Length - 1, read() + 1));
                    refresh();
                    afterWrite?.Invoke();
                });
            }
            refresh();
        }

        private void RefreshNoteDisplayTimes()
        {
            if (_session == null) return;
            var value = GameSettings.CalculateNoteDisplayTime(
                _session.Current.GameDetailSettings.NoteStartOffset,
                _session.Current.GameSettings.NoteSpeed).ToString();
            SetNoteDisplayTime(_gamePanel, value);
            SetNoteDisplayTime(_detailPanel, value);
            RefreshGameSimulationSettings();
        }

        private void RefreshGameSimulationSettings()
        {
            if (_gameSimulationPreview == null || _session == null) return;
            _gameSimulationPreview.SetSettings(
                _session.Current.GameSettings.NoteSpeed,
                _session.Current.GameSettings.NoteOffsetValue,
                _session.Current.GameDetailSettings.NoteStartOffset,
                _session.Current.GameDetailSettings.TimingEffectOffset,
                _session.Current.GameDetailSettings.TimingEffectScaleType,
                _session.Current.GameSettings.IsActiveSenseDisplay,
                _session.Current.GameDetailSettings.LaneWidth);
        }

        private void CreateGameSimulationPreview()
        {
            DestroyGameSimulationPreview();
            _gameSimulationRoot = new GameObject("GameSimulation");
            var camera = Instantiate(
                _gameSimulationCameraPrefab, _gameSimulationRoot.transform, false);
            camera.name = "GameSimulationCamera";
            var simulationCamera = camera.GetComponent<Camera>();
            _gameSimulationPreview =
                _gameSimulationRoot.AddComponent<GameSimulationPreview>();
            _gameSimulationPreview.Configure(
                simulationCamera,
                _gameSimulationLaneGroupPrefab,
                _gameSimulationPreviewUiPrefab,
                _gameSimulationNotePrefab,
                _session.Current.GameSettings.NoteSpeed,
                _session.Current.GameSettings.NoteOffsetValue,
                _session.Current.GameDetailSettings.NoteStartOffset,
                _session.Current.GameDetailSettings.TimingEffectOffset,
                _session.Current.GameDetailSettings.TimingEffectScaleType,
                _session.Current.GameSettings.IsActiveSenseDisplay,
                _session.Current.GameDetailSettings.LaneWidth,
                _session.Current.GameSettings.LaneAlphaValue,
                _session.Current.GameDetailSettings.NoteHeight);

            _gameSimulationView = new GameObject(
                "GameSimulationView", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasGroup));
            var canvas = _gameSimulationView.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            // Original GameSimulationView is order 61 and sits immediately
            // behind Dialog. The recovered dialog uses 110, so retain the same
            // relative ordering.
            canvas.sortingOrder = 109;
            var rootRect = (RectTransform)_gameSimulationView.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var imageObject = new GameObject(
                "RawImage", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(RawImage));
            imageObject.transform.SetParent(_gameSimulationView.transform, false);
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

        private void DestroyGameSimulationPreview()
        {
            if (_gameSimulationRoot != null)
            {
                _gameSimulationRoot.SetActive(false);
                Destroy(_gameSimulationRoot);
            }
            if (_gameSimulationView != null)
            {
                _gameSimulationView.SetActive(false);
                Destroy(_gameSimulationView);
            }
            _gameSimulationRoot = null;
            _gameSimulationView = null;
            _gameSimulationPreview = null;
        }

        private static void SetNoteDisplayTime(GameObject panel, string value)
        {
            if (panel == null) return;
            var settings = FindDescendant(panel.transform, "NoteDisplayTimeSettings");
            var valueRoot = settings != null
                ? FindDescendant(settings, "NoteDisplayTime")
                : null;
            var text = valueRoot != null ? valueRoot.GetComponent<Text>() : null;
            if (text != null) text.text = value;
        }

        private void BindToggle(
            Transform panel,
            string name,
            Func<bool> read,
            Action<bool> write,
            Action changed = null)
        {
            var root = FindDescendant(panel, name);
            if (root == null) return;
            var on = FindDescendant(root, "On")?.GetComponent<Button>();
            var off = FindDescendant(root, "Off")?.GetComponent<Button>();
            Action refresh = () =>
            {
                SetToggleVisual(on, read());
                SetToggleVisual(off, !read());
            };
            if (on != null)
            {
                on.onClick.RemoveAllListeners();
                on.onClick.AddListener(() =>
                {
                    PlayUiSe(UiSeRuntime.Cue.ButtonGo);
                    write(true);
                    refresh();
                    changed?.Invoke();
                });
            }
            if (off != null)
            {
                off.onClick.RemoveAllListeners();
                off.onClick.AddListener(() =>
                {
                    PlayUiSe(UiSeRuntime.Cue.ButtonGo);
                    write(false);
                    refresh();
                    changed?.Invoke();
                });
            }
            refresh();
        }

        private void SetToggleVisual(Button button, bool active)
        {
            if (button == null) return;
            // The original prefab uses Sirius.ToggleImage, not ToggleObject:
            // InnerImage remains active and swaps between the common button
            // bundle's red "on" and gray "off" Sprites.
            var inner = FindDescendant(button.transform, "InnerImage");
            var image = inner != null ? inner.GetComponent<Image>() : null;
            if (image == null)
                throw new InvalidOperationException(
                    $"{button.name} has no ToggleImage InnerImage.");
            inner.gameObject.SetActive(true);
            image.sprite = active ? _toggleOnSprite : _toggleOffSprite;
        }

        private void BindChoice(
            Transform root,
            Func<int> read,
            Action<int> write,
            int count,
            Action afterWrite = null)
        {
            if (root == null) return;
            var names = count == 2
                ? new[] { "On", "Off" }
                : count == 3
                    ? new[] { "First", "Second", "Third" }
                    : new[] { "First", "Second", "Third", "Fourth" };
            var buttons = new Button[count];
            Action refresh = () =>
            {
                var activeIndex = Mathf.Clamp(read(), 0, count - 1);
                for (var index = 0; index < buttons.Length; index++)
                    SetToggleVisual(buttons[index], index == activeIndex);
            };
            for (var index = 0; index < count; index++)
            {
                buttons[index] = FindDescendant(root, names[index])?.GetComponent<Button>();
                if (buttons[index] == null) continue;
                var captured = index;
                buttons[index].onClick.RemoveAllListeners();
                buttons[index].onClick.AddListener(() =>
                {
                    PlayUiSe(UiSeRuntime.Cue.ButtonGo);
                    write(captured);
                    refresh();
                    afterWrite?.Invoke();
                });
            }
            refresh();
        }

        private void BindReset(Transform panel, UnityEngine.Events.UnityAction action)
        {
            var reset = FindDescendant(panel, "ResetButton")?.GetComponent<Button>();
            if (reset == null) return;
            reset.onClick.RemoveAllListeners();
            reset.onClick.AddListener(() =>
            {
                PlayUiSe(UiSeRuntime.Cue.ButtonGo);
                action();
            });
        }

        private static Transform FindChoiceRoot(Transform root, string name, int choiceCount)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != name) continue;
                var hasChoices = choiceCount == 2
                    ? FindDescendant(child, "On") != null &&
                      FindDescendant(child, "Off") != null
                    : FindDescendant(child, "First") != null &&
                      FindDescendant(child, "Second") != null &&
                      FindDescendant(child, "Third") != null &&
                      (choiceCount == 3 ||
                       FindDescendant(child, "Fourth") != null);
                if (hasChoices) return child;
            }
            return null;
        }

        private void CreateNavigation()
        {
            var sideMenu = RequireDescendant(
                _settingsInstance.transform, "TextSideMenuPanel");
            var menuList = FindDirectChild(sideMenu, "MenuList");
            var content = menuList != null
                ? FindDirectChild(menuList, "Content")
                : null;
            if (menuList == null || content == null)
                throw new InvalidOperationException(
                    "Original TextSideMenuPanel content is missing.");

            // The in-game OptionDialog hides both the system and game-custom
            // entries. Retail pause settings therefore go directly from the
            // detailed game panel to volume.
            var entries = new[]
            {
                ("公演設定", 1),
                ("公演設定(詳細)", 2),
                ("音量", 4),
                ("Bluetooth設定", 5),
            };
            _settingsSelectedObject =
                RequireDescendant(content, "SelectedObject") as RectTransform;
            _settingsMenuLabels = new Text[entries.Length];
            _settingsMenuButtons = new RectTransform[entries.Length];
            _settingsMenuPanelTypes = new int[entries.Length];

            // SideMenuPanel.SetEnabledScroller uses a five-item boundary and,
            // for dialog mode, applies 50 px top/bottom offsets.
            var listRect = (RectTransform)menuList;
            listRect.anchorMin = Vector2.zero;
            listRect.anchorMax = Vector2.one;
            listRect.offsetMin = new Vector2(0f, 50f);
            listRect.offsetMax = new Vector2(0f, -50f);

            for (var index = 0; index < entries.Length; index++)
            {
                var capturedIndex = index;
                var item = Instantiate(_textSideMenuButtonPrefab, content, false);
                item.name = $"TextSideMenuButton_{entries[index].Item2}";
                var button = item.GetComponent<Button>();
                var labelRoot = FindDescendant(item.transform, "Label");
                var label = labelRoot != null ? labelRoot.GetComponent<Text>() : null;
                if (button == null || label == null)
                    throw new InvalidOperationException(
                        "Original TextSideMenuButton is incomplete.");
                label.text = entries[index].Item1;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    PlayUiSe(UiSeRuntime.Cue.ButtonGo);
                    SelectSettingsMenu(capturedIndex);
                });
                _settingsMenuLabels[index] = label;
                _settingsMenuButtons[index] = (RectTransform)item.transform;
                _settingsMenuPanelTypes[index] = entries[index].Item2;
            }
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)content);
            SelectSettingsMenu(0);
        }

        private void SelectSettingsMenu(int menuIndex)
        {
            if (_settingsMenuButtons == null ||
                menuIndex < 0 || menuIndex >= _settingsMenuButtons.Length)
                return;
            ShowPanel(_settingsMenuPanelTypes[menuIndex]);
            for (var index = 0; index < _settingsMenuLabels.Length; index++)
            {
                _settingsMenuLabels[index].color = index == menuIndex
                    ? Color.white
                    : new Color32(86, 88, 103, 255);
            }
            if (_settingsScrollRect != null && _settingsScrollRect.content != null)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(
                    _settingsScrollRect.content);
                _settingsScrollRect.StopMovement();
                _settingsScrollRect.verticalNormalizedPosition = 1f;
            }
            if (_settingsSelectedObject != null)
            {
                _settingsSelectedObject.SetAsFirstSibling();
                _settingsSelectedObject.anchoredPosition =
                    _settingsMenuButtons[menuIndex].anchoredPosition +
                    new Vector2(0f, -2f);
            }
        }

        private void ShowPanel(int panel)
        {
            _systemPanel.SetActive(panel == 0);
            _gamePanel.SetActive(panel == 1);
            _detailPanel.SetActive(panel == 2);
            _customPanel.SetActive(panel == 3);
            _soundPanel.SetActive(panel == 4);
            _bluetoothPanel.SetActive(panel == 5);
        }

        private void Save()
        {
            if (_settingsInstance == null || _isRestartConfirmationOpen)
                return;
            if (_selectionHost)
            {
                CloseSelectionSettings(true);
                return;
            }
            // PauseGameDialogBody enters OnRestartConfirmationAsync whenever
            // OnSettingsAsync returns OK. DialogHelper completes and closes the
            // settings dialog before the restart confirmation is presented.
            DestroyGameSimulationPreview();
            DestroyDialog();
            _dialogInstance =
                CreateDialogShell("リトライ", PauseDialogSize, 112);
            _dialogInstance.name = "RestartConfirmationDialog";
            _isRestartConfirmationOpen = true;
            CreateDialogMessage(
                _dialogInstance.transform,
                "設定を反映して最初から公演を始めますか？");
            ConfigureDialogButton(
                _dialogInstance.transform,
                "FirstButtonParent", "FirstButton",
                "キャンセル", ReturnToSettings);
            ConfigureDialogButton(
                _dialogInstance.transform,
                "SecondButtonParent", "SecondButton",
                "反映せずに戻る", DiscardSettings);
            ConfigureDialogButton(
                _dialogInstance.transform,
                "ThirdButtonParent", "ThirdButton",
                "反映してリトライ", ConfirmSettingsAndRestart, true);
        }

        private void ReturnToSettings()
        {
            _isRestartConfirmationOpen = false;
            OpenSettings();
        }

        private void ConfirmSettingsAndRestart()
        {
            var isChanged = _session.HasChanges;
            _isRestartConfirmationOpen = false;
            if (isChanged)
            {
                _store.Save(_session.Current);
                _session.AcceptSavedValues();
                DestroyDialog();
                DestroyTouchBlock();
                if (_pauseInstance != null) _pauseInstance.SetActive(false);
                _gameRuntime.RestartPerformance();
                return;
            }
            OpenPauseMenu();
        }

        private void DiscardSettings()
        {
            _session.Cancel();
            _isRestartConfirmationOpen = false;
            OpenPauseMenu();
        }

        public void Cancel()
        {
            if (_settingsInstance == null) return;
            if (_selectionHost)
            {
                CloseSelectionSettings(false);
                return;
            }
            _session.Cancel();
            OpenPauseMenu();
        }

        public void Resume()
        {
            if (!_gameRuntime.IsPaused || _resumeCoroutine != null) return;
            DestroyDialog();
            DestroyTouchBlock();
            _pauseInstance.SetActive(false);
            _resumeCoroutine = StartCoroutine(ResumeAfterCountDown());
        }

        private void Retry()
        {
            DestroyDialog();
            DestroyTouchBlock();
            if (_pauseInstance != null) _pauseInstance.SetActive(false);
            _gameRuntime.RestartPerformance();
        }

        private void Retire()
        {
            DestroyDialog();
            DestroyTouchBlock();
            _pauseInstance.SetActive(false);
            _gameRuntime.RetireGame();
        }

        private void ShowConfirmation(
            string title,
            string message,
            UnityEngine.Events.UnityAction confirm)
        {
            DestroyDialog();
            _dialogInstance = CreateDialogShell(title, PauseDialogSize);
            CreateDialogMessage(_dialogInstance.transform, message);
            ConfigureDialogButton(
                _dialogInstance.transform, "FirstButtonParent", "FirstButton",
                "戻る", OpenPauseMenu);
            ConfigureDialogButton(
                _dialogInstance.transform, "SecondButtonParent", "SecondButton",
                "決定", confirm, true);
            RequireDescendant(
                _dialogInstance.transform, "ThirdButtonParent").gameObject.SetActive(false);
        }

        private GameObject CreateDialogShell(
            string title,
            Vector2 dialogSize,
            int sortingOrder = 110)
        {
            var shell = Instantiate(_dialogPrefab, _canvasObject.transform, false);
            shell.name = "Dialog";
            // DialogMonoBehaviour.SetOverlayFrontCanvas machine code adds both
            // components, enables override sorting, and uses the literal order
            // 110. This keeps the shared TouchBlock over every HUD graphic while
            // the dialog itself remains interactive in front of the block.
            var frontCanvas = shell.GetComponent<Canvas>();
            if (frontCanvas == null) frontCanvas = shell.AddComponent<Canvas>();
            frontCanvas.overrideSorting = true;
            frontCanvas.sortingOrder = sortingOrder;
            if (shell.GetComponent<GraphicRaycaster>() == null)
                shell.AddComponent<GraphicRaycaster>();
            var shellRect = shell.GetComponent<RectTransform>();
            if (shellRect == null)
                throw new InvalidOperationException(
                    "Original Dialog root RectTransform is missing.");
            shellRect.sizeDelta = dialogSize;
            var shellGroup = shell.GetComponent<CanvasGroup>();
            var bodyGroup = RequireDescendant(shell.transform, "Body")
                .GetComponent<CanvasGroup>();
            if (shellGroup == null || bodyGroup == null)
                throw new InvalidOperationException(
                    "Original Dialog CanvasGroups are missing.");
            // DialogMonoBehaviour.ShowAsync completes both fades at alpha 1.
            // Its stripped presenter is bypassed here, so apply that completed
            // visible state before mounting the recovered body.
            shellGroup.alpha = 1f;
            bodyGroup.alpha = 1f;
            var titleText = RequireDescendant(shell.transform, "TitleText")
                .GetComponent<Text>();
            if (titleText == null)
                throw new InvalidOperationException(
                    "Original Dialog TitleText is missing.");
            titleText.text = title;
            return shell;
        }

        private void CreateTouchBlock()
        {
            DestroyTouchBlock();
            _touchBlockInstance = new GameObject(
                "TouchBlock", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image));
            _touchBlockInstance.transform.SetParent(_canvasObject.transform, false);
            var rect = (RectTransform)_touchBlockInstance.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = _touchBlockInstance.GetComponent<Image>();
            // ColorPreset.BlackTransparent returned by TouchBlock.SetVisible.
            image.color = new Color(0f, 0f, 0f, 0.7f);
            image.raycastTarget = true;
        }

        private System.Collections.IEnumerator ResumeAfterCountDown()
        {
            _resumeCountDownInstance = Instantiate(
                _resumeCountDownPrefab, _canvasObject.transform, false);
            _resumeCountDownInstance.name = "ResumeCountDown";
            // GameResumeTimer.ResumeSeconds and the original animation clip are
            // both exactly three seconds. Realtime preserves this while gameplay
            // and audio remain paused.
            yield return new WaitForSecondsRealtime(3f);
            if (_resumeCountDownInstance != null)
                Destroy(_resumeCountDownInstance);
            _resumeCountDownInstance = null;
            _resumeCoroutine = null;
            _pauseInstance.SetActive(true);
            _gameRuntime.SetPaused(false);
        }

        private void DestroyTouchBlock()
        {
            if (_touchBlockInstance != null) Destroy(_touchBlockInstance);
            _touchBlockInstance = null;
        }

        private void ConfigureDialogButton(
            Transform shell,
            string parentName,
            string buttonName,
            string label,
            UnityEngine.Events.UnityAction action,
            bool isPositive = false)
        {
            var parent = RequireDescendant(shell, parentName);
            parent.gameObject.SetActive(true);
            var buttonRoot = RequireDescendant(parent, buttonName);
            var button = buttonRoot.GetComponent<Button>();
            var text = buttonRoot.GetComponentInChildren<Text>(true);
            if (button == null || text == null)
                throw new InvalidOperationException(
                    $"Original Dialog button {buttonName} is incomplete.");
            text.text = label;
            var image = buttonRoot.GetComponent<Image>();
            if (image == null)
                throw new InvalidOperationException(
                    $"Original Dialog button {buttonName} Image is missing.");
            image.sprite = isPositive
                ? _dialogPositiveButtonSprite
                : _dialogNormalButtonSprite;
            if (isPositive) text.color = Color.white;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                PlayUiSe(isPositive
                    ? UiSeRuntime.Cue.ButtonGo
                    : UiSeRuntime.Cue.ButtonBack);
                action();
            });
        }

        private void PlayUiSe(UiSeRuntime.Cue cue)
        {
            _uiSe?.Play(cue);
        }

        private static void CreateDialogMessage(Transform shell, string message)
        {
            var body = RequireDescendant(shell, "Body");
            var titleText = RequireDescendant(shell, "TitleText").GetComponent<Text>();
            var textObject = new GameObject(
                "ConfirmationMessage", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(body, false);
            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(60f, 20f);
            rect.offsetMax = new Vector2(-60f, -20f);
            var text = textObject.GetComponent<Text>();
            text.text = message;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = titleText != null
                ? titleText.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 32;
            text.color = new Color32(50, 51, 64, 255);
        }

        private void DestroyDialog()
        {
            CloseInformation();
            _isRestartConfirmationOpen = false;
            if (_dialogInstance != null)
            {
                _dialogInstance.SetActive(false);
                Destroy(_dialogInstance);
            }
            _dialogInstance = null;
            _pauseBodyInstance = null;
            _settingsInstance = null;
            _systemPanel = null;
            _gamePanel = null;
            _detailPanel = null;
            _customPanel = null;
            _soundPanel = null;
            _bluetoothPanel = null;
            _settingsSelectedObject = null;
            _settingsMenuLabels = null;
            _settingsMenuButtons = null;
            _settingsMenuPanelTypes = null;
            _settingsScrollRect = null;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root.name == name) return root;
            for (var index = 0; index < root.childCount; index++)
            {
                var found = FindDescendant(root.GetChild(index), name);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindDirectChild(Transform root, string name)
        {
            if (root == null) return null;
            for (var index = 0; index < root.childCount; index++)
            {
                var child = root.GetChild(index);
                if (child.name == name) return child;
            }
            return null;
        }

        private static Transform FindActiveNamedDescendant(
            Transform root, string name)
        {
            Transform fallback = null;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != name) continue;
                if (fallback == null) fallback = child;
                if (child.gameObject.activeSelf) return child;
            }
            return fallback;
        }

        private static Transform RequireDescendant(Transform root, string name)
        {
            var found = FindDescendant(root, name);
            if (found == null)
                throw new InvalidOperationException(
                    $"Original prefab child '{name}' is missing.");
            return found;
        }
    }
}
