using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sirius.Animations;
using Sirius.Game;
using Sirius.LiveEngine;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OpenWDS.Editor
{
    public static class ValidateStarActReconstruction
    {
        private const string PrefabPath = "Assets/Resources/Prefabs/Game/StarActCutIn.prefab";
        private const string ShaderPath = "Assets/Resources/Shader/Game/StarActCutinOutline.shader";
        private const string NoisePath = "Assets/Resources/Texture2D/Game/Noise2.png";
        [Serializable] private class SourceSprite { public string name, projectPath; public float pixelsPerUnit; public Vector2 pivot; public Vector4 border; }
        [Serializable] private class Source { public SourceSprite[] sprites; }
        [Serializable] private class Report { public bool passed, clipParity, bindings; public int transforms, sprites, masks, particles; public string failure; }
        private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        public static void RecoverAndValidate()
        {
            try
            {
                var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(Root, "reverse/reports/staract-prefab-recovery.json")));
                foreach (var sprite in source.sprites.Where(s => s.projectPath.EndsWith(".png")))
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(sprite.projectPath);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.textureShape = TextureImporterShape.Texture2D;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = sprite.pixelsPerUnit;
                    importer.spriteBorder = sprite.border;
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = sprite.pivot;
                    importer.SetTextureSettings(settings);
                    importer.SaveAndReimport();
                }
                var noiseImporter = (TextureImporter)AssetImporter.GetAtPath(NoisePath);
                noiseImporter.textureType = TextureImporterType.Default;
                noiseImporter.textureShape = TextureImporterShape.Texture2D;
                noiseImporter.mipmapEnabled = false;
                noiseImporter.sRGBTexture = true;
                noiseImporter.filterMode = FilterMode.Bilinear;
                noiseImporter.wrapMode = TextureWrapMode.Repeat;
                noiseImporter.anisoLevel = 1;
                noiseImporter.textureCompression = TextureImporterCompression.Uncompressed;
                noiseImporter.SaveAndReimport();
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
                return;
            }
            Run();
        }
        public static void Run()
        {
            var report = new Report();
            var bundles = new List<AssetBundle>();
            GameObject instance = null, original = null, recovered = null, parent = null;
            Material sharedOutline = null;
            string outlineSnapshot = null;
            try
            {
                var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(Root, "reverse/reports/staract-prefab-recovery.json")));
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
                Require(shader != null && !ShaderUtil.ShaderHasError(shader), "Outline shader: " +
                    (shader == null ? "missing" : string.Join("; ", ShaderUtil.GetShaderMessages(shader).Select(x => x.message + " :" + x.line))));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                Require(prefab != null, "Native subtree failed import");
                instance = Object.Instantiate(prefab);
                var components = instance.GetComponentsInChildren<Component>(true);
                Require(components.All(x => x != null), "Missing native component or script");
                report.transforms = instance.GetComponentsInChildren<Transform>(true).Length;
                report.sprites = instance.GetComponentsInChildren<SpriteRenderer>(true).Length;
                report.masks = instance.GetComponentsInChildren<SpriteMask>(true).Length;
                report.particles = instance.GetComponentsInChildren<ParticleSystem>(true).Length;
                Require(report.transforms == 35 && report.sprites == 12 && report.masks == 6 && report.particles == 2, "Original component counts");
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    foreach (var mat in renderer.sharedMaterials)
                        Require(mat != null && mat.shader != null && mat.shader.isSupported, "Renderer material " + renderer.name + " material=" + (mat == null ? "NULL" : mat.name) + " shader=" + (mat == null || mat.shader == null ? "NULL" : mat.shader.name + " supported=" + mat.shader.isSupported + " " + string.Join("; ", ShaderUtil.GetShaderMessages(mat.shader).Select(x => x.message + ":" + x.line))));
                foreach (var sprite in source.sprites)
                {
                    var loaded = AssetDatabase.LoadAssetAtPath<Sprite>(sprite.projectPath);
                    Require(loaded != null && loaded.texture != null && loaded.vertices.Length > 0 && loaded.triangles.Length > 0,
                        "Invalid source sprite " + sprite.name + " object=" + loaded + " texture=" + (loaded == null ? null : loaded.texture) +
                        " vertices=" + (loaded == null ? -1 : loaded.vertices.Length) + " triangles=" + (loaded == null ? -1 : loaded.triangles.Length));
                    Require(Mathf.Abs(loaded.pixelsPerUnit-sprite.pixelsPerUnit) < 0.001f && loaded.border == sprite.border,
                        "Sprite dimensions/border " + sprite.name);
                }
                var animator = instance.GetComponentInChildren<Animator>(true);
                Require(animator != null && animator.runtimeAnimatorController != null, "Original animator binding");
                var bundleDirectory = Path.Combine(Root, "reverse/assets/offline/raw/bundles/split_UnityDataAssetPack/assets/aa/Android/feature_game_animation_assets_assets/animation/game");
                foreach (var file in new[] { "sencecutin_wait_anim.anim.bundle", "sencecutin_anim.anim.bundle", "sencecutin_animator.controller.bundle" })
                {
                    var b = AssetBundle.LoadFromFile(Path.Combine(bundleDirectory, file));
                    Require(b != null, file); bundles.Add(b);
                }
                var controller = bundles.SelectMany(b => b.LoadAllAssets<RuntimeAnimatorController>()).Single();
                original = Object.Instantiate(animator.gameObject);
                recovered = Object.Instantiate(animator.gameObject);
                foreach (var clip in controller.animationClips)
                {
                    var converted = animator.runtimeAnimatorController.animationClips.Single(x => x.name == clip.name);
                    Require(Mathf.Abs(clip.length-converted.length) < 0.001f, "Clip duration " + clip.name);
                    Require(AnimationUtility.GetAnimationEvents(clip).Select(x => x.functionName).SequenceEqual(
                        AnimationUtility.GetAnimationEvents(converted).Select(x => x.functionName)), "Original exit event");
                    for (var sample = 0; sample <= 120; sample++)
                    {
                        var time = sample/60f;
                        clip.SampleAnimation(original, time);
                        converted.SampleAnimation(recovered, time);
                        CompareHierarchy(original.transform, recovered.transform, clip.name + " @" + time);
                    }
                }
                report.clipParity = true;
                var player = instance.GetComponent<StarActCutInPlayer>();
                Require(player != null, "Player field binding");
                var serializedPlayer = new SerializedObject(player);
                Require(serializedPlayer.FindProperty("_animator").objectReferenceValue == animator, "Player animator local reference");
                sharedOutline = serializedPlayer.FindProperty("_outlineMatarial").objectReferenceValue as Material;
                Require(sharedOutline != null, "Player material reference");
                outlineSnapshot = EditorJsonUtility.ToJson(sharedOutline);
                parent = new GameObject("staract-position-fixture");
                parent.transform.position = new Vector3(1, 2, 3);
                instance.transform.SetParent(parent.transform, false);
                var camera = parent.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                var photo = AssetDatabase.LoadAssetAtPath<Sprite>(source.sprites[0].projectPath);
                player.SetMainCamera(camera);
                player.Initialize(photo);
                Require(instance.transform.localPosition.x == 10000 && instance.transform.localPosition.y == 10000,
                    "Standby must use local XY");
                var material = new SerializedObject(player).FindProperty("_outlineMatarial").objectReferenceValue as Material;
                Require(material != null && material.GetFloat("_ColorCount") == 0 && material.GetColor("_Color1") == Color.white, "Default outline");
                var position = instance.transform.position;
                var viewport = new SerializedObject(player).FindProperty("_viewportPosition").vector3Value;
                ((IStarActCutInPlayer)player).Animate(new[] { SenseLightTypes.Special, SenseLightTypes.Support, SenseLightTypes.Special });
                var expected = camera.ViewportToWorldPoint(viewport);
                Require(Mathf.Abs(instance.transform.position.x-expected.x)<0.001f &&
                    Mathf.Abs(instance.transform.position.y-expected.y)<0.001f && instance.transform.position.z==position.z,
                    "Play must use world XY and preserve Z");
                var colors = new SerializedObject(player).FindProperty("_outlineColors");
                Require(material.GetFloat("_ColorCount")==2 && material.GetColor("_Color1")==colors.GetArrayElementAtIndex(4).colorValue &&
                    material.GetColor("_Color2")==colors.GetArrayElementAtIndex(1).colorValue, "Original distinct color order");
                animator.GetComponent<AnimationExitActionTrigger>().OnExit();
                Require(instance.transform.localPosition.x==10000 && instance.transform.localPosition.y==10000, "Original OnExit callback");
                report.bindings = true;
                report.passed = true;
            }
            catch (Exception error) { report.failure = error.ToString(); }
            finally
            {
                if (sharedOutline && outlineSnapshot != null) EditorJsonUtility.FromJsonOverwrite(outlineSnapshot, sharedOutline);
                if (original) Object.DestroyImmediate(original);
                if (recovered) Object.DestroyImmediate(recovered);
                if (instance) Object.DestroyImmediate(instance);
                if (parent) Object.DestroyImmediate(parent);
                foreach (var bundle in bundles) bundle.Unload(true);
                File.WriteAllText(Path.Combine(Root,"reverse/reports/staract-reconstruction-validation.json"),JsonUtility.ToJson(report,true));
            }
            Debug.Log("OPENWDS_STARACT_RECONSTRUCTION passed="+report.passed+" clipParity="+report.clipParity+" failure="+report.failure);
            EditorApplication.Exit(report.passed?0:1);
        }
        private static void CompareHierarchy(Transform a, Transform b, string label)
        {
            Require(a.name==b.name && a.childCount==b.childCount,"Hierarchy "+label);
            Require(Vector3.Distance(a.localPosition,b.localPosition)<0.02f && Quaternion.Angle(a.localRotation,b.localRotation)<0.02f &&
                Vector3.Distance(a.localScale,b.localScale)<0.02f && a.gameObject.activeSelf==b.gameObject.activeSelf,"Transform/active "+a.name+" "+label);
            var ar=a.GetComponent<SpriteRenderer>();var br=b.GetComponent<SpriteRenderer>();
            if(ar != null) Require(Vector4.Distance(ar.color,br.color)<0.002f,"Sprite color "+a.name+" "+label);
            for(var i=0;i<a.childCount;i++) CompareHierarchy(a.GetChild(i),b.GetChild(i),label);
        }
        private static void Require(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
    }
}
