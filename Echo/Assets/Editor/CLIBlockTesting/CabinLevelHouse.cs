using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed partial class CabinLevelBuilder
{
    private readonly struct Opening
    {
        public readonly float Start, End, Bottom, Top;
        public readonly bool Window;

        public Opening(float center, float width, float bottom, float top, bool window = false)
        {
            Start = center - width * .5f;
            End = center + width * .5f;
            Bottom = bottom;
            Top = top;
            Window = window;
        }
    }

    private void BuildCabin(Transform parent)
    {
        var floor = new Parts(Group("Stone footings and plank floors", parent), "Cabin floors");
        for (var x = West + .35f; x < East; x += 2.6f)
        {
            for (var z = 20.3f; z <= Back; z += 3.3f)
            {
                float y = Height(x, z);
                floor.Box(stone, V(x, (Floor + y) * .5f - .1f, z), V(.63f, Floor - y - .2f, .61f));
                floor.Box(darkWood, V(x, Floor - .24f, z), V(.28f, .19f, 3.36f));
            }
        }
        floor.Box(darkWood, V(CabinCenter, Floor - .18f, 27.31f), V(14.27f, .26f, 14.65f));
        BoardFloor(floor, 52.41f, 66.62f, 19.98f, 34.65f, Floor, .24f, planks);
        BoxCollider(parent, V(CabinCenter + .015f, Floor - .11f, 27.315f), V(14.21f, .22f, 14.67f));
        floor.Finish();

        var foundation = new Parts(Group("Dry-laid masonry foundation", parent), "Stone foundation");
        StoneCourse(foundation, V(West - .02f, 0, Front), V(East + .02f, 0, Front));
        StoneCourse(foundation, V(West - .02f, 0, Back), V(East + .02f, 0, Back));
        StoneCourse(foundation, V(West - .02f, 0, Front), V(West - .02f, 0, Back));
        StoneCourse(foundation, V(East + .02f, 0, Front), V(East + .02f, 0, Back));
        foundation.Finish();

        var shell = new Parts(Group("Notched log walls, doors and sash windows", parent), "Cabin shell");
        LogWall(shell, parent, V(West, 0, Front), V(East, 0, Front), true,
            new Opening(54.65f - West, 1.72f, Floor, 8.84f),
            new Opening(59.48f - West, 2.53f, 7.47f, 8.86f, true),
            new Opening(64.2f - West, 1.35f, 7.62f, 8.85f, true));
        LogWall(shell, parent, V(West, 0, Back), V(East, 0, Back), true,
            new Opening(54.63f - West, 1.72f, Floor, 8.84f),
            new Opening(61.94f - West, 2.21f, 7.47f, 8.87f, true));
        LogWall(shell, parent, V(West, 0, Front), V(West, 0, Back), false,
            new Opening(27.9f - Front, 2.34f, 7.45f, 8.87f, true));
        LogWall(shell, parent, V(East, 0, Front), V(East, 0, Back), false,
            new Opening(24.15f - Front, 1.63f, Floor, 8.83f),
            new Opening(28.45f - Front, 1.89f, 7.47f, 8.86f, true),
            new Opening(32.38f - Front, 1.67f, Floor, 8.83f));
        shell.Finish();

        var partitions = new Parts(Group("Bedroom walls and interior finish", parent), "Bedroom partitions");
        Partition(partitions, V(West + .14f, 0, 25.08f), V(63.03f, 0, 25.08f));
        Partition(partitions, V(West + .14f, 0, 31.03f), V(63.03f, 0, 31.03f));
        Partition(partitions, V(63.03f, 0, 25.08f), V(63.03f, 0, 26.05f));
        Partition(partitions, V(63.03f, 0, 28.01f), V(63.03f, 0, 31.03f));
        partitions.Box(siding, V(63.03f, 9.19f, 27.03f), V(.135f, .72f, 1.96f));
        Frame(partitions, V(63.03f, 0, 25.08f), Vector3.forward, new Opening(1.95f, 1.90f, Floor, 8.8f));
        partitions.Finish();

        var roof = new Parts(Group("Pitched cedar roof, gables and chimney", parent), "Cabin roof");
        BuildRoof(roof);
        roof.Finish();

        var access = Group("Porches and four entrances", parent);
        Stairs(access, "Front porch west stairs", V(50.26f, Terrace, 20.94f), V(52.42f, Floor, 20.94f), 1.96f);
        Stairs(access, "Front porch east stairs", V(68.76f, Terrace, 20.98f), V(66.61f, Floor, 20.98f), 1.95f);
        Stairs(access, "Restroom entrance stairs", V(68.76f, Terrace, 24.15f), V(66.52f, Floor, 24.15f), 1.98f);
        Stairs(access, "Campfire entrance stairs", V(68.76f, Terrace, 32.38f), V(66.52f, Floor, 32.38f), 1.98f);
        Stairs(access, "Backyard entrance stairs", V(54.63f, Terrace, 36.8f), V(54.63f, Floor, 34.61f), 1.99f);

        var porch = new Parts(access, "Cabin porch");
        Rail(porch, V(52.61f, Floor, 20.06f), V(66.42f, Floor, 20.06f));
        RoofPanel(porch, 52.12f, 66.89f, 19.20f, 22.36f, 8.92f, 9.67f);
        foreach (float x in new[] { 52.67f, 56.9f, 62.52f, 66.36f })
        {
            porch.Box(darkWood, V(x, 7.68f, 20.08f), V(.17f, 2.57f, .17f));
            porch.Beam(siding, V(x, 8.26f, 20.08f), V(x + (x > 60 ? -.53f : .53f), 9.00f, 20.08f), .115f, .12f);
        }
        Lantern(porch, access, V(56.60f, 8.42f, 22.01f), "Porch lantern", 20, 5.4f);
        Lantern(porch, access, V(66.57f, 8.40f, 33.53f), "Camp door lantern", 17, 4.4f);
        Lantern(porch, access, V(55.85f, 8.44f, 34.64f), "Back door lantern", 15, 4.1f);
        porch.Finish();
        CabinFurniture(Group("Bedroom, living room and stove", parent));
    }

    private void StoneCourse(Parts parts, Vector3 start, Vector3 end)
    {
        Vector3 axis = (end - start).normalized;
        var normal = Vector3.Cross(axis, Vector3.up);
        float length = Vector3.Distance(start, end);
        var rotation = Quaternion.LookRotation(normal);
        const int rows = 5;
        float baseY = Terrace - .12f;
        float height = (Floor - .12f - baseY) / rows;
        parts.Box(stone, Vector3.Lerp(start, end, .5f) + V(0, baseY + height * rows * .5f, 0),
            V(length, height * rows, .30f), true, rotation);
        for (int row = 0; row < rows; row++)
        {
            float y = baseY + height * (row + .5f);
            for (float at = 0; at < length - .018f;)
            {
                float next = Mathf.Min(length, at + Range(.34f, .89f));
                if (length - next < .19f) next = length;
                var pivot = start + axis * ((at + next) * .5f) + V(0, y + Range(-.018f, .019f), 0);
                parts.Box(stone, pivot, V(next - at - .023f, height * Range(.92f, 1.06f), Range(.35f, .48f)),
                    rotation: rotation * Quaternion.Euler(Range(-3, 3), Range(-3, 3), Range(-3, 3)));
                at = next;
            }
        }
    }

    private void BoardFloor(Parts parts, float left, float right, float near, float far, float y, float width, Material material)
    {
        int boards = Mathf.CeilToInt((right - left) / width);
        var rotation = Quaternion.Euler(0, 90, 0);
        for (int i = 0; i < boards; i++)
        {
            float a = Mathf.Lerp(left, right, i / (float)boards);
            float b = Mathf.Lerp(left, right, (i + 1f) / boards);
            float z = near;
            while (z < far - .025f)
            {
                float end = Mathf.Min(far, z + Range(1.2f, 3.8f));
                if (far - end < .25f) end = far;
                int textureRow = (i * 7 + Mathf.FloorToInt(z * 3)) % 16;
                parts.With(material).Box(V((a + b) * .5f, y - .053f, (z + end) * .5f),
                    V(end - z - .009f, .106f, b - a - .008f), rotation,
                    new Vector2(Mathf.Repeat(z * .173f + i * .451f, 1), (textureRow + .11f) / 16),
                    new Vector2(.48f, .047f / (b - a)));
                z = end;
            }
        }
    }

    private void LogWall(Parts parts, Transform collision, Vector3 a, Vector3 b, bool xAxis, params Opening[] openings)
    {
        var axis = (b - a).normalized;
        float length = Vector3.Distance(a, b);
        const int courses = 12;
        float step = (Eave - Floor) / courses;
        for (int i = 0; i < courses; i++)
        {
            float y = Floor + step * (i + .5f);
            float radius = Range(.145f, .160f);
            float overhang = (i % 2 == (xAxis ? 0 : 1)) ? .31f : .20f;
            float start = -overhang;
            foreach (var opening in openings.Where(item => y >= item.Bottom && y <= item.Top))
            {
                AddLog(start, opening.Start, y, radius);
                start = opening.End;
            }
            AddLog(start, length + overhang, y, radius);
        }

        void AddLog(float start, float end, float y, float radius)
        {
            if (end <= start + .03f) return;
            var offset = Vector3.Cross(Vector3.up, axis) * Range(-.013f, .013f);
            var left = a + axis * start + V(0, y, 0) + offset;
            var right = a + axis * end + V(0, y + Range(-.009f, .009f), 0) + offset;
            parts.Round(logs, left, right, radius, radius * Range(.94f, 1.07f), 11, endGrain,
                axialRepeat: .54f, axialU: true);
        }

        var cuts = new SortedSet<float> { Floor, Eave };
        foreach (var opening in openings)
        {
            cuts.Add(opening.Bottom);
            cuts.Add(opening.Top);
        }
        var heights = cuts.ToArray();
        for (int i = 0; i < heights.Length - 1; i++)
        {
            float lower = heights[i], upper = heights[i + 1], mid = (lower + upper) * .5f;
            float start = 0;
            foreach (var opening in openings.Where(item => mid > item.Bottom && mid < item.Top))
            {
                WallCollider(start, opening.Start, mid, upper - lower);
                start = opening.End;
            }
            WallCollider(start, length, mid, upper - lower);
        }

        void WallCollider(float start, float end, float y, float height)
        {
            if (end <= start + .01f) return;
            BoxCollider(collision, a + axis * ((start + end) * .5f) + V(0, y, 0),
                xAxis ? V(end - start, height, .28f) : V(.28f, height, end - start));
        }

        foreach (var opening in openings)
        {
            Frame(parts, a, axis, opening);
            if (!opening.Window && (xAxis || a.x > CabinCenter))
                DoorLeaf(parts, a, axis, opening, xAxis && a.z > 30 ? 83 : -102);
        }
    }

    private void Frame(Parts parts, Vector3 a, Vector3 axis, Opening opening)
    {
        Quaternion rotation = Quaternion.LookRotation(Vector3.Cross(axis, Vector3.up));
        float width = opening.End - opening.Start;
        float height = opening.Top - opening.Bottom;
        float middle = (opening.Start + opening.End) * .5f;
        Vector3 At(float t, float y) => a + axis * t + Vector3.up * y;
        Material trim = opening.Window ? paintedWood : darkWood;
        foreach (float at in new[] { opening.Start, opening.End })
            parts.Box(trim, At(at, opening.Bottom + height * .5f), V(.155f, height + .15f, .42f), rotation: rotation);
        parts.Box(trim, At(middle, opening.Top), V(width + .18f, .218f, .44f), rotation: rotation);

        if (!opening.Window) return;
        parts.Box(trim, At(middle, opening.Bottom), V(width + .29f, .183f, .50f), rotation: rotation);
        parts.Box(glass, At(middle, opening.Bottom + height * .5f), V(width - .135f, height - .13f, .026f), rotation: rotation);
        parts.Box(siding, At(middle, opening.Bottom + height * .48f), V(width - .09f, .052f, .09f), rotation: rotation);
        for (int i = 1; i < 3; i++)
            parts.Box(siding, At(Mathf.Lerp(opening.Start, opening.End, i / 3f), opening.Bottom + height * .5f),
                V(.058f, height - .11f, .10f), rotation: rotation);
    }

    private void DoorLeaf(Parts parts, Vector3 a, Vector3 axis, Opening opening, float swing)
    {
        Vector3 hinge = a + axis * (opening.Start + .09f);
        Vector3 run = Quaternion.Euler(0, swing, 0) * axis;
        var forward = Vector3.Cross(run, Vector3.up);
        Quaternion rotation = Quaternion.LookRotation(forward);
        float width = opening.End - opening.Start - .18f;
        float height = opening.Top - opening.Bottom - .16f;
        var mid = hinge + run * width * .5f + V(0, opening.Bottom + height * .5f + .04f, 0);
        for (int i = 0; i < 8; i++)
        {
            float at = (i + .5f) / 8 - .5f;
            parts.Box(siding, mid + run * (width * at), V(width / 8 - .006f, height, .078f),
                rotation: rotation, repeatU: 1.25f, repeatV: .55f);
        }
        parts.Box(siding, mid, V(width - .02f, height - .04f, .055f), true, rotation);
        foreach (float offset in new[] { -.69f, .69f })
        {
            parts.Box(darkWood, mid + V(0, offset, 0) + forward * .052f, V(width - .05f, .115f, .057f), rotation: rotation);
            parts.Box(metal, hinge + V(0, mid.y + offset, 0), V(.10f, .21f, .10f), rotation: rotation);
        }
        parts.Round(brass, mid + run * (width * .31f) - forward * .082f, mid + run * (width * .31f) + forward * .085f,
            .034f, .034f, 9);
    }

    private void Partition(Parts parts, Vector3 a, Vector3 b)
    {
        Vector3 axis = (b - a).normalized;
        float length = Vector3.Distance(a, b);
        var rotation = Quaternion.LookRotation(Vector3.Cross(axis, Vector3.up));
        float height = Eave - Floor;
        int boards = Mathf.CeilToInt(length / .23f);
        for (int i = 0; i < boards; i++)
        {
            var point = a + axis * ((i + .5f) * length / boards) + V(0, Floor + height * .5f, 0);
            parts.Box(siding, point, V(length / boards - .006f, height, .112f), rotation: rotation,
                repeatU: 1.10f, repeatV: .55f);
        }
        BoxCollider(parts.Parent, (a + b) * .5f + V(0, Floor + height * .5f, 0), V(length, height, .11f), rotation);
        parts.Box(darkWood, (a + b) * .5f + V(0, Floor + .10f, 0), V(length, .15f, .16f), rotation: rotation);
    }

    private void BuildRoof(Parts roof)
    {
        const float overhang = .69f;
        float half = (East - West) * .5f + overhang;
        float start = Front - .82f, end = Back + .83f;
        float eave = Eave - .31f;
        float slope = Mathf.Atan2(Ridge - eave, half) * Mathf.Rad2Deg;
        float run = end - start;
        foreach (int direction in new[] { -1, 1 })
        {
            var rotation = Quaternion.Euler(0, 0, direction * -slope);
            var shakeRotation = Quaternion.Euler(0, 0, direction * (-slope + 4.5f));
            float slant = Vector2.Distance(new Vector2(0, Ridge), new Vector2(half, eave));
            roof.Box(darkWood, V(CabinCenter + half * .5f * direction, (Ridge + eave) * .5f - .12f, (start + end) * .5f),
                V(slant + .03f, .21f, run), rotation: rotation);
            for (int course = 0; course < 23; course++)
            {
                float fraction = (course + .48f) / 23;
                float x = CabinCenter + half * fraction * direction;
                float y = Mathf.Lerp(Ridge, eave, fraction);
                for (float z = start; z < end - .02f;)
                {
                    float next = Mathf.Min(end, z + Range(.26f, .57f));
                    if (end - next < .12f) next = end;
                    Material material = roofTones[random.Next(roofTones.Length)];
                    roof.Box(material, V(x, y + Range(.018f, .022f), (z + next) * .5f),
                        V(slant / 23 + .09f, .048f, next - z - .008f), rotation: shakeRotation,
                        repeatU: .72f, repeatV: 1.35f);
                    z = next;
                }
            }

            foreach (float z in new[] { start - .017f, end + .017f })
                roof.Beam(siding, V(CabinCenter, Ridge + .075f, z), V(CabinCenter + half * direction, eave, z), .23f, .14f);
            roof.Box(darkWood, V(CabinCenter + half * direction, eave - .13f, (start + end) * .5f), V(.16f, .18f, run));
        }

        for (float z = start; z < end; z += .39f)
        {
            float span = Mathf.Min(.37f, end - z);
            roof.Box(brownRoof, V(CabinCenter, Ridge + .062f, z + span * .5f), V(.26f, .10f, span));
        }

        foreach (float z in new[] { Front, Back })
        {
            int columns = Mathf.CeilToInt((East - West) / .23f);
            for (int i = 0; i < columns; i++)
            {
                float x = Mathf.Lerp(West, East, (i + .5f) / columns);
                float top = Mathf.Lerp(Ridge, eave, Mathf.Abs(x - CabinCenter) / half) - .04f;
                float height = top - Eave + .08f;
                roof.Box(siding, V(x, top - height * .5f, z), V((East - West) / columns - .004f, height, .16f),
                    repeatU: 1.05f, repeatV: .46f);
            }
            roof.Box(darkWood, V(CabinCenter, 10.37f, z + (z < 30 ? -.12f : .12f)), V(11.1f, .15f, .13f));
            roof.Box(darkWood, V(CabinCenter, 11.53f, z + (z < 30 ? -.145f : .145f)), V(1.13f, .53f, .07f));
            for (int i = 0; i < 5; i++)
                roof.Box(siding, V(CabinCenter, 11.33f + .09f * i, z + (z < 30 ? -.19f : .19f)), V(1.12f, .039f, .09f));
        }
        for (float z = 22.8f; z < Back; z += 2.15f)
        {
            roof.Beam(darkWood, V(West, Eave - .1f, z), V(CabinCenter, Ridge - .29f, z), .16f, .19f);
            roof.Beam(darkWood, V(East, Eave - .1f, z), V(CabinCenter, Ridge - .29f, z), .16f, .19f);
            roof.Box(siding, V(CabinCenter, Eave - .13f, z), V(East - West, .15f, .14f));
        }

        var pipe = V(62.03f, 0, 29.93f);
        roof.Round(metal, pipe + V(0, 8.2f, 0), pipe + V(0, 13.62f, 0), .145f, .145f, 13);
        roof.Round(metal, pipe + V(0, 13.63f, 0), pipe + V(0, 13.74f, 0), .28f, .18f, 12);
        roof.Box(greenRoof, pipe + V(0, 12.16f, 0), V(.70f, .067f, .66f), rotation: Quaternion.Euler(0, 0, -slope));
    }

    private void RoofPanel(Parts parts, float left, float right, float first, float last, float y1, float y2)
    {
        float depth = Vector2.Distance(new Vector2(first, y1), new Vector2(last, y2));
        var angle = Quaternion.Euler(-Mathf.Atan2(y2 - y1, last - first) * Mathf.Rad2Deg, 0, 0);
        parts.Box(darkWood, V((left + right) * .5f, (y1 + y2) * .5f - .1f, (first + last) * .5f),
            V(right - left, .17f, depth), rotation: angle);
        parts.Box(greenRoof, V((left + right) * .5f, (y1 + y2) * .5f, (first + last) * .5f),
            V(right - left + .1f, .065f, depth + .1f), rotation: angle);
        for (float x = left + .07f; x < right; x += .42f)
            parts.Box(greenRoof, V(x, (y1 + y2) * .5f + .039f, (first + last) * .5f), V(.035f, .043f, depth),
                rotation: angle);
    }

    private void Stairs(Transform parent, string name, Vector3 bottom, Vector3 top, float width)
    {
        var stairs = new Parts(Group(name, parent), name);
        var along = new Vector3(top.x - bottom.x, 0, top.z - bottom.z);
        var heading = along.normalized;
        var side = Vector3.Cross(Vector3.up, heading);
        var rotation = Quaternion.LookRotation(heading);
        int steps = Mathf.CeilToInt((top.y - bottom.y) / .185f);
        float going = along.magnitude / steps;
        float rise = (top.y - bottom.y) / steps;
        for (int i = 0; i < steps; i++)
        {
            Vector3 center = bottom + heading * (going * (i + .5f)) + Vector3.up * (rise * (i + 1));
            stairs.Box(siding, center - V(0, .055f, 0), V(width, .11f, going + .039f), true, rotation);
            stairs.Box(darkWood, center - V(0, rise * .65f, 0) - heading * going * .43f,
                V(width - .065f, rise, .058f), rotation: rotation);
        }
        foreach (float sign in new[] { -1, 1 })
        {
            var offset = side * (width * .43f * sign);
            stairs.Beam(darkWood, bottom + offset - V(0, .15f, 0), top + offset - V(0, .15f, 0), .14f, .22f);
            stairs.Beam(siding, bottom + offset + V(0, .87f, 0), top + offset + V(0, .87f, 0), .11f, .10f);
            for (int i = 0; i <= 2; i++)
            {
                var root = Vector3.Lerp(bottom, top, i * .5f) + offset;
                stairs.Box(darkWood, root + V(0, .35f, 0), V(.12f, 1.10f, .12f), true);
            }
        }
        stairs.Finish();
    }

    private void Rail(Parts parts, Vector3 a, Vector3 b)
    {
        float length = Vector3.Distance(a, b);
        Vector3 side = (b - a).normalized;
        var rotation = Quaternion.LookRotation(Vector3.Cross(side, Vector3.up));
        parts.Box(siding, (a + b) * .5f + V(0, .95f, 0), V(length, .12f, .15f), true, rotation);
        parts.Box(darkWood, (a + b) * .5f + V(0, .30f, 0), V(length, .11f, .10f), rotation: rotation);
        int gaps = Mathf.CeilToInt(length / .66f);
        for (int i = 0; i <= gaps; i++)
        {
            float thick = i % 4 == 0 || i == gaps ? .125f : .076f;
            parts.Box(darkWood, Vector3.Lerp(a, b, i / (float)gaps) + V(0, .44f, 0), V(thick, .88f, thick));
        }
        BoxCollider(parts.Parent, (a + b) * .5f + V(0, .5f, 0), V(length, 1f, .12f), rotation);
    }

    private void Lantern(Parts parts, Transform parent, Vector3 p, string name, float intensity, float range)
    {
        parts.Round(enamel, p - V(0, .118f, 0), p + V(0, .116f, 0), .081f, .078f, 9);
        parts.Round(metal, p - V(0, .16f, 0), p - V(0, .11f, 0), .105f, .105f, 9);
        parts.Round(metal, p + V(0, .11f, 0), p + V(0, .18f, 0), .11f, .053f, 9);
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.PI * .5f;
            var off = V(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * .078f;
            parts.Box(metal, p + off, V(.019f, .26f, .019f));
        }
        PointLight(parent, name, p, "#FFC385", intensity * .42f, range * .72f);
    }

    private void CabinFurniture(Transform parent)
    {
        var bedroom = new Parts(Group("Bed, nightstand and trunk", parent), "Bedroom furniture");
        const float bedX = 55.18f, bedZ = 29.35f;
        bedroom.Box(darkWood, V(bedX, Floor + .36f, bedZ), V(1.65f, .30f, 2.16f), true);
        bedroom.Box(canvas, V(bedX, Floor + .58f, bedZ), V(1.57f, .23f, 2.08f));
        bedroom.Box(cloth, V(bedX, Floor + .72f, bedZ - .30f), V(1.62f, .11f, 1.44f));
        bedroom.Box(cloth, V(bedX + .78f, Floor + .59f, bedZ - .30f), V(.11f, .27f, 1.44f));
        for (int i = -1; i <= 1; i += 2)
        {
            bedroom.Box(canvas, V(bedX + .39f * i, Floor + .75f, bedZ + .64f), V(.66f, .13f, .44f),
                rotation: Quaternion.Euler(0, i * 6, 0));
            for (int j = -1; j <= 1; j += 2)
                bedroom.Box(darkWood, V(bedX + .77f * i, Floor + .28f, bedZ + .96f * j), V(.14f, .56f, .14f));
        }
        bedroom.Box(siding, V(bedX, Floor + .89f, bedZ + 1.0f), V(1.66f, .65f, .12f), true);
        Crate(bedroom, V(57.22f, Floor, 30.4f), V(.94f, .58f, .67f));
        Crate(bedroom, V(53.60f, Floor, 28.37f), V(.53f, .63f, .52f));
        Lantern(bedroom, parent, V(53.64f, Floor + .85f, 28.35f), "Bedside lantern", 14, 4.3f);
        PointLight(parent, "Soft bedroom lamplight", V(58.31f, 8.7f, 28.16f), "#DFC7A4", 5.3f, 5.1f);
        bedroom.Box(cloth, V(57.53f, Floor + .009f, 27.8f), V(1.46f, .018f, 2.13f));
        bedroom.Finish();

        var living = new Parts(Group("Table, bench and iron stove", parent), "Cabin furniture");
        Table(living, V(60.35f, Floor, 23.65f), 2, .83f, .78f);
        Bench(living, V(60.35f, Floor, 24.45f), 1.56f, .36f);
        Crate(living, V(53.42f, Floor, 33.36f), V(.76f, .74f, .56f));
        Crate(living, V(53.40f, Floor + .74f, 33.32f), V(.64f, .44f, .48f));
        living.Box(metal, V(62.03f, Floor + .55f, 29.93f), V(.72f, .73f, .63f), true);
        living.Box(stone, V(62.03f, Floor + .033f, 29.93f), V(1.01f, .068f, 1.01f));
        living.Round(metal, V(62.03f, Floor + .88f, 29.93f), V(62.03f, 8.23f, 29.93f), .142f, .142f, 12);
        living.Box(rust, V(62.03f, Floor + .55f, 29.598f), V(.44f, .41f, .050f));
        living.Box(brass, V(62.17f, Floor + .56f, 29.54f), V(.063f, .21f, .055f));
        Lantern(living, parent, V(60.62f, Floor + 1.02f, 23.65f), "Table lantern", 15, 5.4f);
        Lantern(living, parent, V(63.56f, 8.32f, 33.78f), "Back hall lantern", 20, 5.4f);
        living.Finish();
    }

    private void Table(Parts parts, Vector3 p, float width, float depth, float height)
    {
        for (int j = 0; j < 5; j++)
            parts.Box(siding, p + V(0, height - .04f, (j - 2) * depth / 5), V(width, .075f, depth / 5 - .009f));
        BoxCollider(parts.Parent, p + V(0, height - .045f, 0), V(width, .09f, depth));
        for (int i = -1; i <= 1; i += 2)
            for (int j = -1; j <= 1; j += 2)
                parts.Box(darkWood, p + V(i * width * .39f, height * .46f, j * depth * .385f), V(.115f, height * .91f, .12f));
    }

    private void Bench(Parts parts, Vector3 p, float width, float depth)
    {
        parts.Box(siding, p + V(0, .43f, 0), V(width, .105f, depth), true);
        foreach (float x in new[] { -width * .37f, width * .37f })
            parts.Box(darkWood, p + V(x, .20f, 0), V(.13f, .41f, depth * .83f));
    }

    private void Crate(Parts parts, Vector3 p, Vector3 size)
    {
        parts.Box(siding, p + V(0, size.y * .5f, 0), size, true, repeatU: 1.15f, repeatV: .9f);
        for (int i = -1; i <= 1; i += 2)
        {
            foreach (float x in new[] { -.38f, .38f })
                parts.Box(darkWood, p + V(x * size.x, size.y * .51f, i * (size.z * .5f + .016f)),
                    V(.083f, size.y * 1.025f, .045f));
            for (int j = -1; j <= 1; j += 2)
                parts.Box(darkWood, p + V(i * (size.x * .5f + .012f), size.y * (.5f + j * .33f), 0),
                    V(.045f, .074f, size.z));
        }
    }
}
