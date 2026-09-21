using System;
using System.Collections.Generic;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Fixed-light and storage-light-branch ordinary-solo StarAct subset
    /// recovered from WDS 2.30.3.
    /// Sense lights are granted first; the event that completes the leader's
    /// StarAct condition consumes its required fixed lights and awards score.
    /// </summary>
    public sealed class StarActScoreRuntime
    {
        private readonly PlayerUnitFixture _fixture;
        private readonly int[] _lights = new int[5];
        private readonly List<int> _holdingLights = new List<int>();
        private int _nextEventIndex;

        public long Count { get; private set; }
        public int ActivationCount => _nextEventIndex;
        public IReadOnlyList<StarActEventFixture> Events =>
            _fixture.starActEvents;
        public IReadOnlyList<int> HoldingLights => _holdingLights;

        public StarActScoreRuntime(PlayerUnitFixture fixture)
        {
            _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            _fixture.Validate();
        }

        public long OnSenseActivated(SenseEventFixture senseEvent,
            Action<StarActEventFixture> beforeScore = null,
            Action<StarActEventFixture> afterScore = null)
        {
            if (senseEvent == null) throw new ArgumentNullException(nameof(senseEvent));
            if (senseEvent.lightType < 1 || senseEvent.lightType > 4 ||
                senseEvent.lightCount < 0)
                throw new InvalidOperationException("Unsupported Sense light.");

            _lights[senseEvent.lightType] += senseEvent.lightCount;
            for (var index = 0; index < senseEvent.lightCount; index++)
                _holdingLights.Add(senseEvent.lightType);
            if (_nextEventIndex >= _fixture.starActEvents.Length) return 0L;

            var starActEvent = _fixture.starActEvents[_nextEventIndex];
            if (starActEvent.triggeringSenseEventId != senseEvent.eventId) return 0L;
            if (starActEvent.timingSeconds != senseEvent.timingSeconds)
                throw new InvalidOperationException("StarAct trigger timing is inconsistent.");

            Consume(1, starActEvent.requiredSupportLights);
            Consume(2, starActEvent.requiredControlLights);
            Consume(3, starActEvent.requiredAmplificationLights);
            Consume(4, starActEvent.requiredSpecialLights);
            if (_lights[1] + _lights[2] + _lights[3] + _lights[4] !=
                starActEvent.storageLightCount)
                throw new InvalidOperationException(
                    "StarAct storage-light branch count is inconsistent.");
            beforeScore?.Invoke(starActEvent);
            var addedScore = CalculateScore(starActEvent);
            Count += addedScore;
            afterScore?.Invoke(starActEvent);
            _nextEventIndex++;
            return addedScore;
        }

        public long CalculateScore(StarActEventFixture starActEvent)
        {
            if (starActEvent == null) throw new ArgumentNullException(nameof(starActEvent));
            var leader = Array.Find(
                _fixture.cards,
                card => card.position == starActEvent.activatingPosition);
            if (leader == null ||
                leader.starActMasterId != starActEvent.starActMasterId ||
                leader.starActLevel != starActEvent.level)
                throw new InvalidOperationException("StarAct event leader is inconsistent.");

            // EffectScoreCalculator.GetStarActScore converts the final floating
            // product to Int64. This Master has no pre/effect branches, so the
            // recovered subset is total performance multiplied by ScoreFactor.
            var baseScore = (long)(_fixture.totalStatus *
                                   (starActEvent.scoreFactorPercent / 100d));
            var branchScore = (long)(_fixture.totalStatus *
                                     (starActEvent.additionalScoreFactorPercent / 100d));
            return baseScore + branchScore;
        }

        public int GetLightCount(int lightType)
        {
            if (lightType < 1 || lightType > 4)
                throw new ArgumentOutOfRangeException(nameof(lightType));
            return _lights[lightType];
        }

        public void Reset()
        {
            Count = 0L;
            _nextEventIndex = 0;
            Array.Clear(_lights, 0, _lights.Length);
            _holdingLights.Clear();
        }

        private void Consume(int lightType, int count)
        {
            if (_lights[lightType] < count)
                throw new InvalidOperationException(
                    "StarAct condition was not satisfied by granted Sense lights.");
            _lights[lightType] -= count;
            for (var index = 0; index < count; index++)
            {
                var holdingIndex = _holdingLights.IndexOf(lightType);
                if (holdingIndex < 0)
                    throw new InvalidOperationException(
                        "StarAct holding-light order is inconsistent.");
                _holdingLights.RemoveAt(holdingIndex);
            }
        }
    }
}
