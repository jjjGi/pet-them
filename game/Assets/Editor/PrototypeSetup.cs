using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

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
            PlayerSettings.bundleVersion = "0.10.0";
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
        public static void BuildAndroid()
        {
            CreateScene();
            Directory.CreateDirectory("Builds");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/Prototype.unity" },
                locationPathName = "Builds/PetThem-development.apk",
                target = BuildTarget.Android,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Android build failed.");
        }
    }
}
