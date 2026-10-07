// Item boxes, coins, bananas and shells: the kart-party pickups that the bird and the rivals use on each other.
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;

public enum ItemKind { None, Turbo, Banana, Shell, Star }

public static class Items
{
    class Box { public GameObject go; public Vector3 home; public float back; public bool on = true; }
    class Coin { public GameObject go; public Vector3 home; public bool on = true; }
    class Banana { public GameObject go; public Vector3 pos; public float life, arm; public Racer owner; }
    class Shell { public GameObject go; public Vector3 dir; public Racer owner, target; public float life, arm; }

    static readonly List<Box> boxes = new List<Box>();
    static readonly List<Coin> coins = new List<Coin>();
    static readonly List<Banana> bananas = new List<Banana>();
    static readonly List<Shell> shells = new List<Shell>();

    public static int Coins;

    // ------------------------------------------------------------ building
    public static void Build()
    {
        var tex = KartModel.Tex("tex/itembox.png");
        foreach (float s in new[] { 30f, 62f, 96f })
            foreach (float lane in new[] { -2.2f, 0f, 2.2f })
            {
                var p = Track.Point(s, lane) + Vector3.up * 1.15f;
                var g = Mix.Shape(PrimitiveType.Cube, p, Vector3.one * 0.9f, Color.white, false, "ItemBox");
                Mix.Paint(g, Color.white, 0.6f, tex);
                boxes.Add(new Box { go = g, home = p });
            }
        var ctex = KartModel.Tex("tex/coin.png");
        foreach (float s0 in new[] { 46f, 80f, 106f })
            for (int i = 0; i < 7; i++)
            {
                float s = s0 + i * 1.8f;
                float lane = Mathf.Sin(i * 0.9f) * 2f;
                var p = Track.Point(s, lane) + Vector3.up * 0.75f;
                var g = Mix.Shape(PrimitiveType.Cylinder, p, new Vector3(0.62f, 0.035f, 0.62f), Color.white, false, "Coin");
                Mix.Paint(g, new Color(1f, 0.95f, 0.6f), 0.5f, ctex);
                g.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                coins.Add(new Coin { go = g, home = p });
            }
    }

    public static void RefillCoins() { foreach (var c in coins) { c.on = true; c.go.SetActive(true); } }

    // ------------------------------------------------------------ per frame
    public static void Tick(float dt, PlayerRacer pl, List<Rival> rivals)
    {
        float t = Time.time;
        foreach (var b in boxes)
        {
            if (!b.on)
            {
                if (Time.time > b.back) { b.on = true; b.go.SetActive(true); Fx.Stars(b.home, 6); }
                continue;
            }
            b.go.transform.position = b.home + Vector3.up * Mathf.Sin(t * 2.5f + b.home.x) * 0.12f;
            b.go.transform.rotation = Quaternion.Euler(t * 40f, t * 90f, t * 25f);
            var d = pl.Pos - b.home;
            if (Race.State == RaceState.Racing && Race.Held == ItemKind.None && !Race.Rolling && new Vector2(d.x, d.z).magnitude < 1.5f && Mathf.Abs(d.y) < 1.8f)
            {
                Take(b, true);
                Race.StartRoulette();
            }
        }
        foreach (var c in coins)
        {
            if (!c.on) continue;
            c.go.transform.rotation = Quaternion.Euler(90f, t * 200f, 0f) ;
            var d = pl.Pos - c.home;
            if (new Vector2(d.x, d.z).magnitude < 1.3f && Mathf.Abs(d.y) < 1.8f)
            {
                c.on = false; c.go.SetActive(false);
                Coins++;
                Sfx.At("coin", c.home, 0.8f, 0.9f + Mathf.Min(0.5f, (Coins % 10) * 0.05f));
                Fx.B(c.home, new Color(1f, 0.85f, 0.2f), 6, 2.2f, 0.09f, 0.6f);
                if (Coins % 10 == 0) Race.CoinRush();
            }
        }
        for (int i = bananas.Count - 1; i >= 0; i--)
        {
            var b = bananas[i];
            b.life -= dt; b.arm -= dt;
            b.go.transform.Rotate(0f, 60f * dt, 0f, Space.World);
            bool gone = b.life <= 0f;
            if (!gone && b.arm <= 0f)
            {
                foreach (var r in Race.All)
                {
                    if (r == null || r.Shielded && !(r is PlayerRacer)) continue;
                    var d = r.Pos - b.pos; d.y = 0f;
                    if (d.magnitude < 1.05f)
                    {
                        if (r is PlayerRacer p && p.StarT > 0f) { Fx.Stars(b.pos, 8); }
                        else r.Hit(1, "banana", b.pos);
                        gone = true; break;
                    }
                }
            }
            if (gone) { Fx.B(b.pos + Vector3.up * 0.2f, new Color(1f, 0.9f, 0.2f), 6, 2f, 0.1f, 0.6f); Object.Destroy(b.go); bananas.RemoveAt(i); }
        }
        for (int i = shells.Count - 1; i >= 0; i--)
        {
            var s = shells[i];
            s.life -= dt; s.arm -= dt;
            if (s.target == null || (s.target is Rival rv && !rv.Alive)) s.target = Closest(s);
            if (s.target != null)
            {
                var want = (s.target.Pos + Vector3.up * 0.5f - s.go.transform.position); want.y = 0f;
                if (want.sqrMagnitude > 0.01f) s.dir = Vector3.RotateTowards(s.dir, want.normalized, 2.2f * dt, 0f);
            }
            var np = s.go.transform.position + s.dir * 12f * dt;
            // bounce off the arena walls
            if (np.x < -61f || np.x > -15f) { s.dir.x = -s.dir.x; np = s.go.transform.position + s.dir * 12f * dt; }
            if (np.z < 46f || np.z > 83f) { s.dir.z = -s.dir.z; np = s.go.transform.position + s.dir * 12f * dt; }
            s.go.transform.position = np;
            s.go.transform.Rotate(0f, 520f * dt, 0f, Space.World);
            if (Time.frameCount % 2 == 0) Fx.B(np, new Color(0.5f, 1f, 0.5f), 1, 0.5f, 0.08f, 0.35f);
            bool boom = s.life <= 0f;
            if (!boom && s.arm <= 0f)
                foreach (var r in Race.All)
                {
                    if (r == null || r == s.owner) continue;
                    if (r is Rival rr && !rr.Alive) continue;
                    var d = r.Pos + Vector3.up * 0.5f - np;
                    if (d.magnitude < 1.15f)
                    {
                        if (r is PlayerRacer p && p.StarT > 0f) { Fx.Boom(np, 0.6f); Sfx.At("boom", np, 0.6f); }
                        else { Fx.Boom(np); Sfx.At("boom", np, 1f); r.Hit(2, "shell", np); if (s.owner is PlayerRacer) Race.OnPlayerHit(r); }
                        boom = true; break;
                    }
                }
            if (boom) { Object.Destroy(s.go); shells.RemoveAt(i); }
        }
    }

    static Racer Closest(Shell s)
    {
        Racer best = null; float bd = 1e9f;
        foreach (var r in Race.All)
        {
            if (r == null || r == s.owner) continue;
            if (r is Rival rr && !rr.Alive) continue;
            var d = r.Pos - s.go.transform.position; d.y = 0f;
            if (Vector3.Dot(d.normalized, s.dir) < 0.2f) continue;
            if (d.magnitude < bd && d.magnitude < 30f) { bd = d.magnitude; best = r; }
        }
        return best;
    }

    static void Take(Box b, bool byPlayer)
    {
        b.on = false; b.back = Time.time + 6f; b.go.SetActive(false);
        Fx.Stars(b.home, 14);
        Fx.B(b.home, new Color(0.3f, 0.8f, 1f), 10, 3f, 0.12f, 0.8f);
        Fx.B(b.home, new Color(1f, 0.4f, 0.8f), 10, 3f, 0.12f, 0.8f);
        if (byPlayer) Sfx.At("itembox", b.home, 1f);
    }

    // ------------------------------------------------------------ using items
    public static void Use(ItemKind k, PlayerRacer pl)
    {
        Mix.Log("item used: " + k + " rank " + Race.Rank);
        var fwd = G.Forward;
        switch (k)
        {
            case ItemKind.Turbo:
                pl.BoostT = 2.6f; Sfx.At("boost", null, 0.9f);
                Race.Pop(pl.Pos + Vector3.up * 2.2f, "TURBO!", new Color(1f, 0.6f, 0.1f));
                break;
            case ItemKind.Banana:
                DropBanana(pl.Pos - fwd * 1.7f, pl);
                Race.Pop(pl.Pos + Vector3.up * 2.2f, "BANANA DROP!", new Color(1f, 0.9f, 0.2f));
                break;
            case ItemKind.Shell:
                FireShell(pl.Pos + fwd * 1.8f, fwd, pl, null);
                Race.Pop(pl.Pos + Vector3.up * 2.2f, "SHELL!", new Color(0.4f, 1f, 0.4f));
                break;
            case ItemKind.Star:
                pl.StarT = 7f; Sfx.At("fanfare", null, 0.8f);
                Race.Pop(pl.Pos + Vector3.up * 2.2f, "STAR POWER!", new Color(1f, 0.9f, 0.3f), 1.6f);
                break;
        }
    }

    public static void DropBanana(Vector3 at, Racer owner)
    {
        at.y = Track.FloorY + 0.05f;
        var root = new GameObject("Banana");
        root.transform.position = at;
        var arc = new GameObject("BananaArc").transform;
        arc.SetParent(root.transform, false);
        for (int i = 0; i < 7; i++)
        {
            float a = (i - 3) * 0.27f;
            bool tip = i == 0 || i == 6;
            var g = Mix.Shape(PrimitiveType.Capsule, Vector3.zero, new Vector3(tip ? 0.11f : 0.19f, 0.2f, tip ? 0.11f : 0.19f), tip ? new Color(0.35f, 0.25f, 0.1f) : new Color(1f, 0.9f, 0.12f), false, "BananaPart");
            g.transform.SetParent(arc, false);
            g.transform.localPosition = new Vector3(Mathf.Sin(a) * 0.6f, Mathf.Cos(a) * 0.6f - 0.45f, 0f);
            g.transform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg);
        }
        arc.localRotation = Quaternion.Euler(90f, 0f, 0f);
        arc.localPosition = new Vector3(0f, 0.12f, 0.3f);
        root.transform.localScale = Vector3.one * 1.3f;
        bananas.Add(new Banana { go = root, pos = at, life = 30f, arm = 0.7f, owner = owner });
        if (owner is PlayerRacer) Sfx.At("horn", at, 0.4f, 1.6f);
    }

    public static float? BananaAhead(Racer r)
    {
        foreach (var b in bananas)
        {
            if (b.owner == r) continue;
            var d = b.pos - r.Pos;
            if (Vector3.Dot(d, r.Fwd) > 0f && d.magnitude < 7f) { Track.Nearest(b.pos, out float lane); return lane; }
        }
        return null;
    }

    public static void FireShell(Vector3 at, Vector3 dir, Racer owner, Racer target)
    {
        var tex = KartModel.Tex("tex/shell.png");
        var g = Mix.Shape(PrimitiveType.Sphere, at + Vector3.up * 0.3f, new Vector3(0.7f, 0.5f, 0.7f), Color.white, false, "Shell");
        Mix.Paint(g, Color.white, 0.3f, tex);
        var rim = Mix.Shape(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.85f, 0.04f, 0.85f), Color.white, false, "ShellRim");
        rim.transform.SetParent(g.transform, false);
        Mix.Glow(g.transform.position, new Color(0.4f, 1f, 0.4f), 3f, 1.5f, g.transform);
        shells.Add(new Shell { go = g, dir = dir.normalized, owner = owner, target = target, life = 6f, arm = 0.35f });
        Sfx.At("shell", at, 0.9f);
    }

    public static void Clear()
    {
        foreach (var b in bananas) if (b.go) Object.Destroy(b.go);
        foreach (var s in shells) if (s.go) Object.Destroy(s.go);
        bananas.Clear(); shells.Clear();
    }

    /// <summary>Lane the demo bot should drive at track position s: toward the nearest live box or coin just ahead.</summary>
    public static float SuggestLane(float s)
    {
        float best = 0f, bd = 1e9f;
        float L = Track.Len;
        foreach (var b in boxes)
        {
            if (!b.on) continue;
            float bs = Track.Nearest(b.home, out float lane);
            float ds = ((bs - s) % L + L) % L;
            if (ds > 3f && ds < 14f && ds < bd) { bd = ds; best = lane; }
        }
        foreach (var c in coins)
        {
            if (!c.on) continue;
            float cs = Track.Nearest(c.home, out float lane);
            float ds = ((cs - s) % L + L) % L;
            if (ds > 2f && ds < 9f && ds < bd) { bd = ds; best = lane; }
        }
        return best;
    }

    public static int BananaCount => bananas.Count;
    public static int ShellCount => shells.Count;
}
