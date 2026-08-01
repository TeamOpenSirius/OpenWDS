using System;
using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Audio portion of the ordinary result count presentation. Retail result
    /// panels loop COUNT_UP_2 while their numeric tween runs, stop it when the
    /// tween completes, and use BADGE_NEW_GOT for the completion callback.
    /// The current static result view collapses that into its authored 0.3s
    /// rate/count interval while retaining the same broker operations.
    /// </summary>
    public sealed class RecoveredGameResultSeRuntime : MonoBehaviour
    {
        public const float CountDurationSeconds = 0.3f;

        private RecoveredUiSeRuntime _sharedSe;
        private float _countEndsAt;
        private bool _playCompletion;

        public bool IsCounting { get; private set; }
        public int PresentationCount { get; private set; }
        public int CompletionCueCount { get; private set; }

        public void Configure(RecoveredUiSeRuntime sharedSe)
        {
            _sharedSe = sharedSe != null
                ? sharedSe
                : throw new ArgumentNullException(nameof(sharedSe));
        }

        public void Begin(bool playCompletionCue)
        {
            if (_sharedSe == null)
                throw new InvalidOperationException(
                    "Result SE runtime requires the shared SE player.");
            if (IsCounting)
                _sharedSe.Stop(RecoveredUiSeRuntime.GetCueName(
                    RecoveredUiSeRuntime.Cue.CountUp2));
            _sharedSe.PlayLoop(RecoveredUiSeRuntime.GetCueName(
                RecoveredUiSeRuntime.Cue.CountUp2));
            _countEndsAt = Time.realtimeSinceStartup + CountDurationSeconds;
            _playCompletion = playCompletionCue;
            IsCounting = true;
            PresentationCount++;
        }

        public void Tick(float realtime)
        {
            if (!IsCounting || realtime < _countEndsAt) return;
            _sharedSe.Stop(RecoveredUiSeRuntime.GetCueName(
                RecoveredUiSeRuntime.Cue.CountUp2));
            IsCounting = false;
            if (!_playCompletion) return;
            _sharedSe.Play(RecoveredUiSeRuntime.GetCueName(
                RecoveredUiSeRuntime.Cue.BadgeNewGot));
            CompletionCueCount++;
        }

        private void Update()
        {
            Tick(Time.realtimeSinceStartup);
        }

        private void OnDisable()
        {
            if (!IsCounting || _sharedSe == null) return;
            _sharedSe.Stop(RecoveredUiSeRuntime.GetCueName(
                RecoveredUiSeRuntime.Cue.CountUp2));
            IsCounting = false;
        }
    }
}
