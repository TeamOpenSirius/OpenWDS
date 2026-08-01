using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Sirius.Game.UI
{
    public enum RecoveredAchievementRateSettingType
    {
        Off = 0,
        Subtract = 1,
        Add = 2,
    }

    /// <summary>Recovered from AchievementRatePanel ARM64 and the original prefab.</summary>
    public sealed class AchievementRatePanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Animator _animator;
        [SerializeField] private Image[] _rateImages;
        [SerializeField] private Image[] _ratePer100Images;
        [SerializeField] private CanvasGroup _countDigitPer100CanvasGroup;
        [SerializeField] private GameObject _comboCount;
        [SerializeField] private GameObject _apStar;

        private static readonly int OnFireHash = Animator.StringToHash("OnFire");
        private static readonly int OnCountHash = Animator.StringToHash("OnCount");
        private static readonly Color WhiteTransparent = new Color(1f, 1f, 1f, 0f);

        private Sprite[][] _rateSprites;
        private Sprite[] _pointSprites;
        private Sprite[] _percentSprites;
        private RecoveredAchievementRateSettingType _setting;
        private bool _isAnimation;
        private bool _isAuto;
        private bool _isPerfectContinuous;

        public double Rate { get; private set; }
        public string FormattedRate { get; private set; }
        public bool IsAnimationEnabled => _isAnimation;
        public bool IsVisible => gameObject.activeSelf &&
                                 _canvasGroup.alpha > 0f &&
                                 _comboCount != null &&
                                 _comboCount.activeSelf;
        public bool IsApStarVisible => _apStar != null && _apStar.activeSelf;
        public Image[] RateImages => _rateImages;

        public void Initialize(
            Sprite[][] rateSprites,
            Sprite[] pointSprites,
            Sprite[] percentSprites,
            RecoveredAchievementRateSettingType setting,
            bool isAuto,
            bool isMultiRoomCourse,
            bool isAnimation = false,
            bool isPerfectContinuous = true)
        {
            if (rateSprites == null || rateSprites.Length != 4 ||
                pointSprites == null || pointSprites.Length != 4 ||
                percentSprites == null || percentSprites.Length != 4)
                throw new ArgumentException("AchievementRate GameConfig sprites are incomplete.");
            for (var index = 0; index < rateSprites.Length; index++)
                if (rateSprites[index] == null || rateSprites[index].Length != 10)
                    throw new ArgumentException("AchievementRate digit sprites are incomplete.");

            _rateSprites = rateSprites;
            _pointSprites = pointSprites;
            _percentSprites = percentSprites;
            _setting = setting;
            _isAuto = isAuto;
            _isAnimation = isAnimation;
            _isPerfectContinuous = isPerfectContinuous;
            // ARM64 InitializeAsync does not disable this component root. It
            // independently gates the normal number group and its animator
            // flash duplicate. Multi-room forces the rate display on, while
            // solo Auto hides it even when the local setting is enabled.
            var showRate = (setting != RecoveredAchievementRateSettingType.Off &&
                            !isAuto) ||
                           isMultiRoomCourse;
            gameObject.SetActive(true);
            if (_comboCount != null) _comboCount.SetActive(showRate);
            if (_countDigitPer100CanvasGroup != null)
                _countDigitPer100CanvasGroup.gameObject.SetActive(showRate);
            OnReset();
        }

        public void OnReset()
        {
            var initial = _setting == RecoveredAchievementRateSettingType.Subtract
                ? 101d
                : 0d;
            SetCount(0, RecoveredComboType.None, initial, true, false);
        }

        public void SetCount(
            int comboCount,
            RecoveredComboType comboType,
            double achievementRate,
            bool isMiss,
            bool isAllPerfect)
        {
            if (!gameObject.activeSelf) return;
            var showApStar = _isPerfectContinuous &&
                             isAllPerfect &&
                             !_isAuto;
            if (_apStar.activeSelf != showApStar) _apStar.SetActive(showApStar);

            _canvasGroup.alpha = 1f;
            Rate = achievementRate;
            var style = isMiss ? 3 : (int)comboType;
            SetDigitImages(_rateImages, style, achievementRate);
            // AchievementRatePanel.SetCount contains this dormant OnCount
            // branch, but InitializeAsync writes _isAnimation=false. The
            // shared Combo controller must not make the rate bounce.
            if (_isAnimation) _animator.SetTrigger(OnCountHash);
        }

        private void SetDigitImages(Image[] images, int style, double achievementRate)
        {
            style = Mathf.Clamp(style, 0, 3);
            images[0].sprite = _percentSprites[style];
            images[0].color = Color.white;
            var formatted = FormatRate(achievementRate);
            FormattedRate = formatted;
            var imageIndex = 1;
            for (var charIndex = formatted.Length - 1;
                 charIndex >= 0 && imageIndex < images.Length;
                 charIndex--, imageIndex++)
            {
                var character = formatted[charIndex];
                images[imageIndex].sprite = char.IsDigit(character)
                    ? _rateSprites[style][character - '0']
                    : _pointSprites[style];
                images[imageIndex].color = Color.white;
            }
            for (; imageIndex < images.Length; imageIndex++)
                images[imageIndex].color = WhiteTransparent;
        }

        public static string FormatRate(double achievementRate)
        {
            return achievementRate.ToString(
                "0.0000", CultureInfo.InvariantCulture);
        }
    }
}
