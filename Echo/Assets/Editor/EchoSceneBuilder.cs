using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Random = System.Random;

/// <summary>
/// Builds the foggy forest scene in Assets/Scenes/CLITesting.unity from the
/// Poly Haven texture sets in Assets/Assets/Material.
///
/// Run with:
///   tools\unity-cli.cmd exec EchoSceneBuilder.BuildAll
/// </summary>
public static class EchoSceneBuilder
{
    private const string TexRoot = "Assets/Assets/Material";
    private const string MaterialDir = "Assets/Assets/Material/Materials";
    private const string GeneratedDir = "Assets/Generated";
    private const string MeshDir = "Assets/Generated/Meshes";
    private const string ScenePath = "Assets/Scenes/CLITesting.unity";
    private const string VolumePath = "Assets/Generated/EchoForestVolume.asset";
    private const string PreviewPath = "Logs/cli/preview-forest.png";

    private const string BarkDiff = "eucalyptus_bark_4k/textures/eucalyptus_bark_diff_4k.jpg";
    private const string BarkNor = "eucalyptus_bark_4k/textures/eucalyptus_bark_nor_dx_4k.jpg";
    private const string BarkArm = "eucalyptus_bark_4k/textures/eucalyptus_bark_arm_4k.jpg";
    private const string RockDiff = "rocky_terrain_03_4k/textures/rocky_terrain_03_diff_4k.jpg";
    private const string RockNor = "rocky_terrain_03_4k/textures/rocky_terrain_03_nor_dx_4k.jpg";
    private const string RockArm = "rocky_terrain_03_4k/textures/rocky_terrain_03_arm_4k.jpg";

    // ------------------------------------------------------------ entry

    [MenuItem("Echo/Build Forest Scene")]
    public static void BuildAll()
    {
        try
        {
            PrepareTextures();
            AssetDatabase.Refresh();

            var noise = BuildNoiseTexture();

            var bark = BuildMaterial(
                "EchoBark",
                BarkDiff, BarkNor, BarkArm,
                new Color(0.48f, 0.48f, 0.52f),
                1.15f, 0.85f, 0f, 0.85f, 0.20f, 0.25f, noise, false);

            var rock = BuildMaterial(
                "EchoRockyTerrain",
                RockDiff, RockNor, RockArm,
                new Color(0.95f, 0.97f, 1.00f),
                0.11f, 1.0f, 1.0f, 1.0f, 0.30f, 0.55f, noise, false);

            var groundMat = BuildMaterial(
                "EchoRockyGround",
                RockDiff, RockNor, RockArm,
                new Color(0.85f, 0.87f, 0.92f),
                0.075f, 0.95f, 1.0f, 1.0f, 0.38f, 0.85f, noise, true);

            var groundMesh = BuildGroundMesh();
            var trees = BuildTreeMeshes();
            var rocks = BuildRockMeshes();
            var debris = BuildDebrisMesh();

            BuildScene(groundMesh, trees, rocks, debris, bark, rock, groundMat);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RenderPreviewInternal();
            RenderFrom(new Vector3(0f, 70f, -70f), new Vector3(0f, 0f, 10f), "Logs/cli/preview-top.png", 70f);
            Log("build=PASS");
            Exit(0);
        }
        catch (Exception e)
        {
            Log("BUILD EXCEPTION " + e);
            Exit(1);
        }
    }

    [MenuItem("Echo/Environment/Add Colliders")]
    public static void AddColliders()
    {
        try
        {
            var scene = SceneManager.GetActiveScene();
            var forest = GameObject.Find("Forest");
            if (forest == null)
            {
                Log("colliders=FAIL no 'Forest' root in " + scene.path);
                Exit(1);
                return;
            }

            if (EditorApplication.isPlaying)
            {
                Log("colliders=SKIPPED exit Play Mode first");
                Exit(2);
                return;
            }

            var groundCount = 0;
            var treeCount = 0;
            var rockCount = 0;

            foreach (var mf in forest.GetComponentsInChildren<MeshFilter>(true))
            {
                var go = mf.gameObject;
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;

                if (go.name == "Ground")
                {
                    AddStaticMeshCollider(go, mesh);
                    groundCount++;
                }
                else if (go.name.StartsWith("Tree_", StringComparison.Ordinal))
                {
                    // Exact trunk/branch silhouette instead of a rough capsule.
                    var capsule = go.GetComponent<CapsuleCollider>();
                    if (capsule != null) UnityEngine.Object.DestroyImmediate(capsule);
                    AddStaticMeshCollider(go, mesh);
                    treeCount++;
                }
                else if (go.name.StartsWith("Rock_", StringComparison.Ordinal) ||
                         go.name.StartsWith("HeroRock_", StringComparison.Ordinal))
                {
                    AddStaticMeshCollider(go, mesh);
                    rockCount++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            var playerStatus = FixPlayerInternal();
            var saved = EditorSceneManager.SaveScene(scene);
            Log($"colliders ground={groundCount} trees={treeCount} rocks={rockCount} saved={saved}");

            foreach (var mesh in forest.GetComponentsInChildren<MeshFilter>(true)
                         .Where(m => m.gameObject.name.StartsWith("Tree_", StringComparison.Ordinal))
                         .Select(m => m.sharedMesh)
                         .Where(m => m != null)
                         .Distinct())
            {
                Log($"treeCollisionMesh {mesh.name} verts={mesh.vertexCount} tris={mesh.triangles.Length / 3}");
            }
            Log("player " + playerStatus);
            Exit(0);
        }
        catch (Exception e)
        {
            Log("COLLIDER EXCEPTION " + e);
            Exit(1);
        }
    }

    /// <summary>
    /// Player fix: a CharacterController must not share the object with a Rigidbody
    /// or a CapsuleCollider, and the capsule must not start inside the terrain.
    /// </summary>
    private static string FixPlayerInternal()
    {
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player == null) return "not found";

        var go = player.gameObject;

        var body = go.GetComponent<Rigidbody>();
        if (body != null) UnityEngine.Object.DestroyImmediate(body);

        var capsule = go.GetComponent<CapsuleCollider>();
        if (capsule != null) UnityEngine.Object.DestroyImmediate(capsule);

        var controller = go.GetComponent<CharacterController>();
        if (controller == null) return "missing CharacterController";

        var p = go.transform.position;
        var ray = new Ray(new Vector3(p.x, 400f, p.z), Vector3.down);
        RaycastHit hit = default;
        var found = false;

        var ground = GameObject.Find("Ground");
        var groundCollider = ground != null ? ground.GetComponent<MeshCollider>() : null;
        if (groundCollider != null) found = groundCollider.Raycast(ray, out hit, 1000f);
        if (!found) found = Physics.Raycast(ray, out hit, 1000f, ~0, QueryTriggerInteraction.Ignore);
        if (!found) return "ground raycast missed";

        // Capsule bottom relative to the pivot; put it just above the terrain.
        var bottomOffset = controller.center.y - controller.height * 0.5f;
        var y = hit.point.y - bottomOffset + 0.05f;
        go.transform.position = new Vector3(p.x, y, p.z);

        return $"placed groundY={hit.point.y:F2} playerY={y:F2} rb/capsuleCollider removed";
    }

    private static void CreatePlayer(Vector3 position, Quaternion rotation)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Player";
        go.transform.position = new Vector3(position.x, GroundHeight(position.x, position.z) + 1.02f, position.z);
        go.transform.rotation = rotation;

        // CharacterController only: a Rigidbody + CapsuleCollider on the same
        // object fights the controller and freezes movement.
        var capsule = go.GetComponent<CapsuleCollider>();
        if (capsule != null) UnityEngine.Object.DestroyImmediate(capsule);
        var body = go.GetComponent<Rigidbody>();
        if (body != null) UnityEngine.Object.DestroyImmediate(body);

        var controller = go.AddComponent<CharacterController>();
        controller.center = Vector3.zero;
        controller.height = 2f;
        controller.radius = 0.5f;
        controller.slopeLimit = 45f;
        controller.stepOffset = 0.3f;
        controller.skinWidth = 0.08f;

        go.AddComponent<PlayerController>();
    }

    private static void AddStaticMeshCollider(GameObject go, Mesh mesh)
    {
        var collider = go.GetComponent<MeshCollider>();
        if (collider == null) collider = go.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.convex = false;
    }

    [MenuItem("Echo/Render Preview")]
    public static void RenderPreview()
    {
        try
        {
            RenderPreviewInternal();
            Exit(0);
        }
        catch (Exception e)
        {
            Log("PREVIEW EXCEPTION " + e);
            Exit(1);
        }
    }

    /// <summary>Diagnostics: scene stats plus a top-down render.</summary>
    public static void RenderDiagnostic()
    {
        try
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Log($"roots={scene.rootCount}");

            foreach (var root in scene.GetRootGameObjects())
                Log($"root={root.name} children={root.transform.childCount}");

            var ground = GameObject.Find("Ground");
            if (ground != null)
            {
                var mf = ground.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh != null)
                {
                    var normals = mesh.normals;
                    Log($"ground verts={mesh.vertexCount} tris={mesh.triangles.Length / 3} " +
                        $"bounds={mesh.bounds.size} normal0={normals[0]}");
                }
                var mr = ground.GetComponent<MeshRenderer>();
                Log($"ground material={(mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : "none")} " +
                    $"shader={(mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.shader.name : "none")}");
            }
            else Log("ground object NOT FOUND");

            var cam = Camera.main;
            if (cam != null)
            {
                Log($"camera={cam.transform.position} rot={cam.transform.eulerAngles}");
                var all = new List<(string, float, Vector3, Vector3)>();
                foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (t == cam.transform) continue;
                    var d = Vector3.Distance(t.position, cam.transform.position);
                    if (d < 20f) all.Add((t.name, d, t.position, t.localScale));
                }
                foreach (var item in all.OrderBy(a => a.Item2).Take(8))
                    Log($"near={item.Item1} dist={item.Item2:F2} pos={item.Item3} scale={item.Item4}");
            }

            RenderFrom(new Vector3(0f, 70f, -70f), new Vector3(0f, 0f, 10f), "Logs/cli/preview-top.png", 70f);
            Log("diagnostic=PASS");
            Exit(0);
        }
        catch (Exception e)
        {
            Log("DIAG EXCEPTION " + e);
            Exit(1);
        }
    }

    private static void RenderFrom(Vector3 position, Vector3 lookAt, string relativePath, float fov)
    {
        var go = new GameObject("DiagCamera");
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.11f, 0.13f);
        cam.fieldOfView = fov;
        cam.farClipPlane = 500f;
        go.transform.position = position;
        go.transform.LookAt(lookAt);

        const int width = 1400;
        const int height = 900;
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        var previousActive = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;

        var tex = new Texture2D(width, height, TextureFormat.RGB24, false, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        var absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? ".");
        File.WriteAllBytes(absolute, tex.EncodeToPNG());

        cam.targetTexture = null;
        RenderTexture.active = previousActive;
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.DestroyImmediate(go);
        Log("render=" + absolute);
    }

    public static void PrepareTextures()
    {
        SetupTexture(BarkDiff, false, false);
        SetupTexture(BarkNor, false, true);
        SetupTexture(BarkArm, true, false);
        SetupTexture(RockDiff, false, false);
        SetupTexture(RockNor, false, true);
        SetupTexture(RockArm, true, false);
        Log("textures=prepared");
    }

    // --------------------------------------------------------- textures

    private static void SetupTexture(string relative, bool linear, bool normalMap)
    {
        var path = TexRoot + "/" + relative;
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
        {
            Log("texture missing: " + path);
            return;
        }

        var type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        var changed = importer.textureType != type || importer.sRGBTexture == linear;

        importer.textureType = type;
        importer.sRGBTexture = !linear;
        importer.maxTextureSize = 2048;
        importer.anisoLevel = 4;
        importer.wrapMode = TextureWrapMode.Repeat;

        if (changed || !File.Exists(path + ".meta"))
            importer.SaveAndReimport();
        else
            importer.SaveAndReimport();
    }

    private static Texture2D LoadTexture(string relative)
    {
        var path = TexRoot + "/" + relative;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) throw new FileNotFoundException("Texture not found: " + path);
        return tex;
    }

    // --------------------------------------------------------- materials

    private static Material BuildMaterial(
        string name,
        string diff, string nor, string arm,
        Color tint, float scale, float smoothness, float metallic, float ao,
        float macroStrength, float warp, Texture2D noise, bool twoScale)
    {
        var shader = Shader.Find("Echo/Forest Triplanar");
        if (shader == null) throw new InvalidOperationException("Shader 'Echo/Forest Triplanar' not found.");

        var mat = new Material(shader) { name = name };
        mat.SetTexture("_BaseMap", LoadTexture(diff));
        mat.SetTexture("_NormalMap", LoadTexture(nor));
        mat.SetTexture("_ArmMap", LoadTexture(arm));
        mat.SetTexture("_NoiseMap", noise);
        mat.SetColor("_BaseColor", tint);
        mat.SetFloat("_BaseScale", scale);
        mat.SetFloat("_DetailScale", 0.37f);
        mat.SetFloat("_DetailBlend", 0.45f);
        mat.SetFloat("_NormalScale", 1.0f);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_AoStrength", ao);
        mat.SetFloat("_MacroScale", 0.05f);
        mat.SetFloat("_MacroStrength", macroStrength);
        mat.SetFloat("_Warp", warp);
        mat.SetFloat("_InstanceVariation", 1.0f);
        if (twoScale) mat.EnableKeyword("_TWO_SCALE");
        else mat.DisableKeyword("_TWO_SCALE");
        mat.enableInstancing = true;

        var path = MaterialDir + "/" + name + ".mat";
        CreateAsset(mat, path);
        return mat;
    }

    // ------------------------------------------------------ noise texture

    private static Texture2D BuildNoiseTexture()
    {
        const string folder = GeneratedDir + "/Textures";
        const string path = folder + "/EchoNoise.png";
        const int size = 256;

        EnsureFolder(folder);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        var pixels = new Color[size * size];

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var u = x / (float)size;
                var v = y / (float)size;

                var r = PeriodicFbm(u, v, 4, 1);
                var g = PeriodicFbm(u, v, 4, 2);
                var b = PeriodicFbm(u, v, 9, 3);

                pixels[y * size + x] = new Color(r, g, b, 1f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        var absolute = Path.GetFullPath(path);
        File.WriteAllBytes(absolute, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 256;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
        }

        var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (loaded == null) throw new FileNotFoundException("Noise texture not found: " + path);
        return loaded;
    }

    private static float PeriodicFbm(float u, float v, int cells, int seed)
    {
        float sum = 0f;
        float amplitude = 0.5f;
        float total = 0f;

        for (var octave = 0; octave < 3; octave++)
        {
            sum += PeriodicNoise(u, v, cells, seed + octave * 17) * amplitude;
            total += amplitude;
            amplitude *= 0.5f;
            cells *= 2;
        }

        return sum / total;
    }

    private static float PeriodicNoise(float u, float v, int cells, int seed)
    {
        var x = u * cells;
        var y = v * cells;

        var x0 = Mathf.FloorToInt(x);
        var y0 = Mathf.FloorToInt(y);
        var fx = x - x0;
        var fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);

        var xa = ((x0 % cells) + cells) % cells;
        var ya = ((y0 % cells) + cells) % cells;
        var xb = (xa + 1) % cells;
        var yb = (ya + 1) % cells;

        var n00 = LatticeHash(xa, ya, seed);
        var n10 = LatticeHash(xb, ya, seed);
        var n01 = LatticeHash(xa, yb, seed);
        var n11 = LatticeHash(xb, yb, seed);

        var nx0 = Mathf.Lerp(n00, n10, fx);
        var nx1 = Mathf.Lerp(n01, n11, fx);
        return Mathf.Lerp(nx0, nx1, fy);
    }

    private static float LatticeHash(int x, int y, int seed)
    {
        unchecked
        {
            var h = x * 374761393 + y * 668265263 + seed * 1442695040;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / (float)0x7fffffff;
        }
    }

    // ----------------------------------------------------------- meshes

    private const float GroundSize = 340f;
    private const int GroundRes = 240;

    private static Mesh BuildGroundMesh()
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        var half = GroundSize * 0.5f;
        var step = GroundSize / GroundRes;

        for (var z = 0; z <= GroundRes; z++)
        {
            for (var x = 0; x <= GroundRes; x++)
            {
                var px = -half + x * step;
                var pz = -half + z * step;
                verts.Add(new Vector3(px, GroundHeight(px, pz), pz));
                uvs.Add(new Vector2(x / (float)GroundRes, z / (float)GroundRes));
            }
        }

        for (var z = 0; z < GroundRes; z++)
        {
            for (var x = 0; x < GroundRes; x++)
            {
                var i = z * (GroundRes + 1) + x;
                tris.Add(i);
                tris.Add(i + GroundRes + 1);
                tris.Add(i + 1);
                tris.Add(i + 1);
                tris.Add(i + GroundRes + 1);
                tris.Add(i + GroundRes + 2);
            }
        }

        var mesh = new Mesh { name = "Ground_Forest" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        var minY = verts.Min(v => v.y);
        var maxY = verts.Max(v => v.y);
        var normals = mesh.normals;
        Log($"groundRange min={minY:F2} max={maxY:F2} normal0={normals[0]} " +
            $"g00={GroundHeight(0, 0):F2} gcam={GroundHeight(-2.21f, -26f):F2} g_tree={GroundHeight(6.9f, -41f):F2} " +
            $"perlin00={Mathf.PerlinNoise(0, 0):F3} perlin11={Mathf.PerlinNoise(1, 1):F3}");

        CreateAsset(mesh, MeshDir + "/Ground_Forest.asset");
        return mesh;
    }

    private static float GroundHeight(float x, float z)
    {
        var broad = (Mathf.PerlinNoise(x * 0.018f + 11.3f, z * 0.018f + 4.7f) - 0.5f) * 3.2f;
        var mid = (Mathf.PerlinNoise(x * 0.07f + 3.1f, z * 0.07f + 8.9f) - 0.5f) * 0.9f;
        var fine = (Mathf.PerlinNoise(x * 0.31f, z * 0.31f) - 0.5f) * 0.22f;

        // Path meanders along +Z and sits slightly lower than the banks.
        var pathX = Mathf.Sin(z * 0.03f) * 5f;
        var pathMask = 1f - Smooth01(3.0f, 9.0f, Mathf.Abs(x - pathX));
        var bank = Smooth01(4.0f, 20.0f, Mathf.Abs(x - pathX)) * 1.2f;

        return broad + mid + fine + bank - pathMask * 0.45f;
    }

    private static Dictionary<string, Mesh> BuildTreeMeshes()
    {
        var trees = new Dictionary<string, Mesh>();

        void Make(string name, int seed, float height, float radius, int maxDepth, float branchSpread)
        {
            var rng = new Random(seed);
            var data = new TubeMeshData();
            var start = Vector3.zero;
            GrowTrunk(data, start, Vector3.up, height, radius, 0, maxDepth, branchSpread, rng);

            var mesh = data.ToMesh(name);
            CreateAsset(mesh, MeshDir + "/" + name + ".asset");
            trees[name] = mesh;
        }

        Make("Tree_Bare_A", 101, 13.5f, 0.30f, 3, 1.0f);
        Make("Tree_Bare_B", 202, 11.0f, 0.24f, 3, 1.15f);
        Make("Tree_Bare_C", 303, 15.5f, 0.36f, 3, 0.9f);
        Make("Tree_Bare_Sapling", 404, 5.5f, 0.12f, 2, 1.25f);

        return trees;
    }

    private static void GrowTrunk(
        TubeMeshData data, Vector3 start, Vector3 dir, float length, float radius,
        int depth, int maxDepth, float spread, Random rng)
    {
        var steps = depth == 0 ? 12 : Mathf.Max(3, 9 - depth * 2);
        var sides = depth == 0 ? 9 : Mathf.Max(3, 9 - depth * 2);

        var centers = new List<Vector3>();
        var radii = new List<float>();

        var p = start;
        var d = dir.normalized;
        var topRadius = radius * (depth == 0 ? 0.36f : 0.22f);

        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            centers.Add(p);
            radii.Add(Mathf.Lerp(radius, topRadius, t));

            var jitter = new Vector3(
                (float)(rng.NextDouble() - 0.5),
                (float)(rng.NextDouble() - 0.5) * 0.35f,
                (float)(rng.NextDouble() - 0.5));

            d += jitter * (depth == 0 ? 0.10f : 0.20f);
            d.y += depth == 0 ? 0.015f : 0.05f;
            d.Normalize();
            p += d * (length / steps);
        }

        data.AddTube(centers, radii, sides, uvTilesPerMeter: depth == 0 ? 0.55f : 1.4f);

        if (depth >= maxDepth) return;

        var branchCount = depth == 0 ? rng.Next(6, 9) : depth == 1 ? rng.Next(3, 5) : rng.Next(2, 4);

        for (var b = 0; b < branchCount; b++)
        {
            var at = Mathf.Clamp01(0.35f + (float)rng.NextDouble() * 0.6f);
            var index = Mathf.Clamp(Mathf.RoundToInt(at * steps), 1, steps);

            var side = new Vector3(
                (float)(rng.NextDouble() - 0.5),
                (float)(rng.NextDouble() - 0.5) * 0.4f,
                (float)(rng.NextDouble() - 0.5)).normalized;

            var childDir = (Vector3.up * 0.75f + side * spread).normalized;
            var childLength = length * Mathf.Lerp(0.34f, 0.60f, (float)rng.NextDouble());
            var childRadius = radii[index] * Mathf.Lerp(0.55f, 0.8f, (float)rng.NextDouble());

            GrowTrunk(data, centers[index], childDir, childLength, childRadius,
                depth + 1, maxDepth, spread * 1.1f, rng);
        }
    }

    private static Dictionary<string, Mesh> BuildRockMeshes()
    {
        var rocks = new Dictionary<string, Mesh>();

        void Make(string name, int seed, float roughness, float flatten)
        {
            var mesh = BuildIcoRock(name, seed, roughness, flatten);
            CreateAsset(mesh, MeshDir + "/" + name + ".asset");
            rocks[name] = mesh;
        }

        Make("Rock_Boulder_A", 11, 0.34f, 0.85f);
        Make("Rock_Boulder_B", 22, 0.42f, 0.65f);
        Make("Rock_Boulder_C", 33, 0.28f, 1.0f);
        Make("Rock_Slab_A", 44, 0.22f, 0.22f);
        Make("Rock_Slab_B", 55, 0.30f, 0.15f);

        return rocks;
    }

    private static Mesh BuildIcoRock(string name, int seed, float roughness, float flatten)
    {
        var t = (1f + Mathf.Sqrt(5f)) / 2f;
        var verts = new List<Vector3>
        {
            new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
            new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
            new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1)
        };

        var faces = new[]
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };

        var (meshVerts, meshFaces) = Subdivide(verts, faces, 2);
        var rng = new Random(seed);

        for (var i = 0; i < meshVerts.Count; i++)
        {
            var v = meshVerts[i].normalized;
            var noise = Mathf.PerlinNoise(v.x * 2.7f + seed * 0.37f, v.z * 2.7f + v.y * 1.9f) - 0.5f;
            var bump = 1f + noise * roughness * 2f + (float)(rng.NextDouble() - 0.5) * roughness * 0.12f;
            v *= bump;
            v.y *= flatten;
            meshVerts[i] = v;
        }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(meshVerts);
        mesh.SetTriangles(meshFaces, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static (List<Vector3> verts, List<int> faces) Subdivide(List<Vector3> verts, int[] faces, int iterations)
    {
        var current = verts;
        var currentFaces = faces.ToList();

        for (var it = 0; it < iterations; it++)
        {
            var midpoints = new Dictionary<long, int>();
            var nextFaces = new List<int>();

            int Mid(int a, int b)
            {
                var key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (midpoints.TryGetValue(key, out var existing)) return existing;
                var mid = ((current[a] + current[b]) * 0.5f).normalized;
                current.Add(mid);
                midpoints[key] = current.Count - 1;
                return current.Count - 1;
            }

            for (var i = 0; i < currentFaces.Count; i += 3)
            {
                var a = currentFaces[i];
                var b = currentFaces[i + 1];
                var c = currentFaces[i + 2];
                var ab = Mid(a, b);
                var bc = Mid(b, c);
                var ca = Mid(c, a);

                nextFaces.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            currentFaces = nextFaces;
        }

        return (current, currentFaces);
    }

    private static Mesh BuildDebrisMesh()
    {
        var data = new TubeMeshData();
        var rng = new Random(777);
        for (var i = 0; i < 6; i++)
        {
            var start = new Vector3((float)(rng.NextDouble() - 0.5) * 8f, 0.06f, (float)(rng.NextDouble() - 0.5) * 8f);
            var dir = new Vector3((float)(rng.NextDouble() - 0.5), 0.05f, 1f).normalized;
            var points = new List<Vector3>();
            var radii = new List<float>();
            var p = start;
            for (var s = 0; s < 5; s++)
            {
                points.Add(p);
                radii.Add(Mathf.Lerp(0.07f, 0.025f, s / 4f));
                p += dir * 0.9f + new Vector3((float)(rng.NextDouble() - 0.5) * 0.2f, 0, 0);
            }
            data.AddTube(points, radii, 5, 2f);
        }

        var mesh = data.ToMesh("Debris_Branches");
        CreateAsset(mesh, MeshDir + "/Debris_Branches.asset");
        return mesh;
    }

    // ------------------------------------------------------------ scene

    private static void BuildScene(
        Mesh ground, Dictionary<string, Mesh> trees, Dictionary<string, Mesh> rocks,
        Mesh debris, Material bark, Material rock, Material groundMat)
    {
        // A rebuild replaces the whole scene, so keep the player the user added.
        var existingPlayer = UnityEngine.Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        var hadPlayer = existingPlayer != null;
        var playerPos = hadPlayer ? existingPlayer.transform.position : Vector3.zero;
        var playerRot = hadPlayer ? existingPlayer.transform.rotation : Quaternion.identity;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var forest = new GameObject("Forest");
        var treeRoot = new GameObject("Trees").transform;
        var rockRoot = new GameObject("Rocks").transform;
        var debrisRoot = new GameObject("Debris").transform;
        treeRoot.SetParent(forest.transform);
        rockRoot.SetParent(forest.transform);
        debrisRoot.SetParent(forest.transform);

        // Ground ------------------------------------------------------
        var groundGo = new GameObject("Ground");
        groundGo.transform.SetParent(forest.transform);
        groundGo.AddComponent<MeshFilter>().sharedMesh = ground;
        var groundRenderer = groundGo.AddComponent<MeshRenderer>();
        groundRenderer.sharedMaterial = groundMat;
        groundRenderer.shadowCastingMode = ShadowCastingMode.On;
        groundRenderer.receiveShadows = true;
        AddStaticMeshCollider(groundGo, ground);

        var rng = new Random(24601);
        var placed = new List<Vector3>();

        // Keep the spawn area around the camera clear so the view opens down the path.
        var cameraStart = new Vector3(pathXAt(-70f), 0f, -70f);

        // Trees -------------------------------------------------------
        var treeNames = trees.Keys.OrderBy(k => k).ToArray();
        var placedCount = 0;
        var attempts = 0;
        while (placedCount < 700 && attempts < 40000)
        {
            attempts++;
            var z = (float)(rng.NextDouble() * 2 - 1) * 155f;
            var side = rng.NextDouble() < 0.5 ? -1f : 1f;
            var offset = 6f + (float)Math.Pow(rng.NextDouble(), 0.85) * 82f;
            var x = pathXAt(z) + side * offset;

            if (Mathf.Abs(x) > 160f) continue;
            if ((new Vector3(x, 0f, z) - cameraStart).sqrMagnitude < 144f) continue;
            if (placed.Any(p => (p - new Vector3(x, 0, z)).sqrMagnitude < 10.9f)) continue;

            placed.Add(new Vector3(x, 0, z));

            var name = treeNames[rng.Next(treeNames.Length)];
            var mesh = trees[name];
            var go = new GameObject(name);
            go.transform.SetParent(treeRoot);
            go.transform.position = new Vector3(x, GroundHeight(x, z) - 0.15f, z);
            go.transform.rotation = Quaternion.Euler(
                (float)(rng.NextDouble() - 0.5) * 6f,
                (float)rng.NextDouble() * 360f,
                (float)(rng.NextDouble() - 0.5) * 7f);
            var s = Mathf.Lerp(0.8f, 1.45f, (float)rng.NextDouble());
            go.transform.localScale = new Vector3(s, s * Mathf.Lerp(0.9f, 1.2f, (float)rng.NextDouble()), s);

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = bark;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            AddStaticMeshCollider(go, mesh);
            placedCount++;
        }

        // Rocks -------------------------------------------------------
        var rockNames = rocks.Keys.OrderBy(k => k).ToArray();
        var pathRocks = rockNames.Where(n => n.Contains("Slab")).ToArray();
        for (var i = 0; i < 260; i++)
        {
            var alongPath = i < 120;
            float x, z;
            if (alongPath)
            {
                z = (float)(rng.NextDouble() * 2 - 1) * 150f;
                x = pathXAt(z) + (float)(rng.NextDouble() * 2 - 1) * 4.6f;
            }
            else
            {
                x = (float)(rng.NextDouble() * 2 - 1) * 155f;
                z = (float)(rng.NextDouble() * 2 - 1) * 155f;
                if (Mathf.Abs(x - pathXAt(z)) < 4f) continue;
            }

            if ((new Vector3(x, 0f, z) - cameraStart).sqrMagnitude < 100f) continue;

            var name = alongPath && rng.NextDouble() < 0.55
                ? pathRocks[rng.Next(pathRocks.Length)]
                : rockNames[rng.Next(rockNames.Length)];
            var go = new GameObject(name);
            go.transform.SetParent(rockRoot);
            go.transform.position = new Vector3(x, GroundHeight(x, z) - 0.05f, z);
            go.transform.rotation = Quaternion.Euler(
                (float)(rng.NextDouble() - 0.5) * 22f,
                (float)rng.NextDouble() * 360f,
                (float)(rng.NextDouble() - 0.5) * 22f);

            var s = Mathf.Lerp(0.35f, 1.9f, (float)Math.Pow(rng.NextDouble(), 2.2));
            go.transform.localScale = new Vector3(s, s * Mathf.Lerp(0.55f, 1.1f, (float)rng.NextDouble()), s);

            go.AddComponent<MeshFilter>().sharedMesh = rocks[name];
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = rock;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            AddStaticMeshCollider(go, rocks[name]);
        }

        // Hero boulders framing the path ------------------------------
        var heroRocks = new[]
        {
            new Vector4(-7.5f, -50f, 2.4f, 0f),
            new Vector4(6.8f, -32f, 2.1f, 0f),
            new Vector4(-8.5f, -12f, 2.8f, 0f),
            new Vector4(7.5f, 12f, 2.2f, 0f),
            new Vector4(-6.0f, 36f, 2.6f, 0f),
            new Vector4(9.0f, 62f, 2.0f, 0f)
        };

        for (var i = 0; i < heroRocks.Length; i++)
        {
            var hero = heroRocks[i];
            var name = i % 2 == 0 ? "Rock_Boulder_A" : "Rock_Boulder_C";
            var heroX = pathXAt(hero.y) + hero.x;
            var go = new GameObject("HeroRock_" + i);
            go.transform.SetParent(rockRoot);
            go.transform.position = new Vector3(heroX, GroundHeight(heroX, hero.y) - 0.15f, hero.y);
            go.transform.rotation = Quaternion.Euler(-4f, i * 63f, 3f);
            go.transform.localScale = new Vector3(hero.z, hero.z * 0.75f, hero.z * 1.15f);
            go.AddComponent<MeshFilter>().sharedMesh = rocks[name];
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = rock;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            AddStaticMeshCollider(go, rocks[name]);
        }

        // Scene budget --------------------------------------------------
        long totalVerts = 0;
        long totalTris = 0;
        var renderers = forest.GetComponentsInChildren<MeshFilter>();
        foreach (var mf in renderers)
        {
            if (mf.sharedMesh == null) continue;
            totalVerts += mf.sharedMesh.vertexCount;
            totalTris += mf.sharedMesh.triangles.Length / 3;
        }
        Log($"budget renderers={renderers.Length} verts={totalVerts} tris={totalTris}");

        // Camera ------------------------------------------------------
        var cameraX = pathXAt(-70f);
        var cameraZ = -70f;
        var cameraY = GroundHeight(cameraX, cameraZ) + 1.95f;

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.fieldOfView = 58f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 170f;
        camGo.transform.position = new Vector3(cameraX, cameraY, cameraZ);
        camGo.transform.rotation = Quaternion.Euler(4.0f, 2.5f, 0f);
        cam.gameObject.AddComponent<AudioListener>();

        var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        camData.antialiasingQuality = AntialiasingQuality.High;

        // Light -------------------------------------------------------
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.72f, 0.82f, 1.0f);
        light.intensity = 1.45f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.9f;
        light.shadowBias = 0.03f;
        light.shadowNormalBias = 0.9f;
        lightGo.transform.rotation = Quaternion.Euler(22f, 158f, 0f);

        // Volume ------------------------------------------------------
        var profile = BuildVolumeProfile();
        var volumeGo = new GameObject("Global Volume");
        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0;
        volume.weight = 1f;
        volume.sharedProfile = profile;

        // Lighting / fog ---------------------------------------------
        RenderSettings.skybox = null;
        RenderSettings.sun = light;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.024f;
        RenderSettings.fogColor = new Color(0.36f, 0.43f, 0.55f);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.44f, 0.50f, 0.60f);
        RenderSettings.ambientEquatorColor = new Color(0.28f, 0.32f, 0.40f);
        RenderSettings.ambientGroundColor = new Color(0.09f, 0.09f, 0.11f);
        RenderSettings.ambientIntensity = 1.85f;

        cam.backgroundColor = new Color(0.40f, 0.48f, 0.60f);

        RenderSettings.reflectionIntensity = 0.7f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

        if (hadPlayer) CreatePlayer(playerPos, playerRot);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        var camPos = new Vector3(cameraX, cameraY, cameraZ);
        foreach (var group in new[] { treeRoot, rockRoot })
        {
            foreach (var t in group.GetComponentsInChildren<Transform>()
                         .Where(t => t != group)
                         .OrderBy(t => Vector3.Distance(t.position, camPos))
                         .Take(3))
            {
                Log($"near={t.name} dist={Vector3.Distance(t.position, camPos):F2} " +
                    $"pos={t.position} scale={t.localScale} rot={t.eulerAngles}");
            }
        }
        Log($"camera={camPos} groundY={GroundHeight(cameraX, cameraZ):F2}");

        Log($"scene={ScenePath} trees={placedCount} rocks=260");
    }

    private static float pathXAt(float z) => Mathf.Sin(z * 0.03f) * 5f;

    /// <summary>Standard 0..1 smoothstep (Mathf.SmoothStep lerps between from/to instead).</summary>
    private static float Smooth01(float edge0, float edge1, float x)
    {
        var t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    private static VolumeProfile BuildVolumeProfile()
    {
        DeleteAsset(VolumePath);

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, VolumePath);

        void Add<T>(Action<T> configure) where T : VolumeComponent
        {
            var component = profile.Add<T>(true);
            configure(component);
            AssetDatabase.AddObjectToAsset(component, profile);
        }

        Add<Tonemapping>(c =>
        {
            c.mode.Override(TonemappingMode.ACES);
        });

        Add<ColorAdjustments>(c =>
        {
            c.postExposure.Override(0.85f);
            c.contrast.Override(12f);
            c.saturation.Override(-12f);
            c.colorFilter.Override(new Color(0.84f, 0.91f, 1.0f));
        });

        Add<Bloom>(c =>
        {
            c.threshold.Override(0.95f);
            c.intensity.Override(0.55f);
            c.scatter.Override(0.75f);
            c.tint.Override(new Color(0.62f, 0.72f, 0.95f));
        });

        Add<Vignette>(c =>
        {
            c.intensity.Override(0.34f);
            c.smoothness.Override(0.45f);
            c.color.Override(new Color(0.05f, 0.06f, 0.1f));
        });

        Add<FilmGrain>(c =>
        {
            c.type.Override(FilmGrainLookup.Thin1);
            c.intensity.Override(0.22f);
            c.response.Override(0.8f);
        });

        // Depth of field intentionally omitted: it costs a full-screen pass and the
        // fog already provides the depth cue.

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    // ---------------------------------------------------------- preview

    private static void RenderPreviewInternal()
    {
        const int width = 1600;
        const int height = 900;

        var cam = Camera.main;
        if (cam == null)
        {
            var tagged = GameObject.FindGameObjectWithTag("MainCamera");
            cam = tagged != null ? tagged.GetComponent<Camera>() : null;
        }
        if (cam == null) throw new InvalidOperationException("No main camera in the scene.");

        var previousTarget = cam.targetTexture;
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        rt.Create();

        var previousActive = RenderTexture.active;
        try
        {
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();


            var absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "..", PreviewPath));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? ".");
            File.WriteAllBytes(absolute, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            Log("preview=" + absolute);
        }
        finally
        {
            cam.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }

    // ---------------------------------------------------------- helpers

    private sealed class TubeMeshData
    {
        private readonly List<Vector3> _verts = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Vector2> _uvs = new();
        private readonly List<Vector4> _tangents = new();
        private readonly List<int> _tris = new();

        public void AddTube(List<Vector3> centers, List<float> radii, int sides, float uvTilesPerMeter)
        {
            if (centers.Count < 2) return;

            var baseIndex = _verts.Count;
            var distance = 0f;

            for (var i = 0; i < centers.Count; i++)
            {
                var prev = centers[Mathf.Max(i - 1, 0)];
                var next = centers[Mathf.Min(i + 1, centers.Count - 1)];
                var forward = (next - prev).sqrMagnitude > 1e-8f ? (next - prev).normalized : Vector3.up;

                var reference = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
                var right = Vector3.Cross(forward, reference).normalized;
                var up = Vector3.Cross(right, forward).normalized;

                if (i > 0) distance += Vector3.Distance(centers[i], centers[i - 1]);
                var v = distance * uvTilesPerMeter;

                for (var s = 0; s < sides; s++)
                {
                    var angle = s / (float)sides * Mathf.PI * 2f;
                    var radial = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                    _verts.Add(centers[i] + radial * radii[i]);
                    _normals.Add(radial);
                    _uvs.Add(new Vector2(s / (float)sides, v));
                    _tangents.Add(new Vector4(forward.x, forward.y, forward.z, 1f));
                }

                if (i == 0) continue;

                var ring = baseIndex + i * sides;
                var previousRing = ring - sides;
                for (var s = 0; s < sides; s++)
                {
                    var nextSide = (s + 1) % sides;
                    _tris.Add(previousRing + s);
                    _tris.Add(ring + s);
                    _tris.Add(previousRing + nextSide);

                    _tris.Add(previousRing + nextSide);
                    _tris.Add(ring + s);
                    _tris.Add(ring + nextSide);
                }
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = _verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetTangents(_tangents);
            mesh.SetTriangles(_tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    private static void CreateAsset(UnityEngine.Object asset, string path)
    {
        EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/') ?? GeneratedDir);
        DeleteAsset(path);
        AssetDatabase.CreateAsset(asset, path);
    }

    private static void DeleteAsset(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            AssetDatabase.DeleteAsset(path);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        var parts = folder.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void Log(string message) => Debug.Log("[EchoScene] " + message);

    private static void Exit(int code)
    {
        Log("exit=" + code);
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }
}









