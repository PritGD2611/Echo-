using TMPro;
using UnityEngine;

internal sealed partial class CabinLevelBuilder
{
    private void BuildCompound(Transform parent)
    {
        var approach = Group("Trailhead stair and fallen-tree detour", parent);
        Stairs(approach, "Main approach stairs", V(45.31f, 2.48f, 16.98f), V(45.31f, Terrace, 19.99f), 2.03f);
        var trail = new Parts(approach, "Trailhead barrier and signage");
        var left = V(31.85f, Height(31.85f, 17.7f) + .27f, 17.7f);
        var right = V(41.1f, Height(41.1f, 21.0f) + .28f, 21.0f);
        trail.With(bark).Tube(new[] { left, Vector3.Lerp(left, right, .37f) + V(0, .49f, 0), right },
            new[] { .48f, .40f, .23f }, 12, new Vector2(.3f, .6f), new Vector2(1.2f, .67f));
        trail.With(endGrain).Cap(left, (left - right).normalized, .48f, 12);
        trail.With(endGrain).Cap(right, (right - left).normalized, .23f, 12);
        Capsule(approach, left, Vector3.Lerp(left, right, .59f) + V(0, .38f, 0), .35f);
        Capsule(approach, Vector3.Lerp(left, right, .57f) + V(0, .36f, 0), right, .25f);
        for (int i = 0; i < 15; i++)
        {
            float at = Range(.10f, .92f);
            var point = Vector3.Lerp(left, right, at) + V(0, Mathf.Sin(at * Mathf.PI) * .43f, 0);
            var end = point + V(Range(-.75f, .76f), Range(.13f, 1.28f), Range(-1.54f, 1.38f));
            trail.Round(bark, point, end, Range(.052f, .09f), .016f, 6, endGrain);
        }
        for (int i = 0; i < 5; i++)
        {
            var root = left + V(-.12f, 0, 0);
            var branch = root + V(Range(-1.0f, -.24f), Range(-.31f, .91f), Range(-.92f, .99f));
            trail.Round(bark, root, branch, .10f, .022f, 6, endGrain);
        }
        Wayfinding(trail, approach, 38.17f, 14.73f, V(-.38f, 0, -1), "CABIN  >");
        Wayfinding(trail, approach, 35.45f, 17.68f, V(0, 0, -1), "TRAIL CLOSED", rust, .91f);
        Wayfinding(trail, approach, 52.64f, 42.38f, V(-.17f, 0, -1), "WOODS TRAIL", darkWood, 1.08f);
        trail.Finish();

        BuildStorage(Group("Outdoor storage sheds", parent));
        BuildCampfire(Group("Campfire gravel court", parent));
        BuildBarbecue(Group("Raised BBQ platform", parent));
        BuildRestrooms(Group("Restroom gravel court", parent));

        var yardTransform = Group("Backyard and woods exit", parent);
        var yard = new Parts(yardTransform, "Backyard fixtures");
        Table(yard, V(59.11f, Terrace, 39.34f), 2.06f, .83f, .73f);
        Bench(yard, V(59.11f, Terrace, 40.03f), 1.77f, .34f);
        Bench(yard, V(59.11f, Terrace, 38.65f), 1.77f, .34f);
        Rail(yard, V(55.82f, Terrace, 42.31f), V(62.5f, Terrace, 42.31f));
        Rail(yard, V(40.14f, Terrace, 41.92f), V(49.4f, Terrace, 41.92f));
        Lantern(yard, yardTransform, V(59.52f, Terrace + .96f, 39.25f), "Backyard table lantern", 8, 3.4f);
        yard.Finish();
    }

    private void Wayfinding(Parts parts, Transform parent, float x, float z, Vector3 outward, string text,
        Material material = null, float height = 1.35f)
    {
        var root = V(x, Height(x, z) - .07f, z);
        var rotation = Quaternion.LookRotation(outward);
        parts.Box(darkWood, root + V(0, height * .5f, 0), V(.13f, height, .135f), true);
        parts.Box(material ?? siding, root + V(0, height + .075f, 0) + outward.normalized * .089f,
            V(1.08f, .29f, .061f), rotation: rotation);
        TextOn(parent, text, root + V(0, height + .079f, 0) + outward.normalized * .124f, outward, .037f);
    }

    private static void TextOn(Transform parent, string text, Vector3 position, Vector3 outward, float size)
    {
        var item = new GameObject(text, typeof(RectTransform)).GetComponent<RectTransform>();
        item.SetParent(parent, false);
        item.localPosition = position;
        item.localRotation = Quaternion.LookRotation(-outward);
        item.sizeDelta = new Vector2(1.08f, .28f);
        var lettering = item.gameObject.AddComponent<TextMeshPro>();
        lettering.text = text;
        lettering.font = Load<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        lettering.fontSize = size * 48;
        lettering.fontStyle = FontStyles.Bold;
        lettering.alignment = TextAlignmentOptions.Center;
        lettering.textWrappingMode = TextWrappingModes.NoWrap;
        lettering.color = Tint("#E9DCBC");
        var renderer = item.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void BuildStorage(Transform parent)
    {
        StorageShed(Group("West equipment and wood store", parent), "West storage", 44.51f, 48.64f, 24.82f, 34.58f, true);
        StorageShed(Group("Backyard service shed", parent), "Service shed", 63.22f, 66.48f, 35.70f, 41.16f, false);
    }

    private void StorageShed(Transform parent, string name, float minX, float maxX, float minZ, float maxZ, bool openEast)
    {
        var shed = new Parts(parent, name);
        float floor = Terrace + .16f;
        float middleX = (minX + maxX) * .5f;
        float middleZ = (minZ + maxZ) * .5f;
        BoardFloor(shed, minX, maxX, minZ, maxZ, floor, .23f, siding);
        BoxCollider(parent, V(middleX, floor - .057f, middleZ), V(maxX - minX, .114f, maxZ - minZ));
        foreach (float z in new[] { minZ + .1f, maxZ - .1f })
        {
            shed.Box(darkWood, V(minX + .12f, floor + 1.25f, z), V(.18f, 2.55f, .18f), true);
            shed.Box(darkWood, V(maxX - .12f, floor + 1.36f, z), V(.18f, 2.72f, .18f), true);
        }
        ShedWall(shed, V(minX, floor, minZ), V(minX, floor, maxZ), 2.23f);
        ShedWall(shed, V(minX, floor, minZ), V(maxX, floor, minZ), 2.23f);
        if (openEast)
        {
            ShedWall(shed, V(minX, floor, maxZ), V(maxX, floor, maxZ), 2.23f);
            shed.Box(darkWood, V(maxX - .11f, floor + 1.32f, middleZ), V(.16f, 2.64f, .17f), true);
        }
        else ShedWall(shed, V(maxX, floor, minZ), V(maxX, floor, maxZ), 2.30f);
        RoofPanel(shed, minX - .30f, maxX + .29f, minZ - .26f, maxZ + .32f, floor + 2.48f, floor + 2.77f);
        Firewood(shed, V(minX + .41f, floor, minZ + .55f), 8, 6);
        if (openEast) Firewood(shed, V(minX + .44f, floor, minZ + 3.3f), 8, 5);
        Crate(shed, V(minX + 1.65f, floor, maxZ - 1.09f), V(.71f, .53f, .64f));
        Crate(shed, V(minX + 1.67f, floor + .53f, maxZ - 1.10f), V(.66f, .47f, .55f));
        Crate(shed, V(minX + .47f, floor, maxZ - 2.06f), V(.65f, .41f, .54f));
        shed.Box(siding, V(minX + .44f, floor + 1.31f, maxZ - 1.27f), V(.78f, .087f, 2.18f));
        shed.Round(rust, V(minX + .7f, floor, middleZ + .81f), V(minX + .7f, floor + .73f, middleZ + .81f),
            .27f, .27f, 14, metal, true);
        Lantern(shed, parent, V(openEast ? maxX - .24f : middleX, floor + 2.04f, maxZ - .42f),
            "Storage lantern", 10, 4.1f);
        shed.Finish();
    }

    private void ShedWall(Parts parts, Vector3 a, Vector3 b, float height)
    {
        float length = Vector3.Distance(a, b);
        var rotation = Quaternion.LookRotation(Vector3.Cross((b - a).normalized, Vector3.up));
        int boards = Mathf.CeilToInt(length / .23f);
        for (int i = 0; i < boards; i++)
            parts.Box(siding, Vector3.Lerp(a, b, (i + .5f) / boards) + V(0, height * .5f, 0),
                V(length / boards - .008f, height, .095f), rotation: rotation, repeatU: 1.1f, repeatV: .67f);
        BoxCollider(parts.Parent, (a + b) * .5f + V(0, height * .5f, 0), V(length, height, .095f), rotation);
        parts.Box(darkWood, (a + b) * .5f + V(0, height - .12f, 0) + rotation * V(0, 0, .057f),
            V(length, .13f, .11f), rotation: rotation);
    }

    private void Firewood(Parts parts, Vector3 p, int columns, int rows)
    {
        for (int y = 0; y < rows; y++)
        {
            for (int z = 0; z < columns - y % 2; z++)
            {
                var a = p + V(Range(-.06f, .05f), .10f + .143f * y, z * .191f + y % 2 * .09f);
                var b = a + V(Range(.33f, .55f), Range(-.028f, .021f), Range(-.054f, .055f));
                float radius = Range(.070f, .097f);
                parts.Round(bark, a, b, radius, radius * .91f, 6, endGrain);
            }
        }
        BoxCollider(parts.Parent, p + V(.23f, rows * .07f, columns * .095f), V(.58f, rows * .14f, columns * .19f));
    }

    private void BuildCampfire(Transform parent)
    {
        var fire = new Parts(parent, "Campfire props");
        float x = 72.11f, z = 32.84f;
        var center = V(x, Height(x, z), z);
        fire.Round(ash, center - V(0, .028f, 0), center + V(0, .044f, 0), .79f, .77f, 19);
        for (int i = 0; i < 17; i++)
        {
            float angle = Mathf.PI * 2 * (i + Range(-.14f, .14f)) / 17;
            var p = center + V(Mathf.Cos(angle) * .81f, .11f, Mathf.Sin(angle) * .81f);
            PlaceRock(parent, "Fire ring stone", p, V(Range(.19f, .31f), Range(.12f, .23f), Range(.19f, .28f)),
                i % rocks.Length, angle * Mathf.Rad2Deg, false);
        }
        for (int i = 0; i < 9; i++)
        {
            float angle = Range(0, Mathf.PI * 2);
            var d = V(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var from = center + d * Range(.32f, .60f) + V(0, Range(.06f, .17f), 0);
            fire.Round(i % 3 == 0 ? bark : charcoal, from, center - d * Range(.12f, .31f) + V(0, Range(.15f, .48f), 0),
                Range(.055f, .10f), .047f, 7, charcoal);
        }
        BoxCollider(parent, center + V(0, .14f, 0), V(1.52f, .28f, 1.45f));
        Bench(fire, V(72.0f, Terrace, 30.55f), 2.55f, .54f);
        Bench(fire, V(71.82f, Terrace, 35.05f), 2.4f, .51f);
        fire.Box(siding, V(74.29f, Terrace + .31f, 32.80f), V(.49f, .60f, .54f), true, Quaternion.Euler(0, 12, 0));
        Crate(fire, V(70.1f, Terrace, 35.17f), V(.63f, .43f, .43f));
        Lantern(fire, parent, V(74.26f, Terrace + .79f, 32.78f), "Camp table lantern", 11, 3.2f);
        fire.Finish();

        PointLight(parent, "Firelight", center + V(0, .87f, 0), "#FF8747", 16, 4.8f);
        Particles(parent, "Campfire flames", center + V(0, .24f, 0), flame, 62, .62f, .66f, .20f, .98f,
            new Color(1f, .59f, .16f, 1f), new Color(1f, .19f, .03f, .045f), true);
        Particles(parent, "Campfire sparks", center + V(0, .31f, 0), flame, 8, .65f, .049f, .30f, 1.75f,
            new Color(1f, .66f, .17f, .94f), new Color(.91f, .25f, .03f, 0));
        Particles(parent, "Drifting wood smoke", center + V(0, .66f, 0), smoke, 7, 2.25f, .73f, .34f, .81f,
            new Color(.51f, .50f, .48f, .21f), new Color(.47f, .49f, .50f, 0));
    }

    private static void Particles(Transform parent, string name, Vector3 position, Material material,
        float rate, float lifetime, float size, float radius, float speed, Color start, Color finish, bool stretch = false)
    {
        var item = Group(name, parent);
        item.SetPositionAndRotation(position, Quaternion.Euler(-80, 0, 0));
        var particles = item.gameObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.useAutoRandomSeed = false;
        particles.randomSeed = (uint)(size * 1049 + 17);
        var main = particles.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.maxParticles = 120;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .71f, lifetime * 1.23f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .73f, speed * 1.32f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * .5f, size * 1.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = particles.emission;
        emission.rateOverTime = rate;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12;
        shape.radius = radius;
        var colors = particles.colorOverLifetime;
        colors.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(start, 0), new GradientColorKey(finish, 1) },
            new[] { new GradientAlphaKey(start.a, 0), new GradientAlphaKey(start.a * .84f, .32f),
                new GradientAlphaKey(finish.a, 1) });
        colors.color = gradient;
        var sizeOverLife = particles.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .58f),
            new Keyframe(.29f, 1.05f), new Keyframe(1, material.name == "Soft wood smoke" ? 2.25f : .20f)));
        var renderer = item.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        if (stretch)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.45f;
            renderer.velocityScale = .32f;
        }
        renderer.maxParticleSize = .50f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void BuildBarbecue(Transform parent)
    {
        var deck = new Parts(parent, "Barbecue deck and grills");
        const float y = Terrace + .81f;
        const float left = 72.42f, right = 76.67f, near = 37.78f, far = 42.08f;
        BoardFloor(deck, left, right, near, far, y, .19f, siding);
        BoxCollider(parent, V((left + right) * .5f, y - .06f, (near + far) * .5f),
            V(right - left, .12f, far - near));
        deck.Box(darkWood, V((left + right) * .5f, y - .17f, (near + far) * .5f),
            V(right - left + .074f, .17f, far - near + .074f));
        foreach (float x in new[] { left + .26f, right - .26f })
            foreach (float z in new[] { near + .24f, far - .24f })
                deck.Box(darkWood, V(x, y - .50f, z), V(.21f, .86f, .21f));
        Rail(deck, V(right - .08f, y, near + .06f), V(right - .08f, y, far - .08f));
        Rail(deck, V(left + .07f, y, far - .08f), V(right - .08f, y, far - .08f));
        Rail(deck, V(left + .07f, y, near + .06f), V(left + .07f, y, far - .08f));
        Rail(deck, V(74.84f, y, near + .06f), V(right - .08f, y, near + .06f));
        Stairs(parent, "BBQ deck stairs", V(73.63f, Terrace, 36.38f), V(73.63f, y, near + .025f), 1.73f);

        var grill = V(75.53f, y, 39.73f);
        deck.Round(metal, grill + V(0, .56f, 0), grill + V(0, .84f, 0), .22f, .48f, 18, rust, true);
        deck.Round(rust, grill + V(0, .83f, 0), grill + V(0, .90f, 0), .49f, .47f, 18);
        deck.Round(metal, grill + V(0, .90f, 0), grill + V(0, 1.15f, 0), .47f, .22f, 18);
        deck.Box(darkWood, grill + V(0, 1.20f, 0), V(.27f, .063f, .082f));
        for (int i = 0; i < 3; i++)
        {
            float angle = (i * 120 + 30) * Mathf.Deg2Rad;
            var reach = V(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            deck.Round(metal, grill + reach * .28f + V(0, .09f, 0), grill + reach * .14f + V(0, .61f, 0), .033f, .031f, 7);
        }
        Table(deck, V(73.48f, y, 41.04f), 1.51f, .61f, .83f);
        deck.Box(porcelain, V(73.77f, y + .86f, 40.96f), V(.34f, .042f, .213f));
        Lantern(deck, parent, V(73.13f, y + 1.02f, 41.11f), "BBQ prep lantern", 12, 3.5f);
        deck.Finish();
    }

    private void BuildRestrooms(Transform parent)
    {
        BuildRestroomUnit(Group("Portable toilet 01", parent), "Teal portable toilet", V(75.15f, Height(75.15f, 20.79f), 20.79f),
            toiletTeal, "01");
        BuildRestroomUnit(Group("Portable toilet 02", parent), "Blue portable toilet", V(75.14f, Height(75.14f, 22.64f), 22.64f),
            toiletBlue, "02");
        var court = new Parts(parent, "Restroom screen and handwashing station");
        float screenZ = 26.34f;
        for (int i = 0; i < 33; i++)
        {
            float x = 69.9f + i * .205f;
            float y = Height(x, screenZ);
            court.Box(siding, V(x, y + .89f, screenZ), V(.18f, Range(1.75f, 1.86f), .083f));
        }
        foreach (float x in new[] { 69.91f, 72.13f, 74.36f, 76.45f })
            court.Box(darkWood, V(x, Terrace + .95f, screenZ + .09f), V(.15f, 1.94f, .15f));
        foreach (float y in new[] { Terrace + .46f, Terrace + 1.43f })
            court.Box(darkWood, V(73.18f, y, screenZ + .08f), V(6.72f, .12f, .10f));
        BoxCollider(parent, V(73.18f, Terrace + .94f, screenZ), V(6.72f, 1.80f, .14f));
        court.Box(toiletTeal, V(74.1f, Terrace + .45f, 24.81f), V(.60f, .9f, .49f), true);
        court.Box(roofPlastic, V(74.1f, Terrace + .91f, 24.81f), V(.72f, .12f, .60f));
        court.Box(metal, V(74.17f, Terrace + .977f, 24.82f), V(.46f, .019f, .36f));
        court.Round(metal, V(74.36f, Terrace + .96f, 24.84f), V(74.36f, Terrace + 1.19f, 24.84f),
            .019f, .019f, 7);
        court.Box(metal, V(74.29f, Terrace + 1.18f, 24.84f), V(.16f, .039f, .039f));
        Wayfinding(court, parent, 70.52f, 25.38f, V(-.83f, 0, -.52f), "RESTROOMS", darkWood, 1.16f);
        court.Finish();
    }

    private void BuildRestroomUnit(Transform parent, string name, Vector3 position, Material body, string number)
    {
        parent.SetPositionAndRotation(position, Quaternion.Euler(0, 88, 0));
        var unit = new Parts(parent, name);
        unit.Box(metal, V(0, .078f, 0), V(1.25f, .15f, 1.39f));
        unit.Box(body, V(0, 1.08f, 0), V(1.15f, 1.99f, 1.26f), true);
        unit.Box(roofPlastic, V(0, 2.10f, 0), V(1.23f, .115f, 1.35f));
        unit.Box(roofPlastic, V(0, 2.18f, .015f), V(1.07f, .077f, 1.23f));
        unit.Box(metal, V(0, 1.079f, -.635f), V(.91f, 1.82f, .03f));
        unit.Box(body, V(0, 1.075f, -.661f), V(.876f, 1.775f, .046f));
        unit.Box(body, V(0, 1.10f, -.690f), V(.73f, 1.40f, .025f));
        unit.Box(metal, V(.338f, 1.063f, -.715f), V(.094f, .19f, .04f));
        unit.Box(brass, V(.322f, 1.08f, -.745f), V(.078f, .034f, .04f));
        for (int i = 0; i < 2; i++)
            unit.Box(metal, V(-.455f, .51f + i * 1.05f, -.67f), V(.08f, .13f, .06f));
        for (int i = 0; i < 6; i++)
        {
            unit.Box(metal, V(-.32f + i * .127f, 1.854f, -.695f), V(.067f, .024f, .021f));
            foreach (float sign in new[] { -1, 1 })
                unit.Box(body, V(sign * .583f, 1.06f, -.44f + .172f * i), V(.031f, 1.71f, .048f));
        }
        unit.Box(metal, V(0, 1.56f, -.709f), V(.28f, .25f, .026f));
        unit.Round(metal, V(-.34f, 2.18f, .39f), V(-.34f, 2.31f, .39f), .067f, .066f, 9);
        TextOn(parent, number, V(0, 1.56f, -.725f), Vector3.back, .042f);
        unit.Finish();
    }
}
