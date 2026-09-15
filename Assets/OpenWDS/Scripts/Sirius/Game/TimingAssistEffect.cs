using UnityEngine;

namespace Sirius.Game
{
    /// <summary>
    /// TimingAssistEffect/TimingEffectBase behavior recovered from the original
    /// ARM64 implementation. The prefab retains the original renderer,
    /// controller and animation clip.
    /// </summary>
    public sealed class TimingAssistEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Animator _animator;

        // The original assist controller also names its only state
        // "TimingEffect_anime", despite using TimingAssistEffect_anime as motion.
        private static readonly int TimingEffectAnimationHash =
            Animator.StringToHash("TimingEffect_anime");
        private int _startFrameCount;

        public bool IsUnused { get; private set; } = true;
        public Sprite CurrentSprite =>
            _spriteRenderer != null ? _spriteRenderer.sprite : null;

        public void Initialize(float localScale, Sprite sprite)
        {
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (_animator == null)
                _animator = GetComponent<Animator>();
            _spriteRenderer.sprite = sprite;
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one * localScale;
            IsUnused = false;
        }

        public void SetLocalPosition(Vector3 localPosition)
        {
            transform.localPosition = localPosition;
        }

        public void Play()
        {
            _startFrameCount = Time.frameCount;
            _spriteRenderer.enabled = true;
            if (!_animator.enabled) _animator.enabled = true;
            _animator.Play(TimingEffectAnimationHash, 0, 0f);
        }

        public void OnExit()
        {
            if (Time.frameCount - _startFrameCount < 2) return;
            _spriteRenderer.enabled = false;
            if (_animator.enabled) _animator.enabled = false;
            IsUnused = true;
        }

        private void Update()
        {
            if (IsUnused || !_animator.enabled ||
                Time.frameCount == _startFrameCount)
                return;
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash != TimingEffectAnimationHash ||
                state.normalizedTime < 1f)
                return;
            OnExit();
        }
    }
}
