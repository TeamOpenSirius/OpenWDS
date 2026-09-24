using System;
using System.IO;
using OpenWDS.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenWDS.Editor
{
    public static class ConfigureOfflineSongResources
    {
        public const string BootstrapScene = "Assets/OpenWDS/Scenes/OfflineBootstrap.unity";
        public static readonly string[] Scenes = {
            BootstrapScene,
            "Assets/OpenWDS/Scenes/FrontendRecovery.unity",
            "Assets/OpenWDS/Scenes/OfflineRhythmPreview.unity",
            "Assets/OpenWDS/Scenes/GameResult.unity",
            "Assets/OpenWDS/Scenes/LocalMusicSelection.unity"
        };
        public static void Run()
        {
            AssertSongsExcluded();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.06f, .07f, .10f);
            new GameObject("OfflineSongResources").AddComponent<OfflineSongResourcesBootstrap>();
            if (!EditorSceneManager.SaveScene(scene, BootstrapScene)) throw new IOException("Cannot save bootstrap scene.");
            EditorBuildSettings.scenes = Array.ConvertAll(Scenes, path => new EditorBuildSettingsScene(path, true));
            AssetDatabase.SaveAssets();
            Debug.Log("OPENWDS_OFFLINE_RESOURCES_CONFIGURED songsExternal=true download=false");
        }
        public static void AssertSongsExcluded()
        {
            foreach (var directory in new[] { "StandardCharts", "AnotherNotations" })
                if (Directory.Exists("Assets/StreamingAssets/OpenWDS/" + directory))
                    throw new InvalidOperationException("Song files must live outside Assets: " + directory);
            foreach (var scene in Scenes)
            {
                if (scene == BootstrapScene && !File.Exists(scene)) continue;
                if (!File.Exists(scene)) throw new FileNotFoundException("Missing release scene", scene);
                foreach (var dependency in AssetDatabase.GetDependencies(scene, true))
                    if (dependency.Contains("/StandardCharts/") || dependency.Contains("/AnotherNotations/") || dependency.EndsWith("/LocalMusicCatalog.json"))
                        throw new InvalidOperationException("Scene embeds song resource: " + dependency);
            }
        }
    }
}
