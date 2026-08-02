using UnityEngine;

namespace Sirius.Game
{
    /// <summary>Recovered Sakura Bomb controller (RVA 0xB976DC4).</summary>
    public class BombSakuraController : MonoBehaviour, IRecoveredBombController
    {
        protected const float LaneWidth = 0.925f;
        [SerializeField] protected float _animationTime = 0.7f;
        [SerializeField] protected bool _isCritical;
        [SerializeField] protected ParticleSystem _bombFlower;
        [SerializeField] protected ParticleSystem _bombSmoke;
        [SerializeField] protected ParticleSystem[] _bombLights;
        [SerializeField] protected ParticleSystem _bombPetals;
        [SerializeField] protected ParticleSystem _bombLeafs;
        [SerializeField] protected ParticleSystem _bombTrail;
        [SerializeField] protected ParticleSystem _bombLine1;
        [SerializeField] protected ParticleSystem _bombLine2;
        [SerializeField] protected ParticleSystem[] _bombLineChildren;

        private float _startedAt;
        public float AnimationTime => _animationTime;

        public virtual void ApplyTapEffectType(bool isDefaultTapEffect)
        {
            if (isDefaultTapEffect) return;
            // BombSakuraController.SetUpLightParticles RVA 0xB976B64.
            SetActive(_bombSmoke, false);
            SetActive(_bombLights, false);
            SetActive(_bombPetals, false);
            SetActive(_bombLeafs, false);
            SetActive(_bombTrail, false);
            SetActive(_bombLine1, false);
            SetActive(_bombLine2, false);
        }

        public virtual void Initialize(int laneCount, long startMilliseconds, bool isStrong)
        {
            var width = laneCount * LaneWidth;
            var scale = isStrong ? 1f : 0.2f;
            var velocity = isStrong ? 1f : 0.65f;
            var critical = _isCritical ? 1.5f : 1f;
            Configure(_bombFlower, velocity * critical, 2f * velocity * critical,
                30f * laneCount * velocity, width, 0.63f, scale);
            if (_bombSmoke != null)
            {
                var main = _bombSmoke.main;
                main.startSizeMultiplier = 2f * critical;
                var shape = _bombSmoke.shape;
                shape.radius = width * 0.5f;
                var radial = _bombSmoke.velocityOverLifetime;
                radial.radialMultiplier = scale * 0.2f;
            }
            if (_bombLights != null)
                foreach (var item in _bombLights)
                    if (item != null)
                    {
                        var main = item.main;
                        main.startSizeXMultiplier = width;
                        main.startSizeYMultiplier = 2f * scale * critical;
                    }
            Configure(_bombPetals, 0.1f * velocity,
                0.2f * velocity * critical, 60f * laneCount * critical,
                width, 0.63f, scale);
            Configure(_bombLeafs, 0.5f * velocity,
                2f * velocity, 4f * laneCount, width, 0.5f, scale * critical);
            Configure(_bombTrail, 0.5f * velocity,
                4f * velocity, 0.75f * laneCount, width, 1.5f, scale * critical);
            Configure(_bombLine1, 0.5f * velocity,
                1.5f * velocity, 0.5f * laneCount, width, 1.5f, scale * critical);
            Configure(_bombLine2, 0.5f * velocity,
                1.5f * velocity, 0.5f * laneCount, width, 1.5f, scale * critical);
            if (_bombLineChildren != null)
                foreach (var item in _bombLineChildren)
                    Configure(item, 0.5f * velocity, 1.5f * velocity,
                        0f, width, 1.5f, scale * critical);
            ChangeSpeed(1f);
        }

        public virtual void InitializeColor(bool isScratch) { }

        protected virtual void ChangeSpeed(float speed)
        {
            foreach (var particle in GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.simulationSpeed = speed;
            }
        }

        public void Play()
        {
            _startedAt = Time.time;
            foreach (var particle in GetComponentsInChildren<ParticleSystem>())
                particle.Play(true);
        }

        public bool TryExpire() => Time.time - _startedAt >= _animationTime;

        protected static void Configure(ParticleSystem particle, float minSize,
            float maxSize, float emission, float width, float height, float speed)
        {
            if (particle == null) return;
            var main = particle.main;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            var shape = particle.shape;
            shape.scale = new Vector3(width, height, 0f);
            if (emission > 0f)
            {
                var module = particle.emission;
                module.rateOverTimeMultiplier = emission;
            }
            var velocity = particle.velocityOverLifetime;
            velocity.speedModifierMultiplier = speed;
        }

        protected static void SetActive(ParticleSystem particle, bool active)
        {
            if (particle != null) particle.gameObject.SetActive(active);
        }

        protected static void SetActive(ParticleSystem[] particles, bool active)
        {
            if (particles == null) return;
            foreach (var particle in particles) SetActive(particle, active);
        }
    }
}
