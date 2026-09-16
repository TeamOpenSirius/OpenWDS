using System.Collections.Generic;
using UnityEngine;

namespace Sirius.Game.UI
{
    /// <summary>
    /// Restores the ordinary-live Sense score cut-in queue. Four original slots
    /// are cycled so closely spaced Sense events can overlap like the APK.
    /// </summary>
    public sealed class AdditionalScoreCutInPanel : MonoBehaviour
    {
        [SerializeField] private AdditionalScoreCutInController[] _commonCutIns;
        [SerializeField] private Animator[] _commonCutInAnimators;
        [SerializeField] private Animator[] _commonCutInParentAnimators;
        [SerializeField] private RectTransform[] _commonCutInParentRectTransforms;
        [SerializeField] private RectTransform _parent;

        private Sprite[] _sprites;
        private bool _isActive;
        private int _currentCutIn;
        private IReadOnlyDictionary<long, Sprite> _characterIcons;
        private bool _animationsReady;
        private Vector3 _configuredScreenPosition;

        public int ActivationCount { get; private set; }
        public bool IsActiveSenseDisplay => _isActive;
        public Vector3 ParentScreenPosition =>
            _parent != null ? _parent.position : Vector3.zero;
        public Vector3 ConfiguredScreenPosition => _configuredScreenPosition;
        public bool LastAnimationUsedOriginalStates { get; private set; }
        public bool HasVisibleCutInArtwork
        {
            get
            {
                if (_commonCutIns == null) return false;
                foreach (var cutIn in _commonCutIns)
                {
                    if (cutIn == null || !cutIn.HasArtwork) continue;
                    var group = cutIn.GetComponent<CanvasGroup>();
                    var parentGroup = cutIn.transform.parent.GetComponent<CanvasGroup>();
                    if ((group == null || group.alpha > 0.001f) &&
                        (parentGroup == null || parentGroup.alpha > 0.001f)) return true;
                }
                return false;
            }
        }

        public void Configure(
            Sprite[] sprites,
            bool isActive,
            Camera camera,
            Transform effectParent)
        {
            if (sprites == null || sprites.Length != 12)
                throw new System.ArgumentException(
                    "Four Sense types require left/right/title sprites.", nameof(sprites));
            if (camera == null)
                throw new System.ArgumentNullException(nameof(camera));
            if (effectParent == null)
                throw new System.ArgumentNullException(nameof(effectParent));
            foreach (var animator in _commonCutInAnimators) animator.enabled = false;
            foreach (var animator in _commonCutInParentAnimators) animator.enabled = false;
            _sprites = sprites;
            _isActive = isActive;
            // Zenject invokes the retail constructor after the Canvas is
            // ready. The recovery configures the prefab during scene startup,
            // so finish the pending Canvas layout before writing screen-space
            // RectTransform.position or the scaler will overwrite it.
            Canvas.ForceUpdateCanvases();
            _configuredScreenPosition = CalculateParentScreenPosition(
                camera, effectParent);
            _parent.position = _configuredScreenPosition;
            ResetPanel();
        }

        public static Vector3 CalculateParentScreenPosition(
            Camera camera,
            Transform effectParent)
        {
            // Retail Constructor temporarily uses the default offset (60) on
            // the injected Game/Effects transform, projects that world point,
            // and writes the result to this screen-space RectTransform.
            var localPosition = effectParent.localPosition;
            localPosition.y =
                OpenWDS.Runtime.GameHudRuntime.CalculatePositionY(60);
            var worldPosition = effectParent.parent != null
                ? effectParent.parent.TransformPoint(localPosition)
                : localPosition;
            var screenPosition = RectTransformUtility.WorldToScreenPoint(
                camera, worldPosition);
            return new Vector3(screenPosition.x, screenPosition.y, 0f);
        }

        public void PreparePresentation(RuntimeAnimatorController content,
            RuntimeAnimatorController parent, IReadOnlyDictionary<long, Sprite> characterIcons)
        {
            if (content == null || parent == null || characterIcons == null)
                throw new System.ArgumentException("Original Sense presentation resources are required.");
            _characterIcons = characterIcons;
            for (var slot = 0; slot < _commonCutIns.Length; slot++)
            {
                _commonCutInAnimators[slot].runtimeAnimatorController = content;
                _commonCutInParentAnimators[slot].runtimeAnimatorController = parent;
                _commonCutInAnimators[slot].enabled = true;
                _commonCutInParentAnimators[slot].enabled = true;
            }
            _animationsReady = true;
            ResetPanel();
        }

        public void OnSenseScoreAdded(int senseType, long addedScore, long characterId)
        {
            // Retail IsValidSenseType rejects Alternative and other non-display
            // types instead of clamping them to a different visual type.
            if (!_isActive || senseType < 1 || senseType > 4) return;
            if (!_animationsReady || !_characterIcons.TryGetValue(characterId, out var icon) || icon == null)
                throw new System.InvalidOperationException("Sense resources were not prepared for " + characterId);
            var typeIndex = senseType - 1;
            var slot = _currentCutIn;
            _commonCutIns[slot].SetValue(_sprites[typeIndex * 3],
                _sprites[typeIndex * 3 + 1], _sprites[typeIndex * 3 + 2], icon, addedScore);
            _commonCutInParentRectTransforms[slot].SetAsLastSibling();
            _commonCutInAnimators[slot].SetTrigger("OnStart");
            _commonCutInParentAnimators[slot].SetTrigger("Initialize");
            _commonCutInAnimators[slot].Update(0f);
            // PlayAnimation (0xba09514): move the previous visible item up,
            // fade the second previous, initialize the third previous.
            for (var offset = 1; offset <= 3; offset++)
            {
                var before = (slot - offset + _commonCutIns.Length) % _commonCutIns.Length;
                if (_commonCutInAnimators[before].GetCurrentAnimatorStateInfo(0).IsName("Init")) continue;
                if (offset == 3) InitializeCutIn(before);
                else _commonCutInParentAnimators[before].SetTrigger(offset == 1 ? "MoveUp" : "FadeOut");
            }
            _currentCutIn = (slot + 1) % _commonCutIns.Length;
            UpdateOriginalAnimationStateValidation(slot);
            ActivationCount++;
        }

        private void InitializeCutIn(int slot)
        {
            _commonCutInAnimators[slot].SetTrigger("Initialize");
            _commonCutInParentAnimators[slot].SetTrigger("Initialize");
        }

        private void UpdateOriginalAnimationStateValidation(int slot)
        {
            LastAnimationUsedOriginalStates =
                _commonCutInParentAnimators != null &&
                slot < _commonCutInParentAnimators.Length &&
                _commonCutInParentAnimators[slot] != null &&
                _commonCutInParentAnimators[slot]
                    .GetCurrentAnimatorStateInfo(0).IsName("Init") &&
                _commonCutInAnimators != null &&
                slot < _commonCutInAnimators.Length &&
                _commonCutInAnimators[slot] != null &&
                _commonCutInAnimators[slot]
                    .GetCurrentAnimatorStateInfo(0).IsName("FadeIn");
        }

        public void ResetPanel()
        {
            _currentCutIn = 0;
            ActivationCount = 0;
            LastAnimationUsedOriginalStates = false;
            if (_commonCutIns == null) return;
            for (var slot = 0; slot < _commonCutIns.Length; slot++)
            {
                _commonCutIns[slot].Initialize();
                if (_animationsReady)
                {
                    InitializeCutIn(slot);
                    _commonCutInAnimators[slot].Update(0f);
                    _commonCutInParentAnimators[slot].Update(0f);
                }
                else
                {
                    var group = _commonCutIns[slot].GetComponent<CanvasGroup>();
                    if (group != null) group.alpha = 0f;
                }
            }
        }
    }
}
