using System;
using UnityEngine;
using UnityEngine.Playables;

namespace Sirius.Scarab
{
    // Current ARM64 0x5984c20–0x59856ec; fields from current metadata.
    [ExecuteAlways]
    public class MaterialParameterSetter : MonoBehaviour
    {
        [SerializeField] private PlayableDirector _playableDirector;
        [SerializeField] private Light _dirLight;
        [SerializeField] private MaterialInfo[] _materials;
        private readonly float[] _tempColorProperty = new float[3];
        private int _lightDirectionID = -1;
        private int _lightColorID = -1;
        private int _lightIntensityID = -1;
        private bool _hasMaterial;

        [Serializable]
        private class MaterialInfo
        {
            public Material _originalMaterial;
            public string _materialTrackName;
            [NonSerialized] public Material _instancedMaterial;
        }

        private void Awake()
        {
            _lightDirectionID = Shader.PropertyToID("_PrimaryLightDirection");
            _lightColorID = Shader.PropertyToID("_PrimaryLightColor");
            _lightIntensityID = Shader.PropertyToID("_PrimaryLightIntensity");
        }
        private void Start() => InitPropertyBlocks();
        private void OnDestroy() => DestroyInstancedMaterials();
        public void SetLight(Light light) => _dirLight = light;

        private void InitPropertyBlocks()
        {
            if (_materials == null) return;
            DestroyInstancedMaterials();
            foreach (var info in _materials)
            {
                if (!info._originalMaterial) return;
                info._instancedMaterial = new Material(info._originalMaterial);
                info._instancedMaterial.name = info._originalMaterial.name + " (Copy)";
            }
            var total = 0;
            foreach (var renderer in gameObject.GetComponentsInChildren<Renderer>())
            {
                var replacements = renderer.sharedMaterials;
                var matches = 0;
                for (var i = 0; i < renderer.sharedMaterials.Length; i++)
                    foreach (var info in _materials)
                        if (renderer.sharedMaterials[i] && info._originalMaterial == renderer.sharedMaterials[i])
                        {
                            replacements[i] = info._instancedMaterial;
                            matches++;
                        }
                // Partial matches and duplicate mappings must not change this test.
                if (matches == renderer.sharedMaterials.Length) renderer.sharedMaterials = replacements;
                total += matches;
            }
            if (total < 1)
            {
                Debug.LogWarning("Incorrect material: " + gameObject.name);
                return;
            }
            _hasMaterial = true;
            if (_playableDirector != null)
                foreach (var info in _materials)
                    if (!string.IsNullOrEmpty(info._materialTrackName))
                        TimelineUtility.BindComponentToTrack<MaterialTrack>(info._instancedMaterial,
                            _playableDirector, info._materialTrackName);
        }

        private void DestroyInstancedMaterials()
        {
            if (_materials == null) return;
            foreach (var info in _materials)
                if (info != null && info._instancedMaterial)
                {
                    Destroy(info._instancedMaterial);
                    info._instancedMaterial = null;
                }
        }

        private void LateUpdate()
        {
            var direction = new Vector3(0, 0, -1);
            var color = Vector3.one;
            var intensity = 1f;
            if (_dirLight != null)
            {
                direction = _dirLight.transform.rotation * Vector3.forward;
                color = new Vector3(Mathf.Clamp01(_dirLight.color.r), Mathf.Clamp01(_dirLight.color.g),
                    Mathf.Clamp01(_dirLight.color.b)) * _dirLight.intensity;
                _tempColorProperty[0] = color.x;
                _tempColorProperty[1] = color.y;
                _tempColorProperty[2] = color.z;
                intensity = Mathf.Max(_tempColorProperty);
            }
            if (_materials == null || !_hasMaterial) return;
            foreach (var info in _materials)
            {
                if (!info._instancedMaterial) continue;
                info._instancedMaterial.SetVector(_lightDirectionID,
                    new Vector4(-direction.x, -direction.y, -direction.z, 0));
                info._instancedMaterial.SetVector(_lightColorID, new Vector4(color.x, color.y, color.z, 0));
                info._instancedMaterial.SetFloat(_lightIntensityID, intensity);
            }
        }
        private string GetMaterialName(Material material) => material ? material.name : "null";
    }
}
