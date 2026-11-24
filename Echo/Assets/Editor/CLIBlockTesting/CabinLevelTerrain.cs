using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed partial class CabinLevelBuilder
{
    private static readonly Vector2[] LowerTrail = Trail(
        new(34.0f, -7), new(34.4f, -2), new(35.8f, 4.4f), Spawn, new(35.4f, 14.6f),
        new(36.3f, 19.6f), new(35.9f, 24.7f), new(36.7f, 32.7f), new(36.4f, 39.8f),
        new(35.8f, 47.0f), new(34, 53), new(33.3f, 60), new(29.8f, 67));
    private static readonly Vector2[] Approach = Trail(
        new(35.4f, 14.6f), new(38.7f, 14.2f), new(41.6f, 14.7f), new(44.5f, 15.5f),
        new(45.3f, 17.05f), new(45.3f, 19.9f), new(47.3f, 20.9f), new(51, 21));
    private static readonly Vector2[] ExitTrail = Trail(
        new(54.6f, 36.8f), new(52.9f, 40.6f), new(51.3f, 43.3f), new(51.9f, 46.3f),
        new(50, 50.1f), new(46.5f, 54), new(47.2f, 59.5f), new(43.8f, 64), new(45, 72));

    private static Vector2[] Trail(params Vector2[] knots)
    {
        var points = new List<Vector2>();
        for (int i = 0; i < knots.Length - 1; i++)
        {
            var a = knots[Mathf.Max(0, i - 1)];
            var b = knots[i];
            var c = knots[i + 1];
            var d = knots[Mathf.Min(knots.Length - 1, i + 2)];
            for (int j = 0; j < 5; j++)
            {
                float t = j / 5f;
                points.Add(.5f * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t * t +
                    (-a + 3 * b - 3 * c + d) * t * t * t));
            }
        }
        points.Add(knots[^1]);
        return points.ToArray();
    }

    private static float TrailDistance(Vector2 position, Vector2[] trail)
    {
        float squared = float.MaxValue;
        for (int i = 1; i < trail.Length; i++)
        {
            var start = trail[i - 1];
            var direction = trail[i] - start;
            float along = Mathf.Clamp01(Vector2.Dot(position - start, direction) / direction.sqrMagnitude);
            squared = Mathf.Min(squared, (position - start - direction * along).sqrMagnitude);
        }
        return Mathf.Sqrt(squared);
    }

    private static float PathDistance(float x, float z)
    {
        var point = new Vector2(x, z);
        return Mathf.Min(TrailDistance(point, LowerTrail), Mathf.Min(TrailDistance(point, Approach), TrailDistance(point, ExitTrail)));
    }

    private static float Rectangle(float x, float z, float left, float right, float bottom, float top)
    {
        float dx = Mathf.Max(left - x, x - right);
        float dz = Mathf.Max(bottom - z, z - top);
        return Mathf.Min(Mathf.Max(dx, dz), 0) + new Vector2(Mathf.Max(dx, 0), Mathf.Max(dz, 0)).magnitude;
    }

    private static float TerraceDistance(float x, float z)
    {
        return Mathf.Min(Rectangle(x, z, 39.27f, 77.45f, 19.94f, 42.91f),
            Rectangle(x, z, 66.7f, 77.45f, 17.97f, 42.91f));
    }

    private static float LowerHeight(float z)
    {
        if (z < 10.6f) return Mathf.Lerp(-.28f, 2.48f, Mathf.InverseLerp(-7, 10.6f, z));
        if (z < 20) return 2.48f;
        if (z < 33) return Mathf.Lerp(2.48f, 2.93f, Mathf.InverseLerp(20, 29, z));
        if (z < 44) return Mathf.Lerp(2.93f, 3.23f, Mathf.InverseLerp(33, 44, z));
        return Mathf.Lerp(3.23f, 4.95f, Mathf.InverseLerp(44, 60, z));
    }

    private static float Noise(float x, float z, float frequency) =>
        Mathf.PerlinNoise(x * frequency + 31.4f, z * frequency + 58.7f) - .5f;

    private static float Height(float x, float z)
    {
        float rugged = Noise(x, z, .063f) * 4.5f + Noise(x, z, .176f) * 1.55f + Noise(x, z, .48f) * .44f;
        float outside = .28f + Noise(x + 173, z - 12, .019f) * 5.7f + rugged;
        float lowerRoad = Mathf.Min(TrailDistance(new Vector2(x, z), LowerTrail),
            Rectangle(x, z, 39.8f, 49.2f, 11.3f, 19.8f) + .8f);
        float lowerBlend = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.5f, 8.7f, lowerRoad));
        float terrain = Mathf.Lerp(outside, LowerHeight(z) + Noise(x, z, .47f) * .11f, lowerBlend);

        float distance = TerraceDistance(x, z);
        float nearEdge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.24f, 2.7f, distance))
            * (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(3, 5.5f, distance)));
        float amount = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.12f, 4.05f, distance +
            Noise(x, z, .41f) * .67f * nearEdge));
        float summit = Terrace + Noise(x, z, .75f) * .09f;
        terrain = Mathf.Lerp(terrain, summit, amount) + Noise(x + 23, z, .88f) * nearEdge * .24f;

        if (z >= 16.3f && z < 20.03f)
        {
            float approachCut = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.94f, 1.85f, Mathf.Abs(x - 45.3f)));
            float stairBed = Mathf.Lerp(2.49f, Terrace, Mathf.InverseLerp(17.0f, 19.98f, z)) - .18f;
            terrain = Mathf.Lerp(terrain, Mathf.Min(terrain, stairBed), approachCut);
        }

        float behind = TrailDistance(new Vector2(x, z), ExitTrail);
        float exitBlend = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.3f, 4.5f, behind))) *
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(42.7f, 46, z));
        float exitHeight = Mathf.Lerp(Terrace, 2.2f, Mathf.InverseLerp(43, 62, z));
        return Mathf.Lerp(terrain, exitHeight + Noise(x, z, .42f) * .13f, exitBlend);
    }

    private static float GravelDistance(float x, float z)
    {
        float eastYard = Rectangle(x, z, 66.48f, 76.65f, 18.7f, 41.98f);
        float backyard = Rectangle(x, z, 43.15f, 69.2f, 35.17f, 41.95f);
        float storage = Rectangle(x, z, 44.0f, 52.7f, 24.1f, 35.7f);
        return Mathf.Min(eastYard, Mathf.Min(backyard, storage));
    }

    private static Color GroundWeights(float x, float z, Vector3 normal)
    {
        float irregular = Noise(x, z, 1.27f) * .52f + Noise(x + 21, z, .37f) * .87f;
        float path = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.58f, 1.68f, PathDistance(x, z) + irregular));
        float gravel = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.45f, .82f, GravelDistance(x, z) + irregular));
        float rock = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.56f, .84f, normal.y + irregular * .11f))) *
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.2f, 3.3f, PathDistance(x, z)));
        float dirt = (1 - gravel) * path;
        return new Color((1 - rock) * (1 - gravel - dirt), (1 - rock) * dirt, (1 - rock) * gravel, rock);
    }

    private static float[] Axis(float start, float innerStart, float innerEnd, float end)
    {
        var grid = new List<float>();
        void AddRange(float a, float b, float step)
        {
            int count = Mathf.CeilToInt((b - a) / step);
            for (int i = 0; i < count; i++) grid.Add(Mathf.Lerp(a, b, i / (float)count));
        }
        AddRange(start, innerStart, 2.1f);
        AddRange(innerStart, innerEnd, .48f);
        AddRange(innerEnd, end, 2.1f);
        grid.Add(end);
        return grid.ToArray();
    }

    private void BuildGround(Transform parent)
    {
        var terrain = new Material(FindShader("Echo/Cabin Terrain")) { name = "Continuous woodland ground" };
        foreach (var pair in new[] { ("Grass", "Grass"), ("Path", "Pathway"), ("Gravel", "Gravel"), ("Rock", "Rock") })
        {
            var surface = surfaces[pair.Item2];
            terrain.SetTexture("_" + pair.Item1 + "Color", surface.Color);
            terrain.SetTexture("_" + pair.Item1 + "Normal", surface.Normal);
        }
        terrain.SetTexture("_NoiseMap", Load<Texture2D>("Assets/Generated/Textures/EchoNoise.png"));
        terrain = Save(terrain, "Continuous woodland ground.mat");

        var xs = Axis(-16, 25, 86, 134);
        var zs = Axis(-38, -10, 64, 111);
        var vertices = new Vector3[xs.Length, zs.Length];
        var normals = new Vector3[xs.Length, zs.Length];
        var weights = new Color[xs.Length, zs.Length];
        const float gap = .25f;
        for (int j = 0; j < zs.Length; j++)
        {
            for (int i = 0; i < xs.Length; i++)
            {
                float x = xs[i], z = zs[j];
                vertices[i, j] = V(x, Height(x, z), z);
                normals[i, j] = V(Height(x - gap, z) - Height(x + gap, z), 2 * gap,
                    Height(x, z - gap) - Height(x, z + gap)).normalized;
                weights[i, j] = GroundWeights(x, z, normals[i, j]);
            }
        }

        const int chunk = 29;
        for (int z = 0; z < zs.Length - 1; z += chunk)
        {
            for (int x = 0; x < xs.Length - 1; x += chunk)
            {
                int width = Mathf.Min(chunk, xs.Length - 1 - x) + 1;
                int depth = Mathf.Min(chunk, zs.Length - 1 - z) + 1;
                var buffer = new Geometry();
                for (int j = 0; j < depth; j++)
                {
                    for (int i = 0; i < width; i++)
                        buffer.Vertex(vertices[x + i, z + j], normals[x + i, z + j], Vector2.zero, weights[x + i, z + j]);
                }
                for (int j = 0; j < depth - 1; j++)
                {
                    for (int i = 0; i < width - 1; i++)
                    {
                        int a = j * width + i;
                        buffer.Triangle(a, a + width, a + 1);
                        buffer.Triangle(a + 1, a + width, a + width + 1);
                    }
                }

                string name = $"Ground tile {x / chunk + 1:00}-{z / chunk + 1:00}";
                var mesh = buffer.Bake(name, false);
                var renderer = MeshObject(name, parent, mesh, terrain, ShadowCastingMode.Off);
                renderer.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }
    }
}
