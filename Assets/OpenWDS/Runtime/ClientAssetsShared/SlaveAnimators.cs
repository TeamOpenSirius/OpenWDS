using System.Collections.Generic;
using UnityEngine;

namespace Sirius
{
    // SlaveAnimators.arm64.txt: append preserves order and duplicates. Each
    // setter forwards to every entry, without filtering or changing parameters.
    public sealed class SlaveAnimators : MonoBehaviour
    {
        private readonly List<Animator> _slaveAnimators = new List<Animator>();
        public void Append(Animator animator) => _slaveAnimators.Add(animator);
        public void SetTrigger(int id)
        {
            foreach (var animator in _slaveAnimators) animator.SetTrigger(id);
        }
        public void SetInteger(int id, int value)
        {
            foreach (var animator in _slaveAnimators) animator.SetInteger(id, value);
        }
        public void SetFloat(int id, float value)
        {
            foreach (var animator in _slaveAnimators) animator.SetFloat(id, value);
        }
        public void SetBool(int id, bool value)
        {
            foreach (var animator in _slaveAnimators) animator.SetBool(id, value);
        }
    }
}
