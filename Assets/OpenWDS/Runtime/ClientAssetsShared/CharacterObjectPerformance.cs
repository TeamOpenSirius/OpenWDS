using UnityEngine;

namespace Sirius.CharacterModel
{
    public class CharacterObjectPerformance
    {
        private readonly Animator _animator;
        private readonly CharacterObjectAnimation _animation;
        public CharacterObjectPerformance(Animator animator)
        {
            _animator = animator;
            _animation = new CharacterObjectAnimation(animator);
        }
        public void SetMotionState(CharacterObjectPerformanceConst.MotionState value) => _animation.SetMotionState((int)value);
        public void PlayMotion() => _animation.PlayMotion();
        public void ResetMotionId() => _animation.ResetMotionId();
        public void OnPlayPhotoResultMotion() => _animation.OnPlayPhotoResultMotion();
        public void PlayCostumeMotion() => _animation.PlayCostumeMotion();
        public void ResetCostumeMotionTrigger() => _animation.ResetCostumeMotionTrigger();
        public void ResetTrigger() => _animation.ResetTrigger();
        public void ResetPhotoResultTrigger() => _animation.ResetPhotoResultTrigger();
    }
}
