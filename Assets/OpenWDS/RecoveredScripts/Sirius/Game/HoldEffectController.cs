using UnityEngine;

namespace Sirius.Game
{
    /// <summary>Recovered continuous Hold lane effect (RVA 0xB97C2A4).</summary>
    public class HoldEffectController : MonoBehaviour, IRecoveredHoldEffectController
    {
        [SerializeField] protected float _animationTime = 0.8f;
        [SerializeField] protected ParticleSystem _bombSquare;
        [SerializeField] protected ParticleSystem[] _bombBoxes;
        [SerializeField] protected ParticleSystem[] _bombPillers;
        [SerializeField] protected ParticleSystem _bombParticle;
        [SerializeField] protected ParticleSystem _bombStar;
        [SerializeField] private Gradient _holdColor;
        [SerializeField] private Gradient _scratchHoldColor;
        [SerializeField] protected Gradient _starHoldColor;
        [SerializeField] protected Gradient _starScratchHoldColor;

        private bool _ending;
        private float _endedAt;
        public GameObject EffectObject => gameObject;

        public void Initialize(int laneCount, bool isScratch, float singleLaneWidth)
        {
            _ending = false;
            var width = laneCount * singleLaneWidth;
            var holdColor = new ParticleSystem.MinMaxGradient(
                isScratch ? _scratchHoldColor : _holdColor);
            var starColor = new ParticleSystem.MinMaxGradient(
                isScratch ? _starScratchHoldColor : _starHoldColor);

            if (_bombSquare != null)
            {
                var main = _bombSquare.main;
                main.startSizeX = new ParticleSystem.MinMaxCurve(
                    (singleLaneWidth - 0.2f) * laneCount, width);
                main.loop = true;
                SetColor(_bombSquare, holdColor);
            }

            if (_bombBoxes != null)
            {
                for (var index = 0; index < _bombBoxes.Length; index++)
                {
                    var particle = _bombBoxes[index];
                    if (particle == null) continue;
                    var main = particle.main;
                    main.startSizeXMultiplier = width;
                    main.loop = true;
                    if (index == 2 || index == 3)
                    {
                        var shape = particle.shape;
                        shape.position = new Vector3(
                            (index == 2 ? -0.5f : 0.5f) * width, 0f, 0f);
                    }
                    SetColor(particle, holdColor);
                }
            }

            if (_bombPillers != null)
            {
                for (var index = 0; index < _bombPillers.Length; index++)
                {
                    var particle = _bombPillers[index];
                    if (particle == null) continue;
                    if (index < 4)
                    {
                        var shape = particle.shape;
                        shape.position = new Vector3(
                            (index & 1) == 0 ? -width * 0.5f : width * 0.5f,
                            index < 2 ? 0.31f : -0.31f,
                            0f);
                    }
                    var main = particle.main;
                    main.loop = true;
                    SetColor(particle, holdColor);
                }
            }

            ConfigureBurst(_bombParticle, holdColor, width, 0.61f,
                laneCount * 30f);
            ConfigureBurst(_bombStar, starColor, width - 0.4f, 0.61f,
                laneCount * 10f);
        }

        private static void ConfigureBurst(
            ParticleSystem particle,
            ParticleSystem.MinMaxGradient color,
            float width,
            float height,
            float emissionRate)
        {
            if (particle == null) return;
            var shape = particle.shape;
            shape.scale = new Vector3(width, height, 0f);
            var emission = particle.emission;
            emission.rateOverTime = emissionRate;
            var main = particle.main;
            main.loop = true;
            SetColor(particle, color);
        }

        private static void SetColor(
            ParticleSystem particle,
            ParticleSystem.MinMaxGradient color)
        {
            if (particle == null) return;
            var colors = particle.colorOverLifetime;
            colors.color = color;
        }

        public void Play()
        {
            foreach (var particle in GetComponentsInChildren<ParticleSystem>(true))
                particle.Play(true);
        }

        public void OnHoldEnd()
        {
            _ending = true;
            _endedAt = Time.time;
            foreach (var particle in GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.loop = false;
            }
        }

        public bool TryExpire() => _ending && Time.time - _endedAt >= _animationTime;
    }
}
