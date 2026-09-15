using UnityEngine;
using UnityEngine.UI;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Local adapter for Sirius.ScrollText. The retail implementation scrolls at
    /// 120 px/s after a one-second delay and wraps from the viewport's right edge.
    /// </summary>
    public sealed class ScrollTextRuntime : MonoBehaviour
    {
        private const float InitialDelaySeconds = 1f;
        private const float AppearanceSeconds = 0.5f;
        private const float AppearanceIntervalSeconds = 1f;
        private const float ScrollPixelsPerSecond = 120f;

        private RectTransform _viewport;
        private RectTransform _bodyRect;
        private Text _bodyText;
        private bool _animationRequested;
        private bool _cubicAppearance;
        private float _elapsed;

        public Text BodyText => _bodyText;
        public bool IsScrollable =>
            _viewport != null &&
            _bodyRect != null &&
            BodyWidth > _viewport.rect.width + 0.01f;
        public bool IsAnimating => _animationRequested && IsScrollable;

        private float BodyWidth =>
            Mathf.Max(
                _bodyRect != null ? _bodyRect.rect.width : 0f,
                _bodyText != null ? _bodyText.preferredWidth : 0f);

        public void Configure(Text bodyText, bool cubicAppearance = false)
        {
            var changed = _bodyText != bodyText;
            _viewport = transform as RectTransform;
            _bodyText = bodyText;
            _bodyRect = bodyText != null ? bodyText.rectTransform : null;
            _cubicAppearance = cubicAppearance;
            if (changed) Restart();
        }

        public void SetAnimation(bool enabled)
        {
            if (_animationRequested == enabled) return;
            _animationRequested = enabled;
            Restart();
        }

        public void RestartForNewContent()
        {
            Restart();
        }

        private void OnDisable()
        {
            ResetPosition();
        }

        private void OnDestroy()
        {
            ResetPosition();
        }

        private void Restart()
        {
            _elapsed = 0f;
            ResetPosition();
        }

        private void Update()
        {
            if (!_animationRequested || !IsScrollable)
            {
                if (_bodyRect != null &&
                    Mathf.Abs(_bodyRect.anchoredPosition.x) > 0.01f)
                    ResetPosition();
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < InitialDelaySeconds) return;

            var bodyWidth = BodyWidth;
            var viewportWidth = _viewport.rect.width;
            var scrollDuration = bodyWidth / ScrollPixelsPerSecond;
            var returnDuration = _cubicAppearance
                ? AppearanceSeconds
                : viewportWidth / ScrollPixelsPerSecond;
            var cycleDuration = scrollDuration + returnDuration +
                (_cubicAppearance ? AppearanceIntervalSeconds : 0f);
            var cycleTime = Mathf.Repeat(
                _elapsed - InitialDelaySeconds,
                cycleDuration);
            if (cycleTime < scrollDuration)
            {
                SetBodyPositionX(Mathf.LerpUnclamped(
                    0f,
                    -bodyWidth,
                    cycleTime / scrollDuration));
                return;
            }

            cycleTime -= scrollDuration;
            if (cycleTime < returnDuration)
            {
                var t = cycleTime / returnDuration;
                // ScrollAnimationType.LinearScrollAndCubicAppearance uses
                // DOTween Ease.InCubic for the right-edge appearance segment.
                if (_cubicAppearance) t = t * t * t;
                SetBodyPositionX(Mathf.LerpUnclamped(viewportWidth, 0f, t));
                return;
            }
            SetBodyPositionX(0f);
        }

        private void ResetPosition()
        {
            if (_bodyRect != null) SetBodyPositionX(0f);
        }

        private void SetBodyPositionX(float x)
        {
            var position = _bodyRect.anchoredPosition;
            position.x = x;
            _bodyRect.anchoredPosition = position;
        }
    }
}
