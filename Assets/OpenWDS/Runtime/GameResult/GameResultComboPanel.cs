using UnityEngine;
using UnityEngine.UI;

namespace Sirius.GameResult
{
    public sealed class GameResultComboPanel : RecoveredGameResultPanelBase
    {
        [SerializeField] private Text _maxComboText;
        [SerializeField] private GameObject[] _badgeEffectObjects;

        public override void Initialize(RecoveredGameResultViewData data)
        {
            base.Initialize(data);
            if (_maxComboText != null) _maxComboText.text = data.MaxCombo.ToString();
            var allPerfect = data.IsAllPerfect || data.IsPerfectStar;
            SetBadge(0, allPerfect);
            SetBadge(1, !allPerfect && data.IsFullCombo);
        }

        private void SetBadge(int index, bool active)
        {
            if (_badgeEffectObjects == null || index >= _badgeEffectObjects.Length ||
                _badgeEffectObjects[index] == null) return;
            _badgeEffectObjects[index].SetActive(active);
        }
    }
}
