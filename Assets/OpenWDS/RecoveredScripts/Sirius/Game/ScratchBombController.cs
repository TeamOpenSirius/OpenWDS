using UnityEngine;

namespace Sirius.Game
{
    // Placeholder for Sirius.Game.ScratchBombController from Sirius.dll.
    public class ScratchBombController : BombController
    {
        [SerializeField] private Transform _slashEffectTransform;
        [SerializeField] private Transform _scratchBombFlare;

        public override void ApplyTapEffectType(bool isDefaultTapEffect)
        {
            base.ApplyTapEffectType(isDefaultTapEffect);
            if (isDefaultTapEffect || _slashEffectTransform == null) return;
            // ScratchBombController.Initialize RVA 0xB97AA10: in Light mode,
            // disable every slash renderer except the authored flare object.
            foreach (var renderer in
                     _slashEffectTransform.GetComponentsInChildren<Renderer>(true))
            {
                if (_scratchBombFlare != null &&
                    renderer.gameObject == _scratchBombFlare.gameObject)
                    continue;
                renderer.enabled = false;
            }
        }

        public override void Initialize(
            int laneCount, long startMilliseconds, bool isStrong)
        {
            if (_slashEffectTransform != null)
                _slashEffectTransform.gameObject.SetActive(isStrong);
            // Retail only toggles the cached slash root here. BomFlare remains
            // authored active below that root, so it is also the one renderer
            // preserved by ApplyTapEffectType in Light mode. Deactivating it
            // independently removes the purple ring from strong Scratch hits.
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
