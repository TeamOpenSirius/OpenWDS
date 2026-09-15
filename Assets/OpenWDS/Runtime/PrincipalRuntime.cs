using System;
using System.Collections.Generic;

namespace OpenWDS.Runtime
{
    public enum PrincipalEffectType
    {
        Gain,
        LimitUp,
        Bonus,
        GainPercentageOfLimit,
    }

    public readonly struct PrincipalUnit
    {
        public readonly int Order;
        public readonly int InitialMaxValue;

        public PrincipalUnit(int order, int initialMaxValue)
        {
            Order = order;
            InitialMaxValue = initialMaxValue;
        }
    }

    /// <summary>
    /// Principal, PrincipalCalculator, the Principal gauge-up duration scheduler,
    /// and SenseEffectActivator's principal-add path. LiveUnit-derived values are
    /// explicit inputs; this class deliberately has no screenshot-derived defaults.
    /// </summary>
    public sealed class PrincipalRuntime
    {
        private sealed class TimedBuff
        {
            public long RemainingMilliseconds;
            public double Value;
        }

        private sealed class Context
        {
            public int InitialMaxValue;
            public int CurrentPrincipal;
            public int MaxPrincipal;
            public readonly List<double> NonTargetBuffs = new List<double>();
            public readonly List<TimedBuff> TimedBuffs = new List<TimedBuff>();
        }

        private readonly Dictionary<int, Context> _contexts =
            new Dictionary<int, Context>();

        public int DefaultOrder { get; private set; }
        public int UnitCount => _contexts.Count;
        public bool IsInitialized => _contexts.Count > 0;
        public event Action<int, int> DefaultValueChanged;

        public void Initialize(IReadOnlyList<PrincipalUnit> units)
        {
            if (units == null) throw new ArgumentNullException(nameof(units));
            _contexts.Clear();
            DefaultOrder = units.Count > 0 ? units[0].Order : 0;
            for (var index = 0; index < units.Count; index++)
            {
                var unit = units[index];
                _contexts[unit.Order] = new Context
                {
                    InitialMaxValue = unit.InitialMaxValue,
                    CurrentPrincipal = 0,
                    MaxPrincipal = unit.InitialMaxValue,
                };
            }
        }

        public int GetCurrentPrincipal(int order) => GetContext(order).CurrentPrincipal;
        public int GetMaxPrincipal(int order) => GetContext(order).MaxPrincipal;

        public void Add(int amount, int order)
        {
            var context = GetContext(order);
            context.CurrentPrincipal = Math.Min(
                context.MaxPrincipal, context.CurrentPrincipal + amount);
            NotifyIfDefault(order, context);
        }

        public void IncreaseMax(int amount, int order)
        {
            var context = GetContext(order);
            context.MaxPrincipal += amount;
            NotifyIfDefault(order, context);
        }

        public void AddNonTargetBuff(double value, int order)
        {
            GetContext(order).NonTargetBuffs.Add(value);
        }

        public bool RemoveNonTargetBuff(double value, int order)
        {
            return GetContext(order).NonTargetBuffs.Remove(value);
        }

        public void AddPrincipalGaugeUpBuff(
            long durationInMilliseconds,
            double value,
            int order)
        {
            if (durationInMilliseconds < 1)
            {
                AddNonTargetBuff(value, order);
                return;
            }
            GetContext(order).TimedBuffs.Add(new TimedBuff
            {
                RemainingMilliseconds = durationInMilliseconds,
                Value = value,
            });
        }

        public void ExtendBuffDuration(long durationInMilliseconds, int order)
        {
            if (durationInMilliseconds <= 0) return;
            foreach (var buff in GetContext(order).TimedBuffs)
                buff.RemainingMilliseconds += durationInMilliseconds;
        }

        public void TickBuffs(long elapsedMilliseconds)
        {
            if (elapsedMilliseconds <= 0) return;
            foreach (var context in _contexts.Values)
            {
                for (var index = context.TimedBuffs.Count - 1; index >= 0; index--)
                {
                    var buff = context.TimedBuffs[index];
                    buff.RemainingMilliseconds -= elapsedMilliseconds;
                    if (buff.RemainingMilliseconds <= 0)
                        context.TimedBuffs.RemoveAt(index);
                }
            }
        }

        public int GetAddingPrincipal(long baseAmount, int order)
        {
            var context = GetContext(order);
            var totalBuff = 0d;
            foreach (var value in context.NonTargetBuffs) totalBuff += value;
            foreach (var buff in context.TimedBuffs) totalBuff += buff.Value;
            return CalculateAddingPrincipal(baseAmount, totalBuff);
        }

        public int ActivateSense(
            long senseId,
            IReadOnlyDictionary<long, int> addingPrincipalMap,
            int order)
        {
            if (addingPrincipalMap == null)
                throw new ArgumentNullException(nameof(addingPrincipalMap));
            var addingPrincipal = GetAddingPrincipal(addingPrincipalMap[senseId], order);
            Add(addingPrincipal, order);
            return addingPrincipal;
        }

        /// <summary>
        /// Applies the value transformation performed by the four recovered
        /// Principal effect contents after their shared fire checker succeeds.
        /// The caller owns trigger evaluation; a rejected effect must not mutate
        /// Principal state.
        /// </summary>
        public bool ApplyEffect(
            PrincipalEffectType effectType,
            double value,
            int order,
            bool canFire)
        {
            if (!canFire) return false;

            var context = GetContext(order);
            switch (effectType)
            {
                case PrincipalEffectType.Gain:
                    Add((int)value, order);
                    break;
                case PrincipalEffectType.LimitUp:
                    IncreaseMax((int)value, order);
                    break;
                case PrincipalEffectType.Bonus:
                    Add((int)(context.CurrentPrincipal * value / 100d), order);
                    break;
                case PrincipalEffectType.GainPercentageOfLimit:
                    Add((int)(context.MaxPrincipal * value / 100d), order);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(effectType), effectType, "Unknown Principal effect type.");
            }
            return true;
        }

        public void Reset()
        {
            foreach (var context in _contexts.Values)
            {
                context.CurrentPrincipal = 0;
                context.MaxPrincipal = context.InitialMaxValue;
                context.NonTargetBuffs.Clear();
                context.TimedBuffs.Clear();
            }
            if (_contexts.TryGetValue(DefaultOrder, out var defaultContext))
                NotifyIfDefault(DefaultOrder, defaultContext);
        }

        public static int CalculateAddingPrincipal(long baseAmount, double totalBuff)
        {
            return (int)(baseAmount * (1d + totalBuff / 100d));
        }

        private Context GetContext(int order)
        {
            if (!_contexts.TryGetValue(order, out var context))
                throw new KeyNotFoundException("Unknown live-unit order: " + order);
            return context;
        }

        private void NotifyIfDefault(int order, Context context)
        {
            if (order == DefaultOrder)
                DefaultValueChanged?.Invoke(
                    context.CurrentPrincipal, context.MaxPrincipal);
        }
    }
}
