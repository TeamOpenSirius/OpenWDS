using System.Linq;
using OpenWDS.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace OpenWDS.Editor
{
    public static class CreateGameResultScene
    {
        public const string ScenePath = "Assets/OpenWDS/Scenes/GameResult.unity";
        [MenuItem("OpenWDS/Create GameResult Scene")]
        public static void Run()
        {
            const string assetsFolder = "Assets/Resources/ScriptableObject";
            if (!AssetDatabase.IsValidFolder(assetsFolder)) AssetDatabase.CreateFolder("Assets/Resources", "ScriptableObject");
            var assetPath = assetsFolder + "/SceneTransitionAssets.asset";
            var assets = AssetDatabase.LoadAssetAtPath<SceneTransitionAssets>(assetPath);
            if (assets == null)
            {
                assets = ScriptableObject.CreateInstance<SceneTransitionAssets>();
                AssetDatabase.CreateAsset(assets, assetPath);
            }
            assets.Curtain = AssetDatabase.LoadAssetAtPath<Spine.Unity.SkeletonDataAsset>("Assets/Resources/Spine/curtain_Albedo_SkeletonData.asset");
            assets.CurtainMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Spine/Runtime/spine-unity/Materials/SkeletonGraphicDefault.mat");
            if (assets.Curtain == null || assets.CurtainMaterial == null) throw new System.InvalidOperationException("Original curtain assets missing.");
            EditorUtility.SetDirty(assets);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("GameResultPresenter").AddComponent<GameResultSceneRuntime>();
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new System.InvalidOperationException("Cannot save GameResult scene.");
            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            AssetDatabase.SaveAssets();
        }
    }
}
