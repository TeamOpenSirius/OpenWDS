using UnityEngine;

namespace Sirius.Game
{
    /// <summary>Recovered Notes Bomb controller (RVA 0xB975F24).</summary>
    public class BombNotesController : MonoBehaviour, IBombController
    {
        protected const float LaneWidth = 0.925f;
        [SerializeField] protected float _animationTime = 0.7f;
        [SerializeField] protected bool _isCritical;
        [SerializeField] protected ParticleSystem _bombCircle;
        [SerializeField] protected ParticleSystem _bombLight;
        [SerializeField] protected ParticleSystem[] _bombLights;
        [SerializeField] protected ParticleSystem _bombNotes;
        [SerializeField] protected ParticleSystem _bombLines;
        [SerializeField] protected ParticleSystem _bombLinesChild;
        [SerializeField] protected ParticleSystem _bombRings1;
        [SerializeField] protected ParticleSystem _bombRings2;
        [SerializeField] protected ParticleSystem _bombRote;

        private float _startedAt;
        public float AnimationTime => _animationTime;

        public virtual void ApplyTapEffectType(bool isDefaultTapEffect)
        {
            if (isDefaultTapEffect) return;
            // BombNotesController.SetUpLightParticles RVA 0xB975D0C.
            SetActive(_bombLight, false);
            SetActive(_bombLights, false);
            SetActive(_bombNotes, false);
            SetActive(_bombLines, false);
            SetActive(_bombRings1, false);
            SetActive(_bombRings2, false);
        }

        public virtual void Initialize(int laneCount, long startMilliseconds, bool isStrong)
        {
            var width = laneCount * LaneWidth;
            var scale = isStrong ? 1f : 0.2f;
            var velocity = isStrong ? 1f : 0.65f;
            var critical = _isCritical ? 1.5f : 1f;
            Configure(_bombCircle, velocity * critical, 2f * velocity * critical,
                30f * laneCount * velocity, width, 0.63f, 1f);
            if (_bombLight != null)
            {
                var main = _bombLight.main;
                main.startSizeXMultiplier = 2f * laneCount;
                main.startSizeYMultiplier = 3f * scale * critical;
            }
            if (_bombLights != null)
                foreach (var item in _bombLights)
                    ConfigureSize(item, width, 2f * scale * critical);
            Configure(_bombNotes, 0.1f * velocity,
                0.2f * velocity * critical, 60f * laneCount * critical,
                width, 0.63f, scale);
            Configure(_bombLines, 0.63f * velocity * critical,
                0.63f * velocity * critical, 150f * laneCount * velocity,
                width, 0.63f, velocity * critical);
            Configure(_bombLinesChild, 0.63f * velocity * critical,
                0.63f * velocity * critical, 0f, width, 0.63f, velocity * critical);
            Configure(_bombRings1, 0.63f * velocity,
                1.5f * velocity, 25f * laneCount * velocity * critical,
                width, 1.5f, 1f);
            Configure(_bombRings2, 0.5f * velocity,
                1.5f * velocity, laneCount * velocity,
                width, 0.5f, 1f);
            Configure(_bombRote, 0.5f * velocity,
                1.5f * velocity, laneCount * velocity,
                width, 0.5f, 1f);
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

        private static void ConfigureSize(ParticleSystem particle, float x, float y)
        {
            if (particle == null) return;
            var main = particle.main;
            main.startSizeXMultiplier = x;
            main.startSizeYMultiplier = y;
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
