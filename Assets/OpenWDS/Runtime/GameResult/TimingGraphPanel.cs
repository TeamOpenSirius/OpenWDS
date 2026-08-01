using System.Collections.Generic;
using System.Linq;
using OpenWDS.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Sirius.GameResult
{
    public sealed class TimingGraphPanel : RecoveredGameResultPanelBase
    {
        [SerializeField] private GameObject _content;
        [SerializeField] private Transform _plusParent;
        [SerializeField] private Transform _minusParent;
        [SerializeField] private Text _fast;
        [SerializeField] private Text _slow;
        private IEnumerable<KeyValuePair<int, int>> inputResults;

        public override void Initialize(RecoveredGameResultViewData data)
        {
            base.Initialize(data);
            inputResults = data.InputDiffSummary;
            ClearGenerated(_plusParent);
            ClearGenerated(_minusParent);

            var plus = OrderPositiveBuckets(inputResults);
            var minus = OrderNegativeBuckets(inputResults);
            var maxCount = inputResults.Any()
                ? Mathf.Max(1, inputResults.Max(value => value.Value))
                : 1;
            CreateGraphContent(plus, _plusParent, maxCount);
            CreateGraphContent(minus, _minusParent, maxCount);

            // Original predicates multiply the 5 ms bucket by five and exclude
            // PERFECT_STAR's |diff| <= 25 ms range from FAST/SLOW totals.
            if (_fast != null)
                _fast.text = minus.Where(value => Mathf.Abs(value.Key * 5) > 25)
                    .Sum(value => value.Value).ToString();
            if (_slow != null)
                _slow.text = plus.Where(value => Mathf.Abs(value.Key * 5) > 25)
                    .Sum(value => value.Value).ToString();
        }

        public static KeyValuePair<int, int>[] OrderPositiveBuckets(
            IEnumerable<KeyValuePair<int, int>> values)
        {
            return values.Where(value => value.Key > 0)
                .OrderBy(value => value.Key).ToArray();
        }

        public static KeyValuePair<int, int>[] OrderNegativeBuckets(
            IEnumerable<KeyValuePair<int, int>> values)
        {
            // Retail b10_4 filters key < 0 and b10_5 returns key unchanged to
            // OrderBy. The most-negative bucket is therefore created first.
            return values.Where(value => value.Key < 0)
                .OrderBy(value => value.Key).ToArray();
        }

        private void CreateGraphContent(
            IEnumerable<KeyValuePair<int, int>> values,
            Transform parent,
            int maxCount)
        {
            if (_content == null || parent == null) return;
            foreach (var pair in values)
            {
                var bar = Instantiate(_content, parent);
                bar.name = "Timing_" + (pair.Key * 5);
                bar.SetActive(true);
                var image = bar.GetComponent<Image>();
                if (image == null) image = bar.GetComponentInChildren<Image>(true);
                if (image == null) continue;
                var size = image.rectTransform.sizeDelta;
                // Retail's MaxBlockCount is a minimum scale denominator. Sparse
                // samples must not be stretched to full height, which previously
                // made a handful of edge buckets look like a bimodal wall.
                size.y = Mathf.Min(
                    180f,
                    pair.Value * 180f / Mathf.Max(50, maxCount));
                image.rectTransform.sizeDelta = size;
                image.color = TimingColor(RecoveredTapTimingDecider.DecideMusicTime(
                    pair.Key * 5L, 0).TimingType);
            }
            _content.SetActive(false);
        }

        private void ClearGenerated(Transform parent)
        {
            if (parent == null) return;
            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                var child = parent.GetChild(index);
                if (child.gameObject != _content) Destroy(child.gameObject);
            }
        }

        private static Color TimingColor(RecoveredTimingType timingType)
        {
            switch (timingType)
            {
                case RecoveredTimingType.PerfectStar: return new Color32(255, 103, 157, 255);
                case RecoveredTimingType.Perfect: return new Color32(255, 157, 196, 255);
                case RecoveredTimingType.Great: return new Color32(255, 235, 13, 255);
                case RecoveredTimingType.Good: return new Color32(0, 195, 220, 255);
                default: return new Color32(134, 103, 233, 255);
            }
        }
    }
}
