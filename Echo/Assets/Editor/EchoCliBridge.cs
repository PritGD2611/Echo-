using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Lets tools/unity-cli.ps1 drive the *running* Editor by dropping request files
/// into Temp/echo-cli/queue. The Editor polls the queue from EditorApplication.update,
/// so no second Unity instance (and no project lock) is needed.
///
/// Requests:  { "id": "...", "method": "Info", "args": "" }
/// Responses: { "id", "method", "ok", "exitCode", "error", "output": [...], "durationMs" }
/// </summary>
[InitializeOnLoad]
public static class EchoCliBridge
{
    private const double PollIntervalSeconds = 0.2;

    private static readonly HashSet<string> AllowedMethods = new HashSet<string>(StringComparer.Ordinal)
    {
        nameof(EchoCli.Ping),
        nameof(EchoCli.Info),
        nameof(EchoCli.ValidateLive),
        nameof(EchoCli.Refresh),
        "EchoSceneBuilder.AddColliders",
        "EchoSceneBuilder.ApplySurfaceDetail",
        "EchoSceneBuilder.RenderSurfaceCheck",
        "EchoSceneBuilder.RenderMainPreview",
        "EchoSceneBuilder.VerifyChanges"
    };

    private static double _lastPoll;
    private static bool _handling;
    private static readonly List<string> Output = new List<string>();

    [Serializable]
    private class Request
    {
        public string id;
        public string method;
        public string args;
    }

    [Serializable]
    private class Response
    {
        public string id;
        public string method;
        public bool ok;
        public int exitCode;
        public string error;
        public string[] output;
        public long durationMs;
    }

    static EchoCliBridge()
    {
        if (Application.isBatchMode) return;
        EditorApplication.update += Tick;
    }

    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    private static string QueueDir => Path.Combine(ProjectRoot, "Temp", "echo-cli", "queue");

    private static void Tick()
    {
        if (Application.isBatchMode || _handling) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.timeSinceStartup - _lastPoll < PollIntervalSeconds) return;
        _lastPoll = EditorApplication.timeSinceStartup;

        try
        {
            if (!Directory.Exists(QueueDir)) return;
            var requests = Directory.GetFiles(QueueDir, "*.req.json");
            if (requests.Length == 0) return;

            Array.Sort(requests, StringComparer.Ordinal);
            Handle(requests[0]);
        }
        catch (Exception e)
        {
            // Never let bridge plumbing break the Editor loop.
            Debug.LogWarning("[EchoCliBridge] " + e.Message);
        }
    }

    private static void Handle(string requestPath)
    {
        _handling = true;
        var started = DateTime.UtcNow;
        Request request = null;
        var response = new Response { ok = false, exitCode = 1 };

        try
        {
            request = JsonUtility.FromJson<Request>(File.ReadAllText(requestPath));
            if (request == null || string.IsNullOrEmpty(request.id))
                throw new InvalidDataException("Malformed request file: " + requestPath);

            response.id = request.id;
            response.method = request.method;

            if (!AllowedMethods.Contains(request.method ?? string.Empty))
                throw new InvalidOperationException(
                    $"Method '{request.method}' is not allowed in live mode. Allowed: {string.Join(", ", AllowedMethods)}");

            Output.Clear();
            Application.logMessageReceived += Capture;
            try
            {
                Dispatch(request.method);
            }
            finally
            {
                Application.logMessageReceived -= Capture;
            }

            response.ok = true;
            response.exitCode = 0;
            response.error = null;
        }
        catch (Exception e)
        {
            response.ok = false;
            response.exitCode = 1;
            response.error = e.GetType().Name + ": " + e.Message;
            Output.Add("[EchoCli] EXCEPTION " + e);
        }
        finally
        {
            Application.logMessageReceived -= Capture;
            response.output = Output.ToArray();
            response.durationMs = (long)(DateTime.UtcNow - started).TotalMilliseconds;

            try
            {
                if (response.id == null)
                    response.id = Path.GetFileName(requestPath).Replace(".req.json", string.Empty);
                if (response.method == null && request != null)
                    response.method = request.method;

                var responsePath = Path.Combine(QueueDir, response.id + ".res.json");
                var tempPath = responsePath + ".tmp";
                File.WriteAllText(tempPath, JsonUtility.ToJson(response), new UTF8Encoding(false));
                if (File.Exists(responsePath)) File.Delete(responsePath);
                File.Move(tempPath, responsePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[EchoCliBridge] Could not write response: " + e.Message);
            }

            try { File.Delete(requestPath); }
            catch { /* picked up next tick otherwise */ }

            _handling = false;
        }
    }

    private static void Dispatch(string method)
    {
        switch (method)
        {
            case nameof(EchoCli.Ping): EchoCli.Ping(); break;
            case nameof(EchoCli.Info): EchoCli.Info(); break;
            case nameof(EchoCli.ValidateLive): EchoCli.ValidateLive(); break;
            case nameof(EchoCli.Refresh): EchoCli.Refresh(); break;
            case "EchoSceneBuilder.AddColliders": EchoSceneBuilder.AddColliders(); break;
            case "EchoSceneBuilder.ApplySurfaceDetail": EchoSceneBuilder.ApplySurfaceDetail(); break;
            case "EchoSceneBuilder.RenderSurfaceCheck": EchoSceneBuilder.RenderSurfaceCheck(); break;
            case "EchoSceneBuilder.RenderMainPreview": EchoSceneBuilder.RenderMainPreview(); break;
            case "EchoSceneBuilder.VerifyChanges": EchoSceneBuilder.VerifyChanges(); break;
            default: throw new InvalidOperationException("Unhandled method: " + method);
        }
    }

    private static void Capture(string condition, string stackTrace, LogType type)
    {
        if (condition != null && condition.StartsWith("[EchoCli]", StringComparison.Ordinal))
            Output.Add(condition);
    }
}




