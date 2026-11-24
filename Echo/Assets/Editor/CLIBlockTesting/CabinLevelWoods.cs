using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed partial class CabinLevelBuilder
{
    private sealed class TreeForm
    {
        public Mesh[] Trunk = new Mesh[3];
        public Mesh[] Crown = new Mesh[3];
        public float Height;
        public float Radius;
    }

    private void BuildWoods(Transform parent)
    {
        var stand = Group("Pines and firs with distance LODs", parent);
        var forms = new[]
        {
            MakeTree("Scots pine", 3520, 14.3f, .31f, true),
            MakeTree("Tall pine", 8897, 18.5f, .37f, true),
            MakeTree("Balsam fir", 8257, 13.1f, .26f, false),
            MakeTree("Silver fir", 1883, 17.7f, .35f, false),
            MakeTree("Fir sapling", 5772, 6.2f, .124f, false)
        };
        var reserved = new List<Vector2>();
        void Tree(float x, float z, int variant, float size)
        {
            var point = new Vector2(x, z);
            reserved.Add(point);
            PlaceTree(stand, forms[variant], x, z, size, Range(0, 360));
        }

        Tree(31.65f, 15.55f, 0, 1.13f);
        Tree(50.12f, 8.63f, 3, .97f);
        Tree(44.21f, 5.56f, 2, 1.13f);
        Tree(40.47f, 3.37f, 1, .92f);
        Tree(33.19f, 10.92f, 1, .91f);
        Tree(37.98f, 11.2f, 0, .88f);
        Tree(32.15f, 22.68f, 1, .88f);
        Tree(41.39f, 17.20f, 0, .94f);
        Tree(38.24f, 25.26f, 1, .87f);
        Tree(40.37f, 33.39f, 0, 1.06f);
        Tree(41.88f, 39.36f, 2, .96f);
        Tree(46.44f, 40.98f, 1, .92f);
        Tree(58.31f, 43.74f, 3, .87f);
        Tree(75.13f, 44.11f, 1, .91f);
        Tree(78.99f, 40.73f, 2, .98f);
        Tree(78.47f, 30.57f, 1, 1.10f);
        Tree(79.05f, 23.41f, 0, .99f);
        Tree(56.08f, 15.36f, 0, .98f);
        Tree(64.14f, 13.82f, 3, 1.12f);

        for (float z = -20; z < 88; z += 4.9f)
        {
            for (float x = 7; x < 112; x += 4.9f)
            {
                if (Range(0, 1) < .15f) continue;
                float px = x + Range(-2.15f, 2.15f), pz = z + Range(-2.15f, 2.15f);
                if (TerraceDistance(px, pz) < 1.25f || PathDistance(px, pz) < 1.88f ||
                    Rectangle(px, pz, 39.4f, 50.5f, 11.2f, 20.5f) < .25f) continue;
                if (Rectangle(px, pz, 48.8f, 57.3f, 15.3f, 20.37f) < .1f) continue;
                var point = new Vector2(px, pz);
                if (reserved.Exists(other => (other - point).sqrMagnitude < 7.6f)) continue;
                int variant = Range(0, 1) < .12f ? 4 : random.Next(4);
                if (variant > 1 && PathDistance(px, pz) < 4.25f) variant %= 2;
                Tree(px, pz, variant, Range(.73f, 1.19f));
            }
        }

        var ridge = Group("Rocky slopes and boulders", parent);
        for (int i = 0; i < 19; i++)
        {
            float z = 17.9f + i * 1.37f + Range(-.37f, .35f);
            float x = 78.45f + Range(-.30f, .85f);
            var scale = V(Range(1.05f, 2.21f), Range(.71f, 1.46f), Range(.86f, 1.67f));
            PlaceRock(ridge, "East cliff outcrop", V(x, Height(x, z) - .13f, z), scale,
                i % rocks.Length, Range(0, 360), true);
        }
        for (int i = 0; i < 24; i++)
        {
            float x = 39.9f + i * 1.54f + Range(-.47f, .29f);
            float z = (x < 66.7f ? 18.59f : 16.62f) + Range(-.59f, .35f);
            if (PathDistance(x, z) < 2.02f) continue;
            var scale = V(Range(.65f, 1.65f), Range(.43f, 1.14f), Range(.58f, 1.42f));
            PlaceRock(ridge, "South ledge boulder", V(x, Height(x, z) - .20f, z), scale,
                i % rocks.Length, Range(0, 360), true);
        }
        for (int i = 0; i < 112; i++)
        {
            float x = Range(23, 91), z = Range(-4, 66);
            if (PathDistance(x, z) < .97f || TerraceDistance(x, z) < .30f || GravelDistance(x, z) < 1) continue;
            float scale = Range(.10f, .68f) * (Range(0, 1) < .13f ? 1.8f : 1);
            PlaceRock(ridge, "Moss-bank stone", V(x, Height(x, z) - scale * .18f, z),
                V(scale, scale * Range(.52f, .93f), scale * Range(.69f, 1.43f)), random.Next(rocks.Length),
                Range(0, 360), scale > .69f);
        }
        for (int i = 0; i < 37; i++)
        {
            float x = 35.6f + Range(-3.10f, 3.76f), z = Range(3.1f, 37.6f);
            if (PathDistance(x, z) < .68f) continue;
            float s = Range(.09f, .31f);
            PlaceRock(ridge, "Trail edge cobble", V(x, Height(x, z) - s * .08f, z), V(s, s * .55f, s * 1.1f),
                random.Next(rocks.Length), Range(0, 360), false);
        }
        ScatterPlants(Group("Ferns, heath and grasses", parent));
        ScatterDeadwood(Group("Snags and forest litter", parent));
    }

    private void PlaceRock(Transform parent, string name, Vector3 position, Vector3 scale, int variant,
        float rotation, bool solid)
    {
        var obj = MeshObject(name, parent, rocks[variant], stone).transform;
        obj.SetPositionAndRotation(position, Quaternion.Euler(Range(-16, 17), rotation, Range(-11, 12)));
        obj.localScale = scale;
        if (solid) obj.gameObject.AddComponent<MeshCollider>().sharedMesh = rocks[variant];
    }

    private void ScatterPlants(Transform parent)
    {
        const string path = "Assets/TerrainSampleAssets/Prefabs/";
        var meadow = new[] { "Fern_A", "Fern_B", "Fern_C", "Grass_A", "GrassDry_A", "GrassDry_B", "GrassDry_C",
            "Plant_A", "Plant_B", "Plant_C", "Plant_D", "Heather_A", "Bush_A", "Bush_B", "Grass_B", "BushDry_B" };
        var library = Array.ConvertAll(meadow, name => Load<GameObject>(path + name + ".prefab"));
        string[] textureNames = { "Fern", "Fern", "Fern", "Grass_A", "GrassDry", "GrassDry", "GrassDry",
            "Plant", "Plant", "Plant", "Plant", "Heather_A", "Bush_A", "Bush_A", "Grass_A", "BushDry" };
        var plantMaterials = new Dictionary<string, Material>();
        foreach (string source in textureNames)
        {
            if (!plantMaterials.ContainsKey(source)) plantMaterials.Add(source, PlantMaterial(source));
        }
        var tiles = new Dictionary<Vector2Int, List<Vector4>>();
        for (int i = 0; i < 2400; i++)
        {
            float x = i < 780 ? Range(29, 55) : Range(19, 98);
            float z = i < 780 ? Range(2, 56) : Range(-4, 70);
            float trail = PathDistance(x, z), gravel = GravelDistance(x, z), terrace = TerraceDistance(x, z);
            if (trail < Range(.89f, 1.67f) || gravel < .2f) continue;
            if (Rectangle(x, z, 42.7f, 69.4f, 19.2f, 36.8f) < .10f && terrace < 0) continue;
            if (Rectangle(x, z, 68.0f, 77.5f, 17.4f, 42.4f) < 0) continue;
            float pockets = Noise(x, z, .21f) * .72f + Noise(x + 31, z, .095f) * .9f;
            if (pockets + Range(-.24f, .22f) < -.055f && trail > 2.6f) continue;
            int variant = random.Next(library.Length);
            if (variant >= 14 && trail > 2.8f) variant = 3 + variant % 4;
            float size = Range(.50f, 1.22f);
            if (variant < 3) size *= 1.20f;
            if (variant >= 12) size *= .69f;
            var key = new Vector2Int(Mathf.FloorToInt(x / 12), Mathf.FloorToInt(z / 12));
            if (!tiles.TryGetValue(key, out var samples)) tiles.Add(key, samples = new List<Vector4>());
            samples.Add(new Vector4(x, z, size, variant));
        }

        foreach (var (key, points) in tiles)
        {
            var tile = Group($"Understory {key.x:00}-{key.y:00}", parent);
            foreach (var point in points)
            {
                var prefab = library[(int)point.w];
                var plant = UnityEngine.Object.Instantiate(prefab, tile);
                plant.name = prefab.name;
                plant.transform.SetPositionAndRotation(V(point.x, Height(point.x, point.y) - .038f, point.y),
                    Quaternion.Euler(Range(-8, 8), Range(0, 360), Range(-8, 8)));
                plant.transform.localScale = Vector3.one * point.z;
                foreach (var renderer in plant.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = plantMaterials[textureNames[(int)point.w]];
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }
    }

    private Material PlantMaterial(string source)
    {
        const string path = "Assets/TerrainSampleAssets/Textures/Details/";
        var stencil = Lit("Forest understory " + source, "#B0B599", .14f);
        stencil.SetTexture("_BaseMap", Load<Texture2D>(path + source + "_BaseColor.tif"));
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(path + source + "_Normal.tif");
        if (normal != null)
        {
            stencil.SetTexture("_BumpMap", normal);
            stencil.SetFloat("_BumpScale", .65f);
            stencil.EnableKeyword("_NORMALMAP");
        }
        SetAlphaCutout(stencil);
        EditorUtility.SetDirty(stencil);
        return stencil;
    }

    private void ScatterDeadwood(Transform parent)
    {
        var parts = new Parts(parent, "Snags and deadwood");
        for (int i = 0; i < 48; i++)
        {
            float x = Range(28, 84), z = Range(-2, 59);
            if (PathDistance(x, z) < 2.15f || TerraceDistance(x, z) < .66f) continue;
            var p = V(x, Height(x, z) - .04f, z);
            if (i % 4 == 0)
            {
                float h = Range(.35f, 1.77f), r = Range(.125f, .24f);
                var split = p + V(Range(-.08f, .12f), h, Range(-.07f, .09f));
                parts.Round(bark, p, split, r, r * .63f, 9, endGrain, h > .65f);
                for (int j = 0; j < 3; j++)
                {
                    var foot = p + Quaternion.Euler(0, j * 120 + i * 17, 0) * V(.54f, 0, 0);
                    foot.y = Height(foot.x, foot.z) - .09f;
                    parts.Round(bark, p + V(0, .20f, 0), foot, r * .58f, .023f, 5);
                }
            }
            else
            {
                var end = p + Quaternion.Euler(0, Range(0, 360), 0) * V(Range(.49f, 2.73f), 0, 0);
                end.y = Height(end.x, end.z) + Range(.021f, .21f);
                float radius = Range(.045f, .12f);
                parts.Round(bark, p + V(0, radius, 0), end, radius, radius * .63f, 7, endGrain);
            }
        }
        parts.Finish();
    }

    private TreeForm MakeTree(string name, int seed, float height, float radius, bool pine)
    {
        var result = new TreeForm { Height = height, Radius = radius };
        var rng = new System.Random(seed);
        float R(float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());
        var bend = V(R(-.63f, .63f), 0, R(-.58f, .58f));

        Vector3 Center(float t) => V(0, t * height - .14f, 0) +
            bend * (t * t * 1.3f + Mathf.Sin(t * 6) * .25f);
        var branches = new List<(float t, float angle, float length, float rise, int atlas)>();
        int levels = pine ? 10 : 13;
        for (int level = 0; level < levels; level++)
        {
            float t = Mathf.Lerp(pine ? .44f : .22f, .95f, level / (levels - 1f));
            int count = pine ? rng.Next(4, 6) : rng.Next(5, 7);
            float turn = R(0, 360);
            for (int j = 0; j < count; j++)
            {
                float spread = pine ? (1 - t) * .44f : (1 - t) * .32f;
                float length = Mathf.Max(.34f, spread * height * R(.74f, 1.20f));
                branches.Add((t + R(-.020f, .020f), turn + j * (360f / count) + R(-16f, 16f),
                    length, length * (pine ? R(.03f, .31f) : R(-.32f, .16f)), rng.Next(4)));
            }
        }

        for (int lod = 0; lod < 3; lod++)
        {
            var trunk = new Geometry();
            var crown = new Geometry();
            int trunkRings = lod == 0 ? 15 : lod == 1 ? 8 : 4;
            var nodes = new Vector3[trunkRings];
            var radii = new float[trunkRings];
            for (int i = 0; i < trunkRings; i++)
            {
                float t = i / (trunkRings - 1f);
                nodes[i] = Center(t);
                radii[i] = Mathf.Lerp(radius, .024f, Mathf.Pow(t, .66f)) + (.115f * (1 - t) * Mathf.Exp(-t * 37));
            }
            trunk.Tube(nodes, radii, lod == 0 ? 10 : lod == 1 ? 7 : 5, Vector2.zero, new Vector2(1.4f, .77f));
            int step = lod == 2 ? 3 : 1;
            for (int i = 0; i < branches.Count; i += step)
            {
                var branch = branches[i];
                var root = Center(branch.t);
                var direction = Quaternion.Euler(0, branch.angle, 0) * Vector3.right;
                var across = Vector3.Cross(direction, Vector3.up);
                var end = root + direction * branch.length + V(0, branch.rise, 0);
                var mid = Vector3.Lerp(root, end, .47f) - V(0, branch.length * .09f, 0);
                float width = branch.length * (pine ? .47f : .56f) * (lod == 2 ? 1.17f : 1f);
                if (lod == 0 || lod == 1 && i % 2 == 0)
                {
                    float b = radius * (1 - branch.t) * .46f;
                    trunk.Tube(new[] { root, mid, end }, new[] { b, b * .62f, .009f }, lod == 0 ? 5 : 3,
                        Vector2.one * branch.t, new Vector2(.65f, 1.4f));
                }

                NeedleCard(crown, Vector3.Lerp(root, mid, pine ? .28f : .10f), end, across, width, branch.atlas);
                if (lod < 2)
                    NeedleCard(crown, Vector3.Lerp(root, mid, .22f), end + V(0, .14f, 0), (across * .3f + Vector3.up).normalized,
                        width * .72f, (branch.atlas + 1) % 4);
                if (lod == 0 && branch.length > .94f)
                {
                    NeedleCard(crown, mid, end - direction * .22f + across * width * .69f + V(0, width * .24f, 0),
                        across + Vector3.up * .29f, width * .62f, (branch.atlas + 2) % 4);
                    NeedleCard(crown, mid, end - direction * .22f - across * width * .71f - V(0, width * .15f, 0),
                        across - Vector3.up * .4f, width * .67f, (branch.atlas + 3) % 4);
                }
            }

            for (int i = 0; i < 5; i++)
            {
                var side = Quaternion.Euler(0, i * 72, 0) * Vector3.right;
                NeedleCard(crown, Center(.904f), Center(.994f) + side * .28f, Vector3.Cross(side, Vector3.up),
                    height * .028f, i % 4);
                if (lod != 0) continue;
                var tip = side * radius * (2.4f + i % 3) - V(0, .23f, 0);
                trunk.Tube(new[] { Center(.025f), Center(.008f) + side * radius * 1.08f, tip },
                    new[] { radius * .38f, radius * .32f, .021f }, 5, Vector2.one * (i * .21f), Vector2.one);
                if (pine)
                {
                    var low = Center(.19f + i * .043f);
                    trunk.Tube(new[] { low, low + side * height * .051f - V(0, .16f, 0),
                        low + side * height * (.065f + i * .003f) + V(0, .04f, 0) },
                        new[] { radius * .21f, radius * .13f, .012f }, 5, Vector2.zero, Vector2.one);
                }
            }

            result.Trunk[lod] = trunk.Bake(name + " trunk LOD" + lod);
            result.Crown[lod] = crown.Bake(name + " crown LOD" + lod, false);
        }
        return result;
    }

    private static void NeedleCard(Geometry mesh, Vector3 root, Vector3 tip, Vector3 across, float width, int variant)
    {
        var side = across.normalized * width;
        var uv = new Vector2((variant % 2) * .5f, (variant / 2) * .5f);
        var normal = (Vector3.up * .81f + (tip - root).normalized * .19f).normalized;
        mesh.Quad(root - side, tip - side, tip + side, root + side, normal,
            uv + new Vector2(.005f, .005f), new Vector2(.49f, .49f));
    }

    private void PlaceTree(Transform parent, TreeForm form, float x, float z, float size, float angle)
    {
        var tree = Group(form.Trunk[0].name.Replace(" trunk LOD0", ""), parent);
        tree.SetPositionAndRotation(V(x, Height(x, z) - .055f, z), Quaternion.Euler(Range(-3, 3), angle, Range(-3, 3)));
        tree.localScale = V(size * Range(.83f, 1.04f), size, size * Range(.84f, 1.12f));
        var levels = new LOD[3];
        for (int i = 0; i < 3; i++)
        {
            var wood = MeshObject("Bark LOD " + i, tree, form.Trunk[i], bark, i < 2 ? ShadowCastingMode.On : ShadowCastingMode.Off);
            var needles = MeshObject("Needles LOD " + i, tree, form.Crown[i], foliage, i == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off);
            levels[i] = new LOD(i == 0 ? .38f : i == 1 ? .15f : .031f, new Renderer[] { wood, needles });
        }
        var group = tree.gameObject.AddComponent<LODGroup>();
        group.SetLODs(levels);
        group.RecalculateBounds();
        if (x > 26 && x < 86 && z > -7 && z < 63)
        {
            var trunk = tree.gameObject.AddComponent<CapsuleCollider>();
            trunk.radius = form.Radius * .87f;
            trunk.height = Mathf.Max(3.0f, form.Height * .37f);
            trunk.center = V(0, trunk.height * .5f - .10f, 0);
        }
    }

    private static Texture2D MakeNeedleAtlas()
    {
        const int tile = 512;
        const int size = tile * 2;
        var pixels = new Color[size * size];
        var rng = new System.Random(59742);
        float R(float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());

        for (int style = 0; style < 4; style++)
        {
            int tileX = style % 2 * tile;
            int tileY = style / 2 * tile;
            var branch = new Vector2(21, 256);
            var tip = new Vector2(492, 253);
            Stroke(pixels, size, tileX, tileY, tile, branch, tip, 2.7f, new Color(.37f, .29f, .18f));
            for (int group = 0; group < 17; group++)
            {
                float t = (group + 1f) / 18f;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var twigRoot = Vector2.Lerp(branch, tip, t) + new Vector2(R(-8, 8), R(-4, 4));
                    var twigTip = twigRoot + new Vector2(R(69, 111) * (1 - t * .62f),
                        sign * R(112, 216) * Mathf.Pow(Mathf.Sin(t * 2.74f + .12f), .62f) * (1 - t * .54f));
                    Stroke(pixels, size, tileX, tileY, tile, twigRoot, twigTip, R(.78f, 1.5f), new Color(.39f, .33f, .215f));
                    for (int needle = 0; needle < 34; needle++)
                    {
                        float amount = R(.07f, 1f);
                        var root = Vector2.Lerp(twigRoot, twigTip, amount) + new Vector2(R(-3, 3), R(-1.9f, 1.9f));
                        float reach = R(12, 42) * (1 - amount * .48f);
                        float angle = R(-1.45f, 1.50f);
                        var end = root + new Vector2(Mathf.Cos(angle) * reach, Mathf.Sin(angle) * reach + sign * 11);
                        float green = R(.23f, .47f) * (style == 2 ? .92f : 1.05f);
                        var tint = new Color(green * R(.64f, .88f), green, green * R(.52f, .72f));
                        Stroke(pixels, size, tileX, tileY, tile, root, end, R(.58f, 1.23f), tint);
                    }
                }
            }
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        string path = Art + "/Pine needle atlas.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return Import(path, false, false, true);
    }

    private static void Stroke(Color[] pixels, int stride, int x0, int y0, int tile,
        Vector2 a, Vector2 b, float width, Color color)
    {
        int minX = Mathf.Max(5, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - width - 1));
        int maxX = Mathf.Min(tile - 6, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + width + 1));
        int minY = Mathf.Max(5, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - width - 1));
        int maxY = Mathf.Min(tile - 6, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + width + 1));
        var line = b - a;
        float lengthSquared = line.sqrMagnitude;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var offset = new Vector2(x, y) - a;
                float t = Mathf.Clamp01(Vector2.Dot(offset, line) / lengthSquared);
                float dist = (offset - line * t).magnitude;
                float alpha = Mathf.Clamp01(width * Mathf.Lerp(1.15f, .41f, t) + .68f - dist);
                int index = (y + y0) * stride + (x + x0);
                if (alpha < pixels[index].a * .89f || alpha < .02f) continue;
                pixels[index] = new Color(color.r, color.g, color.b, Mathf.Max(pixels[index].a, alpha));
            }
        }
    }
}
