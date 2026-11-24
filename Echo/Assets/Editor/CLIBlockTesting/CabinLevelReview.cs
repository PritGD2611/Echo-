using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StarterAssets;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

internal sealed partial class CabinLevelBuilder
{
    private static void Verify(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var level = roots.Single(root => root.name == LevelName);
        var archive = roots.Single(root => root.name == BlockmeshName);
        if (archive.activeSelf || archive.transform.childCount < 20)
            throw new InvalidOperationException("Original scene geometry was not archived intact.");
        if (level.GetComponentsInChildren<Camera>().Length != 1)
            throw new InvalidOperationException("Expected one active exploration camera.");
        var player = level.GetComponentInChildren<FirstPersonController>();
        if (player == null || player.GetComponent<CharacterController>() == null)
            throw new InvalidOperationException("First-person controls are missing.");

        var meshes = level.GetComponentsInChildren<MeshFilter>();
        var renderers = level.GetComponentsInChildren<Renderer>();
        var materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Append(RenderSettings.skybox).Distinct().ToArray();
        var invalidMaterials = materials.Where(material => material == null || material.shader == null ||
            !material.shader.isSupported || ShaderUtil.ShaderHasError(material.shader)).ToArray();
        if (invalidMaterials.Length > 0)
            throw new InvalidOperationException("Unsupported materials: " +
                string.Join(", ", invalidMaterials.Select(material => material == null ? "(missing)" : material.name)));

        Physics.SyncTransforms();
        VerifyNight(level.transform);
        CheckSurface("Trailhead", Spawn.x, Spawn.y, Height(Spawn.x, Spawn.y), .13f);
        CheckSurface("Front porch", 58.2f, 20.76f, Floor, .055f);
        CheckSurface("Cabin bedroom", 57.85f, 28.75f, Floor, .055f);
        CheckSurface("Backyard exit", 52.89f, 41.40f, Terrace, .10f);
        CheckSurface("Restroom court", 72.75f, 22.86f, Terrace, .10f);
        CheckSurface("Campfire court", 73.72f, 34.31f, Terrace, .10f);
        CheckSurface("BBQ deck", 73.62f, 39.15f, Terrace + .81f, .055f);
        CheckSurface("West storage", 46.83f, 28.79f, Terrace + .16f, .055f);
        CheckEntrance("Front", V(54.65f, Floor + 1.15f, 21.28f), Vector3.forward);
        CheckEntrance("Backyard", V(54.63f, Floor + 1.15f, 35.28f), Vector3.back);
        CheckEntrance("Restroom", V(67.15f, Floor + 1.15f, 24.15f), Vector3.left);
        CheckEntrance("Campfire", V(67.15f, Floor + 1.15f, 32.38f), Vector3.left);
        CheckEntrance("Bedroom", V(63.82f, Floor + 1.15f, 27.03f), Vector3.left);
        CheckStair("Main approach", new Vector2(45.31f, 17.08f), new Vector2(45.31f, 19.88f), 2.48f, Terrace);
        CheckStair("Cabin west porch", new Vector2(50.32f, 20.94f), new Vector2(52.37f, 20.94f), Terrace, Floor);
        CheckStair("Cabin east porch", new Vector2(68.72f, 20.98f), new Vector2(66.64f, 20.98f), Terrace, Floor);
        CheckStair("Restroom entrance", new Vector2(68.71f, 24.15f), new Vector2(66.59f, 24.15f), Terrace, Floor);
        CheckStair("Campfire entrance", new Vector2(68.71f, 32.38f), new Vector2(66.59f, 32.38f), Terrace, Floor);
        CheckStair("Cabin backyard", new Vector2(54.63f, 36.74f), new Vector2(54.63f, 34.66f), Terrace, Floor);
        CheckStair("BBQ deck", new Vector2(73.63f, 36.44f), new Vector2(73.63f, 37.76f), Terrace, Terrace + .81f);

        var groups = level.GetComponentsInChildren<LODGroup>();
        var lodMeshes = new HashSet<MeshFilter>();
        var triangleCounts = new Dictionary<Mesh, long>();
        long Count(MeshFilter filter)
        {
            var mesh = filter.sharedMesh;
            if (mesh == null) throw new InvalidOperationException("Missing mesh on " + filter.name);
            if (triangleCounts.TryGetValue(mesh, out long count)) return count;
            for (int i = 0; i < mesh.subMeshCount; i++) count += mesh.GetIndexCount(i) / 3;
            triangleCounts.Add(mesh, count);
            return count;
        }

        var lodTriangles = new long[3];
        foreach (var group in groups)
        {
            var lods = group.GetLODs();
            if (lods.Length != 3) throw new InvalidOperationException("Incomplete tree LOD on " + group.name);
            for (int i = 0; i < lods.Length; i++)
            {
                foreach (var renderer in lods[i].renderers)
                {
                    if (!renderer.TryGetComponent<MeshFilter>(out var mesh)) continue;
                    lodMeshes.Add(mesh);
                    lodTriangles[i] += Count(mesh);
                }
            }
        }

        long staticTriangles = meshes.Where(mesh => !lodMeshes.Contains(mesh)).Sum(Count);
        int missingScripts = level.GetComponentsInChildren<Transform>()
            .Sum(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject));
        if (missingScripts > 0) throw new InvalidOperationException("Missing scripts in completed scene.");
        Debug.Log($"[EchoCli] Cabin level verified. Archived roots={archive.transform.childCount} " +
                  $"colliders={level.GetComponentsInChildren<Collider>().Length} trees={groups.Length} " +
                  $"staticTriangles={staticTriangles:N0} treeTrianglesByLODTier={string.Join("/", lodTriangles)}");
    }

    private static void CheckSurface(string label, float x, float z, float expected, float tolerance)
    {
        var origin = V(x, expected + .23f, z);
        if (!Physics.Raycast(origin, Vector3.down, out var hit, tolerance + .23f) ||
            Mathf.Abs(hit.point.y - expected) > tolerance || hit.normal.y < .75f)
            throw new InvalidOperationException($"Walkable surface check failed for {label}: expected height {expected:F3}.");
    }

    private static void CheckEntrance(string label, Vector3 origin, Vector3 toward)
    {
        if (Physics.SphereCast(origin, .31f, toward, out var hit, 1.51f, ~0, QueryTriggerInteraction.Ignore))
            throw new InvalidOperationException($"{label} doorway clearance is blocked by {hit.collider.transform.parent.name}.");
    }

    private static void CheckStair(string label, Vector2 bottom, Vector2 top, float from, float to)
    {
        float last = from;
        for (int i = 0; i <= 50; i++)
        {
            var p = Vector2.Lerp(bottom, top, i / 50f);
            if (!Physics.Raycast(V(p.x, to + .25f, p.y), Vector3.down, out var hit, to - from + .6f) ||
                hit.point.y - last > .224f || hit.point.y - last < -.06f)
                throw new InvalidOperationException($"Stair check failed for {label} at {p}: height {hit.point.y:F3}, prior {last:F3}.");
            last = hit.point.y;
        }
        if (Mathf.Abs(last - to) > .21f) throw new InvalidOperationException("Incomplete stair: " + label);
    }

    private static void RenderPreviews()
    {
        const string path = "Logs/cli/CLIBlockTesting";
        Directory.CreateDirectory(path);
        foreach (var particle in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            particle.Simulate(3.82f, false, true, true);

        Capture(path, "01-trailhead", V(Spawn.x, Height(Spawn.x, Spawn.y) + 1.7f, Spawn.y),
            V(40.3f, 4.87f, 23.25f), 63);
        Capture(path, "02-log-cabin", V(50.18f, 7.27f, 16.25f), V(59.24f, 8.81f, 24.1f), 58);
        Capture(path, "03-campfire-and-bbq", V(69.7f, 6.63f, 28.04f), V(73.32f, 5.64f, 37.49f), 59);
        Capture(path, "04-restroom-court", V(69.25f, 6.57f, 23.02f), V(75.02f, 5.83f, 22.05f), 55);
        Capture(path, "05-storage-and-backyard", V(49.8f, 6.56f, 34.52f), V(51.8f, 5.94f, 39.73f), 70);
        Capture(path, "06-bedroom", V(62.04f, 8.10f, 27.02f), V(55.97f, 7.46f, 29.45f), 66);
        Capture(path, "07-compound-overview", V(58.1f, 43.5f, 4.2f), V(56.57f, 5.5f, 29.5f), 62);
        Capture(path, "08-storage-door-night", V(51.78f, 6.92f, 31.58f), V(47.78f, 6.21f, 28.04f), 65);
        Capture(path, "09-approach-wall-night", V(45.28f, 5.98f, 17.69f), V(47.24f, 6.55f, 23.51f), 71);
    }

    private static void Capture(string path, string name, Vector3 eye, Vector3 target, float fieldOfView)
    {
        var item = new GameObject("Temporary level preview");
        var camera = item.AddComponent<Camera>();
        item.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye, Vector3.up));
        camera.fieldOfView = fieldOfView;
        camera.nearClipPlane = .06f;
        camera.farClipPlane = 170;
        camera.allowHDR = true;

        const int width = 1440, height = 900;
        var buffer = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        var former = RenderTexture.active;
        bool fog = RenderSettings.fog;
        try
        {
            if (name.Contains("overview")) RenderSettings.fog = false;
            camera.targetTexture = buffer;
            camera.Render();
            RenderTexture.active = buffer;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(path, name + ".png"), image.EncodeToPNG());
        }
        finally
        {
            RenderSettings.fog = fog;
            camera.targetTexture = null;
            RenderTexture.active = former;
            Object.DestroyImmediate(buffer);
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(item);
        }
    }
}
