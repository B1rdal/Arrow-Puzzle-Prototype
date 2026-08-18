/*
Summary:
ProgressiveLevelBatchGenerator drives the standalone editor's real procedural
generator to create a deterministic 30-level campaign. It tests several seeds for
each profile, keeps the strongest solvable result, and writes matching JSON and
PathArrowLevelData assets plus a progression report.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ProgressiveLevelBatchGenerator
{
    private const string AssetOutputFolder = "Assets/LevelsData/GeneratedProgression25";
    private const string JsonOutputFolder = "Assets/LevelsData/GeneratedProgression25Json";
    private const string RequestRelativePath = "Library/CodexGenerateProgression25.request";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [InitializeOnLoadMethod]
    private static void QueueRequestedGeneration()
    {
        string requestPath = GetProjectPath(RequestRelativePath);
        if (File.Exists(requestPath))
        {
            EditorApplication.delayCall += GenerateRequestedBatch;
        }
    }

    public static void GenerateBatch()
    {
        GenerateBatchFromIndex(0);
    }

    private static void GenerateBatchFromIndex(int startLevelIndex)
    {
        LevelProfile[] profiles = CreateProfiles();
        startLevelIndex = Mathf.Clamp(startLevelIndex, 0, profiles.Length - 1);
        EnsureFolder(AssetOutputFolder);
        EnsureFolder(JsonOutputFolder);
        PruneObsoleteLevelFiles(profiles);

        string failurePath = GetProjectPath($"{JsonOutputFolder}/GenerationFailure.txt");
        if (File.Exists(failurePath))
        {
            File.Delete(failurePath);
        }

        List<string> reportLines = new List<string>
        {
            "Generated Progressive Batch 30",
            $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            "Difficulty checkpoints: levels 5, 10, 15, 20, 25, and 30.",
            "Coverage target: 100% of every active board cell.",
            string.Empty
        };
        AppendExistingReportLines(reportLines, profiles, startLevelIndex);

        try
        {
            for (int levelIndex = startLevelIndex; levelIndex < profiles.Length; levelIndex++)
            {
                LevelProfile profile = profiles[levelIndex];
                BatchCandidate bestCandidate = null;
                int minimumCandidateCount = profile.isSpike ? 4 : 2;
                int maximumCandidateCount = profile.width >= 30
                    ? 24
                    : (profile.width >= 23 ? 12 : (profile.isSpike ? 8 : 5));

                for (int candidateIndex = 0; candidateIndex < maximumCandidateCount; candidateIndex++)
                {
                    float progress = (levelIndex + candidateIndex / (float)maximumCandidateCount) / profiles.Length;
                    if (EditorUtility.DisplayCancelableProgressBar(
                        "Generating Progressive Levels",
                        $"Level {levelIndex + 1}/{profiles.Length}: {profile.name} (candidate {candidateIndex + 1}/{maximumCandidateCount})",
                        progress))
                    {
                        throw new OperationCanceledException("Progressive level generation was canceled.");
                    }

                    int seed = profile.seed + candidateIndex * 7919;
                    RuntimeArrowLevelDocument document = GenerateCandidate(profile, seed, out string generatorStatus);
                    LevelMetrics metrics = AnalyzeDocument(document);
                    if (!metrics.solvable)
                    {
                        Debug.LogWarning($"Rejected {profile.name} seed {seed}: {metrics.error}");
                        continue;
                    }

                    double score = ScoreCandidate(profile, metrics);
                    bool hasBetterCoverage = bestCandidate == null
                        || metrics.occupiedCellCount > bestCandidate.metrics.occupiedCellCount;
                    bool hasEqualCoverageAndBetterScore = bestCandidate != null
                        && metrics.occupiedCellCount == bestCandidate.metrics.occupiedCellCount
                        && score > bestCandidate.score;
                    if (hasBetterCoverage || hasEqualCoverageAndBetterScore)
                    {
                        bestCandidate = new BatchCandidate(document, metrics, seed, score, generatorStatus);
                    }

                    bool hasFullCoverage = bestCandidate.metrics.occupiedCellCount == bestCandidate.metrics.zoneCellCount;
                    if (candidateIndex + 1 >= minimumCandidateCount && hasFullCoverage)
                    {
                        break;
                    }
                }

                if (bestCandidate == null)
                {
                    throw new InvalidOperationException($"No solvable candidate was generated for {profile.name}.");
                }

                // A batch level is accepted only when every playable cell belongs to an arrow.
                if (bestCandidate.metrics.occupiedCellCount != bestCandidate.metrics.zoneCellCount)
                {
                    int emptyCellCount = bestCandidate.metrics.zoneCellCount - bestCandidate.metrics.occupiedCellCount;
                    throw new InvalidOperationException(
                        $"{profile.name} still has {emptyCellCount} empty active cell(s) after {maximumCandidateCount} candidates.");
                }

                WriteLevel(profile, bestCandidate);
                reportLines.Add(BuildReportLine(levelIndex + 1, profile, bestCandidate));
            }

            File.WriteAllLines(GetProjectPath($"{JsonOutputFolder}/GenerationReport.txt"), reportLines);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Generated {profiles.Length} progressive levels in {AssetOutputFolder} and {JsonOutputFolder}.");
        }
        catch (OperationCanceledException exception)
        {
            Debug.LogWarning(exception.Message);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            File.WriteAllText(
                GetProjectPath($"{JsonOutputFolder}/GenerationFailure.txt"),
                $"{DateTime.Now:O}\n{exception}");
            AssetDatabase.Refresh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void GenerateRequestedBatch()
    {
        string requestPath = GetProjectPath(RequestRelativePath);
        string request = string.Empty;
        try
        {
            request = File.ReadAllText(requestPath);
            File.Delete(requestPath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not remove the generation request marker: {exception.Message}");
        }

        int startLevelIndex = ParseRequestedStartIndex(request, CreateProfiles().Length);
        GenerateBatchFromIndex(startLevelIndex);
    }

    private static int ParseRequestedStartIndex(string request, int levelCount)
    {
        string[] lines = request.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < lines.Length; i++)
        {
            const string Prefix = "start=";
            string line = lines[i].Trim();
            if (line.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(line.Substring(Prefix.Length).Trim(), out int levelNumber))
            {
                return Mathf.Clamp(levelNumber - 1, 0, levelCount - 1);
            }
        }

        return 0;
    }

    private static RuntimeArrowLevelDocument GenerateCandidate(LevelProfile profile, int seed, out string status)
    {
        GameObject host = new GameObject("ProgressiveLevelGeneratorHost")
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        try
        {
            RuntimeArrowLevelEditorApp app = host.AddComponent<RuntimeArrowLevelEditorApp>();
            SetField(app, "width", profile.width);
            SetField(app, "height", profile.height);
            SetField(app, "widthText", profile.width.ToString());
            SetField(app, "heightText", profile.height.ToString());
            SetField(app, "generatorMinLengthText", profile.minLength.ToString());
            SetField(app, "generatorMaxLengthText", profile.maxLength.ToString());
            SetField(app, "generatorFillPercentText", profile.fillPercent.ToString());
            SetField(app, "generatorAttemptText", profile.attempts.ToString());
            SetField(app, "generatorSeedText", seed.ToString());
            SetField(app, "generatorMinimumLengthWeight", profile.minimumLengthWeight);
            SetField(app, "generatorRandomSeed", false);
            SetField(app, "generatorClearExisting", true);
            SetField(app, "generatorAlgorithmModeIndex", 1);
            SetField(app, "generatorColorModeIndex", 0);
            SetField(app, "selectedThemeIndex", 1);

            HashSet<Vector2Int> activeCells = GetField<HashSet<Vector2Int>>(app, "activeCells");
            activeCells.Clear();
            List<Vector2Int> mask = CreateMask(profile);
            for (int i = 0; i < mask.Count; i++)
            {
                activeCells.Add(mask[i]);
            }

            bool usesCustomShape = profile.shape != BoardShape.Rectangle;
            SetField(app, "customShapeEnabled", usesCustomShape);
            SetField(app, "generatorUseCurrentShape", usesCustomShape);

            Invoke(app, "GenerateProceduralLevel");
            status = GetField<string>(app, "currentStatusMessage") ?? string.Empty;
            RuntimeArrowLevelDocument document = (RuntimeArrowLevelDocument)Invoke(app, "BuildDocument");
            if (document == null || document.arrows == null || document.arrows.Count == 0)
            {
                throw new InvalidOperationException($"Generator produced no arrows for {profile.name}. Status: {status}");
            }

            NormalizeDocumentColors(document);
            return document;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static void NormalizeDocumentColors(RuntimeArrowLevelDocument document)
    {
        SerializableColor black = SerializableColor.FromColor(Color.black);
        for (int i = 0; i < document.arrows.Count; i++)
        {
            document.arrows[i].color = black;
            document.arrows[i].id = $"Arrow {i + 1}";
        }
    }

    private static double ScoreCandidate(LevelProfile profile, LevelMetrics metrics)
    {
        double fillScore = metrics.fillRatio * 100000d;
        double narrowWaveScore = -metrics.overTwoWaveCount * 1600d - Math.Max(0, metrics.maxWaveWidth - 2) * 700d;
        double depthScore = metrics.dependencyDepth * 150d;
        double complexityScore = metrics.averageTurns * 90d + metrics.arrowCount * (profile.isSpike ? 24d : 10d);
        return fillScore + narrowWaveScore + depthScore + complexityScore;
    }

    private static LevelMetrics AnalyzeDocument(RuntimeArrowLevelDocument document)
    {
        LevelMetrics metrics = new LevelMetrics();
        if (document == null || document.arrows == null || document.arrows.Count == 0)
        {
            metrics.error = "Document has no arrows.";
            return metrics;
        }

        HashSet<Vector2Int> zone = BuildZone(document);
        Dictionary<Vector2Int, int> occupied = new Dictionary<Vector2Int, int>();
        List<HashSet<Vector2Int>> arrowCells = new List<HashSet<Vector2Int>>();
        List<Vector2Int> heads = new List<Vector2Int>();
        List<Vector2Int> exits = new List<Vector2Int>();
        int totalTurns = 0;
        int longestArrow = 0;

        for (int arrowIndex = 0; arrowIndex < document.arrows.Count; arrowIndex++)
        {
            RuntimeArrowJson arrow = document.arrows[arrowIndex];
            if (arrow.points == null || arrow.points.Count < 2)
            {
                metrics.error = $"Arrow {arrowIndex + 1} has fewer than two points.";
                return metrics;
            }

            List<Vector2Int> points = new List<Vector2Int>();
            for (int pointIndex = 0; pointIndex < arrow.points.Count; pointIndex++)
            {
                points.Add(arrow.points[pointIndex].ToVector2Int());
            }

            HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
            if (!TryExpandPath(points, zone, cells, out string pathError))
            {
                metrics.error = $"Arrow {arrowIndex + 1}: {pathError}";
                return metrics;
            }

            foreach (Vector2Int cell in cells)
            {
                if (occupied.ContainsKey(cell))
                {
                    metrics.error = $"Arrow {arrowIndex + 1} overlaps another arrow at {cell.x},{cell.y}.";
                    return metrics;
                }

                occupied[cell] = arrowIndex;
            }

            Vector2Int head = points[points.Count - 1];
            Vector2Int previous = points[points.Count - 2];
            Vector2Int exitDelta = head - previous;
            Vector2Int exitDirection = new Vector2Int(Math.Sign(exitDelta.x), Math.Sign(exitDelta.y));
            if (exitDirection == Vector2Int.zero || (exitDirection.x != 0 && exitDirection.y != 0))
            {
                metrics.error = $"Arrow {arrowIndex + 1} has an invalid exit direction.";
                return metrics;
            }

            heads.Add(head);
            exits.Add(exitDirection);
            arrowCells.Add(cells);
            longestArrow = Math.Max(longestArrow, cells.Count);
            totalTurns += CountTurns(points);
        }

        HashSet<int> removed = new HashSet<int>();
        List<int> wave = new List<int>();
        while (removed.Count < document.arrows.Count)
        {
            wave.Clear();
            for (int arrowIndex = 0; arrowIndex < document.arrows.Count; arrowIndex++)
            {
                if (!removed.Contains(arrowIndex)
                    && CanEscape(arrowIndex, heads, exits, occupied, removed, zone))
                {
                    wave.Add(arrowIndex);
                }
            }

            if (wave.Count == 0)
            {
                metrics.error = $"Deadlock after clearing {removed.Count}/{document.arrows.Count} arrows.";
                return metrics;
            }

            if (metrics.dependencyDepth == 0)
            {
                metrics.initialPlayableCount = wave.Count;
            }

            metrics.dependencyDepth++;
            metrics.maxWaveWidth = Math.Max(metrics.maxWaveWidth, wave.Count);
            if (wave.Count > 2)
            {
                metrics.overTwoWaveCount++;
            }

            for (int waveIndex = 0; waveIndex < wave.Count; waveIndex++)
            {
                int escapedIndex = wave[waveIndex];
                removed.Add(escapedIndex);
                foreach (Vector2Int cell in arrowCells[escapedIndex])
                {
                    occupied.Remove(cell);
                }
            }
        }

        metrics.solvable = true;
        metrics.arrowCount = document.arrows.Count;
        metrics.occupiedCellCount = 0;
        for (int i = 0; i < arrowCells.Count; i++)
        {
            metrics.occupiedCellCount += arrowCells[i].Count;
        }

        metrics.zoneCellCount = zone.Count;
        metrics.fillRatio = zone.Count > 0 ? metrics.occupiedCellCount / (float)zone.Count : 0f;
        metrics.averageTurns = totalTurns / (float)document.arrows.Count;
        metrics.longestArrow = longestArrow;
        return metrics;
    }

    private static bool CanEscape(
        int arrowIndex,
        List<Vector2Int> heads,
        List<Vector2Int> exits,
        Dictionary<Vector2Int, int> occupied,
        HashSet<int> removed,
        HashSet<Vector2Int> zone)
    {
        Vector2Int check = heads[arrowIndex] + exits[arrowIndex];
        while (zone.Contains(check))
        {
            if (occupied.TryGetValue(check, out int blockerIndex) && !removed.Contains(blockerIndex))
            {
                return false;
            }

            check += exits[arrowIndex];
        }

        return true;
    }

    private static bool TryExpandPath(
        List<Vector2Int> points,
        HashSet<Vector2Int> zone,
        HashSet<Vector2Int> cells,
        out string error)
    {
        cells.Clear();
        error = string.Empty;

        for (int segmentIndex = 0; segmentIndex < points.Count - 1; segmentIndex++)
        {
            Vector2Int start = points[segmentIndex];
            Vector2Int end = points[segmentIndex + 1];
            Vector2Int delta = end - start;
            if (delta == Vector2Int.zero || (delta.x != 0 && delta.y != 0))
            {
                error = "Path contains a zero-length or diagonal segment.";
                return false;
            }

            Vector2Int step = new Vector2Int(Math.Sign(delta.x), Math.Sign(delta.y));
            int length = Math.Max(Math.Abs(delta.x), Math.Abs(delta.y));
            int firstDistance = segmentIndex == 0 ? 0 : 1;

            for (int distance = firstDistance; distance <= length; distance++)
            {
                Vector2Int cell = start + step * distance;
                if (!zone.Contains(cell))
                {
                    error = $"Path leaves the active board at {cell.x},{cell.y}.";
                    return false;
                }

                if (!cells.Add(cell))
                {
                    error = $"Path overlaps itself at {cell.x},{cell.y}.";
                    return false;
                }
            }
        }

        return true;
    }

    private static int CountTurns(List<Vector2Int> points)
    {
        if (points.Count < 3)
        {
            return 0;
        }

        int turns = 0;
        Vector2Int previousDirection = CardinalDirection(points[1] - points[0]);
        for (int i = 2; i < points.Count; i++)
        {
            Vector2Int direction = CardinalDirection(points[i] - points[i - 1]);
            if (direction != previousDirection)
            {
                turns++;
                previousDirection = direction;
            }
        }

        return turns;
    }

    private static Vector2Int CardinalDirection(Vector2Int delta)
    {
        return new Vector2Int(Math.Sign(delta.x), Math.Sign(delta.y));
    }

    private static HashSet<Vector2Int> BuildZone(RuntimeArrowLevelDocument document)
    {
        HashSet<Vector2Int> zone = new HashSet<Vector2Int>();
        if (document.UsesCustomShape && document.activeCells != null)
        {
            for (int i = 0; i < document.activeCells.Count; i++)
            {
                zone.Add(document.activeCells[i].ToVector2Int());
            }

            return zone;
        }

        for (int y = 0; y < document.height; y++)
        {
            for (int x = 0; x < document.width; x++)
            {
                zone.Add(new Vector2Int(x, y));
            }
        }

        return zone;
    }

    private static void WriteLevel(LevelProfile profile, BatchCandidate candidate)
    {
        string jsonAssetPath = $"{JsonOutputFolder}/{profile.name}.json";
        File.WriteAllText(GetProjectPath(jsonAssetPath), JsonUtility.ToJson(candidate.document, true));

        string levelAssetPath = $"{AssetOutputFolder}/{profile.name}.asset";
        PathArrowLevelData levelAsset = AssetDatabase.LoadAssetAtPath<PathArrowLevelData>(levelAssetPath);
        if (levelAsset == null)
        {
            levelAsset = ScriptableObject.CreateInstance<PathArrowLevelData>();
            AssetDatabase.CreateAsset(levelAsset, levelAssetPath);
        }

        WriteDocumentToAsset(candidate.document, levelAsset);
    }

    private static void WriteDocumentToAsset(RuntimeArrowLevelDocument document, PathArrowLevelData asset)
    {
        SerializedObject serializedAsset = new SerializedObject(asset);
        serializedAsset.FindProperty("width").intValue = Math.Max(1, document.width);
        serializedAsset.FindProperty("height").intValue = Math.Max(1, document.height);
        serializedAsset.FindProperty("hasCustomShape").boolValue = document.UsesCustomShape;

        SerializedProperty activeCellsProperty = serializedAsset.FindProperty("activeCells");
        activeCellsProperty.arraySize = document.activeCells == null ? 0 : document.activeCells.Count;
        for (int i = 0; i < activeCellsProperty.arraySize; i++)
        {
            activeCellsProperty.GetArrayElementAtIndex(i).vector2IntValue = document.activeCells[i].ToVector2Int();
        }

        SerializedProperty arrowsProperty = serializedAsset.FindProperty("arrows");
        arrowsProperty.arraySize = document.arrows.Count;
        for (int arrowIndex = 0; arrowIndex < document.arrows.Count; arrowIndex++)
        {
            RuntimeArrowJson sourceArrow = document.arrows[arrowIndex];
            SerializedProperty targetArrow = arrowsProperty.GetArrayElementAtIndex(arrowIndex);
            targetArrow.FindPropertyRelative("id").stringValue = sourceArrow.id;
            targetArrow.FindPropertyRelative("color").colorValue = sourceArrow.color.ToColor();

            SerializedProperty pointsProperty = targetArrow.FindPropertyRelative("points");
            pointsProperty.arraySize = sourceArrow.points.Count;
            for (int pointIndex = 0; pointIndex < sourceArrow.points.Count; pointIndex++)
            {
                pointsProperty.GetArrayElementAtIndex(pointIndex).vector2IntValue = sourceArrow.points[pointIndex].ToVector2Int();
            }
        }

        serializedAsset.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    private static string BuildReportLine(int levelNumber, LevelProfile profile, BatchCandidate candidate)
    {
        return BuildReportLine(levelNumber, profile, candidate.metrics, candidate.seed.ToString());
    }

    private static string BuildReportLine(
        int levelNumber,
        LevelProfile profile,
        LevelMetrics metrics,
        string seedLabel)
    {
        string spike = profile.isSpike ? " [SPIKE]" : string.Empty;
        return $"{levelNumber:00}. {profile.name}{spike}: {profile.width}x{profile.height} {profile.shape}, "
            + $"{metrics.arrowCount} arrows, {metrics.occupiedCellCount}/{metrics.zoneCellCount} cells ({metrics.fillRatio:P1}), "
            + $"waves {metrics.dependencyDepth}, widest {metrics.maxWaveWidth}, >2 waves {metrics.overTwoWaveCount}, "
            + $"initial {metrics.initialPlayableCount}, avg turns {metrics.averageTurns:F2}, longest {metrics.longestArrow}, seed {seedLabel}.";
    }

    private static List<Vector2Int> CreateMask(LevelProfile profile)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        if (profile.shape == BoardShape.Rectangle)
        {
            return cells;
        }

        for (int y = 0; y < profile.height; y++)
        {
            for (int x = 0; x < profile.width; x++)
            {
                if (IsShapeCellActive(profile.shape, x, y, profile.width, profile.height))
                {
                    cells.Add(new Vector2Int(x, y));
                }
            }
        }

        return cells;
    }

    private static bool IsShapeCellActive(BoardShape shape, int x, int y, int width, int height)
    {
        float centerX = (width - 1) * 0.5f;
        float centerY = (height - 1) * 0.5f;

        switch (shape)
        {
            case BoardShape.Beveled:
            {
                int bevel = Math.Max(1, Math.Min(width, height) / 6);
                int left = Math.Min(x, width - 1 - x);
                int vertical = Math.Min(y, height - 1 - y);
                return left + vertical >= bevel;
            }
            case BoardShape.Cross:
            {
                int halfVerticalArm = Math.Max(2, width / 5);
                int halfHorizontalArm = Math.Max(2, height / 5);
                return Math.Abs(x - centerX) <= halfVerticalArm || Math.Abs(y - centerY) <= halfHorizontalArm;
            }
            case BoardShape.Heart:
            {
                float nx = (x - centerX) / Math.Max(1f, width * 0.48f);
                float ny = (y - height * 0.47f) / Math.Max(1f, height * 0.48f);
                float baseTerm = nx * nx + ny * ny - 1f;
                return baseTerm * baseTerm * baseTerm - nx * nx * ny * ny * ny <= 0.08f;
            }
            case BoardShape.Hexagon:
            {
                int bevel = Math.Max(1, Math.Min(width, height) / 5);
                int edgeDistance = Math.Min(y, height - 1 - y);
                int inset = Math.Max(0, bevel - edgeDistance);
                return x >= inset && x < width - inset;
            }
            case BoardShape.Hourglass:
            {
                float distanceFromCenter = Math.Abs(y - centerY) / Math.Max(1f, centerY);
                int maximumInset = Math.Max(1, width / 4);
                int inset = Mathf.RoundToInt(maximumInset * (1f - distanceFromCenter));
                return x >= inset && x < width - inset;
            }
            case BoardShape.Shield:
            {
                float normalizedY = y / Math.Max(1f, height - 1f);
                if (normalizedY >= 0.48f)
                {
                    return x >= 1 && x < width - 1;
                }

                float halfWidth = Mathf.Lerp(0.5f, width * 0.5f - 1f, normalizedY / 0.48f);
                return Math.Abs(x - centerX) <= halfWidth;
            }
            case BoardShape.Clover:
            {
                float radius = Math.Min(width, height) * 0.28f;
                float offset = radius * 0.72f;
                return InsideCircle(x, y, centerX - offset, centerY, radius)
                    || InsideCircle(x, y, centerX + offset, centerY, radius)
                    || InsideCircle(x, y, centerX, centerY - offset, radius)
                    || InsideCircle(x, y, centerX, centerY + offset, radius);
            }
            default:
                return true;
        }
    }

    private static bool InsideCircle(float x, float y, float centerX, float centerY, float radius)
    {
        float dx = x - centerX;
        float dy = y - centerY;
        return dx * dx + dy * dy <= radius * radius;
    }

    private static LevelProfile[] CreateProfiles()
    {
        return new[]
        {
            P(1, 6, 6, BoardShape.Rectangle, 100, 2, 4, 0.70f, 140),
            P(2, 7, 7, BoardShape.Rectangle, 100, 2, 5, 0.66f, 150),
            P(3, 8, 8, BoardShape.Beveled, 100, 2, 5, 0.62f, 160),
            P(4, 9, 9, BoardShape.Rectangle, 100, 2, 6, 0.58f, 170),
            P(5, 11, 10, BoardShape.Rectangle, 100, 2, 7, 0.72f, 240, true),

            P(6, 11, 11, BoardShape.Rectangle, 100, 2, 7, 0.56f, 180),
            P(7, 13, 12, BoardShape.Cross, 100, 2, 8, 0.53f, 190),
            P(8, 12, 12, BoardShape.Rectangle, 100, 2, 8, 0.50f, 200),
            P(9, 15, 14, BoardShape.Heart, 100, 2, 9, 0.47f, 220),
            P(10, 16, 14, BoardShape.Hexagon, 100, 2, 10, 0.64f, 280, true),

            P(11, 15, 15, BoardShape.Rectangle, 100, 2, 10, 0.45f, 220),
            P(12, 17, 16, BoardShape.Hourglass, 100, 2, 11, 0.43f, 240),
            P(13, 16, 16, BoardShape.Rectangle, 100, 2, 11, 0.40f, 250),
            P(14, 18, 17, BoardShape.Shield, 100, 2, 12, 0.38f, 270),
            P(15, 20, 18, BoardShape.Rectangle, 100, 2, 13, 0.55f, 340, true),

            P(16, 19, 19, BoardShape.Rectangle, 100, 2, 13, 0.36f, 270),
            P(17, 21, 20, BoardShape.Beveled, 100, 2, 14, 0.34f, 290),
            P(18, 20, 20, BoardShape.Rectangle, 100, 2, 14, 0.33f, 300),
            P(19, 23, 22, BoardShape.Clover, 100, 2, 15, 0.32f, 320),
            P(20, 24, 22, BoardShape.Rectangle, 100, 2, 16, 0.48f, 400, true),

            P(21, 23, 23, BoardShape.Rectangle, 100, 2, 15, 0.31f, 320),
            P(22, 26, 24, BoardShape.Heart, 100, 2, 16, 0.30f, 350),
            P(23, 24, 24, BoardShape.Rectangle, 100, 2, 16, 0.28f, 370),
            P(24, 26, 25, BoardShape.Rectangle, 100, 2, 17, 0.34f, 420),
            P(25, 28, 26, BoardShape.Rectangle, 100, 2, 18, 0.46f, 500, true),

            P(26, 29, 27, BoardShape.Rectangle, 100, 2, 18, 0.29f, 480),
            P(27, 30, 28, BoardShape.Rectangle, 100, 2, 19, 0.27f, 500),
            P(28, 30, 30, BoardShape.Rectangle, 100, 2, 20, 0.26f, 520),
            P(29, 32, 30, BoardShape.Rectangle, 100, 2, 21, 0.25f, 560),
            P(30, 33, 30, BoardShape.Rectangle, 100, 2, 22, 0.42f, 650, true)
        };
    }

    private static void AppendExistingReportLines(
        List<string> reportLines,
        LevelProfile[] profiles,
        int startLevelIndex)
    {
        if (startLevelIndex <= 0)
        {
            return;
        }

        for (int levelIndex = 0; levelIndex < startLevelIndex; levelIndex++)
        {
            LevelProfile profile = profiles[levelIndex];
            string jsonPath = GetProjectPath($"{JsonOutputFolder}/{profile.name}.json");
            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException(
                    $"Cannot resume generation because level {levelIndex + 1} JSON is missing.",
                    jsonPath);
            }

            RuntimeArrowLevelDocument document = JsonUtility.FromJson<RuntimeArrowLevelDocument>(File.ReadAllText(jsonPath));
            LevelMetrics metrics = AnalyzeDocument(document);
            if (!metrics.solvable || metrics.occupiedCellCount != metrics.zoneCellCount)
            {
                throw new InvalidDataException(
                    $"Cannot resume generation because level {levelIndex + 1} is not solvable and fully occupied.");
            }

            reportLines.Add(BuildReportLine(levelIndex + 1, profile, metrics, "preserved"));
        }
    }

    private static void PruneObsoleteLevelFiles(LevelProfile[] profiles)
    {
        HashSet<string> expectedAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> expectedJsonPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < profiles.Length; i++)
        {
            expectedAssetPaths.Add($"{AssetOutputFolder}/{profiles[i].name}.asset");
            expectedJsonPaths.Add($"{JsonOutputFolder}/{profiles[i].name}.json");
        }

        string[] assetGuids = AssetDatabase.FindAssets("t:PathArrowLevelData", new[] { AssetOutputFolder });
        for (int i = 0; i < assetGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
            if (!expectedAssetPaths.Contains(assetPath))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        string jsonFolderPath = GetProjectPath(JsonOutputFolder);
        string[] jsonFiles = Directory.GetFiles(jsonFolderPath, "Level_*.json", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < jsonFiles.Length; i++)
        {
            string assetPath = $"{JsonOutputFolder}/{Path.GetFileName(jsonFiles[i])}";
            if (!expectedJsonPaths.Contains(assetPath))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }
    }

    private static LevelProfile P(
        int level,
        int width,
        int height,
        BoardShape shape,
        int fillPercent,
        int minLength,
        int maxLength,
        float minimumLengthWeight,
        int attempts,
        bool isSpike = false,
        int seedOffset = 0)
    {
        string suffix = shape == BoardShape.Rectangle ? "Rectangle" : shape.ToString();
        return new LevelProfile
        {
            name = $"Level_{level:00}_{width}x{height}_{suffix}",
            width = width,
            height = height,
            shape = shape,
            fillPercent = fillPercent,
            minLength = minLength,
            maxLength = maxLength,
            minimumLengthWeight = minimumLengthWeight,
            attempts = attempts,
            seed = 19001 + level * 1009 + seedOffset,
            isSpike = isSpike
        };
    }

    private static object Invoke(RuntimeArrowLevelEditorApp app, string methodName)
    {
        MethodInfo method = typeof(RuntimeArrowLevelEditorApp).GetMethod(methodName, PrivateInstance);
        if (method == null)
        {
            throw new MissingMethodException(typeof(RuntimeArrowLevelEditorApp).Name, methodName);
        }

        try
        {
            return method.Invoke(app, null);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            throw exception.InnerException;
        }
    }

    private static void SetField<T>(RuntimeArrowLevelEditorApp app, string fieldName, T value)
    {
        FieldInfo field = typeof(RuntimeArrowLevelEditorApp).GetField(fieldName, PrivateInstance);
        if (field == null)
        {
            throw new MissingFieldException(typeof(RuntimeArrowLevelEditorApp).Name, fieldName);
        }

        field.SetValue(app, value);
    }

    private static T GetField<T>(RuntimeArrowLevelEditorApp app, string fieldName)
    {
        FieldInfo field = typeof(RuntimeArrowLevelEditorApp).GetField(fieldName, PrivateInstance);
        if (field == null)
        {
            throw new MissingFieldException(typeof(RuntimeArrowLevelEditorApp).Name, fieldName);
        }

        return (T)field.GetValue(app);
    }

    private static string GetProjectPath(string assetOrRelativePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetOrRelativePath));
    }

    private static void EnsureFolder(string assetPath)
    {
        string[] parts = assetPath.Split('/');
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

    private enum BoardShape
    {
        Rectangle,
        Beveled,
        Cross,
        Heart,
        Hexagon,
        Hourglass,
        Shield,
        Clover
    }

    private sealed class LevelProfile
    {
        public string name;
        public int width;
        public int height;
        public BoardShape shape;
        public int fillPercent;
        public int minLength;
        public int maxLength;
        public float minimumLengthWeight;
        public int attempts;
        public int seed;
        public bool isSpike;
    }

    private sealed class BatchCandidate
    {
        public readonly RuntimeArrowLevelDocument document;
        public readonly LevelMetrics metrics;
        public readonly int seed;
        public readonly double score;
        public readonly string generatorStatus;

        public BatchCandidate(
            RuntimeArrowLevelDocument document,
            LevelMetrics metrics,
            int seed,
            double score,
            string generatorStatus)
        {
            this.document = document;
            this.metrics = metrics;
            this.seed = seed;
            this.score = score;
            this.generatorStatus = generatorStatus;
        }
    }

    private sealed class LevelMetrics
    {
        public bool solvable;
        public string error = string.Empty;
        public int arrowCount;
        public int occupiedCellCount;
        public int zoneCellCount;
        public float fillRatio;
        public int initialPlayableCount;
        public int dependencyDepth;
        public int maxWaveWidth;
        public int overTwoWaveCount;
        public float averageTurns;
        public int longestArrow;
    }
}
