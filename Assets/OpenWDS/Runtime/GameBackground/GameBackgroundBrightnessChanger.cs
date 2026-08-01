using UnityEngine;

namespace Sirius.Game
{
    public interface IGameBackgroundBrightnessChanger
    {
        void SetBrightness(int currentLife);
    }

    public sealed class GameBackgroundBrightnessChanger : MonoBehaviour,
        IGameBackgroundBrightnessChanger
    {
        [SerializeField] private Animator _animator;
        private static readonly int ShadowBool = Animator.StringToHash("Shadow");

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
        }

        public void SetBrightness(int currentLife)
        {
            // The recovered game does not yet drive player life. Preserve the
            // original animator endpoint without guessing its life threshold.
            if (_animator != null && currentLife > 0)
                _animator.SetBool(ShadowBool, false);
        }
    }
}
