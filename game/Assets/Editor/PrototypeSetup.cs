using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using PetThem.Game;

namespace PetThem.Editor
{
    public static class PrototypeSetup
    {
        [MenuItem("PET THEM/Create or open prototype scene")]
        public static void CreateScene()
        {
            const string path = "Assets/Scenes/Prototype.unity";
            Directory.CreateDirectory("Assets/Scenes");
            if (!File.Exists(path))
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), path);
            }
            else EditorSceneManager.OpenScene(path);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
            PlayerSettings.companyName = "PetThem";
            PlayerSettings.productName = "PET THEM!";
            // One source of truth, so the package version and the menu can never disagree.
            PlayerSettings.bundleVersion = Texts.Version;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.petthem.game");
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            // This prototype deliberately uses legacy touch input to minimize package dependencies.
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var inputMode = settings.FindProperty("activeInputHandler");
            if (inputMode != null) { inputMode.intValue = 0; settings.ApplyModifiedPropertiesWithoutUndo(); }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("PET THEM/Build Android development APK")]
        public static void BuildAndroid() => BuildApk(BuildOptions.Development, "development");

        /// <summary>
        /// The same build without the development flag, which is the one small enough to send.
        /// </summary>
        /// <remarks>
        /// The development build carries the profiler and debug symbols, and they are nearly all
        /// of its size: 31.2 MiB against 16.7 MiB for 0.15.0, almost entirely libunity.so at
        /// 24.0 MB rather than 11.4 MB. That put it over the 30 MiB limit for handing a file to
        /// the user, so this exists to avoid editing the build script every time one is needed.
        ///
        /// It is the same game from the same commit. What it loses is the development console, so
        /// a crash on the phone has nothing to report itself with -- prefer the development build
        /// when that matters and the size allows.
        /// </remarks>
        [MenuItem("PET THEM/Build Android release APK")]
        public static void BuildAndroidRelease() => BuildApk(BuildOptions.None, "release");

        private static void BuildApk(BuildOptions options, string label)
        {
            CreateScene();
            Directory.CreateDirectory("Builds");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/Prototype.unity" },
                locationPathName = $"Builds/PetThem-{label}.apk",
                target = BuildTarget.Android,
                options = options
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception($"Android {label} build failed.");
        }
    }
}
