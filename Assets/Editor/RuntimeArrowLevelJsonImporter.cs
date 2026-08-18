/*
Summary:
RuntimeArrowLevelJsonImporter bridges the standalone level editor and Unity assets.
It imports the editor's JSON files into PathArrowLevelData assets, and can export a
selected PathArrowLevelData asset back to JSON for editing in the standalone .exe.
*/

using System.IO;
using UnityEditor;
using UnityEngine;

public static class RuntimeArrowLevelJsonImporter
{
    [MenuItem("Tools/Arrow Puzzle/Runtime Level Editor/Import JSON Level")]
    public static void ImportJsonLevel()
    {
        string jsonPath = EditorUtility.OpenFilePanel("Import Runtime Arrow Level JSON", "", "json");
        if (string.IsNullOrEmpty(jsonPath))
        {
            return;
        }

        RuntimeArrowLevelDocument document;
        try
        {
            document = JsonUtility.FromJson<RuntimeArrowLevelDocument>(File.ReadAllText(jsonPath));
        }
        catch (System.Exception exception)
        {
            EditorUtility.DisplayDialog("Import JSON Level", $"Could not read JSON file:\n{exception.Message}", "OK");
            return;
        }

        if (document == null)
        {
            EditorUtility.DisplayDialog("Import JSON Level", "The selected file was not a valid arrow level JSON file.", "OK");
            return;
        }

        string defaultName = Path.GetFileNameWithoutExtension(jsonPath);
        string assetPath = EditorUtility.SaveFilePanelInProject(
            "Save PathArrowLevelData Asset",
            string.IsNullOrWhiteSpace(defaultName) ? "RuntimeImportedLevel" : defaultName,
            "asset",
            "Choose where to save the imported level asset.",
            "Assets");

        if (string.IsNullOrEmpty(assetPath))
        {
            return;
        }

        PathArrowLevelData asset = AssetDatabase.LoadAssetAtPath<PathArrowLevelData>(assetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<PathArrowLevelData>();
            AssetDatabase.CreateAsset(asset, assetPath);
        }

        WriteDocumentToAsset(document, asset);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        Debug.Log($"Imported runtime level JSON to asset: {assetPath}");
    }

    [MenuItem("Tools/Arrow Puzzle/Runtime Level Editor/Export Selected Level To JSON")]
    public static void ExportSelectedLevelToJson()
    {
        if (Selection.activeObject is not PathArrowLevelData levelData)
        {
            EditorUtility.DisplayDialog("Export JSON Level", "Select a PathArrowLevelData asset first.", "OK");
            return;
        }

        string jsonPath = EditorUtility.SaveFilePanel(
            "Export Runtime Arrow Level JSON",
            "",
            $"{levelData.name}.json",
            "json");

        if (string.IsNullOrEmpty(jsonPath))
        {
            return;
        }

        RuntimeArrowLevelDocument document = CreateDocumentFromAsset(levelData);
        File.WriteAllText(jsonPath, JsonUtility.ToJson(document, true));
        EditorUtility.RevealInFinder(jsonPath);
        Debug.Log($"Exported selected level asset to JSON: {jsonPath}");
    }

    [MenuItem("Tools/Arrow Puzzle/Runtime Level Editor/Export Selected Level To JSON", true)]
    private static bool CanExportSelectedLevelToJson()
    {
        return Selection.activeObject is PathArrowLevelData;
    }

    private static RuntimeArrowLevelDocument CreateDocumentFromAsset(PathArrowLevelData levelData)
    {
        RuntimeArrowLevelDocument document = new RuntimeArrowLevelDocument
        {
            width = levelData.Width,
            height = levelData.Height,
            hasCustomShape = levelData.HasCustomShape
        };

        foreach (Vector2Int activeCell in levelData.ActiveCells)
        {
            document.activeCells.Add(IntPoint.FromVector2Int(activeCell));
        }

        foreach (PathArrowData arrowData in levelData.Arrows)
        {
            RuntimeArrowJson arrow = new RuntimeArrowJson
            {
                id = arrowData.Id,
                color = SerializableColor.FromColor(arrowData.Color)
            };

            foreach (Vector2Int point in arrowData.Points)
            {
                arrow.points.Add(IntPoint.FromVector2Int(point));
            }

            document.arrows.Add(arrow);
        }

        return document;
    }

    private static void WriteDocumentToAsset(RuntimeArrowLevelDocument document, PathArrowLevelData asset)
    {
        SerializedObject serializedAsset = new SerializedObject(asset);
        serializedAsset.FindProperty("width").intValue = Mathf.Max(1, document.width);
        serializedAsset.FindProperty("height").intValue = Mathf.Max(1, document.height);

        SerializedProperty hasCustomShapeProperty = serializedAsset.FindProperty("hasCustomShape");
        if (hasCustomShapeProperty != null)
        {
            hasCustomShapeProperty.boolValue = document.UsesCustomShape;
        }

        SerializedProperty activeCellsProperty = serializedAsset.FindProperty("activeCells");

        if (activeCellsProperty != null)
        {
            activeCellsProperty.ClearArray();

            if (document.activeCells != null)
            {
                for (int i = 0; i < document.activeCells.Count; i++)
                {
                    activeCellsProperty.InsertArrayElementAtIndex(i);
                    activeCellsProperty.GetArrayElementAtIndex(i).vector2IntValue = document.activeCells[i].ToVector2Int();
                }
            }
        }

        SerializedProperty arrowsProperty = serializedAsset.FindProperty("arrows");
        arrowsProperty.ClearArray();

        if (document.arrows != null)
        {
            for (int i = 0; i < document.arrows.Count; i++)
            {
                RuntimeArrowJson sourceArrow = document.arrows[i];
                arrowsProperty.InsertArrayElementAtIndex(i);

                SerializedProperty targetArrow = arrowsProperty.GetArrayElementAtIndex(i);
                targetArrow.FindPropertyRelative("id").stringValue =
                    string.IsNullOrWhiteSpace(sourceArrow.id) ? $"Arrow {i + 1}" : sourceArrow.id;
                targetArrow.FindPropertyRelative("color").colorValue = sourceArrow.color.ToColor();

                SerializedProperty pointsProperty = targetArrow.FindPropertyRelative("points");
                pointsProperty.ClearArray();

                if (sourceArrow.points == null)
                {
                    continue;
                }

                for (int pointIndex = 0; pointIndex < sourceArrow.points.Count; pointIndex++)
                {
                    pointsProperty.InsertArrayElementAtIndex(pointIndex);
                    pointsProperty.GetArrayElementAtIndex(pointIndex).vector2IntValue =
                        sourceArrow.points[pointIndex].ToVector2Int();
                }
            }
        }

        serializedAsset.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }
}
