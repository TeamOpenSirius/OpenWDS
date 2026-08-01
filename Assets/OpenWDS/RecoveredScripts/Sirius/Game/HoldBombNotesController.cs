using UnityEngine;

namespace Sirius.Game
{
    public class HoldBombNotesController : BombNotesController
    {
        [SerializeField] protected Gradient _holdColor1;
        [SerializeField] protected Gradient _holdColor2;
        [SerializeField] protected Gradient _holdColor3;
        [SerializeField] protected Gradient _scratchHoldColor1;
        [SerializeField] protected Gradient _scratchHoldColor2;
        [SerializeField] protected Gradient _scratchHoldColor3;

        public override void InitializeColor(bool isScratch)
        {
            Set(_bombCircle, isScratch ? _scratchHoldColor1 : _holdColor1);
            Set(_bombLight, isScratch ? _scratchHoldColor1 : _holdColor1);
            if (_bombLights != null)
                foreach (var item in _bombLights)
                    Set(item, isScratch ? _scratchHoldColor1 : _holdColor1);
            Set(_bombNotes, isScratch ? _scratchHoldColor2 : _holdColor2);
            Set(_bombLines, isScratch ? _scratchHoldColor2 : _holdColor2);
            Set(_bombLinesChild, isScratch ? _scratchHoldColor2 : _holdColor2);
            Set(_bombRings1, isScratch ? _scratchHoldColor3 : _holdColor3);
            Set(_bombRings2, isScratch ? _scratchHoldColor3 : _holdColor3);
            Set(_bombRote, isScratch ? _scratchHoldColor3 : _holdColor3);
        }

        protected static void Set(ParticleSystem particle, Gradient gradient)
        {
            if (particle == null) return;
            var colors = particle.colorOverLifetime;
            colors.color = new ParticleSystem.MinMaxGradient(gradient);
        }
    }
}
