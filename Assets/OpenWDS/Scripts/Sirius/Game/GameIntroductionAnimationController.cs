using System;
using Sirius.Animations;
using UnityEngine;
using UnityEngine.UI;

namespace Sirius.Game
{
    [RequireComponent(typeof(AnimationExitTrigger))]
    public sealed class GameIntroductionAnimationController : MonoBehaviour
    {
        [SerializeField] private Image _jacketImage;
        [SerializeField] private Animator _animator;
        [SerializeField] private GameObject[] _difficultyTexts;
        [SerializeField] private Text _musicNameText;
        [SerializeField] private Text _musicInfoText;
        [SerializeField] private GameObject _categoryObject;
        [SerializeField] private Image _categoryImage;
        [SerializeField] private Text _categoryText;
        [SerializeField] private CategorySprite[] _categorySprites;

        private AnimationExitTrigger _animationExitTrigger;
        private bool _applicationPaused;
        private bool _applicationUnfocused;

        public bool IsPlaying { get; private set; }
        public string MusicName => _musicNameText != null
            ? _musicNameText.text : string.Empty;
        public string MusicInfo => _musicInfoText != null
            ? _musicInfoText.text : string.Empty;
        public Sprite Jacket => _jacketImage != null
            ? _jacketImage.sprite : null;
        public bool IsUnlockOlivier { get; private set; }
        public int SelectedDifficultyIndex { get; private set; } = -1;
        public bool IsNormalAnimationPlaying =>
            _animator != null && _animator.GetCurrentAnimatorStateInfo(0)
                .IsName("GameIntroduction_nomal_anim");
        public bool IsUnlockAnimationPlaying =>
            _animator != null && _animator.GetCurrentAnimatorStateInfo(0)
                .IsName("GameIntroduction_difficultyChage_anim");
        public float NormalizedTime => _animator != null
            ? _animator.GetCurrentAnimatorStateInfo(0).normalizedTime
            : 0f;
        public bool IsAnimationPaused =>
            _animator != null && Mathf.Approximately(_animator.speed, 0f);

        public static float GetAuthoredDuration(bool isUnlockOlivier)
        {
            return isUnlockOlivier ? 15.083333f : 6.0833335f;
        }

        [Serializable]
        private struct CategorySprite
        {
            [SerializeField] private int _key;
            [SerializeField] private Sprite _value;
        }

        public void Initialize(
            Sprite jacket, string musicName, string musicInfo)
        {
            if (_jacketImage != null)
            {
                _jacketImage.sprite = jacket;
                _jacketImage.enabled = jacket != null;
            }
            if (_musicNameText != null) _musicNameText.text = musicName ?? string.Empty;
            if (_musicInfoText != null) _musicInfoText.text = musicInfo ?? string.Empty;
            if (_categoryObject != null) _categoryObject.SetActive(false);
            if (_difficultyTexts != null)
            {
                for (var index = 0; index < _difficultyTexts.Length; index++)
                    if (_difficultyTexts[index] != null)
                        _difficultyTexts[index].SetActive(false);
            }
            _animationExitTrigger = GetComponent<AnimationExitTrigger>();
        }

        public void Play(int difficultyIndex, bool isUnlockOlivier)
        {
            gameObject.SetActive(true);
            if (_animator == null)
                throw new InvalidOperationException(
                    "GameIntroduction Animator is missing.");
            if (_difficultyTexts == null ||
                difficultyIndex < 0 ||
                difficultyIndex >= _difficultyTexts.Length)
                throw new ArgumentOutOfRangeException(nameof(difficultyIndex));

            // Original PlayAsync first Rebinds, then enables difficulty - 1.
            // Its special animation is selected only for Olivier (enum value 5)
            // together with the first-unlock flag; the flag alone is insufficient.
            _animator.Rebind();
            for (var index = 0; index < _difficultyTexts.Length; index++)
                if (_difficultyTexts[index] != null)
                    _difficultyTexts[index].SetActive(index == difficultyIndex);
            var playUnlockAnimation =
                difficultyIndex == _difficultyTexts.Length - 1 &&
                isUnlockOlivier;
            SelectedDifficultyIndex = difficultyIndex;

            _animationExitTrigger ??= GetComponent<AnimationExitTrigger>();
            _animationExitTrigger.Exited -= OnAnimationExit;
            _animationExitTrigger.Exited += OnAnimationExit;
            _animator.enabled = true;
            IsUnlockOlivier = playUnlockAnimation;
            _animator.SetBool(
                Animator.StringToHash("DifficultyChange"),
                playUnlockAnimation);
            _animator.Update(0f);
            IsPlaying = true;
            ApplyPauseState();
        }

        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            ApplyPauseState();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _applicationUnfocused = !hasFocus;
            ApplyPauseState();
        }

        private void ApplyPauseState()
        {
            if (_animator == null) return;
            _animator.speed =
                _applicationPaused || _applicationUnfocused
                    ? 0f
                    : 1f;
        }

        private void OnAnimationExit()
        {
            IsPlaying = false;
            if (_animationExitTrigger != null)
                _animationExitTrigger.Exited -= OnAnimationExit;
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_animationExitTrigger != null)
                _animationExitTrigger.Exited -= OnAnimationExit;
        }
    }
}
