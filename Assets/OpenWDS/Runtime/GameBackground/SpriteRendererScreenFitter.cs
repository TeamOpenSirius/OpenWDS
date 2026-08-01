using UnityEngine;

namespace Sirius.Screens
{
    public sealed class SpriteRendererScreenFitter : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Camera _camera;
        [SerializeField] private bool _useMainCamera;

        private void Awake()
        {
            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_useMainCamera || _camera == null) _camera = Camera.main;
            Adjust();
        }

        private void Start()
        {
            Adjust();
        }

        private void Adjust()
        {
            if (_spriteRenderer == null || _spriteRenderer.sprite == null || _camera == null)
                return;
            var distance = Mathf.Abs(Vector3.Dot(
                transform.position - _camera.transform.position,
                _camera.transform.forward));
            if (distance <= Mathf.Epsilon) return;
            var visibleHeight = 2f * distance *
                                Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            var visibleWidth = visibleHeight * _camera.aspect;
            var size = _spriteRenderer.sprite.bounds.size;
            if (size.x <= Mathf.Epsilon || size.y <= Mathf.Epsilon) return;
            var fit = Mathf.Max(visibleWidth / size.x, visibleHeight / size.y);
            transform.localScale = new Vector3(fit, fit, fit);
        }
    }
}
