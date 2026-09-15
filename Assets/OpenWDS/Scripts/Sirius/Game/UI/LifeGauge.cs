using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sirius.Game.UI
{
    /// <summary>Recovered from Sirius.Game.UI.LifeGauge ARM64.</summary>
    public class LifeGauge : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _currentLifeCount;
        [SerializeField] private TextMeshProUGUI _currentLifeGuardCount;
        [SerializeField] private Slider _gauge;
        [SerializeField] private Image _fillImage;
        [SerializeField] private Image _lifeImage;
        [SerializeField] private Image _lifeRingImage;
        [SerializeField] private Image _lifeLabelImage;
        [SerializeField] private Sprite _defaultHeartSprite;
        [SerializeField] private Sprite _lifeGuardSprite;
        [SerializeField] private Sprite _hotIconSprite;
        [SerializeField] private Sprite _courseIconSprite;
        [SerializeField] private Sprite _hotBarSprite;
        [SerializeField] private Sprite _courseBarSprite;
        [SerializeField] private Sprite _hotRingSprite;
        [SerializeField] private Sprite _courseRingSprite;
        [SerializeField] private Sprite _hotLabelSprite;
        [SerializeField] private Sprite _courseLabelSprite;

        private static readonly Color32 DangerColor = new Color32(235, 67, 64, 255);
        private static readonly Color32 OverLifeColor = new Color32(254, 224, 117, 255);
        private readonly char[] _chars = new char[8];
        private bool _isCourse;

        public TextMeshProUGUI CurrentLifeCount => _currentLifeCount;
        public TextMeshProUGUI CurrentLifeGuardCount => _currentLifeGuardCount;
        public Slider Gauge => _gauge;
        public Image FillImage => _fillImage;

        protected void Awake()
        {
            if (_chars.Length != 0) _chars[0] = '+';
        }

        public void SetLifeValue(int lifeValue, int maxLifeValue, int lifeGuardCount)
        {
            _currentLifeCount.SetText(lifeValue.ToString());
            SetLifeGuardText(lifeGuardCount);
            _gauge.maxValue = maxLifeValue;
            _gauge.minValue = 0f;
            _gauge.value = lifeValue;
            SetColor(lifeValue, maxLifeValue);
            SetLifeImage(lifeGuardCount);
        }

        public void SetCourse(bool isCourse, bool isHot)
        {
            _isCourse = isCourse;
            if (!isCourse) return;
            _lifeImage.sprite = isHot ? _hotIconSprite : _courseIconSprite;
            _lifeRingImage.sprite = isHot ? _hotRingSprite : _courseRingSprite;
            _lifeLabelImage.sprite = isHot ? _hotLabelSprite : _courseLabelSprite;
            _fillImage.sprite = isHot ? _hotBarSprite : _courseBarSprite;
            _fillImage.SetNativeSize();
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void SetLifeImage(int lifeGuardCount)
        {
            if (_isCourse) return;
            _lifeImage.sprite = lifeGuardCount > 0
                ? _lifeGuardSprite
                : _defaultHeartSprite;
        }

        private void SetLifeGuardText(int lifeGuardCount)
        {
            if (lifeGuardCount < 1)
            {
                _currentLifeGuardCount.SetCharArray(_chars, 0, 0);
                return;
            }
            var digitCount = (int)Mathf.Log10(lifeGuardCount) + 1;
            var remaining = lifeGuardCount;
            for (var index = digitCount; index > 0; index--)
            {
                _chars[index] = (char)('0' + remaining % 10);
                remaining /= 10;
            }
            _currentLifeGuardCount.SetCharArray(_chars, 0, digitCount + 1);
        }

        private void SetColor(int lifeValue, int maxLifeValue)
        {
            Color32 textColor;
            Color32 fillColor;
            if (lifeValue > maxLifeValue)
            {
                textColor = OverLifeColor;
                fillColor = Color.white;
            }
            else if ((int)(maxLifeValue * 0.2f) >= lifeValue)
            {
                textColor = DangerColor;
                fillColor = DangerColor;
            }
            else
            {
                textColor = Color.white;
                fillColor = Color.white;
            }
            _currentLifeCount.color = textColor;
            if (!_isCourse) _fillImage.color = fillColor;
        }
    }
}
