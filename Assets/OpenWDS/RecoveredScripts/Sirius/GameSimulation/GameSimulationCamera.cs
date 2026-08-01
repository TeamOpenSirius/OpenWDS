using UnityEngine;

namespace Sirius.GameSimulation
{
    /// <summary>
    /// Recovered from Sirius.GameSimulation.GameSimulationCamera.
    /// The retail component owns only the preview Camera's visibility.
    /// </summary>
    public sealed class GameSimulationCamera : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        public RenderTexture TargetTexture =>
            _camera != null ? _camera.targetTexture : null;

        private void Awake()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera != null) _camera.enabled = false;
        }

        public void Show()
        {
            if (_camera != null) _camera.enabled = true;
        }

        public void Hide()
        {
            if (_camera != null) _camera.enabled = false;
        }
    }
}
