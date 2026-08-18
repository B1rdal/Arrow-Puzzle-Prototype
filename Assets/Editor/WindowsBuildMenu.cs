/*
Summary:
WindowsBuildMenu adds editor-only commands for making a Windows .exe build from the
same Unity project. It applies portrait-friendly standalone player settings, gathers
enabled build scenes, and creates a zip-ready Windows build folder.
*/

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WindowsBuildMenu
{
    private const string ProductName = "Arrow Puzzle Prototype";
    private const string BuildFolder = "Builds/Windows";
    private const string ExeName = "ArrowPuzzlePrototype.exe";

    public static void ApplyWindowsSettings()
    {
        PlayerSettings.productName = ProductName;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 720;
        PlayerSettings.defaultScreenHeight = 1280;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;

        EditorUtility.SetDirty(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
        AssetDatabase.SaveAssets();
        Debug.Log("Windows player settings applied.");
    }

    public static void BuildWindowsExe()
    {
        ApplyWindowsSettings();

        string[] scenes = GetEnabledBuildScenes();

        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Windows Build",
                "No enabled scenes found in Build Settings. Add SampleScene to Scenes In Build first.",
                "OK");
            return;
        }

        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
        {
            EditorUtility.DisplayDialog("Windows Build", "Could not switch to Windows Standalone build target.", "OK");
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string outputDirectory = Path.Combine(projectRoot, BuildFolder);
        Directory.CreateDirectory(outputDirectory);

        string outputPath = Path.Combine(outputDirectory, ExeName);
        BuildPlayerOptions buildOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Windows build succeeded: {outputPath} ({summary.totalSize / (1024f * 1024f):0.0} MB)");
            EditorUtility.RevealInFinder(outputDirectory);
            return;
        }

        Debug.LogError($"Windows build failed: {summary.result}");
        EditorUtility.DisplayDialog("Windows Build", $"Build failed: {summary.result}", "OK");
    }

    private static string[] GetEnabledBuildScenes()
    {
        List<string> scenes = new List<string>();

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
            {
                scenes.Add(scene.path);
            }
        }

        return scenes.ToArray();
    }
}
