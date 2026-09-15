using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenWDS.Runtime;
using UnityEditor;
using UnityEngine;

namespace OpenWDS.Editor
{
    public static class ValidateAllSplitLaneEffects
    {
        [Serializable]
        private sealed class Report
        {
            public int bundleCount;
            public int prefabCount;
            public int validatedPrefabCount;
            public int chartFileCount;
            public int chartSplitLaneEventCount;
            public int[] chartReferencedEffectIds;
            public string[] recoveredShaderNames;
            public int missingPrefabRequestCount;
            public string[] failures;
            public bool valid;
        }

        public static void Run()
        {
            var failures = new List<string>();
            var referencedIds = new HashSet<int>();
            var chartFileCount = 0;
            var chartSplitLaneEventCount = 0;
            var chartsRoot = Path.Combine(
                Application.streamingAssetsPath, "OpenWDS/StandardCharts");
            foreach (var path in Directory.GetFiles(
                         chartsRoot, "*.csv", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(path) == "music_config.csv") continue;
                chartFileCount++;
                foreach (var note in StandardNotation.Parse(
                             File.ReadAllText(path)))
                {
                    if (!SplitLaneRuntime.IsSplitLane(note.GimmickType))
                        continue;
                    chartSplitLaneEventCount++;
                    referencedIds.Add(note.GimmickValue);
                }
            }

            var shaderNames = new[]
            {
                "OpenWDS/SplitEffect/ParticleAdditive",
                "OpenWDS/SplitEffect/ParticleAlpha",
                "OpenWDS/SplitEffect/SplitEffectSyuriken",
                "OpenWDS/SplitEffect/ParticleTrailAdditive",
                "OpenWDS/SplitEffect/ButterflyEffect",
            };
            var recoveredShaderNames = new HashSet<string>();
            foreach (var shaderName in shaderNames)
            {
                var shader = Shader.Find(shaderName);
                if (shader == null || !shader.isSupported)
                    failures.Add("missing-or-unsupported-shader:" + shaderName);
            }

            var parent = new GameObject("SplitEffectValidationParent");
            SplitLaneAssetRuntime runtime = null;
            var validatedPrefabCount = 0;
            try
            {
                runtime = new SplitLaneAssetRuntime(parent.transform);
                var prefabIds = runtime.PrefabIds.OrderBy(value => value).ToArray();
                foreach (var referencedId in referencedIds)
                    if (!prefabIds.Contains(referencedId))
                        failures.Add("chart-missing-effect:" + referencedId);

                var instanceId = 1;
                foreach (var effectId in prefabIds)
                {
                    var note = new NotationNote
                    {
                        Id = instanceId++,
                        StartTickCount = 0f,
                        EndTickCount = 2f,
                        GimmickType = 33,
                        GimmickValue = effectId,
                    };
                    var entry = new SplitLaneEntry(note, 33, true, false);
                    var before = parent.transform.childCount;
                    runtime.OnSplitLane(in entry);
                    if (parent.transform.childCount != before + 1)
                    {
                        failures.Add("instantiate-failed:" + effectId);
                        continue;
                    }
                    var instance = parent.transform.GetChild(
                        parent.transform.childCount - 1).gameObject;
                    var controller = instance.GetComponent<
                        Sirius.Game.SplitEffectController>();
                    if (controller == null)
                        failures.Add("missing-controller:" + effectId);
                    else
                    {
                        var serialized = new SerializedObject(controller);
                        var elements = serialized.FindProperty("_splitEffectElements");
                        if (elements == null || !elements.isArray ||
                            elements.arraySize == 0)
                            failures.Add("missing-element-array:" + effectId);
                        else
                            for (var index = 0; index < elements.arraySize; index++)
                                if (elements.GetArrayElementAtIndex(index)
                                        .objectReferenceValue == null)
                                {
                                    failures.Add(string.Format(
                                        "unresolved-element:{0}:{1}", effectId, index));
                                    break;
                                }
                    }
                    var missingScripts = instance.GetComponentsInChildren<Component>(true)
                        .Count(component => component == null);
                    if (missingScripts != 0)
                        failures.Add("missing-script:" + effectId + ":" + missingScripts);
                    var unsupported = new HashSet<string>();
                    foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                        foreach (var material in renderer.sharedMaterials)
                        {
                            if (material == null || material.shader == null) continue;
                            if (material.shader.name.StartsWith(
                                    "OpenWDS/SplitEffect/",
                                    StringComparison.Ordinal))
                                recoveredShaderNames.Add(material.shader.name);
                            if (!material.shader.isSupported)
                                unsupported.Add(material.shader.name);
                        }
                    if (unsupported.Count != 0)
                        failures.Add("unsupported-shader:" + effectId + ":" +
                            string.Join("/", unsupported.ToArray()));
                    if (instance.GetComponentsInChildren<SpriteRenderer>(true).Length != 7)
                        failures.Add("line-count:" + effectId);
                    if (controller != null && missingScripts == 0 && unsupported.Count == 0)
                        validatedPrefabCount++;
                    UnityEngine.Object.DestroyImmediate(instance);
                }

                var report = new Report
                {
                    bundleCount = runtime.BundleCount,
                    prefabCount = runtime.PrefabCount,
                    validatedPrefabCount = validatedPrefabCount,
                    chartFileCount = chartFileCount,
                    chartSplitLaneEventCount = chartSplitLaneEventCount,
                    chartReferencedEffectIds = referencedIds.OrderBy(value => value).ToArray(),
                    recoveredShaderNames = recoveredShaderNames.OrderBy(
                        value => value).ToArray(),
                    missingPrefabRequestCount = runtime.MissingPrefabRequestCount,
                    failures = failures.ToArray(),
                };
                report.valid = report.bundleCount == 790 &&
                    report.prefabCount == 318 &&
                    report.validatedPrefabCount == 318 &&
                    report.chartFileCount == 50 &&
                    report.chartSplitLaneEventCount == 322 &&
                    report.chartReferencedEffectIds.Length == 12 &&
                    shaderNames.All(report.recoveredShaderNames.Contains) &&
                    report.missingPrefabRequestCount == 0 &&
                    report.failures.Length == 0;
                var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                var reportPath = Path.GetFullPath(Path.Combine(
                    projectRoot, "../../reverse/reports/",
                    "unity-all-split-lane-effects-validation.json"));
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true) + "\n");
                Debug.Log(string.Format(
                    "OPENWDS_ALL_SPLIT_EFFECTS_VALIDATION valid={0} " +
                    "bundles={1} prefabs={2}/{3} charts={4} events={5} ids={6} failures={7}",
                    report.valid,
                    report.bundleCount,
                    report.validatedPrefabCount,
                    report.prefabCount,
                    report.chartFileCount,
                    report.chartSplitLaneEventCount,
                    string.Join(",", report.chartReferencedEffectIds.Select(
                        value => value.ToString()).ToArray()),
                    report.failures.Length));
                if (!report.valid)
                    throw new InvalidOperationException(
                        "All SplitEffect validation failed: " +
                        string.Join(";", report.failures));
            }
            finally
            {
                runtime?.Dispose();
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }
    }
}
