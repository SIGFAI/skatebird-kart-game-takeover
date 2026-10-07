// Procedural go-kart model built from the game's primitives (local space: forward +Z, up +Y, about 1.7 long).
using System.IO;
using Sigf.Kit;
using UnityEngine;

public class KartModel
{
    public GameObject Root;
    public Transform[] Wheels = new Transform[4];   // spin pivots (rotate around local X)
    public Transform FrontPivot0, FrontPivot1;       // front wheel steer pivots
    public Transform Exhaust0, Exhaust1;
    public Transform Driver;
    public Renderer[] Paintable;
    public Color Body;

    public static Texture2D Tex(string rel)
    {
        try { return File.Exists(Mix.AssetPath(rel)) ? Mix.Texture(rel) : null; }
        catch { return null; }
    }

    static GameObject Part(Transform parent, PrimitiveType t, Vector3 lp, Vector3 scale, Color c, Texture2D tex = null, float glow = 0f)
    {
        var go = Mix.Shape(t, Vector3.zero, scale, c, false, "KartPart");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = lp;
        if (tex != null || glow > 0f) Mix.Paint(go, c, glow, tex);
        return go;
    }

    /// <summary>A kart. livery: "red","blue","green","yellow". driver: 0 none (the bird rides), 1 frog, 2 bear, 3 robot.</summary>
    public static KartModel Build(string livery, Color body, int driver, float scale = 1f)
    {
        var k = new KartModel { Body = body };
        var root = new GameObject("SigfKart");
        k.Root = root;
        var t = root.transform;
        var paint = Tex("tex/kart_" + livery + ".png");
        var tire = Tex("tex/tire.png");
        var dark = new Color(0.12f, 0.12f, 0.14f);
        var chrome = new Color(0.8f, 0.82f, 0.88f);

        // chassis: floor plate, nose cone, side pods, rear block
        Part(t, PrimitiveType.Cube, new Vector3(0, 0.12f, 0), new Vector3(0.95f, 0.14f, 1.45f), body, paint);
        Part(t, PrimitiveType.Cube, new Vector3(0, 0.16f, 0.95f), new Vector3(0.62f, 0.12f, 0.55f), body, paint);
        Part(t, PrimitiveType.Sphere, new Vector3(0, 0.2f, 1.25f), new Vector3(0.5f, 0.2f, 0.4f), body, paint);
        Part(t, PrimitiveType.Cube, new Vector3(-0.55f, 0.2f, 0.05f), new Vector3(0.22f, 0.2f, 0.85f), body, paint);
        Part(t, PrimitiveType.Cube, new Vector3(0.55f, 0.2f, 0.05f), new Vector3(0.22f, 0.2f, 0.85f), body, paint);
        Part(t, PrimitiveType.Cube, new Vector3(0, 0.3f, -0.62f), new Vector3(0.8f, 0.3f, 0.34f), body, paint);
        // seat back + steering wheel
        Part(t, PrimitiveType.Cube, new Vector3(0, 0.42f, -0.4f), new Vector3(0.5f, 0.4f, 0.1f), dark);
        var sw = Part(t, PrimitiveType.Cylinder, new Vector3(0, 0.46f, 0.38f), new Vector3(0.26f, 0.02f, 0.26f), dark);
        sw.transform.localRotation = Quaternion.Euler(-65f, 0f, 0f);
        // front bumper (chrome bar)
        var bar = Part(t, PrimitiveType.Cylinder, new Vector3(0, 0.14f, 1.52f), new Vector3(0.1f, 0.4f, 0.1f), chrome);
        bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        // rear wing
        Part(t, PrimitiveType.Cube, new Vector3(-0.32f, 0.62f, -0.78f), new Vector3(0.05f, 0.45f, 0.05f), dark);
        Part(t, PrimitiveType.Cube, new Vector3(0.32f, 0.62f, -0.78f), new Vector3(0.05f, 0.45f, 0.05f), dark);
        Part(t, PrimitiveType.Cube, new Vector3(0, 0.88f, -0.8f), new Vector3(1.1f, 0.05f, 0.3f), body, paint);
        // exhausts
        k.Exhaust0 = Part(t, PrimitiveType.Cylinder, new Vector3(-0.22f, 0.3f, -0.86f), new Vector3(0.1f, 0.1f, 0.1f), chrome).transform;
        k.Exhaust1 = Part(t, PrimitiveType.Cylinder, new Vector3(0.22f, 0.3f, -0.86f), new Vector3(0.1f, 0.1f, 0.1f), chrome).transform;
        k.Exhaust0.localRotation = k.Exhaust1.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // wheels: pivot (steer) -> spin -> tire cylinder
        float[] xs = { -0.62f, 0.62f, -0.64f, 0.64f };
        float[] zs = { 0.85f, 0.85f, -0.6f, -0.6f };
        float[] rs = { 0.2f, 0.2f, 0.26f, 0.26f };
        for (int i = 0; i < 4; i++)
        {
            var pivot = new GameObject("WheelPivot").transform;
            pivot.SetParent(t, false);
            pivot.localPosition = new Vector3(xs[i], rs[i], zs[i]);
            var spin = new GameObject("WheelSpin").transform;
            spin.SetParent(pivot, false);
            var w = Part(spin, PrimitiveType.Cylinder, Vector3.zero, new Vector3(rs[i] * 2f, 0.12f, rs[i] * 2f), Color.white, tire);
            if (tire == null) Mix.Paint(w, dark);
            w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var hub = Part(spin, PrimitiveType.Cylinder, new Vector3(Mathf.Sign(xs[i]) * 0.07f, 0f, 0f), new Vector3(rs[i] * 1.0f, 0.065f, rs[i] * 1.0f), chrome);
            hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            k.Wheels[i] = spin;
            if (i == 0) k.FrontPivot0 = pivot;
            if (i == 1) k.FrontPivot1 = pivot;
        }

        if (driver > 0) BuildDriver(k, driver);
        t.localScale = Vector3.one * scale;
        k.Rends = root.GetComponentsInChildren<Renderer>();
        k.BaseColors = new Color[k.Rends.Length];
        for (int i = 0; i < k.Rends.Length; i++) k.BaseColors[i] = k.Rends[i].material.color;
        return k;
    }

    static void BuildDriver(KartModel k, int kind)
    {
        var t = k.Root.transform;
        var d = new GameObject("Driver").transform;
        d.SetParent(t, false);
        d.localPosition = new Vector3(0, 0.38f, -0.05f);
        k.Driver = d;
        Color skin, shirt;
        switch (kind)
        {
            case 1: skin = new Color(0.35f, 0.8f, 0.25f); shirt = new Color(0.95f, 0.85f, 0.2f); break;   // frog
            case 2: skin = new Color(0.55f, 0.35f, 0.2f); shirt = new Color(0.9f, 0.3f, 0.3f); break;     // bear
            default: skin = new Color(0.7f, 0.75f, 0.85f); shirt = new Color(0.3f, 0.5f, 0.9f); break;    // robot
        }
        Part(d, PrimitiveType.Sphere, new Vector3(0, 0.2f, 0), new Vector3(0.5f, 0.55f, 0.45f), shirt);       // torso
        var head = Part(d, PrimitiveType.Sphere, new Vector3(0, 0.66f, 0.03f), new Vector3(0.5f, 0.46f, 0.46f), skin);
        // helmet
        var helm = Part(d, PrimitiveType.Sphere, new Vector3(0, 0.74f, 0f), new Vector3(0.58f, 0.46f, 0.58f), k.Body);
        helm.transform.localRotation = Quaternion.identity;
        // eyes
        Part(d, PrimitiveType.Sphere, new Vector3(-0.1f, 0.64f, 0.22f), new Vector3(0.13f, 0.16f, 0.1f), Color.white);
        Part(d, PrimitiveType.Sphere, new Vector3(0.1f, 0.64f, 0.22f), new Vector3(0.13f, 0.16f, 0.1f), Color.white);
        Part(d, PrimitiveType.Sphere, new Vector3(-0.1f, 0.64f, 0.27f), new Vector3(0.06f, 0.08f, 0.05f), Color.black);
        Part(d, PrimitiveType.Sphere, new Vector3(0.1f, 0.64f, 0.27f), new Vector3(0.06f, 0.08f, 0.05f), Color.black);
        if (kind == 1) { Part(d, PrimitiveType.Sphere, new Vector3(-0.15f, 0.82f, 0.05f), new Vector3(0.16f, 0.16f, 0.16f), skin); Part(d, PrimitiveType.Sphere, new Vector3(0.15f, 0.82f, 0.05f), new Vector3(0.16f, 0.16f, 0.16f), skin); }
        if (kind == 2) { Part(d, PrimitiveType.Sphere, new Vector3(-0.24f, 0.88f, 0f), new Vector3(0.18f, 0.18f, 0.12f), skin); Part(d, PrimitiveType.Sphere, new Vector3(0.24f, 0.88f, 0f), new Vector3(0.18f, 0.18f, 0.12f), skin); Part(d, PrimitiveType.Sphere, new Vector3(0, 0.56f, 0.28f), new Vector3(0.2f, 0.14f, 0.14f), new Color(0.9f, 0.75f, 0.55f)); }
        if (kind == 3) { var a = Part(d, PrimitiveType.Cylinder, new Vector3(0, 1.0f, 0f), new Vector3(0.04f, 0.1f, 0.04f), Color.gray); Part(d, PrimitiveType.Sphere, new Vector3(0, 1.12f, 0f), new Vector3(0.1f, 0.1f, 0.1f), Color.red, null, 1.5f); }
        // arms on the wheel
        Part(d, PrimitiveType.Sphere, new Vector3(-0.28f, 0.2f, 0.3f), new Vector3(0.14f, 0.14f, 0.3f), skin);
        Part(d, PrimitiveType.Sphere, new Vector3(0.28f, 0.2f, 0.3f), new Vector3(0.14f, 0.14f, 0.3f), skin);
    }

    public Renderer[] Rends;
    public Color[] BaseColors;

    /// <summary>Multiplies every part by a colour (star rainbow, hit flash); null restores the paint.</summary>
    public void Tint(Color? c)
    {
        for (int i = 0; i < Rends.Length; i++)
            if (Rends[i] != null) Rends[i].material.color = c.HasValue ? Color.Lerp(BaseColors[i], c.Value, 0.7f) : BaseColors[i];
    }

    public void Show(bool on) { foreach (var r in Rends) if (r != null) r.enabled = on; }

    public void Spin(float distance, float steer)
    {
        for (int i = 0; i < 4; i++) if (Wheels[i] != null) Wheels[i].Rotate(distance * 120f, 0f, 0f, Space.Self);
        var q = Quaternion.Euler(0f, steer * 28f, 0f);
        if (FrontPivot0 != null) FrontPivot0.localRotation = q;
        if (FrontPivot1 != null) FrontPivot1.localRotation = q;
    }
}
