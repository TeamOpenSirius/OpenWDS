using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using OpenWDS.Runtime;
using UnityEngine;
using UnityEngine.Networking;

namespace Sirius.GameResult
{
    /// <summary>Builds TMP fonts from the game's original OTF/TTF sources instead of blank exported atlases.</summary>
    public sealed class GameResultFontRuntime : IDisposable
    {
        public const string RelativeRoot = "OpenWDS/GameResultFonts";
        private static readonly string[] BundleFileNames =
        {
            "fontgroup_assets_ronowstd-gbs.bundle",
            "fontgroup_assets_udtypos515std-regular2.bundle",
        };
        private static readonly Dictionary<string, byte[]> PreparedBundleBytes =
            new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private static bool _prepareStarted;
        private static bool _prepareCompleted;
        private readonly List<LocalAssetBundleLease> _bundles = new List<LocalAssetBundleLease>();
        private readonly Dictionary<string, TMP_FontAsset> _fonts =
            new Dictionary<string, TMP_FontAsset>(StringComparer.Ordinal);
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
        /// cannot read that URI, so copy the two original source-font bundles to memory
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

        public GameResultFontRuntime()
        {
            var root = Path.Combine(Application.streamingAssetsPath, RelativeRoot);
            if (Directory.Exists(root))
            {
                foreach (var fileName in BundleFileNames)
                    AddBundle(LocalAssetBundleLease.FromFile(fileName, Path.Combine(root, fileName)));
            }
            else
            {
                foreach (var fileName in BundleFileNames)
                    if (PreparedBundleBytes.TryGetValue(fileName, out var bytes))
                        AddBundle(LocalAssetBundleLease.FromMemory(fileName, bytes));
            }
        }

        private void AddBundle(LocalAssetBundleLease lease)
        {
            var bundle = lease.Bundle;
            if (bundle == null) { lease.Dispose(); return; }
            _bundles.Add(lease);
            // Typed LoadAllAssets still deserializes TMP MonoBehaviours when the
            // source scene has been unloaded. Load only the original OTF/TTF by
            // their explicit container paths, then rebuild valid TMP assets.
            foreach (var assetName in bundle.GetAllAssetNames())
            {
                if (!assetName.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) &&
                    !assetName.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)) continue;
                var sourceFont = bundle.LoadAsset<Font>(assetName);
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
                label.font = font;
                label.fontSharedMaterial = font.material;
                label.SetAllDirty();
                applied++;
            }
            return applied;
        }

        public void Dispose()
        {
            foreach (var bundle in _bundles) bundle.Dispose();
            _bundles.Clear();
            _fonts.Clear();
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
