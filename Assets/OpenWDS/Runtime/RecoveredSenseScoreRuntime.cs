using System;
using System.Collections.Generic;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Ordinary-solo Sense timing and score subset recovered from WDS 2.30.3.
    /// Events come from SenseNotationMaster after PartyStatusCalculator resolves
    /// Alternative senses to their actual actor and SenseMaster.
    /// </summary>
    public sealed class RecoveredSenseScoreRuntime
    {
        private readonly RecoveredPlayerUnitFixture _fixture;
        private int _nextEventIndex;

        public long Count { get; private set; }
        public int ActivationCount => _nextEventIndex;

        public RecoveredSenseScoreRuntime(RecoveredPlayerUnitFixture fixture)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _fixture.Validate();
        }

        public IReadOnlyList<RecoveredSenseEventFixture> Events =>
            _fixture.senseEvents;

        public long Tick(
            long chartMilliseconds,
            Action<RecoveredSenseEventFixture, long, int> onActivated = null)
        {
            long frameScore = 0;
            while (_nextEventIndex < _fixture.senseEvents.Length)
            {
                var senseEvent = _fixture.senseEvents[_nextEventIndex];
                if (chartMilliseconds < senseEvent.timingSeconds * 1000L) break;

                var addedScore = CalculateScore(senseEvent);
                Count += addedScore;
                frameScore += addedScore;
                _nextEventIndex++;
                // SenseEffectActivator passes the index within the senses fired
                // by this timing event. This fixture resolves one Sense each.
                onActivated?.Invoke(senseEvent, addedScore, 0);
            }
            return frameScore;
        }

        public long CalculateScore(RecoveredSenseEventFixture senseEvent)
        {
            if (senseEvent == null) throw new ArgumentNullException(nameof(senseEvent));
            var card = Array.Find(
                _fixture.cards,
                candidate => candidate.position == senseEvent.activatingPosition);
            if (card == null || card.senseMasterId != senseEvent.senseMasterId)
                throw new InvalidOperationException("Sense event actor is inconsistent.");

            // EffectScoreCalculator.GetSenseScore truncates the final product to
            // Int64. With this fixture, all omitted live/principal/life factors
            // are 1.0; SenseScoreUp is a percentage-addition factor.
            var scoreFactor = card.senseScorePercent / 100d;
            var buffFactor = 1d + senseEvent.scoreBuffPercent / 100d;
            return (long)(scoreFactor * card.partyStatus.totalStatus * buffFactor);
        }

        public void Reset()
        {
            Count = 0;
            _nextEventIndex = 0;
        }
    }
}
