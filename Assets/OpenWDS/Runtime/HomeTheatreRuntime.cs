using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cinemachine;
using Sirius;
using Sirius.CharacterModel;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenWDS.Runtime
{
    /// <summary>Original additive Theatre environment and explicit offline character fixture.</summary>
    public sealed class HomeTheatreRuntime : MonoBehaviour
    {
        [Serializable] private sealed class BundleRow { public string key, path; public bool scene; }
        [Serializable] private sealed class Binding { public string field, path; }
        [Serializable] private sealed class Index { public BundleRow[] bundles; public Binding[] bindings; public string scene, prefab, character; }
        private readonly List<LocalAssetBundleLease> _leases = new List<LocalAssetBundleLease>();
        private Index _index;
        private Scene _scene;
        private Camera _camera;
        private CinemachineBrain _brain;
        private bool _released;
        public bool IsReady { get; private set; }
        public GameObject Environment { get; private set; }
        public CharacterObjectPerformanceTrigger Character { get; private set; }
        public int BundleCount => _leases.Count;
        public bool HasScene => _scene.IsValid() && _scene.isLoaded;
        public CinemachineVirtualCamera HomeCamera { get; private set; }

        public IEnumerator Prepare(Camera camera)
        {
            _camera = camera;
            byte[] bytes = null;
            yield return StreamingAssetsRuntime.ReadBytes("OpenWDS/HomeTheatre/index.json", b => bytes = b);
            _index = JsonUtility.FromJson<Index>(Encoding.UTF8.GetString(bytes));
            foreach (var row in _index.bundles)
            {
                LocalAssetBundleLease lease = null;
                yield return LocalAssetBundleLease.LoadStreaming(row.key, row.path, value =>
                {
                    lease = value;
                    if (_released) value.Dispose(); else _leases.Add(value);
                });
                if (_released) yield break;
                if (lease.Bundle == null) throw new InvalidOperationException("Theatre bundle failed: " + row.path);
            }
            var operation = SceneManager.LoadSceneAsync(_index.scene, LoadSceneMode.Additive);
            if (operation == null) throw new InvalidOperationException("Theatre scene load did not start");
            string scenePath = _index.scene;
            operation.completed += _ =>
            {
                if (this != null && !_released) return;
                var abandoned = SceneManager.GetSceneByPath(scenePath);
                if (abandoned.IsValid() && abandoned.isLoaded) SceneManager.UnloadSceneAsync(abandoned);
            };
            yield return operation;
            if (_released) yield break;
            _scene = SceneManager.GetSceneByPath(_index.scene);
            if (!HasScene) throw new InvalidOperationException("Original additive Theatre scene missing");
            // Scope belongs to the additive scene so direct navigation also destroys it before unloading assets.
            var owner = new GameObject("HomeTheatreScope");
            SceneManager.MoveGameObjectToScene(owner, _scene);
            transform.SetParent(owner.transform, false);
            var prefab = Asset<GameObject>(_index.prefab);
            AdaptMaterials(prefab);
            Environment = Instantiate(prefab, transform, false);
            Environment.name = prefab.name;
            foreach (var listener in Environment.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            HomeCamera = Field("_homeVirtualCamera").GetComponent<CinemachineVirtualCamera>();
            // Native Initialize 0x597C31C and cctor 0x597E13C: 1.26 m and (0,-180,0).
            var position = HomeCamera.transform.localPosition; position.y = 1.26f;
            HomeCamera.transform.localPosition = position;
            foreach (var field in new[] { "_homeVirtualCamera", "_memberVirtualCamera", "_liveVirtualCamera", "_liveMonitorVirtualCamera", "_storyVirtualCamera", "_storyMonitorVirtualCamera", "_shopVirtualCamera" })
                Field(field).gameObject.SetActive(field == "_homeVirtualCamera");
            HomeCamera.transform.localEulerAngles = new Vector3(0, -180, 0);
            var characterPrefab = Asset<GameObject>(_index.character);
            AdaptMaterials(characterPrefab);
            var actor = Instantiate(characterPrefab, Field("_mainParent"), false);
            Character = actor.GetComponent<CharacterObjectPerformanceTrigger>();
            if (Character == null) throw new InvalidOperationException("Theatre character trigger missing");
            Character.Initialize(CharacterObjectPerformanceConst.AnimatorTypes.Home);
            Character.SetDirLight(Field("_mainCharacterLight").GetComponent<Light>());
            Character.SetMotionState(CharacterObjectPerformanceConst.MotionState.Idle);
            Character.PlayMotion();
            // ARM64 0x597CC3C: y -= 1 - HeightSettings._bodyScale.
            if (actor.TryGetComponent<HeightSettings>(out var height))
                foreach (var field in new[] { "_homeVirtualCamera", "_liveVirtualCamera" })
                {
                    var target = Field(field); var p = target.localPosition; p.y -= 1f - height._bodyScale; target.localPosition = p;
                }
            Field("_homeBg").gameObject.SetActive(true);
            var poster = Field("_posterMeshRenderer").GetComponent<MeshRenderer>();
            if (poster.sharedMaterial == null) throw new InvalidOperationException("Original theatre poster material missing");
            _camera.orthographic = false;
            _camera.cullingMask = ~0;
            _camera.gameObject.tag = "MainCamera";
            _brain = _camera.gameObject.AddComponent<CinemachineBrain>();
            _brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0);
            var cameraData = _camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (cameraData == null) cameraData = _camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            cameraData.requiresDepthTexture = true;
            if (Environment.GetComponentsInChildren<MonoBehaviour>(true).Any(x => x == null) || actor.GetComponentsInChildren<MonoBehaviour>(true).Any(x => x == null))
                throw new InvalidOperationException("Theatre contains missing scripts");
            yield return null;
            IsReady = true;
            Debug.Log($"OPENWDS_HOME_THEATRE_READY bundles={BundleCount} renderers={Environment.GetComponentsInChildren<Renderer>(true).Length} character={_index.character}");
        }
        private T Asset<T>(string path) where T : UnityEngine.Object
        {
            foreach (var lease in _leases)
                if (!lease.Bundle.isStreamedSceneAssetBundle && lease.Bundle.GetAllAssetNames().Any(n => string.Equals(n, path, StringComparison.OrdinalIgnoreCase)))
                    return lease.Bundle.LoadAsset<T>(path);
            throw new InvalidOperationException("Theatre asset missing: " + path);
        }
        private Transform Field(string field)
        {
            var path = _index.bindings.Single(b => b.field == field).path;
            var result = Environment.transform.Find(path.Substring(path.IndexOf('/') + 1));
            if (result == null) throw new InvalidOperationException("Theatre binding missing: " + path);
            return result;
        }
        private static void AdaptMaterials(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
                if (renderer.sharedMaterials.Any(m => m == null) && (renderer.enabled || !(renderer is ParticleSystemRenderer)))
                    throw new InvalidOperationException("Theatre renderer material missing: " + renderer.name);
            // Seven original disabled particle grouping renderers have authored null slots.
            foreach (var material in renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct())
            {
                if (material == null || material.shader == null) throw new InvalidOperationException("Theatre material missing");
                string name = material.shader.name;
                if (name.StartsWith("OpenWDS/")) continue;
                string path = null;
                switch (name)
                {
                    case "Scarab/BGShaderDiffuse": path = "Shader/HomeTheatre/BGDiffuse"; break;
                    case "Scarab/BGShaderSkybox": path = "Shader/HomeTheatre/BGSkybox"; break;
                    case "Scarab/Chara_Base_MV_Scarab": path = "Shader/Character/ScarabCharacter"; break;
                    case "Sirius_Effect/EF_SG_Common_Alpha": path = "Shader/HomeTheatre/Alpha"; break;
                    case "PerformanceFX/PerformanceFX": path = "Shader/HomeTheatre/Performance"; break;
                }
                var shader = path != null ? Resources.Load<Shader>(path) : Shader.Find(name);
                if (shader == null || !shader.isSupported || (path == null && !name.StartsWith("Universal Render Pipeline/")))
                    throw new InvalidOperationException("Unrestored theatre shader: " + name);
                material.shader = shader;
            }
        }
        public IEnumerator Release()
        {
            IsReady = false;
            if (_brain != null) { _brain.enabled = false; Destroy(_brain); }
            if (Environment != null) Environment.SetActive(false);
            if (HasScene) yield return SceneManager.UnloadSceneAsync(_scene);
        }
        private void OnDestroy()
        {
            _released = true; IsReady = false;
            if (_brain != null) { _brain.enabled = false; Destroy(_brain); }
            foreach (var lease in _leases.AsEnumerable().Reverse()) lease.Dispose();
            _leases.Clear();
        }
    }
}
