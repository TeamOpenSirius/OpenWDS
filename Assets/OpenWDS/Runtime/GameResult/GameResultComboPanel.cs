using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Sirius.GameResult
{
    public sealed class GameResultComboPanel : GameResultPanelBase
    {
        [SerializeField] private Text _maxComboText;
        [SerializeField] private GameObject[] _badgeEffectObjects;

        public override void Initialize(GameResultViewData data)
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

        internal Tween CreateCountUp(GameResultViewData data)
        {
            var value = 0;
            _maxComboText.text = "0";
            SetBadge(0, false);
            SetBadge(1, false);
            return DOTween.To(() => value, current =>
            {
                value = current;
                _maxComboText.text = current.ToString();
            }, data.MaxCombo, 0.35f).SetEase(Ease.Linear).OnComplete(() =>
            {
                var allPerfect = data.IsAllPerfect || data.IsPerfectStar;
                SetBadge(0, allPerfect);
                SetBadge(1, !allPerfect && data.IsFullCombo);
            });
        }
    }
}
