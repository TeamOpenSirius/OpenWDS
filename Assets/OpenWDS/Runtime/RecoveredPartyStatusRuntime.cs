using System;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Pure status formulas recovered from SiriusLogic.Shared.StatusLogic ARM64.
    /// PartyStatusCalculator supplies the values aggregated from master/user data.
    /// </summary>
    public static class RecoveredPartyStatusRuntime
    {
        public static int CalculateCharacterStatus(
            int baseValue,
            int baseStatusUpValue,
            int storyReadBonus,
            int characterLevelFactor,
            int awakeningPhase,
            int baseCorrectionEffectPercent,
            float starRankPercent)
        {
            var baseTotal = baseValue + baseStatusUpValue + storyReadBonus;
            var levelValue = baseTotal * characterLevelFactor / 100d;
            var multiplier =
                (100d + awakeningPhase * 10d + (double)starRankPercent +
                 baseCorrectionEffectPercent / 100d) / 100d;
            return (int)(levelValue * multiplier);
        }

        public static int CalculatePartySlotStatus(
            int characterStatus,
            float posterPercent,
            float posterFixed,
            float accessoryPercent,
            float accessoryFixed)
        {
            var poster = (int)(posterFixed + characterStatus * posterPercent / 100f);
            var accessory =
                (int)(accessoryFixed + characterStatus * accessoryPercent / 100f);
            return characterStatus + poster + accessory;
        }
    }
}
