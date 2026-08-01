using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

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

            var output = Path.GetFullPath(
                Path.Combine("Build", "Android", "OpenWDS-touch-test.apk"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development |
                          BuildOptions.AllowDebugging,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log(
                "OPENWDS_ANDROID_BUILD " +
                $"result={summary.result} errors={summary.totalErrors} " +
                $"warnings={summary.totalWarnings} bytes={summary.totalSize} " +
                $"output={output}");
            if (summary.result != BuildResult.Succeeded ||
                !File.Exists(output))
            {
                throw new InvalidOperationException(
                    "OpenWDS Android touch-device build failed.");
            }
        }
    }
}
