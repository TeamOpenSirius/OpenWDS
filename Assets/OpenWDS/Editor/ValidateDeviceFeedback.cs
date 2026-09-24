using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenWDS.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenWDS.Editor
{
    public static class ValidateDeviceFeedback
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/OpenWDS/Scenes/OfflineRhythmPreview.unity");
            var game = UnityEngine.Object.FindObjectOfType<GameRuntime>();
            var serialized = new SerializedObject(game);
            var camera = (Camera)serialized.FindProperty("_gameCamera").objectReferenceValue;
            var lanes = (Sirius.Game.LaneGroup)serialized.FindProperty("_laneGroup").objectReferenceValue;
            var managers = lanes.ColliderManagers;
            managers.InitializeMappings();
            Physics.SyncTransforms();
            var hits = new Dictionary<int, HitLaneEntity>();
            using (var raycaster = new LaneRaycaster(camera, managers.MainColliders,
                       managers.SubLeftInnerColliders, managers.SubRightInnerColliders,
                       managers.SubLeftOuterColliders, managers.SubRightOuterColliders))
                for (var lane = 1; lane <= 12; lane++)
                    hits[lane] = raycaster.RaycastPoint(camera.WorldToScreenPoint(lanes.GetLaneCollider(lane).position));
            var notes = StandardNotation.Parse(File.ReadAllText(SongResourceStore.Resolve("OpenWDS/StandardCharts/162/1/5.csv")));
            var trace = new List<string>();
            var blue = notes.Single(n => n.Id == 585);
            var purple = notes.Single(n => n.Id == 589);
            var prefabsProperty = serialized.FindProperty("_noteObjectPrefabs");
            var prefabs = Enumerable.Range(0, prefabsProperty.arraySize).Select(i =>
                (GameObject)prefabsProperty.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
            var bombsProperty = serialized.FindProperty("_defaultBombEffectPrefabs");
            var bombs = Enumerable.Range(0, bombsProperty.arraySize).Select(i =>
                (GameObject)bombsProperty.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
            int cases = 0;
            foreach (var reverse in new[] { false, true })
            foreach (var shared in new[] { false, true })
            foreach (var phase in new[] { OpenWDS.Runtime.TouchPhase.Stationary, OpenWDS.Runtime.TouchPhase.Moved })
            {
                var oldBody = reverse ? purple : blue;
                var target = reverse ? blue : purple;
                var slice = new[] { blue, purple };
                var now = Math.Max(blue.StartMilliseconds, purple.StartMilliseconds) + 100;
                var hit = hits[target.Lane];
                var action = new UnifiedHoldAction(new StandardHoldNoteManager(slice), new ScratchNoteManager(slice));
                var input = new InputEntity(1, now, new Vector2(oldBody.Lane, 0),
                    new Vector2(target.Lane, 0), Vector2.zero, phase);
                var switched = action.TryHold(input, hit, now, now, oldBody, shared);
                Require(switched.AssignmentNote == target, $"Neustart Main/Sub transfer {oldBody.Id}->{target.Id}: {switched.AssignmentNote?.Id}");
                var owner = new GameObject("HoldTransferCase");
                owner.transform.SetParent(lanes.NoteParent, false);
                try
                {
                    var visuals = new NoteVisualRuntime(slice, owner.transform, prefabs, 5,
                        scratchHoldMaterial: (Material)serialized.FindProperty("_scratchHoldMaterial").objectReferenceValue,
                        isActiveConcurrentLine: false);
                    var effects = new LaneEffectRuntime(lanes, owner.transform,
                        (GameObject)serialized.FindProperty("_beamEffectPrefab").objectReferenceValue, bombs);
                    var holds = new LaneHoldManager();
                    var events = new List<InputEffectEntity>();
                    visuals.Tick(now);
                    holds.Set(1, oldBody);
                    if (shared) holds.Set(2, oldBody);
                    holds.GetHoldEvents(events);
                    foreach (var e in events) { visuals.OnHold(e.NoteId, true); effects.OnEffect(e); }
                    events.Clear();
                    holds.Set(1, switched.AssignmentNote);
                    holds.GetHoldEvents(events);
                    Require(events.Count == (shared ? 1 : 2), "Hold transfer event deduplication");
                    foreach (var e in events)
                    {
                        Require(visuals.OnHold(e.NoteId, e.EffectType == InputEffectType.HoldStart), "Hold visual missed its ownership event");
                        effects.OnEffect(e);
                    }
                    visuals.Tick(now);
                    var body = owner.transform.Find($"Runtime_{target.Id}_{target.NoteType}");
                    Require(Mathf.Abs(body.localPosition.y) < .0001f, "New Hold was not clipped at the judgement line");
                    var line = body.GetComponentsInChildren<SpriteRenderer>().First(r => r.transform.parent == body && r.drawMode != SpriteDrawMode.Simple);
                    var block = new MaterialPropertyBlock(); line.GetPropertyBlock(block);
                    Require(block.GetInteger("IsTouchMask") == 1, "New Hold touch shader mask missing");
                    var previousEffect = owner.GetComponentsInChildren<Sirius.Game.HoldEffectController>()
                        .Single(c => c.gameObject.name == "Runtime_Hold_" + oldBody.Id);
                    Require(previousEffect.GetComponentsInChildren<ParticleSystem>(true).All(p => p.main.loop == shared),
                        "Old lane effect must end unless another finger still holds it");
                    trace.Add($"TRANSFER {oldBody.Id}->{target.Id} phase={phase} shared={shared} mask=1 oldLoop={shared}");
                    cases++;
                }
                finally { UnityEngine.Object.DestroyImmediate(owner); }
            }
            using (var runtime = new InputHandlerRuntime(notes, 0f, n => new Vector2(n.Lane, 0), p => hits[(int)p.x]))
            {
                for (long t = -100; t < 94000; t += 8)
                {
                    runtime.Tick(t, t + 100);
                    foreach (var e in runtime.HoldEvents)
                        trace.Add($"{t} {e.EffectType} id={e.NoteId} type={e.NoteType} lane={e.LaneId}");
                    if (t > 43500 && t < 47200)
                        foreach (var pair in runtime.ActiveTouchHolds)
                            trace.Add($"{t} touch={pair.Key} id={pair.Value.Id} type={pair.Value.NoteType} lane={pair.Value.Lane}");
                }
            }
            File.WriteAllLines("../../reverse/reports/neustart-hold-trace.txt", trace);
            File.WriteAllText("../../reverse/reports/device-feedback-validation.json",
                "{\"passed\":true,\"neustartTransferCases\":" + cases + "}");
            Debug.Log($"OPENWDS_DEVICE_FEEDBACK passed=True transferCases={cases}");
        }
        private static void Require(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException(message);
        }
    }
}
