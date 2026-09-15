using UnityEngine;

namespace Sirius.Game
{
    public class ScratchBombSakuraController : BombSakuraController
    {
        [SerializeField] private Transform _slashEffectTransform;
        private GameObject _cachedSlashEffectObject;

        public override void Initialize(int laneCount, long startMilliseconds, bool isStrong)
        {
            if (_cachedSlashEffectObject == null && _slashEffectTransform != null)
                _cachedSlashEffectObject = _slashEffectTransform.gameObject;
            if (_cachedSlashEffectObject != null &&
                _cachedSlashEffectObject.activeSelf != isStrong)
                _cachedSlashEffectObject.SetActive(isStrong);
            base.Initialize(laneCount, startMilliseconds, isStrong);
            ChangeSpeed(1f);
        }
    }
}
