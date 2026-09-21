using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OpenWDS.Runtime
{
    /// <summary>Owns one view's catalog dependency closure, sharing canonical bundles with retained pages.</summary>
    public sealed class FrontendViewLibrary : IDisposable
    {
        [Serializable] public sealed class BundleRow { public int id; public string key, path; }
        [Serializable] public sealed class AssetRow { public string key, internalId; public int bundle; public int[] dependencies; }
        [Serializable] public sealed class Binding { public int bundle; public string owner, type, field, path; }
        [Serializable] public sealed class Index { public BundleRow[] bundles; public AssetRow[] assets; public Binding[] bindings; public SpriteBinding[] sprites; }
        [Serializable] public sealed class SpriteBinding { public string type, field, name; public int ordinal; }
        private readonly List<Sprite> _atlasSprites = new List<Sprite>();
        private readonly Dictionary<int, LocalAssetBundleLease> _leases = new Dictionary<int, LocalAssetBundleLease>();
        private readonly Dictionary<Material, Material> _materials = new Dictionary<Material, Material>();
        private readonly Dictionary<string, TMP_FontAsset> _fonts = new Dictionary<string, TMP_FontAsset>();
        private readonly List<Material> _fontMaterials = new List<Material>();
        private readonly Shader _spriteShader, _fontShader;
        private readonly Dictionary<TMP_SpriteAsset, TMP_SpriteAsset> _spriteAssets = new Dictionary<TMP_SpriteAsset, TMP_SpriteAsset>();
        public FrontendViewLibrary(Shader spriteShader, Shader fontShader) { _spriteShader = spriteShader; _fontShader = fontShader; }
        private Index _index;
        private AssetRow _asset;
        private bool _disposed;
        public int BundleCount => _leases.Count;
        public GameObject Instance { get; private set; }

        private IEnumerator Prepare(string key)
        {
            if (_disposed || _index != null) throw new InvalidOperationException("A view scope can load only once.");
            byte[] bytes = null;
            yield return StreamingAssetsRuntime.ReadBytes("OpenWDS/Frontend/index.json", data => bytes = data);
            if (_disposed) yield break;
            _index = JsonUtility.FromJson<Index>(Encoding.UTF8.GetString(bytes));
            _asset = _index.assets.Single(a => a.key == key);
            foreach (var id in _asset.dependencies.Distinct())
            {
                var row = _index.bundles.Single(b => b.id == id);
                LocalAssetBundleLease lease = null;
                yield return LocalAssetBundleLease.LoadStreaming(row.key, row.path, value =>
                {
                    lease = value;
                    if (_disposed) value.Dispose(); else _leases.Add(id, value);
                });
                if (_disposed) yield break;
                if (lease.Bundle == null) throw new InvalidOperationException("Frontend bundle failed: " + row.path);
            }
        }

        public Sprite Sprite { get; private set; }
        public IEnumerator LoadSprite(string key)
        {
            yield return Prepare(key);
            if (_disposed) yield break;
            Sprite = _leases[_asset.bundle].Bundle.LoadAsset<Sprite>(_asset.internalId);
            if (Sprite == null) throw new InvalidOperationException("Frontend sprite missing: " + key);
        }

        public IEnumerator Load(string key, Transform parent)
        {
            yield return Prepare(key);
            if (_disposed) yield break;
            var prefab = _leases[_asset.bundle].Bundle.LoadAsset<GameObject>(_asset.internalId);
            if (prefab == null) throw new InvalidOperationException("Frontend prefab missing: " + key);
            Instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            Instance.name = prefab.name;
            var sourceFonts = _leases.Values.SelectMany(l => l.Bundle.LoadAllAssets<Font>()).ToArray();
            foreach (var text in Instance.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.spriteAsset != null) text.spriteAsset = AdaptSpriteAsset(text.spriteAsset);
                if (text.font == null) throw new InvalidOperationException("Frontend TMP font reference missing: " + text.name);
                var source = sourceFonts.FirstOrDefault(f => text.font.name.StartsWith(f.name, StringComparison.Ordinal));
                if (source == null) throw new InvalidOperationException("Frontend source Font missing: " + text.font.name);
                if (!_fonts.TryGetValue(source.name, out var font))
                {
                    // Same source-Font regeneration as the result page: APK TMP atlases do not populate reliably in Editor.
                    font = TMP_FontAsset.CreateFontAsset(source);
                    if (font == null) throw new InvalidOperationException("Cannot create frontend TMP font: " + source.name);
                    _fonts.Add(source.name, font);
                }
                var original = text.fontSharedMaterial;
                if (_fontShader == null || !_fontShader.isSupported) throw new InvalidOperationException("TMP SDF shader unavailable");
                // The original title material uses the full SDF shader, whose outline is not a Mobile keyword variant.
                var material = new Material(font.material) { shader = _fontShader };
                foreach (var property in new[] { "_FaceColor", "_OutlineColor", "_UnderlayColor" })
                    if (original != null && original.HasProperty(property)) material.SetColor(property, original.GetColor(property));
                foreach (var property in new[] { "_OutlineWidth", "_OutlineSoftness", "_FaceDilate", "_UnderlayOffsetX", "_UnderlayOffsetY", "_UnderlayDilate", "_UnderlaySoftness" })
                    if (original != null && original.HasProperty(property)) material.SetFloat(property, original.GetFloat(property));
                _fontMaterials.Add(material);
                text.font = font;
                text.fontSharedMaterial = material;
            }
            foreach (var mesh in Instance.GetComponentsInChildren<TMP_SubMeshUI>(true))
                if (mesh.spriteAsset != null)
                {
                    mesh.spriteAsset = AdaptSpriteAsset(mesh.spriteAsset);
                    mesh.sharedMaterial = mesh.spriteAsset.material;
                }
            foreach (var renderer in Instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(AdaptMaterial).ToArray();
            foreach (var graphic in Instance.GetComponentsInChildren<Graphic>(true))
                graphic.material = AdaptMaterial(graphic.material);
            if (Instance.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c == null))
                throw new InvalidOperationException("Frontend prefab contains missing scripts: " + key);
        }

        private TMP_SpriteAsset AdaptSpriteAsset(TMP_SpriteAsset source)
        {
            if (_spriteAssets.TryGetValue(source, out var cached)) return cached;
            if (_spriteShader == null || !_spriteShader.isSupported) throw new InvalidOperationException("TMP sprite shader unavailable");
            var clone = UnityEngine.Object.Instantiate(source);
            clone.material = new Material(source.material) { shader = _spriteShader };
            _spriteAssets.Add(source, clone);
            return clone;
        }

        private Material AdaptMaterial(Material source)
        {
            if (source == null || source.shader == null) return source;
            string path;
            switch (source.shader.name)
            {
                case "UI/Additive": path = "Shader/UI_Additive_0"; break;
                case "UIParticle/AlphaBlend": path = "Shader/UIParticle_AlphaBlend"; break;
                case "UIParticle/AlphaBlendBlack": path = "Shader/UIParticle_AlphaBlendBlack"; break;
                case "UI/MaskAdditive": path = "Shader/UI_MaskAdditive"; break;
                default: return source;
            }
            if (_materials.TryGetValue(source, out var existing)) return existing;
            var shader = Resources.Load<Shader>(path);
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Frontend shader unavailable: " + path);
            var material = new Material(source) { shader = shader };
            _materials.Add(source, material);
            return material;
        }

        public Sprite ArraySprite(string type, string field, int ordinal)
        {
            var row = _index.sprites.Single(s => s.type == type && s.field == field && s.ordinal == ordinal);
            foreach (var lease in _leases.Values)
                foreach (var atlas in lease.Bundle.LoadAllAssets<UnityEngine.U2D.SpriteAtlas>())
                {
                    var sprite = atlas.GetSprite(row.name);
                    if (sprite == null) continue;
                    _atlasSprites.Add(sprite);
                    return sprite;
                }
            throw new InvalidOperationException("Original array Sprite missing: " + row.name);
        }

        public Transform[] Fields(string type, string field) => _index.bindings
            .Where(b => b.bundle == _asset.bundle && b.type == type && b.field == field).Select(Resolve).ToArray();

        public Transform Field(string type, string field)
        {
            var binding = _index.bindings.Single(b => b.bundle == _asset.bundle && b.type == type && b.field == field);
            return Resolve(binding);
        }

        private Transform Resolve(Binding binding)
        {
            string prefix = Instance.name + "/";
            if (binding.path == Instance.name) return Instance.transform;
            if (!binding.path.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidOperationException("Binding is outside the view: " + binding.path);
            var result = Instance.transform.Find(binding.path.Substring(prefix.Length));
            if (result == null) throw new InvalidOperationException("Source binding missing: " + binding.path);
            return result;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Sprite = null;
            foreach (var sprite in _atlasSprites) UnityEngine.Object.Destroy(sprite);
            _atlasSprites.Clear();
            if (Instance != null) { Instance.SetActive(false); UnityEngine.Object.Destroy(Instance); Instance = null; }
            foreach (var lease in _leases.Values.Reverse()) lease.Dispose();
            _leases.Clear();
            foreach (var material in _materials.Values) UnityEngine.Object.Destroy(material);
            _materials.Clear();
            foreach (var sprite in _spriteAssets.Values)
            {
                UnityEngine.Object.Destroy(sprite.material);
                UnityEngine.Object.Destroy(sprite);
            }
            _spriteAssets.Clear();
            foreach (var material in _fontMaterials) UnityEngine.Object.Destroy(material);
            _fontMaterials.Clear();
            foreach (var font in _fonts.Values)
            {
                foreach (var atlas in font.atlasTextures) if (atlas != null) UnityEngine.Object.Destroy(atlas);
                UnityEngine.Object.Destroy(font.material);
                UnityEngine.Object.Destroy(font);
            }
            _fonts.Clear();
        }
    }
}
