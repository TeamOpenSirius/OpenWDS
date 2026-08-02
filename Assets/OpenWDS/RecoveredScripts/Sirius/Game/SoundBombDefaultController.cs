using UnityEngine;

namespace Sirius.Game
{
    /// <summary>Recovered Default style Sound bomb controller.</summary>
    public class SoundBombDefaultController : MonoBehaviour, IRecoveredBombController
    {
        [SerializeField] private float _animationTime = 0.7f;
        [SerializeField] private ParticleSystem[] _bombEffects;
        [SerializeField] private Gradient _holdColor1;
        [SerializeField] private Gradient _scratchColor1;
        [SerializeField] private Gradient _holdColor2;
        [SerializeField] private Gradient _scratchColor2;

        private float _startedAt;
        public float AnimationTime => _animationTime;

        // SoundBombController.Initialize has no IsDefaultTapEffect branch.
        public void ApplyTapEffectType(bool isDefaultTapEffect) { }

        public void Initialize(int laneCount, long startMilliseconds, bool isStrong)
        {
            if (_bombEffects == null) return;
            foreach (var particle in _bombEffects)
            {
                if (particle == null) continue;
                var main = particle.main;
                main.simulationSpeed = 1f;
            }
        }

        public void InitializeColor(bool isScratch)
        {
            if (_bombEffects == null) return;
            for (var index = 0; index < _bombEffects.Length; index++)
            {
                var particle = _bombEffects[index];
                if (particle == null) continue;
                Gradient gradient;
                if (index < 2)
                    gradient = isScratch ? _scratchColor1 : _holdColor1;
                else
                    gradient = isScratch ? _scratchColor2 : _holdColor2;
                var colors = particle.colorOverLifetime;
                colors.color = new ParticleSystem.MinMaxGradient(gradient);
            }
        }

        public void Play()
        {
            _startedAt = Time.time;
            foreach (var particle in GetComponentsInChildren<ParticleSystem>())
                particle.Play(true);
        }

        public bool TryExpire() => Time.time - _startedAt >= _animationTime;
    }
}
