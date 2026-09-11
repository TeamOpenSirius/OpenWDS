using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OpenWDS.Editor
{
    // Run tools/recover_stage_success_curves.py first. No AssetRipper export is used.
    public static class RecoverStageSuccess
    {
        private const string Destination = "Assets/OpenWDS/RecoveredStageSuccess";
        private const string PrefabPath = "Assets/Resources/Prefabs/OrdinarySoloGameResult.prefab";
        [Serializable] private sealed class Manifest { public string name; public float duration; public Curve[] curves; }
        [Serializable] private sealed class Curve { public string path, kind, property; public Key[] keys; }
        [Serializable] private sealed class Key { public float time, value, inSlope, outSlope; public bool step; }
        private static string ProjectFile(string name) => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../", name));
        private static readonly Dictionary<int, Sprite> Sprites = new Dictionary<int, Sprite>();
        private static string[] _pngs;

        public static void Run()
        {
            Directory.CreateDirectory(Destination);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ProjectFile("reverse/reports/stage-success-curves.json")));
            var bundles = Directory.GetFiles(ProjectFile("reverse/extracted/game-result-bundles"), "*.bundle")
                .Select(AssetBundle.LoadFromFile).ToArray();
            GameObject instance = null, target = null;
            try
            {
                var bundle = bundles.Single(b => b != null && b.GetAllAssetNames()
                    .Any(n => n.EndsWith("/gameresultview.prefab", StringComparison.OrdinalIgnoreCase)));
                var original = bundle.LoadAsset<GameObject>(bundle.GetAllAssetNames()
                    .Single(n => n.EndsWith("/gameresultview.prefab", StringComparison.OrdinalIgnoreCase)));
                var source = original.transform.Find("SucceseTextImagePosition");
                if (source == null) throw new InvalidOperationException("Original StageSuccess root absent.");
                instance = Object.Instantiate(source.gameObject);
                instance.name = source.name;
                var animator = instance.GetComponentInChildren<Animator>(true);
                var clip = new AnimationClip { name = manifest.name, frameRate = 60 };
                foreach (var curve in manifest.curves)
                {
                    var type = BindingType(curve.kind);
                    var keys = curve.keys.Select(k => new Keyframe(k.time, k.value, k.inSlope,
                        k.step ? float.PositiveInfinity : k.outSlope)).ToArray();
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(curve.path, type, curve.property), new AnimationCurve(keys));
                }
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.stopTime = manifest.duration;
                settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                ValidateCurves(source.GetComponentInChildren<Animator>(true), clip, manifest);
                var clipPath = Destination + "/StageSuccess.anim";
                SaveAsset(clip, clipPath);
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                var controllerPath = Destination + "/StageSuccess.controller";
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine = controller.layers[0].stateMachine;
                var state = machine.states.Length == 0 ? machine.AddState(manifest.name) : machine.states.Single().state;
                state.motion = clip;
                state.speed = 1;
                state.writeDefaultValues = true;
                machine.defaultState = state;
                animator.runtimeAnimatorController = controller;
                _pngs = Directory.GetFiles(ProjectFile("reverse/assets/offline/groups/game-result/exported/assetstudio"), "*.png", SearchOption.AllDirectories);
                Sprites.Clear();
                foreach (var image in instance.GetComponentsInChildren<Image>(true))
                {
                    image.sprite = RecoverSprite(image.sprite);
                    if (image.material.name == "UI_Add")
                        image.material = RecoverMaterial(image.material);
                    else if (image.material.shader.name == "UI/Default") image.material = null;
                    else throw new InvalidOperationException("Unknown image material " + image.material.name);
                }
                foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var sheet = ps.textureSheetAnimation;
                    for (var i = 0; i < sheet.spriteCount; ++i) sheet.SetSprite(i, RecoverSprite(sheet.GetSprite(i)));
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(RecoverMaterial).ToArray();
                }
                // Every retained reference must be a project asset, builtin, or inside this subtree.
                foreach (var component in instance.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) throw new InvalidOperationException("Missing source component");
                    var serialized = new SerializedObject(component);
                    var scriptProperty = serialized.FindProperty("m_Script");
                    if (component is MonoBehaviour && scriptProperty != null)
                    {
                        // Bundle MonoScript references have no project GUID.
                        // Bind the exact loaded type to its source MonoScript.
                        var script = MonoImporter.GetAllRuntimeMonoScripts().Single(m =>
                            m.GetClass() == component.GetType() &&
                            !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m))));
                        scriptProperty.objectReferenceValue = script;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var reference = property.objectReferenceValue;
                        if (reference == null || AssetDatabase.Contains(reference)) continue;
                        if (reference is Component c && c.transform.IsChildOf(instance.transform)) continue;
                        if (reference is GameObject g && g.transform.IsChildOf(instance.transform)) continue;
                        throw new InvalidOperationException("Unpersisted reference " + component.name + "/" + property.propertyPath + ": " + reference.name + " " + reference.GetType());
                    }
                }
                target = PrefabUtility.LoadPrefabContents(PrefabPath);
                var old = target.transform.Find(source.name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                instance.transform.SetParent(target.transform, false);
                instance.transform.SetSiblingIndex(source.GetSiblingIndex());
                PrefabUtility.SaveAsPrefabAsset(target, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("OPENWDS_STAGE_SUCCESS_RECOVERED curves=" + manifest.curves.Length + " sprites=" + Sprites.Count + " duration=" + clip.length);
            }
            finally
            {
                if (target != null) PrefabUtility.UnloadPrefabContents(target);
                else if (instance != null) Object.DestroyImmediate(instance);
                foreach (var b in bundles) if (b != null) b.Unload(true);
            }
        }

        private static void ValidateCurves(Animator source, AnimationClip recovered, Manifest manifest)
        {
            var original = Object.Instantiate(source.gameObject);
            var converted = Object.Instantiate(source.gameObject);
            try
            {
                var retail = source.runtimeAnimatorController.animationClips.Single();
                foreach (var time in Enumerable.Range(0, 113).Select(i => i / 60f))
                {
                    retail.SampleAnimation(original, time);
                    recovered.SampleAnimation(converted, time);
                    foreach (var curve in manifest.curves)
                    {
                        var a = original.transform.Find(curve.path);
                        var b = converted.transform.Find(curve.path);
                        float expected, actual;
                        if (curve.property.StartsWith("localEulerAnglesRaw."))
                        {
                            var axis = curve.property.EndsWith("x") ? 0 : curve.property.EndsWith("y") ? 1 : 2;
                            expected = a.localEulerAngles[axis]; actual = b.localEulerAngles[axis];
                            actual = expected + Mathf.DeltaAngle(expected, actual);
                        }
                        else
                        {
                            var type = BindingType(curve.kind);
                            var ao = type == typeof(GameObject) ? (Object)a.gameObject : a.GetComponent(type);
                            var bo = type == typeof(GameObject) ? (Object)b.gameObject : b.GetComponent(type);
                            var ap = new SerializedObject(ao).FindProperty(curve.property);
                            var bp = new SerializedObject(bo).FindProperty(curve.property);
                            if (ap == null || bp == null) throw new InvalidOperationException("Unresolved property " + curve.property);
                            expected = ap.propertyType == SerializedPropertyType.Boolean ? (ap.boolValue ? 1f : 0f) : ap.floatValue;
                            actual = bp.propertyType == SerializedPropertyType.Boolean ? (bp.boolValue ? 1f : 0f) : bp.floatValue;
                        }
                        if (Mathf.Abs(expected - actual) > 0.02f)
                            throw new InvalidOperationException($"Stage curve mismatch {time}: {curve.path}/{curve.property} retail={expected} recovered={actual}");
                    }
                }
                Debug.Log("OPENWDS_STAGE_CURVE_PARITY samples=113 channels=23 passed=true");
            }
            finally { Object.DestroyImmediate(original); Object.DestroyImmediate(converted); }
        }

        private static Type BindingType(string kind)
        {
            switch (kind)
            {
                case "Transform": return typeof(Transform);
                case "RectTransform": return typeof(RectTransform);
                case "GameObject": return typeof(GameObject);
                case "CanvasGroup": return typeof(CanvasGroup);
                case "Image": return typeof(Image);
                default: throw new InvalidOperationException("Unknown binding type " + kind);
            }
        }
        private static void SaveAsset(Object value, string path)
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing == null) AssetDatabase.CreateAsset(value, path);
            else { EditorUtility.CopySerialized(value, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(value); }
        }
        private static Sprite RecoverSprite(Sprite source)
        {
            if (source == null) return null;
            if (Sprites.TryGetValue(source.GetInstanceID(), out var result)) return result;
            var png = _pngs.Single(p => Path.GetFileNameWithoutExtension(p) == source.name);
            var path = Destination + "/" + source.name + ".png";
            File.Copy(png, path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = source.pixelsPerUnit;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height);
            settings.spriteBorder = source.border;
            importer.SetTextureSettings(settings);
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            result = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (result == null || result.rect.size != source.rect.size)
                throw new InvalidOperationException("Sprite dimensions differ from retail: " + source.name);
            Sprites.Add(source.GetInstanceID(), result);
            return result;
        }
        private static Material RecoverMaterial(Material source)
        {
            string path;
            switch (source.shader.name)
            {
                case "UI/Additive": path = "Assets/Resources/Material/Game/UI/UI_Add__2068d44a.mat"; break;
                case "UIParticle/AlphaBlendBlack": path = "Assets/Resources/Material/Game/UI/UI_AlphaBlack.mat"; break;
                default: throw new InvalidOperationException("Unknown source shader " + source.shader.name);
            }
            var template = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (template == null || source.mainTexture != null) throw new InvalidOperationException("Missing shader recovery or unresolved texture");
            var material = new Material(template);
            material.CopyPropertiesFromMaterial(source);
            material.name = source.name;
            var output = Destination + "/" + source.name + ".mat";
            SaveAsset(material, output);
            return AssetDatabase.LoadAssetAtPath<Material>(output);
        }
    }
}
