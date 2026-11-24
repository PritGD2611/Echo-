using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Entry points invoked by tools/unity-cli.ps1 through Unity's -executeMethod.
/// Every public static method must exit explicitly so batch-mode runs return a useful exit code.
/// </summary>
public static class EchoCli
{
    private const string Tag = "[EchoCli] ";

    // The cabin commands load a scene, so they are batch-only.
    public static void BuildCabinLevel() => CabinLevelBuilder.Build();
    public static void InspectCabinLevel() => CabinLevelBuilder.Inspect();
    public static void UpdateCabinNight() => CabinLevelBuilder.UpdateNight();
    public static void UpdateCabinLocalLighting() => CabinLevelBuilder.UpdateLocalLighting();

    // ---------------------------------------------------------------- ping

    /// <summary>Health check used by the live bridge (safe while the Editor is open).</summary>
    public static void Ping()
    {
        Log("pong=true");
        Log($"unity={Application.unityVersion}");
        Log($"project={Application.productName}");
        Log($"projectPath={Directory.GetCurrentDirectory()}");
        Log($"isPlaying={EditorApplication.isPlaying}");
        Log($"isCompiling={EditorApplication.isCompiling}");
        Log($"isUpdating={EditorApplication.isUpdating}");
        Log($"compileFailed={EditorUtility.scriptCompilationFailed}");
        Log($"activeScene={SceneManager.GetActiveScene().path}");
        Log($"buildTarget={EditorUserBuildSettings.activeBuildTarget}");
        Log($"time={DateTime.UtcNow:O}");
        Exit(0);
    }

    // ---------------------------------------------------------------- info

    public static void Info()
    {
        var scenes = EditorBuildSettings.scenes;
        Log($"project={Application.productName}");
        Log($"unity={Application.unityVersion}");
        Log($"company={Application.companyName}");
        Log($"platform={Application.platform}");
        Log($"buildTarget={EditorUserBuildSettings.activeBuildTarget}");
        Log($"projectPath={Directory.GetCurrentDirectory()}");
        Log($"scenes={scenes.Length} enabled={scenes.Count(s => s.enabled)}");
        foreach (var scene in scenes)
            Log($"scene={(scene.enabled ? "on " : "off")} {scene.path}");
        Log($"compileFailed={EditorUtility.scriptCompilationFailed}");
        Log($"colorSpace={PlayerSettings.colorSpace}");
        Log($"graphicsApi={string.Join(",", PlayerSettings.GetGraphicsAPIs(EditorUserBuildSettings.activeBuildTarget))}");
        Log($"date={DateTime.UtcNow:O}");
        Exit(0);
    }

    // ------------------------------------------------------------ validate

    public static void Validate()
    {
        var problems = new List<string>();

        if (EditorUtility.scriptCompilationFailed)
            problems.Add("Scripts failed to compile.");

        var enabledScenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        if (enabledScenes.Length == 0)
            problems.Add("No enabled scenes in Build Settings.");

        foreach (var scene in enabledScenes)
        {
            if (!File.Exists(scene.path))
            {
                problems.Add($"Missing scene asset: {scene.path}");
                continue;
            }

            var loaded = EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            var missing = 0;
            foreach (var root in loaded.GetRootGameObjects())
                missing += root.GetComponentsInChildren<Component>(true).Count(c => c == null);

            Log($"scene={scene.path} roots={loaded.rootCount} missingScripts={missing}");
            if (missing > 0)
                problems.Add($"{scene.path} has {missing} missing script reference(s).");
        }

        var duplicates = FindDuplicateTypeNames();
        foreach (var group in duplicates)
            Log($"duplicateTypeName={group.Key} ({group.Count()})");

        FinishValidation(problems);
    }

    /// <summary>
    /// Read-only validation that is safe while the Editor is open: no scenes are
    /// opened. Unresolved script GUIDs are found by scanning scene YAML.
    /// </summary>
    public static void ValidateLive()
    {
        var problems = new List<string>();

        if (EditorUtility.scriptCompilationFailed)
            problems.Add("Scripts failed to compile.");

        var enabledScenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        if (enabledScenes.Length == 0)
            problems.Add("No enabled scenes in Build Settings.");

        foreach (var scene in enabledScenes)
        {
            if (!File.Exists(scene.path))
            {
                problems.Add($"Missing scene asset: {scene.path}");
                continue;
            }

            var guids = Regex
                .Matches(File.ReadAllText(scene.path), @"m_Script:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-fA-F]{32})")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var missing = guids.Count(guid =>
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                return string.IsNullOrEmpty(path) || AssetDatabase.LoadAssetAtPath<MonoScript>(path) == null;
            });

            Log($"scene={scene.path} scriptRefs={guids.Length} missingScripts={missing}");
            if (missing > 0)
                problems.Add($"{scene.path} has {missing} unresolved script reference(s).");
        }

        var duplicates = FindDuplicateTypeNames();
        foreach (var group in duplicates)
            Log($"duplicateTypeName={group.Key} ({group.Count()})");

        FinishValidation(problems);
    }

    private static IEnumerable<IGrouping<string, string>> FindDuplicateTypeNames() =>
        AssetDatabase.FindAssets("t:Script")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal))
            .GroupBy(p => Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

    private static void FinishValidation(List<string> problems)
    {
        if (problems.Count == 0)
        {
            Log("validation=PASS");
            Exit(0);
            return;
        }

        foreach (var problem in problems)
            Log($"PROBLEM {problem}");
        Log("validation=FAIL");
        Exit(1);
    }

    // --------------------------------------------------------------- build

    public static void Build()
    {
        var target = EditorUserBuildSettings.activeBuildTarget;
        var output = ArgValue("-cliOut") ??
                     (target == BuildTarget.Android
                         ? "Builds/Android/Echo.apk"
                         : "Builds/Windows/Echo.exe");

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Log("ERROR no enabled scenes in Build Settings");
            Exit(1);
            return;
        }

        Log($"building target={target} output={output} scenes={scenes.Length}");
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = target,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        foreach (var step in report.steps)
            foreach (var message in step.messages)
                if (message.type == LogType.Error || message.type == LogType.Exception || message.type == LogType.Warning)
                    Log($"BUILD {message.type}: {message.content}");

        Log($"result={summary.result} errors={summary.totalErrors} warnings={summary.totalWarnings} " +
            $"size={summary.totalSize} bytes time={summary.totalTime}");
        Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    // ------------------------------------------------------------- helpers

    /// <summary>Batch entry point: force a full asset reimport.</summary>
    public static void Reimport() => RefreshAssets();

    /// <summary>Live-safe entry point: force a full asset reimport.</summary>
    public static void Refresh() => RefreshAssets();

    private static void RefreshAssets()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Log("refresh=done");
        Exit(0);
    }

    private static string ArgValue(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static void Log(string message) => Debug.Log(Tag + message);

    private static void Exit(int code)
    {
        Log($"exit={code}");
        if (Application.isBatchMode)
            EditorApplication.Exit(code);
    }
}
