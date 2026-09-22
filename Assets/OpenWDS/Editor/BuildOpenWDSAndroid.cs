using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OpenWDS.Runtime;

namespace OpenWDS.Editor
{
    public static class BuildOpenWDSAndroid
    {
        private static readonly string[] Scenes = ConfigureOfflineSongResources.Scenes;

        public static void BuildTouchDeviceApk()
        {
            ConfigureOfflineSongResources.Run();
            foreach (var scene in Scenes)
            {
                if (!File.Exists(scene))
                    throw new FileNotFoundException(
                        "Required Android build scene is missing.", scene);
            }

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new InvalidOperationException(
                    "Unity could not activate the Android build target.");
            }

            PlayerSettings.companyName = "OpenWDS";
            PlayerSettings.productName = "OpenWDS";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(
                BuildTargetGroup.Android, "dev.openwds.touchtest");
            PlayerSettings.defaultInterfaceOrientation =
                UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.Android.targetArchitectures =
                AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            DisableAutoJudgeInBuildScene();
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            ConfigureOfflineSongResources.AssertSongsExcluded();

            var output = Path.GetFullPath(
                Path.Combine("Build", "Android", "OpenWDS-touch-test.apk"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.DetailedBuildReport,
            };
            var buildDiagnostics = new List<string>();
            Application.LogCallback captureBuildDiagnostic =
                (condition, stackTrace, type) =>
                {
                    if (type == LogType.Error || type == LogType.Assert ||
                        type == LogType.Exception)
                        buildDiagnostics.Add(condition ?? string.Empty);
                };
            BuildReport report;
            Application.logMessageReceived += captureBuildDiagnostic;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                Application.logMessageReceived -= captureBuildDiagnostic;
            }
            var summary = report.summary;
            var packedPaths = report.packedAssets.SelectMany(asset => asset.contents)
                .Select(asset => asset.sourceAssetPath).Where(path => !string.IsNullOrEmpty(path)).Distinct().OrderBy(path => path).ToArray();
            if (packedPaths.Length == 0) throw new InvalidOperationException("Unity did not provide a packed-asset report.");
            File.WriteAllLines(Path.ChangeExtension(output, ".packed-assets.txt"), packedPaths);
            if (packedPaths.Any(path => path.Contains("/StandardCharts/") || path.Contains("/AnotherNotations/") || path.EndsWith("/LocalMusicCatalog.json")))
                throw new InvalidOperationException("Song assets were serialized into the player.");
            var meaningfulDiagnostics = buildDiagnostics
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToArray();
            var knownShaderGraphAssertions = meaningfulDiagnostics.Count(message =>
                message.Contains(
                    "buildTarget.platform >= kFirstValidStandaloneTarget"));
            var knownBundleImportDiagnostics = meaningfulDiagnostics.Count(message =>
                message ==
                    "unsupported platform: couldn't get group name for target %i" ||
                (message.StartsWith("Compute shader ", StringComparison.Ordinal) &&
                 message.EndsWith(
                     " was not imported correctly.", StringComparison.Ordinal)));
            var unexpectedDiagnostics = meaningfulDiagnostics.Where(message =>
                !message.Contains(
                    "buildTarget.platform >= kFirstValidStandaloneTarget") &&
                message !=
                    "unsupported platform: couldn't get group name for target %i" &&
                !(message.StartsWith("Compute shader ", StringComparison.Ordinal) &&
                  message.EndsWith(
                      " was not imported correctly.", StringComparison.Ordinal)))
                .Distinct()
                .ToArray();
            foreach (var diagnostic in unexpectedDiagnostics)
                Debug.LogError(
                    "OPENWDS_ANDROID_UNEXPECTED_DIAGNOSTIC " + diagnostic);
            var unexpectedErrors = unexpectedDiagnostics.Length;
            Debug.Log(
                "OPENWDS_ANDROID_BUILD " +
                $"result={summary.result} errors={summary.totalErrors} " +
                $"knownShaderGraphAssertions={knownShaderGraphAssertions} " +
                $"knownBundleImportDiagnostics={knownBundleImportDiagnostics} " +
                $"unexpectedErrors={unexpectedErrors} " +
                $"warnings={summary.totalWarnings} playerBuildBytes={summary.totalSize} " +
                $"apkBytes={new FileInfo(output).Length} " +
                $"development=false autoTouch=false unitySplash=true " +
                $"output={output}");
            if (summary.result != BuildResult.Succeeded ||
                !File.Exists(output) || unexpectedErrors != 0)
            {
                throw new InvalidOperationException(
                    "OpenWDS Android touch-device build failed.");
            }
        }

        private static void DisableAutoJudgeInBuildScene()
        {
            const string gameplayScene =
                "Assets/OpenWDS/Scenes/OfflineRhythmPreview.unity";
            var scene = EditorSceneManager.OpenScene(
                gameplayScene, OpenSceneMode.Single);
            var runtime = UnityEngine.Object.FindObjectOfType<
                GameRuntime>();
            if (runtime == null)
            {
                throw new InvalidOperationException(
                    "GameRuntime is missing from the Android gameplay scene.");
            }

            var serializedRuntime = new SerializedObject(runtime);
            var autoJudge = serializedRuntime.FindProperty("_enableAutoJudge");
            if (autoJudge == null)
            {
                throw new InvalidOperationException(
                    "GameRuntime._enableAutoJudge is missing.");
            }
            autoJudge.boolValue = false;
            serializedRuntime.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorSceneManager.SaveScene(scene, gameplayScene))
            {
                throw new InvalidOperationException(
                    "Failed to save the Android gameplay scene with AutoTouch disabled.");
            }
        }
    }
}
