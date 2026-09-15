using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sirius.Game.UI
{
    /// <summary>Recovered from Sirius.Game.UI.PrincipalGauge ARM64.</summary>
    public class PrincipalGauge : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _currentPrincipalCount;
        [SerializeField] private TextMeshProUGUI _maxPrincipalCount;
        [SerializeField] private Slider _gauge;

        public TextMeshProUGUI CurrentPrincipalCount => _currentPrincipalCount;
        public TextMeshProUGUI MaxPrincipalCount => _maxPrincipalCount;
        public Slider Gauge => _gauge;

        public void SetPrincipalValue(int principalValue, int maxPrincipalValue)
        {
            _currentPrincipalCount.SetText(principalValue.ToString());
            _maxPrincipalCount.SetText(maxPrincipalValue.ToString());
            _gauge.maxValue = maxPrincipalValue;
            _gauge.minValue = 0f;
            _gauge.value = principalValue;
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
