using System;
using System.IO;
using OpenWDS.Runtime;
using UnityEngine;

namespace OpenWDS.Editor
{
    public static class ValidatePrincipalMaster
    {
        [Serializable] private sealed class Case
        {
            public PrincipalEffectFixture effect;
            public bool passive;
            public int initialMaximum, resolvedBase;
            public int expectedCurrent, expectedMaximum, expectedAdding;
        }
        [Serializable] private sealed class Catalog
        {
            public int effectCount;
            public long[] missingDetailIds;
            public Case[] cases;
        }

        public static void Run()
        {
            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(
                Path.Combine(Application.dataPath, "OpenWDS/Editor/PrincipalMasterFixture.json")));
            Require(catalog.effectCount == 301 && catalog.cases.Length == 2960 &&
                catalog.missingDetailIds.Length == 1 && catalog.missingDetailIds[0] == 11410101,
                "frozen catalog coverage");
            var principal = new PrincipalRuntime();
            principal.Initialize(new[] { new PrincipalUnit(2, 1000), new PrincipalUnit(5, 700) });
            foreach (var test in catalog.cases)
            {
                principal.Initialize(new[] { new PrincipalUnit(2, test.initialMaximum), new PrincipalUnit(5, 700) });
                principal.Add(137, 2);
                var effects = new[] { test.effect };
                SensePrincipalEffects.ValidateEffects(effects, 10);
                if (!test.passive) SensePrincipalEffects.FireEffects(effects, principal, new LifeRuntime(), 2);
                Require(principal.GetCurrentPrincipal(2) == test.expectedCurrent &&
                    principal.GetMaxPrincipal(2) == test.expectedMaximum &&
                    principal.GetAddingPrincipal(test.resolvedBase, 2) == test.expectedAdding &&
                    principal.GetCurrentPrincipal(5) == 0 && principal.GetMaxPrincipal(5) == 700,
                    "master effect " + test.effect.effectMasterId);
            }
            principal.Initialize(new[] { new PrincipalUnit(2, 1000), new PrincipalUnit(5, 700) });
            principal.Add(100, 2);
            principal.ApplyEffect(PrincipalEffectType.Bonus, 29, 2, true);
            Require(principal.GetCurrentPrincipal(2) == 128, "divide before multiply truncation");
            principal.Reset();
            principal.Add(100, 2);
            principal.AddNonTargetBuff(100, 2);
            var life = new LifeRuntime(900);
            SensePrincipalEffects.FireEffects(new[] {
                Effect(29, 2), Effect(27, 100),
                new PrincipalEffectFixture { effectMasterId = 1, effectType = 37, value = 7,
                    triggers = new[] { new EffectTriggerFixture { type = 1, value = 1000 } } },
                Effect(48, 100), Effect(52, 10)
            }, principal, life, 2);
            Require(life.GuardCount == 2 && life.Value == 1000 &&
                principal.GetCurrentPrincipal(2) == 314, "sequential LIFE trigger / direct gains ignore buffs");
            life.AddLifeGuardCount(-10);
            Require(life.GuardCount == 0, "guard lower clamp");
            life.Set(1200);
            Require(life.Value == 1200, "fixed LIFE allows over max");
            life.Add(-2000);
            Require(life.Value == 1, "skill delta cannot kill");
            life.Set(0);
            life.Set(1000);
            life.Add(1000);
            Require(life.Value == 0, "fixed LIFE cannot revive");
            principal.Reset();
            principal.AddPrincipalGaugeUpBuff(1000, 20, 2);
            principal.TickBuffs(1099);
            SensePrincipalEffects.FireEffects(new[] { Effect(26, 500) }, principal, life, 2);
            principal.TickBuffs(500);
            Require(principal.GetAddingPrincipal(100, 2) == 120, "extend near expiry");
            principal.TickBuffs(1);
            principal.ExtendBuffDuration(1000, 2);
            Require(principal.GetAddingPrincipal(100, 2) == 100, "expired buff cannot return");
            ValidateStages();
            Debug.Log("OPENWDS_PRINCIPAL_MASTER passed=True effects=301 details=2960 missingDetail=11410101");
        }

        private static void ValidateStages()
        {
            // Synthetic ordering probes on the existing real timing schedule.
            // They are not exported as purported Master skills or player teams.
            var fixture = JsonUtility.FromJson<PlayerUnitFixture>(File.ReadAllText(
                Path.Combine(Application.streamingAssetsPath, "OpenWDS/TestPlayer/stella-principal-gauge-unit.json")));
            fixture.schemaVersion = 10;
            fixture.principalStartEffects = new[] { Effect(41, 100), Effect(28, 900),
                new PrincipalEffectFixture { effectMasterId = 1, effectType = 37, value = 13,
                    triggers = new[] { new EffectTriggerFixture { type = 2, value = 900 } } },
                Effect(27, 100) };
            foreach (var item in fixture.senseEvents)
                item.principalPreEffects = new[] { Effect(37, 3) };
            foreach (var item in fixture.starActEvents)
            {
                item.principalPreEffects = new[] { Effect(37, 7) };
                item.principalEffects = new[] { Effect(52, 10) };
            }
            var principal = new PrincipalRuntime();
            principal.Initialize(new[] { new PrincipalUnit(fixture.order, fixture.initialMaxPrincipal) });
            var life = new LifeRuntime();
            var sense = new SenseScoreRuntime(fixture);
            var starAct = new StarActScoreRuntime(fixture);
            fixture.ApplyStartEffects(principal, life);
            Require(principal.GetCurrentPrincipal(fixture.order) == 13 &&
                principal.GetMaxPrincipal(fixture.order) == 1100 && life.Value == 1000, "start stage");
            sense.Tick(100000, (item, score, index) =>
            {
                principal.Add(item.acquirableGauge, fixture.order);
                SensePrincipalEffects.Fire(item, principal, life, fixture.order);
                starAct.OnSenseActivated(item, before =>
                {
                    Require(starAct.Count == 0, "StarAct pre before score");
                    SensePrincipalEffects.FireEffects(before.principalPreEffects, principal, life, fixture.order);
                    Require(principal.GetCurrentPrincipal(fixture.order) == 884, "Sense before StarAct");
                }, after =>
                {
                    Require(starAct.Count > 0, "StarAct post after score");
                    SensePrincipalEffects.Fire(after, principal, life, fixture.order);
                });
            }, before => SensePrincipalEffects.FireEffects(before.principalPreEffects, principal, life, fixture.order));
            Require(sense.ActivationCount == 8 && starAct.ActivationCount == 1 &&
                principal.GetCurrentPrincipal(fixture.order) == 994, "scheduled stage ordering");
        }

        private static PrincipalEffectFixture Effect(int type, double value) =>
            new PrincipalEffectFixture { effectMasterId = 1, effectType = type, value = value };

        private static void Require(bool value, string name)
        {
            if (!value) throw new InvalidOperationException("Principal master regression: " + name);
        }
    }
}
