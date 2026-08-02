using UnityEngine;

namespace Sirius.Game
{
    public interface IRecoveredBombController
    {
        float AnimationTime { get; }
        void ApplyTapEffectType(bool isDefaultTapEffect);
        void Initialize(int laneCount, long startMilliseconds, bool isStrong);
        void InitializeColor(bool isScratch);
        void Play();
        bool TryExpire();
    }

    public interface IRecoveredHoldEffectController
    {
        void Initialize(int laneCount, bool isScratch, float singleLaneWidth);
        void Play();
        void OnHoldEnd();
        bool TryExpire();
        GameObject EffectObject { get; }
    }

    /// <summary>Recovered Default Bomb particle controller (RVA 0xB9501D8).</summary>
    public class BombController : MonoBehaviour, IRecoveredBombController
    {
        protected const float LaneWidth = 0.925f;

        [SerializeField] protected float _animationTime = 0.7f;
        [SerializeField] protected bool _isCritical;
        [SerializeField] protected ParticleSystem _bombSquare;
        [SerializeField] protected ParticleSystem[] _bombBoxes;
        [SerializeField] protected ParticleSystem[] _bombPillers;
        [SerializeField] protected ParticleSystem _bombParticle;
        [SerializeField] protected ParticleSystem _bombStar;
        [SerializeField] protected ParticleSystem _bombStarCenter;
        [SerializeField] protected ParticleSystem _bombFlare;

        private float _startedAt;

        public float AnimationTime => _animationTime;

        public virtual void ApplyTapEffectType(bool isDefaultTapEffect)
        {
            if (isDefaultTapEffect) return;
            // BombController.SetUpLightParticles RVA 0xB94FFFC: Light keeps
            // Square and Flare, and disables these five authored groups.
            SetActive(_bombBoxes, false);
            SetActive(_bombPillers, false);
            SetActive(_bombParticle, false);
            SetActive(_bombStar, false);
            SetActive(_bombStarCenter, false);
        }

        public virtual void InitializeColor(bool isScratch) { }

        public virtual void Initialize(
            int laneCount, long startMilliseconds, bool isStrong)
        {
            var width = laneCount * LaneWidth;
            var strongScale = isStrong ? 1f : 0.2f;
            var strongVelocity = isStrong ? 1f : 0.65f;
            var criticalScale = _isCritical ? 1.5f : 1f;

            InitializeSquare(laneCount, width, strongScale, criticalScale);
            InitializeBoxes(width, strongScale, criticalScale);
            InitializePillers(width, strongScale, criticalScale);
            InitializeParticle(
                _bombParticle, laneCount, width, strongScale,
                strongVelocity, criticalScale);
            InitializeStar(
                _bombStar, laneCount, width, strongScale,
                strongVelocity, criticalScale, false);
            InitializeStar(
                _bombStarCenter, laneCount, width, strongScale,
                strongVelocity, criticalScale, true);
            ResetSimulationSpeed();
        }

        protected virtual void InitializeSquare(
            int laneCount, float width, float strongScale, float criticalScale)
        {
            if (_bombSquare == null) return;
            var main = _bombSquare.main;
            main.startSizeX = new ParticleSystem.MinMaxCurve(
                laneCount * 0.725f, width);
            var velocity = _bombSquare.velocityOverLifetime;
            velocity.speedModifierMultiplier = 80f * strongScale * criticalScale;
        }

        protected virtual void InitializeBoxes(
            float width, float strongScale, float criticalScale)
        {
            if (_bombBoxes == null) return;
            for (var index = 0; index < _bombBoxes.Length; index++)
            {
                var particle = _bombBoxes[index];
                if (particle == null) continue;
                var main = particle.main;
                if (index < 2) main.startSizeXMultiplier = width;
                main.startSizeYMultiplier =
                    (index == 0 ? 3f : 2f) * strongScale * criticalScale;
                if (index == 2 || index == 3)
                {
                    var shape = particle.shape;
                    shape.position = new Vector3(
                        (index == 2 ? -0.5f : 0.5f) * width, 0f, 0f);
                }
            }
        }

        protected virtual void InitializePillers(
            float width, float strongScale, float criticalScale)
        {
            if (_bombPillers == null) return;
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
                main.startSizeYMultiplier = 3.5f * strongScale * criticalScale;
            }
        }

        protected virtual void InitializeParticle(
            ParticleSystem particle,
            int laneCount,
            float width,
            float strongScale,
            float strongVelocity,
            float criticalScale)
        {
            if (particle == null) return;
            var main = particle.main;
            main.startSizeXMultiplier = 5f * strongScale * criticalScale;
            var emission = particle.emission;
            emission.rateOverTimeMultiplier =
                75f * laneCount * strongVelocity * criticalScale;
            var shape = particle.shape;
            shape.scale = new Vector3(width, 0.61f, 0f);
            var velocity = particle.velocityOverLifetime;
            velocity.speedModifierMultiplier = strongScale * criticalScale;
        }

        protected virtual void InitializeStar(
            ParticleSystem particle,
            int laneCount,
            float width,
            float strongScale,
            float strongVelocity,
            float criticalScale,
            bool center)
        {
            if (particle == null) return;
            var main = particle.main;
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.1f * strongVelocity * criticalScale,
                0.3f * strongVelocity * criticalScale);
            var emission = particle.emission;
            emission.rateOverTimeMultiplier =
                75f * laneCount * strongVelocity * criticalScale;
            var shape = particle.shape;
            shape.scale = new Vector3(
                width + (center ? -1.11f : 0f), 0.61f, 0f);
            var velocity = particle.velocityOverLifetime;
            velocity.speedModifierMultiplier = strongScale * criticalScale;
        }

        public virtual void Play()
        {
            _startedAt = Time.time;
            // Original OnInitializedParticles runs after light-only objects are
            // disabled and caches only active child systems.
            foreach (var particle in GetComponentsInChildren<ParticleSystem>())
                particle.Play(true);
        }

        public bool TryExpire() => Time.time - _startedAt >= _animationTime;

        protected virtual void ChangeSpeed(float simulationSpeed)
        {
            foreach (var particle in GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.simulationSpeed = simulationSpeed;
            }
        }

        protected void ResetSimulationSpeed() => ChangeSpeed(1f);

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
