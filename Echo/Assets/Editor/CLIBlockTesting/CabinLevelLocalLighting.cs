using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed partial class CabinLevelBuilder
{
    private const string DeckLighting = "Grill and deck lighting";
    private const string ShedLighting = "Overhead storage lighting";
    private const string PassageLighting = "Cabin-side passage lighting";
    private const string LocalLightingPreviews = "Logs/cli/CLIBlockTesting/LocalLighting";

    internal static void UpdateLocalLighting()
    {
        Run(() =>
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var level = scene.GetRootGameObjects().Single(root => root.name == LevelName).transform;
            var builder = new CabinLevelBuilder
            {
                darkWood = Load<Material>(Art + "/Dark timber and porch joists.mat"),
                metal = Load<Material>(Art + "/Gunmetal and stove enamel.mat"),
                enamel = Load<Material>(Art + "/Warm lantern diffuser.mat")
            };
            bool canRender = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            if (canRender) RenderLocalLightingPreviews(LocalLightingPreviews + "/Before");
            builder.ApplyLocalLighting(level);
            Verify(scene);
            VerifyLocalLighting(level);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + ScenePath);
            AssetDatabase.SaveAssets();
            if (canRender) RenderLocalLightingPreviews(LocalLightingPreviews + "/After");
        });
    }

    private void ApplyLocalLighting(Transform level)
    {
        var deck = RequiredChild(level, "Outdoor living areas/Raised BBQ platform");
        var prepLantern = RequiredChild(deck, "BBQ prep lantern").GetComponent<Light>();
        prepLantern.intensity = 3.45f;
        prepLantern.range = 3.10f;
        PlaceDeckLight(ReplaceGeneratedChild(deck, DeckLighting));

        var shed = RequiredChild(level, ShedPath);
        var rearLantern = RequiredChild(shed, "Storage lantern").GetComponent<Light>();
        rearLantern.intensity = 2.7f;
        PlaceShedLights(ReplaceGeneratedChild(shed, ShedLighting));

        var doorLantern = RequiredChild(shed, EnclosureName + "/Storage door lantern").GetComponent<Light>();
        doorLantern.intensity = 5.1f;
        doorLantern.range = 3.65f;
        doorLantern.transform.localPosition = V(ShedEast + .14f, ShedFloor + 1.90f, DoorNorth + .57f);
        var house = RequiredChild(level, "Main log house");
        PlacePassageLights(ReplaceGeneratedChild(house, PassageLighting));
    }

    private void PlaceDeckLight(Transform parent)
    {
        const float deck = Terrace + .81f;
        var parts = new Parts(parent, "BBQ task light");
        var post = V(76.48f, deck, 38.40f);
        var head = post + V(-.55f, 2.26f, -.28f);
        parts.Box(darkWood, post + V(0, 1.13f, 0), V(.122f, 2.34f, .122f));
        parts.Box(metal, post + V(0, 1.03f, 0), V(.152f, .080f, .146f));
        parts.Beam(metal, post + V(0, 2.27f, 0), head, .049f, .049f);
        HoodedLight(parts, "BBQ grill downlight", head, V(74.87f, deck + .16f, 39.40f),
            "#FFE0B4", 12.7f, 5.7f, 116f);
        parts.Finish();
    }

    private void PlaceShedLights(Transform parent)
    {
        var parts = new Parts(parent, "Storage interior lights");
        var front = V(46.48f, ShedFloor + 2.19f, 26.75f);
        var rear = V(46.48f, ShedFloor + 2.32f, 31.15f);
        foreach (var head in new[] { front, rear })
        {
            parts.Box(darkWood, head + V(0, .117f, 0), V(4.05f, .123f, .135f));
            parts.Box(metal, head + V(0, .054f, 0), V(.23f, .060f, .16f));
        }
        HoodedLight(parts, "Firewood ceiling light", front, V(46.02f, ShedFloor, 27.20f),
            "#F6D9AF", 9.8f, 4.75f, 122f);
        HoodedLight(parts, "Aisle ceiling light", rear, V(46.62f, ShedFloor, 32.02f),
            "#F6D9AF", 8.5f, 4.65f, 119f);
        parts.Finish();
    }

    private void PlacePassageLights(Transform parent)
    {
        var parts = new Parts(parent, "Cabin-side passage fixture");
        void Mount(float z, string name, Vector3 target, float intensity, float range)
        {
            var plate = V(West - .152f, 8.50f, z);
            var head = plate - V(.38f, .07f, 0);
            parts.Box(metal, plate, V(.060f, .219f, .145f));
            parts.Beam(metal, plate + V(0, .034f, 0), head, .043f, .049f);
            HoodedLight(parts, name, head, target, "#EACBA4", intensity, range, 103f);
        }

        Mount(33.62f, "Backyard passage downlight", V(50.62f, Terrace, 33.65f), 14.8f, 6.35f);
        Mount(24.44f, "Boundary passage downlight", V(50.60f, Terrace, 25.59f), 11.5f, 5.65f);
        parts.Finish();
    }

    private void HoodedLight(Parts parts, string name, Vector3 face, Vector3 target,
        string color, float intensity, float range, float cone)
    {
        var direction = (target - face).normalized;
        parts.Round(metal, face - direction * .09f, face, .076f, .133f, 12);
        parts.Round(enamel, face + direction * .003f, face + direction * .019f, .099f, .094f, 12);

        var fixture = Group(name, parts.Parent);
        fixture.localPosition = face + direction * .059f;
        fixture.localRotation = Quaternion.LookRotation(direction, Vector3.forward);
        var light = fixture.gameObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = Tint(color);
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = cone;
        light.innerSpotAngle = cone * .72f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = .83f;
        light.shadowBias = .055f;
        light.shadowNormalBias = .24f;
    }

    private static void VerifyLocalLighting(Transform level)
    {
        var deck = RequiredChild(level, "Outdoor living areas/Raised BBQ platform/" + DeckLighting);
        var shed = RequiredChild(level, ShedPath + "/" + ShedLighting);
        var passage = RequiredChild(level, "Main log house/" + PassageLighting);
        CheckCoverage(deck, "BBQ grill downlight", V(75.53f, Terrace + 1.96f, 39.73f));
        CheckCoverage(deck, "BBQ grill downlight", V(73.75f, Terrace + .87f, 38.15f));
        CheckCoverage(shed, "Firewood ceiling light", V(45.35f, ShedFloor + .64f, 26.66f));
        CheckCoverage(shed, "Aisle ceiling light", V(46.97f, ShedFloor + .08f, 32.64f));
        CheckCoverage(passage, "Backyard passage downlight", V(50.71f, Terrace + .14f, 33.32f));
        CheckCoverage(passage, "Boundary passage downlight", V(50.39f, Terrace + .14f, 23.81f));
        Debug.Log("[EchoCli] Local light coverage verified for the BBQ, storage shed and side passage.");
    }

    private static void CheckCoverage(Transform parent, string name, Vector3 target)
    {
        var light = RequiredChild(parent, name).GetComponent<Light>();
        Vector3 travel = target - light.transform.position;
        if (!light.enabled || light.intensity <= 0 || travel.magnitude > light.range ||
            Vector3.Angle(light.transform.forward, travel) > light.spotAngle * .5f)
            throw new InvalidOperationException(name + " does not cover its target.");
    }

    private static void RenderLocalLightingPreviews(string path)
    {
        Directory.CreateDirectory(path);
        Capture(path, "BBQ-deck", V(73.85f, 7.75f, 38.11f), V(74.64f, 6.17f, 40.38f), 65);
        Capture(path, "Storage-interior", V(47.56f, 6.79f, 25.68f), V(46.63f, 6.00f, 32.24f), 67);
        Capture(path, "Cabin-side-passage", V(50.34f, 7.09f, 35.84f), V(50.60f, 5.93f, 27.07f), 67);
    }
}
