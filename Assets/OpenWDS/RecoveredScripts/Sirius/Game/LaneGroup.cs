using DG.Tweening;
using OpenWDS.Runtime;
using UnityEngine;

namespace Sirius.Game
{
    // Serialization-compatible subset of Sirius.Game.LaneGroup.
    public class LaneGroup : MonoBehaviour
    {
        [SerializeField] private Transform[] _laneColliders;
        [SerializeField] private Transform _laneGroupMain;
        [SerializeField] private Transform _laneEffectParent;
        [SerializeField] private Transform _noteObjectParent;
        [SerializeField] private Transform _judgeArea;
        [SerializeField] private SpriteRenderer _laneBorderSpriteRenderer;
        [SerializeField] private SpriteRenderer _laneSpriteRenderer;
        [SerializeField] private LaneMaskScaler _laneMaskScaler;
        [SerializeField] private LaneColliderManagers _laneColliderManagers;
        [SerializeField] private LaneNoteStartLine _laneNoteStartLine;

        private float _laneBorderAlpha;
        private Sequence _splitLaneShowSequence;
        private Sequence _splitLaneHideSequence;

        public Transform LaneEffectParent => _laneEffectParent;
        public Transform NoteParent => _noteObjectParent;
        public LaneColliderManagers ColliderManagers => _laneColliderManagers;
        public int LaneCount => _laneColliders != null ? _laneColliders.Length : 0;
        public float LaneBorderAlpha => _laneBorderAlpha;
        public float LaneDarknessAlpha =>
            _laneSpriteRenderer != null ? _laneSpriteRenderer.color.a : 0f;
        public Vector3 JudgeAreaLocalPosition =>
            _judgeArea != null ? _judgeArea.localPosition : Vector3.zero;
        public float LaneScaleX =>
            _laneGroupMain != null ? _laneGroupMain.localScale.x : 0f;

        public void ApplyRecoveredSettings(int laneWidth, int laneAlphaValue)
        {
            ApplyRecoveredLaneWidth(laneWidth);
            if (_laneSpriteRenderer != null)
            {
                // Retail LaneGroup.Initialize preserves RGB and multiplies the
                // serialized renderer alpha by LaneAlphaValue / 100.
                var color = _laneSpriteRenderer.color;
                color.a *= laneAlphaValue / 100f;
                _laneSpriteRenderer.color = color;
            }
            ApplyRecoveredJudgeAreaOffset(
                RecoveredGameConfigValues.JudgeAreaOffset);
        }

        public void ApplyRecoveredJudgeAreaOffset(Vector3 judgeAreaOffset)
        {
            if (_judgeArea == null) return;
            var position = _judgeArea.localPosition;
            position.y = judgeAreaOffset.y;
            _judgeArea.localPosition = position;
        }

        public void ApplyRecoveredLaneWidth(int laneWidth)
        {
            if (_laneGroupMain != null)
            {
                var scale = _laneGroupMain.localScale;
                scale.x = RecoveredGameSettings.CalculateLaneScale(laneWidth);
                _laneGroupMain.localScale = scale;
            }
        }

        public void InitializeRecoveredSplitLaneVisual(
            Camera gameCamera = null,
            int lineOpacity = 100,
            bool showLane = true)
        {
            // Original LaneGroup.Initialize explicitly applies both of these;
            // relying on prefab/editor state makes BG_Lane disappear after scene
            // regeneration and gives transparent sprites the wrong depth order.
            if (_laneSpriteRenderer != null)
                _laneSpriteRenderer.enabled = showLane;
            if (gameCamera != null)
            {
                gameCamera.transparencySortMode = TransparencySortMode.CustomAxis;
                gameCamera.transparencySortAxis = transform.forward;
            }
            if (_laneBorderSpriteRenderer == null) return;
            _splitLaneShowSequence?.Kill();
            _splitLaneHideSequence?.Kill();

            _laneBorderAlpha = CalculateLaneBorderAlpha(lineOpacity);
            var color = _laneBorderSpriteRenderer.color;
            color.a = 0f;
            _laneBorderSpriteRenderer.color = color;
            _laneBorderSpriteRenderer.enabled = true;

            // LaneGroup.Initialize ARM64: the show tween is scheduled one second
            // after the scheduler's early Show event, then fades for one second.
            _splitLaneShowSequence = DOTween.Sequence()
                .Append(_laneBorderSpriteRenderer
                    .DOFade(_laneBorderAlpha, 1f)
                    .SetDelay(1f))
                .SetLink(_laneBorderSpriteRenderer.gameObject)
                .SetAutoKill(false)
                .Pause();
            _splitLaneHideSequence = DOTween.Sequence()
                .Append(_laneBorderSpriteRenderer.DOFade(0f, 0.5f))
                .SetLink(_laneBorderSpriteRenderer.gameObject)
                .SetAutoKill(false)
                .Pause();
        }

        public void OnSplitLane(in RecoveredSplitLaneEntry entry)
        {
            if (_splitLaneShowSequence == null || _splitLaneHideSequence == null)
                InitializeRecoveredSplitLaneVisual();
            if (_splitLaneShowSequence == null) return;

            if (entry.ShouldShow)
            {
                if (_splitLaneHideSequence.IsPlaying())
                    _splitLaneHideSequence.Pause();
                _splitLaneShowSequence.Restart();
            }
            else if (!entry.IsContinued)
            {
                if (_splitLaneShowSequence.IsPlaying())
                    _splitLaneShowSequence.Pause();
                _splitLaneHideSequence.Restart();
            }
        }

        public static float CalculateLaneBorderAlpha(int lineOpacity)
        {
            lineOpacity = Mathf.Clamp(lineOpacity, 0, 100);
            // ARM64 LaneGroup.Initialize:
            // ((SplitEffectLineOpacity * (51 - 14) / 100) + 14) / 255.
            return ((lineOpacity * 37f / 100f) + 14f) / 255f;
        }

        public Transform GetLaneCollider(int laneId)
        {
            if (_laneColliders == null || laneId < 1 || laneId > _laneColliders.Length)
            {
                throw new System.ArgumentOutOfRangeException(nameof(laneId));
            }
            return _laneColliders[laneId - 1];
        }

        private void OnDestroy()
        {
            _splitLaneShowSequence?.Kill();
            _splitLaneHideSequence?.Kill();
        }
    }
}
