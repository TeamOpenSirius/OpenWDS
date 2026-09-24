using System;
using UnityEngine;

namespace Sirius.Game
{
    public enum BoundaryClearType
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

        public BoundaryClearType ClearType { get; private set; }
        public bool IsPlaying { get; private set; }
        public float NormalizedTime => !IsPlaying
            ? 0f
            : Mathf.Clamp01(
                (Time.realtimeSinceStartup - _startedAt) /
                GetAuthoredDuration(ClearType));

        public float Show(BoundaryClearType clearType)
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

        public static string GetTrigger(BoundaryClearType clearType)
        {
            return clearType switch
            {
                // Retail ClearType 1 is ToFinish. ToFailed is the distinct
                // special failure presentation (5), not a zero-Life solo result.
                BoundaryClearType.Failed => "ToFinish",
                BoundaryClearType.Clear => "ToClear",
                BoundaryClearType.FullCombo => "ToFullCombo",
                BoundaryClearType.AllPerfect => "ToAllPerfect",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(clearType), clearType, null),
            };
        }

        public static float GetAuthoredDuration(
            BoundaryClearType clearType)
        {
            // Stop times from the retail ClearAnimation clips.
            return clearType switch
            {
                BoundaryClearType.Failed => 4.733333f,
                BoundaryClearType.Clear => 6.45f,
                BoundaryClearType.FullCombo => 8.9f,
                BoundaryClearType.AllPerfect => 9.5f,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(clearType), clearType, null),
            };
        }
    }
}
