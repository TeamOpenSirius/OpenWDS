using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace OpenWDS.Runtime
{
    /// <summary>
    /// Original AppConst.SpritEffectSettingType values. The misspelling is kept
    /// because it is part of the serialized settings contract.
    /// </summary>
    public enum RecoveredSpritEffectSettingType
    {
        Rich = 0,
        Normal = 1,
        Light = 2,
    }

    /// <summary>Runtime loader/pool boundary for the original online SplitEffects.</summary>
    public sealed class RecoveredSplitLaneAssetRuntime : IDisposable
    {
        public const string RelativeRoot = "OpenWDS/SplitLane/1.96.0";
        private readonly List<AssetBundle> _bundles = new List<AssetBundle>();
        private readonly Dictionary<int, GameObject> _prefabs =
            new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Sirius.Game.SplitEffectController> _active =
            new Dictionary<int, Sirius.Game.SplitEffectController>();
        private readonly Transform _parent;
        private readonly bool _lightSetting;
        private readonly int _lineOpacity;

        public int BundleCount => _bundles.Count;
        public int PrefabCount => _prefabs.Count;

        private static readonly IReadOnlyDictionary<string, string> CompatibleShaderNames =
            new Dictionary<string, string>
            {
                {
                    "SplitEffect/ParticleAdditive",
                    "OpenWDS/Recovered/SplitEffect/ParticleAdditive"
                },
                {
                    "SplitEffect/ParticleAlpha",
                    "OpenWDS/Recovered/SplitEffect/ParticleAlpha"
                },
                {
                    "SplitEffect/SplitEffectSyuriken",
                    "OpenWDS/Recovered/SplitEffect/SplitEffectSyuriken"
                },
            };

        public RecoveredSplitLaneAssetRuntime(
            Transform parent,
            int spritEffectSettingType = (int)RecoveredSpritEffectSettingType.Normal,
            int splitEffectLineOpacity = 100)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _lightSetting = IsLightSetting(spritEffectSettingType);
            _lineOpacity = Mathf.Clamp(
                splitEffectLineOpacity,
                RecoveredGameSettings.MinimumSplitEffectLineOpacity,
                RecoveredGameSettings.MaximumSplitEffectLineOpacity);
            var root = Path.Combine(Application.streamingAssetsPath, RelativeRoot);
            if (!Directory.Exists(root)) return;
            var files = Directory.GetFiles(root, "*.bundle", SearchOption.AllDirectories)
                .OrderBy(path => path.Contains("game_splitlane_assets_spliteffects") ? 1 : 0)
                .ThenBy(path => path, StringComparer.Ordinal)
                .ToArray();
            foreach (var path in files)
            {
                var bundle = AssetBundle.LoadFromFile(path);
                if (bundle == null) continue;
                _bundles.Add(bundle);
                if (!path.Contains("game_splitlane_assets_spliteffects"))
                {
                    // Addressables normally materializes the dependency closure.
                    // Direct AssetBundle loading must do that before the main
                    // prefab's cross-bundle object references are deserialized.
                    bundle.LoadAllAssets<UnityEngine.Object>();
                    continue;
                }
                foreach (var prefab in bundle.LoadAllAssets<GameObject>())
                    if (int.TryParse(prefab.name, out var id)) _prefabs[id] = prefab;
            }
        }

        public void OnSplitLane(in RecoveredSplitLaneEntry entry)
        {
            if (entry.SplitLaneEffectId == 0) return;
            if (entry.ShouldShow)
            {
                if (!_prefabs.TryGetValue(entry.SplitLaneEffectId, out var prefab)) return;
                var instance = UnityEngine.Object.Instantiate(prefab, _parent, false);
                instance.name = "SplitEffect_" + entry.SplitLaneEffectId + "_" + entry.Id;
                ApplyCompatibleShaders(instance);
                var controller = instance.GetComponent<Sirius.Game.SplitEffectController>();
                if (controller == null)
                {
                    UnityEngine.Object.Destroy(instance);
                    return;
                }
                var minimumOpacity = RecoveredGameSettings.MinimumSplitEffectLineOpacity;
                var maximumOpacity = RecoveredGameSettings.MaximumSplitEffectLineOpacity;
                controller.Initialize(
                    _lightSetting,
                    _lineOpacity,
                    in minimumOpacity,
                    in maximumOpacity);
                controller.OnFadeIn(entry.SplitCount, (int)entry.SplitLaneType);
                ApplyCompatibleShaders(instance);
                _active[entry.Id] = controller;
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
                (int)RecoveredSpritEffectSettingType.Light;
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
        }
    }
}
