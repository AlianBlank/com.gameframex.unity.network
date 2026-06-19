using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GameFrameX.Network.Tests.Editor
{
    public static class NetworkBuildValidationRunner
    {
        public static void BuildStandaloneOSX()
        {
            var outputPath = GetArgumentValue(
                "-buildOutput",
                Path.GetFullPath("Builds/NetworkValidation/StandaloneOSX/GameFrameXNetworkValidation.app"));
            var summaryPath = GetArgumentValue(
                "-buildSummary",
                Path.GetFullPath("Packages/com.gameframex.unity.network/BuildValidation.StandaloneOSX.summary.txt"));

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            Directory.CreateDirectory(Path.GetDirectoryName(summaryPath));

            var scenes = GetEnabledScenes();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            WriteSummary(report, outputPath, summaryPath, scenes);

            if (report.summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.Exit(0);
        }

        private static string[] GetEnabledScenes()
        {
            var scenes = EditorBuildSettings.scenes;
            var enabledScenes = new System.Collections.Generic.List<string>();
            foreach (var scene in scenes)
            {
                if (scene.enabled)
                {
                    enabledScenes.Add(scene.path);
                }
            }

            return enabledScenes.ToArray();
        }

        private static string GetArgumentValue(string name, string defaultValue)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return defaultValue;
        }

        private static void WriteSummary(BuildReport report, string outputPath, string summaryPath, string[] scenes)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Network StandaloneOSX Build Validation");
            builder.AppendLine("Result: " + report.summary.result);
            builder.AppendLine("Output: " + outputPath);
            builder.AppendLine("TotalSize: " + report.summary.totalSize);
            builder.AppendLine("TotalTime: " + report.summary.totalTime);
            builder.AppendLine("TotalErrors: " + report.summary.totalErrors);
            builder.AppendLine("TotalWarnings: " + report.summary.totalWarnings);
            builder.AppendLine("Scenes:");
            foreach (var scene in scenes)
            {
                builder.AppendLine("- " + scene);
            }

            File.WriteAllText(summaryPath, builder.ToString());
            Debug.Log(builder.ToString());
        }
    }
}
