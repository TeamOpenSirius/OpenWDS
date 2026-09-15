using UnityEngine;

namespace Sirius
{
    // Current ELF OnStateEnter=0x59717a0, OnStateUpdate=0x5971878.
    public sealed class ExampleStateBehaviour : StateMachineBehaviour
    {
        [SerializeField] private int _loopMin = 1;
        [SerializeField] private int _loopMax = 1;
        [SerializeField] private int _branchesNum = 1;
        private readonly int _goToNext = Animator.StringToHash("GoToNext");
        private readonly int _nextBranch = Animator.StringToHash("NextBranch");
        private int _loopTimes;
        private SlaveAnimators _slaveAnimators;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            _loopTimes = Random.Range(_loopMin, _loopMax + 1);
            if (_slaveAnimators == null) _slaveAnimators = animator.gameObject.GetComponent<SlaveAnimators>();
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (_loopTimes >= 1 && stateInfo.normalizedTime >= _loopTimes)
            {
                var branch = Random.Range(0, _branchesNum);
                animator.SetInteger(_nextBranch, branch);
                animator.SetTrigger(_goToNext);
                if (_slaveAnimators != null)
                {
                    _slaveAnimators.SetInteger(_nextBranch, branch);
                    _slaveAnimators.SetTrigger(_goToNext);
                    _loopTimes = 0;
                }
            }
            else if (_slaveAnimators != null)
            {
                // This forwarding also occurs before reaching the loop bound
                // in the original; it is not gated by the host trigger above.
                _slaveAnimators.SetTrigger(_goToNext);
            }
        }
    }
}
