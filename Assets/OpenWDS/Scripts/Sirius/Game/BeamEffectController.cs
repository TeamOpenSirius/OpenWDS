using UnityEngine;

namespace Sirius.Game
{
    /// <summary>
    /// Executable recovery of the original BeamEffectController. The serialized
    /// fields and the width/color/frame operations match the Android ARM64 method
    /// at RVA 0xB97BEE8.
    /// </summary>
    public class BeamEffectController : MonoBehaviour
    {
        [SerializeField] protected float _animationTime = 0.5f;
        [SerializeField] protected ParticleSystem _beamSquare;
        [SerializeField] protected ParticleSystem[] _beamPillers;
        [SerializeField] protected ParticleSystem[] _beamFrames;
        [SerializeField] private Gradient _normalColor;
        [SerializeField] private Gradient _criticalColor;

        private float _startTime;

        public void Initialize(int laneCount, bool isCritical, float singleLaneWidth)
        {
            var color = new ParticleSystem.MinMaxGradient(
                isCritical ? _criticalColor : _normalColor);
            var width = laneCount * singleLaneWidth;

            if (_beamSquare != null)
            {
                var main = _beamSquare.main;
                main.startSizeXMultiplier = width;
                var colors = _beamSquare.colorOverLifetime;
                colors.color = color;
            }

            if (_beamFrames == null) return;
            for (var index = 0; index < _beamFrames.Length; index++)
            {
                var frame = _beamFrames[index];
                if (frame == null) continue;
                var shape = frame.shape;
                var position = shape.position;
                position.x = width * (index < 3 ? -0.5f : 0.5f);
                position.z = 0f;
                shape.position = position;
                // ARM64 0xB9800A0: only the size setter skips entries 0/3.
                // LT/LB/RT/RB (1/2/4/5) use the current note width.
                if (index != 0 && index != 3)
                {
                    var main = frame.main;
                    main.startSizeXMultiplier = width;
                }
                var colors = frame.colorOverLifetime;
                colors.color = color;
            }
        }

        public void Play()
        {
            _startTime = Time.time;
            foreach (var particle in GetComponentsInChildren<ParticleSystem>(true))
                particle.Play(true);
        }

        public bool TryExpire() => Time.time - _startTime >= _animationTime;
    }
}
