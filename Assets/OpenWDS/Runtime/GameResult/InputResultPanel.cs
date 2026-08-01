using System;
using OpenWDS.Runtime;
using TMPro;
using UnityEngine;

namespace Sirius.GameResult
{
    public sealed class InputResultPanel : RecoveredGameResultPanelBase
    {
        [Serializable]
        private struct TimingResultReference
        {
            [SerializeField] internal RecoveredTimingType _key;
            [SerializeField] internal TextMeshProUGUI _value;
        }

        [SerializeField] private GameObject _perfectStar;
        [SerializeField] private TimingResultReference[] _timingResults;

        public override void Initialize(RecoveredGameResultViewData data)
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
