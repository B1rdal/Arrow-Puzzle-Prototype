/*
Summary:
Campaign100BatchGenerator drives the runtime editor's current Complex Guided DX and
DX Flow algorithms to create a deterministic 200-level campaign. Easy, hard, and very
hard levels progress on separate curves in a repeating 4/1/4/1 cadence. Every result
must be solvable and cover every active board cell before it is written to JSON and a
matching PathArrowLevelData asset.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class Campaign100BatchGenerator
{
    // The request marker queues this long batch in an already-open Unity Editor.
    private const string AssetOutputFolder = "Assets/LevelsData/GeneratedCampaign100";
    private const string JsonOutputFolder = "Assets/LevelsData/GeneratedCampaign100Json";
    private const string RequestRelativePath = "Library/CodexGenerateCampaign100.request";
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

    public static void GenerateShapedHalfBatch()
    {
        GenerateBatchFromIndex(0, true);
    }

    private static void GenerateBatchFromIndex(
        int startLevelIndex,
        bool shapedOnly = false,
        int endLevelIndexExclusive = 200)
    {
        LevelProfile[] profiles = CreateProfiles();
        startLevelIndex = Mathf.Clamp(startLevelIndex, 0, profiles.Length - 1);
        endLevelIndexExclusive = Mathf.Clamp(
            endLevelIndexExclusive,
            startLevelIndex + 1,
            profiles.Length);
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
            $"Generated DX Campaign {profiles.Length}",
            $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            "Cadence: 4 Easy, 1 Hard, 4 Easy, 1 Very Hard.",
            "Easy uses Complex Guided DX. Hard and Very Hard use Complex Guided DX Flow.",
            "Each difficulty tier progresses on its own curve.",
            "Coverage target: 100% of every active board cell.",
            string.Empty
        };
        List<string> campaignOrder = new List<string>
        {
            $"{profiles.Length}-Level Campaign Order",
            "4 Easy -> 1 Hard -> 4 Easy -> 1 Very Hard",
            string.Empty
        };
        AppendExistingReportLines(reportLines, profiles, startLevelIndex);
        for (int preservedIndex = 0; preservedIndex < startLevelIndex; preservedIndex++)
        {
            campaignOrder.Add($"{preservedIndex + 1:000}: {profiles[preservedIndex].name}");
        }

        try
        {
            for (int levelIndex = startLevelIndex; levelIndex < endLevelIndexExclusive; levelIndex++)
            {
                LevelProfile profile = profiles[levelIndex];
                if (shapedOnly && profile.shape == BoardShape.Rectangle)
                {
                    continue;
                }

                BatchCandidate bestCandidate = null;
                int maximumCandidateCount = 0;
                int requestedWidth = profile.width;
                int requestedHeight = profile.height;
                BoardShape requestedShape = profile.shape;
                int maximumSizeRecovery = profile.campaignLevel > 150 ? 6 : 0;
                for (int sizeRecovery = 0; sizeRecovery <= maximumSizeRecovery; sizeRecovery++)
                {
                    profile.width = Math.Max(6, requestedWidth - sizeRecovery);
                    profile.height = Math.Max(6, requestedHeight - sizeRecovery);
                    profile.shape = requestedShape;
                    BoardShape[] shapeAttempts = profile.shape == BoardShape.Rectangle
                        ? new[] { profile.shape }
                        : CreateReliableShapeAttemptOrder(profile);
                    for (int shapeAttemptIndex = 0;
                        shapeAttemptIndex < shapeAttempts.Length;
                        shapeAttemptIndex++)
                    {
                        profile.shape = shapeAttempts[shapeAttemptIndex];
                        bestCandidate = GenerateBestCandidate(
                            profile,
                            levelIndex,
                            profiles.Length,
                            out maximumCandidateCount);
                        if (bestCandidate != null
                            && bestCandidate.metrics.occupiedCellCount
                            == bestCandidate.metrics.zoneCellCount)
                        {
                            break;
                        }
                    }

                    if (bestCandidate != null
                        && bestCandidate.metrics.occupiedCellCount
                        == bestCandidate.metrics.zoneCellCount)
                    {
                        break;
                    }
                }

                bestCandidate = TryTrimTinyCustomRemainder(profile, bestCandidate);
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
                campaignOrder.Add($"{levelIndex + 1:000}: {profile.name}");
            }

            if (shapedOnly || endLevelIndexExclusive < profiles.Length)
            {
                RebuildCompleteReports(profiles, out reportLines, out campaignOrder);
            }

            File.WriteAllLines(GetProjectPath($"{JsonOutputFolder}/GenerationReport.txt"), reportLines);
            File.WriteAllLines(GetProjectPath($"{JsonOutputFolder}/CampaignOrder.txt"), campaignOrder);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Generated {profiles.Length} campaign levels in {AssetOutputFolder} and {JsonOutputFolder}.");
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

    private static BatchCandidate GenerateBestCandidate(
        LevelProfile profile,
        int levelIndex,
        int levelCount,
        out int maximumCandidateCount)
    {
        BatchCandidate bestCandidate = null;
        int minimumCandidateCount = profile.isVeryHard ? 6 : (profile.isSpike ? 4 : 2);
        maximumCandidateCount = profile.isVeryHard ? 10 : (profile.isSpike ? 8 : 6);
        if (profile.campaignLevel <= 60)
        {
            minimumCandidateCount = profile.isVeryHard ? 10 : (profile.isSpike ? 8 : 4);
            maximumCandidateCount = profile.isVeryHard ? 14 : (profile.isSpike ? 14 : 8);
        }
        else if (profile.campaignLevel <= 80)
        {
            minimumCandidateCount = profile.isVeryHard ? 10 : (profile.isSpike ? 8 : 3);
            maximumCandidateCount = profile.isVeryHard ? 12 : (profile.isSpike ? 12 : 10);
        }
        else
        {
            minimumCandidateCount = profile.isVeryHard ? 10 : (profile.isSpike ? 8 : 3);
            maximumCandidateCount = profile.isVeryHard ? 14 : (profile.isSpike ? 14 : 12);
        }
        if (profile.campaignLevel == 11 || profile.campaignLevel == 12)
        {
            minimumCandidateCount = 8;
            maximumCandidateCount = 16;
        }
        if (profile.campaignLevel == 79)
        {
            minimumCandidateCount = 3;
            maximumCandidateCount = 14;
        }
        if (profile.isSpike && profile.campaignLevel <= 60)
        {
            minimumCandidateCount = profile.isVeryHard ? 10 : 8;
            maximumCandidateCount = 14;
        }
        else if (profile.isSpike && profile.campaignLevel <= 80)
        {
            minimumCandidateCount = profile.isVeryHard ? 10 : 8;
            maximumCandidateCount = 12;
        }
        else if (profile.isSpike)
        {
            minimumCandidateCount = profile.isVeryHard ? 10 : 8;
            maximumCandidateCount = 14;
        }

        if (profile.shape != BoardShape.Rectangle)
        {
            minimumCandidateCount = profile.campaignLevel <= 200
                ? (profile.isSpike ? 8 : 3)
                : 1;
            maximumCandidateCount = profile.campaignLevel <= 200
                ? (profile.isVeryHard ? 14 : (profile.isSpike ? 12 : 4))
                : (profile.isVeryHard ? 5 : (profile.isSpike ? 4 : 3));
            if (profile.campaignLevel == 12)
            {
                minimumCandidateCount = 8;
                maximumCandidateCount = 16;
            }
        }

        // Boards after level 150 are substantially larger. Keep their search
        // selective so equivalent candidates do not multiply batch time, while
        // retaining extra passes for the Hard/Very Hard opening-quality gate.
        if (profile.campaignLevel > 150)
        {
            minimumCandidateCount = 3;
            maximumCandidateCount = profile.isSpike ? 5 : 4;
        }

        for (int candidateIndex = 0; candidateIndex < maximumCandidateCount; candidateIndex++)
        {
            float progress = (levelIndex + candidateIndex / (float)maximumCandidateCount) / levelCount;
            if (EditorUtility.DisplayCancelableProgressBar(
                "Generating DX Campaign",
                $"Level {levelIndex + 1}/{levelCount}: {profile.name} {profile.shape} "
                    + $"(candidate {candidateIndex + 1}/{maximumCandidateCount})",
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
            bool requiresQualityGate = profile.campaignLevel <= 20
                || profile.isSpike;
            bool meetsOpeningQuality = !requiresQualityGate
                || MeetsOpeningQuality(profile, bestCandidate.metrics);
            if (candidateIndex + 1 >= minimumCandidateCount && hasFullCoverage && meetsOpeningQuality)
            {
                break;
            }
        }

        return bestCandidate;
    }

    private static BatchCandidate TryTrimTinyCustomRemainder(
        LevelProfile profile,
        BatchCandidate candidate)
    {
        if (candidate == null
            || profile.shape == BoardShape.Rectangle
            || candidate.document.activeCells == null)
        {
            return candidate;
        }

        int remainder = candidate.metrics.zoneCellCount - candidate.metrics.occupiedCellCount;
        int maximumTrim = Mathf.Clamp(
            Mathf.CeilToInt(candidate.metrics.zoneCellCount * 0.02f),
            4,
            8);
        if (remainder <= 0 || remainder > maximumTrim)
        {
            return candidate;
        }

        List<Vector2Int> emptyCells = GetEmptyDocumentCells(candidate.document);
        if (emptyCells.Count != remainder)
        {
            return candidate;
        }

        List<IntPoint> originalActiveCells = new List<IntPoint>(candidate.document.activeCells);
        HashSet<Vector2Int> trimmedCells = new HashSet<Vector2Int>(emptyCells);
        candidate.document.activeCells.RemoveAll(point => trimmedCells.Contains(point.ToVector2Int()));

        LevelMetrics repairedMetrics = AnalyzeDocument(candidate.document);
        if (repairedMetrics.solvable
            && repairedMetrics.occupiedCellCount == repairedMetrics.zoneCellCount)
        {
            return new BatchCandidate(
                candidate.document,
                repairedMetrics,
                candidate.seed,
                ScoreCandidate(profile, repairedMetrics),
                $"{candidate.generatorStatus}; removed {remainder} tiny uncovered custom-mask cell(s)");
        }

        candidate.document.activeCells = originalActiveCells;
        return candidate;
    }

    private static bool MeetsOpeningQuality(LevelProfile profile, LevelMetrics metrics)
    {
        if ((profile.campaignLevel == 11 || profile.campaignLevel == 12)
            && (metrics.topBlockedByBottomCount == 0 || metrics.bottomBlockedByTopCount == 0))
        {
            return false;
        }

        if (profile.isSpike)
        {
            int minimumRegionTransitions = Math.Max(4, metrics.dependencyDepth / 4);
            bool hasHorizontalInteraction =
                metrics.leftBlockedByRightCount > 0 && metrics.rightBlockedByLeftCount > 0;
            bool hasSpatialReversal =
                metrics.horizontalBacktrackCount + metrics.verticalBacktrackCount >= 2;
            if (!hasHorizontalInteraction
                || !hasSpatialReversal
                || metrics.regionTransitionCount < minimumRegionTransitions
                || metrics.longestRegionStreak > (profile.isVeryHard ? 5 : 4))
            {
                return false;
            }
        }

        int maximumInitialChoices = profile.isVeryHard || profile.isSpike ? 2 : 3;
        int minimumDepth = profile.isVeryHard
            ? profile.height
            : (profile.isSpike ? profile.height - 1 : Math.Max(5, profile.height - 3));
        return metrics.initialPlayableCount <= maximumInitialChoices
            && metrics.dependencyDepth >= minimumDepth;
    }

    private static BoardShape[] CreateShapeAttemptOrder(LevelProfile profile)
    {
        BoardShape requestedShape = profile.shape;
        List<BoardShape> order = new List<BoardShape> { requestedShape };
        BoardShape[] fallbacks =
        {
            BoardShape.Beveled,
            BoardShape.Hexagon,
            BoardShape.Hourglass,
            BoardShape.Cross,
            BoardShape.Heart,
            BoardShape.Shield,
            BoardShape.Clover,
            BoardShape.Diamond,
            BoardShape.Oval,
            BoardShape.Staircase,
            BoardShape.LShape,
            BoardShape.Arrowhead
        };
        int fallbackOffset = Variant(profile, 307, fallbacks.Length);
        for (int i = 0; i < fallbacks.Length; i++)
        {
            BoardShape fallback = fallbacks[(i + fallbackOffset) % fallbacks.Length];
            if (!order.Contains(fallback))
            {
                order.Add(fallback);
            }
        }

        return order.ToArray();
    }

    private static BoardShape[] CreateReliableShapeAttemptOrder(LevelProfile profile)
    {
        BoardShape requestedShape = profile.shape;
        List<BoardShape> order = new List<BoardShape> { requestedShape };
        BoardShape[] reliableFallbacks =
        {
            BoardShape.LShape,
            BoardShape.TwinBlocks,
            BoardShape.Staircase,
            BoardShape.Cross,
            BoardShape.Dumbbell
        };
        int fallbackOffset = Variant(profile, 331, reliableFallbacks.Length);
        for (int i = 0; i < reliableFallbacks.Length; i++)
        {
            BoardShape fallback =
                reliableFallbacks[(i + fallbackOffset) % reliableFallbacks.Length];
            if (!order.Contains(fallback))
            {
                order.Add(fallback);
            }
        }

        return order.ToArray();
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
            SetField(app, "generatorComplexityPercent", profile.complexityPercent);
            SetField(app, "generatorAutoLength", true);
            SetField(app, "generatorRandomSeed", false);
            SetField(app, "generatorClearExisting", true);
            SetField(app, "generatorAlgorithmModeIndex", profile.algorithmModeIndex);
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

            // A high-complexity pass can occasionally leave a tiny isolated
            // remainder after structure wins over density. Preserve the board,
            // then let regular DX add only the missing tail cells.
            LevelMetrics initialMetrics = AnalyzeDocument(document);
            if (initialMetrics.solvable
                && initialMetrics.occupiedCellCount < initialMetrics.zoneCellCount)
            {
                SetField(app, "generatorAlgorithmModeIndex", 4);
                SetField(app, "generatorClearExisting", false);
                SetField(app, "generatorComplexityPercent", Math.Max(60, profile.complexityPercent - 12));
                for (int repairPass = 0; repairPass < 2; repairPass++)
                {
                    SetField(app, "generatorSeedText", (seed + 50021 + repairPass * 7919).ToString());
                    Invoke(app, "GenerateProceduralLevel");
                    document = (RuntimeArrowLevelDocument)Invoke(app, "BuildDocument");
                    LevelMetrics repairedMetrics = AnalyzeDocument(document);
                    if (repairedMetrics.solvable
                        && repairedMetrics.occupiedCellCount == repairedMetrics.zoneCellCount)
                    {
                        status += $" DX coverage repair pass {repairPass + 1}.";
                        break;
                    }
                }
            }

            LevelMetrics postRepairMetrics = AnalyzeDocument(document);
            if (postRepairMetrics.solvable
                && postRepairMetrics.occupiedCellCount < postRepairMetrics.zoneCellCount
                && TryCompleteCoverageWithEndpointRepairs(document))
            {
                status += " Endpoint coverage repair.";
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
        double narrowWaveScore = -metrics.overTwoWaveCount * 1900d
            - Math.Max(0, metrics.maxWaveWidth - 2) * 950d
            - Math.Max(0, metrics.initialPlayableCount - 2) * 2200d;
        double depthScore = metrics.dependencyDepth * (profile.isVeryHard ? 310d : (profile.isSpike ? 260d : 210d));
        double complexityScore = metrics.averageTurns * (profile.isSpike ? 210d : 145d)
            + metrics.arrowCount * (profile.isVeryHard ? 38d : (profile.isSpike ? 30d : 16d))
            + metrics.longestArrow * (profile.isSpike ? 24d : 14d);
        double openingQualityScore = 0d;
        if (profile.campaignLevel <= 200)
        {
            double averageArrowLength = metrics.arrowCount > 0
                ? metrics.occupiedCellCount / (double)metrics.arrowCount
                : 0d;
            double desiredAverageLength = profile.isVeryHard ? 12d : (profile.isSpike ? 10d : 7d);
            double fragmentationPenalty = Math.Max(0d, desiredAverageLength - averageArrowLength) * 1350d;
            double longAnchorScore = metrics.longestArrow * (profile.isVeryHard ? 95d : (profile.isSpike ? 75d : 48d));
            double turnDensityScore = metrics.averageTurns * (profile.isVeryHard ? 560d : (profile.isSpike ? 430d : 260d));
            int desiredInitialChoices = profile.isVeryHard || profile.isSpike ? 1 : 2;
            double obviousOpeningPenalty = Math.Max(
                0,
                metrics.initialPlayableCount - desiredInitialChoices) * (profile.isVeryHard ? 9000d : 4200d);
            openingQualityScore = averageArrowLength * 720d
                + longAnchorScore
                + turnDensityScore
                - fragmentationPenalty
                - obviousOpeningPenalty;
        }

        double crossRegionScore = 0d;
        if (profile.campaignLevel == 11 || profile.campaignLevel == 12)
        {
            int balancedCrossings = Math.Min(
                metrics.topBlockedByBottomCount,
                metrics.bottomBlockedByTopCount);
            crossRegionScore = metrics.crossHalfDependencyCount * 5200d
                + balancedCrossings * 12500d;
            if (balancedCrossings > 0)
            {
                crossRegionScore += 40000d;
            }
        }

        double spatialFlowScore = 0d;
        if (profile.isSpike)
        {
            bool horizontalTwoWay =
                metrics.leftBlockedByRightCount > 0 && metrics.rightBlockedByLeftCount > 0;
            bool verticalTwoWay =
                metrics.topBlockedByBottomCount > 0 && metrics.bottomBlockedByTopCount > 0;
            double spikeMultiplier = profile.isVeryHard ? 1.25d : 1d;
            spatialFlowScore = (metrics.regionTransitionCount * 1800d
                + (metrics.horizontalBacktrackCount + metrics.verticalBacktrackCount) * 4200d
                - metrics.longestRegionStreak * 2600d
                - (metrics.horizontalSweepRatio + metrics.verticalSweepRatio) * 8500d
                + (horizontalTwoWay ? 18000d : 0d)
                + (verticalTwoWay ? 12000d : 0d)) * spikeMultiplier;
        }

        return fillScore
            + narrowWaveScore
            + depthScore
            + complexityScore
            + openingQualityScore
            + crossRegionScore
            + spatialFlowScore;
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

        AnalyzeCrossHalfDependencies(
            document.width,
            document.height,
            heads,
            exits,
            arrowCells,
            occupied,
            metrics);

        HashSet<int> removed = new HashSet<int>();
        List<int> wave = new List<int>();
        Vector2[] arrowCenters = BuildArrowCenters(arrowCells);
        int previousRegion = -1;
        int currentRegionStreak = 0;
        float firstWaveX = 0f;
        float firstWaveY = 0f;
        float previousWaveX = 0f;
        float previousWaveY = 0f;
        float horizontalTravel = 0f;
        float verticalTravel = 0f;
        int previousHorizontalDirection = 0;
        int previousVerticalDirection = 0;
        while (removed.Count < document.arrows.Count)
        {
            wave.Clear();
            for (int arrowIndex = 0; arrowIndex < document.arrows.Count; arrowIndex++)
            {
                if (!removed.Contains(arrowIndex)
                    && CanEscape(
                        arrowIndex,
                        heads,
                        exits,
                        occupied,
                        removed,
                        document.width,
                        document.height))
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

            float waveX = 0f;
            float waveY = 0f;
            for (int waveIndex = 0; waveIndex < wave.Count; waveIndex++)
            {
                waveX += arrowCenters[wave[waveIndex]].x;
                waveY += arrowCenters[wave[waveIndex]].y;
            }

            waveX /= wave.Count;
            waveY /= wave.Count;
            int region = (waveX >= (document.width - 1) * 0.5f ? 1 : 0)
                + (waveY >= (document.height - 1) * 0.5f ? 2 : 0);
            if (metrics.dependencyDepth == 0)
            {
                firstWaveX = waveX;
                firstWaveY = waveY;
                currentRegionStreak = 1;
            }
            else
            {
                if (region != previousRegion)
                {
                    metrics.regionTransitionCount++;
                    currentRegionStreak = 1;
                }
                else
                {
                    currentRegionStreak++;
                }

                float deltaX = waveX - previousWaveX;
                float deltaY = waveY - previousWaveY;
                horizontalTravel += Math.Abs(deltaX);
                verticalTravel += Math.Abs(deltaY);
                int horizontalDirection = Math.Sign(deltaX);
                int verticalDirection = Math.Sign(deltaY);
                if (horizontalDirection != 0
                    && previousHorizontalDirection != 0
                    && horizontalDirection != previousHorizontalDirection)
                {
                    metrics.horizontalBacktrackCount++;
                }

                if (verticalDirection != 0
                    && previousVerticalDirection != 0
                    && verticalDirection != previousVerticalDirection)
                {
                    metrics.verticalBacktrackCount++;
                }

                if (horizontalDirection != 0)
                {
                    previousHorizontalDirection = horizontalDirection;
                }

                if (verticalDirection != 0)
                {
                    previousVerticalDirection = verticalDirection;
                }
            }

            previousRegion = region;
            previousWaveX = waveX;
            previousWaveY = waveY;
            metrics.longestRegionStreak = Math.Max(
                metrics.longestRegionStreak,
                currentRegionStreak);
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
        metrics.horizontalSweepRatio = horizontalTravel > 0.001f
            ? Math.Abs(previousWaveX - firstWaveX) / horizontalTravel
            : 1f;
        metrics.verticalSweepRatio = verticalTravel > 0.001f
            ? Math.Abs(previousWaveY - firstWaveY) / verticalTravel
            : 1f;
        return metrics;
    }

    private static Vector2[] BuildArrowCenters(List<HashSet<Vector2Int>> arrowCells)
    {
        Vector2[] centers = new Vector2[arrowCells.Count];
        for (int arrowIndex = 0; arrowIndex < arrowCells.Count; arrowIndex++)
        {
            Vector2 total = Vector2.zero;
            foreach (Vector2Int cell in arrowCells[arrowIndex])
            {
                total += cell;
            }

            centers[arrowIndex] = total / Math.Max(1, arrowCells[arrowIndex].Count);
        }

        return centers;
    }

    private static void AnalyzeCrossHalfDependencies(
        int width,
        int height,
        List<Vector2Int> heads,
        List<Vector2Int> exits,
        List<HashSet<Vector2Int>> arrowCells,
        Dictionary<Vector2Int, int> occupied,
        LevelMetrics metrics)
    {
        float middleY = (height - 1) * 0.5f;
        bool[] isTopArrow = new bool[arrowCells.Count];
        bool[] isRightArrow = new bool[arrowCells.Count];
        float middleX = (width - 1) * 0.5f;
        for (int arrowIndex = 0; arrowIndex < arrowCells.Count; arrowIndex++)
        {
            float totalX = 0f;
            float totalY = 0f;
            foreach (Vector2Int cell in arrowCells[arrowIndex])
            {
                totalX += cell.x;
                totalY += cell.y;
            }

            isRightArrow[arrowIndex] =
                totalX / Math.Max(1, arrowCells[arrowIndex].Count) > middleX;
            isTopArrow[arrowIndex] = totalY / Math.Max(1, arrowCells[arrowIndex].Count) > middleY;
        }

        for (int arrowIndex = 0; arrowIndex < arrowCells.Count; arrowIndex++)
        {
            int blockerIndex = FindInitialBlocker(
                arrowIndex,
                heads,
                exits,
                occupied,
                width,
                height);
            if (blockerIndex < 0)
            {
                continue;
            }

            if (isTopArrow[arrowIndex] != isTopArrow[blockerIndex])
            {
                metrics.crossHalfDependencyCount++;
                if (isTopArrow[arrowIndex])
                {
                    metrics.topBlockedByBottomCount++;
                }
                else
                {
                    metrics.bottomBlockedByTopCount++;
                }
            }

            if (isRightArrow[arrowIndex] != isRightArrow[blockerIndex])
            {
                if (isRightArrow[arrowIndex])
                {
                    metrics.rightBlockedByLeftCount++;
                }
                else
                {
                    metrics.leftBlockedByRightCount++;
                }
            }
        }
    }

    private static int FindInitialBlocker(
        int arrowIndex,
        List<Vector2Int> heads,
        List<Vector2Int> exits,
        Dictionary<Vector2Int, int> occupied,
        int width,
        int height)
    {
        Vector2Int check = heads[arrowIndex] + exits[arrowIndex];
        while (check.x >= 0 && check.x < width && check.y >= 0 && check.y < height)
        {
            if (occupied.TryGetValue(check, out int blockerIndex))
            {
                return blockerIndex;
            }

            check += exits[arrowIndex];
        }

        return -1;
    }

    private static bool TryCompleteCoverageWithEndpointRepairs(RuntimeArrowLevelDocument document)
    {
        for (int pass = 0; pass < 8; pass++)
        {
            List<Vector2Int> emptyCells = GetEmptyDocumentCells(document);
            if (emptyCells.Count == 0)
            {
                return true;
            }

            bool changed = false;
            for (int emptyIndex = 0; emptyIndex < emptyCells.Count && !changed; emptyIndex++)
            {
                Vector2Int emptyCell = emptyCells[emptyIndex];
                for (int arrowIndex = 0; arrowIndex < document.arrows.Count && !changed; arrowIndex++)
                {
                    RuntimeArrowJson arrow = document.arrows[arrowIndex];
                    if (arrow.points == null || arrow.points.Count < 2)
                    {
                        continue;
                    }

                    Vector2Int tail = arrow.points[0].ToVector2Int();
                    if (ManhattanDistance(tail, emptyCell) == 1)
                    {
                        arrow.points.Insert(0, IntPoint.FromVector2Int(emptyCell));
                        if (AnalyzeDocument(document).solvable)
                        {
                            changed = true;
                            continue;
                        }

                        arrow.points.RemoveAt(0);
                    }

                    Vector2Int head = arrow.points[arrow.points.Count - 1].ToVector2Int();
                    if (ManhattanDistance(head, emptyCell) == 1)
                    {
                        arrow.points.Add(IntPoint.FromVector2Int(emptyCell));
                        if (AnalyzeDocument(document).solvable)
                        {
                            changed = true;
                            continue;
                        }

                        arrow.points.RemoveAt(arrow.points.Count - 1);
                    }
                }
            }

            if (!changed)
            {
                emptyCells = GetEmptyDocumentCells(document);
                for (int firstIndex = 0; firstIndex < emptyCells.Count && !changed; firstIndex++)
                {
                    for (int secondIndex = firstIndex + 1; secondIndex < emptyCells.Count && !changed; secondIndex++)
                    {
                        if (ManhattanDistance(emptyCells[firstIndex], emptyCells[secondIndex]) != 1)
                        {
                            continue;
                        }

                        RuntimeArrowJson repairArrow = new RuntimeArrowJson
                        {
                            id = $"Repair Arrow {document.arrows.Count + 1}",
                            color = SerializableColor.FromColor(Color.black)
                        };
                        repairArrow.points.Add(IntPoint.FromVector2Int(emptyCells[firstIndex]));
                        repairArrow.points.Add(IntPoint.FromVector2Int(emptyCells[secondIndex]));
                        document.arrows.Add(repairArrow);
                        if (AnalyzeDocument(document).solvable)
                        {
                            changed = true;
                            continue;
                        }

                        document.arrows.RemoveAt(document.arrows.Count - 1);
                    }
                }
            }

            if (!changed)
            {
                return false;
            }
        }

        LevelMetrics finalMetrics = AnalyzeDocument(document);
        return finalMetrics.solvable && finalMetrics.occupiedCellCount == finalMetrics.zoneCellCount;
    }

    private static List<Vector2Int> GetEmptyDocumentCells(RuntimeArrowLevelDocument document)
    {
        HashSet<Vector2Int> emptyCells = BuildZone(document);
        HashSet<Vector2Int> arrowCells = new HashSet<Vector2Int>();
        for (int arrowIndex = 0; arrowIndex < document.arrows.Count; arrowIndex++)
        {
            RuntimeArrowJson arrow = document.arrows[arrowIndex];
            List<Vector2Int> points = new List<Vector2Int>();
            for (int pointIndex = 0; pointIndex < arrow.points.Count; pointIndex++)
            {
                points.Add(arrow.points[pointIndex].ToVector2Int());
            }

            if (TryExpandPath(points, BuildZone(document), arrowCells, out _))
            {
                emptyCells.ExceptWith(arrowCells);
            }
        }

        return new List<Vector2Int>(emptyCells);
    }

    private static int ManhattanDistance(Vector2Int first, Vector2Int second)
    {
        return Math.Abs(first.x - second.x) + Math.Abs(first.y - second.y);
    }

    private static bool CanEscape(
        int arrowIndex,
        List<Vector2Int> heads,
        List<Vector2Int> exits,
        Dictionary<Vector2Int, int> occupied,
        HashSet<int> removed,
        int width,
        int height)
    {
        Vector2Int check = heads[arrowIndex] + exits[arrowIndex];
        while (check.x >= 0 && check.x < width && check.y >= 0 && check.y < height)
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
        string spike = profile.isVeryHard ? " [VERY HARD]" : (profile.isSpike ? " [HARD]" : " [EASY]");
        string mode = profile.algorithmModeIndex == 5 ? "DX Flow" : "DX";
        return $"{levelNumber:000}. {profile.name}{spike}: {profile.width}x{profile.height} {profile.shape}, {mode} complexity {profile.complexityPercent}%, "
            + $"{metrics.arrowCount} arrows, {metrics.occupiedCellCount}/{metrics.zoneCellCount} cells ({metrics.fillRatio:P1}), "
            + $"waves {metrics.dependencyDepth}, widest {metrics.maxWaveWidth}, >2 waves {metrics.overTwoWaveCount}, "
            + $"initial {metrics.initialPlayableCount}, avg turns {metrics.averageTurns:F2}, longest {metrics.longestArrow}, "
            + $"cross T>B {metrics.topBlockedByBottomCount}/B>T {metrics.bottomBlockedByTopCount}, "
            + $"L>R {metrics.leftBlockedByRightCount}/R>L {metrics.rightBlockedByLeftCount}, "
            + $"regions {metrics.regionTransitionCount}, streak {metrics.longestRegionStreak}, "
            + $"backtrack H{metrics.horizontalBacktrackCount}/V{metrics.verticalBacktrackCount}, seed {seedLabel}.";
    }

    private static void RebuildCompleteReports(
        LevelProfile[] profiles,
        out List<string> reportLines,
        out List<string> campaignOrder)
    {
        reportLines = new List<string>
        {
            $"Generated DX Campaign {profiles.Length}",
            $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            "Cadence: 4 Easy, 1 Hard, 4 Easy, 1 Very Hard.",
            "Easy uses Complex Guided DX. Hard and Very Hard use Complex Guided DX Flow.",
            "Exactly 50 deterministic random campaign positions use custom board shapes.",
            "Coverage target: 100% of every active board cell.",
            string.Empty
        };
        campaignOrder = new List<string>
        {
            $"{profiles.Length}-Level Campaign Order",
            "4 Easy -> 1 Hard -> 4 Easy -> 1 Very Hard",
            string.Empty
        };

        for (int levelIndex = 0; levelIndex < profiles.Length; levelIndex++)
        {
            LevelProfile profile = profiles[levelIndex];
            string jsonPath = GetProjectPath($"{JsonOutputFolder}/{profile.name}.json");
            RuntimeArrowLevelDocument document =
                JsonUtility.FromJson<RuntimeArrowLevelDocument>(File.ReadAllText(jsonPath));
            LevelMetrics metrics = AnalyzeDocument(document);
            if (!metrics.solvable || metrics.occupiedCellCount != metrics.zoneCellCount)
            {
                throw new InvalidDataException(
                    $"Cannot rebuild reports because {profile.name} is not solvable and fully occupied.");
            }

            reportLines.Add(BuildReportLine(levelIndex + 1, profile, metrics, "campaign"));
            campaignOrder.Add($"{levelIndex + 1:000}: {profile.name}");
        }
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
                if (IsShapeCellActive(profile, x, y))
                {
                    cells.Add(new Vector2Int(x, y));
                }
            }
        }

        return cells;
    }

    private static bool IsShapeCellActive(LevelProfile profile, int x, int y)
    {
        BoardShape shape = profile.shape;
        int width = profile.width;
        int height = profile.height;
        float centerX = (width - 1) * 0.5f;
        float centerY = (height - 1) * 0.5f;

        switch (shape)
        {
            case BoardShape.Beveled:
            {
                int maximumCut = Math.Max(2, Math.Min(width, height) / 4);
                int topLeft = 1 + Variant(profile, 11, maximumCut);
                int topRight = 1 + Variant(profile, 17, maximumCut);
                int bottomLeft = 1 + Variant(profile, 23, maximumCut);
                int bottomRight = 1 + Variant(profile, 29, maximumCut);
                return x + y >= bottomLeft
                    && (width - 1 - x) + y >= bottomRight
                    && x + (height - 1 - y) >= topLeft
                    && (width - 1 - x) + (height - 1 - y) >= topRight;
            }
            case BoardShape.Cross:
            {
                int verticalCenter = Mathf.Clamp(
                    Mathf.RoundToInt(centerX) + Variant(profile, 31, 3) - 1,
                    1,
                    width - 2);
                int horizontalCenter = Mathf.Clamp(
                    Mathf.RoundToInt(centerY) + Variant(profile, 37, 3) - 1,
                    1,
                    height - 2);
                int verticalHalfWidth = Math.Max(
                    1,
                    width / 7 + Variant(profile, 41, 2) + profile.campaignLevel % 2);
                int horizontalHalfHeight = Math.Max(1, height / 7 + Variant(profile, 43, 2));
                return Math.Abs(x - verticalCenter) <= verticalHalfWidth
                    || Math.Abs(y - horizontalCenter) <= horizontalHalfHeight;
            }
            case BoardShape.Heart:
            {
                float horizontalScale = width * (0.43f + Variant(profile, 47, 5) * 0.012f);
                float verticalScale = height * (0.43f + Variant(profile, 53, 5) * 0.012f);
                float verticalOffset = height * (0.43f + Variant(profile, 59, 5) * 0.018f);
                float nx = (x - centerX) / Math.Max(1f, horizontalScale);
                float ny = (y - verticalOffset) / Math.Max(1f, verticalScale);
                float baseTerm = nx * nx + ny * ny - 1f;
                float threshold = 0.035f + Variant(profile, 61, 5) * 0.018f;
                return baseTerm * baseTerm * baseTerm - nx * nx * ny * ny * ny <= threshold;
            }
            case BoardShape.Hexagon:
            {
                int slopeDepth = Math.Max(2, Math.Min(width, height) / 5 + Variant(profile, 67, 3));
                int edgeDistance = Math.Min(x, width - 1 - x);
                int inset = Math.Max(0, slopeDepth - edgeDistance);
                int verticalShift = Variant(profile, 71, 3) - 1;
                int lowerInset = Math.Max(0, inset + verticalShift);
                int upperInset = Math.Max(0, inset - verticalShift);
                return y >= lowerInset && y < height - upperInset;
            }
            case BoardShape.Hourglass:
            {
                float waistY = centerY + Variant(profile, 73, 3) - 1;
                float distanceFromCenter = Math.Abs(y - waistY) / Math.Max(1f, centerY);
                int maximumInset = Math.Max(1, width / 5 + Variant(profile, 79, Math.Max(2, width / 8)));
                int inset = Mathf.RoundToInt(maximumInset * (1f - distanceFromCenter));
                int horizontalShift = Variant(profile, 83 + y, 3) - 1;
                return x >= inset + horizontalShift && x < width - inset + horizontalShift;
            }
            case BoardShape.Shield:
            {
                float normalizedY = y / Math.Max(1f, height - 1f);
                float shoulderHeight = 0.42f + Variant(profile, 89, 5) * 0.035f;
                int sideInset = 1 + Variant(profile, 97, Math.Max(1, width / 10));
                if (normalizedY >= shoulderHeight)
                {
                    return x >= sideInset && x < width - sideInset;
                }

                float tipShift = Variant(profile, 101, 3) - 1;
                float halfWidth = Mathf.Lerp(
                    0.5f,
                    width * 0.5f - sideInset,
                    normalizedY / shoulderHeight);
                return Math.Abs(x - (centerX + tipShift * (1f - normalizedY))) <= halfWidth;
            }
            case BoardShape.Clover:
            {
                float radius = Math.Min(width, height) * (0.245f + Variant(profile, 103, 4) * 0.012f);
                float horizontalOffset = radius * (0.62f + Variant(profile, 107, 4) * 0.07f);
                float verticalOffset = radius * (0.62f + Variant(profile, 109, 4) * 0.07f);
                float centerShiftX = Variant(profile, 113, 3) - 1;
                float centerShiftY = Variant(profile, 127, 3) - 1;
                return InsideCircle(x, y, centerX - horizontalOffset + centerShiftX, centerY, radius)
                    || InsideCircle(x, y, centerX + horizontalOffset + centerShiftX, centerY, radius)
                    || InsideCircle(x, y, centerX, centerY - verticalOffset + centerShiftY, radius)
                    || InsideCircle(x, y, centerX, centerY + verticalOffset + centerShiftY, radius);
            }
            case BoardShape.TwinBlocks:
            {
                int gap = 1 + Variant(profile, 131, 2);
                int leftWidth = Math.Max(2, (width - gap) / 2);
                int rightStart = leftWidth + gap;
                int leftBottom = Variant(profile, 137, Math.Max(1, height / 5));
                int leftTop = height - 1 - Variant(profile, 139, Math.Max(1, height / 6));
                int rightBottom = Variant(profile, 149, Math.Max(1, height / 6));
                int rightTop = height - 1 - Variant(profile, 151, Math.Max(1, height / 5));
                bool leftBlock = x < leftWidth && y >= leftBottom && y <= leftTop;
                bool rightBlock = x >= rightStart && y >= rightBottom && y <= rightTop;
                return leftBlock || rightBlock;
            }
            case BoardShape.TwinStacks:
            {
                int gap = 1 + Variant(profile, 157, 2);
                int bottomHeight = Math.Max(2, (height - gap) / 2);
                int topStart = bottomHeight + gap;
                int bottomLeft = Variant(profile, 163, Math.Max(1, width / 6));
                int bottomRight = width - 1 - Variant(profile, 167, Math.Max(1, width / 5));
                int topLeft = Variant(profile, 173, Math.Max(1, width / 5));
                int topRight = width - 1 - Variant(profile, 179, Math.Max(1, width / 6));
                bool bottomBlock = y < bottomHeight && x >= bottomLeft && x <= bottomRight;
                bool topBlock = y >= topStart && x >= topLeft && x <= topRight;
                return bottomBlock || topBlock;
            }
            case BoardShape.Dumbbell:
            {
                int roomWidth = Math.Max(2, width / 3 + Variant(profile, 181, Math.Max(1, width / 8)));
                int verticalMargin = Variant(profile, 191, Math.Max(1, height / 6));
                int bridgeHalfHeight = 1 + Variant(profile, 193, Math.Max(1, height / 10));
                int bridgeCenter = Mathf.Clamp(
                    Mathf.RoundToInt(centerY) + Variant(profile, 197, 5) - 2,
                    bridgeHalfHeight,
                    height - 1 - bridgeHalfHeight);
                bool leftRoom = x < roomWidth && y >= verticalMargin && y < height - verticalMargin;
                bool rightRoom = x >= width - roomWidth && y >= verticalMargin && y < height - verticalMargin;
                bool bridge = y >= bridgeCenter - bridgeHalfHeight
                    && y <= bridgeCenter + bridgeHalfHeight;
                return leftRoom || rightRoom || bridge;
            }
            case BoardShape.Diamond:
            {
                float verticalRadius = Math.Max(1f, height * 0.5f - 0.5f);
                float horizontalOffset = (Variant(profile, 211, 3) - 1) * 0.35f;
                float verticalOffset = (Variant(profile, 223, 3) - 1) * 0.35f;
                float normalizedY = Mathf.Clamp01(
                    Math.Abs(y - centerY - verticalOffset) / verticalRadius);
                float minimumHalfWidth = Math.Max(2f, width * 0.19f);
                float maximumHalfWidth = width * 0.5f - 0.5f;
                float halfWidth = Mathf.Lerp(maximumHalfWidth, minimumHalfWidth, normalizedY);
                return Math.Abs(x - centerX - horizontalOffset) <= halfWidth;
            }
            case BoardShape.Oval:
            {
                float horizontalRadius = Math.Max(1f, width * (0.49f + Variant(profile, 227, 2) * 0.01f));
                float verticalRadius = Math.Max(1f, height * (0.51f + Variant(profile, 229, 3) * 0.012f));
                float horizontalOffset = Variant(profile, 233, 3) - 1;
                float verticalOffset = Variant(profile, 239, 3) - 1;
                float normalizedX = Math.Abs(x - centerX - horizontalOffset) / horizontalRadius;
                float normalizedY = Math.Abs(y - centerY - verticalOffset) / verticalRadius;
                return Mathf.Pow(normalizedX, 4f) + Mathf.Pow(normalizedY, 4f) <= 1f;
            }
            case BoardShape.Staircase:
            {
                int bandCount = 4 + Variant(profile, 241, 2);
                int band = Mathf.Clamp(y * bandCount / Math.Max(1, height), 0, bandCount - 1);
                int stepWidth = Math.Max(1, width / (bandCount + 3));
                bool risesRight = Variant(profile, 251, 2) == 0;
                int shiftedBand = risesRight ? band : bandCount - 1 - band;
                int leftInset = shiftedBand * stepWidth / 2;
                int rightInset = (bandCount - 1 - shiftedBand) * stepWidth / 2;
                return x >= leftInset && x < width - rightInset;
            }
            case BoardShape.LShape:
            {
                int verticalThickness = Math.Max(3, width / 3 + Variant(profile, 257, 2));
                int horizontalThickness = Math.Max(3, height / 3 + Variant(profile, 263, 2));
                int rotation = Variant(profile, 269, 4);
                bool verticalArm = rotation < 2
                    ? x < verticalThickness
                    : x >= width - verticalThickness;
                bool horizontalArm = (rotation == 0 || rotation == 3)
                    ? y < horizontalThickness
                    : y >= height - horizontalThickness;
                return verticalArm || horizontalArm;
            }
            case BoardShape.Arrowhead:
            {
                bool pointsRight = Variant(profile, 271, 2) == 0;
                int directedX = pointsRight ? x : width - 1 - x;
                int tailLength = Math.Max(3, width / 3);
                int tailHalfHeight = Math.Max(2, height / 6 + Variant(profile, 277, 2));
                if (directedX < tailLength)
                {
                    return Math.Abs(y - centerY) <= tailHalfHeight;
                }

                float progress = (directedX - tailLength)
                    / Math.Max(1f, width - 1f - tailLength);
                float widestHalfHeight = height * 0.46f;
                float tipHalfHeight = Math.Max(2f, height * 0.16f);
                float halfHeight = progress < 0.68f
                    ? Mathf.Lerp(tailHalfHeight + 1f, widestHalfHeight, progress / 0.68f)
                    : Mathf.Lerp(widestHalfHeight, tipHalfHeight, (progress - 0.68f) / 0.32f);
                return Math.Abs(y - centerY) <= halfHeight;
            }
            default:
                return true;
        }
    }

    private static int Variant(LevelProfile profile, int salt, int exclusiveMaximum)
    {
        if (exclusiveMaximum <= 1)
        {
            return 0;
        }

        uint mixed = (uint)profile.campaignLevel * 2654435761u
            + (uint)salt * 2246822519u
            + 3266489917u;
        mixed ^= mixed >> 15;
        return (int)(mixed % (uint)exclusiveMaximum);
    }

    private static bool InsideCircle(float x, float y, float centerX, float centerY, float radius)
    {
        float dx = x - centerX;
        float dy = y - centerY;
        return dx * dx + dy * dy <= radius * radius;
    }

    private static LevelProfile[] CreateProfiles()
    {
        List<LevelProfile> profiles = new List<LevelProfile>(200);
        int easyIndex = 0;
        int hardIndex = 0;
        int veryHardIndex = 0;

        for (int campaignLevel = 1; campaignLevel <= 200; campaignLevel++)
        {
            int cadencePosition = (campaignLevel - 1) % 10 + 1;
            if (cadencePosition == 5)
            {
                hardIndex++;
                float progress = (hardIndex - 1) / 19f;
                float acceleratedProgress = Mathf.Pow(progress, 0.55f);
                int width = 14 + Mathf.RoundToInt(acceleratedProgress * 8f);
                int height = 13 + Mathf.RoundToInt(acceleratedProgress * 7f);
                BoardShape shape = GetCampaignBoardShape(campaignLevel);
                profiles.Add(P(
                    $"Level_H_{hardIndex}", width, height, shape,
                    Mathf.RoundToInt(Mathf.Lerp(82f, 99f, acceleratedProgress)),
                    Mathf.Lerp(0.36f, 0.16f, acceleratedProgress),
                    Mathf.RoundToInt(Mathf.Lerp(420f, 650f, acceleratedProgress)),
                    5, true, false, campaignLevel));
            }
            else if (cadencePosition == 10)
            {
                veryHardIndex++;
                float progress = (veryHardIndex - 1) / 19f;
                float acceleratedProgress = Mathf.Pow(progress, 0.55f);
                int width = 16 + Mathf.RoundToInt(acceleratedProgress * 8f);
                int height = 15 + Mathf.RoundToInt(acceleratedProgress * 7f);
                BoardShape shape = GetCampaignBoardShape(campaignLevel);
                profiles.Add(P(
                    $"Level_VH_{veryHardIndex}", width, height, shape,
                    Mathf.RoundToInt(Mathf.Lerp(92f, 100f, acceleratedProgress)),
                    Mathf.Lerp(0.26f, 0.12f, acceleratedProgress),
                    Mathf.RoundToInt(Mathf.Lerp(500f, 760f, acceleratedProgress)),
                    5, true, true, campaignLevel));
            }
            else
            {
                easyIndex++;
                float progress = (easyIndex - 1) / 159f;
                float acceleratedProgress = Mathf.Pow(progress, 0.55f);
                int width = 8 + Mathf.RoundToInt(acceleratedProgress * 10f);
                int height = 7 + Mathf.RoundToInt(acceleratedProgress * 10f);
                BoardShape shape = GetCampaignBoardShape(campaignLevel);
                profiles.Add(P(
                    $"Level_EZ_{easyIndex}", width, height, shape,
                    Mathf.RoundToInt(Mathf.Lerp(52f, 98f, acceleratedProgress)),
                    Mathf.Lerp(0.58f, 0.20f, acceleratedProgress),
                    Mathf.RoundToInt(Mathf.Lerp(240f, 440f, acceleratedProgress)),
                    4, false, false, campaignLevel));
            }
        }

        UpgradeOpeningProfiles(profiles);
        UpgradeNextTwentyProfiles(profiles);
        UpgradeThirdTwentyProfiles(profiles);
        UpgradeFourthTwentyProfiles(profiles);
        UpgradeFinalTwentyProfiles(profiles);
        UpgradeSixthTwentyProfiles(profiles);
        UpgradeFinalThirtyProfiles(profiles);
        UpgradeFinalFiftyProfiles(profiles);
        AddCampaignNumberPrefixes(profiles);
        return profiles.ToArray();
    }

    private static void AddCampaignNumberPrefixes(List<LevelProfile> profiles)
    {
        for (int levelIndex = 0; levelIndex < profiles.Count; levelIndex++)
        {
            profiles[levelIndex].name = $"{levelIndex + 1}_{profiles[levelIndex].name}";
        }
    }

    private static void UpgradeOpeningProfiles(List<LevelProfile> profiles)
    {
        int easyNumber = 0;
        int hardNumber = 0;
        int veryHardNumber = 0;
        for (int i = 0; i < 20; i++)
        {
            LevelProfile profile = profiles[i];
            if (profile.isVeryHard)
            {
                veryHardNumber++;
                profile.width = 17 + veryHardNumber * 2;
                profile.height = 16 + veryHardNumber * 2;
                profile.complexityPercent = 97 + veryHardNumber;
                profile.minimumLengthWeight = 0.13f - veryHardNumber * 0.02f;
                profile.attempts = 650 + veryHardNumber * 90;
            }
            else if (profile.isSpike)
            {
                hardNumber++;
                profile.width = 14 + hardNumber * 2;
                profile.height = 13 + hardNumber * 2;
                profile.complexityPercent = 89 + hardNumber * 3;
                profile.minimumLengthWeight = 0.22f - hardNumber * 0.025f;
                profile.attempts = 540 + hardNumber * 80;
            }
            else
            {
                easyNumber++;
                int block = (easyNumber - 1) / 4;
                int step = (easyNumber - 1) % 4;
                profile.width = 10 + block + (step + 1) / 2;
                profile.height = 9 + block + step / 2;
                profile.complexityPercent = 66 + block * 5 + step * 2;
                profile.minimumLengthWeight = Mathf.Lerp(0.40f, 0.23f, (easyNumber - 1) / 15f);
                profile.attempts = 340 + block * 45 + step * 20;
            }

            profile.maxLength = Math.Max(
                8,
                Mathf.RoundToInt(
                    Mathf.Sqrt(profile.width * profile.height)
                    * Mathf.Lerp(1.58f, 1.88f, profile.complexityPercent / 100f)));
        }
    }

    private static void UpgradeNextTwentyProfiles(List<LevelProfile> profiles)
    {
        int easyNumber = 0;
        int hardNumber = 0;
        int veryHardNumber = 0;
        for (int i = 20; i < 40; i++)
        {
            LevelProfile profile = profiles[i];
            if (profile.isVeryHard)
            {
                veryHardNumber++;
                profile.width = 20 + veryHardNumber * 2;
                profile.height = 19 + veryHardNumber * 2;
                profile.complexityPercent = 98 + veryHardNumber;
                profile.minimumLengthWeight = 0.10f - veryHardNumber * 0.015f;
                profile.attempts = 780 + veryHardNumber * 100;
            }
            else if (profile.isSpike)
            {
                hardNumber++;
                profile.width = 17 + hardNumber * 2;
                profile.height = 16 + hardNumber * 2;
                profile.complexityPercent = 92 + hardNumber * 3;
                profile.minimumLengthWeight = 0.17f - hardNumber * 0.02f;
                profile.attempts = 650 + hardNumber * 90;
            }
            else
            {
                easyNumber++;
                int block = (easyNumber - 1) / 4;
                int step = (easyNumber - 1) % 4;
                profile.width = 14 + block + (step + 1) / 2;
                profile.height = 13 + block + step / 2;
                profile.complexityPercent = 80 + block * 4 + step * 2;
                profile.minimumLengthWeight = Mathf.Lerp(0.30f, 0.16f, (easyNumber - 1) / 15f);
                profile.attempts = 450 + block * 55 + step * 25;
            }

            profile.complexityPercent = Mathf.Clamp(profile.complexityPercent, 0, 100);
            profile.maxLength = Math.Max(
                10,
                Mathf.RoundToInt(
                    Mathf.Sqrt(profile.width * profile.height)
                    * Mathf.Lerp(1.68f, 2.02f, profile.complexityPercent / 100f)));
        }
    }

    private static void UpgradeThirdTwentyProfiles(List<LevelProfile> profiles)
    {
        int easyNumber = 0;
        int hardNumber = 0;
        int veryHardNumber = 0;
        for (int i = 40; i < 60; i++)
        {
            LevelProfile profile = profiles[i];
            if (profile.isVeryHard)
            {
                veryHardNumber++;
                profile.width = 24 + veryHardNumber;
                profile.height = 23 + veryHardNumber;
                profile.complexityPercent = 100;
                profile.minimumLengthWeight = 0.065f - veryHardNumber * 0.01f;
                profile.attempts = 960 + veryHardNumber * 120;
            }
            else if (profile.isSpike)
            {
                hardNumber++;
                profile.width = 21 + hardNumber;
                profile.height = 20 + hardNumber;
                profile.complexityPercent = 98 + hardNumber;
                profile.minimumLengthWeight = 0.12f - hardNumber * 0.018f;
                profile.attempts = 820 + hardNumber * 100;
            }
            else
            {
                easyNumber++;
                int block = (easyNumber - 1) / 4;
                int step = (easyNumber - 1) % 4;
                profile.width = 18 + block + (step + 1) / 2;
                profile.height = 17 + block + step / 2;
                profile.complexityPercent = 88 + block * 3 + step * 2;
                profile.minimumLengthWeight = Mathf.Lerp(0.20f, 0.10f, (easyNumber - 1) / 15f);
                profile.attempts = 560 + block * 65 + step * 30;
            }

            profile.complexityPercent = Mathf.Clamp(profile.complexityPercent, 0, 100);
            profile.maxLength = Math.Max(
                12,
                Mathf.RoundToInt(
                    Mathf.Sqrt(profile.width * profile.height)
                    * Mathf.Lerp(1.82f, 2.15f, profile.complexityPercent / 100f)));
        }
    }

    private static void UpgradeFourthTwentyProfiles(List<LevelProfile> profiles)
    {
        int easyNumber = 0;
        int hardNumber = 0;
        int veryHardNumber = 0;
        for (int i = 60; i < 80; i++)
        {
            LevelProfile profile = profiles[i];
            if (profile.isVeryHard)
            {
                veryHardNumber++;
                profile.width = 26 + veryHardNumber;
                profile.height = 25 + veryHardNumber;
                profile.complexityPercent = 100;
                profile.minimumLengthWeight = 0.04f - veryHardNumber * 0.005f;
                profile.attempts = 1120 + veryHardNumber * 120;
            }
            else if (profile.isSpike)
            {
                hardNumber++;
                profile.width = 23 + hardNumber;
                profile.height = 22 + hardNumber;
                profile.complexityPercent = 100;
                profile.minimumLengthWeight = 0.085f - hardNumber * 0.012f;
                profile.attempts = 980 + hardNumber * 100;
            }
            else
            {
                easyNumber++;
                int block = (easyNumber - 1) / 4;
                int step = (easyNumber - 1) % 4;
                profile.width = 22 + block + (step + 1) / 2;
                profile.height = 21 + block + step / 2;
                profile.complexityPercent = 94 + block * 2 + step * 2;
                profile.minimumLengthWeight = Mathf.Lerp(0.13f, 0.065f, (easyNumber - 1) / 15f);
                profile.attempts = 650 + block * 70 + step * 35;
            }

            profile.complexityPercent = Mathf.Clamp(profile.complexityPercent, 0, 100);
            profile.maxLength = Math.Max(
                14,
                Mathf.RoundToInt(
                    Mathf.Sqrt(profile.width * profile.height)
                    * Mathf.Lerp(1.95f, 2.28f, profile.complexityPercent / 100f)));
        }
    }

    private static void UpgradeFinalTwentyProfiles(List<LevelProfile> profiles)
    {
        int easyNumber = 0;
        int hardNumber = 0;
        int veryHardNumber = 0;
        for (int i = 80; i < 100; i++)
        {
            LevelProfile profile = profiles[i];
            if (profile.isVeryHard)
            {
                veryHardNumber++;
                profile.width = 27 + veryHardNumber * 2;
                profile.height = 26 + veryHardNumber * 2;
                profile.complexityPercent = 100;
                profile.minimumLengthWeight = 0.028f - veryHardNumber * 0.004f;
                profile.attempts = 1320 + veryHardNumber * 140;
            }
            else if (profile.isSpike)
            {
                hardNumber++;
                profile.width = 25 + hardNumber * 2;
                profile.height = 24 + hardNumber * 2;
                profile.complexityPercent = 100;
                profile.minimumLengthWeight = 0.06f - hardNumber * 0.01f;
                profile.attempts = 1160 + hardNumber * 120;
            }
            else
            {
                easyNumber++;
                int block = (easyNumber - 1) / 4;
                int step = (easyNumber - 1) % 4;
                profile.width = 26 + block + (step + 1) / 2;
                profile.height = 25 + block + step / 2;
                profile.complexityPercent = 98 + block + step;
                profile.minimumLengthWeight = Mathf.Lerp(0.08f, 0.035f, (easyNumber - 1) / 15f);
                profile.attempts = 760 + block * 80 + step * 40;
            }

            profile.complexityPercent = Mathf.Clamp(profile.complexityPercent, 0, 100);
            profile.maxLength = Math.Max(
                16,
                Mathf.RoundToInt(
                    Mathf.Sqrt(profile.width * profile.height)
                    * Mathf.Lerp(2.08f, 2.35f, profile.complexityPercent / 100f)));
        }
    }

    private static void UpgradeSixthTwentyProfiles(List<LevelProfile> profiles)
    {
        int easyNumber = 0;
        int hardNumber = 0;
        int veryHardNumber = 0;
        for (int i = 100; i < 120; i++)
        {
            LevelProfile profile = profiles[i];
            if (profile.isVeryHard)
            {
                veryHardNumber++;
                profile.width = 31 + veryHardNumber * 2;
                profile.height = 30 + veryHardNumber * 2;
                profile.complexityPercent = 100;
                profile.minimumLengthWeight = Mathf.Max(0.01f, 0.022f - veryHardNumber * 0.004f);
                profile.attempts = 1580 + veryHardNumber * 150;
            }
            else if (profile.isSpike)
            {
                hardNumber++;
                profile.width = 29 + hardNumber * 2;
                profile.height = 28 + hardNumber * 2;
                profile.complexityPercent = 100;
                profile.minimumLengthWeight = Mathf.Max(0.015f, 0.045f - hardNumber * 0.009f);
                profile.attempts = 1400 + hardNumber * 130;
            }
            else
            {
                easyNumber++;
                int block = (easyNumber - 1) / 4;
                int step = (easyNumber - 1) % 4;
                profile.width = 31 + block + (step + 1) / 2;
                profile.height = 30 + block + step / 2;
                profile.complexityPercent = 100;
                profile.minimumLengthWeight = Mathf.Lerp(0.045f, 0.018f, (easyNumber - 1) / 15f);
                profile.attempts = 900 + block * 90 + step * 45;
            }

            profile.maxLength = Math.Max(
                18,
                Mathf.RoundToInt(
                    Mathf.Sqrt(profile.width * profile.height)
                    * (profile.isVeryHard ? 2.48f : (profile.isSpike ? 2.42f : 2.38f))));
        }
    }

    private static void UpgradeFinalThirtyProfiles(List<LevelProfile> profiles)
    {
        int easyNumber = 0;
        int hardNumber = 0;
        int veryHardNumber = 0;
        for (int i = 120; i < 150; i++)
        {
            LevelProfile profile = profiles[i];
            if (profile.isVeryHard)
            {
                veryHardNumber++;
                profile.width = 35 + veryHardNumber * 2;
                profile.height = 34 + veryHardNumber * 2;
                if (profile.campaignLevel == 150)
                {
                    profile.width = 39;
                    profile.height = 38;
                }
                profile.complexityPercent = 100;
                profile.minimumLengthWeight = 0.01f;
                profile.attempts = 1800 + veryHardNumber * 170;
            }
            else if (profile.isSpike)
            {
                hardNumber++;
                profile.width = 33 + hardNumber * 2;
                profile.height = 32 + hardNumber * 2;
                profile.complexityPercent = 100;
                profile.minimumLengthWeight = Mathf.Max(0.012f, 0.032f - hardNumber * 0.006f);
                profile.attempts = 1580 + hardNumber * 145;
            }
            else
            {
                easyNumber++;
                int block = (easyNumber - 1) / 4;
                int step = (easyNumber - 1) % 4;
                profile.width = 36 + block + (step + 1) / 2;
                profile.height = 35 + block + step / 2;
                if (profile.campaignLevel >= 137)
                {
                    profile.width = Math.Min(profile.width, 39);
                    profile.height = Math.Min(profile.height, 37);
                }
                if (profile.campaignLevel == 137 || profile.campaignLevel == 143)
                {
                    profile.width = 38;
                }
                if (profile.campaignLevel == 143)
                {
                    profile.height = 36;
                }
                if (profile.campaignLevel == 144)
                {
                    profile.width = 38;
                }
                if (profile.campaignLevel == 146)
                {
                    profile.height = 36;
                }
                if (profile.campaignLevel == 149)
                {
                    profile.width = 36;
                    profile.height = 35;
                }
                profile.complexityPercent = 100;
                profile.minimumLengthWeight = Mathf.Lerp(0.028f, 0.012f, (easyNumber - 1) / 23f);
                profile.attempts = 1040 + block * 95 + step * 50;
            }

            profile.maxLength = Math.Max(
                20,
                Mathf.RoundToInt(
                    Mathf.Sqrt(profile.width * profile.height)
                    * (profile.isVeryHard ? 2.56f : (profile.isSpike ? 2.50f : 2.44f))));
        }
    }

    private static void UpgradeFinalFiftyProfiles(List<LevelProfile> profiles)
    {
        int easyNumber = 0;
        int hardNumber = 0;
        int veryHardNumber = 0;
        for (int i = 150; i < 200; i++)
        {
            LevelProfile profile = profiles[i];
            if (profile.isVeryHard)
            {
                veryHardNumber++;
                profile.width = Math.Min(39, 37 + (veryHardNumber + 1) / 2);
                profile.height = Math.Min(38, 36 + (veryHardNumber + 1) / 2);
                profile.complexityPercent = 100;
                profile.minimumLengthWeight = 0.01f;
                profile.attempts = 2100 + veryHardNumber * 180;
            }
            else if (profile.isSpike)
            {
                hardNumber++;
                profile.width = Math.Min(39, 36 + hardNumber);
                profile.height = Math.Min(38, 35 + (hardNumber + 1) / 2);
                profile.complexityPercent = 100;
                profile.minimumLengthWeight = 0.012f;
                profile.attempts = 1900 + hardNumber * 155;
            }
            else
            {
                easyNumber++;
                int sizeStep = (easyNumber - 1) % 4;
                profile.width = 36 + (sizeStep + 1) / 2;
                profile.height = 35 + sizeStep / 2;
                if (profile.campaignLevel == 179)
                {
                    // Larger variants leave one structurally untileable cell
                    // for this deterministic seed family.
                    profile.width = 34;
                    profile.height = 32;
                }
                else if (profile.campaignLevel == 156)
                {
                    profile.width = 35;
                    profile.height = 35;
                }
                else if (profile.campaignLevel == 184)
                {
                    profile.width = 37;
                    profile.height = 36;
                }
                profile.complexityPercent = 100;
                profile.minimumLengthWeight =
                    Mathf.Lerp(0.018f, 0.01f, (easyNumber - 1) / 39f);
                profile.attempts =
                    1180 + ((easyNumber - 1) / 8) * 80 + sizeStep * 40;
            }

            profile.maxLength = Math.Max(
                20,
                Mathf.RoundToInt(
                    Mathf.Sqrt(profile.width * profile.height)
                    * (profile.isVeryHard ? 2.62f : (profile.isSpike ? 2.56f : 2.48f))));
        }
    }

    private static BoardShape GetCampaignBoardShape(int campaignLevel)
    {
        if (!ShapedCampaignLevels.Contains(campaignLevel))
        {
            return BoardShape.Rectangle;
        }

        // The 19x18 hexagon used by campaign level 46 leaves two isolated
        // coverage cells for this generator. Beveled keeps a custom silhouette
        // while allowing the board to reach complete coverage reliably.
        if (campaignLevel == 46)
        {
            return BoardShape.Beveled;
        }

        switch (campaignLevel)
        {
            case 62:
                return BoardShape.Diamond;
            case 63:
                return BoardShape.Oval;
            case 68:
                return BoardShape.Staircase;
            case 69:
                return BoardShape.LShape;
            case 74:
                return BoardShape.Arrowhead;
            case 76:
                return BoardShape.Diamond;
            case 78:
                return BoardShape.Oval;
            case 81:
                return BoardShape.Arrowhead;
            case 82:
                return BoardShape.Staircase;
            case 83:
                return BoardShape.Beveled;
            case 86:
                return BoardShape.Oval;
            case 88:
                return BoardShape.LShape;
            case 91:
                return BoardShape.Shield;
            case 93:
                return BoardShape.TwinStacks;
            case 96:
                return BoardShape.TwinBlocks;
            case 98:
                return BoardShape.Dumbbell;
            case 101:
                return BoardShape.Beveled;
            case 102:
                return BoardShape.Staircase;
            case 104:
                return BoardShape.Beveled;
            case 106:
                return BoardShape.Oval;
            case 108:
                return BoardShape.LShape;
            case 111:
                return BoardShape.Shield;
            case 113:
                return BoardShape.TwinStacks;
            case 114:
                return BoardShape.Cross;
            case 116:
                return BoardShape.TwinBlocks;
            case 118:
                return BoardShape.Dumbbell;
            case 121:
                return BoardShape.Beveled;
            case 122:
                return BoardShape.Staircase;
            case 124:
                return BoardShape.Beveled;
            case 127:
                return BoardShape.TwinBlocks;
            case 126:
                return BoardShape.LShape;
            case 128:
                return BoardShape.TwinBlocks;
            case 131:
                return BoardShape.TwinBlocks;
            case 133:
                return BoardShape.Cross;
            case 134:
                return BoardShape.LShape;
            case 136:
                return BoardShape.Dumbbell;
            case 138:
                return BoardShape.LShape;
            case 141:
                return BoardShape.LShape;
            case 142:
                return BoardShape.Staircase;
            case 144:
                return BoardShape.LShape;
            case 146:
                return BoardShape.LShape;
            case 148:
                return BoardShape.TwinBlocks;
            case 151:
            case 161:
            case 171:
            case 181:
            case 191:
                return BoardShape.LShape;
            case 152:
            case 162:
            case 172:
            case 182:
            case 192:
                return BoardShape.TwinBlocks;
            case 154:
            case 164:
            case 174:
            case 184:
            case 194:
                return BoardShape.Staircase;
            case 156:
            case 166:
            case 176:
            case 186:
            case 196:
                return BoardShape.Cross;
            case 158:
            case 168:
            case 178:
            case 188:
            case 198:
                return BoardShape.Dumbbell;
        }

        BoardShape[] customShapes =
        {
            BoardShape.Beveled,
            BoardShape.Hexagon,
            BoardShape.Heart,
            BoardShape.Hourglass,
            BoardShape.Shield,
            BoardShape.Cross,
            BoardShape.Clover,
            BoardShape.TwinBlocks,
            BoardShape.TwinStacks,
            BoardShape.Dumbbell,
            BoardShape.Diamond,
            BoardShape.Oval,
            BoardShape.Staircase,
            BoardShape.LShape,
            BoardShape.Arrowhead
        };
        uint mixed = (uint)campaignLevel * 2654435761u + 1013904223u;
        BoardShape selectedShape = customShapes[(int)(mixed % (uint)customShapes.Length)];
        return campaignLevel == 29 && selectedShape == BoardShape.Clover
            ? BoardShape.Hexagon
            : selectedShape;
    }

    private static HashSet<int> CreateShapedCampaignLevels()
    {
        // Keep the campaign's previous custom levels in the selected half, then
        // use a fixed shuffle so the remaining positions stay random and reproducible.
        HashSet<int> selected = new HashSet<int> { 14, 19, 29, 39, 44, 59 };
        List<int> candidates = new List<int>();
        for (int campaignLevel = 1; campaignLevel <= 100; campaignLevel++)
        {
            if (!selected.Contains(campaignLevel))
            {
                candidates.Add(campaignLevel);
            }
        }

        System.Random random = new System.Random(20260723);
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            int temporary = candidates[i];
            candidates[i] = candidates[swapIndex];
            candidates[swapIndex] = temporary;
        }

        for (int i = 0; selected.Count < 50; i++)
        {
            selected.Add(candidates[i]);
        }

        // A custom 23x22 very-hard mask is disproportionately expensive and
        // unreliable. Keep the random 50/50 split by moving that slot to the
        // adjacent easy campaign position.
        selected.Remove(90);
        selected.Add(91);

        // Keep the second very-hard opening spike rectangular so its outer
        // boundary does not create several trivial exits. Move that custom
        // silhouette to the following recovery level to preserve the 50/50 mix.
        selected.Remove(20);
        selected.Add(21);

        // Difficulty spikes need a closed rectangular boundary. Move their
        // custom silhouettes onto nearby recovery levels without changing the
        // campaign's total of 50 shaped boards.
        selected.Remove(25);
        selected.Add(26);
        selected.Remove(35);
        selected.Add(42);
        selected.Remove(40);
        selected.Add(41);

        // Keep the third campaign section's hard and very-hard spikes closed.
        // Their custom silhouettes move to nearby easy recovery levels.
        selected.Remove(45);
        selected.Add(48);
        selected.Remove(50);
        selected.Add(49);
        selected.Remove(60);
        selected.Add(58);

        // Fourth-section spikes stay rectangular. Move their custom slots onto
        // nearby recovery levels where the new silhouettes can be explored.
        selected.Remove(65);
        selected.Add(62);
        selected.Remove(70);
        selected.Add(69);
        selected.Remove(75);
        selected.Add(76);

        // The final section spreads its custom masks across recovery levels and
        // keeps every hard/very-hard spike on a closed rectangular boundary.
        selected.Remove(85);
        selected.Add(82);
        selected.Remove(84);
        selected.Add(88);
        selected.Remove(92);
        selected.Add(98);

        // Continue the 50/50 silhouette mix through levels 101-120. Spikes at
        // 105, 110, 115, and 120 remain rectangular to preserve closed-board flow.
        selected.Add(101);
        selected.Add(102);
        selected.Add(104);
        selected.Add(106);
        selected.Add(108);
        selected.Add(111);
        selected.Add(113);
        selected.Add(114);
        selected.Add(116);
        selected.Add(118);

        // The final 30 levels keep a 50/50 rectangular/custom split. All six
        // hard checkpoints remain rectangular; silhouettes occupy recovery slots.
        selected.Add(121);
        selected.Add(122);
        selected.Add(127);
        selected.Add(126);
        selected.Add(128);
        selected.Add(131);
        selected.Add(133);
        selected.Add(134);
        selected.Add(136);
        selected.Add(138);
        selected.Add(141);
        selected.Add(142);
        selected.Add(144);
        selected.Add(146);
        selected.Add(148);

        // Levels 151-200 keep the established 50/50 silhouette mix. Each
        // ten-level cadence uses five shaped recovery levels while both spikes
        // retain a closed rectangular boundary.
        for (int decadeStart = 151; decadeStart <= 191; decadeStart += 10)
        {
            selected.Add(decadeStart);
            selected.Add(decadeStart + 1);
            selected.Add(decadeStart + 3);
            selected.Add(decadeStart + 5);
            selected.Add(decadeStart + 7);
        }

        return selected;
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
        string name,
        int width,
        int height,
        BoardShape shape,
        int complexityPercent,
        float minimumLengthWeight,
        int attempts,
        int algorithmModeIndex,
        bool isSpike,
        bool isVeryHard,
        int campaignLevel)
    {
        return new LevelProfile
        {
            name = name,
            width = width,
            height = height,
            shape = shape,
            fillPercent = 100,
            minLength = 2,
            maxLength = Math.Max(
                6,
                Mathf.RoundToInt(Mathf.Sqrt(width * height) * Mathf.Lerp(1.45f, 1.68f, complexityPercent / 100f))),
            minimumLengthWeight = minimumLengthWeight,
            attempts = attempts,
            seed = 100003 + campaignLevel * 1009,
            complexityPercent = complexityPercent,
            algorithmModeIndex = algorithmModeIndex,
            isSpike = isSpike,
            isVeryHard = isVeryHard,
            campaignLevel = campaignLevel
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
        Clover,
        TwinBlocks,
        TwinStacks,
        Dumbbell,
        Diamond,
        Oval,
        Staircase,
        LShape,
        Arrowhead
    }

    private static readonly HashSet<int> ShapedCampaignLevels = CreateShapedCampaignLevels();

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
        public int complexityPercent;
        public int algorithmModeIndex;
        public bool isSpike;
        public bool isVeryHard;
        public int campaignLevel;
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
        public int crossHalfDependencyCount;
        public int topBlockedByBottomCount;
        public int bottomBlockedByTopCount;
        public int leftBlockedByRightCount;
        public int rightBlockedByLeftCount;
        public int regionTransitionCount;
        public int longestRegionStreak;
        public int horizontalBacktrackCount;
        public int verticalBacktrackCount;
        public float horizontalSweepRatio;
        public float verticalSweepRatio;
    }
}
