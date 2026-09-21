using System.Linq;
using Sirius.Animations;
using Sirius.ClientAssetsShared.Extensions;
using Sirius.LiveEngine;
using UnityEngine;

namespace Sirius.LiveEngine
{
    public enum SenseLightTypes { Variable = 0, Support = 1, Control = 2, Amplification = 3, Special = 4 }
}

namespace Sirius.Game
{
    public interface IStarActCutInPlayer
    {
        void SetMainCamera(Camera gameMainCamera);
        void Initialize(Sprite characterSprite);
        void Animate(SenseLightTypes[] storageSenceLights = null);
    }

    // 2.31.2 VA 0xb9e59c0–0xb9e5fe0; original serialized field names.
    public sealed class StarActCutInPlayer : MonoBehaviour, IStarActCutInPlayer
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer[] _spriteRenderers;
        [SerializeField] private Vector3 _viewportPosition = Vector3.zero;
        [SerializeField] private ParticleSystem[] _particleSystems;
        [SerializeField] private Material _outlineMatarial;
        [SerializeField] private Color[] _outlineColors = new Color[5];
        private readonly int ColorCountID = Shader.PropertyToID("_ColorCount");
        private bool _isInitialized;
        private Camera _gameMainCamera;
        private Vector3 _playPosition;
        private readonly int _animate = Animator.StringToHash("Animate");
        private Material _ownedOutline;

        private void Awake()
        {
            // The retail player lives in its own scene. Isolate its material
            // when the recovered prefab is instantiated across repeated games.
            if (_outlineMatarial == null) return;
            var original = _outlineMatarial;
            _ownedOutline = new Material(original);
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m == original ? _ownedOutline : m).ToArray();
            _outlineMatarial = _ownedOutline;
        }

        private void OnDestroy()
        {
            if (_ownedOutline != null) Destroy(_ownedOutline);
        }

        private void Start()
        {
            foreach (var particle in _particleSystems)
            {
                particle.gameObject.SetActive(false);
                var main = particle.main;
                main.playOnAwake = true;
            }
        }
        public void SetMainCamera(Camera gameMainCamera) => _gameMainCamera = gameMainCamera;
        public void Initialize(Sprite characterSprite)
        {
            if (characterSprite == null)
            {
                gameObject.SetActive(false);
                return;
            }
            foreach (var renderer in _spriteRenderers) renderer.sprite = characterSprite;
            _playPosition = _gameMainCamera.ViewportToWorldPoint(_viewportPosition);
            _animator.GetComponent<AnimationExitActionTrigger>().SetExitAction(SetStandbyPosition);
            SetStandbyPosition();
            SetColorMaterial();
            _isInitialized = true;
        }
        public void ResetPresentation()
        {
            _animator.Rebind();
            foreach (var particle in _particleSystems) particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetStandbyPosition();
            SetColorMaterial();
        }
        private void SetStandbyPosition() => transform.SetLocalPositionXY(10000, 10000);
        private void SetPlayPosition() => transform.SetPositionXY(_playPosition.x, _playPosition.y);
        private void SetColorMaterial(SenseLightTypes[] storageSenceLights = null)
        {
            if (storageSenceLights != null && storageSenceLights.Length != 0)
            {
                var lights = storageSenceLights.Distinct().ToArray();
                _outlineMatarial.SetFloat(ColorCountID, lights.Length);
                for (var i = 0; i < lights.Length; i++)
                    _outlineMatarial.SetColor(string.Format("_Color{0}", i + 1), _outlineColors[(int)lights[i]]);
            }
            else
            {
                _outlineMatarial.SetColor("_Color1", Color.white);
                _outlineMatarial.SetFloat(ColorCountID, 0);
            }
        }
        void IStarActCutInPlayer.Animate(SenseLightTypes[] storageSenceLights)
        {
            if (!_isInitialized) return;
            SetPlayPosition();
            SetColorMaterial(storageSenceLights);
            _animator.SetTrigger(_animate);
            foreach (var particle in _particleSystems) particle.Play();
        }
    }
}
