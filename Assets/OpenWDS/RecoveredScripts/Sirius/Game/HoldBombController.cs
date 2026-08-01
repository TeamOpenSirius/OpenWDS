using UnityEngine;

namespace Sirius.Game
{
    // Placeholder for Sirius.Game.HoldBombController from Sirius.dll.
    public class HoldBombController : BombController
    {
        [SerializeField] private Gradient _holdColor;
        [SerializeField] private Gradient _scratchHoldColor;
        [SerializeField] protected Gradient _starHoldColor;
        [SerializeField] protected Gradient _starScratchHoldColor;

        public override void InitializeColor(bool isScratch)
        {
            var color = new ParticleSystem.MinMaxGradient(
                isScratch ? _scratchHoldColor : _holdColor);
            SetColor(_bombSquare, color);
            if (_bombBoxes != null)
                foreach (var particle in _bombBoxes) SetColor(particle, color);
            if (_bombPillers != null)
                foreach (var particle in _bombPillers) SetColor(particle, color);
            SetColor(_bombParticle, color);
            SetColor(_bombStar, new ParticleSystem.MinMaxGradient(
                isScratch ? _starScratchHoldColor : _starHoldColor));
        }

        private static void SetColor(
            ParticleSystem particle, ParticleSystem.MinMaxGradient color)
        {
            if (particle == null) return;
            var colors = particle.colorOverLifetime;
            colors.color = color;
        }
    }
}
