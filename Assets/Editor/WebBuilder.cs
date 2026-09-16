using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FlappyVoice.Editor
{
    /// <summary>
    /// Owns the shipping Web build so its settings live in code rather than in whatever state
    /// ProjectSettings was last saved in. Mirrors the parent catalog's
    /// <c>Variant.Originals.Web.Editor.OriginalsWebBuild</c> — same release settings, same
    /// <c>Variant</c> template, same staging and size check — because this repo is a submodule at
    /// <c>games/flappy-voice/</c> and must produce what the parent's publish step expects.
    /// </summary>
    public static class WebBuilder
    {
        // The parent refuses to publish a Unity game larger than this, and finding out at publish
        // time means a wasted build. Fail here instead, on the same number.
        private const long PublishedSizeLimit = 75L * 1024L * 1024L;

        // The parent's contract: a Unity game's publishDir is "dist", checked by publish.mjs.
        private const string OutputDirectoryName = "dist";

        /// <summary>
        /// Reads the catalog entry the build needs. Only the fields Unity consumes are declared —
        /// <see cref="JsonUtility"/> ignores the rest, which is what keeps this from having to
        /// track the parent's full schema.
        /// </summary>
        [Serializable]
        private sealed class GameConfig
        {
            public string orientation = null;
        }

        /// <summary>
        /// Applies the release Web settings. Separate from the build so the Editor can be put into
        /// the shipping configuration without waiting for a full player build.
        /// </summary>
        [MenuItem("Flappy Voice/Apply Web Settings")]
        public static void ApplyWebSettings()
        {
            NamedBuildTarget webTarget = NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.WebGL);

            // game.json is the catalog's file, not Unity's, but orientation has to reach the
            // template before the first frame, so the build reads it rather than duplicating it.
            string configPath = Path.Combine(Application.dataPath, "..", "game.json");
            GameConfig config = JsonUtility.FromJson<GameConfig>(File.ReadAllText(configPath));
            string orientation = config?.orientation ?? "portrait";
            if (orientation != "portrait" && orientation != "landscape")
            {
                throw new BuildFailedException("game.json orientation must be portrait or landscape.");
            }
            bool landscape = orientation == "landscape";

            PlayerSettings.defaultWebScreenWidth = landscape ? 1280 : 1125;
            PlayerSettings.defaultWebScreenHeight = landscape ? 720 : 2436;

            // The parent's choice, adopted wholesale: every Original stops when its tab is hidden.
            // This project used to set it true so a tab switch did not cost the run, but the
            // microphone stops feeding the ring buffer when the page is backgrounded anyway, so
            // what was being preserved was a run the player could not hear themselves play.
            PlayerSettings.runInBackground = false;

            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetApiCompatibilityLevel(webTarget, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetIl2CppCodeGeneration(webTarget, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetManagedStrippingLevel(webTarget, ManagedStrippingLevel.High);
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            // PROJECT:Variant is the copy in Assets/WebGLTemplates, kept byte-identical to the
            // parent's canonical one. It posts the orientation to the host before Unity boots.
            PlayerSettings.WebGL.template = "PROJECT:Variant";
            PlayerSettings.SetTemplateCustomValue("ORIGINALS_ORIENTATION", orientation);

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;

            // The parent's choice again: its CDN serves Content-Encoding: br, so the JavaScript
            // fallback decompressor is dead weight on every load. This is the one setting that
            // costs us the "works on any dumb static host" property — see Docs/build.md.
            PlayerSettings.WebGL.decompressionFallback = false;

            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;

            // Threads would put a SharedArrayBuffer in the build and oblige the host to send COOP
            // and COEP headers on a page it embeds in an iframe. Never turn this on.
            PlayerSettings.WebGL.threadsSupport = false;

            PlayerSettings.WebGL.wasm2023 = true;
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Builds the shipping Web player into <c>dist/</c>. Builds to a staging directory first so
        /// a failed or oversized build cannot leave a half-written <c>dist/</c> behind for the
        /// publish step to pick up.
        /// </summary>
        [MenuItem("Flappy Voice/Build Web Player")]
        public static void BuildWeb()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                throw new Exception("[WebBuilder] Could not switch to the Web build target; is the module installed?");
            }

            ApplyWebSettings();

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new BuildFailedException("Could not resolve the Unity project root.");

            // Unity names the payload files after the output directory's final component, so the
            // staging path has to end in "dist" too or the moved build ships as staging.data.
            string stagingRoot = Path.Combine(projectRoot, "Build", ".originals-web-build");
            string stagingPath = Path.Combine(stagingRoot, OutputDirectoryName);
            string outputPath = Path.Combine(projectRoot, OutputDirectoryName);

            if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true);
            Directory.CreateDirectory(stagingRoot);

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    // The scene list is named here rather than read from EditorBuildSettings: the
                    // scene is generated by SceneBuilder, and a build that depends on what someone
                    // last ticked in a dialog is not reproducible.
                    scenes = new[] { "Assets/Scenes/Game.unity" },
                    locationPathName = stagingPath,
                    target = BuildTarget.WebGL,
                    targetGroup = BuildTargetGroup.WebGL,
                    options = BuildOptions.None,
                });
            }
            catch
            {
                if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true);
                throw;
            }

            if (report.summary.result != BuildResult.Succeeded)
            {
                Directory.Delete(stagingRoot, true);
                throw new BuildFailedException(
                    $"[WebBuilder] Build {report.summary.result} with {report.summary.totalErrors} error(s).");
            }

            long publishedSize = Directory.GetFiles(stagingPath, "*", SearchOption.AllDirectories)
                .Sum(file => new FileInfo(file).Length);
            if (publishedSize > PublishedSizeLimit)
            {
                Directory.Delete(stagingRoot, true);
                throw new BuildFailedException(
                    $"[WebBuilder] Build is {publishedSize} bytes; the published limit is {PublishedSizeLimit} bytes.");
            }

            if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
            Directory.Move(stagingPath, outputPath);
            Directory.Delete(stagingRoot, true);

            Debug.Log($"[WebBuilder] Built to {outputPath} ({publishedSize} published bytes, " +
                      $"{report.summary.totalSize} build-report bytes).");
        }
    }
}
