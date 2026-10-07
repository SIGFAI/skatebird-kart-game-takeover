// The racing circuit: a stadium-shaped track laid on the playground floor, with a start gantry, a cloud marshal,
// curbs, cones, boost pads, item boxes and coins.
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;

public static class Track
{
    public const float FloorY = 1.0f;
    public static readonly Vector3 C1 = new Vector3(-54f, 0f, 67f), C2 = new Vector3(-22f, 0f, 67f);
    public const float R = 8f, HalfW = 3.4f, StartS = 14f;
    public static float Straight => C2.x - C1.x;
    public static float Len => 2f * Straight + 2f * Mathf.PI * R;

    public static void At(float s, out Vector3 pos, out Vector3 tan)
    {
        float L = Len, St = Straight, arc = Mathf.PI * R;
        s = ((s % L) + L) % L;
        if (s < St) { pos = new Vector3(C1.x + s, FloorY, C1.z - R); tan = Vector3.right; }
        else if (s < St + arc)
        {
            float a = -Mathf.PI / 2f + (s - St) / R;
            pos = new Vector3(C2.x + R * Mathf.Cos(a), FloorY, C2.z + R * Mathf.Sin(a));
            tan = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
        }
        else if (s < 2f * St + arc) { pos = new Vector3(C2.x - (s - St - arc), FloorY, C1.z + R); tan = Vector3.left; }
        else
        {
            float a = Mathf.PI / 2f + (s - 2f * St - arc) / R;
            pos = new Vector3(C1.x + R * Mathf.Cos(a), FloorY, C1.z + R * Mathf.Sin(a));
            tan = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
        }
    }

    public static Vector3 Right(Vector3 tan) => new Vector3(tan.z, 0f, -tan.x);

    /// <summary>World point on the track at s, lane offset (positive = outside/right).</summary>
    public static Vector3 Point(float s, float lane)
    {
        At(s, out var p, out var t);
        return p + Right(t) * lane;
    }

    static Vector3[] samples;
    const float Step = 0.5f;

    /// <summary>Track parameter nearest to a world position, and the lateral offset (positive = right).</summary>
    public static float Nearest(Vector3 w, out float lane)
    {
        if (samples == null)
        {
            int n = Mathf.CeilToInt(Len / Step);
            samples = new Vector3[n];
            for (int i = 0; i < n; i++) { At(i * Step, out var p, out _); samples[i] = p; }
        }
        float best = 1e9f; int bi = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float dx = samples[i].x - w.x, dz = samples[i].z - w.z;
            float d = dx * dx + dz * dz;
            if (d < best) { best = d; bi = i; }
        }
        At(bi * Step, out var q, out var tg);
        lane = Vector3.Dot(w - q, Right(tg));
        return bi * Step;
    }

    // ------------------------------------------------------------------ building
    public static readonly List<GameObject> Built = new List<GameObject>();
    static GameObject Add(GameObject g) { Built.Add(g); return g; }

    public static void Build()
    {
        var asphalt = new Color(0.16f, 0.16f, 0.19f);
        float L = Len;
        int n = Mathf.RoundToInt(L / 1.6f);
        float seg = L / n;
        var red = new Color(0.9f, 0.1f, 0.12f);
        for (int i = 0; i < n; i++)
        {
            float s = (i + 0.5f) * seg;
            At(s, out var p, out var t);
            var rot = Quaternion.LookRotation(t, Vector3.up);
            var r = Mix.Shape(PrimitiveType.Cube, p + Vector3.up * 0.025f, new Vector3(HalfW * 2f + 0.05f, 0.05f, seg * 1.04f), asphalt, false, "Road");
            r.transform.rotation = rot; Add(r);
            // curbs: red / white alternating
            Color cc = (i % 2 == 0) ? red : Color.white;
            foreach (float side in new[] { -1f, 1f })
            {
                var c = Mix.Shape(PrimitiveType.Cube, p + Right(t) * side * (HalfW + 0.2f) + Vector3.up * 0.06f, new Vector3(0.4f, 0.12f, seg * 1.02f), cc, false, "Curb");
                c.transform.rotation = rot; Add(c);
            }
            // dashed centre line
            if (i % 2 == 0)
            {
                var d = Mix.Shape(PrimitiveType.Cube, p + Vector3.up * 0.056f, new Vector3(0.18f, 0.02f, seg * 0.7f), new Color(1f, 0.9f, 0.3f), false, "Dash");
                d.transform.rotation = rot; Add(d);
            }
        }
        BuildStartLine();
        BuildDecor();
    }

    static void BuildStartLine()
    {
        At(StartS, out var p, out var t);
        var rot = Quaternion.LookRotation(t, Vector3.up);
        var checker = KartModel.Tex("tex/checker.png");
        // checkered strip on the road
        var strip = Mix.Shape(PrimitiveType.Cube, p + Vector3.up * 0.06f, new Vector3(HalfW * 2f, 0.02f, 1.2f), Color.white, false, "StartLine");
        strip.transform.rotation = rot;
        if (checker != null) { Mix.Paint(strip, Color.white, 0f, checker); strip.GetComponent<Renderer>().material.mainTextureScale = new Vector2(6f, 1f); }
        Add(strip);
        // gantry: two posts and a beam with a checkered banner
        var right = Right(t);
        float h = 4.2f;
        foreach (float side in new[] { -1f, 1f })
        {
            var post = Mix.Shape(PrimitiveType.Cylinder, p + right * side * (HalfW + 0.8f) + Vector3.up * h / 2f, new Vector3(0.45f, h / 2f, 0.45f), new Color(0.9f, 0.9f, 0.95f), true, "GantryPost");
            Add(post);
            var cap = Mix.Shape(PrimitiveType.Sphere, p + right * side * (HalfW + 0.8f) + Vector3.up * h, Vector3.one * 0.7f, new Color(1f, 0.8f, 0.1f), false, "GantryCap");
            Add(cap);
        }
        var beam = Mix.Shape(PrimitiveType.Cube, p + Vector3.up * (h + 0.3f), new Vector3((HalfW + 0.8f) * 2f, 0.9f, 0.3f), Color.white, false, "GantryBanner");
        beam.transform.rotation = rot;
        if (checker != null) { Mix.Paint(beam, Color.white, 0f, checker); beam.GetComponent<Renderer>().material.mainTextureScale = new Vector2(14f, 2f); }
        Add(beam);
        // three signal lamps on a bar under the beam
        var bar = Mix.Shape(PrimitiveType.Cube, p + Vector3.up * (h - 0.9f), new Vector3(2.8f, 0.9f, 0.3f), new Color(0.08f, 0.08f, 0.1f), false, "LampBar");
        bar.transform.rotation = rot; Add(bar);
        for (int i = 0; i < 3; i++)
        {
            var lamp = Mix.Shape(PrimitiveType.Sphere, p + right * (i - 1) * 0.85f + Vector3.up * (h - 0.9f) - t * 0.2f, Vector3.one * 0.62f, new Color(0.25f, 0.25f, 0.25f), false, "Lamp" + i);
            Lamps[i] = lamp; Add(lamp);
        }
        BuildCloud(p + Vector3.up * (h + 1.8f) + right * 0f);
    }

    public static GameObject[] Lamps = new GameObject[3];
    public static Transform Cloud;

    static void BuildCloud(Vector3 at)
    {
        var root = new GameObject("CloudMarshal");
        root.transform.position = at;
        Cloud = root.transform;
        Built.Add(root);
        var white = new Color(0.97f, 0.97f, 1f);
        void Puff(float x, float y, float z, float r) { var g = Mix.Shape(PrimitiveType.Sphere, Vector3.zero, Vector3.one * r, white, false, "Puff"); g.transform.SetParent(root.transform, false); g.transform.localPosition = new Vector3(x, y, z); }
        Puff(0, 0, 0, 1.9f); Puff(-1.1f, -0.2f, 0, 1.3f); Puff(1.1f, -0.2f, 0, 1.3f); Puff(-0.5f, 0.6f, 0, 1.2f); Puff(0.55f, 0.55f, 0, 1.3f);
        // face toward the player side (-Z)
        void Part(PrimitiveType t, float x, float y, float z, Vector3 sc, Color c) { var g = Mix.Shape(t, Vector3.zero, sc, c, false, "Face"); g.transform.SetParent(root.transform, false); g.transform.localPosition = new Vector3(x, y, z); }
        Part(PrimitiveType.Sphere, -0.4f, 0.15f, -0.85f, new Vector3(0.38f, 0.5f, 0.2f), Color.white);
        Part(PrimitiveType.Sphere, 0.4f, 0.15f, -0.85f, new Vector3(0.38f, 0.5f, 0.2f), Color.white);
        Part(PrimitiveType.Sphere, -0.4f, 0.12f, -0.95f, new Vector3(0.17f, 0.25f, 0.1f), Color.black);
        Part(PrimitiveType.Sphere, 0.4f, 0.12f, -0.95f, new Vector3(0.17f, 0.25f, 0.1f), Color.black);
        Part(PrimitiveType.Sphere, -0.8f, -0.25f, -0.8f, new Vector3(0.3f, 0.2f, 0.1f), new Color(1f, 0.6f, 0.65f));
        Part(PrimitiveType.Sphere, 0.8f, -0.25f, -0.8f, new Vector3(0.3f, 0.2f, 0.1f), new Color(1f, 0.6f, 0.65f));
        Part(PrimitiveType.Cube, 0f, -0.35f, -0.9f, new Vector3(0.5f, 0.12f, 0.1f), new Color(0.4f, 0.1f, 0.1f));
        // a checkered flag on a pole
        Part(PrimitiveType.Cylinder, 1.9f, 0.6f, -0.2f, new Vector3(0.08f, 0.9f, 0.08f), new Color(0.5f, 0.35f, 0.2f));
        var flag = Mix.Shape(PrimitiveType.Cube, Vector3.zero, new Vector3(0.9f, 0.6f, 0.05f), Color.white, false, "Flag");
        flag.transform.SetParent(root.transform, false); flag.transform.localPosition = new Vector3(2.35f, 1.25f, -0.2f);
        var ck = KartModel.Tex("tex/checker.png"); if (ck != null) Mix.Paint(flag, Color.white, 0f, ck);
        FlagT = flag.transform;
    }
    public static Transform FlagT;

    static void BuildDecor()
    {
        var tire = KartModel.Tex("tex/tire.png");
        var orange = new Color(1f, 0.45f, 0.05f);
        // tyre stacks and cones on the outside of the curves
        for (int i = 0; i < 14; i++)
        {
            float s = (Straight + 2f) + i * (Mathf.PI * R / 13f);
            At(s, out var p, out var t);
            Cone(p + Right(t) * (HalfW + 1.4f));
        }
        for (int i = 0; i < 14; i++)
        {
            float s = (2f * Straight + Mathf.PI * R) + 2f + i * (Mathf.PI * R / 13f);
            At(s, out var p, out var t);
            Cone(p + Right(t) * (HalfW + 1.4f));
        }
        // tyre walls on the inside of the straights
        for (int i = 0; i < 6; i++)
        {
            float s = 4f + i * 5.5f;
            At(s, out var p, out var t);
            TyreStack(p - Right(t) * (HalfW + 1.2f), tire);
            At(s + Straight + Mathf.PI * R, out p, out t);
            TyreStack(p - Right(t) * (HalfW + 1.2f), tire);
        }
        // banner poles with rainbow flags along the outside
        for (int i = 0; i < 8; i++)
        {
            At(i * 4.5f + 1f, out var p, out var t);
            Banner(p + Right(t) * (HalfW + 2.4f), t);
        }
    }

    static void Cone(Vector3 at)
    {
        var o = new Color(1f, 0.45f, 0.05f);
        Add(Mix.Shape(PrimitiveType.Cube, at + Vector3.up * 0.05f, new Vector3(0.7f, 0.1f, 0.7f), o, true, "ConeBase"));
        Add(Mix.Shape(PrimitiveType.Cylinder, at + Vector3.up * 0.3f, new Vector3(0.55f, 0.2f, 0.55f), o, true, "Cone"));
        Add(Mix.Shape(PrimitiveType.Cylinder, at + Vector3.up * 0.55f, new Vector3(0.38f, 0.12f, 0.38f), Color.white, true, "ConeBand"));
        Add(Mix.Shape(PrimitiveType.Cylinder, at + Vector3.up * 0.78f, new Vector3(0.2f, 0.12f, 0.2f), o, true, "ConeTip"));
    }

    static void TyreStack(Vector3 at, Texture2D tire)
    {
        for (int i = 0; i < 3; i++)
        {
            var g = Mix.Shape(PrimitiveType.Cylinder, at + Vector3.up * (0.2f + i * 0.4f), new Vector3(1.1f, 0.2f, 1.1f), i % 2 == 0 ? Color.white : new Color(0.9f, 0.15f, 0.15f), true, "Tyre");
            if (tire != null) Mix.Paint(g, i % 2 == 0 ? Color.white : new Color(1f, 0.6f, 0.6f), 0f, tire);
            g.transform.rotation = Quaternion.Euler(0f, i * 30f, 0f);
            Add(g);
        }
    }

    static void Banner(Vector3 at, Vector3 t)
    {
        var rain = KartModel.Tex("tex/rainbow.png");
        Add(Mix.Shape(PrimitiveType.Cylinder, at + Vector3.up * 1.6f, new Vector3(0.12f, 1.6f, 0.12f), new Color(0.85f, 0.85f, 0.9f), true, "BannerPole"));
        var f = Mix.Shape(PrimitiveType.Cube, at + Vector3.up * 2.7f + t * 0.5f, new Vector3(1.0f, 0.7f, 0.04f), Color.white, false, "BannerFlag");
        f.transform.rotation = Quaternion.LookRotation(Right(t), Vector3.up) * Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(0f, 0f, 0f);
        f.transform.rotation = Quaternion.LookRotation(t, Vector3.up) * Quaternion.Euler(0f, 90f, 0f);
        if (rain != null) Mix.Paint(f, Color.white, 0f, rain);
        Add(f);
    }

    // ------------------------------------------------------------------ boost pads
    public static readonly List<Vector3> Pads = new List<Vector3>();
    public static void BuildPads()
    {
        var tex = KartModel.Tex("tex/boostpad.png");
        foreach (float s in new[] { 38f, 66f, 94f })
        {
            At(s, out var p, out var t);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(q.GetComponent<Collider>());
            q.name = "BoostPad";
            q.transform.position = p + Vector3.up * 0.075f;
            q.transform.rotation = Quaternion.LookRotation(Vector3.down, t);   // face up, arrows toward travel
            q.transform.localScale = new Vector3(2.4f, 3f, 1f);
            Mix.Paint(q, Color.white, 1.2f, tex);
            Add(q);
            Pads.Add(p);
        }
    }
}
