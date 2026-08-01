using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Sirius.GameResult
{
    public sealed class GameResultPanel : RecoveredGameResultPanelBase
    {
        public enum PanelType
        {
            TimingCount = 0,
            TimingGraph = 1,
        }

        [SerializeField] private UnityEngine.Object _nextRewardPanel;
        [SerializeField] private GameResultAveragePanel _averagePanel;
        [SerializeField] private InputResultPanel _inputResultPanel;
        [SerializeField] private GameResultComboPanel _comboPanel;
        [SerializeField] private TimingGraphPanel _timingGraphPanel;
        [SerializeField] private GameResultRatePanel _ratePanel;
        [SerializeField] private GameResultTimingPanel _timingPanel;
        [SerializeField] private UnityEngine.Object _tournamentScorePanel;
        [SerializeField] private UnityEngine.Object _modeChangeButton;
        [SerializeField] private CanvasGroup _resultPanelCanvasGroup;
        [SerializeField] private CanvasGroup _autoHiddenPanelCanvasGroup;
        private CanvasGroup _nextRewardCanvasGroup;
        private Button _recoveredModeChangeButton;
        private RecoveredGameResultViewData _data;
        private PanelType _panelType;

        public GameResultAveragePanel AveragePanel => _averagePanel;
        public InputResultPanel InputResultPanel => _inputResultPanel;
        public GameResultComboPanel ComboPanel => _comboPanel;
        public TimingGraphPanel TimingGraphPanel => _timingGraphPanel;
        public GameResultRatePanel RatePanel => _ratePanel;
        public GameResultTimingPanel TimingPanel => _timingPanel;
        public PanelType CurrentPanelType => _panelType;

        public override void Initialize(RecoveredGameResultViewData data)
        {
            base.Initialize(data);
            _data = data;
            // GameResultView.ShowAsync normally fades every outer CanvasGroup in.
            // The recovered view does not retain that server-facing component.
            foreach (var parentGroup in GetComponentsInParent<CanvasGroup>(true))
                SetCanvas(parentGroup, true);
            _averagePanel?.Initialize(data);
            _inputResultPanel?.Initialize(data);
            _comboPanel?.Initialize(data);
            _timingGraphPanel?.Initialize(data);
            _ratePanel?.Initialize(data);
            _timingPanel?.Initialize(data);

            var nextReward = transform.Find(
                "ResultPanel /HeaderPanel/NextRewardPanel");
            if (nextReward != null)
            {
                _nextRewardCanvasGroup = nextReward.GetComponent<CanvasGroup>();
                BindNextReward(nextReward, data);
            }
            var modeButton = transform.Find("ResultPanel /ModeChangeButton");
            if (modeButton != null)
            {
                _recoveredModeChangeButton =
                    modeButton.GetComponentInChildren<Button>(true);
                if (_recoveredModeChangeButton != null)
                {
                    _recoveredModeChangeButton.onClick.RemoveListener(ChangePanel);
                    _recoveredModeChangeButton.onClick.AddListener(ChangePanel);
                }
            }

            _panelType = PanelType.TimingCount;
            ApplyPresentation();
        }

        public void ChangePanel()
        {
            _panelType = _panelType == PanelType.TimingCount
                ? PanelType.TimingGraph
                : PanelType.TimingCount;
            ApplyPanelType();
        }

        private void ApplyPresentation()
        {
            var showDetailed = !_data.IsAuto;
            SetCanvas(_resultPanelCanvasGroup, showDetailed);
            SetCanvas(_autoHiddenPanelCanvasGroup, !showDetailed);
            if (_recoveredModeChangeButton != null)
                _recoveredModeChangeButton.gameObject.SetActive(showDetailed);
            if (showDetailed) ApplyPanelType();
        }

        private void ApplyPanelType()
        {
            var graph = _panelType == PanelType.TimingGraph;
            SetCanvas(_nextRewardCanvasGroup, !graph);
            _averagePanel?.SetVisible(graph);
            _inputResultPanel?.SetVisible(true);
            _comboPanel?.SetVisible(!graph);
            _timingGraphPanel?.SetVisible(graph);
            _ratePanel?.SetVisible(!graph);
            _timingPanel?.SetVisible(graph);
        }

        private static void SetCanvas(CanvasGroup canvasGroup, bool visible)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private static void BindNextReward(
            Transform root,
            RecoveredGameResultViewData data)
        {
            var thisTimeRate = root.Find("ThisTimeRate");
            var rateText = thisTimeRate != null
                ? thisTimeRate.GetComponent<Text>()
                : null;
            if (rateText != null)
                rateText.text = data.AchievementRate.ToString(
                    "0.0000", CultureInfo.InvariantCulture) + "%";

            var slider = root.Find("RewardGage")?.GetComponent<Slider>();
            if (slider != null)
            {
                var grade = GetAchievementGrade(data.AchievementRate);
                var lower = grade <= 0 ? 0f : GradeRates[grade - 1];
                var upper = grade >= GradeRates.Length
                    ? 101f
                    : GradeRates[grade];
                slider.normalizedValue = Mathf.Approximately(lower, upper)
                    ? 1f
                    : Mathf.Clamp01(
                        ((float)data.AchievementRate - lower) /
                        (upper - lower));
            }

            var newRecord = root.Find("NewRecord");
            if (newRecord != null)
            {
                newRecord.gameObject.SetActive(data.IsNewAchievementRate);
                // ARM64 GameResultNextRewardPanel.SetRateText writes
                // thisTimeAchievementRate - bestEverAchievementRate to
                // _newRecordRate. The retail prefab uses a full-width percent
                // glyph, so looking for ASCII '%' left its 999.99 placeholder.
                var newRecordRate =
                    newRecord.GetComponentInChildren<Text>(true);
                if (newRecordRate != null)
                    newRecordRate.text = Math.Max(
                            0d,
                            data.AchievementRate -
                            data.BestEverAchievementRate)
                        .ToString(
                            "0.00", CultureInfo.InvariantCulture) + "％";
            }

            var gradeRoot = root.Find("ThisTimeRate/Badge/Grade");
            if (gradeRoot != null)
            {
                var grade = GetAchievementGrade(data.AchievementRate);
                var selected = GradeNames[grade];
                for (var index = 0; index < gradeRoot.childCount; index++)
                {
                    var child = gradeRoot.GetChild(index);
                    child.gameObject.SetActive(child.name == selected);
                }
            }
        }

        private static readonly float[] GradeRates =
            { 80f, 90f, 95f, 98f, 100f, 100.25f, 100.5f, 100.75f, 100.95f };

        private static readonly string[] GradeNames =
            { "None", "C", "B", "A", "A+", "S", "S+", "SS", "SS+", "SSS" };

        internal static int GetAchievementGrade(double rate)
        {
            var grade = 0;
            for (var index = 0; index < GradeRates.Length; index++)
            {
                if (rate < GradeRates[index]) break;
                grade = index + 1;
            }
            return grade;
        }

    }
}
