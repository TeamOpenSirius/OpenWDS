using UnityEngine;
using UnityEngine.UI;
using System.Globalization;

namespace Sirius.GameResult
{
    public sealed class GameResultRatePanel : RecoveredGameResultPanelBase
    {
        [SerializeField] private Object _notationRate;
        [SerializeField] private Object _playerRate;
        [SerializeField] private Text _notationRateText;
        [SerializeField] private Text _playerRateText;
        [SerializeField] private Text _noRateText;

        public override void Initialize(RecoveredGameResultViewData data)
        {
            base.Initialize(data);
            // GameResultRatePanel.Initialize in the original ARM64 has no AUTO
            // branch. It initializes both rate blocks from GameResultPanelEntity;
            // offline fixtures retain the same path with zero-valued server data.
            if (_notationRateText != null)
                _notationRateText.gameObject.SetActive(true);
            if (_playerRateText != null)
                _playerRateText.gameObject.SetActive(true);
            if (_noRateText != null)
                _noRateText.gameObject.SetActive(false);

            BindRateBlock(
                _notationRateText,
                data.BestEverNotationRate,
                data.ThisTimeNotationRate,
                data.IsNewNotationRate);
            BindRateBlock(
                _playerRateText,
                data.BeforePlayerRate,
                data.AfterPlayerRate,
                data.IsNewPlayerRate);
        }

        private static void BindRateBlock(
            Text dataLabel,
            double previousRate,
            double currentRate,
            bool isNewRecord)
        {
            // DataLabel is nested below the label Image; its grandparent is the
            // NotationRate/PlayerRate block that owns the nested RatePanel.
            var ratePanel = dataLabel != null
                ? dataLabel.transform.parent?.parent?.Find("RatePanel")
                : null;
            if (ratePanel == null) return;

            var current = ratePanel.Find("ThisTimeRate");
            if (current != null)
            {
                current.gameObject.SetActive(true);
                var currentText = current.GetComponent<Text>();
                if (currentText != null)
                {
                    currentText.text = currentRate.ToString(
                        "0.00", CultureInfo.InvariantCulture);
                    // Original GameResultRate applies ColorHelper.RateColor on
                    // every count-up update, including the final value.
                    currentText.color =
                        OpenWDS.Runtime.RecoveredPlayerRating
                            .GetGameResultTextColor(currentRate);
                }
            }
            var previous = ratePanel.Find("PreviousRate");
            if (previous != null)
            {
                // The retail prefab authors this object inactive, then
                // GameResultRate.Initialize/FireAnimationSequence reveals it
                // for a new record.  The recovered result skips that sequence,
                // so leaving the authored state untouched also hides the arrow
                // and makes the rate change impossible to read.
                previous.gameObject.SetActive(isNewRecord);
                var previousGroup = previous.GetComponent<CanvasGroup>();
                if (previousGroup != null) previousGroup.alpha = 1f;
                var previousText = previous.GetComponent<Text>();
                if (previousText != null)
                {
                    previousText.text = previousRate.ToString(
                        "0.00", CultureInfo.InvariantCulture);
                    previousText.color =
                        OpenWDS.Runtime.RecoveredPlayerRating
                            .GetGameResultTextColor(previousRate);
                }
            }
            var arrow = ratePanel.Find("PreviousRate/Arrow");
            if (arrow != null) arrow.gameObject.SetActive(isNewRecord);
            var notRate = ratePanel.Find("NotRateText");
            if (notRate != null) notRate.gameObject.SetActive(false);
            var notTarget = ratePanel.Find("NotRateTargetText");
            if (notTarget != null) notTarget.gameObject.SetActive(false);
        }
    }
}
