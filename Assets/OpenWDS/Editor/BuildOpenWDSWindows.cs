using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenWDS.Editor
{
    public static class BuildOpenWDSWindows
    {
        private static readonly string[] Scenes =
        {
            "Assets/OpenWDS/Scenes/LocalMusicSelection.unity",
            "Assets/OpenWDS/Scenes/OfflineRhythmPreview.unity",
        };

        [Serializable]
        private sealed class WindowsBuildReport
        {
            public string generatedAtUtc;
            public string result;
            public string output;
            public ulong totalBytes;
            public int errors;
            public int warnings;
            public int knownShaderGraphAssertions;
            public int unexpectedErrors;
            public int splitLaneBundleCount;
            public bool splitLaneIndexPresent;
            public string[] scenes;
            public bool valid;
        }

        public static void BuildOfflineTest()
        {
            foreach (var scene in Scenes)
                if (!File.Exists(scene))
                    throw new FileNotFoundException(
                        "Required Windows build scene is missing.", scene);

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException(
                    "Unity could not activate the Windows x64 build target.");

            PlayerSettings.companyName = "OpenWDS";
            PlayerSettings.productName = "OpenWDS Offline Test";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;

            var output = Path.GetFullPath(Path.Combine(
                "Build", "Windows", "OpenWDS-offline-test", "OpenWDS.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.Development,
            });
            var summary = build.summary;
            var errorMessages = build.steps
                .SelectMany(step => step.messages)
                .Where(message => message.type == LogType.Error ||
                                  message.type == LogType.Assert ||
                                  message.type == LogType.Exception)
                .ToArray();
            var knownShaderGraphAssertions = errorMessages.Count(message =>
                    message.content.Contains(
                        "buildTarget.platform >= kFirstValidStandaloneTarget"));
            var unexpectedErrors = errorMessages.Length -
                knownShaderGraphAssertions;
            var playerRoot = Path.GetDirectoryName(output);
            var splitLaneRoot = Path.Combine(
                playerRoot, "OpenWDS_Data", "StreamingAssets", "OpenWDS",
                "SplitLane", "1.96.0");
            var splitLaneBundleCount = Directory.Exists(splitLaneRoot)
                ? Directory.GetFiles(
                    splitLaneRoot, "*.bundle", SearchOption.AllDirectories).Length
                : 0;
            var splitLaneIndexPresent = File.Exists(Path.Combine(
                splitLaneRoot, "split-effect-index.json"));
            var report = new WindowsBuildReport
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                result = summary.result.ToString(),
                output = output,
                totalBytes = summary.totalSize,
                errors = summary.totalErrors,
                warnings = summary.totalWarnings,
                knownShaderGraphAssertions = knownShaderGraphAssertions,
                unexpectedErrors = unexpectedErrors,
                splitLaneBundleCount = splitLaneBundleCount,
                splitLaneIndexPresent = splitLaneIndexPresent,
                scenes = Scenes.ToArray(),
            };
            report.valid = summary.result == BuildResult.Succeeded &&
                File.Exists(output) && report.unexpectedErrors == 0 &&
                report.splitLaneBundleCount == 790 &&
                report.splitLaneIndexPresent;
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var reportPath = Path.GetFullPath(Path.Combine(
                projectRoot, "../../reverse/reports/",
                "unity-windows-offline-build.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true) + "\n");
            Debug.Log(string.Format(
                "OPENWDS_WINDOWS_BUILD valid={0} result={1} errors={2} " +
                "knownShaderGraphAssertions={3} unexpectedErrors={4} " +
                "warnings={5} bytes={6} splitBundles={7} splitIndex={8} output={9}",
                report.valid, report.result, report.errors,
                report.knownShaderGraphAssertions, report.unexpectedErrors,
                report.warnings,
                report.totalBytes, report.splitLaneBundleCount,
                report.splitLaneIndexPresent, report.output));
            if (!report.valid)
                throw new InvalidOperationException(
                    "OpenWDS Windows offline test build failed validation.");
        }
    }
}
