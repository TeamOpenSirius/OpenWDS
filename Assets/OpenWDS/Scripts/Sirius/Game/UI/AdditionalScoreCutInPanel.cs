using System.Collections;
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
        private Coroutine[] _running;
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
                    if (group == null || group.alpha > 0.001f) return true;
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
            _sprites = sprites;
            _isActive = isActive;
            _running = new Coroutine[_commonCutIns != null
                ? _commonCutIns.Length
                : 0];
            if (_commonCutInParentRectTransforms != null)
                foreach (var rect in _commonCutInParentRectTransforms)
                    if (rect != null &&
                        rect.GetComponent<
                            AdditionalScoreCutInAnimationEventReceiver>() == null)
                        rect.gameObject.AddComponent<
                            AdditionalScoreCutInAnimationEventReceiver>();
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

        public void OnSenseScoreAdded(int senseType, long addedScore)
        {
            if (!_isActive || _sprites == null || _commonCutIns == null ||
                _commonCutIns.Length == 0)
                return;
            var typeIndex = Mathf.Clamp(senseType, 1, 4) - 1;
            var slot = _currentCutIn++ % _commonCutIns.Length;
            var controller = _commonCutIns[slot];
            controller.SetValue(
                _sprites[typeIndex * 3],
                _sprites[typeIndex * 3 + 1],
                _sprites[typeIndex * 3 + 2],
                null,
                addedScore);

            if (_commonCutInParentRectTransforms != null &&
                slot < _commonCutInParentRectTransforms.Length)
            {
                var rect = _commonCutInParentRectTransforms[slot];
                rect.SetAsLastSibling();
                rect.gameObject.SetActive(false);
                rect.gameObject.SetActive(true);
            }
            if (_commonCutInParentAnimators != null &&
                slot < _commonCutInParentAnimators.Length &&
                _commonCutInParentAnimators[slot] != null)
            {
                _commonCutInParentAnimators[slot].Play(
                    "CutIn_anim", 0, 0f);
                _commonCutInParentAnimators[slot].Update(0f);
            }
            if (_commonCutInAnimators != null &&
                slot < _commonCutInAnimators.Length &&
                _commonCutInAnimators[slot] != null)
            {
                _commonCutInAnimators[slot].Play("Wait", 0, 0f);
                _commonCutInAnimators[slot].SetTrigger("Animate");
                _commonCutInAnimators[slot].Update(0f);
            }
            UpdateOriginalAnimationStateValidation(slot);
            if (_running[slot] != null) StopCoroutine(_running[slot]);
            _running[slot] = StartCoroutine(PlayParentCurve(slot));
            ActivationCount++;
        }

        private void UpdateOriginalAnimationStateValidation(int slot)
        {
            LastAnimationUsedOriginalStates =
                _commonCutInParentAnimators != null &&
                slot < _commonCutInParentAnimators.Length &&
                _commonCutInParentAnimators[slot] != null &&
                _commonCutInParentAnimators[slot]
                    .GetCurrentAnimatorStateInfo(0).IsName("CutIn_anim") &&
                _commonCutInAnimators != null &&
                slot < _commonCutInAnimators.Length &&
                _commonCutInAnimators[slot] != null &&
                _commonCutInAnimators[slot]
                    .GetCurrentAnimatorStateInfo(0).IsName("SenceCutIn_anim");
        }

        public void ResetPanel()
        {
            _currentCutIn = 0;
            ActivationCount = 0;
            LastAnimationUsedOriginalStates = false;
            if (_commonCutIns != null)
                foreach (var cutIn in _commonCutIns)
                    if (cutIn != null)
                    {
                        cutIn.Initialize();
                        var group = cutIn.GetComponent<CanvasGroup>();
                        if (group != null) group.alpha = 0f;
                    }
        }

        private IEnumerator PlayParentCurve(int slot)
        {
            var rect = _commonCutInParentRectTransforms[slot];
            var parentGroup = rect.GetComponent<CanvasGroup>();
            var contentGroup = _commonCutIns[slot].GetComponent<CanvasGroup>();
            var basePosition = rect.anchoredPosition;
            if (parentGroup != null) parentGroup.alpha = 1f;
            if (contentGroup != null) contentGroup.alpha = 1f;

            // CutIn_anim: x 320→0 at 0.1666667s, content fades by 0.5s,
            // parent holds to 0.6666667s then fades through its 1.1666666s end.
            var elapsed = 0f;
            const float end = 1.1666666f;
            while (elapsed < end)
            {
                elapsed += Time.deltaTime;
                UpdateOriginalAnimationStateValidation(slot);
                var slide = Mathf.Clamp01(elapsed / 0.1666667f);
                rect.anchoredPosition = new Vector2(
                    basePosition.x + Mathf.Lerp(320f, 0f, slide),
                    basePosition.y);
                if (contentGroup != null)
                    contentGroup.alpha = elapsed <= 0.1666667f
                        ? 1f
                        : 1f - Mathf.Clamp01(
                            (elapsed - 0.1666667f) / 0.3333333f);
                if (parentGroup != null)
                    parentGroup.alpha = elapsed <= 0.6666667f
                        ? 1f
                        : 1f - Mathf.Clamp01(
                            (elapsed - 0.6666667f) / 0.5f);
                yield return null;
            }
            rect.anchoredPosition = basePosition;
            if (parentGroup != null) parentGroup.alpha = 0f;
            if (contentGroup != null) contentGroup.alpha = 0f;
            _running[slot] = null;
        }
    }
}
