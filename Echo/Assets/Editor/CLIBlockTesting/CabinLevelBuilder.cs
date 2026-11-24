using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

internal sealed partial class CabinLevelBuilder
{
    private const string ScenePath = "Assets/Scenes/CLIBlockTesting.unity";
    private const string BlockmeshName = "Original blockmesh (disabled)";
    private const string LevelName = "Forest cabin level";
    private const string AssetRoot = "Assets/CLIBlockTesting";
    private const string Art = AssetRoot + "/Generated";
    private const float Terrace = 4.85f;
    private const float Floor = 6.40f;
    private const float West = 52.68f;
    private const float East = 66.35f;
    private const float Front = 22.22f;
    private const float Back = 34.39f;
    private const float Eave = 9.69f;
    private const float Ridge = 13.65f;
    private const float CabinCenter = (West + East) * .5f;
    private static readonly Vector2 Spawn = new(35.15f, 10.25f);

    private readonly System.Random random = new(62731);
    private readonly Dictionary<string, Surface> surfaces = new();
    private Material planks, siding, logs, endGrain, bark, stone, darkWood, greenRoof, brownRoof;
    private Material metal, rust, glass, cloth, canvas, brass, charcoal, ash, enamel, paintedWood;
    private Material porcelain, toiletTeal, toiletBlue, roofPlastic, foliage, flame, smoke;
    private Material[] roofTones;
    private Mesh[] rocks;

    // This command saves a scene and must run outside the user's active Editor.
    internal static void Build()
    {
        Run(() => new CabinLevelBuilder().BuildScene());
    }

    internal static void Inspect()
    {
        Run(() =>
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Verify(scene);
            RenderPreviews();
        });
    }

    private static void Run(Action action)
    {
        try
        {
            action();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError("[EchoCli] EXCEPTION " + exception);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    private void BuildScene()
    {
        Directory.CreateDirectory(Art);
        AssetDatabase.Refresh();
        MakeMaterials();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var original = scene.GetRootGameObjects();
        var blockmesh = original.SingleOrDefault(item => item.name == BlockmeshName);
        if (blockmesh == null)
        {
            blockmesh = Group(BlockmeshName, null).gameObject;
            foreach (var item in original) item.transform.SetParent(blockmesh.transform, true);
        }
        blockmesh.SetActive(false);
        var prior = original.SingleOrDefault(item => item.name == LevelName);
        if (prior != null) Object.DestroyImmediate(prior);

        var level = Group(LevelName, null);
        BuildGround(Group("Terrain and trails", level));
        BuildCabin(Group("Main log house", level));
        BuildCompound(Group("Outdoor living areas", level));
        BuildWoods(Group("Conifer woods and cliff outcrops", level));
        BuildLighting(Group("Fog, sky and lighting", level));
        BuildPlayer(Group("First-person exploration", level));
        ApplyNightChanges(level);
        blockmesh.transform.SetAsLastSibling();

        Verify(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + ScenePath);
        AssetDatabase.SaveAssets();
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) RenderPreviews();
    }

    private readonly struct Surface
    {
        public readonly Texture2D Color, Normal, Occlusion;

        public Surface(Texture2D color, Texture2D normal, Texture2D occlusion)
        {
            Color = color;
            Normal = normal;
            Occlusion = occlusion;
        }
    }

    private Surface LoadSurface(string folder)
    {
        string root = "Assets/Material/" + folder;
        var colorFile = Directory.GetFiles(root, "*_Color.jpg").Single();
        var prefix = colorFile.Substring(0, colorFile.Length - "_Color.jpg".Length).Replace('\\', '/');
        return new Surface(
            Import(prefix + "_Color.jpg", false, false),
            Import(prefix + "_NormalGL.jpg", true, true),
            Import(prefix + "_AmbientOcclusion.jpg", false, true));
    }

    private static Texture2D Import(string path, bool normal, bool linear, bool alpha = false)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            throw new IOException("Texture importer missing at " + path);
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType != type || importer.sRGBTexture == linear || importer.anisoLevel != 8 ||
            (alpha && !importer.mipMapsPreserveCoverage))
        {
            importer.textureType = type;
            importer.sRGBTexture = !linear;
            importer.anisoLevel = 8;
            importer.alphaIsTransparency = alpha;
            importer.mipMapsPreserveCoverage = alpha;
            importer.alphaTestReferenceValue = .37f;
            if (alpha) importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private void MakeMaterials()
    {
        foreach (var folder in new[] { "Floor", "Logs", "TreeBark", "Rock", "Grass", "Gravel", "Pathway" })
            surfaces.Add(folder, LoadSurface(folder));

        planks = Lit("Plank floor", "#D5B998", .28f, "Floor", 1f);
        siding = Lit("Weathered exterior boards", "#9A8A76", .17f, "Floor", 1.1f);
        darkWood = Lit("Dark timber and porch joists", "#66564A", .14f, "Floor", 1.1f);
        paintedWood = Lit("Faded oxblood window trim", "#754034", .24f, "Floor", .6f);
        logs = Lit("Hewn cabin logs", "#B6A593", .22f, "Logs", 1.08f);
        endGrain = Lit("Sawn end grain", "#AA896A", .19f);
        endGrain.SetTexture("_BaseMap", MakeEndGrain());
        EditorUtility.SetDirty(endGrain);
        bark = Lit("Conifer bark", "#A29F91", .15f, "TreeBark", 1.15f);
        stone = RockMaterial();
        roofTones = new[]
        {
            Lit("Cedar shakes umber", "#6D5145", .21f, "Floor", .85f),
            Lit("Cedar shakes ash", "#827062", .23f, "Floor", .85f),
            Lit("Cedar shakes brown", "#88604A", .22f, "Floor", .85f),
            Lit("Cedar shakes gray", "#605A53", .26f, "Floor", .85f),
            Lit("Cedar shakes faded", "#9A7967", .20f, "Floor", .85f)
        };
        brownRoof = roofTones[0];
        greenRoof = Lit("Patinated green roof metal", "#3E4D43", .37f, null, 0, .45f);
        metal = Lit("Gunmetal and stove enamel", "#343938", .38f, null, 0, .55f);
        rust = Lit("Weathered red steel", "#713D31", .27f, null, 0, .25f);
        brass = Lit("Tarnished brass", "#977445", .42f, null, 0, .61f);
        glass = Lit("Dusty window glass", "#62797B", .85f, null, 0, .18f);
        glass.SetFloat("_Surface", 1);
        glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        glass.SetFloat("_ZWrite", 0);
        var glassTint = glass.GetColor("_BaseColor");
        glassTint.a = .39f;
        glass.SetColor("_BaseColor", glassTint);
        glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        glass.SetOverrideTag("RenderType", "Transparent");
        glass.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(glass);
        cloth = Lit("Mustard wool", "#9B7950", .12f);
        canvas = Lit("Natural cotton", "#D3C6A8", .08f);
        charcoal = Lit("Charcoal and hearth soot", "#382F2A", .08f, "Rock", .8f);
        ash = Lit("Pale ash", "#81766B", .10f, "Gravel", .75f);
        enamel = Lit("Warm lantern diffuser", "#FFE0A0", .37f);
        enamel.SetColor("_EmissionColor", Tint("#FFAE56") * 2.5f);
        enamel.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(enamel);
        porcelain = Lit("Faded ivory plastic", "#C8C8B8", .31f);
        toiletTeal = Lit("Portable toilet faded teal", "#41716F", .35f);
        toiletBlue = Lit("Portable toilet slate blue", "#485B76", .34f);
        roofPlastic = Lit("Portable toilet cream roof", "#CFCAB2", .29f);

        foliage = Lit("Pine needle atlas", "#CFDCBC", .14f);
        foliage.SetTexture("_BaseMap", MakeNeedleAtlas());
        SetAlphaCutout(foliage);
        EditorUtility.SetDirty(foliage);
        var soft = MakeSoftParticle();
        flame = ParticleMaterial("Fire glow", soft, BlendMode.One);
        smoke = ParticleMaterial("Soft wood smoke", soft, BlendMode.OneMinusSrcAlpha);
        rocks = new[] { "Rock_Boulder_A", "Rock_Boulder_B", "Rock_Boulder_C", "Rock_Slab_A", "Rock_Slab_B" }
            .Select(name => Load<Mesh>("Assets/Generated/Meshes/" + name + ".asset")).ToArray();
    }

    private static void SetAlphaCutout(Material material)
    {
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", .37f);
        material.SetFloat("_Cull", 0);
        material.EnableKeyword("_ALPHATEST_ON");
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.renderQueue = (int)RenderQueue.AlphaTest;
    }

    private Material RockMaterial()
    {
        var result = new Material(FindShader("Echo/Forest Triplanar")) { name = "Mottled cliff rock" };
        var surface = surfaces["Rock"];
        result.SetTexture("_BaseMap", surface.Color);
        result.SetTexture("_NormalMap", surface.Normal);
        result.SetTexture("_NoiseMap", Load<Texture2D>("Assets/Generated/Textures/EchoNoise.png"));
        result.SetColor("_BaseColor", Tint("#ACA9A1"));
        result.SetFloat("_BaseScale", .65f);
        result.SetFloat("_NormalScale", .61f);
        result.SetFloat("_Smoothness", .20f);
        result.SetFloat("_Metallic", 0);
        result.SetFloat("_Warp", .18f);
        result.SetFloat("_DetailBlend", .48f);
        result.EnableKeyword("_TWO_SCALE");
        return Save(result, result.name + ".mat");
    }

    private Material Lit(string name, string color, float smoothness, string surfaceName = null,
        float normalStrength = 1, float metallic = 0)
    {
        var material = new Material(FindShader("Universal Render Pipeline/Lit"))
        {
            name = name,
            enableInstancing = true
        };
        material.SetColor("_BaseColor", Tint(color));
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        if (surfaceName != null)
        {
            var surface = surfaces[surfaceName];
            material.SetTexture("_BaseMap", surface.Color);
            material.SetTexture("_BumpMap", surface.Normal);
            material.SetTexture("_OcclusionMap", surface.Occlusion);
            material.SetFloat("_BumpScale", normalStrength);
            material.SetFloat("_OcclusionStrength", .64f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_OCCLUSIONMAP");
        }

        return Save(material, name + ".mat");
    }

    private static Material ParticleMaterial(string name, Texture2D texture, BlendMode destination)
    {
        var material = new Material(FindShader("Universal Render Pipeline/Particles/Unlit")) { name = name };
        material.SetTexture("_BaseMap", texture);
        material.SetFloat("_Surface", 1);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)destination);
        material.SetFloat("_ZWrite", 0);
        material.SetFloat("_Cull", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        return Save(material, name + ".mat");
    }

    private static Color Tint(string hex)
    {
        if (!ColorUtility.TryParseHtmlString(hex, out var color)) throw new ArgumentException(hex);
        return color;
    }

    private static Shader FindShader(string name) =>
        Shader.Find(name) ?? throw new InvalidOperationException("Missing shader " + name);

    private static T Load<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new IOException("Missing asset " + path);

    private static T Save<T>(T value, string filename) where T : Object
    {
        string path = Art + "/" + filename;
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(value, path);
            return value;
        }

        EditorUtility.CopySerialized(value, existing);
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(value);
        return existing;
    }

    private float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

    private static Transform Group(string name, Transform parent)
    {
        var item = new GameObject(name).transform;
        if (parent != null) item.SetParent(parent, false);
        return item;
    }

    private static MeshRenderer MeshObject(string name, Transform parent, Mesh mesh, Material material,
        ShadowCastingMode shadows = ShadowCastingMode.On)
    {
        var item = Group(name, parent).gameObject;
        item.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = item.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = shadows;
        return renderer;
    }

    private static void BoxCollider(Transform parent, Vector3 center, Vector3 size, Quaternion rotation = default)
    {
        var volume = Group("Collision", parent);
        volume.localPosition = center;
        volume.localRotation = rotation == default ? Quaternion.identity : rotation;
        volume.gameObject.AddComponent<BoxCollider>().size = size;
    }

    private static void BuildPlayer(Transform parent)
    {
        const string prefabs = "Assets/StarterAssets/FirstPersonController/Prefabs/";
        var player = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(prefabs + "PlayerCapsule.prefab"), parent);
        player.name = "Trailhead player";
        player.transform.SetPositionAndRotation(new Vector3(Spawn.x, Height(Spawn.x, Spawn.y) + .08f, Spawn.y),
            Quaternion.Euler(0, 23, 0));
        player.transform.Find("Capsule").gameObject.SetActive(false);
        var controller = player.GetComponent<CharacterController>();
        controller.height = 1.78f;
        controller.radius = .33f;
        controller.center = Vector3.up * .89f;
        controller.stepOffset = .27f;
        var playerInput = player.GetComponent<FirstPersonController>();
        playerInput.JumpHeight = .72f;
        playerInput.RotationSpeed = .75f;
        playerInput.GroundedRadius = .34f;
        playerInput.GroundedOffset = -.05f;
        var mount = playerInput.CinemachineCameraTarget.transform;
        mount.localPosition = Vector3.up * 1.62f;

        var camera = Group("Main Camera", mount).gameObject;
        camera.tag = "MainCamera";
        var lens = camera.AddComponent<Camera>();
        lens.fieldOfView = 67;
        lens.nearClipPlane = .06f;
        lens.farClipPlane = 170f;
        lens.allowHDR = true;
        camera.AddComponent<AudioListener>();
        camera.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
    }

    private static void BuildLighting(Transform parent)
    {
        var storm = Load<Material>("Assets/SkySeries Freebie/DarkStorm.mat");
        var sky = new Material(FindShader("Echo/Cabin Overcast Sky")) { name = "Overcast forest sky" };
        sky.SetTexture("_Clouds", storm.GetTexture("_Tex"));
        RenderSettings.skybox = Save(sky, "Overcast forest sky.mat");
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Tint("#82949A");
        RenderSettings.fogDensity = .027f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Tint("#B1BEC3");
        RenderSettings.ambientEquatorColor = Tint("#899697");
        RenderSettings.ambientGroundColor = Tint("#777D76");
        RenderSettings.ambientIntensity = 1.18f;
        RenderSettings.reflectionIntensity = .53f;

        var sunlight = Group("Overcast evening sun", parent);
        sunlight.rotation = Quaternion.Euler(39, 47, 0);
        var sun = sunlight.gameObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.34f;
        sun.color = Tint("#E0E9F2");
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = .59f;
        sun.shadowBias = .07f;
        sun.shadowNormalBias = .37f;
        RenderSettings.sun = sun;
        OvercastFill(parent, "Cool skylight from east", 47, -105, "#BCD0E2", .44f);
        OvercastFill(parent, "Low gray sky from northwest", 22, 158, "#DFE6D8", .25f);

        string path = Art + "/Forest camera grade.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Forest camera grade";
            AssetDatabase.CreateAsset(profile, path);
        }
        SetVolumeProfile(profile);
        var volume = Group("Global color grade", parent).gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    private static void OvercastFill(Transform parent, string name, float tilt, float heading, string color, float intensity)
    {
        var item = Group(name, parent);
        item.localRotation = Quaternion.Euler(tilt, heading, 0);
        var light = item.gameObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Tint(color);
        light.intensity = intensity;
    }

    private static void SetVolumeProfile(VolumeProfile profile)
    {
        var grade = VolumeComponent<ColorAdjustments>(profile, "Color Adjustments");
        grade.postExposure.Override(.31f);
        grade.contrast.Override(3);
        grade.saturation.Override(-13);
        var bloom = VolumeComponent<Bloom>(profile, "Bloom");
        bloom.intensity.Override(.20f);
        bloom.threshold.Override(1.15f);
        var tone = VolumeComponent<Tonemapping>(profile, "Tonemapping");
        tone.mode.Override(TonemappingMode.ACES);
        EditorUtility.SetDirty(profile);
    }

    private static T VolumeComponent<T>(VolumeProfile profile, string name) where T : VolumeComponent
    {
        if (!profile.TryGet<T>(out var effect))
        {
            effect = profile.Add<T>();
            effect.name = name;
            AssetDatabase.AddObjectToAsset(effect, profile);
        }
        EditorUtility.SetDirty(effect);
        return effect;
    }

    private static Light PointLight(Transform parent, string name, Vector3 position, string color,
        float intensity, float range)
    {
        var item = Group(name, parent);
        item.localPosition = position;
        var light = item.gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = Tint(color);
        light.intensity = intensity;
        light.range = range;
        return light;
    }
}
