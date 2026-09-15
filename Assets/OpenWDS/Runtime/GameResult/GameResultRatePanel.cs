using UnityEngine;
using UnityEngine.UI;
using System.Globalization;
using DG.Tweening;

namespace Sirius.GameResult
{
    public sealed class GameResultRatePanel : GameResultPanelBase
    {
        [SerializeField] private Object _notationRate;
        [SerializeField] private Object _playerRate;
        [SerializeField] private Text _notationRateText;
        [SerializeField] private Text _playerRateText;
        [SerializeField] private Text _noRateText;

        public override void Initialize(GameResultViewData data)
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
                        OpenWDS.Runtime.PlayerRating
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
                        OpenWDS.Runtime.PlayerRating
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

        internal Sequence CreateCountUp(GameResultViewData data, System.Action completion)
        {
            // GameResultRatePanel joins the two independent rate sequences.
            return DOTween.Sequence()
                .Join(CreateRateCountUp(_notationRateText, data.ThisTimeNotationRate,
                    data.IsNewNotationRate, completion))
                .Join(CreateRateCountUp(_playerRateText, data.AfterPlayerRate,
                    data.IsNewPlayerRate, completion));
        }

        private static Sequence CreateRateCountUp(Text label, double target,
            bool isNewRecord, System.Action completion)
        {
            var panel = label.transform.parent.parent.Find("RatePanel");
            var current = panel.Find("ThisTimeRate").GetComponent<Text>();
            var previous = panel.Find("PreviousRate");
            var previousGroup = previous.GetComponent<CanvasGroup>();
            var value = 0d;
            current.text = "0.00";
            current.color = OpenWDS.Runtime.PlayerRating.GetGameResultTextColor(0d);
            if (isNewRecord)
            {
                previousGroup.alpha = 0f;
                // Original prefab starts at its authored X; FireAnimationSequence
                // moves PreviousRate to -198 after the numeric tween completes.
            }
            var sequence = DOTween.Sequence();
            sequence.Append(DOTween.To(() => value, updated =>
            {
                value = updated;
                current.text = updated.ToString("0.00", CultureInfo.InvariantCulture);
                current.color = OpenWDS.Runtime.PlayerRating.GetGameResultTextColor(updated);
            }, target, 0.3f).SetEase(Ease.Linear));
            sequence.AppendCallback(() => completion?.Invoke());
            if (isNewRecord)
            {
                sequence.Append(previousGroup.DOFade(1f, 0.1f));
                sequence.Join(previous.DOLocalMoveX(-198f, 0.2f));
            }
            return sequence;
        }
    }
}
