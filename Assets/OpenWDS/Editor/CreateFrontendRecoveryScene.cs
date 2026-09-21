using OpenWDS.Runtime;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenWDS.Editor
{
    public static class CreateFrontendRecoveryScene
    {
        public const string ScenePath = "Assets/OpenWDS/Scenes/FrontendRecovery.unity";
        [MenuItem("OpenWDS/Create Frontend Recovery Scene")]
        public static void Run()
        {
            CreateGameResultScene.Run();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cri = new GameObject("CRIWARE").AddComponent<CriWare.CriWareInitializer>();
            cri.initializesMana = false;
            cri.atomConfig.acfFileName = "OpenWDS/CRI/Sirius.acf";
            var camera = new GameObject("UICamera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            var root = new GameObject("MainCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Shared offline MainCanvas contract, including the original common popup dimensions.
            scaler.referenceResolution = new Vector2(1920, 1200);
            scaler.matchWidthOrHeight = 0.5f;
            var frontend = root.AddComponent<FrontendRecoveryRuntime>();
            frontend.Configure(
                AssetDatabase.LoadAssetAtPath<Shader>("Assets/TextMesh Pro/Shaders/TMP_Sprite.shader"),
                AssetDatabase.LoadAssetAtPath<Shader>("Assets/TextMesh Pro/Shaders/TMP_SDF.shader"));
            var selectionPage = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/SelectionPage.prefab");
            if (selectionPage == null) throw new System.InvalidOperationException("Generate the selection page with CreateLocalMusicSelectionScene first.");
            new GameObject("MainPageNavigation").AddComponent<MainPageNavigationRuntime>().Configure(frontend, selectionPage);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("OPENWDS_FRONTEND_SCENE_CREATED " + ScenePath);
        }
    }
}
