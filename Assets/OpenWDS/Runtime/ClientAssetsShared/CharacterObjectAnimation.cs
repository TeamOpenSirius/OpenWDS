using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sirius.CharacterModel
{
    public class CharacterObjectAnimation
    {
        private const int DefaultMotionId = 999;
        private readonly Animator _animator;
        private readonly Dictionary<HomeAnimatorParameter, string> _homeAnimatorParameterStrings;
        private readonly Dictionary<PhotoResultAnimatorParameter, string> _photoResultAnimatorParameterStrings;
        private bool _isPlayingMotion;
        private int _motionStateId = DefaultMotionId;
        private enum HomeAnimatorParameter { MotionState, PlayTrigger, CostumeMotionTrigger }
        private enum PhotoResultAnimatorParameter { PlayTrigger }

        public CharacterObjectAnimation(Animator animator)
        {
            _animator = animator;
            _homeAnimatorParameterStrings = Enum.GetValues(typeof(HomeAnimatorParameter))
                .Cast<HomeAnimatorParameter>().ToDictionary(value => value, ToStringCached);
            _photoResultAnimatorParameterStrings = Enum.GetValues(typeof(PhotoResultAnimatorParameter))
                .Cast<PhotoResultAnimatorParameter>().ToDictionary(value => value, ToStringCached);
        }
        public void SetMotionState(int value)
        {
            if (_motionStateId == value) return;
            _motionStateId = value;
            _isPlayingMotion = false;
            SetIntegerParameter(HomeAnimatorParameter.MotionState, value);
        }
        public void PlayMotion()
        {
            if (_isPlayingMotion) return;
            _isPlayingMotion = true;
            SetTriggerParameter(HomeAnimatorParameter.PlayTrigger);
        }
        public void ResetMotionId() => _motionStateId = DefaultMotionId;
        public void ResetTrigger() => ResetTriggerParameter(HomeAnimatorParameter.PlayTrigger);
        public void OnPlayPhotoResultMotion() => SetTriggerParameter(PhotoResultAnimatorParameter.PlayTrigger);
        public void ResetPhotoResultTrigger() => ResetTriggerParameter(PhotoResultAnimatorParameter.PlayTrigger);
        public void PlayCostumeMotion() => SetTriggerParameter(HomeAnimatorParameter.CostumeMotionTrigger);
        public void ResetCostumeMotionTrigger() => ResetTriggerParameter(HomeAnimatorParameter.CostumeMotionTrigger);
        private void SetIntegerParameter(HomeAnimatorParameter parameter, int value) =>
            _animator.SetInteger(_homeAnimatorParameterStrings[parameter], value);
        private void SetTriggerParameter(HomeAnimatorParameter parameter) =>
            _animator.SetTrigger(_homeAnimatorParameterStrings[parameter]);
        private void SetTriggerParameter(PhotoResultAnimatorParameter parameter) =>
            _animator.SetTrigger(_photoResultAnimatorParameterStrings[parameter]);
        private void ResetTriggerParameter(HomeAnimatorParameter parameter) =>
            _animator.ResetTrigger(_homeAnimatorParameterStrings[parameter]);
        private void ResetTriggerParameter(PhotoResultAnimatorParameter parameter) =>
            _animator.ResetTrigger(_photoResultAnimatorParameterStrings[parameter]);
        private string ToStringCached(HomeAnimatorParameter pageCategory)
        {
            switch (pageCategory)
            {
                case HomeAnimatorParameter.MotionState: return "MotionState";
                case HomeAnimatorParameter.PlayTrigger: return "PlayTrigger";
                case HomeAnimatorParameter.CostumeMotionTrigger: return "CostumeMotionTrigger";
                default: throw new ArgumentOutOfRangeException(nameof(pageCategory), pageCategory, null);
            }
        }
        private string ToStringCached(PhotoResultAnimatorParameter pageCategory)
        {
            if (pageCategory == PhotoResultAnimatorParameter.PlayTrigger) return "PlayTrigger";
            throw new ArgumentOutOfRangeException(nameof(pageCategory), pageCategory, null);
        }
    }
}
