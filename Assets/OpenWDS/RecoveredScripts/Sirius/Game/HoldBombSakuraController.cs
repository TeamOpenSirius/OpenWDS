using UnityEngine;

namespace Sirius.Game
{
    public class HoldBombSakuraController : BombSakuraController
    {
        [SerializeField] protected Gradient _holdColor1;
        [SerializeField] protected Gradient _holdColor2;
        [SerializeField] protected Gradient _holdColor3;
        [SerializeField] protected Gradient _scratchHoldColor1;
        [SerializeField] protected Gradient _scratchHoldColor2;
        [SerializeField] protected Gradient _scratchHoldColor3;

        public override void InitializeColor(bool isScratch)
        {
            Set(_bombFlower, isScratch ? _scratchHoldColor1 : _holdColor1);
            Set(_bombSmoke, isScratch ? _scratchHoldColor1 : _holdColor1);
            if (_bombLights != null)
                foreach (var item in _bombLights)
                    Set(item, isScratch ? _scratchHoldColor1 : _holdColor1);
            Set(_bombPetals, isScratch ? _scratchHoldColor2 : _holdColor2);
            Set(_bombLeafs, isScratch ? _scratchHoldColor2 : _holdColor2);
            Set(_bombTrail, isScratch ? _scratchHoldColor3 : _holdColor3);
            Set(_bombLine1, isScratch ? _scratchHoldColor3 : _holdColor3);
            Set(_bombLine2, isScratch ? _scratchHoldColor3 : _holdColor3);
            if (_bombLineChildren != null)
                foreach (var item in _bombLineChildren)
                    Set(item, isScratch ? _scratchHoldColor3 : _holdColor3);
        }

        private static void Set(ParticleSystem particle, Gradient gradient)
        {
            if (particle == null) return;
            var colors = particle.colorOverLifetime;
            colors.color = new ParticleSystem.MinMaxGradient(gradient);
        }
    }
}
