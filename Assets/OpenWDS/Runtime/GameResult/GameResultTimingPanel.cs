using UnityEngine;
using UnityEngine.UI;

namespace Sirius.GameResult
{
    public sealed class GameResultTimingPanel : RecoveredGameResultPanelBase
    {
        [SerializeField] private Text _recommendationTimingText;
        [SerializeField] private Text _currentRecommendationTimingText;

        public override void Initialize(RecoveredGameResultViewData data)
        {
            base.Initialize(data);
            if (_recommendationTimingText != null)
                _recommendationTimingText.text = Format(data.RecommendationTiming);
            if (_currentRecommendationTimingText != null)
                _currentRecommendationTimingText.text =
                    "（現在 " + Format(data.CurrentRecommendationTiming) + "）";
        }

        internal static string Format(double value) =>
            value.ToString("0.0",
                System.Globalization.CultureInfo.InvariantCulture);
    }
}
