using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OpenWDS.Editor
{
    public static class ValidateTitleTransition
    {
        public static void Run()
        {
            var path = Path.GetFullPath("Assets/StreamingAssets/OpenWDS/Frontend/title-transition.bundle");
            var scripts = AssetBundle.LoadFromFile(Path.GetFullPath("Assets/StreamingAssets/OpenWDS/Menu/0.bundle"));
            var bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null) throw new InvalidOperationException("Title transition bundle failed to load");
            GameObject instance = null;
            try
            {
                var prefab = bundle.LoadAsset<GameObject>("TitleTransitionFade");
                if (prefab == null) throw new InvalidOperationException("Title transition root missing");
                instance = UnityEngine.Object.Instantiate(prefab);
                foreach (var component in instance.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component == null) throw new InvalidOperationException("Title transition missing script");
                foreach (var node in instance.GetComponentsInChildren<Transform>(true))
                    Debug.Log("OPENWDS_TITLE_COMPONENT " + node.name + "=" + string.Join(",", node.GetComponents<Component>().Select(c => c == null ? "MISSING" : c.GetType().FullName)));
                var animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.runtimeAnimatorController == null)
                    throw new InvalidOperationException("Title transition animator missing");
                Debug.Log("OPENWDS_TITLE_TRANSITION clips=" + string.Join(",", animator.runtimeAnimatorController.animationClips.Select(c => c.name + ":" + c.length)));
                foreach (var image in instance.GetComponentsInChildren<Image>(true))
                    Debug.Log($"OPENWDS_TITLE_IMAGE name={image.name} sprite={image.sprite?.name} material={image.material?.name} shader={image.material?.shader?.name}");
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                bundle.Unload(true);
                if (scripts != null) scripts.Unload(true);
            }
        }
    }
}
