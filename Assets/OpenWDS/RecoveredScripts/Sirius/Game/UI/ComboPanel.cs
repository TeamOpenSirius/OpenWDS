using UnityEngine;
using UnityEngine.UI;

namespace Sirius.Game.UI
{
    public enum RecoveredComboType
    {
        None = 0,
        FullCombo = 1,
        AllPerfect = 2,
    }

    /// <summary>Original ComboPanel digit and animation behavior.</summary>
    public sealed class ComboPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Animator _animator;
        [SerializeField] private Image _comboLabelImage;
        [SerializeField] private Image[] _countDigitImages;
        [SerializeField] private Image[] _countDigitImagesPer100;
        [SerializeField] private CanvasGroup _countDigitPer100CanvasGroup;

        private static readonly int OnFireHash = Animator.StringToHash("OnFire");
        private static readonly int OnCountHash = Animator.StringToHash("OnCount");
        private static readonly float[][] DigitPositionXs =
        {
            null,
            new[] { -138f },
            new[] { -92f, -184f },
            new[] { -46f, -138f, -230f },
            new[] { 0f, -92f, -184f, -276f },
        };

        private Sprite[][] _counts;
        private Sprite[] _labels;
        private RectTransform[] _digitRects;
        private RectTransform[] _digitPer100Rects;
        private int _combo;
        private RecoveredComboType _comboType;

        public int ComboCount => _combo;
        public RecoveredComboType ComboType => _comboType;

        public void Initialize(Sprite[][] counts, Sprite[] labels)
        {
            _counts = counts;
            _labels = labels;
            _digitRects = GetRects(_countDigitImages);
            _digitPer100Rects = GetRects(_countDigitImagesPer100);
            _canvasGroup.alpha = 0f;
            _countDigitPer100CanvasGroup.alpha = 0f;
            SetComboDigitImages(_countDigitImages, _digitRects, 0,
                RecoveredComboType.None);
        }

        public void SetComboCount(int comboCount, RecoveredComboType comboType)
        {
            var previousHundreds = _combo / 100;
            var currentHundreds = comboCount / 100;
            if (previousHundreds != currentHundreds)
            {
                SetComboDigitImages(
                    _countDigitImagesPer100, _digitPer100Rects,
                    currentHundreds * 100, comboType);
                _countDigitPer100CanvasGroup.alpha = 1f;
                _animator.SetTrigger(OnFireHash);
            }

            _combo = comboCount;
            _comboType = comboType;
            _canvasGroup.alpha = comboCount > 0 ? 1f : 0f;
            SetComboDigitImages(_countDigitImages, _digitRects, comboCount, comboType);
            _animator.SetTrigger(OnCountHash);
        }

        private void SetComboDigitImages(
            Image[] images, RectTransform[] rects, int comboCount,
            RecoveredComboType comboType)
        {
            var digitCount = comboCount < 1
                ? 1
                : Mathf.FloorToInt(Mathf.Log10(comboCount)) + 1;
            var positions = DigitPositionXs[Mathf.Clamp(digitCount, 1, 4)];
            var remaining = comboCount;
            var spriteSet = _counts[(int)comboType];
            for (var index = 0; index < images.Length; index++)
            {
                if (index >= digitCount)
                {
                    // Original ColorPreset.WhiteTransparent.  Keeping the assigned
                    // sprite while hiding the slot also avoids Image's solid-white
                    // no-sprite rendering path.
                    images[index].color = new Color(1f, 1f, 1f, 0f);
                    continue;
                }
                var digit = remaining % 10;
                remaining /= 10;
                images[index].sprite = spriteSet[digit];
                images[index].color = Color.white;
                var position = rects[index].anchoredPosition;
                position.x = positions[index];
                rects[index].anchoredPosition = position;
            }
            _comboLabelImage.sprite = _labels[(int)comboType];
        }

        private static RectTransform[] GetRects(Image[] images)
        {
            var result = new RectTransform[images.Length];
            for (var index = 0; index < images.Length; index++)
                result[index] = (RectTransform)images[index].transform;
            return result;
        }
    }
}
