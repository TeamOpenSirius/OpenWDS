using System;
using OpenWDS.Runtime;
using TMPro;
using DG.Tweening;
using System.Linq;
using UnityEngine;

namespace Sirius.GameResult
{
    public sealed class InputResultPanel : GameResultPanelBase
    {
        [Serializable]
        private struct TimingResultReference
        {
            [SerializeField] internal TimingType _key;
            [SerializeField] internal TextMeshProUGUI _value;
        }

        [SerializeField] private GameObject _perfectStar;
        [SerializeField] private TimingResultReference[] _timingResults;

        internal Sequence CreateCountUp(GameResultViewData data)
        {
            // FireAnimationAsync: descending TimingTypes; skip zero counts;
            // await each 0.3s linear integer tween before the next row.
            var sequence = DOTween.Sequence();
            foreach (var result in _timingResults.OrderByDescending(item => (int)item._key))
            {
                if (result._value == null) continue;
                var label = result._value;
                label.text = ZeroPaddingWithGray(0, 4);
                if (!data.ShouldShowPerfectStar && result._key == TimingType.PerfectStar)
                    continue;
                var target = data.GetDisplayedTimingCount(result._key);
                if (target == 0) continue;
                var value = 0;
                sequence.Append(DOTween.To(() => value, current =>
                {
                    value = current;
                    label.text = ZeroPaddingWithGray(current, 4);
                }, target, 0.3f).SetEase(Ease.Linear));
            }
            return sequence;
        }

        public override void Initialize(GameResultViewData data)
        {
            base.Initialize(data);
            if (_perfectStar != null)
                _perfectStar.SetActive(data.ShouldShowPerfectStar);
            if (_timingResults == null) return;
            foreach (var result in _timingResults)
            {
                if (result._value != null)
                    result._value.text = ZeroPaddingWithGray(
                        data.GetDisplayedTimingCount(result._key), 4);
            }
        }

        internal static string ZeroPaddingWithGray(int value, int digits)
        {
            var text = value.ToString();
            var zeroCount = System.Math.Max(0, digits - text.Length);
            return zeroCount == 0
                ? text
                : "<color=#AEB0C0FF>" + new string('0', zeroCount) + "</color>" + text;
        }
    }
}
