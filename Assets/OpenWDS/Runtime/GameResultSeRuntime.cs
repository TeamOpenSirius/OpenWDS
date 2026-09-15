using System;
using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>Result SE driven by actual numeric tween boundaries.</summary>
    public sealed class GameResultSeRuntime : MonoBehaviour
    {
        private UiSeRuntime _sharedSe;

        public bool IsCounting { get; private set; }
        public int PresentationCount { get; private set; }
        public int CompletionCueCount { get; private set; }
        public int RankCueCount { get; private set; }
        public int CountStartCount { get; private set; }
        public int CountStopCount { get; private set; }

        public void Configure(UiSeRuntime sharedSe)
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
            _sharedSe.PlayLoop(UiSeRuntime.GetCueName(
                UiSeRuntime.Cue.CountUp2));
            IsCounting = true;
            CountStartCount++;
        }

        public void StopCount()
        {
            if (_sharedSe == null) return;
            _sharedSe.Stop(UiSeRuntime.GetCueName(
                UiSeRuntime.Cue.CountUp2));
            IsCounting = false;
            CountStopCount++;
        }

        public void PlayRank()
        {
            _sharedSe.Play(UiSeRuntime.Cue.BadgeRankGot);
            RankCueCount++;
        }

        public void PlayCompletion()
        {
            _sharedSe.Play(UiSeRuntime.GetCueName(
                UiSeRuntime.Cue.BadgeNewGot));
            CompletionCueCount++;
        }

        private void OnDisable()
        {
            if (!IsCounting || _sharedSe == null) return;
            _sharedSe.Stop(UiSeRuntime.GetCueName(
                UiSeRuntime.Cue.CountUp2));
            IsCounting = false;
        }
    }
}
