using UnityEngine;

namespace Sirius.Game
{
    // Serialization-compatible subset of Sirius.Game.LaneEffectController.
    public class LaneEffectController : MonoBehaviour
    {
        [SerializeField] private Transform _splitEffectParent;

        public Transform SplitEffectParent => _splitEffectParent;
    }
}
