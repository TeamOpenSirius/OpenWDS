using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace Sirius.GameResult
{
    /// <summary>Loads the game's original TMP font assets instead of AssetRipper's blank atlases.</summary>
    public sealed class RecoveredGameResultFontRuntime : IDisposable
    {
        public const string RelativeRoot = "OpenWDS/GameResultFonts";
        private static readonly string[] BundleFileNames =
        {
            "fontgroup_assets_ronowstd-gbs.bundle",
            "fontgroup_assets_ronowstd-gbssdf.bundle",
            "fontgroup_assets_ronowstd-gbssdfwhite.bundle",
            "fontgroup_assets_udtypos515std-regular2.bundle",
            "fontgroup_assets_udtypos515std-regular2sdf.bundle",
        };
        private static readonly Dictionary<string, byte[]> PreparedBundleBytes =
            new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private static bool _prepareStarted;
        private static bool _prepareCompleted;
        private readonly List<AssetBundle> _bundles = new List<AssetBundle>();
        private readonly Dictionary<string, TMP_FontAsset> _fonts =
            new Dictionary<string, TMP_FontAsset>(StringComparer.Ordinal);
        private readonly Dictionary<string, Texture2D> _atlases =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Dictionary<string, Font> _sourceFonts =
            new Dictionary<string, Font>(StringComparer.Ordinal);
        private readonly List<TMP_FontAsset> _generatedFonts =
            new List<TMP_FontAsset>();
        private readonly List<string> _assets = new List<string>();

        public int BundleCount => _bundles.Count;
        public int FontCount => _fonts.Count;
        public string AssetSummary => string.Join(",", _assets.ToArray());
        public static bool IsStreamingAssetPreparationComplete =>
            _prepareCompleted;

        /// <summary>
        /// Android stores StreamingAssets inside the APK jar. AssetBundle.LoadFromFile
        /// cannot read that URI, so copy the five small local bundles to memory
        /// asynchronously while the song is being played.
        /// </summary>
        public static IEnumerator PrepareStreamingAssets()
        {
            if (_prepareStarted)
            {
                while (!_prepareCompleted) yield return null;
                yield break;
            }
            _prepareStarted = true;
            if (Directory.Exists(
                    Path.Combine(Application.streamingAssetsPath, RelativeRoot)))
            {
                _prepareCompleted = true;
                yield break;
            }

            foreach (var fileName in BundleFileNames)
            {
                var uri = Application.streamingAssetsPath.TrimEnd('/') + "/" +
                          RelativeRoot + "/" + fileName;
                using (var request = UnityWebRequest.Get(uri))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError(
                            "OPENWDS_RESULT_FONT_PREPARE_FAILED file=" +
                            fileName + " error=" + request.error);
                        continue;
                    }
                    PreparedBundleBytes[fileName] =
                        request.downloadHandler.data;
                }
            }
            _prepareCompleted = true;
            Debug.Log(
                "OPENWDS_RESULT_FONT_PREPARED bundles=" +
                PreparedBundleBytes.Count);
        }

        public RecoveredGameResultFontRuntime()
        {
            var root = Path.Combine(Application.streamingAssetsPath, RelativeRoot);
            if (Directory.Exists(root))
            {
                var paths = Directory.GetFiles(
                    root, "*.bundle", SearchOption.TopDirectoryOnly);
                Array.Sort(paths, StringComparer.Ordinal);
                foreach (var path in paths)
                    AddBundle(AssetBundle.LoadFromFile(path));
            }
            else
            {
                foreach (var fileName in BundleFileNames)
                    if (PreparedBundleBytes.TryGetValue(fileName, out var bytes))
                        AddBundle(AssetBundle.LoadFromMemory(bytes));
            }
        }

        private void AddBundle(AssetBundle bundle)
        {
            if (bundle == null) return;
            _bundles.Add(bundle);
                // AssetRipper emitted the original TMP SDF ScriptableObjects with
                // an unresolved MonoScript. Loading every bundle entry as Object
                // instantiates those broken objects and produces Unity's
                // "referenced script (Unknown)" warning. They were null here in
                // every supported bundle anyway; the original source Font and
                // atlas are sufficient to rebuild a valid TMP_FontAsset below.
                foreach (var assetName in bundle.GetAllAssetNames())
                    _assets.Add("declared:" + assetName);
                foreach (var atlas in bundle.LoadAllAssets<Texture2D>())
                {
                    if (atlas == null) continue;
                    _atlases[atlas.name] = atlas;
                    _assets.Add("texture:" + atlas.name + ":" +
                                atlas.width + "x" + atlas.height);
                }
                foreach (var sourceFont in bundle.LoadAllAssets<Font>())
                {
                    if (sourceFont == null) continue;
                    _sourceFonts[sourceFont.name] = sourceFont;
                    _assets.Add("font:" + sourceFont.name);
                }
        }

        public int Apply(GameObject root)
        {
            if (root == null) return 0;
            var applied = 0;
            foreach (var label in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (label.font == null) continue;
                var font = label.font;
                if (_fonts.TryGetValue(font.name, out var cached))
                {
                    font = cached;
                }
                else foreach (var pair in _sourceFonts)
                {
                    if (!font.name.StartsWith(pair.Key, StringComparison.Ordinal)) continue;
                    var generated = TMP_FontAsset.CreateFontAsset(pair.Value);
                    if (generated == null) break;
                    generated.name = font.name;
                    _generatedFonts.Add(generated);
                    _fonts[font.name] = generated;
                    font = generated;
                    break;
                }
                if (_fonts.TryGetValue(font.name, out var bundledFont))
                    font = bundledFont;
                var atlasName = font.name + " Atlas";
                if (_atlases.TryGetValue(atlasName, out var atlas) &&
                    !_generatedFonts.Contains(font))
                {
                    font.atlasTextures = new[] { atlas };
                    if (font.material != null) font.material.mainTexture = atlas;
                }
                label.font = font;
                label.fontSharedMaterial = font.material;
                label.SetAllDirty();
                applied++;
            }
            return applied;
        }

        public void Dispose()
        {
            foreach (var bundle in _bundles) bundle.Unload(false);
            _bundles.Clear();
            _fonts.Clear();
            _atlases.Clear();
            _sourceFonts.Clear();
            foreach (var font in _generatedFonts)
                if (font != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(font);
                    else UnityEngine.Object.DestroyImmediate(font);
                }
            _generatedFonts.Clear();
            _assets.Clear();
        }
    }
}
