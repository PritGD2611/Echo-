using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed partial class CabinLevelBuilder
{
    private static Vector3 V(float x, float y, float z) => new(x, y, z);

    private sealed class Geometry
    {
        private readonly List<Vector3> vertices = new();
        private readonly List<Vector3> normals = new();
        private readonly List<Vector2> uvs = new();
        private readonly List<int> indices = new();
        private readonly List<Color> colors = new();

        public void Vertex(Vector3 position, Vector3 normal, Vector2 uv, Color color)
        {
            vertices.Add(position);
            normals.Add(normal);
            uvs.Add(uv);
            colors.Add(color);
        }

        public void Triangle(int a, int b, int c)
        {
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uv, Vector2 size)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            Quad(a, b, c, d, normal, uv, size);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 uv, Vector2 size)
        {
            int first = vertices.Count;
            Vertex(a, normal, uv, Color.white);
            Vertex(b, normal, uv + Vector2.right * size.x, Color.white);
            Vertex(c, normal, uv + size, Color.white);
            Vertex(d, normal, uv + Vector2.up * size.y, Color.white);
            Triangle(first, first + 1, first + 2);
            Triangle(first, first + 2, first + 3);
        }

        public void Box(Vector3 center, Vector3 size, Quaternion rotation, Vector2 uv, Vector2 scale)
        {
            Vector3 P(float x, float y, float z) => center + rotation * Vector3.Scale(size, V(x, y, z)) * .5f;
            var a = P(-1, -1, -1);
            var b = P(1, -1, -1);
            var c = P(1, 1, -1);
            var d = P(-1, 1, -1);
            var e = P(-1, -1, 1);
            var f = P(1, -1, 1);
            var g = P(1, 1, 1);
            var h = P(-1, 1, 1);
            var xy = Vector2.Scale(new Vector2(size.x, size.y), scale);
            var xz = Vector2.Scale(new Vector2(size.x, size.z), scale);
            var zy = Vector2.Scale(new Vector2(size.z, size.y), scale);
            Quad(b, a, d, c, uv, xy);
            Quad(e, f, g, h, uv, xy);
            Quad(a, e, h, d, uv, zy);
            Quad(f, b, c, g, uv, zy);
            Quad(h, g, c, d, uv, xz);
            Quad(a, b, f, e, uv, xz);
        }

        public void Tube(Vector3[] centers, float[] radii, int sides, Vector2 uv, Vector2 repeat, bool axialU = false)
        {
            var direction = (centers[^1] - centers[0]).normalized;
            var xAxis = Vector3.Cross(direction, Mathf.Abs(direction.y) > .8f ? Vector3.forward : Vector3.up).normalized;
            var yAxis = Vector3.Cross(direction, xAxis).normalized;
            int first = vertices.Count;
            float distance = 0;
            for (int ring = 0; ring < centers.Length; ring++)
            {
                if (ring > 0) distance += Vector3.Distance(centers[ring], centers[ring - 1]);
                for (int side = 0; side <= sides; side++)
                {
                    float turn = side * Mathf.PI * 2 / sides;
                    var normal = xAxis * Mathf.Cos(turn) + yAxis * Mathf.Sin(turn);
                    var tile = new Vector2(side * repeat.x / sides, distance * repeat.y);
                    if (axialU) tile = new Vector2(tile.y, tile.x);
                    Vertex(centers[ring] + normal * radii[ring], normal, uv + tile, Color.white);
                }
            }

            for (int ring = 0; ring < centers.Length - 1; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    int a = first + ring * (sides + 1) + side;
                    int b = a + sides + 1;
                    Triangle(a, a + 1, b + 1);
                    Triangle(a, b + 1, b);
                }
            }
        }

        public void Cap(Vector3 center, Vector3 normal, float radius, int sides)
        {
            var xAxis = Vector3.Cross(normal, Mathf.Abs(normal.y) > .8f ? Vector3.forward : Vector3.up).normalized;
            var yAxis = Vector3.Cross(normal, xAxis);
            int first = vertices.Count;
            Vertex(center, normal, new Vector2(.5f, .5f), Color.white);
            for (int side = 0; side <= sides; side++)
            {
                float a = side * Mathf.PI * 2 / sides;
                var offset = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vertex(center + (xAxis * offset.x + yAxis * offset.y) * radius, normal,
                    new Vector2(.5f, .5f) + offset * .48f, Color.white);
            }
            for (int side = 0; side < sides; side++) Triangle(first, first + 1 + side, first + 2 + side);
        }

        public Mesh Bake(string name, bool tangents = true)
        {
            var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            if (tangents) mesh.RecalculateTangents();
            return Save(mesh, name + ".asset");
        }
    }

    private sealed class Parts
    {
        private readonly Dictionary<Material, Geometry> geometry = new();
        private readonly Transform parent;
        private readonly string name;
        public Transform Parent => parent;

        public Parts(Transform parent, string name)
        {
            this.parent = parent;
            this.name = name;
        }

        public Geometry With(Material material)
        {
            if (!geometry.TryGetValue(material, out var builder)) geometry.Add(material, builder = new Geometry());
            return builder;
        }

        public void Box(Material material, Vector3 center, Vector3 size, bool solid = false, Quaternion rotation = default,
            float repeatU = .55f, float repeatV = .55f, Vector2 uv = default)
        {
            if (rotation == default) rotation = Quaternion.identity;
            With(material).Box(center, size, rotation, uv + TextureOffset(center), new Vector2(repeatU, repeatV));
            if (solid) BoxCollider(parent, center, size, rotation);
        }

        public void Beam(Material material, Vector3 a, Vector3 b, float width, float depth, bool solid = false)
        {
            Vector3 direction = b - a;
            Box(material, (a + b) * .5f, V(width, direction.magnitude, depth), solid,
                Quaternion.FromToRotation(Vector3.up, direction.normalized));
        }

        public void Round(Material material, Vector3 a, Vector3 b, float radius, float tip, int sides,
            Material caps = null, bool solid = false, float axialRepeat = .72f, bool axialU = false)
        {
            With(material).Tube(new[] { a, b }, new[] { radius, tip }, sides, TextureOffset(a),
                new Vector2(.88f, axialRepeat), axialU);
            Vector3 direction = (b - a).normalized;
            With(caps ?? material).Cap(a, -direction, radius, sides);
            With(caps ?? material).Cap(b, direction, tip, sides);
            if (solid) Capsule(parent, a, b, Mathf.Min(radius, tip) * .94f);
        }

        public void Finish(ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            int index = 0;
            foreach (var pair in geometry)
            {
                var mesh = pair.Value.Bake(name + " " + ++index);
                MeshObject(pair.Key.name, parent, mesh, pair.Key,
                    pair.Key.renderQueue >= (int)RenderQueue.Transparent ? ShadowCastingMode.Off : shadows);
            }
        }

        private static Vector2 TextureOffset(Vector3 point)
        {
            return new Vector2(
                Mathf.Repeat(point.x * .317f + point.z * .129f + point.y * .85f, 1),
                Mathf.Repeat(point.z * .241f + point.x * .193f + point.y * .397f, 1));
        }
    }

    private static void Capsule(Transform parent, Vector3 a, Vector3 b, float radius)
    {
        var item = Group("Trunk collision", parent);
        item.localPosition = (a + b) * .5f;
        item.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        var collider = item.gameObject.AddComponent<CapsuleCollider>();
        collider.radius = radius;
        collider.height = (b - a).magnitude + radius * 2;
    }

    private static Texture2D MakeEndGrain()
    {
        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Log end grain" };
        var colors = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x / (float)size - .53f, y / (float)size - .46f);
                float angle = Mathf.Atan2(p.y, p.x);
                float radius = p.magnitude * 1.05f + Mathf.Sin(angle * 4 + p.magnitude * 15) * .012f;
                float rings = Mathf.Pow(.5f + .5f * Mathf.Sin(radius * 285 + Mathf.Sin(radius * 50) * 2.8f), 5);
                float split = Mathf.Pow(Mathf.Max(0, Mathf.Cos(angle * 5f + radius * 3.8f)), 70) *
                    Mathf.SmoothStep(0, 1, (radius - .19f) * 4);
                float grain = Mathf.PerlinNoise(x * .17f, y * .17f);
                float light = .88f - .26f * rings - .45f * split - .19f * grain;
                colors[y * size + x] = new Color(light, light * .86f, light * .72f);
            }
        }
        texture.SetPixels32(colors);
        texture.Apply(true, true);
        return Save(texture, "Log end grain.asset");
    }

    private static Texture2D MakeSoftParticle()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = "Soft particle",
            wrapMode = TextureWrapMode.Clamp
        };
        var colors = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var uv = new Vector2(x / (size - 1f) * 2 - 1, y / (size - 1f) * 2 - 1);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - uv.magnitude), 1.8f);
                colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels32(colors);
        texture.Apply(true, true);
        return Save(texture, "Soft particle.asset");
    }
}
