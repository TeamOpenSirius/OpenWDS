using UnityEngine;

namespace Sirius.Game
{
    /// <summary>Recovered continuous Notes hold effect (RVA 0xB97CDE0).</summary>
    public class HoldNotesEffectController : MonoBehaviour, IRecoveredHoldEffectController
    {
        [SerializeField] protected float _animationTime = 1f;
        [SerializeField] protected ParticleSystem _bombCircle;
        [SerializeField] protected ParticleSystem _bombLight;
        [SerializeField] protected ParticleSystem[] _bombLights;
        [SerializeField] protected ParticleSystem _bombNotes;
        [SerializeField] protected ParticleSystem _bombLines;
        [SerializeField] protected ParticleSystem _bombLinesChild;
        [SerializeField] protected ParticleSystem _bombRings;
        [SerializeField] protected ParticleSystem _bombRote;
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
            var color1 = isScratch ? _scratchHoldColor1 : _holdColor1;
            var color2 = isScratch ? _scratchHoldColor2 : _holdColor2;
            var color3 = isScratch ? _scratchHoldColor3 : _holdColor3;
            Configure(_bombCircle, width, 0.63f, laneCount * 5f, color1);
            Configure(_bombLight, width, 0.63f, 0f, color1);
            if (_bombLights != null)
                foreach (var item in _bombLights)
                    Configure(item, width, 0.63f, 0f, color1);
            Configure(_bombNotes, width, 0.63f, laneCount * 20f, color2);
            Configure(_bombLines, width, 0.63f, laneCount * 5f, color2);
            Configure(_bombLinesChild, width, 0.63f, 0f, color2);
            Configure(_bombRings, width, 0.63f, laneCount * 3f, color3);
            Configure(_bombRote, width, 0.63f, 0f, color3);
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
            SetLoop(_bombCircle, false);
            SetLoop(_bombLight, false);
            if (_bombLights != null)
                for (var index = 0; index < Mathf.Min(2, _bombLights.Length); index++)
                    SetLoop(_bombLights[index], false);
            SetLoop(_bombNotes, false);
            SetLoop(_bombLines, false);
            SetLoop(_bombRings, false);
            SetLoop(_bombRote, false);
        }

        public bool TryExpire() => _ending && Time.time - _endedAt >= _animationTime;

        private static void Configure(ParticleSystem particle, float width,
            float height, float rate, Gradient gradient)
        {
            if (particle == null) return;
            var shape = particle.shape;
            shape.scale = new Vector3(width, height, 0f);
            if (rate > 0f)
            {
                var emission = particle.emission;
                emission.rateOverTimeMultiplier = rate;
            }
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
