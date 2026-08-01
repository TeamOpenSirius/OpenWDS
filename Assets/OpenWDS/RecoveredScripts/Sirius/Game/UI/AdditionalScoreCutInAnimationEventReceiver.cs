using UnityEngine;

namespace Sirius.Game.UI
{
    /// <summary>
    /// Receives the OnCompleted event embedded in the original CutIn_anim.
    /// The exported prefab lost the runtime receiver; panel slot/reset state is
    /// still controlled by AdditionalScoreCutInPanel.
    /// </summary>
    public sealed class AdditionalScoreCutInAnimationEventReceiver : MonoBehaviour
    {
        public void OnCompleted()
        {
        }
    }
}
