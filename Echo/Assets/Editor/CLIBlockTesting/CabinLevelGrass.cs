using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed partial class CabinLevelBuilder
{
    private const string GrassCoverName = "Woodland grass cover";
    private const string GrassPreviewPath = "Logs/cli/CLIBlockTesting/Grass";
    private const float GrassTileSize = 18f;
    private const float GrassSpacing = .315f;

    private static readonly Rect[] GrassTufts =
    {
        GrassAtlasRect(12, 28, 87, 166),
        GrassAtlasRect(106, 23, 202, 171),
        GrassAtlasRect(269, 20, 375, 164),
        GrassAtlasRect(269, 172, 373, 303),
        GrassAtlasRect(270, 307, 374, 433),
        GrassAtlasRect(416, 184, 508, 331)
    };

    private static readonly Rect[] SeedHeads =
    {
        GrassAtlasRect(408, 3, 506, 128),
        GrassAtlasRect(211, 211, 333, 417),
        GrassAtlasRect(336, 131, 511, 417)
    };

    internal static void UpdateGrass()
    {
        Run(() =>
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var level = scene.GetRootGameObjects().Single(root => root.name == LevelName).transform;
            bool canRender = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            if (canRender) RenderGrassPreviews(GrassPreviewPath + "/Before");
            AddGrassCover(level);
            Verify(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + ScenePath);
            AssetDatabase.SaveAssets();
            if (canRender)
            {
                EditorSceneManager.OpenScene(ScenePath);
                RenderGrassPreviews(GrassPreviewPath + "/After");
            }
        });
    }

    private static void AddGrassCover(Transform level)
    {
        var parent = RequiredChild(level, "Conifer woods and cliff outcrops");
        var grass = parent.Find(GrassCoverName) ?? Group(GrassCoverName, parent);
        var terrain = RequiredChild(level, "Terrain and trails");
        var ground = new HashSet<Collider>(terrain.GetComponentsInChildren<MeshCollider>());
        if (ground.Count == 0) throw new InvalidOperationException("The cabin terrain has no mesh colliders.");

        Physics.SyncTransforms();
        Bounds bounds = ground.First().bounds;
        foreach (var collider in ground) bounds.Encapsulate(collider.bounds);
        var material = GrassCoverMaterial();
        var stalkMaterial = GrassStalkMaterial();
        var rng = new System.Random(48061);
        var tileCount = new Vector2Int(Mathf.CeilToInt(bounds.size.x / GrassTileSize),
            Mathf.CeilToInt(bounds.size.z / GrassTileSize));
        int tufts = 0, outerTufts = 0, triangles = 0, chunks = 0, stalks = 0;
        for (int row = 0; row < tileCount.y; row++)
        {
            for (int column = 0; column < tileCount.x; column++)
            {
                float left = bounds.min.x + column * GrassTileSize;
                float back = bounds.min.z + row * GrassTileSize;
                var batch = new Geometry();
                var seedHeads = new Geometry();
                int batchTufts = 0;
                int batchStalks = 0;
                for (float cellZ = back; cellZ < Mathf.Min(back + GrassTileSize, bounds.max.z) - .12f; cellZ += GrassSpacing)
                {
                    for (float cellX = left; cellX < Mathf.Min(left + GrassTileSize, bounds.max.x) - .12f; cellX += GrassSpacing)
                    {
                        float x = cellX + GrassSpacing * Sample(rng, .08f, .90f);
                        float z = cellZ + GrassSpacing * Sample(rng, .08f, .90f);
                        if (x >= bounds.max.x || z >= bounds.max.z) continue;

                        float outerDistance = Rectangle(x, z, 22, 89, -8, 66);
                        float focus = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, 31, outerDistance));
                        float patch = Mathf.Clamp(.71f + 1.43f * Noise(x, z, .125f) + .58f * Noise(x, z, .34f), .29f, 1f);
                        if (rng.NextDouble() > Mathf.Lerp(.27f, 1f, focus) * patch) continue;
                        if (PathDistance(x, z) < 1.40f || GravelDistance(x, z) < .44f) continue;

                        if (!Physics.Raycast(V(x, bounds.max.y + 35, z), Vector3.down, out var hit, bounds.size.y + 40,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || !ground.Contains(hit.collider))
                            continue;
                        if (hit.normal.y < .73f || GroundWeights(x, z, hit.normal).r < .72f) continue;

                        var root = hit.point - Vector3.up * .029f;
                        float height = Sample(rng, .22f, .41f);
                        float heading = Sample(rng, 0, Mathf.PI * 2);
                        var uv = GrassTufts[rng.Next(GrassTufts.Length)];
                        float width = Mathf.Clamp(height * uv.width / uv.height * Sample(rng, 1.12f, 1.36f), .24f, .43f);
                        AddGrassCard(batch, root, hit.normal, heading, width, height, uv);
                        if (rng.NextDouble() < .55f * focus)
                        {
                            var secondary = GrassTufts[rng.Next(GrassTufts.Length)];
                            AddGrassCard(batch, root, hit.normal, heading + Sample(rng, 1.21f, 1.94f),
                                width * .83f, height * .91f, secondary);
                        }
                        float tallPocket = Mathf.Clamp01(.42f + Noise(x + 12, z - 41, .29f) * 1.7f);
                        if (focus > .14f && rng.NextDouble() < .17f * tallPocket)
                        {
                            AddGrassCard(seedHeads, root, hit.normal, heading + .64f, Sample(rng, .21f, .32f),
                                Sample(rng, .43f, .64f), SeedHeads[rng.Next(SeedHeads.Length)]);
                            batchStalks++;
                        }
                        if (focus < .25f) outerTufts++;
                        batchTufts++;
                    }
                }

                string name = $"Woodland grass {column + 1:00}-{row + 1:00}";
                var previousTile = grass.Find(name);
                if (batchTufts == 0)
                {
                    if (previousTile != null) UnityEngine.Object.DestroyImmediate(previousTile.gameObject);
                    continue;
                }
                var mesh = BakeGrassTile(name, batch, seedHeads, batchStalks > 0);
                var renderer = previousTile == null
                    ? MeshObject(name, grass, mesh, material, ShadowCastingMode.Off)
                    : previousTile.GetComponent<MeshRenderer>();
                renderer.GetComponent<MeshFilter>().sharedMesh = mesh;
                renderer.sharedMaterials = batchStalks > 0 ? new[] { material, stalkMaterial } : new[] { material };
                tufts += batchTufts;
                stalks += batchStalks;
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                    triangles += (int)mesh.GetIndexCount(submesh) / 3;
                chunks++;
            }
        }

        if (tufts < 40000 || outerTufts < 8000 || stalks < 1500 || triangles > 280000)
            throw new InvalidOperationException($"Unexpected grass coverage: {tufts} clumps, {outerTufts} outer, " +
                                                $"{stalks} seed heads and {triangles} triangles.");
        Debug.Log($"[EchoCli] Woodland grass: {tufts:N0} clumps over {chunks} batches, " +
                  $"{outerTufts:N0} in outer woods, {stalks:N0} seed-head clumps and {triangles:N0} triangles.");
    }

    private static Material GrassCoverMaterial()
    {
        var source = Load<Material>(Art + "/Forest understory Grass_A.mat");
        var material = new Material(source) { name = "Woodland grass cover" };
        material.SetColor("_BaseColor", new Color32(186, 195, 162, 255));
        material.SetFloat("_Cutoff", .31f);
        return Save(material, material.name + ".mat");
    }

    private static Material GrassStalkMaterial()
    {
        const string atlas = "Assets/TerrainSampleAssets/Textures/Details/Grass_C_";
        var source = Load<Material>(Art + "/Forest understory Grass_A.mat");
        var material = new Material(source) { name = "Woodland grass seed heads" };
        material.SetColor("_BaseColor", new Color32(202, 208, 163, 255));
        material.SetFloat("_Cutoff", .28f);
        material.SetTexture("_BaseMap", Load<Texture2D>(atlas + "BaseColor.tif"));
        material.SetTexture("_BumpMap", Load<Texture2D>(atlas + "Normal.tif"));
        return Save(material, material.name + ".mat");
    }

    private static Mesh BakeGrassTile(string name, Geometry grass, Geometry seedHeads, bool hasSeedHeads)
    {
        if (!hasSeedHeads) return grass.Bake(name);
        var shortGrass = grass.ToMesh(name);
        var tallGrass = seedHeads.ToMesh(name);
        var combined = new Mesh { name = name };
        if (shortGrass.vertexCount + tallGrass.vertexCount > ushort.MaxValue)
            combined.indexFormat = IndexFormat.UInt32;
        combined.CombineMeshes(new[] { new CombineInstance { mesh = shortGrass }, new CombineInstance { mesh = tallGrass } },
            false, false);
        UnityEngine.Object.DestroyImmediate(shortGrass);
        UnityEngine.Object.DestroyImmediate(tallGrass);
        return Save(combined, name + ".asset");
    }

    private static void AddGrassCard(Geometry batch, Vector3 root, Vector3 groundNormal, float heading,
        float width, float height, Rect uv)
    {
        var across = V(Mathf.Cos(heading), 0, Mathf.Sin(heading)) * (width * .5f);
        var rootEdge = across * .84f;
        rootEdge.y = Mathf.Clamp(-Vector3.Dot(rootEdge, groundNormal) / groundNormal.y, -.045f, .045f);
        var rise = V(Mathf.Sin(heading) * height * .11f, height, -Mathf.Cos(heading) * height * .11f);
        var normal = (Vector3.up * .67f + groundNormal * .33f).normalized;
        batch.Quad(root - rootEdge, root + rootEdge, root + across + rise, root - across + rise,
            normal, uv.min, uv.size);
    }

    private static Rect GrassAtlasRect(float left, float top, float right, float bottom) =>
        Rect.MinMaxRect(left / 512, 1 - bottom / 512, right / 512, 1 - top / 512);

    private static float Sample(System.Random rng, float minimum, float maximum) =>
        Mathf.Lerp(minimum, maximum, (float)rng.NextDouble());

    private static void RenderGrassPreviews(string path)
    {
        Directory.CreateDirectory(path);
        Capture(path, "01-west-woods", V(27.7f, Height(27.7f, 27.8f) + 1.74f, 27.8f),
            V(32.3f, Height(32.3f, 35.2f) + .25f, 35.2f), 63);
        Capture(path, "02-trail-edge", V(35.2f, Height(35.2f, 10.2f) + 1.68f, 10.2f),
            V(37.6f, Height(37.6f, 17.3f) + .40f, 17.3f), 65);
        Capture(path, "03-back-woods", V(50.2f, Height(50.2f, 49.5f) + 1.63f, 49.5f),
            V(47.7f, Height(47.7f, 56.4f) + .46f, 56.4f), 64);
        Capture(path, "04-grass-detail", V(27.9f, Height(27.9f, 28.3f) + 1.15f, 28.3f),
            V(30.1f, Height(30.1f, 31.6f) + .18f, 31.6f), 58);
    }
}
