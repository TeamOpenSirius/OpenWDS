using System;
using UnityEngine;

namespace Sirius.Game
{
    public enum RecoveredBoundaryClearType
    {
        Failed,
        Clear,
        FullCombo,
        AllPerfect,
    }

    public sealed class GameResultPanel : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        private float _startedAt;

        public RecoveredBoundaryClearType ClearType { get; private set; }
        public bool IsPlaying { get; private set; }
        public float NormalizedTime => !IsPlaying
            ? 0f
            : Mathf.Clamp01(
                (Time.realtimeSinceStartup - _startedAt) /
                GetAuthoredDuration(ClearType));

        public float Show(RecoveredBoundaryClearType clearType)
        {
            if (_animator == null)
                throw new InvalidOperationException(
                    "ClearAnimation Animator is missing.");
            ClearType = clearType;
            gameObject.SetActive(true);
            _animator.enabled = true;
            _animator.Rebind();
            _animator.Update(0f);
            _animator.SetTrigger(GetTrigger(clearType));
            _startedAt = Time.realtimeSinceStartup;
            IsPlaying = true;
            return GetAuthoredDuration(clearType);
        }

        public void Hide()
        {
            IsPlaying = false;
            gameObject.SetActive(false);
        }

        public static string GetTrigger(RecoveredBoundaryClearType clearType)
        {
            return clearType switch
            {
                RecoveredBoundaryClearType.Failed => "ToFailed",
                RecoveredBoundaryClearType.Clear => "ToClear",
                RecoveredBoundaryClearType.FullCombo => "ToFullCombo",
                RecoveredBoundaryClearType.AllPerfect => "ToAllPerfect",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(clearType), clearType, null),
            };
        }

        public static float GetAuthoredDuration(
            RecoveredBoundaryClearType clearType)
        {
            // Stop times from the retail ClearAnimation clips.
            return clearType switch
            {
                RecoveredBoundaryClearType.Failed => 4.733333f,
                RecoveredBoundaryClearType.Clear => 6.45f,
                RecoveredBoundaryClearType.FullCombo => 8.9f,
                RecoveredBoundaryClearType.AllPerfect => 9.5f,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(clearType), clearType, null),
            };
        }
    }
}
