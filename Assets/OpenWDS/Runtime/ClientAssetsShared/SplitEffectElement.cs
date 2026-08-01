using UnityEngine;

namespace Sirius.Game
{
    /// <summary>
    /// Original component type referenced by SplitEffectController's serialized
    /// element array. Its visual behavior is carried by the bundled particle
    /// systems; retaining this exact component type restores the cross-bundle
    /// PPtrs without replacing those assets.
    /// </summary>
    public sealed class SplitEffectElement : MonoBehaviour
    {
        [SerializeField] private Color _lineColor;
        [SerializeField] private ParticleSystem[] _lineEffects;

        public Color LineColor => _lineColor;
        public ParticleSystem[] LineEffects => _lineEffects;
    }
}
