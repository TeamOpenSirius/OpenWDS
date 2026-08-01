using System.Collections.Generic;
using Sirius.Animations;
using UnityEngine;

namespace Sirius.Game
{
    /// <summary>
    /// Serialization-compatible recovery of the controller embedded in the
    /// original online SplitEffects prefabs. Initialization and split positions
    /// follow SplitEffectController ARM64 at 0x59796F8/0x5979C28.
    /// </summary>
    public sealed class SplitEffectController : MonoBehaviour
    {
        private const float LineHeight = 27f;
        private const float LineOffsetPosition = -5f;
        private const float EffectOffsetPosition = 2.5f;

        [SerializeField] private SplitEffectElement[] _splitEffectElements;
        [SerializeField] private Animator _splitEffectAnimator;
        [SerializeField] private SpriteRenderer[] _splitLines;
        [SerializeField] private AnimationExitActionTrigger _animationExitTrigger;

        private readonly Dictionary<int, SplitEffectElement> _splitEffects =
            new Dictionary<int, SplitEffectElement>();
        private readonly Dictionary<int, Transform> _splitLineTransforms =
            new Dictionary<int, Transform>();
        private bool _splitEffectLightSetting;
        private Transform _cachedTransform;

        public void Initialize(
            bool splitEffectLightSetting,
            int splitEffectLineOpacitySettings,
            in int minOpacityValue,
            in int maxOpacityValue)
        {
            if (_cachedTransform != null) return;
            _cachedTransform = transform;
            _splitEffectLightSetting = splitEffectLightSetting;

            var scale = _cachedTransform.localScale;
            scale.y = LineHeight;
            _cachedTransform.localScale = scale;
            if (_splitEffectAnimator != null)
            {
                var animatorPosition = _splitEffectAnimator.transform.localPosition;
                animatorPosition.y = LineOffsetPosition;
                _splitEffectAnimator.transform.localPosition = animatorPosition;
            }

            var lineCount = _splitLines != null ? _splitLines.Length : 0;
            var opacity = Mathf.Clamp(
                splitEffectLineOpacitySettings, minOpacityValue, maxOpacityValue) / 100f;
            for (var index = 0; index < lineCount; index++)
            {
                var line = _splitLines[index];
                if (line == null) continue;
                _splitLineTransforms[index] = line.transform;

                if (_splitEffectElements == null || _splitEffectElements.Length == 0)
                    continue;
                var source = _splitEffectElements[index % _splitEffectElements.Length];
                if (source == null) continue;
                // ARM64 Initialize obtains source.gameObject, then calls the
                // Instantiate(original, parent) overload with this line's
                // Transform. The bundled systems emit over distance, so this
                // parent relationship is required: moving a split line moves
                // its emitter and produces the original particle burst.
                var element = Instantiate(source, line.transform);
                element.name = "SplitEffectElement_" + (index + 1);
                var elementPosition = element.transform.localPosition;
                elementPosition.y = EffectOffsetPosition;
                element.transform.localPosition = elementPosition;
                var lineColor = element.LineColor;
                lineColor.r *= opacity;
                lineColor.g *= opacity;
                lineColor.b *= opacity;
                line.color = lineColor;
                if (element.LineEffects != null)
                {
                    foreach (var particle in element.LineEffects)
                        if (particle != null)
                            particle.Stop(
                                true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                _splitEffects[index] = element;
            }
        }

        public void OnFadeIn(int splitCount, int splitLaneType)
        {
            EnsureInitialized();
            splitCount = Mathf.Clamp(splitCount, 1, 6);
            gameObject.SetActive(true);

            if (_splitLines != null)
            {
                for (var index = 0; index < _splitLines.Length; index++)
                    if (_splitLines[index] != null)
                        _splitLines[index].gameObject.SetActive(index <= splitCount);
            }

            // Original branch: Ignore(7), Light(5), and the global light setting
            // suppress particles. BothEnds(1) plays only the outer elements.
            if (splitLaneType != 7 && splitLaneType != 5 &&
                !_splitEffectLightSetting)
            {
                for (var index = 0; index <= splitCount; index++)
                {
                    if (splitLaneType == 1 && index != 0 && index != splitCount)
                        continue;
                    if (!_splitEffects.TryGetValue(index, out var element) ||
                        element == null || element.LineEffects == null)
                        continue;
                    foreach (var particle in element.LineEffects)
                    {
                        if (particle == null) continue;
                        if (particle.isPlaying) particle.Stop(true);
                        particle.Play(true);
                    }
                }
            }

            if (_splitEffectAnimator != null)
            {
                _splitEffectAnimator.enabled = true;
                _splitEffectAnimator.Play("SplitEffect_fadeIn_anim", 0, 0f);
            }

            ApplyOriginalSplitPositions(splitCount);
        }

        public void OnFadeOut()
        {
            if (_splitEffectAnimator != null)
                _splitEffectAnimator.Play("SplitEffect_fadeOut_anim", 0, 0f);
        }

        public void Clear()
        {
            foreach (var element in _splitEffects.Values)
            {
                if (element == null || element.LineEffects == null) continue;
                foreach (var particle in element.LineEffects)
                    if (particle != null) particle.Stop(true);
            }
            gameObject.SetActive(false);
        }

        private void EnsureInitialized()
        {
            if (_cachedTransform != null) return;
            var minimumOpacity = 0;
            var maximumOpacity = 100;
            Initialize(false, 100, in minimumOpacity, in maximumOpacity);
        }

        private void ApplyOriginalSplitPositions(int splitCount)
        {
            // Literal table recovered from the six ARM64 switch cases. The total
            // width is twelve lanes (11.1 units). Values stay on the original
            // twelve-lane boundaries rather than advancing from the left edge.
            switch (splitCount)
            {
                case 1:
                    SetLineX(0, -5.55f); SetLineX(1, 5.55f);
                    break;
                case 2:
                    SetLineX(0, -5.55f); SetLineX(1, 0f);
                    SetLineX(2, 5.55f);
                    break;
                case 3:
                    SetLineX(0, -5.55f); SetLineX(1, -1.85f);
                    SetLineX(2, 1.85f); SetLineX(3, 5.55f);
                    break;
                case 4:
                    SetLineX(0, -5.55f); SetLineX(1, -2.775f);
                    SetLineX(2, 0f); SetLineX(3, 2.775f);
                    SetLineX(4, 5.55f);
                    break;
                case 5:
                    SetLineX(0, -5.55f); SetLineX(1, -2.775f);
                    SetLineX(2, -0.925f); SetLineX(3, 0.925f);
                    SetLineX(4, 2.775f); SetLineX(5, 5.55f);
                    break;
                case 6:
                    SetLineX(0, -5.55f); SetLineX(1, -3.7f);
                    SetLineX(2, -1.85f); SetLineX(3, 0f);
                    SetLineX(4, 1.85f); SetLineX(5, 3.7f);
                    SetLineX(6, 5.55f);
                    break;
            }
        }

        private void SetLineX(int index, float value)
        {
            if (!_splitLineTransforms.TryGetValue(index, out var target) ||
                target == null) return;
            var position = target.localPosition;
            position.x = value;
            target.localPosition = position;
        }
    }
}
