using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Yibi.Editor
{
    // Builds only the existing environment probe, without changing build settings or game scenes.
    public static class EnvironmentVerification
    {
        public static void BuildSmoke()
        {
            string workspace = Directory.GetParent(Application.dataPath).Parent.FullName;
            string output = Path.Combine(workspace, "Builds", "EnvironmentVerification");
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-verificationOutput");
            if (index >= 0 && index + 1 < args.Length) output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { M0Environment.ScenePath },
                locationPathName = Path.Combine(output, "EnvironmentProbe.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            string summary = "result=" + report.summary.result + "\nerrors=" + report.summary.totalErrors
                + "\nwarnings=" + report.summary.totalWarnings + "\nbytes=" + report.summary.totalSize;
            File.WriteAllText(Path.Combine(output, "build-summary.txt"), summary);
            Debug.Log("ENVIRONMENT_VERIFICATION_BUILD " + summary);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Environment verification build failed: " + summary);
        }
    }
}
