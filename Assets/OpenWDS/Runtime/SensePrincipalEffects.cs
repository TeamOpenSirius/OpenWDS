using System;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Principal subset of EffectContentFactory, LifeGuardBranchEffectContent
    /// and EffectContentFireChecker. Actor/party triggers must be resolved before
    /// constructing this payload; only runtime LIFE triggers are accepted here.
    /// </summary>
    public static class SensePrincipalEffects
    {
        public static void Validate(PrincipalProgramFixture sense, int schemaVersion)
        {
            if (sense == null) throw new InvalidOperationException("Missing Principal effect program.");
            ValidateEffects(sense.principalEffects, schemaVersion);
            ValidateEffects(sense.principalPreEffects, schemaVersion);
            ValidateEffects(sense.principalTimingEffects, schemaVersion);
            if (sense.principalBranchCondition == 0)
            {
                if (sense.principalBranches != null && sense.principalBranches.Length != 0)
                    throw new InvalidOperationException("Principal branches require a condition.");
                return;
            }
            if (schemaVersion < 9 || sense.principalBranchCondition != 8 ||
                sense.principalBranches == null ||
                (sense.principalEffects != null && sense.principalEffects.Length != 0))
                throw new InvalidOperationException("Unsupported Principal branch condition.");
            foreach (var branch in sense.principalBranches)
            {
                if (branch == null || branch.branchId <= 0 ||
                    branch.judgeType < 1 || branch.judgeType > 3 || branch.effects == null)
                    throw new InvalidOperationException("Invalid Principal LifeGuard branch.");
                ValidateEffects(branch.effects, schemaVersion);
                foreach (var effect in branch.effects)
                    if (effect.effectType == 29)
                        throw new InvalidOperationException("Guard grants must be removed from LifeGuard branches.");
            }
        }

        public static void ValidateEffects(PrincipalEffectFixture[] effects, int schemaVersion)
        {
            if (effects == null) return;
            foreach (var effect in effects)
            {
                if (schemaVersion < 8 || effect == null || effect.effectMasterId <= 0 ||
                    !IsSupported(effect.effectType) ||
                    double.IsNaN(effect.value) || double.IsInfinity(effect.value) ||
                    Math.Abs(effect.value) > int.MaxValue ||
                    effect.durationMilliseconds > long.MaxValue - 100)
                    throw new InvalidOperationException("Unsupported Principal effect.");
                if (effect.triggers == null) continue;
                foreach (var trigger in effect.triggers)
                    if (schemaVersion < 9 || trigger == null ||
                        (trigger.type != 1 && trigger.type != 2))
                        throw new InvalidOperationException("Unresolved Principal effect trigger.");
            }
        }

        public static long Fire(
            PrincipalProgramFixture sense, PrincipalRuntime principal, LifeRuntime life, int order)
        {
            var effects = sense.principalEffects;
            long selectedBranch = 0;
            if (sense.principalBranchCondition != 0)
            {
                if (sense.principalBranchCondition != 8)
                    throw new InvalidOperationException("Unsupported Principal branch condition.");
                effects = null;
                // SetEffectIndex clears its previous choice on every Fire and
                // selects the first match in the original array order.
                foreach (var branch in sense.principalBranches)
                {
                    if (!Matches(life.GuardCount, branch.conditionValue, branch.judgeType)) continue;
                    selectedBranch = branch.branchId;
                    effects = branch.effects;
                    break;
                }
            }
            FireEffects(effects, principal, life, order);
            FireEffects(sense.principalTimingEffects, principal, life, order);
            return selectedBranch;
        }

        private static bool IsSupported(int type) => type == 26 || type == 27 || type == 28 ||
            type == 29 || type == 37 || type == 40 || type == 41 || type == 48 || type == 52;

        public static void FireEffects(PrincipalEffectFixture[] effects,
            PrincipalRuntime principal, LifeRuntime life, int order)
        {
            if (effects == null) return;
            foreach (var effect in effects)
            {
                if (!CanFire(effect.triggers, life.Value)) continue;
                switch (effect.effectType)
                {
                    case 26: principal.ExtendBuffDuration((long)effect.value, order); break;
                    case 27: life.Add((int)effect.value); break;
                    case 28: life.Set((int)effect.value); break;
                    case 29: life.AddLifeGuardCount((int)effect.value); break;
                    case 37: principal.ApplyEffect(PrincipalEffectType.Gain, effect.value, order, true); break;
                    case 48: principal.ApplyEffect(PrincipalEffectType.Bonus, effect.value, order, true); break;
                    case 52: principal.ApplyEffect(PrincipalEffectType.GainPercentageOfLimit, effect.value, order, true); break;
                    case 40:
                        principal.AddPrincipalGaugeUpBuff(effect.durationMilliseconds, effect.value, order);
                        break;
                    case 41:
                        principal.ApplyEffect(PrincipalEffectType.LimitUp, effect.value, order, true);
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported Principal effect.");
                }
            }
        }

        public static bool CanFire(EffectTriggerFixture[] triggers, int life)
        {
            if (triggers == null) return true;
            foreach (var trigger in triggers)
            {
                // CheckTrigger compares signed Int64 thresholds to current LIFE,
                // inclusively; it does not use MaxLife or a percentage.
                switch (trigger.type)
                {
                    case 1: if (life < trigger.value) return false; break;
                    case 2: if (life > trigger.value) return false; break;
                    default:
                        throw new InvalidOperationException("Unresolved Principal effect trigger.");
                }
            }
            return true;
        }

        private static bool Matches(long current, long threshold, int judgeType)
        {
            switch (judgeType)
            {
                case 1: return current == threshold;
                case 2: return current >= threshold;
                case 3: return current <= threshold;
                default: throw new ArgumentOutOfRangeException(nameof(judgeType));
            }
        }
    }
}
