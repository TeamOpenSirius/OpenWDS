using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sirius.Game;
using Sirius.LiveEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OpenWDS.Editor
{
    // An isolated rendering check using the original scene camera and a catalog
    // sample. This does not select a production actor or claim screenshot parity.
    [InitializeOnLoad]
    public static class RunStarActProbePlayMode
    {
        private const string Key = "OpenWDS.StarActProbe";
        [Serializable] private class FileRow { public string path; public long bytes; }
        [Serializable] private class RootRow { public string internalId; }
        [Serializable] private class Manifest { public FileRow[] files; public RootRow[] rootLocations; }
        [Serializable] private class TransformRow { public Vector3 m_LocalPosition, m_LocalScale; public Quaternion m_LocalRotation; }
        [Serializable] private class Projection { public float fieldOfView, near, far, orthographicSize; public bool orthographic; public int cullingMask; }
        [Serializable] private class Source { public TransformRow[] cameraTransforms; public Projection cameraProjection; }
        [Serializable] private class Sample { public float time; public string screenshot; public int foregroundPixels, magentaPixels, particles; public Vector3 position; }
        [Serializable] private class Report
        {
            public bool passed, exited;
            public string failure, imageAddress;
            public string scope = "Original StarAct subtree, controller, outline GPU passes and camera; catalog sample only, no production selection or original screenshot parity";
            public List<Sample> samples = new List<Sample>();
        }
        private static readonly List<AssetBundle> Bundles = new List<AssetBundle>();
        private static readonly float[] Times = { 0.2f, 0.6f, 1.0f, 1.6f, 2.3f };
        private static Report _report;
        private static Camera _camera;
        private static StarActCutInPlayer _player;
        private static Material _outline;
        private static float _begin, _start;
        private static bool _triggered;
        private static Color32[] _background;
        private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        static RunStarActProbePlayMode()
        {
            EditorApplication.update += Poll;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (SessionState.GetInt(Key, 0) == 1 && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                    SessionState.SetString(Key + ".error", message);
            };
        }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a dedicated batch Editor");
            SessionState.SetInt(Key, 1);
            SessionState.SetString(Key + ".error", "");
            SessionState.SetString(Key + ".start", DateTime.UtcNow.Ticks.ToString());
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        private static void Poll()
        {
            var state = SessionState.GetInt(Key, 0);
            if (state == 0) return;
            if (state == 2)
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
                Require(error.Length == 0, error);
                Require((DateTime.UtcNow.Ticks-long.Parse(SessionState.GetString(Key + ".start", "0"))) / (double)TimeSpan.TicksPerSecond < 180, "Probe timeout");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                if (_report == null) { Begin(); return; }
                if (!_triggered)
                {
                    if (Time.time-_begin < 0.1f) return;
                    _background = Render();
                    ((IStarActCutInPlayer)_player).Animate(new[] { SenseLightTypes.Support, SenseLightTypes.Special });
                    _triggered = true; _start = Time.time;
                    return;
                }
                var elapsed = Time.time-_start;
                if (_report.samples.Count < Times.Length && elapsed >= Times[_report.samples.Count])
                {
                    var pixels = Render();
                    var sample = new Sample { time = elapsed, screenshot = "staract-probe-" + _report.samples.Count + ".png", position = _player.transform.localPosition,
                        particles = _player.GetComponentsInChildren<ParticleSystem>(true).Sum(p => p.particleCount) };
                    for (var i = 0; i < pixels.Length; i++)
                    {
                        var p = pixels[i]; var b = _background[i];
                        if (p.r > 245 && p.g < 10 && p.b > 245) sample.magentaPixels++;
                        if (Math.Abs(p.r-b.r)+Math.Abs(p.g-b.g)+Math.Abs(p.b-b.b) > 15) sample.foregroundPixels++;
                    }
                    var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                    texture.SetPixels32(pixels); texture.Apply();
                    File.WriteAllBytes(Path.Combine(Root, "reverse/reports", sample.screenshot), texture.EncodeToPNG());
                    Object.DestroyImmediate(texture);
                    _report.samples.Add(sample);
                }
                if (_report.samples.Count < Times.Length) return;
                _report.exited = _player.transform.localPosition.x == 10000 && _player.transform.localPosition.y == 10000;
                Require(_report.exited, "Original animation OnExit did not return to standby");
                Require(_report.samples.All(s => s.magentaPixels == 0), "Magenta shader error pixels");
                Require(_report.samples.Take(4).All(s => s.foregroundPixels > 1000), "Cut-in missing in active samples");
                Require(_report.samples.Take(4).Any(s => s.particles > 0), "Original particles never emitted");
                Require(_report.samples.Last().foregroundPixels == 0, "Cut-in remains visible after exit");
                Finish(true, "");
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }
        private static void Begin()
        {
            _report = new Report();
            Time.captureFramerate = 60;
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(Root, "reverse/reports/staract-prefab-recovery.json")));
            Transform parent = null;
            foreach (var row in source.cameraTransforms.Reverse())
            {
                var node = new GameObject("OriginalCameraTransform").transform;
                node.SetParent(parent, false); node.localPosition = row.m_LocalPosition;
                node.localRotation = row.m_LocalRotation; node.localScale = row.m_LocalScale;
                parent = node;
            }
            _camera = parent.gameObject.AddComponent<Camera>();
            var p = source.cameraProjection;
            _camera.fieldOfView = p.fieldOfView; _camera.nearClipPlane = p.near; _camera.farClipPlane = p.far;
            _camera.orthographic = p.orthographic; _camera.orthographicSize = p.orthographicSize; _camera.cullingMask = p.cullingMask;
            _camera.aspect = 1280f/720; _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.08f, 0.1f, 0.12f, 1); // Probe background only.
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(Root, "reverse/reports/online-downloads/1.96.0/staract-110010-probe.json")));
            var addresses = manifest.rootLocations.Select(r => r.internalId).Distinct().ToArray();
            Require(addresses.Length == 1, "Ambiguous catalog image address");
            _report.imageAddress = addresses[0];
            Sprite photo = null;
            foreach (var row in manifest.files)
            {
                var path = Path.Combine(Root, row.path);
                Require(new FileInfo(path).Length == row.bytes, "Bundle size mismatch");
                var bundle = AssetBundle.LoadFromFile(path); Require(bundle != null, "Image bundle missing"); Bundles.Add(bundle);
                if (bundle.GetAllAssetNames().Any(n => string.Equals(n, addresses[0], StringComparison.OrdinalIgnoreCase))) photo = bundle.LoadAsset<Sprite>(addresses[0]);
            }
            Require(photo != null && photo.rect.size == new Vector2(880, 640), "Original image Sprite unavailable");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Game/StarActCutIn.prefab");
            _player = Object.Instantiate(prefab).GetComponent<StarActCutInPlayer>();
            var serialized = new SerializedObject(_player);
            var old = (Material)serialized.FindProperty("_outlineMatarial").objectReferenceValue;
            _outline = new Material(old);
            foreach (var renderer in _player.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m == old ? _outline : m).ToArray();
            serialized.FindProperty("_outlineMatarial").objectReferenceValue = _outline;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _player.SetMainCamera(_camera); _player.Initialize(photo);
            _begin = Time.time;
        }
        private static Color32[] Render()
        {
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                _camera.targetTexture = target; _camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply(); return texture.GetPixels32();
            }
            finally
            {
                _camera.targetTexture = null; RenderTexture.active = previous;
                Object.DestroyImmediate(texture); Object.DestroyImmediate(target);
            }
        }
        private static void Finish(bool passed, string failure)
        {
            if (_report == null) _report = new Report();
            _report.passed = passed; _report.failure = failure;
            File.WriteAllText(Path.Combine(Root, "reverse/reports/staract-playmode-validation.json"), JsonUtility.ToJson(_report, true));
            Debug.Log("OPENWDS_STARACT_PLAYMODE passed=" + passed + " exited=" + _report.exited + " failure=" + failure);
            SessionState.SetBool(Key + ".passed", passed); SessionState.SetInt(Key, 2);
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
