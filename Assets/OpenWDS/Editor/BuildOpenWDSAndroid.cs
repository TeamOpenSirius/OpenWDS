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
        private static readonly string[] Scenes =
        {
            "Assets/OpenWDS/Scenes/LocalMusicSelection.unity",
            "Assets/OpenWDS/Scenes/OfflineRhythmPreview.unity",
        };

        public static void BuildTouchDeviceApk()
        {
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
            PlayerSettings.productName = "OpenWDS Touch Test";
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

            var output = Path.GetFullPath(
                Path.Combine("Build", "Android", "OpenWDS-touch-test.apk"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
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
                $"warnings={summary.totalWarnings} bytes={summary.totalSize} " +
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
                RecoveredGameRuntime>();
            if (runtime == null)
            {
                throw new InvalidOperationException(
                    "RecoveredGameRuntime is missing from the Android gameplay scene.");
            }

            var serializedRuntime = new SerializedObject(runtime);
            var autoJudge = serializedRuntime.FindProperty("_enableAutoJudge");
            if (autoJudge == null)
            {
                throw new InvalidOperationException(
                    "RecoveredGameRuntime._enableAutoJudge is missing.");
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
