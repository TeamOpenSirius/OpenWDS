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
                shape.position = position;
                // The two centre frame entries are the only ones whose gradient
                // is not rewritten by the original loop.
                if (index == 0 || index == 3) continue;
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
