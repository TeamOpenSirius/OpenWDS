using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OpenWDS.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TouchPhase = OpenWDS.Runtime.TouchPhase;

namespace OpenWDS.Editor
{
    /// <summary>Device feedback: actual FINISH animation, split settings after animation evaluation, and held tail cleanup.</summary>
    public static class ValidateDeviceFourIssues
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] private sealed class Report
        {
            public bool passed;
            public int splitCases, holdCases;
            public bool finishAnimation;
            public List<string> observations = new List<string>();
            public List<string> failures = new List<string>();
        }
        private static Report _report;
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
        private static void Check(bool valid, string message)
        {
            if (!valid) _report.failures.Add(message);
        }
        public static void Run()
        {
            _report = new Report();
            try
            {
                EditorSceneManager.OpenScene("Assets/OpenWDS/Scenes/OfflineRhythmPreview.unity");
                var game = UnityEngine.Object.FindObjectOfType<GameRuntime>();
                ValidateFinish(game);
                ValidateSplits();
                ValidateHolds(game);
            }
            catch (Exception e) { _report.failures.Add(e.ToString()); }
            _report.passed = _report.failures.Count == 0 && _report.finishAnimation &&
                _report.splitCases == 216 && _report.holdCases == 16;
            File.WriteAllText("../../reverse/reports/device-four-issues-validation.json", JsonUtility.ToJson(_report, true));
            Debug.Log($"OPENWDS_DEVICE_FOUR_ISSUES passed={_report.passed} split={_report.splitCases} hold={_report.holdCases} failures={_report.failures.Count}");
            if (!_report.passed) throw new InvalidOperationException(string.Join("\n", _report.failures.Take(20)));
        }
        public static void ValidateSplitsInPlayMode()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Requires PlayMode.");
            _report = new Report();
            ValidateSplits();
            _report.passed = _report.splitCases == 216 && _report.failures.Count == 0;
            File.WriteAllText("../../reverse/reports/split-settings-playmode.json", JsonUtility.ToJson(_report, true));
            if (!_report.passed) throw new InvalidOperationException(string.Join("\n", _report.failures.Take(20)));
        }

        private static void ValidateFinish(GameRuntime game)
        {
            var instance = UnityEngine.Object.Instantiate(Field<GameObject>(game, "_clearAnimationPrefab"));
            var panel = instance.GetComponent<Sirius.Game.GameResultPanel>();
            var animator = Field<Animator>(panel, "_animator");
            panel.Show(GameRuntime.GetBoundaryClearType(new GameResultRuntime(Array.Empty<NotationNote>()), false));
            animator.Update(0f);
            animator.Update(1.2f);
            var finish = panel.transform.Find("FINISH");
            var failed = panel.transform.Find("FAILED");
            _report.finishAnimation = finish != null && finish.gameObject.activeInHierarchy &&
                (failed == null || !failed.gameObject.activeInHierarchy) &&
                animator.GetCurrentAnimatorStateInfo(0).IsName("ClearAnimation_finish_anim");
            Check(_report.finishAnimation, "Zero-Life must play the authored FINISH clip, with FAILED hidden.");
            _report.observations.Add("FINISH state=" + animator.GetCurrentAnimatorStateInfo(0).fullPathHash +
                " active=" + (finish != null && finish.gameObject.activeInHierarchy));
            panel.Hide();
            UnityEngine.Object.DestroyImmediate(instance);
        }
        private static void ValidateSplits()
        {
            var ids = new[] { 1, 10010, 3010 };
            var notation = ids.Select((id, i) => new NotationNote { Id = i + 1, GimmickType = 33, GimmickValue = id }).ToArray();
            foreach (var setting in new[] { 0, 1, 2 })
            foreach (var opacity in new[] { 10, 100 })
            {
                var parent = new GameObject("SplitSettingsValidation");
                try
                {
                    using (var runtime = new SplitLaneAssetRuntime(parent.transform, setting, opacity, notation))
                    foreach (var id in ids)
                    foreach (var count in new[] { 1, 3, 6 })
                    foreach (var type in new[] { 1, 3, 5, 7 })
                    {
                        var note = new NotationNote { Id = ++_report.splitCases, GimmickType = type * 10 + count, GimmickValue = id };
                        var scheduler = new SplitLaneRuntime(new[] { note }, setting);
                        scheduler.Tick(-1000);
                        var entry = scheduler.FrameEntries.Single();
                        var expectedType = type == 1 && setting == 0 ? 3 : type;
                        Check((int)entry.SplitLaneType == expectedType, "Split setting conversion " + setting + ":" + type);
                        runtime.OnSplitLane(entry);
                        var root = parent.transform.GetChild(parent.transform.childCount - 1).gameObject;
                        try
                        {
                            var control = root.GetComponent<Sirius.Game.SplitEffectController>();
                            var lines = Field<SpriteRenderer[]>(control, "_splitLines");
                            var elements = Field<Dictionary<int, Sirius.Game.SplitEffectElement>>(control, "_splitEffects");
                            var animator = Field<Animator>(control, "_splitEffectAnimator");
                            var positions = lines.Select(l => l.transform.localPosition).ToArray();
                            var label = $"id={id} setting={setting} opacity={opacity} split={count} type={type}";
                            // Validate after the actual bundled animator has evaluated, not just after Initialize.
                            foreach (var dt in new[] { 0f, .25f, .5f })
                            {
                                animator.Update(dt);
                                for (var i = 0; i < lines.Length; i++)
                                {
                                    var expected = elements[i].LineColor;
                                    expected.r *= opacity / 100f; expected.g *= opacity / 100f; expected.b *= opacity / 100f;
                                    Check(Vector3.Distance(new Vector3(lines[i].color.r, lines[i].color.g, lines[i].color.b),
                                        new Vector3(expected.r, expected.g, expected.b)) < .002f, "Split opacity overwritten " + label + " line=" + i);
                                    Check(Vector3.Distance(positions[i], lines[i].transform.localPosition) < .001f, "Split position overwritten " + label + " line=" + i);
                                    Check(lines[i].enabled == (i <= count), "Split visibility overwritten " + label + " line=" + i);
                                    var expectedPlaying = i <= count && (type == 7 || (setting != 2 && type != 5)) &&
                                        (expectedType != 1 || i == 0 || i == count);
                                    if (!expectedPlaying)
                                        foreach (var particle in elements[i].GetComponentsInChildren<ParticleSystem>(true))
                                            Check(!particle.isPlaying && particle.particleCount == 0, "Suppressed split still emits " + label + " line=" + i + " particle=" + particle.name);
                                    foreach (var particle in elements[i].LineEffects)
                                        Check(particle.isPlaying == (expectedPlaying && particle.gameObject.activeInHierarchy), "Split particle branch " + label + " line=" + i + " particle=" + particle.name + " playing=" + particle.isPlaying);
                                }
                            }
                            _report.observations.Add(label + " rgb=" + lines[0].color + " particles=" + elements.Values.Sum(e => e.LineEffects.Count(p => p.isPlaying)));
                        }
                        finally { UnityEngine.Object.DestroyImmediate(root); }
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(parent); }
            }
        }
        private static void ValidateHolds(GameRuntime game)
        {
            var lanes = Field<Sirius.Game.LaneGroup>(game, "_laneGroup");
            var managers = lanes.ColliderManagers;
            managers.InitializeMappings(); Physics.SyncTransforms();
            var camera = Field<Camera>(game, "_gameCamera");
            HitLaneEntity hit;
            using (var raycaster = new LaneRaycaster(camera, managers.MainColliders, managers.SubLeftInnerColliders,
                       managers.SubRightInnerColliders, managers.SubLeftOuterColliders, managers.SubRightOuterColliders))
                hit = raycaster.RaycastPoint(camera.WorldToScreenPoint(lanes.GetLaneCollider(6).position));
            foreach (var type in new[] { NoteType.Hold, NoteType.CriticalHold, NoteType.ScratchHold, NoteType.ScratchCriticalHold })
            foreach (var phase in new[] { TouchPhase.Stationary, TouchPhase.Moved })
            foreach (var fingers in new[] { 1, 2 })
            {
                var body = new NotationNote { Id = 2, NoteType = (int)type, Lane = 6, Width = 1, StartTickCount = 1, EndTickCount = 2 };
                var startType = (int)type >= 110 ? (int)type - 28 : (int)type - 20;
                var start = new NotationNote { Id = 1, NoteType = startType, Lane = 6, Width = 1, StartTickCount = 1, EndTickCount = 1 };
                var parent = new GameObject("HeldTailValidation");
                parent.transform.SetParent(lanes.NoteParent, false);
                try
                {
                    var effects = new LaneEffectRuntime(lanes, parent.transform, Field<GameObject>(game, "_beamEffectPrefab"),
                        Field<GameObject[]>(game, "_defaultBombEffectPrefabs"));
                    using (var input = new InputHandlerRuntime(new[] { start, body }, 0, n => Vector2.zero, p => hit))
                    {
                        var ends = 0; var tailResults = 0; var seenLoop = false;
                        foreach (var ms in new long[] { 1000, 1100, 1500, 1999, 2000, 2008, 2100, 2400 })
                        {
                            // Scratch tails require a real flick; continue holding after that
                            // successful judgement, without synthesizing a finger-up event.
                            var scratchTail = ms == 2000 && (int)type >= 110;
                            var touches = Enumerable.Range(1, fingers).Select(id => new InputEntity(id, ms, Vector2.zero,
                                Vector2.zero, scratchTail ? new Vector2(100f, 0f) : Vector2.zero,
                                ms == 1000 ? TouchPhase.Began : scratchTail ? TouchPhase.Moved : phase)).ToArray();
                            input.TickPlayer(ms, ms, touches);
                            foreach (var e in input.HoldEvents) { effects.OnEffect(e); if (e.EffectType == InputEffectType.HoldEnd) ends++; }
                            tailResults += input.InputResults.Count(r => r.NoteId == body.Id);
                            var hold = parent.GetComponentsInChildren<Sirius.Game.HoldEffectController>(true)
                                .FirstOrDefault(c => c.name == "Runtime_Hold_" + body.Id);
                            if (ms == 1500) seenLoop = hold != null && hold.GetComponentsInChildren<ParticleSystem>(true).All(p => p.main.loop);
                            if (ms >= 2008)
                            {
                                Check(input.ActiveTouchHoldCount == 0, $"Held tail retains ownership type={type} phase={phase} fingers={fingers} t={ms}");
                                Check(hold != null && hold.GetComponentsInChildren<ParticleSystem>(true).All(p => !p.main.loop), "Held tail keeps emitting: " + type);
                            }
                        }
                        Check(seenLoop && ends == 1 && tailResults == 1, $"Held tail lifecycle type={type} phase={phase} fingers={fingers} loop={seenLoop} ends={ends} results={tailResults}");
                        _report.holdCases++;
                        _report.observations.Add($"HOLD type={type} phase={phase} fingers={fingers} ends={ends} results={tailResults}");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(parent); }
            }
        }
    }
}
