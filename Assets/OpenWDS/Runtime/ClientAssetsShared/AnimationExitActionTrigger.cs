using System;
using UnityEngine;

namespace Sirius.Animations
{
    // Current ARM64: SetExitAction 0x5984a8c, OnExit 0x5984acc.
    public sealed class AnimationExitActionTrigger : MonoBehaviour
    {
        private Action _exitAction;
        public void SetExitAction(Action exitAction) => _exitAction = exitAction;
        public void OnExit() => _exitAction?.Invoke();
        // The original unused OnExitAsObservable throws NotImplementedException.
        // Its UniRx signature is not introduced into this explicit callback path.
    }
}
