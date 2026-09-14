using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FlappyVoice.Editor
{
    // The Web build is the shipping target (the game is embedded by a host app), so its settings
    // live in code rather than in whatever state ProjectSettings was last saved in.
    public static class WebBuilder
    {
        private const string OutputDir = "Build/Web";

        [MenuItem("Flappy Voice/Build Web Player")]
        public static void BuildWeb()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                throw new Exception("[WebBuilder] Could not switch to the Web build target; is the module installed?");
            }

            ApplyWebSettings();

            string output = Path.GetFullPath(OutputDir);
            Directory.CreateDirectory(output);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Game.unity" },
                locationPathName = output,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"[WebBuilder] Build {report.summary.result} with {report.summary.totalErrors} error(s).");
            }

            Debug.Log($"[WebBuilder] Built to {output} ({report.summary.totalSize / (1024 * 1024)} MB).");
        }

        private static void ApplyWebSettings()
        {
            // Brotli halves the download, and the fallback decompressor keeps it working on hosts
            // that serve the files without a Content-Encoding header — which is most of them.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;

            // The microphone stops feeding the ring buffer the moment the page is backgrounded;
            // running in the background at least keeps the run's state intact across a tab switch.
            PlayerSettings.runInBackground = true;
        }
    }
}
