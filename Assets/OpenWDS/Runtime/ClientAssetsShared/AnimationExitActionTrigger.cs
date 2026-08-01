using System;
using UnityEngine;

namespace Sirius.Animations
{
    // Serialization-compatible class required by the online SplitEffect prefabs.
    public sealed class AnimationExitActionTrigger : MonoBehaviour
    {
        public event Action Exited;

        // Animation events in different client revisions used both spellings.
        public void OnAnimationExit() => Exited?.Invoke();
        public void AnimationExit() => Exited?.Invoke();
    }
}
