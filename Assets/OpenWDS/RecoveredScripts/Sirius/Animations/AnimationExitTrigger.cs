using System;
using UnityEngine;

namespace Sirius.Animations
{
    public sealed class AnimationExitTrigger : MonoBehaviour
    {
        /// <summary>
        /// Raised by the original animation clip's terminal AnimationEvent.
        /// The retail component exposes the same OnExit entry point and
        /// publishes it through an observable.
        /// </summary>
        public event Action Exited;

        public void OnExit()
        {
            Exited?.Invoke();
        }
    }
}
