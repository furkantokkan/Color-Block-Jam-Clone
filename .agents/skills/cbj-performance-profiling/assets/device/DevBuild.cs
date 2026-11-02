// COPY-ONLY build script for the device check (cbj-performance-profiling skill). device_build.ps1 copies it into
// Assets/DevProbe/Editor of a mirror copy; it must never be added to the real project.
// Builds Addressables, then a Development APK whose first scene is Game, so the phone opens straight into the level
// set in the copy's Game scene. With -cbjFromInit on the command line the APK starts at Init like the real game.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DevProbe.Editor
{
    public static class DevBuild
    {
        private const string k_init = "Assets/ColorBlockJam/Scenes/Init.unity";
        private const string k_hub = "Assets/ColorBlockJam/Scenes/Hub.unity";
        private const string k_game = "Assets/ColorBlockJam/Scenes/Game.unity";

        public static void Run()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("DEVBUILD FAILED: " + error);
                EditorApplication.Exit(1);
            }
        }

        private static void Build()
        {
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult addressables);
            if (!string.IsNullOrEmpty(addressables.Error)) throw new InvalidOperationException("Addressables build failed: " + addressables.Error);

            EditorUserBuildSettings.buildAppBundle = false;
            bool fromInit = Array.IndexOf(Environment.GetCommandLineArgs(), "-cbjFromInit") >= 0;
            var options = new BuildPlayerOptions
            {
                scenes = fromInit ? new[] { k_init, k_hub, k_game } : new[] { k_game, k_hub, k_init },
                locationPathName = Path.GetFullPath("Builds/cbj-dev.apk"),
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("DEVBUILD result " + report.summary.result + " size " + report.summary.totalSize + " time " + report.summary.totalTime
                + " errors " + report.summary.totalErrors + " warnings " + report.summary.totalWarnings);
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Build " + report.summary.result);
        }
    }
}
