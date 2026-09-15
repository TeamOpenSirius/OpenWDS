using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Original AppConst.SpritEffectSettingType values. The misspelling is kept
    /// because it is part of the serialized settings contract.
    /// </summary>
    public enum SpritEffectSettingType
    {
        Rich = 0,
        Normal = 1,
        Light = 2,
    }

    /// <summary>Runtime loader/pool boundary for the original online SplitEffects.</summary>
    public sealed class SplitLaneAssetRuntime : IDisposable
    {
        [Serializable]
        private sealed class SplitEffectIndex
        {
            public string assetVersion;
            public string catalogSha256;
            public int bundleCount;
            public int prefabCount;
            public int[] effectIds;
            public string[] bundlePaths;
            public SplitEffectBundleSet[] effects;
        }

        [Serializable]
        private sealed class SplitEffectBundleSet
        {
            public int effectId;
            public string[] bundlePaths;
        }

        public const string RelativeRoot = "OpenWDS/SplitLane/1.96.0";
        private readonly List<AssetBundle> _bundles = new List<AssetBundle>();
        private readonly Dictionary<int, GameObject> _prefabs =
            new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Sirius.Game.SplitEffectController> _active =
            new Dictionary<int, Sirius.Game.SplitEffectController>();
        private readonly Transform _parent;
        private readonly bool _lightSetting;
        private readonly int _lineOpacity;
        private readonly HashSet<int> _missingPrefabRequests = new HashSet<int>();
        private readonly HashSet<int> _shownEffectIds = new HashSet<int>();
        private static readonly Dictionary<string, byte[]> PreparedBundleBytes =
            new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private static string _preparedIndexJson;
        private static bool _preparationInProgress;
        private static string _prepareError;

        public int BundleCount => _bundles.Count;
        public int PrefabCount => _prefabs.Count;
        public int MissingPrefabRequestCount => _missingPrefabRequests.Count;
        public IReadOnlyCollection<int> PrefabIds => _prefabs.Keys;

        private static readonly IReadOnlyDictionary<string, string> CompatibleShaderNames =
            new Dictionary<string, string>
            {
                {
                    "SplitEffect/ParticleAdditive",
                    "OpenWDS/SplitEffect/ParticleAdditive"
                },
                {
                    "SplitEffect/ParticleAlpha",
                    "OpenWDS/SplitEffect/ParticleAlpha"
                },
                {
                    "SplitEffect/SplitEffectSyuriken",
                    "OpenWDS/SplitEffect/SplitEffectSyuriken"
                },
                {
                    "SplitEffect/ParticleTrailAdditive",
                    "OpenWDS/SplitEffect/ParticleTrailAdditive"
                },
                {
                    "SenceFX/ButterflyEffect",
                    "OpenWDS/SplitEffect/ButterflyEffect"
                },
            };

        /// <summary>
        /// Android keeps StreamingAssets inside the APK. Read only the bundle
        /// closures referenced by the selected notation before changing scenes.
        /// The original GameLoader likewise selects distinct non-zero effect IDs
        /// from the current notation before calling CreateGameSplitLane.
        /// </summary>
        public static IEnumerator PrepareStreamingAssets(string notationText)
        {
            var requestedIds = GetRequiredEffectIds(
                StandardNotation.Parse(notationText));
            var root = Path.Combine(Application.streamingAssetsPath, RelativeRoot);
            if (Directory.Exists(root)) yield break;

            while (_preparationInProgress) yield return null;
            _preparationInProgress = true;
            _prepareError = null;

            if (string.IsNullOrEmpty(_preparedIndexJson))
            {
                var indexUri = Application.streamingAssetsPath.TrimEnd('/') + "/" +
                               RelativeRoot + "/split-effect-index.json";
                using (var request = UnityWebRequest.Get(indexUri))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        CompletePreparationWithError(
                            "SplitEffect index request failed: " + request.error);
                        yield break;
                    }
                    _preparedIndexJson = request.downloadHandler.text;
                }
            }

            var index = JsonUtility.FromJson<SplitEffectIndex>(
                _preparedIndexJson);
            string indexError;
            if (!TryValidateIndex(index, out indexError))
            {
                CompletePreparationWithError(indexError);
                yield break;
            }

            string[] selectedPaths;
            try
            {
                selectedPaths = GetBundlePaths(index, requestedIds);
            }
            catch (Exception error)
            {
                CompletePreparationWithError(error.Message);
                yield break;
            }
            foreach (var relativePath in selectedPaths)
            {
                if (PreparedBundleBytes.ContainsKey(relativePath)) continue;
                var uri = Application.streamingAssetsPath.TrimEnd('/') + "/" +
                          RelativeRoot + "/" + relativePath;
                using (var request = UnityWebRequest.Get(uri))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        CompletePreparationWithError(
                            "SplitEffect bundle request failed: " +
                            relativePath + ": " + request.error);
                        yield break;
                    }
                    PreparedBundleBytes[relativePath] =
                        request.downloadHandler.data;
                }
            }
            _preparationInProgress = false;
            Debug.Log(
                "OPENWDS_SPLIT_EFFECT_STREAMING_PREPARED effects=" +
                requestedIds.Length + " bundles=" + selectedPaths.Length);
        }

        private static void CompletePreparationWithError(string error)
        {
            _prepareError = error;
            _preparationInProgress = false;
            Debug.LogError("OPENWDS_SPLIT_EFFECT_STREAMING_FAILED " + error);
        }

        public static void ThrowIfStreamingAssetPreparationFailed()
        {
            if (!string.IsNullOrEmpty(_prepareError))
                throw new InvalidOperationException(_prepareError);
        }

        private static bool TryValidateIndex(
            SplitEffectIndex index, out string error)
        {
            if (index == null || index.effectIds == null ||
                index.bundlePaths == null || index.effects == null ||
                index.bundleCount <= 0 || index.prefabCount <= 0 ||
                index.prefabCount != index.effectIds.Length ||
                index.effects.Length != index.prefabCount ||
                index.bundlePaths.Length != index.bundleCount)
            {
                error = "Offline SplitEffect index is malformed.";
                return false;
            }
            var indexedIds = new HashSet<int>(index.effectIds);
            if (indexedIds.Count != index.prefabCount ||
                index.effects.Any(effect => effect == null ||
                    effect.bundlePaths == null ||
                    !indexedIds.Contains(effect.effectId)) ||
                index.effects.Select(effect => effect.effectId).Distinct().Count() !=
                    index.prefabCount)
            {
                error = "Offline SplitEffect index has inconsistent effect closures.";
                return false;
            }
            error = null;
            return true;
        }

        private static int[] GetRequiredEffectIds(
            IEnumerable<NotationNote> notation)
        {
            if (notation == null) return Array.Empty<int>();
            return notation
                .Where(note => note != null &&
                    SplitLaneRuntime.IsSplitLane(note.GimmickType) &&
                    note.GimmickValue != 0)
                .Select(note => note.GimmickValue)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
        }

        private static string[] GetBundlePaths(
            SplitEffectIndex index, IEnumerable<int> requiredIds)
        {
            var sets = index.effects.ToDictionary(
                effect => effect.effectId, effect => effect.bundlePaths);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var effectId in requiredIds)
            {
                string[] closure;
                if (!sets.TryGetValue(effectId, out closure))
                    throw new InvalidDataException(
                        "Notation requested unknown SplitEffect ID " + effectId + ".");
                foreach (var path in closure) paths.Add(path);
            }
            return paths
                .OrderBy(path => path.Contains(
                    "game_splitlane_assets_spliteffects") ? 1 : 0)
                .ThenBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static byte[] GetPreparedBundleBytes(string relativePath)
        {
            byte[] bytes;
            if (!PreparedBundleBytes.TryGetValue(relativePath, out bytes))
                throw new InvalidDataException(
                    "SplitEffect bundle was not prepared: " + relativePath);
            return bytes;
        }

        public SplitLaneAssetRuntime(
            Transform parent,
            int spritEffectSettingType = (int)SpritEffectSettingType.Normal,
            int splitEffectLineOpacity = 100,
            IEnumerable<NotationNote> notation = null)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _lightSetting = IsLightSetting(spritEffectSettingType);
            _lineOpacity = Mathf.Clamp(
                splitEffectLineOpacity,
                GameSettings.MinimumSplitEffectLineOpacity,
                GameSettings.MaximumSplitEffectLineOpacity);
            var loadTimer = System.Diagnostics.Stopwatch.StartNew();
            var root = Path.Combine(Application.streamingAssetsPath, RelativeRoot);
            var hasFileSystemRoot = Directory.Exists(root);
            if (!hasFileSystemRoot)
                ThrowIfStreamingAssetPreparationFailed();
            var indexPath = Path.Combine(root, "split-effect-index.json");
            if (hasFileSystemRoot && !File.Exists(indexPath))
                throw new FileNotFoundException(
                    "Offline SplitEffect index is missing.", indexPath);
            var index = JsonUtility.FromJson<SplitEffectIndex>(
                hasFileSystemRoot
                    ? File.ReadAllText(indexPath)
                    : _preparedIndexJson);
            string indexError;
            if (!TryValidateIndex(index, out indexError))
                throw new InvalidDataException(indexError + " " + indexPath);
            var requiredIds = notation != null
                ? GetRequiredEffectIds(notation)
                : index.effectIds.OrderBy(value => value).ToArray();
            var expectedIds = new HashSet<int>(requiredIds);
            var bundlePaths = GetBundlePaths(index, requiredIds);
            foreach (var relativePath in bundlePaths)
            {
                var path = Path.Combine(root, relativePath);
                var bundle = hasFileSystemRoot
                    ? AssetBundle.LoadFromFile(path)
                    : AssetBundle.LoadFromMemory(GetPreparedBundleBytes(relativePath));
                if (bundle == null)
                    throw new InvalidDataException(
                        "Failed to load offline SplitEffect bundle: " + relativePath);
                _bundles.Add(bundle);
                if (!relativePath.Contains("game_splitlane_assets_spliteffects"))
                {
                    // Addressables normally materializes the dependency closure.
                    // Direct AssetBundle loading must do that before the main
                    // prefab's cross-bundle object references are deserialized.
                    bundle.LoadAllAssets<UnityEngine.Object>();
                    continue;
                }
                foreach (var prefab in bundle.LoadAllAssets<GameObject>())
                {
                    if (!int.TryParse(prefab.name, out var id)) continue;
                    if (_prefabs.ContainsKey(id))
                        throw new InvalidDataException(
                            "Duplicate offline SplitEffect prefab ID: " + id);
                    _prefabs.Add(id, prefab);
                }
            }
            if (_prefabs.Count != expectedIds.Count ||
                !_prefabs.Keys.All(expectedIds.Contains) ||
                !expectedIds.All(_prefabs.ContainsKey))
                throw new InvalidDataException(string.Format(
                    "Offline SplitEffect prefab index mismatch: expected {0}, loaded {1}.",
                    expectedIds.Count, _prefabs.Count));
            if (!hasFileSystemRoot) PreparedBundleBytes.Clear();
            loadTimer.Stop();
            Debug.Log(string.Format(
                "OPENWDS_SPLIT_EFFECT_LIBRARY_LOADED effects={0} bundles={1} " +
                "prefabs={2} elapsedMs={3}",
                expectedIds.Count, _bundles.Count, _prefabs.Count,
                loadTimer.ElapsedMilliseconds));
        }

        public void OnSplitLane(in SplitLaneEntry entry)
        {
            if (entry.SplitLaneEffectId == 0) return;
            if (entry.ShouldShow)
            {
                if (!_prefabs.TryGetValue(entry.SplitLaneEffectId, out var prefab))
                {
                    if (_missingPrefabRequests.Add(entry.SplitLaneEffectId))
                        Debug.LogError(
                            "Notation requested missing offline SplitEffect ID " +
                            entry.SplitLaneEffectId + ".");
                    return;
                }
                var instance = UnityEngine.Object.Instantiate(prefab, _parent, false);
                instance.name = "SplitEffect_" + entry.SplitLaneEffectId + "_" + entry.Id;
                ApplyCompatibleShaders(instance);
                var controller = instance.GetComponent<Sirius.Game.SplitEffectController>();
                if (controller == null)
                {
                    UnityEngine.Object.Destroy(instance);
                    return;
                }
                var minimumOpacity = GameSettings.MinimumSplitEffectLineOpacity;
                var maximumOpacity = GameSettings.MaximumSplitEffectLineOpacity;
                controller.Initialize(
                    _lightSetting,
                    _lineOpacity,
                    in minimumOpacity,
                    in maximumOpacity);
                controller.OnFadeIn(entry.SplitCount, (int)entry.SplitLaneType);
                ApplyCompatibleShaders(instance);
                _active[entry.Id] = controller;
                if (_shownEffectIds.Add(entry.SplitLaneEffectId))
                    Debug.Log("OPENWDS_SPLIT_EFFECT_SHOWN id=" +
                        entry.SplitLaneEffectId);
                return;
            }

            if (!_active.TryGetValue(entry.Id, out var active)) return;
            active.OnFadeOut();
            _active.Remove(entry.Id);
            UnityEngine.Object.Destroy(active.gameObject, 0.5f);
        }

        public static bool IsLightSetting(int spritEffectSettingType)
        {
            // SplitEffectObjectPool.CreateInstance ARM64 compares the enum value
            // with literal 2 and passes the equality result to Initialize.
            return spritEffectSettingType ==
                (int)SpritEffectSettingType.Light;
        }

        public static int ApplyCompatibleShaders(GameObject root)
        {
            if (root == null) return 0;
            var replacements = 0;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null ||
                        !CompatibleShaderNames.TryGetValue(material.shader.name,
                            out var compatibleName))
                        continue;
                    var compatible = Shader.Find(compatibleName);
                    if (compatible == null || !compatible.isSupported) continue;
                    material.shader = compatible;
                    replacements++;
                }
            }
            return replacements;
        }

        public void Dispose()
        {
            foreach (var active in _active.Values)
                if (active != null) UnityEngine.Object.Destroy(active.gameObject);
            _active.Clear();
            foreach (var bundle in _bundles) bundle.Unload(false);
            _bundles.Clear();
            _prefabs.Clear();
            _missingPrefabRequests.Clear();
            _shownEffectIds.Clear();
        }
    }
}
