using UnityEngine;

namespace Sirius.Game
{
    /// <summary>Recovered continuous Sakura hold effect (RVA 0xB97D900).</summary>
    public class HoldSakuraEffectController : MonoBehaviour, IHoldEffectController
    {
        [SerializeField] protected float _animationTime = 0.7f;
        [SerializeField] protected ParticleSystem _bombFlower;
        [SerializeField] protected ParticleSystem _bombSmoke;
        [SerializeField] protected ParticleSystem _bombLine1;
        [SerializeField] protected ParticleSystem _bombLine2;
        [SerializeField] protected Gradient _holdColor1;
        [SerializeField] protected Gradient _holdColor2;
        [SerializeField] protected Gradient _holdColor3;
        [SerializeField] protected Gradient _scratchHoldColor1;
        [SerializeField] protected Gradient _scratchHoldColor2;
        [SerializeField] protected Gradient _scratchHoldColor3;

        private bool _ending;
        private float _endedAt;
        public GameObject EffectObject => gameObject;

        public void Initialize(int laneCount, bool isScratch, float singleLaneWidth)
        {
            _ending = false;
            var width = laneCount * singleLaneWidth;
            Configure(_bombFlower, width, 0.63f, laneCount * 5f,
                isScratch ? _scratchHoldColor1 : _holdColor1);
            Configure(_bombSmoke, width, 1f, laneCount * 5f + 40f,
                isScratch ? _scratchHoldColor2 : _holdColor2);
            Configure(_bombLine1, width, 1.5f, laneCount * 0.75f,
                isScratch ? _scratchHoldColor3 : _holdColor3);
            Configure(_bombLine2, width, 1.5f, laneCount * 0.5f,
                isScratch ? _scratchHoldColor3 : _holdColor3);
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
            SetLoop(_bombFlower, false);
            SetLoop(_bombSmoke, false);
            SetLoop(_bombLine1, false);
            SetLoop(_bombLine2, false);
        }

        public bool TryExpire() => _ending && Time.time - _endedAt >= _animationTime;

        private static void Configure(ParticleSystem particle, float width,
            float height, float rate, Gradient gradient)
        {
            if (particle == null) return;
            var shape = particle.shape;
            shape.scale = new Vector3(width, height, 0f);
            var emission = particle.emission;
            emission.rateOverTimeMultiplier = rate;
            var main = particle.main;
            main.loop = true;
            var colors = particle.colorOverLifetime;
            colors.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void SetLoop(ParticleSystem particle, bool value)
        {
            if (particle == null) return;
            var main = particle.main;
            main.loop = value;
        }
    }
}
