using UnityEngine;

namespace Sirius.GameResult
{
    public abstract class RecoveredGameResultPanelBase : MonoBehaviour
    {
        [SerializeField] protected CanvasGroup _canvasGroup;

        public virtual void Initialize(RecoveredGameResultViewData data)
        {
            if (data == null) throw new System.ArgumentNullException(nameof(data));
            if (_canvasGroup == null) return;
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        internal void SetVisible(bool visible)
        {
            if (_canvasGroup == null)
            {
                gameObject.SetActive(visible);
                return;
            }
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }
}
