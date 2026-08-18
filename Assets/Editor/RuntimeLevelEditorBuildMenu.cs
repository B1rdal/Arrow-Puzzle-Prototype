/*
Summary:
RuntimeLevelEditorBuildMenu creates and builds the standalone Windows level editor.
It generates a small runtime scene with RuntimeArrowLevelEditorApp, applies desktop
window settings, and builds only that scene into an .exe.
*/

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RuntimeLevelEditorBuildMenu
{
    private const string ProductName = "Arrow Level Editor";
    private const string ScenePath = "Assets/Scenes/RuntimeLevelEditorScene.unity";
    private const string DefaultArrowStylePath = "Assets/ArrowData/DefaultPathArrowStyle.asset";
    private const string BuildFolder = "Builds/LevelEditor";
    private const string ExeName = "ArrowLevelEditor.exe";

    [MenuItem("Tools/Arrow Puzzle/Runtime Level Editor/Create Or Refresh Scene")]
    public static void CreateOrRefreshScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        CreateOrRefreshSceneAsset(openSceneAfterSave: true);
        Debug.Log($"Runtime level editor scene refreshed: {ScenePath}");
    }

    public static void CreateOrRefreshSceneForAutomation()
    {
        CreateOrRefreshSceneAsset(openSceneAfterSave: true);
        Debug.Log($"Runtime level editor scene refreshed: {ScenePath}");
    }

    [MenuItem("Tools/Arrow Puzzle/Runtime Level Editor/Build EXE")]
    public static void BuildExe()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        CreateOrRefreshSceneAsset(openSceneAfterSave: false);
        ApplyWindowsSettings();

        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
        {
            EditorUtility.DisplayDialog("Runtime Level Editor", "Could not switch to Windows Standalone build target.", "OK");
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string outputDirectory = Path.Combine(projectRoot, BuildFolder);
        Directory.CreateDirectory(outputDirectory);

        string outputPath = Path.Combine(outputDirectory, ExeName);
        BuildPlayerOptions buildOptions = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Runtime level editor build succeeded: {outputPath} ({summary.totalSize / (1024f * 1024f):0.0} MB)");
            EditorUtility.RevealInFinder(outputDirectory);
            return;
        }

        Debug.LogError($"Runtime level editor build failed: {summary.result}");
        EditorUtility.DisplayDialog("Runtime Level Editor", $"Build failed: {summary.result}", "OK");
    }

    private static void ApplyWindowsSettings()
    {
        PlayerSettings.productName = ProductName;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 800;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;

        Object playerSettings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
        if (playerSettings != null)
        {
            EditorUtility.SetDirty(playerSettings);
        }

        AssetDatabase.SaveAssets();
    }

    private static void CreateOrRefreshSceneAsset(bool openSceneAfterSave)
    {
        EnsureFolder("Assets/Scenes");
        bool showLevelGeneratorButton = ReadExistingGeneratorButtonSetting();

        Scene previousScene = SceneManager.GetActiveScene();
        string previousScenePath = previousScene.path;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = Path.GetFileNameWithoutExtension(ScenePath);

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.11f, 0.12f, 0.14f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.nearClipPlane = -10f;
        camera.farClipPlane = 10f;
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);

        GameObject editorObject = new GameObject("RuntimeLevelEditorApp");
        RuntimeArrowLevelEditorApp app = editorObject.AddComponent<RuntimeArrowLevelEditorApp>();
        PathArrowStyleData defaultStyle = AssetDatabase.LoadAssetAtPath<PathArrowStyleData>(DefaultArrowStylePath);
        SerializedObject serializedApp = new SerializedObject(app);
        if (defaultStyle != null)
        {
            serializedApp.FindProperty("playableTestArrowStyle").objectReferenceValue = defaultStyle;
        }

        serializedApp.FindProperty("showLevelGeneratorButton").boolValue = showLevelGeneratorButton;
        serializedApp.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        if (openSceneAfterSave)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string previousSceneFullPath = string.IsNullOrEmpty(previousScenePath)
            ? string.Empty
            : Path.Combine(projectRoot, previousScenePath);

        if (!string.IsNullOrEmpty(previousScenePath) && File.Exists(previousSceneFullPath))
        {
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
        }
    }

    private static bool ReadExistingGeneratorButtonSetting()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string sceneFullPath = Path.Combine(projectRoot, ScenePath);
        if (!File.Exists(sceneFullPath))
        {
            return true;
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForRead = !scene.IsValid() || !scene.isLoaded;
        if (openedForRead)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        try
        {
            GameObject[] rootObjects = scene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                RuntimeArrowLevelEditorApp app = rootObjects[i].GetComponentInChildren<RuntimeArrowLevelEditorApp>(true);
                if (app == null)
                {
                    continue;
                }

                SerializedProperty property = new SerializedObject(app).FindProperty("showLevelGeneratorButton");
                return property == null || property.boolValue;
            }

            return true;
        }
        finally
        {
            if (openedForRead && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        string[] parts = assetFolder.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
