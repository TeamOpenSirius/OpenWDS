using UnityEngine;

namespace Sirius.Game
{
    // Placeholder for Sirius.Game.ScratchBombController from Sirius.dll.
    public class ScratchBombController : BombController
    {
        [SerializeField] private Transform _slashEffectTransform;
        [SerializeField] private Transform _scratchBombFlare;

        public override void Initialize(
            int laneCount, long startMilliseconds, bool isStrong)
        {
            if (_slashEffectTransform != null)
                _slashEffectTransform.gameObject.SetActive(isStrong);
            if (_scratchBombFlare != null)
                _scratchBombFlare.gameObject.SetActive(!isStrong);
            base.Initialize(laneCount, startMilliseconds, isStrong);
        }

        protected override void ChangeSpeed(float simulationSpeed)
        {
            base.ChangeSpeed(simulationSpeed);
            if (_slashEffectTransform == null) return;
            foreach (var particle in
                     _slashEffectTransform.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.simulationSpeed = simulationSpeed;
            }
        }
    }
}
