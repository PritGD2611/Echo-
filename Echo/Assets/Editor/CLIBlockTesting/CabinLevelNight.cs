using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

internal sealed partial class CabinLevelBuilder
{
    private const string ShedPath = "Outdoor living areas/Outdoor storage sheds/West equipment and wood store";
    private const string EnclosureName = "Storage enclosure and door";
    private const string WallName = "Approach boundary wall";
    private const float ShedWest = 44.51f;
    private const float ShedEast = 48.64f;
    private const float ShedSouth = 24.82f;
    private const float ShedNorth = 34.58f;
    private const float ShedFloor = Terrace + .16f;
    private const float DoorSouth = 27.72f;
    private const float DoorNorth = 29.06f;
    private const float DoorHead = ShedFloor + 2.07f;
    private const float WallZ = 22.91f;

    internal static void UpdateNight()
    {
        Run(() =>
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var level = scene.GetRootGameObjects().Single(root => root.name == LevelName).transform;
            var builder = new CabinLevelBuilder
            {
                siding = Load<Material>(Art + "/Weathered exterior boards.mat"),
                darkWood = Load<Material>(Art + "/Dark timber and porch joists.mat"),
                stone = Load<Material>(Art + "/Mottled cliff rock.mat"),
                metal = Load<Material>(Art + "/Gunmetal and stove enamel.mat"),
                brass = Load<Material>(Art + "/Tarnished brass.mat"),
                enamel = Load<Material>(Art + "/Warm lantern diffuser.mat")
            };
            builder.ApplyNightChanges(level);
            Verify(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + ScenePath);
            AssetDatabase.SaveAssets();
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) RenderPreviews();
        });
    }

    private void ApplyNightChanges(Transform level)
    {
        var shed = RequiredChild(level, ShedPath);
        BuildShedEnclosure(ReplaceGeneratedChild(shed, EnclosureName));
        var outdoor = RequiredChild(level, "Outdoor living areas");
        BuildApproachWall(ReplaceGeneratedChild(outdoor, WallName));
        SetNightLighting(RequiredChild(level, "Fog, sky and lighting"));
        ApplyLocalLighting(level);
    }

    private static Transform RequiredChild(Transform parent, string path)
    {
        return parent.Find(path) ?? throw new InvalidOperationException("Unable to find " + path);
    }

    private static Transform ReplaceGeneratedChild(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        return Group(name, parent);
    }

    private void BuildShedEnclosure(Transform parent)
    {
        var shell = new Parts(parent, "West storage enclosure");
        float x = ShedEast - .035f;
        CloseWestShedSide(shell, V(x, ShedFloor, ShedSouth), V(x, ShedFloor, DoorSouth), true);
        CloseWestShedSide(shell, V(x, ShedFloor, DoorNorth), V(x, ShedFloor, ShedNorth), true);
        CloseWestShedSide(shell, V(x, DoorHead, DoorSouth), V(x, DoorHead, DoorNorth), true);

        const float topOfOldBoards = ShedFloor + 2.20f;
        CloseWestShedSide(shell, V(ShedWest + .025f, topOfOldBoards, ShedSouth),
            V(ShedWest + .025f, topOfOldBoards, ShedNorth), false);
        CloseWestShedSide(shell, V(ShedWest, topOfOldBoards, ShedSouth + .025f),
            V(ShedEast, topOfOldBoards, ShedSouth + .025f), false);
        CloseWestShedSide(shell, V(ShedWest, topOfOldBoards, ShedNorth - .025f),
            V(ShedEast, topOfOldBoards, ShedNorth - .025f), false);

        float middle = (DoorSouth + DoorNorth) * .5f;
        foreach (float jamb in new[] { DoorSouth - .03f, DoorNorth + .03f })
            shell.Box(darkWood, V(ShedEast + .012f, (ShedFloor + DoorHead) * .5f, jamb),
                V(.15f, DoorHead - ShedFloor, .105f));
        shell.Box(darkWood, V(ShedEast + .017f, DoorHead + .042f, middle),
            V(.16f, .13f, DoorNorth - DoorSouth + .17f));
        shell.Box(darkWood, V(ShedEast + .075f, ShedFloor - .033f, middle),
            V(.17f, .065f, DoorNorth - DoorSouth + .085f));
        Lantern(shell, parent, V(ShedEast + .10f, ShedFloor + 1.93f, DoorNorth + .57f),
            "Storage door lantern", 15f, 4.5f);
        shell.Finish();

        var door = Group("Hinged storage door", parent);
        door.localPosition = V(ShedEast - .01f, ShedFloor + .014f, DoorSouth + .028f);
        var leaf = new Parts(door, "West storage door");
        float width = DoorNorth - DoorSouth - .046f;
        float height = DoorHead - ShedFloor - .037f;
        const int boards = 7;
        for (int i = 0; i < boards; i++)
        {
            float z = (i + .5f) * width / boards;
            leaf.Box(siding, V(0, height * .5f, z), V(.082f, height, width / boards - .0015f));
        }
        foreach (float y in new[] { .29f, 1.72f })
        {
            leaf.Box(darkWood, V(-.054f, y, width * .5f), V(.041f, .107f, width - .036f));
            leaf.Box(metal, V(.049f, y, .13f), V(.023f, .054f, .24f));
        }
        leaf.Beam(darkWood, V(-.062f, .33f, .07f), V(-.062f, 1.68f, width - .07f), .062f, .079f);
        foreach (float side in new[] { -1f, 1f })
        {
            leaf.Box(metal, V(side * .05f, 1.01f, width - .13f), V(.022f, .18f, .077f));
            leaf.Round(brass, V(side * .06f, 1.01f, width - .13f),
                V(side * .108f, 1.01f, width - .13f), .026f, .025f, 9);
            leaf.Box(brass, V(side * .11f, 1.01f, width - .177f), V(.031f, .032f, .117f));
        }
        leaf.Finish();
        var collider = door.gameObject.AddComponent<BoxCollider>();
        collider.center = V(0, height * .5f, width * .5f);
        collider.size = V(.084f, height, width);
        door.gameObject.AddComponent<StorageShedDoor>();
    }

    private void CloseWestShedSide(Parts parts, Vector3 from, Vector3 to, bool solid)
    {
        float RoofAt(float z) => ShedFloor + Mathf.Lerp(2.48f, 2.77f,
            Mathf.InverseLerp(ShedSouth - .26f, ShedNorth + .32f, z)) - .143f;

        float length = Vector3.Distance(from, to);
        var rotation = Quaternion.LookRotation(Vector3.Cross((to - from).normalized, Vector3.up));
        int boards = Mathf.CeilToInt(length / .205f);
        for (int i = 0; i < boards; i++)
        {
            Vector3 bottom = Vector3.Lerp(from, to, (i + .5f) / boards);
            float height = RoofAt(bottom.z) - bottom.y;
            parts.Box(siding, bottom + V(0, height * .5f, 0),
                V(length / boards - .0014f, height, .107f), rotation: rotation, repeatU: .62f, repeatV: .64f);
        }

        if (solid)
        {
            float height = Mathf.Max(RoofAt(from.z), RoofAt(to.z)) - from.y;
            BoxCollider(parts.Parent, Vector3.Lerp(from, to, .5f) + V(0, height * .5f, 0),
                V(length, height, .107f), rotation);
        }
    }

    private void BuildApproachWall(Transform parent)
    {
        var wall = new Parts(parent, "Approach timber wall");
        var boundary = new[]
        {
            new Vector2(37.60f, 21.76f),
            new Vector2(39.57f, 22.91f),
            new Vector2(West - .025f, WallZ)
        };
        for (int segment = 1; segment < boundary.Length; segment++)
        {
            var from = boundary[segment - 1];
            var to = boundary[segment];
            Vector3 Place(Vector2 p) => V(p.x, Height(p.x, p.y), p.y);
            Vector3 a = Place(from), b = Place(to);
            var axis = V(b.x - a.x, 0, b.z - a.z);
            var rotation = Quaternion.LookRotation(Vector3.Cross(axis.normalized, Vector3.up));
            float length = axis.magnitude;
            int panels = Mathf.CeilToInt(length / 2.35f);
            for (int i = 0; i < panels; i++)
            {
                Vector3 left = Place(Vector2.Lerp(from, to, i / (float)panels));
                Vector3 right = Place(Vector2.Lerp(from, to, (i + 1f) / panels));
                Vector3 middle = Place(Vector2.Lerp(from, to, (i + .5f) / panels));
                float low = Mathf.Min(left.y, right.y, middle.y) - .19f;
                float high = Mathf.Max(left.y, right.y, middle.y) + 2.46f;
                float span = length / panels;
                wall.Box(stone, V(middle.x, low + .22f, middle.z), V(span - .015f, .44f, .42f), rotation: rotation);
                int boards = Mathf.CeilToInt(span / .198f);
                for (int j = 0; j < boards; j++)
                {
                    Vector3 bottom = Vector3.Lerp(left, right, (j + .5f) / boards);
                    float top = high + Range(-.054f, .022f);
                    wall.Box(siding, V(bottom.x, (low + top) * .5f, bottom.z),
                        V(span / boards - .0025f, top - low, .117f), rotation: rotation, repeatU: .65f, repeatV: .48f);
                }

                foreach (float y in new[] { high - .29f, low + .62f })
                    wall.Box(darkWood, V(middle.x, y, middle.z) + rotation * V(0, 0, -.073f),
                        V(span, .103f, .10f), rotation: rotation);
                BoxCollider(parent, V(middle.x, (low + high) * .5f, middle.z), V(span + .04f, high - low, .29f), rotation);

                if (i == 0) AddPost(left);
                AddPost(right);
            }
        }

        void AddPost(Vector3 p)
        {
            wall.Box(darkWood, p + V(0, 1.225f, 0), V(.23f, 2.78f, .26f));
            wall.Box(darkWood, p + V(0, 2.625f, 0), V(.29f, .068f, .31f));
        }

        Lantern(wall, parent, V(46.97f, Terrace + 1.84f, WallZ - .235f),
            "Approach wall lantern", 17f, 5.8f);
        wall.Finish();
        PointLight(parent, "Trail fork lantern spill", V(41.75f, 3.9f, 15.86f), "#FFBD7C", 2.9f, 6.5f);
    }

    private static void SetNightLighting(Transform parent)
    {
        var overcast = Load<Material>(Art + "/Overcast forest sky.mat");
        var sky = new Material(overcast) { name = "Night forest sky" };
        sky.SetFloat("_NightAmount", 1);
        RenderSettings.skybox = Save(sky, sky.name + ".mat");
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Tint("#273648");
        RenderSettings.fogDensity = .032f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Tint("#657C9B");
        RenderSettings.ambientEquatorColor = Tint("#465363");
        RenderSettings.ambientGroundColor = Tint("#30373D");
        RenderSettings.ambientIntensity = .81f;
        RenderSettings.reflectionIntensity = .31f;

        var moon = parent.Find("Moonlight") ?? RequiredChild(parent, "Overcast evening sun");
        moon.name = "Moonlight";
        moon.localRotation = Quaternion.Euler(37, 18, 0);
        var key = moon.GetComponent<Light>();
        key.color = Tint("#A0B9E8");
        key.intensity = .63f;
        key.shadowStrength = .76f;
        RenderSettings.sun = key;

        var east = RequiredChild(parent, "Cool skylight from east").GetComponent<Light>();
        east.color = Tint("#718DBA");
        east.intensity = .21f;
        var northwest = RequiredChild(parent, "Low gray sky from northwest").GetComponent<Light>();
        northwest.color = Tint("#8496AE");
        northwest.intensity = .105f;
    }

    private static void VerifyNight(Transform level)
    {
        if (RenderSettings.skybox == null || RenderSettings.skybox.GetFloat("_NightAmount") < .99f)
            throw new InvalidOperationException("The night sky was not enabled.");

        var shed = RequiredChild(level, ShedPath);
        var door = RequiredChild(shed, EnclosureName + "/Hinged storage door").GetComponent<StorageShedDoor>();
        if (door == null || !Physics.Raycast(V(49.6f, ShedFloor + 1.15f, (DoorSouth + DoorNorth) * .5f),
                Vector3.left, out var hit, 1.3f) || hit.collider.gameObject != door.gameObject)
            throw new InvalidOperationException("The storage door does not block the doorway when closed.");

        var wall = RequiredChild(level, "Outdoor living areas/" + WallName);
        foreach (float x in new[] { 40f, 43f, 45.31f, 47.68f, 50.1f, 52.3f })
        {
            var position = V(x, Terrace + 1.05f, WallZ - .62f);
            if (!Physics.Raycast(position, Vector3.forward, out hit, .81f) || !hit.collider.transform.IsChildOf(wall))
                throw new InvalidOperationException($"The approach wall does not block the direct shortcut at x={x:F2}.");
        }
    }
}
