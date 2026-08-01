using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sirius.Game.UI
{
    /// <summary>
    /// Runtime-compatible restoration of the original AdditionalScoreCutIn
    /// serialized component. The original prefab supplies all artwork.
    /// </summary>
    public sealed class AdditionalScoreCutInController : MonoBehaviour
    {
        [SerializeField] private Image _charaIcon;
        [SerializeField] private Image _leftImage;
        [SerializeField] private Image _rightImage;
        [SerializeField] private Image _senseActiveImage;
        [SerializeField] private TextMeshProUGUI _score;

        public bool HasArtwork =>
            _leftImage != null && _leftImage.sprite != null &&
            _rightImage != null && _rightImage.sprite != null &&
            _senseActiveImage != null && _senseActiveImage.sprite != null &&
            _score != null && !string.IsNullOrEmpty(_score.text);

        public void Initialize()
        {
            if (_charaIcon != null)
                _charaIcon.enabled = _charaIcon.sprite != null;
        }

        public void SetValue(
            Sprite left, Sprite right, Sprite senseActive,
            Sprite characterIcon, long pointValue)
        {
            if (_leftImage != null) _leftImage.sprite = left;
            if (_rightImage != null) _rightImage.sprite = right;
            if (_senseActiveImage != null) _senseActiveImage.sprite = senseActive;
            if (_charaIcon != null)
            {
                _charaIcon.sprite = characterIcon;
                // Character-card icons belong to the account/character asset
                // provider. Keep the local common cut-in valid when it is absent.
                _charaIcon.enabled = characterIcon != null;
            }
            if (_score != null) _score.text = pointValue.ToString();
        }

        // Receiver retained by the original SenceCutIn_anim clip. Slot
        // visibility/reset is owned by AdditionalScoreCutInPanel.
        public void OnExit()
        {
        }
    }
}
