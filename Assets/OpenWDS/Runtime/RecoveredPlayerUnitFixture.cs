using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenWDS.Runtime
{
    [Serializable]
    public sealed class RecoveredPlayerUnitStatusFixture
    {
        public int concentration;
        public int expression;
        public int vocal;
        public int totalStatus;
    }

    [Serializable]
    public sealed class RecoveredEquipmentEffectFixture
    {
        public float concentrationPercent;
        public float concentrationFixed;
        public float expressionPercent;
        public float expressionFixed;
        public float vocalPercent;
        public float vocalFixed;
    }

    [Serializable]
    public sealed class RecoveredPosterFixture
    {
        public long posterMasterId;
        public string posterName;
        public int level;
        public long[] activePosterAbilityMasterIds;
        public long[] activeEffectMasterIds;
    }

    [Serializable]
    public sealed class RecoveredAccessoryFixture
    {
        public long accessoryMasterId;
        public string accessoryName;
        public int level;
        public long[] accessoryEffectMasterIds;
        public long[] activeEffectMasterIds;
    }

    [Serializable]
    public sealed class RecoveredPlayerUnitCardFixture
    {
        public int position;
        public long characterMasterId;
        public long characterBaseMasterId;
        public long companyMasterId;
        public string characterName;
        public string cardName;
        public int rarity;
        public int attribute;
        public int level;
        public int characterLevelFactor;
        public int starRank;
        public float starRankPercent;
        public int talentStage;
        public int awakeningPhase;
        public int senseLevel;
        public long senseMasterId;
        public string senseName;
        public int senseType;
        public int senseScorePercent;
        public int senseAcquirableGauge;
        public int senseLightCount;
        public int senseCoolTimeSeconds;
        public long starActMasterId;
        public int starActLevel;
        public RecoveredPlayerUnitStatusFixture masterMinLevelStatus;
        public RecoveredPlayerUnitStatusFixture calculatedStatus;
        public RecoveredPosterFixture poster;
        public RecoveredAccessoryFixture accessory;
        public RecoveredEquipmentEffectFixture posterEffect;
        public RecoveredEquipmentEffectFixture accessoryEffect;
        public RecoveredPlayerUnitStatusFixture partyStatus;
    }

    [Serializable]
    public sealed class RecoveredStartEffectFixture
    {
        public int sourcePosition;
        public string sourceType;
        public long sourceMasterId;
        public long posterAbilityMasterId;
        public long effectMasterId;
        public int effectType;
        public int calculationType;
        public int fireTimingType;
        public float value;
    }

    [Serializable]
    public sealed class RecoveredSenseEventFixture
    {
        public long eventId;
        public int timingSeconds;
        public int sourcePosition;
        public int activatingPosition;
        public long senseMasterId;
        public int acquirableGauge;
        public float scoreBuffPercent;
        public int lightType;
        public int lightCount;
    }

    [Serializable]
    public sealed class RecoveredStarActEventFixture
    {
        public int timingSeconds;
        public long triggeringSenseEventId;
        public int activatingPosition;
        public long starActMasterId;
        public long starActConditionMasterId;
        public int level;
        public int scoreFactorPercent;
        public int requiredFreeLights;
        public int requiredSupportLights;
        public int requiredControlLights;
        public int requiredAmplificationLights;
        public int requiredSpecialLights;
        public int storageLightCount;
        public long branchId;
        public long branchEffectMasterId;
        public int additionalScoreFactorPercent;
    }

    /// <summary>
    /// Public MasterMemory-derived test boundary for LiveUnitWithOrder. It is not
    /// account data and models a level-1 unit with an explicit public-Master
    /// poster/accessory fixture and without character growth bonuses.
    /// </summary>
    [Serializable]
    public sealed class RecoveredPlayerUnitFixture
    {
        public int schemaVersion;
        public string purpose;
        public string profile;
        public string sourceDatabase;
        public string sourceDatabaseSha256;
        public int musicId;
        public string difficulty;
        public int order;
        public int leaderPosition;
        public double baseScoreDifficultyAutoCoefficient;
        public int baseScorePercentage;
        public int initialMaxPrincipal;
        public int totalStatus;
        public int characterTotalStatus;
        public long senseNotationMasterId;
        public RecoveredSenseEventFixture[] senseEvents;
        public RecoveredStarActEventFixture[] starActEvents;
        public RecoveredStartEffectFixture[] startEffects;
        public RecoveredPlayerUnitCardFixture[] cards;

        public static RecoveredPlayerUnitFixture Parse(TextAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            var fixture = JsonUtility.FromJson<RecoveredPlayerUnitFixture>(asset.text);
            fixture?.Validate();
            return fixture;
        }

        public RecoveredSoloScoreContext CreateScoreContext(
            IReadOnlyCollection<int> scoreNoteIds)
        {
            Validate();
            return new RecoveredSoloScoreContext(
                order,
                baseScoreDifficultyAutoCoefficient,
                totalStatus,
                baseScorePercentage,
                scoreNoteIds);
        }

        public int GetInitialLifeAddition()
        {
            Validate();
            var total = 0;
            foreach (var effect in startEffects)
            {
                // EffectTypes.LifeHealing=27, CalculationTypes.FixedAddition=3,
                // FireTimingTypes.StartLive=3.
                if (effect.effectType == 27 && effect.calculationType == 3 &&
                    effect.fireTimingType == 3)
                    total += (int)effect.value;
            }
            return total;
        }

        public int GetStarActSenseLightCount()
        {
            Validate();
            var total = -1;
            foreach (var starActEvent in starActEvents)
            {
                var eventTotal =
                    starActEvent.requiredFreeLights +
                    starActEvent.requiredSupportLights +
                    starActEvent.requiredControlLights +
                    starActEvent.requiredAmplificationLights +
                    starActEvent.requiredSpecialLights;
                if (eventTotal < 0 || eventTotal > 10)
                    throw new InvalidOperationException(
                        "StarAct Sense-light count must be in 0..10.");
                if (total >= 0 && total != eventTotal)
                    throw new InvalidOperationException(
                        "Player-unit StarAct light counts are inconsistent.");
                total = eventTotal;
            }
            return Math.Max(0, total);
        }

        public void Validate()
        {
            if (schemaVersion != 7 || purpose != "offline-test-player-unit")
                throw new InvalidOperationException("Unsupported player-unit fixture schema.");
            if (cards == null || cards.Length != 5)
                throw new InvalidOperationException("Player-unit fixture requires five cards.");
            if (startEffects == null)
                throw new InvalidOperationException("Player-unit start effects are missing.");
            if (senseNotationMasterId <= 0 || senseEvents == null ||
                senseEvents.Length == 0)
                throw new InvalidOperationException("Player-unit Sense schedule is missing.");
            if (starActEvents == null || starActEvents.Length == 0)
                throw new InvalidOperationException("Player-unit StarAct schedule is missing.");
            if (baseScoreDifficultyAutoCoefficient <= 0d)
                throw new InvalidOperationException("Score coefficient must be positive.");
            if (initialMaxPrincipal <= 0)
                throw new InvalidOperationException(
                    "Initial MaxPrincipal must be positive.");
            if (leaderPosition < 1 || leaderPosition > 5)
                throw new InvalidOperationException("Leader position must be in 1..5.");

            var positions = new HashSet<int>();
            var characterBases = new HashSet<long>();
            var characterSum = 0;
            var partySum = 0;
            var posterCount = 0;
            var accessoryCount = 0;
            foreach (var card in cards)
            {
                if (card == null || card.masterMinLevelStatus == null ||
                    card.calculatedStatus == null || card.partyStatus == null ||
                    card.posterEffect == null || card.accessoryEffect == null)
                    throw new InvalidOperationException("Player-unit card/status is missing.");
                if (!positions.Add(card.position) || card.position < 1 || card.position > 5)
                    throw new InvalidOperationException("Player-unit positions must be unique 1..5.");
                if (!characterBases.Add(card.characterBaseMasterId))
                    throw new InvalidOperationException("Player-unit character bases must be unique.");
                if (card.companyMasterId <= 0)
                    throw new InvalidOperationException("Player-unit company is missing.");
                if (card.senseMasterId <= 0 || string.IsNullOrEmpty(card.senseName) ||
                    card.senseScorePercent < 0 || card.senseAcquirableGauge < 0 ||
                    card.senseLightCount < 0 || card.senseCoolTimeSeconds < 0)
                    throw new InvalidOperationException("Player-unit Sense is invalid.");
                if (card.starActMasterId <= 0 || card.starActLevel <= 0)
                    throw new InvalidOperationException("Player-unit StarAct is invalid.");
                var source = card.masterMinLevelStatus;
                if (source.concentration + source.expression + source.vocal !=
                    source.totalStatus)
                    throw new InvalidOperationException("Master status sum is invalid.");
                var status = card.calculatedStatus;
                if (status.concentration != RecoveredPartyStatusRuntime.CalculateCharacterStatus(
                        source.concentration, 0, 0, card.characterLevelFactor,
                        card.awakeningPhase, 0, card.starRankPercent) ||
                    status.expression != RecoveredPartyStatusRuntime.CalculateCharacterStatus(
                        source.expression, 0, 0, card.characterLevelFactor,
                        card.awakeningPhase, 0, card.starRankPercent) ||
                    status.vocal != RecoveredPartyStatusRuntime.CalculateCharacterStatus(
                        source.vocal, 0, 0, card.characterLevelFactor,
                        card.awakeningPhase, 0, card.starRankPercent))
                    throw new InvalidOperationException("Calculated character status is invalid.");
                var cardTotal = status.concentration + status.expression + status.vocal;
                if (cardTotal != status.totalStatus)
                    throw new InvalidOperationException("Card TotalStatus does not match its status sum.");
                characterSum += cardTotal;

                var party = card.partyStatus;
                if (party.concentration != CalculateSlotStatus(
                        status.concentration, card.posterEffect.concentrationPercent,
                        card.posterEffect.concentrationFixed,
                        card.accessoryEffect.concentrationPercent,
                        card.accessoryEffect.concentrationFixed) ||
                    party.expression != CalculateSlotStatus(
                        status.expression, card.posterEffect.expressionPercent,
                        card.posterEffect.expressionFixed,
                        card.accessoryEffect.expressionPercent,
                        card.accessoryEffect.expressionFixed) ||
                    party.vocal != CalculateSlotStatus(
                        status.vocal, card.posterEffect.vocalPercent,
                        card.posterEffect.vocalFixed,
                        card.accessoryEffect.vocalPercent,
                        card.accessoryEffect.vocalFixed))
                    throw new InvalidOperationException("Party slot equipment status is invalid.");
                var partyTotal = party.concentration + party.expression + party.vocal;
                if (partyTotal != party.totalStatus)
                    throw new InvalidOperationException("Party slot TotalStatus is invalid.");
                partySum += partyTotal;
                if (card.poster != null) posterCount++;
                if (card.accessory != null) accessoryCount++;
            }
            if (characterSum != characterTotalStatus)
                throw new InvalidOperationException("Character TotalStatus does not match its cards.");
            if (partySum != totalStatus)
                throw new InvalidOperationException("LiveUnit TotalStatus does not match its cards.");
            if (posterCount == 0 || accessoryCount == 0)
                throw new InvalidOperationException("Equipment fixture must cover poster and accessory.");

            foreach (var effect in startEffects)
            {
                if (effect == null || effect.sourcePosition < 1 ||
                    effect.sourcePosition > 5 || effect.sourceType != "poster" ||
                    effect.sourceMasterId <= 0 || effect.posterAbilityMasterId <= 0 ||
                    effect.effectMasterId <= 0 || effect.fireTimingType != 3 ||
                    effect.effectType != 27 || effect.calculationType != 3 ||
                    effect.value < 0f)
                    throw new InvalidOperationException("Unsupported player-unit start effect.");
                var sourceCard = Array.Find(
                    cards, card => card.position == effect.sourcePosition);
                if (sourceCard?.poster == null ||
                    sourceCard.poster.activePosterAbilityMasterIds == null ||
                    sourceCard.poster.activeEffectMasterIds == null ||
                    sourceCard.poster.posterMasterId != effect.sourceMasterId ||
                    Array.IndexOf(
                        sourceCard.poster.activePosterAbilityMasterIds,
                        effect.posterAbilityMasterId) < 0 ||
                    Array.IndexOf(
                        sourceCard.poster.activeEffectMasterIds,
                        effect.effectMasterId) < 0)
                    throw new InvalidOperationException(
                        "Player-unit start effect source does not match its poster.");
            }

            var eventIds = new HashSet<long>();
            var previousTiming = -1;
            foreach (var senseEvent in senseEvents)
            {
                if (senseEvent == null || !eventIds.Add(senseEvent.eventId) ||
                    senseEvent.timingSeconds < previousTiming ||
                    senseEvent.sourcePosition < 1 || senseEvent.sourcePosition > 5 ||
                    senseEvent.activatingPosition < 1 ||
                    senseEvent.activatingPosition > 5 ||
                    senseEvent.acquirableGauge < 0 ||
                    senseEvent.scoreBuffPercent < 0f ||
                    senseEvent.lightType < 1 || senseEvent.lightType > 4 ||
                    senseEvent.lightCount < 0)
                    throw new InvalidOperationException("Player-unit Sense event is invalid.");
                var activatingCard = Array.Find(
                    cards, card => card.position == senseEvent.activatingPosition);
                if (activatingCard == null ||
                    activatingCard.senseMasterId != senseEvent.senseMasterId ||
                    activatingCard.senseAcquirableGauge !=
                        senseEvent.acquirableGauge ||
                    activatingCard.senseScorePercent <= 0)
                    throw new InvalidOperationException(
                        "Player-unit Sense event activation does not match its card.");
                previousTiming = senseEvent.timingSeconds;
            }

            var previousStarActTiming = -1;
            foreach (var starActEvent in starActEvents)
            {
                if (starActEvent == null ||
                    starActEvent.timingSeconds < previousStarActTiming ||
                    starActEvent.triggeringSenseEventId <= 0 ||
                    starActEvent.activatingPosition != leaderPosition ||
                    starActEvent.starActMasterId <= 0 ||
                    starActEvent.starActConditionMasterId <= 0 ||
                    starActEvent.level <= 0 || starActEvent.scoreFactorPercent <= 0 ||
                    starActEvent.requiredFreeLights != 0 ||
                    starActEvent.requiredSupportLights < 0 ||
                    starActEvent.requiredControlLights < 0 ||
                    starActEvent.requiredAmplificationLights < 0 ||
                    starActEvent.requiredSpecialLights < 0 ||
                    starActEvent.storageLightCount < 0 ||
                    (starActEvent.branchId == 0 &&
                     (starActEvent.branchEffectMasterId != 0 ||
                      starActEvent.additionalScoreFactorPercent != 0)) ||
                    (starActEvent.branchId != 0 &&
                     (starActEvent.branchEffectMasterId <= 0 ||
                      starActEvent.additionalScoreFactorPercent <= 0)))
                    throw new InvalidOperationException(
                        "Player-unit StarAct event is invalid.");
                var triggeringSense = Array.Find(
                    senseEvents,
                    senseEvent =>
                        senseEvent.eventId == starActEvent.triggeringSenseEventId);
                var leader = Array.Find(
                    cards, card => card.position == leaderPosition);
                if (triggeringSense == null ||
                    triggeringSense.timingSeconds != starActEvent.timingSeconds ||
                    leader == null ||
                    leader.starActMasterId != starActEvent.starActMasterId ||
                    leader.starActLevel != starActEvent.level)
                    throw new InvalidOperationException(
                        "Player-unit StarAct event does not match its trigger/leader.");
                previousStarActTiming = starActEvent.timingSeconds;
            }
        }

        private static int CalculateSlotStatus(
            int characterStatus,
            float posterPercent,
            float posterFixed,
            float accessoryPercent,
            float accessoryFixed)
        {
            return RecoveredPartyStatusRuntime.CalculatePartySlotStatus(
                characterStatus,
                posterPercent,
                posterFixed,
                accessoryPercent,
                accessoryFixed);
        }
    }
}
