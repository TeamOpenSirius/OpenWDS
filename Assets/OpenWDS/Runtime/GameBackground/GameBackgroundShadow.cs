using UnityEngine;

namespace Sirius
{
    public sealed class GameBackgroundShadow : MonoBehaviour
    {
        [SerializeField] private GameObject _background;
        [SerializeField] private GameObject _backgroundShadow;

        private void Awake()
        {
            if (_background == null)
                _background = transform.Find("sample")?.gameObject;
            if (_backgroundShadow == null)
                _backgroundShadow = transform.Find("BGShadow")?.gameObject;
        }

        // Recovered from GameBackgroundShadow.Initialize at RVA 0xAF5A13C.
        public void Initialize()
        {
            if (_background == null || _backgroundShadow == null) return;
            _backgroundShadow.transform.localScale = _background.transform.localScale;
        }
    }
}
