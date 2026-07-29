// GameBuild.cs
// Static build pipeline for Strategos.
// Callable from the Unity Editor menu or from the command line via -executeMethod.
//
// CLI usage (batch mode):
//   Unity -batchmode -quit -nographics \
//         -projectPath /path/to/strategos \
//         -executeMethod Strategos.Editor.GameBuild.BuildWindows \
//         -customBuildPath Artifacts/Windows \
//         [-development] \
//         -logFile Artifacts/build.log
//
// Available methods:
//   Strategos.Editor.GameBuild.BuildWindows
//   Strategos.Editor.GameBuild.BuildLinux
//   Strategos.Editor.GameBuild.BuildMacOS
//   Strategos.Editor.GameBuild.BuildWebGL
//   Strategos.Editor.GameBuild.BuildAll

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Strategos.Editor
{
    public static class GameBuild
    {
        private const string DemoScenePath = "Assets/Scenes/Demo/SymbolDemo.unity";

        // -------------------------------------------------------------------------
        // Editor menu items  (Strategos → Build → …)
        // -------------------------------------------------------------------------

        [MenuItem("Strategos/Build/Windows x64")]
        public static void BuildWindows() => Run(BuildTarget.StandaloneWindows64);

        [MenuItem("Strategos/Build/Linux x64  (Steam Deck)")]
        public static void BuildLinux() => Run(BuildTarget.StandaloneLinux64);

        [MenuItem("Strategos/Build/macOS")]
        public static void BuildMacOS() => Run(BuildTarget.StandaloneOSX);

        [MenuItem("Strategos/Build/WebGL")]
        public static void BuildWebGL() => Run(BuildTarget.WebGL);

        [MenuItem("Strategos/Build/All Platforms")]
        public static void BuildAll()
        {
            Run(BuildTarget.StandaloneWindows64);
            Run(BuildTarget.StandaloneLinux64);
            Run(BuildTarget.StandaloneOSX);
        }

        // -------------------------------------------------------------------------
        // Core build runner
        // -------------------------------------------------------------------------

        private static void Run(BuildTarget target)
        {
            string output    = ResolveOutputPath(target);
            bool   isDev     = HasArg("-development");
            var    scenes    = GetEnabledScenes();

            if (scenes.Length == 0)
            {
                Fail($"No scenes in EditorBuildSettings. " +
                     "Run 'Strategos > Open Demo Scene' first, or add scenes manually.");
                return;
            }

            Directory.CreateDirectory(output);

            var opts = new BuildPlayerOptions
            {
                scenes           = scenes,
                locationPathName = OutputFile(target, output),
                target           = target,
                options          = isDev ? BuildOptions.Development : BuildOptions.None,
            };

            LogHeader(target, opts);

            var report  = UnityEditor.BuildPipeline.BuildPlayer(opts);
            var summary = report.summary;

            LogSummary(summary);

            if (summary.result != BuildResult.Succeeded)
                Fail($"Build FAILED for {target} (errors: {summary.totalErrors})");
            else if (IsBatchMode())
                EditorApplication.Exit(0);
        }

        // -------------------------------------------------------------------------
        // Path helpers
        // -------------------------------------------------------------------------

        private static string ResolveOutputPath(BuildTarget target)
        {
            // -customBuildPath passed by the PowerShell/bash scripts or game-ci
            string custom = GetArg("-customBuildPath");
            if (!string.IsNullOrEmpty(custom)) return custom;

            return Path.Combine("Artifacts", PlatformDir(target));
        }

        private static string OutputFile(BuildTarget target, string dir) => target switch
        {
            BuildTarget.StandaloneWindows64 => Path.Combine(dir, "Strategos.exe"),
            BuildTarget.StandaloneOSX       => Path.Combine(dir, "Strategos"),
            BuildTarget.StandaloneLinux64   => Path.Combine(dir, "Strategos"),
            BuildTarget.WebGL               => dir,   // WebGL outputs a directory
            _                               => Path.Combine(dir, "Strategos"),
        };

        private static string PlatformDir(BuildTarget t) => t switch
        {
            BuildTarget.StandaloneWindows64 => "Windows",
            BuildTarget.StandaloneLinux64   => "Linux",
            BuildTarget.StandaloneOSX       => "macOS",
            BuildTarget.WebGL               => "WebGL",
            _                               => t.ToString(),
        };

        // -------------------------------------------------------------------------
        // Scene helpers
        // -------------------------------------------------------------------------

        private static string[] GetEnabledScenes()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                // In batch mode, SceneBootstrapper's delayCall may not have fired yet.
                // Call EnsureSceneRegistered() directly so the demo scene is created
                // and added to build settings synchronously before we proceed.
                Debug.Log("[GameBuild] No scenes registered — bootstrapping demo scene.");
                SceneBootstrapper.EnsureSceneRegistered();

                scenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path)
                    .ToArray();
            }

            return scenes;
        }

        // -------------------------------------------------------------------------
        // CLI argument helpers
        // -------------------------------------------------------------------------

        private static string GetArg(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        private static bool HasArg(string flag) =>
            Environment.GetCommandLineArgs()
                .Any(a => a.Equals(flag, StringComparison.OrdinalIgnoreCase));

        private static bool IsBatchMode() =>
            HasArg("-batchmode");

        // -------------------------------------------------------------------------
        // Exit helpers
        // -------------------------------------------------------------------------

        private static void Fail(string message)
        {
            Debug.LogError($"[GameBuild] {message}");
            if (IsBatchMode()) EditorApplication.Exit(1);
        }

        // -------------------------------------------------------------------------
        // Logging
        // -------------------------------------------------------------------------

        private static void LogHeader(BuildTarget target, BuildPlayerOptions opts)
        {
            Debug.Log("[GameBuild] ═══════════════════════════════════════════");
            Debug.Log($"[GameBuild] Target  : {target}");
            Debug.Log($"[GameBuild] Output  : {opts.locationPathName}");
            Debug.Log($"[GameBuild] Options : {opts.options}");
            Debug.Log($"[GameBuild] Scenes  : {opts.scenes.Length}");
            foreach (var s in opts.scenes) Debug.Log($"[GameBuild]   › {s}");
            Debug.Log("[GameBuild] ═══════════════════════════════════════════");
        }

        private static void LogSummary(BuildSummary s)
        {
            string icon = s.result == BuildResult.Succeeded ? "✓" : "✗";
            Debug.Log(
                $"[GameBuild] {icon} {s.result}  |  " +
                $"Time: {s.totalTime:hh\\:mm\\:ss}  |  " +
                $"Size: {s.totalSize / 1_048_576.0:F1} MB  |  " +
                $"Errors: {s.totalErrors}  |  " +
                $"Warnings: {s.totalWarnings}");

            if (s.result == BuildResult.Succeeded)
                Debug.Log($"[GameBuild] → {s.outputPath}");
        }
    }
}
#endif
