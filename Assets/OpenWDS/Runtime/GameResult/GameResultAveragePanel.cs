using TMPro;
using UnityEngine.UI;

namespace Sirius.GameResult
{
    public sealed class GameResultAveragePanel : GameResultPanelBase
    {
        [UnityEngine.SerializeField] private TextMeshProUGUI _thisTimeRateText;
        [UnityEngine.SerializeField] private Slider _thisTimeRateGage;
        [UnityEngine.SerializeField] private TextMeshProUGUI _allThisTimeRateText;
        [UnityEngine.SerializeField] private Slider _allThisTimeRateGage;
        [UnityEngine.SerializeField] private Text _descriptionText;

        public override void Initialize(GameResultViewData data)
        {
            base.Initialize(data);
            if (_thisTimeRateGage != null)
                _thisTimeRateGage.value = (float)data.AchievementRate;
            if (_allThisTimeRateGage != null)
                _allThisTimeRateGage.value = (float)data.TotalAchievementRate;
            if (_thisTimeRateText != null)
                _thisTimeRateText.text = FormatAchievementRate(data.AchievementRate);
            if (_allThisTimeRateText != null)
                _allThisTimeRateText.text = FormatAchievementRate(data.TotalAchievementRate);
        }

        // DoubleExtensions.ToStringForAchievementRate(value, "%", 24): its
        // ARM64 path formats F2, splits the final two digits, and feeds four
        // values (prefix/fontSize/fraction/unit) to the TMP rich-text template.
        internal static string FormatAchievementRate(double value)
        {
            var text = value.ToString(
                "F2", System.Globalization.CultureInfo.InvariantCulture);
            return text.Substring(0, text.Length - 2) +
                   "<size=24>" + text.Substring(text.Length - 2) + "%</size>";
        }
    }
}
