using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EncoreKit.GameHarness.Editor
{
    // Builds the harness headless: Unity -batchmode -quit -projectPath <host> -buildTarget Android -executeMethod EncoreKit.GameHarness.Editor.HarnessBuild.Android
    // (or -buildTarget WebGL with HarnessBuild.WebGL)
    public static class HarnessBuild
    {
        private const string ScenePath = "Assets/EncoreGameHarness.unity";
        private const string BundleId = "com.encorekit.unityharness";

        public static void Android()
        {
            PrepareScene();
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)35;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            ResolveAndroidDependencies();
            Build(BuildTarget.Android, "Builds/Harness/harness.apk");
        }

        public static void Ios()
        {
            PrepareScene();
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, BundleId);
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            // ENCORE_IOS_DEVICE=1 builds for a real device; sign the exported Xcode project with your team.
            var device = Environment.GetEnvironmentVariable("ENCORE_IOS_DEVICE") == "1";
            PlayerSettings.iOS.sdkVersion = device ? iOSSdkVersion.DeviceSDK : iOSSdkVersion.SimulatorSDK;
            // 2022.3 defaults the simulator build to x86_64, which current arm64 simulator runtimes cannot run.
            PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
            Build(BuildTarget.iOS, "Builds/Harness/iOS");
        }

        public static void WebGL()
        {
            PrepareScene();
            // Uncompressed, so any static file server can serve the build.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            Build(BuildTarget.WebGL, "Builds/Harness/WebGL");
        }

        private static void PrepareScene()
        {
            PlayerSettings.productName = "Encore Harness";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var harness = new SerializedObject(new GameObject("GameHarness").AddComponent<GameHarness>());
            // Harness only: ENCORE_API_KEY sets the key (for example a dashboard project's pk_test_ key), and
            // ENCORE_CLAIM_REMINDER=0 leaves the ClaimReminder out, as a publisher following the gift button setup would.
            var key = Environment.GetEnvironmentVariable("ENCORE_API_KEY");
            if (!string.IsNullOrEmpty(key)) harness.FindProperty("apiKey").stringValue = key;
            if (Environment.GetEnvironmentVariable("ENCORE_CLAIM_REMINDER") == "0") harness.FindProperty("claimReminder").boolValue = false;
            harness.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // EDM4U resolves on its own in the Editor; batchmode must ask for it before the build.
        private static void ResolveAndroidDependencies()
        {
            Type resolver = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                resolver = assembly.GetType("GooglePlayServices.PlayServicesResolver");
                if (resolver != null) break;
            }
            var method = resolver?.GetMethod("ResolveSync", new[] { typeof(bool) });
            if (method == null) throw new Exception("External Dependency Manager's Android resolver not found");
            if (!(bool)method.Invoke(null, new object[] { true })) throw new Exception("Android dependency resolution failed");
        }

        // Harness only: ENCORE_IOS_SDK_PATH points EncoreKit at a swift checkout holding a Debug Encore.xcframework, so the
        // SDK can reach a local backend. Runs after EDM4U writes the Podfile (order 40) and before it installs pods (50).
        [PostProcessBuild(45)]
        public static void UseLocalIosSdk(BuildTarget target, string path)
        {
            var sdk = Environment.GetEnvironmentVariable("ENCORE_IOS_SDK_PATH");
            if (target != BuildTarget.iOS || string.IsNullOrEmpty(sdk)) return;
            if (!Directory.Exists(Path.Combine(sdk, "Encore.xcframework")))
                throw new Exception($"No Encore.xcframework in {sdk}: run CONFIG=Debug scripts/release/build-xcframework.sh there first");
            // The same binary podspec a release publishes, so only the build configuration differs.
            var spec = Path.Combine(sdk, "EncoreKit.podspec");
            File.WriteAllText(spec, File.ReadAllText(Path.Combine(sdk, "templates/binary.podspec.tpl")).Replace("{{VERSION}}", "0.0.0"));
            var podfile = Path.Combine(path, "Podfile");
            var pinned = System.Text.RegularExpressions.Regex.Replace(
                File.ReadAllText(podfile), @"pod 'EncoreKit', '[^']*'(, :source => '[^']*')?", $"pod 'EncoreKit', :path => '{sdk}'");
            File.WriteAllText(podfile, pinned);
            Debug.Log($"[HarnessBuild] EncoreKit from {sdk}");
        }

        // Harness only: ENCORE_WEB_ENVIRONMENT (for example localhost) points the web SDK at another backend.
        [PostProcessBuild(100)]
        public static void UseWebEnvironment(BuildTarget target, string path)
        {
            var environment = Environment.GetEnvironmentVariable("ENCORE_WEB_ENVIRONMENT");
            if (target != BuildTarget.WebGL || string.IsNullOrEmpty(environment)) return;
            var index = Path.Combine(path, "index.html");
            var html = File.ReadAllText(index);
            if (!html.Contains("</head>")) throw new Exception($"No </head> in {index}");
            File.WriteAllText(index, html.Replace("</head>", $"<script>window.EncoreUnityEnvironment = \"{environment}\";</script>\n  </head>"));
            Debug.Log($"[HarnessBuild] Encore web environment {environment}");
        }

        private static void Build(BuildTarget target, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                options = BuildOptions.Development,
            });
            Debug.Log($"[HarnessBuild] {target}: {report.summary.result}, {report.summary.totalErrors} errors, output {path}");
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }

#if UNITY_ANDROID
    // Harness only: ENCORE_ANDROID_SDK_VERSION points the generated Gradle project at a local Encore build published to
    // mavenLocal (for example 2.5.0-local). EncoreDependencies.xml, what a publisher gets, stays the same.
    public sealed class UseLocalAndroidSdk : UnityEditor.Android.IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var version = Environment.GetEnvironmentVariable("ENCORE_ANDROID_SDK_VERSION");
            if (string.IsNullOrEmpty(version)) return;
            var gradle = Path.Combine(path, "build.gradle");
            var text = File.ReadAllText(gradle);
            var pinned = System.Text.RegularExpressions.Regex.Replace(text, @"com\.encorekit:encore:[^'""]+", $"com.encorekit:encore:{version}");
            if (pinned == text) throw new Exception($"No com.encorekit:encore dependency in {gradle}");
            File.WriteAllText(gradle, pinned);
            if (!File.ReadAllText(Path.Combine(path, "..", "settings.gradle")).Contains("mavenLocal()"))
                throw new Exception("The Gradle settings template must list mavenLocal() for a local Encore build");
            Debug.Log($"[HarnessBuild] Encore Android {version} from mavenLocal");
        }
    }
#endif
}
