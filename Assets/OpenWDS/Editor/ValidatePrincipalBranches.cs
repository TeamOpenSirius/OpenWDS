using System;
using System.IO;
using OpenWDS.Runtime;
using UnityEngine;

namespace OpenWDS.Editor
{
    public static class ValidatePrincipalBranches
    {
        [Serializable]
        private sealed class MasterFixture
        {
            public int schemaVersion;
            public SenseEventFixture sense;
        }

        public static void Run()
        {
            var fixture = JsonUtility.FromJson<MasterFixture>(File.ReadAllText(
                Path.Combine(Application.dataPath, "OpenWDS/Editor/PrincipalBranchFixture.json")));
            var sense = fixture.sense;
            SensePrincipalEffects.Validate(sense, fixture.schemaVersion);
            var principal = new PrincipalRuntime();
            principal.Initialize(new[] { new PrincipalUnit(2, 1000), new PrincipalUnit(5, 750) });
            // Actual Sense 142390: equal 1..4 guards, >= 5 guards, no zero branch.
            for (var guards = 0; guards <= 6; guards++)
            {
                principal.Reset();
                principal.Add(sense.acquirableGauge, 5);
                var branch = SensePrincipalEffects.Fire(sense, principal, new LifeRuntime(guardCount: guards), 5);
                Require(branch == (guards == 0 ? 0 : 1423900 + Math.Min(guards, 5)), "master branch selection");
                Require(principal.GetMaxPrincipal(5) == 750 + Math.Min(guards, 5) * 20 &&
                    principal.GetCurrentPrincipal(5) == 70 && principal.GetMaxPrincipal(2) == 1000,
                    "master branch value / target isolation");
            }
            principal.Reset();
            var life = new LifeRuntime(guardCount: 2);
            var miss = new[] { InputResultEntity.OnMiss(new NotationNote { NoteType = (int)NoteType.Flick }) };
            Require(SensePrincipalEffects.Fire(sense, principal, life, 2) == 1423902, "two guards");
            life.Process(miss);
            Require(SensePrincipalEffects.Fire(sense, principal, life, 2) == 1423901, "guard consumption");
            life.Process(miss);
            Require(SensePrincipalEffects.Fire(sense, principal, life, 2) == 0 &&
                principal.GetMaxPrincipal(2) == 1060 && life.Value == 1000, "stale branch after guards exhausted");

            // Overlapping synthetic branches protect first-match ordering and <=.
            sense.principalBranches[0].judgeType = 3;
            sense.principalBranches[0].conditionValue = 6;
            Require(SensePrincipalEffects.Fire(sense, principal, new LifeRuntime(guardCount: 5), 2) == 1423901,
                "first match");
            Require(SensePrincipalEffects.Fire(sense, principal, new LifeRuntime(guardCount: 6), 2) == 1423901 &&
                SensePrincipalEffects.Fire(sense, principal, new LifeRuntime(guardCount: 7), 2) == 1423905,
                "inclusive below branch / next branch");

            var effect = new PrincipalEffectFixture
            {
                effectMasterId = 20150, effectType = 40, value = 7, durationMilliseconds = 1000,
                triggers = new[] { new EffectTriggerFixture { type = 1, value = 1000 },
                    new EffectTriggerFixture { type = 2, value = 1000 } }
            };
            var triggered = new SenseEventFixture { principalEffects = new[] { effect } };
            SensePrincipalEffects.Validate(triggered, 9);
            principal.Reset();
            SensePrincipalEffects.Fire(triggered, principal, new LifeRuntime(999), 2);
            SensePrincipalEffects.Fire(triggered, principal, new LifeRuntime(1001), 2);
            Require(principal.GetAddingPrincipal(120, 2) == 120, "rejected triggers changed buffs");
            SensePrincipalEffects.Fire(triggered, principal, new LifeRuntime(1000), 2);
            Require(principal.GetAddingPrincipal(120, 2) == 128 &&
                principal.GetAddingPrincipal(120, 5) == 120, "inclusive LIFE conjunction / buff target");
            principal.TickBuffs(1099);
            Require(principal.GetAddingPrincipal(120, 2) == 128, "native 100ms tail");
            principal.TickBuffs(1);
            Require(principal.GetAddingPrincipal(120, 2) == 120, "triggered buff expiry");
            effect.effectType = 41;
            effect.value = 40;
            effect.durationMilliseconds = 0;
            SensePrincipalEffects.Fire(triggered, principal, new LifeRuntime(999), 2);
            Require(principal.GetMaxPrincipal(2) == 1000, "rejected limit effect");
            SensePrincipalEffects.Fire(triggered, principal, new LifeRuntime(1000), 2);
            Require(principal.GetMaxPrincipal(2) == 1040, "accepted limit effect");
            effect.triggers[0].value = (long)int.MaxValue + 1;
            Require(!SensePrincipalEffects.CanFire(new[] { effect.triggers[0] }, int.MaxValue), "Int64 LIFE threshold");
            effect.triggers[0].type = 3;
            RequireRejected(() => SensePrincipalEffects.Validate(triggered, 9));
            sense.principalBranchCondition = 12;
            RequireRejected(() => SensePrincipalEffects.Validate(sense, 9));
            principal.Reset();
            Require(principal.GetMaxPrincipal(2) == 1000 && principal.GetAddingPrincipal(120, 2) == 120,
                "reset clears branch effects");
            Debug.Log("OPENWDS_PRINCIPAL_BRANCHES passed=True masterSense=142390 guardConsumption=True " +
                "firstMatch=True lifeTriggers=True targetIsolation=True reset=True");
        }

        private static void Require(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Principal branch regression: " + name);
        }

        private static void RequireRejected(Action action)
        {
            try { action(); }
            catch (InvalidOperationException) { return; }
            throw new InvalidOperationException("Unsupported Principal condition was accepted.");
        }
    }
}
