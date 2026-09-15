using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sirius;
using Sirius.CharacterModel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OpenWDS.Editor
{
    [InitializeOnLoad]
    public static class RunCharacterProbePlayMode
    {
        private const string Key = "OpenWDS.CharacterProbe";
        [Serializable] private class FileRow { public string path; public long bytes; }
        [Serializable] private class RootRow { public string internalId; }
        [Serializable] private class Manifest { public FileRow[] files; public RootRow[] rootLocations; }
        [Serializable] private class ClothRow { public string path, result; }
        [Serializable] private class ShaderRow { public string material, shader; public bool supported; }
        [Serializable] private class Sample { public float time, normalizedTime, mouthWeight; public int stateHash, motion, magentaPixels, foregroundPixels; public string screenshot; }
        [Serializable] private class Report
        {
            public bool passed, animationMoved, renderingPassed;
            public string failure, prefab;
            public string scope = "Original sample prefab and recovered no-keyword Forward rendering probe; not result integration or original screenshot parity";
            public int missingScripts, renderers, bones, nullBones;
            public List<ClothRow> cloth = new List<ClothRow>();
            public List<ShaderRow> shaders = new List<ShaderRow>();
            public List<Sample> samples = new List<Sample>();
            public List<string> errors = new List<string>();
        }
        private static readonly List<AssetBundle> Bundles = new List<AssetBundle>();
        private static Report _report;
        private static GameObject _holder;
        private static CharacterObjectPerformanceTrigger _trigger;
        private static Transform[] _bones;
        private static Quaternion[] _rotations;
        private static float _start;
        private static int _phase;
        private static Camera _camera;
        private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        static RunCharacterProbePlayMode()
        {
            EditorApplication.update += Poll;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (SessionState.GetInt(Key, 0) == 1 &&
                    (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                {
                    if (_report != null) _report.errors.Add(message);
                    SessionState.SetString(Key + ".error", message);
                }
            };
        }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a dedicated batch Editor.");
            SessionState.SetInt(Key, 1);
            SessionState.SetString(Key + ".error", "");
            SessionState.SetString(Key + ".start", DateTime.UtcNow.Ticks.ToString());
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        private static void Poll()
        {
            var status = SessionState.GetInt(Key, 0);
            if (status == 0) return;
            if (status == 2)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetInt(Key, 0);
                    EditorApplication.Exit(SessionState.GetBool(Key + ".passed", false) ? 0 : 1);
                }
                return;
            }
            try
            {
                var error = SessionState.GetString(Key + ".error", "");
                if (error.Length != 0) throw new InvalidOperationException(error);
                if ((DateTime.UtcNow.Ticks - long.Parse(SessionState.GetString(Key + ".start", "0"))) /
                    (double)TimeSpan.TicksPerSecond > 180) throw new TimeoutException("Character probe timeout");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                if (_report == null) { Begin(); return; }
                var elapsed = Time.time - _start;
                for (var i = 0; i < _bones.Length; i++)
                    if (Quaternion.Angle(_rotations[i], _bones[i].localRotation) > 0.01f) _report.animationMoved = true;
                if (elapsed >= _phase + 1)
                {
                    var animator = _trigger.Animator;
                    var state = animator.GetCurrentAnimatorStateInfo(0);
                    var screenshot = "character-probe-" + _phase + ".png";
                    var pixels = Capture(screenshot);
                    _report.samples.Add(new Sample { magentaPixels = pixels.x, foregroundPixels = pixels.y, time = elapsed, stateHash = state.fullPathHash, screenshot = screenshot,
                        normalizedTime = state.normalizedTime, motion = animator.GetInteger("MotionState"),
                        mouthWeight = animator.GetLayerWeight(animator.GetLayerIndex("Mouth Layer")) });
                    if (_phase == 0) _trigger.MouthMotionPlay("SmallMouthMotion");
                    if (_phase == 1) _trigger.MouthMotionStop();
                    _phase++;
                }
                if (_phase < 4) return;
                Require(_report.animationMoved, "No original bone rotation changed in the player loop.");
                Require(_report.samples[1].mouthWeight == 1 && _report.samples[2].mouthWeight == 0,
                    "Mouth layer did not start and stop.");
                _report.renderingPassed = _report.samples.All(sample => sample.magentaPixels == 0 && sample.foregroundPixels > 1000);
                Require(_report.renderingPassed, "Missing geometry or magenta error pixels in captured character.");
                Finish(true, "");
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }
        private static void Begin()
        {
            _report = new Report();
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(Root,
                "reverse/reports/online-downloads/1.96.0/result-character-probe.json")));
            Require(manifest.files.Length == 45 && manifest.rootLocations.Length == 1, "Unexpected probe manifest");
            foreach (var file in manifest.files)
            {
                var path = Path.Combine(Root, file.path);
                Require(new FileInfo(path).Length == file.bytes, "Bundle size: " + file.path);
                var bundle = AssetBundle.LoadFromFile(path);
                Require(bundle != null, "Bundle load: " + file.path);
                Bundles.Add(bundle);
            }
            _report.prefab = manifest.rootLocations[0].internalId;
            GameObject prefab = null;
            foreach (var bundle in Bundles)
                if (bundle.GetAllAssetNames().Any(name => string.Equals(name, _report.prefab, StringComparison.OrdinalIgnoreCase)))
                    prefab = bundle.LoadAsset<GameObject>(_report.prefab);
            Require(prefab != null, "Root prefab not found at original catalog address");
            _holder = new GameObject("CharacterProbeOnly");
            _holder.SetActive(false);
            var instance = UnityEngine.Object.Instantiate(prefab, _holder.transform, false);
            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) { _report.missingScripts++; continue; }
                if (behaviour.GetType().FullName == "MagicaCloth.MagicaBoneCloth")
                {
                    var result = behaviour.GetType().GetMethod("VerifyData").Invoke(behaviour, null).ToString();
                    _report.cloth.Add(new ClothRow { path = AnimationUtility.CalculateTransformPath(behaviour.transform,
                        instance.transform), result = result });
                }
            }
            var recoveredShader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/OpenWDS/Editor/CharacterProbeShader/ScarabOriginalForward.shader");
            Require(recoveredShader != null && !ShaderUtil.ShaderHasError(recoveredShader), "Recovered probe shader failed compilation: " + (recoveredShader == null ? "asset missing" : string.Join("; ", ShaderUtil.GetShaderMessages(recoveredShader).Select(message => message.message + " at " + message.file + ":" + message.line))));
            foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                _report.renderers++;
                Require(renderer.sharedMesh != null && renderer.sharedMesh.vertexCount > 0, "Empty mesh " + renderer.name);
                _report.bones += renderer.bones.Length;
                _report.nullBones += renderer.bones.Count(bone => bone == null);
                foreach (var material in renderer.sharedMaterials)
                {
                    Require(material != null && material.shader != null, "Missing material/shader " + renderer.name);
                    Require(material.shader.name == "Scarab/Chara_Base_MV_Scarab" || material.shader == recoveredShader, "Unexpected source shader");
                    Require(material.shaderKeywords.Length == 0, "Unrecovered character shader keywords");
                    // This edits bundle materials only in this dedicated probe process.
                    material.shader = recoveredShader;
                    _report.shaders.Add(new ShaderRow { material = material.name, shader = material.shader.name,
                        supported = material.shader.isSupported });
                }
            }
            Require(_report.missingScripts == 0 && _report.nullBones == 0 && _report.renderers > 0, "Incomplete prefab bindings");
            Require(_report.cloth.Count > 0 && _report.cloth.All(row => row.result == "None"), "BoneCloth VerifyData failed");
            _trigger = instance.GetComponentInChildren<CharacterObjectPerformanceTrigger>(true);
            Require(_trigger != null && _trigger.Animator != null, "Original character trigger/Animator missing");
            _holder.SetActive(true);
            _trigger.Initialize(CharacterObjectPerformanceConst.AnimatorTypes.Home);
            _trigger.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _trigger.ResetTrigger();
            _trigger.SetMotionState(CharacterObjectPerformanceConst.MotionState.Motion01);
            _trigger.PlayMotion();
            _bones = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(renderer => renderer.bones).Distinct().ToArray();
            _rotations = _bones.Select(bone => bone.localRotation).ToArray();
            // Probe framing only; this is not the original result stage camera.
            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            _camera = new GameObject("CharacterProbeCamera").AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.18f, 0.2f, 0.23f);
            _camera.orthographic = true;
            _camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.15f;
            _camera.transform.position = bounds.center + new Vector3(0, 0, 5);
            _camera.transform.LookAt(bounds.center);
            _start = Time.time;
        }
        private static Vector2Int Capture(string filename)
        {
            var target = new RenderTexture(768, 768, 24);
            var texture = new Texture2D(768, 768, TextureFormat.RGB24, false);
            var oldActive = RenderTexture.active;
            try
            {
                _camera.targetTexture = target;
                _camera.aspect = 1;
                _camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(Root, "reverse/reports", filename), texture.EncodeToPNG());
                var pixels = texture.GetPixels32();
                var background = pixels[0];
                var magenta = 0;
                var foreground = 0;
                foreach (var pixel in pixels)
                {
                    if (pixel.r > 245 && pixel.g < 10 && pixel.b > 245) magenta++;
                    if (Math.Abs(pixel.r-background.r)+Math.Abs(pixel.g-background.g)+Math.Abs(pixel.b-background.b) > 15) foreground++;
                }
                return new Vector2Int(magenta, foreground);
            }
            finally
            {
                _camera.targetTexture = null;
                RenderTexture.active = oldActive;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
        private static void Finish(bool passed, string failure)
        {
            if (_report == null) _report = new Report();
            _report.passed = passed;
            _report.failure = failure;
            File.WriteAllText(Path.Combine(Root, "reverse/reports/character-prefab-playmode-validation.json"),
                JsonUtility.ToJson(_report, true) + "\n");
            Debug.Log("OPENWDS_CHARACTER_PREFAB passed=" + passed + " renderers=" + _report.renderers + " cloth=" + _report.cloth.Count);
            SessionState.SetBool(Key + ".passed", passed);
            SessionState.SetInt(Key, 2);
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
