using System;
using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>Result SE driven by actual numeric tween boundaries.</summary>
    public sealed class RecoveredGameResultSeRuntime : MonoBehaviour
    {
        private RecoveredUiSeRuntime _sharedSe;

        public bool IsCounting { get; private set; }
        public int PresentationCount { get; private set; }
        public int CompletionCueCount { get; private set; }
        public int RankCueCount { get; private set; }
        public int CountStartCount { get; private set; }
        public int CountStopCount { get; private set; }

        public void Configure(RecoveredUiSeRuntime sharedSe)
        {
            _sharedSe = sharedSe != null
                ? sharedSe
                : throw new ArgumentNullException(nameof(sharedSe));
        }

        public void BeginPresentation() => PresentationCount++;

        public void StartCount()
        {
            if (_sharedSe == null)
                throw new InvalidOperationException("Result SE requires the shared SE player.");
            _sharedSe.PlayLoop(RecoveredUiSeRuntime.GetCueName(
                RecoveredUiSeRuntime.Cue.CountUp2));
            IsCounting = true;
            CountStartCount++;
        }

        public void StopCount()
        {
            if (_sharedSe == null) return;
            _sharedSe.Stop(RecoveredUiSeRuntime.GetCueName(
                RecoveredUiSeRuntime.Cue.CountUp2));
            IsCounting = false;
            CountStopCount++;
        }

        public void PlayRank()
        {
            _sharedSe.Play(RecoveredUiSeRuntime.Cue.BadgeRankGot);
            RankCueCount++;
        }

        public void PlayCompletion()
        {
            _sharedSe.Play(RecoveredUiSeRuntime.GetCueName(
                RecoveredUiSeRuntime.Cue.BadgeNewGot));
            CompletionCueCount++;
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
