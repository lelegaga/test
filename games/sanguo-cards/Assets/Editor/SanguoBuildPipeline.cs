using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sanguo.Editor
{
    /// <summary>
    /// One-click project configuration and command-line builds:
    ///   Unity -batchmode -quit -projectPath games/sanguo-cards -executeMethod Sanguo.Editor.SanguoBuild.BuildAndroidApk
    /// Release signing for Android is read from the SANGUO_KEYSTORE, SANGUO_KEYSTORE_PASS,
    /// SANGUO_KEY_ALIAS and SANGUO_KEY_PASS environment variables (debug signing otherwise).
    /// </summary>
    public static class SanguoProjectSetup
    {
        public const string BootScenePath = "Assets/Scenes/Boot.unity";
        public const string BundleId = "com.sanguo.cards";

        [MenuItem("Sanguo/Apply Recommended Settings")]
        public static void ApplyAll()
        {
            ApplyPlayerSettings();
            EnsureBootScene();
            Debug.Log("[Sanguo] Player settings applied and boot scene registered.");
        }

        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Sanguo";
            PlayerSettings.productName = "三国身份牌";
            PlayerSettings.runInBackground = true;

            // Landscape only (phones, tablets, notch/punch-hole handled by the UI safe area).
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, BundleId);
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, BundleId);

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            PlayerSettings.iOS.requiresFullScreen = true;

            // Windows debug build: resizable window.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
        }

        /// <summary>The UI is built from code, so the only scene is an empty boot scene.</summary>
        public static void EnsureBootScene()
        {
            if (!File.Exists(BootScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BootScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, BootScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScenePath, true) };
        }
    }

    public static class SanguoBuild
    {
        [MenuItem("Sanguo/Build/Android APK")]
        public static void BuildAndroidApk() => BuildAndroid(false);

        [MenuItem("Sanguo/Build/Android App Bundle (AAB)")]
        public static void BuildAndroidAab() => BuildAndroid(true);

        [MenuItem("Sanguo/Build/iOS Xcode Project")]
        public static void BuildIOS()
        {
            SanguoProjectSetup.ApplyAll();
            Build(BuildTarget.iOS, "Builds/iOS", BuildOptions.None);
        }

        [MenuItem("Sanguo/Build/Windows Debug")]
        public static void BuildWindowsDebug()
        {
            SanguoProjectSetup.ApplyAll();
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Sanguo.exe", BuildOptions.Development | BuildOptions.AllowDebugging);
        }

        private static void BuildAndroid(bool appBundle)
        {
            SanguoProjectSetup.ApplyAll();
            EditorUserBuildSettings.buildAppBundle = appBundle;
            ConfigureAndroidSigning();
            Build(BuildTarget.Android, "Builds/Android/sanguo-cards." + (appBundle ? "aab" : "apk"), BuildOptions.None);
        }

        private static void ConfigureAndroidSigning()
        {
            string keystore = Environment.GetEnvironmentVariable("SANGUO_KEYSTORE");
            if (string.IsNullOrEmpty(keystore))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                return;
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("SANGUO_KEYSTORE_PASS") ?? string.Empty;
            PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("SANGUO_KEY_ALIAS") ?? string.Empty;
            PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("SANGUO_KEY_PASS") ?? string.Empty;
        }

        private static void Build(BuildTarget target, string path, BuildOptions options)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SanguoProjectSetup.BootScenePath },
                locationPathName = path,
                target = target,
                options = options
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                string message = "[Sanguo] Build failed for " + target + ": " + report.summary.result;
                if (Application.isBatchMode)
                {
                    Debug.LogError(message);
                    EditorApplication.Exit(1);
                }
                throw new BuildFailedException(message);
            }
            Debug.Log("[Sanguo] Built " + target + " → " + path + " (" + report.summary.totalSize / (1024 * 1024) + " MB)");
        }
    }

    /// <summary>Adds the iOS local network permission text and Bonjour service types after an iOS build.</summary>
    public sealed class IosBuildPostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            string plist = Path.Combine(report.summary.outputPath, "Info.plist");
            if (!File.Exists(plist))
            {
                Debug.LogWarning("[Sanguo] Info.plist not found at " + plist);
                return;
            }
            InfoPlistEditor.ApplyLanKeys(plist);
            Debug.Log("[Sanguo] Added local network keys to " + plist);
        }
    }
}
