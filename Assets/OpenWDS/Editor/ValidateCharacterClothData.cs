using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace OpenWDS.Editor
{
    // Loads the original ScriptableObjects, without instantiating an incomplete
    // character. VerifyData is the imported MagicaCloth implementation.
    public static class ValidateCharacterClothData
    {
        [Serializable] private sealed class BundleFile { public string path; public long bytes; }
        [Serializable] private sealed class Manifest { public bool dryRun; public BundleFile[] files; }
        [Serializable] private sealed class Row
        {
            public string bundle;
            public string type;
            public string name;
            public int savedVersion;
            public int runtimeVersion;
            public string result;
        }
        [Serializable] private sealed class Report
        {
            public string generatedAtUtc;
            public bool passed;
            public int originalStateBehaviours;
            public bool supportFixturesPassed;
            public string scope = "Original MeshData/ClothData/SelectionData deserialization; no character playback";
            public string failure;
            public List<Row> data = new List<Row>();
        }

        public static void Run()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            var report = new Report { generatedAtUtc = DateTime.UtcNow.ToString("O") };
            var bundles = new List<AssetBundle>();
            var errors = new List<string>();
            Application.LogCallback onLog = (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    errors.Add(message);
            };
            Application.logMessageReceived += onLog;
            try
            {
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(root,
                    "reverse/reports/online-downloads/1.96.0/result-character-probe.json")));
                if (manifest.dryRun || manifest.files == null || manifest.files.Length != 45)
                    throw new InvalidDataException("Expected the complete 45-bundle character probe closure.");
                foreach (var file in manifest.files)
                {
                    var path = Path.Combine(root, file.path);
                    if (new FileInfo(path).Length != file.bytes)
                        throw new InvalidDataException("Bundle size differs: " + path);
                    var bundle = AssetBundle.LoadFromFile(path);
                    if (bundle == null) throw new InvalidDataException("Could not load: " + path);
                    bundles.Add(bundle);
                }
                var names = new[] { "MeshData", "ClothData", "SelectionData" };
                var expected = new[] { 25, 25, 20 };
                for (var t = 0; t < names.Length; t++)
                {
                    var type = Type.GetType("MagicaCloth." + names[t] + ", MagicaCloth", true);
                    var count = 0;
                    for (var b = 0; b < bundles.Count; b++)
                        foreach (var asset in bundles[b].LoadAllAssets(type))
                        {
                            var row = new Row
                            {
                                bundle = manifest.files[b].path,
                                type = type.FullName,
                                name = asset.name,
                                savedVersion = (int)type.GetProperty("SaveDataVersion").GetValue(asset),
                                runtimeVersion = (int)type.GetMethod("GetVersion").Invoke(asset, null),
                                result = type.GetMethod("VerifyData").Invoke(asset, null).ToString()
                            };
                            report.data.Add(row);
                            count++;
                            if (row.savedVersion != row.runtimeVersion || row.result != "None")
                                throw new InvalidDataException(JsonUtility.ToJson(row));
                        }
                    if (count != expected[t])
                        throw new InvalidDataException(names[t] + " count=" + count + " expected=" + expected[t]);
                }
                report.originalStateBehaviours = ValidateCharacterSupport.Run(bundles);
                report.supportFixturesPassed = true;
                if (errors.Count != 0) throw new InvalidDataException(string.Join("\n", errors));
                report.passed = true;
            }
            catch (Exception exception)
            {
                report.failure = exception.ToString();
                throw;
            }
            finally
            {
                for (var i = bundles.Count - 1; i >= 0; i--) bundles[i].Unload(true);
                Application.logMessageReceived -= onLog;
                File.WriteAllText(Path.Combine(root, "reverse/reports/character-cloth-data-validation.json"),
                    JsonUtility.ToJson(report, true) + "\n");
                Debug.Log("OPENWDS_CHARACTER_CLOTH_DATA passed=" + report.passed + " count=" + report.data.Count);
            }
        }
    }
}
