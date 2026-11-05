using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SonTob.Editor
{
    /// <summary>
    /// GitHub Actions(GameCI)와 메뉴에서 같은 방법으로 WebGL을 만듭니다.
    /// 씬 파일이 없으면 빈 Main 씬을 만들어 씁니다(게임은 Bootstrap이 띄움).
    /// </summary>
    public static class BuildScript
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("손톱 쥐/Main 씬 만들기")]
        public static void EnsureScene()
        {
            if (File.Exists(ScenePath)) return;
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        [MenuItem("손톱 쥐/WebGL 빌드")]
        public static void BuildWebGL()
        {
            EnsureScene();
            PlayerSettings.productName = "손톱 먹은 쥐";
            PlayerSettings.companyName = "jin";
            PlayerSettings.defaultWebScreenWidth = 540;
            PlayerSettings.defaultWebScreenHeight = 960;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true; // GitHub Pages는 Content-Encoding을 못 붙여서 필요
            PlayerSettings.WebGL.template = "APPLICATION:Default";

            string output = GetArg("-customBuildPath") ?? "build/WebGL/WebGL";
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var sum = report.summary;
            Debug.Log("Build result: " + sum.result + " → " + output);
            // game-ci/cli reads this block (or "Build succeeded!") from the log to decide pass/fail.
            Debug.Log(
                "\n###########################\n#      Build results      #\n###########################\n\n" +
                "Duration: " + sum.totalTime + "\nWarnings: " + sum.totalWarnings + "\n" +
                "Errors: " + (sum.result == BuildResult.Succeeded ? 0 : Math.Max(1, sum.totalErrors)) + "\n" +
                "Size: " + sum.totalSize + " bytes\n");
            if (sum.result == BuildResult.Succeeded) Debug.Log("Build succeeded!");
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
