using UnityEngine;

namespace Sirius.Game
{
    /// <summary>
    /// TimingEffectBase/TimingEffect behavior recovered from RVAs
    /// 0xB9F28D0-0xB9F2D20.  The prefab supplies the original renderer,
    /// controller and TimingEffect_anime clip.
    /// </summary>
    public sealed class TimingEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Animator _animator;

        private static readonly int TimingEffectAnimationHash =
            Animator.StringToHash("TimingEffect_anime");
        private int _startFrameCount;

        public bool IsUnused { get; private set; } = true;

        public void Initialize(float localScale, Sprite sprite)
        {
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (_animator == null)
                _animator = GetComponent<Animator>();
            _spriteRenderer.sprite = sprite;
            transform.localPosition = Vector3.zero;
            transform.localScale = new Vector3(localScale, localScale, localScale);
            IsUnused = false;
        }

        public void SetLocalPosition(Vector3 localPosition)
        {
            transform.localPosition = localPosition;
        }

        public void Play()
        {
            OnBeforeRent();
            _animator.Play(TimingEffectAnimationHash, 0, 0f);
        }

        /// <summary>
        /// TimingEffectBase.OnBeforeRent recovered from RVA 0xB9F2A14.
        /// The original pool invokes this immediately before an effect is reused.
        /// </summary>
        public void OnBeforeRent()
        {
            _startFrameCount = Time.frameCount;
            _spriteRenderer.enabled = true;
            if (!_animator.enabled)
                _animator.enabled = true;
        }

        /// <summary>
        /// Receiver for TimingEffect_anime's terminal AnimationEvent.  The original
        /// ignores events fired during the first two frames, then returns the effect
        /// to its pool after disabling both renderer and animator.
        /// </summary>
        public void OnExit()
        {
            if (Time.frameCount - _startFrameCount < 2)
                return;
            _spriteRenderer.enabled = false;
            if (_animator.enabled)
                _animator.enabled = false;
            IsUnused = true;
        }

        private void Update()
        {
            if (IsUnused || !_animator.enabled || Time.frameCount == _startFrameCount)
                return;
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash != TimingEffectAnimationHash || state.normalizedTime < 1f)
                return;
            OnExit();
        }
    }
}
