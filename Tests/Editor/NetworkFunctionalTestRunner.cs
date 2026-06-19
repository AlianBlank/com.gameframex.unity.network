using System;
using System.IO;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace GameFrameX.Network.Tests.Editor
{
    public static class NetworkFunctionalTestRunner
    {
        private const string AssemblyName = "GameFrameX.Network.Tests";
        private const string DefaultResultsPath = "Packages/com.gameframex.unity.network/TestResults.xml";
        private const string DefaultSummaryPath = "Packages/com.gameframex.unity.network/TestResults.summary.txt";

        public static void RunEditModeTests()
        {
            var resultsPath = GetArgumentValue("-testResults", DefaultResultsPath);
            var summaryPath = Path.ChangeExtension(resultsPath, ".summary.txt");
            if (string.Equals(summaryPath, resultsPath, StringComparison.OrdinalIgnoreCase))
            {
                summaryPath = DefaultSummaryPath;
            }

            Debug.Log("Running network EditMode tests with TestRunnerApi.");

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ResultCallbacks(resultsPath, summaryPath));
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { AssemblyName }
            })
            {
                runSynchronously = true
            });
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

        private sealed class ResultCallbacks : ICallbacks
        {
            private readonly string _resultsPath;
            private readonly string _summaryPath;

            public ResultCallbacks(string resultsPath, string summaryPath)
            {
                _resultsPath = resultsPath;
                _summaryPath = summaryPath;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log("Network EditMode test run started: " + testsToRun.FullName);
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                WriteXml(result, _resultsPath);
                WriteSummary(result, _summaryPath);

                Debug.LogFormat(
                    LogType.Log,
                    LogOption.NoStacktrace,
                    null,
                    "Network EditMode test run finished. Passed={0}, Failed={1}, Skipped={2}, Inconclusive={3}",
                    result.PassCount,
                    result.FailCount,
                    result.SkipCount,
                    result.InconclusiveCount);

                EditorApplication.Exit(result.FailCount == 0 && result.InconclusiveCount == 0 ? 0 : 1);
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Failed)
                {
                    Debug.LogError(result.FullName + Environment.NewLine + result.Message + Environment.NewLine + result.StackTrace);
                }
            }

            private static void WriteXml(ITestResultAdaptor result, string path)
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    NewLineOnAttributes = false
                };

                using (var writer = XmlWriter.Create(path, settings))
                {
                    result.ToXml().WriteTo(writer);
                }
            }

            private static void WriteSummary(ITestResultAdaptor result, string path)
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var builder = new StringBuilder();
                builder.AppendLine("Network EditMode Tests");
                builder.AppendLine("Passed: " + result.PassCount);
                builder.AppendLine("Failed: " + result.FailCount);
                builder.AppendLine("Skipped: " + result.SkipCount);
                builder.AppendLine("Inconclusive: " + result.InconclusiveCount);
                builder.AppendLine("Duration: " + result.Duration);
                AppendFailures(result, builder);

                File.WriteAllText(path, builder.ToString());
            }

            private static void AppendFailures(ITestResultAdaptor result, StringBuilder builder)
            {
                if (result.TestStatus == TestStatus.Failed)
                {
                    builder.AppendLine();
                    builder.AppendLine(result.FullName);
                    builder.AppendLine(result.Message);
                    builder.AppendLine(result.StackTrace);
                }

                if (!result.HasChildren)
                {
                    return;
                }

                foreach (var child in result.Children)
                {
                    AppendFailures(child, builder);
                }
            }
        }
    }
}
