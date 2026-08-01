using TMPro;
using UnityEngine;

namespace Sirius.Game.UI
{
    /// <summary>Recovered from ScorePanel ARM64 and the original prefab.</summary>
    public sealed class ScorePanel : MonoBehaviour
    {
        private const long MaxScore = 999999999999L;

        [SerializeField] private TextMeshProUGUI _scoreCount;
        [SerializeField] private TextMeshProUGUI _zeroText;
        [SerializeField] private IncrementScorePanel _incrementScorePanel;
        [SerializeField] private IncrementScorePanel[] _incrementSenseScorePanels;
        [SerializeField] private IncrementScorePanel _incrementStarActScorePanel;
        [SerializeField] private GameObject _autoPlay;
        [SerializeField] private CanvasGroup _panelCanvasGroup;

        private long _score;
        private bool _is12Digits;
        private bool _isDisableScore;

        public long Score => _score;
        public TextMeshProUGUI ScoreCount => _scoreCount;
        public TextMeshProUGUI ZeroText => _zeroText;
        public bool IsAutoPlayVisible => _autoPlay != null && _autoPlay.activeSelf;

        public void Initialize(bool isAutoPlay, bool is12Digits, bool isDisableScore)
        {
            _is12Digits = is12Digits;
            _isDisableScore = isDisableScore;
            _autoPlay.SetActive(isAutoPlay);
            if (_is12Digits)
            {
                _scoreCount.fontSize = 42f;
                var position = _scoreCount.rectTransform.anchoredPosition;
                position.x = -48f;
                _scoreCount.rectTransform.anchoredPosition = position;
            }
            else
            {
                var position = _scoreCount.rectTransform.anchoredPosition;
                position.x = -63f;
                _scoreCount.rectTransform.anchoredPosition = position;
            }

            _incrementScorePanel.Initialize();
            foreach (var panel in _incrementSenseScorePanels) panel.Initialize();
            _incrementStarActScorePanel.Initialize();
            _panelCanvasGroup.alpha = isDisableScore ? 0f : 1f;
            SetScoreCount(0L, false);
        }

        public void SetScoreCount(long score, bool useIncrementScore)
        {
            if (_isDisableScore) return;
            var increment = score - _score;
            if (useIncrementScore && increment >= 1L)
                _incrementScorePanel.FireIncrementScore(increment);
            _score = score;
            var displayed = score < MaxScore ? score : MaxScore;
            var text = displayed.ToString();
            _scoreCount.SetText(text);
            _zeroText.SetText(new string('0', (_is12Digits ? 12 : 11) - text.Length));
        }

        public void SetSenseScoreCount(long addedScore, int index)
        {
            if (index >= 0 && index < _incrementSenseScorePanels.Length)
                _incrementSenseScorePanels[index].FireIncrementScore(addedScore);
        }

        public void SetStarActScoreCount(long addedScore) =>
            _incrementStarActScorePanel.FireIncrementScore(addedScore);

        /// <summary>
        /// The retail loader finishes IncrementScorePanel's sizing animation
        /// before GameView becomes visible. The offline scene initializes the
        /// HUD synchronously, so settle those authored animations explicitly.
        /// </summary>
        public void CompleteInitializationAnimations()
        {
            _incrementScorePanel.CompleteInitialization();
            foreach (var panel in _incrementSenseScorePanels)
                panel.CompleteInitialization();
            _incrementStarActScorePanel.CompleteInitialization();
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public void Show()
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }
    }
}
