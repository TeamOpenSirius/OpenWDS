using TMPro;
using UnityEngine;

namespace Sirius.Game.UI
{
    public sealed class IncrementScoreCount : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _incrementCountText;
        [SerializeField] private TextMeshProUGUI _incrementCountBgText;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Animator _animator;

        private static readonly int StateHash = Animator.StringToHash("IncrementScoreCount_anim");

        public void SetText(long value)
        {
            var text = "+" + value;
            _incrementCountText.SetText(text);
            if (_incrementCountBgText != null) _incrementCountBgText.SetText(text);
        }

        public void OnPlay()
        {
            _animator.enabled = true;
            _animator.Play(StateHash, 0, 0f);
        }

        public void OnExit()
        {
            _animator.enabled = false;
            _canvasGroup.alpha = 0f;
        }
    }
}
