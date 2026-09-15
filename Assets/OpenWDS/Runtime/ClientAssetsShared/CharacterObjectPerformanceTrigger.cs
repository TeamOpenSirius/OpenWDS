using System;
using Sirius.Scarab;
using UnityEngine;

namespace Sirius.CharacterModel
{
    // Recovered explicit motion/sequence API. The separate home random timer
    // (SetRandomMotionPlay, UniRx) is not implemented or invoked by this slice.
    public class CharacterObjectPerformanceTrigger : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private RuntimeAnimatorController _homeAnimatorController;
        [SerializeField] private RuntimeAnimatorController _theatreAnimatorController;
        [SerializeField] private MaterialParameterSetter _materialParameterSetter;
        private RuntimeAnimatorController _photoAnimatorController;
        private CharacterObjectPerformance _characterObjectPerformance;
        private SlaveAnimators _slaveAnimators;
        private int _mouthLayer;
        private readonly int _motionStateId = Animator.StringToHash("MotionState");
        private readonly int _playTriggerId = Animator.StringToHash("PlayTrigger");
        public Animator Animator => _animator;

        public void Initialize(CharacterObjectPerformanceConst.AnimatorTypes type)
        {
            if (_animator == null) return;
            _animator.runtimeAnimatorController = type == CharacterObjectPerformanceConst.AnimatorTypes.Home
                ? _homeAnimatorController : _animator.runtimeAnimatorController;
            if (_animator.runtimeAnimatorController == null) _animator.runtimeAnimatorController = _homeAnimatorController;
            InitializePerformance();
        }
        public void Initialize(RuntimeAnimatorController controller)
        {
            if (_animator == null) return;
            _animator.runtimeAnimatorController = controller;
            InitializePerformance();
        }
        // Common statements of both original Initialize overloads.
        private void InitializePerformance()
        {
            _mouthLayer = _animator.GetLayerIndex("Mouth Layer");
            if (_characterObjectPerformance == null) _characterObjectPerformance = new CharacterObjectPerformance(_animator);
            _animator.keepAnimatorStateOnDisable = true;
            _animator.TryGetComponent(out _slaveAnimators);
        }
        public void AppendSlave(Animator animator)
        {
            if (_slaveAnimators != null) _slaveAnimators.Append(animator);
        }
        public void StartSequence(RuntimeAnimatorController controller, int sequenceIndex)
        {
            _animator.runtimeAnimatorController = controller;
            _animator.SetInteger(_motionStateId, sequenceIndex + 1);
            _animator.SetTrigger(_playTriggerId);
        }
        public void SetMotionState(CharacterObjectPerformanceConst.MotionState value) => _characterObjectPerformance.SetMotionState(value);
        public void PlayMotion() => _characterObjectPerformance.PlayMotion();
        public void ResetMotionId() => _characterObjectPerformance.ResetMotionId();
        public void OnPlayPhotoResultMotion() => _characterObjectPerformance.OnPlayPhotoResultMotion();
        public void PlayCostumeMotion() => _characterObjectPerformance.PlayCostumeMotion();
        public void ResetTrigger() => _characterObjectPerformance.ResetTrigger();
        public void ResetPhotoResultTrigger() => _characterObjectPerformance.ResetPhotoResultTrigger();
        public void ResetCostumeMotionTrigger() => _characterObjectPerformance.ResetCostumeMotionTrigger();
        public void MouthMotionPlay(string mouthMotion)
        {
            if (_animator == null) return;
            var motion = Enum.TryParse<CharacterObjectPerformanceConst.HomeCharacterMouthMotion>(mouthMotion, out _)
                ? Enum.Parse<CharacterObjectPerformanceConst.HomeCharacterMouthMotion>(mouthMotion)
                : CharacterObjectPerformanceConst.HomeCharacterMouthMotion.Idle;
            _animator.SetLayerWeight(_mouthLayer, 1);
            string name;
            switch (motion)
            {
                case CharacterObjectPerformanceConst.HomeCharacterMouthMotion.Idle: name = "Idel"; break;
                case CharacterObjectPerformanceConst.HomeCharacterMouthMotion.SmallMouthMotion: name = "Talk_Mouth_01"; break;
                case CharacterObjectPerformanceConst.HomeCharacterMouthMotion.MediumMouthMotion: name = "Talk_Mouth_02"; break;
                default: throw new ArgumentOutOfRangeException(nameof(mouthMotion), mouthMotion, null);
            }
            if (name == "Idel") MouthMotionStop();
            else _animator.Play(name, _mouthLayer);
        }
        public void MouthMotionStop()
        {
            if (_animator != null) _animator.SetLayerWeight(_mouthLayer, 0);
        }
        public void SetDirLight(Light light)
        {
            if (_materialParameterSetter != null) _materialParameterSetter.SetLight(light);
        }
    }
}
