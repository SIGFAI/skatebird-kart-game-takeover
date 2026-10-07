// Everything that races: the player's kart (the skateboard in a kart body) and three rival drivers.
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;

public static class Sfx
{
    public static void At(string name, Vector3? pos = null, float vol = 1f, float pitch = 1f)
    {
        try { Mix.Play(Mix.Sound("snd/" + name + ".wav"), pos, vol, pitch); }
        catch (System.Exception e) { Mix.Warn("sound " + name + ": " + e.Message); }
    }
}

public abstract class Racer
{
    public string Name;
    public KartModel Kart;
    public bool IsPlayer;
    public float BaseProg;                 // progress at the start of the current race
    public float Total;                    // absolute progress along the track (laps * length + s - start)
    public abstract Vector3 Pos { get; }
    public abstract Vector3 Fwd { get; }
    public abstract float Speed { get; }
    public abstract bool Shielded { get; }
    public abstract void Hit(int damage, string cause, Vector3 from);
    public float Prog => Total - BaseProg;
}

public class PlayerRacer : Racer
{
    public float SpinT, BoostT, StarT;
    public PlayerRacer() { Name = "YOU"; IsPlayer = true; }
    public override Vector3 Pos => G.Pos;
    public override Vector3 Fwd => G.Forward;
    public override float Speed => G.Speed;
    public override bool Shielded => StarT > 0f || SpinT > 0.2f;

    public override void Hit(int damage, string cause, Vector3 from)
    {
        if (Shielded) return;
        SpinT = 1.1f;
        if (G.Body != null) G.SetVelocity(G.Velocity * 0.35f);
        Fx.B(Pos + Vector3.up * 0.8f, new Color(1f, 0.9f, 0.2f), 14, 3f, 0.12f, 1.1f);
        Sfx.At("spin", Pos, 1f);
        Race.Pop(Pos + Vector3.up * 2.2f, cause == "banana" ? "SLIPPED!" : "OUCH!", new Color(1f, 0.5f, 0.3f));
    }
}

public class Rival : Racer
{
    public int Hp = 5, MaxHp = 5;
    public float S;
    float lane, laneTarget, laneTimer, baseSpeed;
    public float SpinT, DeadT, InvulnT, BoostT, ItemT, ShellT;
    float yawExtra, lastLane, cur;
    Vector3 pos, fwd = Vector3.right;
    Color color;
    Rigidbody rb;
    public bool Alive => DeadT <= 0f;
    public Color Color => color;
    public int Driver;

    public Rival(string name, string livery, Color col, int driver, float s0, float lane0, float speed)
    {
        Name = name; color = col; Driver = driver;
        S = s0; lane = laneTarget = lane0; baseSpeed = speed;
        Kart = KartModel.Build(livery, col, driver, 1f);
        var root = Kart.Root;
        var bc = root.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, 0.45f, 0.1f); bc.size = new Vector3(1.3f, 0.9f, 2.0f);
        rb = root.AddComponent<Rigidbody>(); rb.isKinematic = true;
        itemCool = Random.Range(4f, 8f);
        ShellT = Random.Range(8f, 14f);
        Place();
        Total = S; BaseProg = S;
    }
    float itemCool;
    public override Vector3 Pos => pos;
    public override Vector3 Fwd => fwd;
    public override float Speed => cur;
    public override bool Shielded => InvulnT > 0f || SpinT > 0.3f || DeadT > 0f;

    void Place()
    {
        pos = Track.Point(S, lane);
        Track.At(S, out _, out var t);
        float steer = Mathf.Clamp((lane - lastLane) / Mathf.Max(0.001f, Time.deltaTime) * 0.25f, -1f, 1f);
        var f = (t + Track.Right(t) * steer * 0.35f).normalized;
        fwd = Vector3.Slerp(fwd, f, 0.25f);
        var tr = Kart.Root.transform;
        tr.position = pos + Vector3.up * (SpinT > 0f ? Mathf.Sin(SpinT / 1.1f * Mathf.PI) * 0.4f : 0f);
        tr.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(0f, yawExtra, 0f);
        lastLane = lane;
    }

    public override void Hit(int damage, string cause, Vector3 from)
    {
        if (Shielded) return;
        Hp -= damage;
        Mix.Log("hit " + Name + " by " + cause + " hp " + Hp);
        InvulnT = 1.3f;
        Fx.B(pos + Vector3.up * 0.9f, new Color(1f, 0.9f, 0.2f), 16, 3.5f, 0.12f, 1.0f);
        Fx.B(pos + Vector3.up * 0.9f, Color.white, 8, 2f, 0.1f, 0.8f);
        var l = Mix.Glow(pos + Vector3.up, new Color(1f, 0.8f, 0.3f), 4f, 3f); Object.Destroy(l.gameObject, 0.25f);
        if (Hp <= 0) { Explode(); return; }
        SpinT = 1.1f; yawExtra = 0f; cur *= 0.2f;
        Sfx.At("spin", pos, 1f);
        Race.Pop(pos + Vector3.up * 2.4f, cause == "banana" ? "SLIPPED!" : cause == "star" ? "STARRED!" : cause == "bump" ? "BONK!" : "-" + damage, new Color(1f, 0.8f, 0.2f));
    }

    void Explode()
    {
        DeadT = 3f; SpinT = 0f; BoostT = 0f;
        Sfx.At("boom", pos, 1f);
        Fx.B(pos + Vector3.up * 0.7f, new Color(1f, 0.5f, 0.1f), 30, 6f, 0.22f, 1.4f);
        Fx.B(pos + Vector3.up * 0.7f, new Color(0.2f, 0.2f, 0.2f), 14, 4f, 0.22f, 1.6f);
        Fx.B(pos + Vector3.up * 0.7f, color, 14, 5f, 0.18f, 1.6f);
        var l = Mix.Glow(pos + Vector3.up, new Color(1f, 0.6f, 0.2f), 9f, 6f); Object.Destroy(l.gameObject, 0.5f);
        // the wheels fly off
        for (int i = 0; i < 4; i++)
        {
            var w = Mix.Shape(PrimitiveType.Cylinder, pos + Vector3.up * 0.5f, new Vector3(0.45f, 0.1f, 0.45f), new Color(0.1f, 0.1f, 0.1f), false, "FlyingWheel");
            w.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            var r = w.AddComponent<Rigidbody>();
            r.velocity = new Vector3(Random.Range(-4f, 4f), Random.Range(4f, 8f), Random.Range(-4f, 4f));
            r.angularVelocity = Random.insideUnitSphere * 12f;
            Object.Destroy(w, 2.2f);
        }
        Kart.Show(false);
        Race.Pop(pos + Vector3.up * 2.6f, "K.O.!", new Color(1f, 0.3f, 0.2f), 1.6f);
        Race.OnRivalKo(this);
    }

    public void Tick(float dt, Racer player)
    {
        if (DeadT > 0f)
        {
            DeadT -= dt;
            if (DeadT <= 0f)
            {
                Hp = MaxHp; InvulnT = 2.5f; cur = 2f; yawExtra = 0f; BoostT = 2.2f;
                Kart.Show(true);
                Race.Pop(pos + Vector3.up * 2.4f, "BACK IN!", Color.white);
                Place();
            }
            return;
        }
        if (InvulnT > 0f)
        {
            InvulnT -= dt;
            bool vis = SpinT > 0f || InvulnT <= 0f || ((int)(InvulnT * 12f) % 2 == 0);
            Kart.Show(vis);
        }
        float want = baseSpeed;
        if (Race.State == RaceState.Racing || Race.State == RaceState.Finished)
        {
            // rubber band to keep the pack around the player
            float gap = Prog - player.Prog - 7f;   // aim to run a little ahead of the player so the pack stays in view
            want *= Mathf.Clamp(1f - gap * 0.03f, 0.8f, 1.35f);
        }
        else want = 0f;
        if (BoostT > 0f) { BoostT -= dt; want = Mathf.Min(want * 1.3f, 12f); Flames(); }
        if (SpinT > 0f) { SpinT -= dt; want = 0f; yawExtra += 720f / 1.1f * dt; if (SpinT <= 0f) { yawExtra = 0f; } }
        cur = Mathf.MoveTowards(cur, want, (want > cur ? 6f : 14f) * dt);
        S += cur * dt;
        // lane: wander, dodge bananas
        laneTimer -= dt;
        if (laneTimer <= 0f) { laneTimer = Random.Range(2f, 4f); laneTarget = Random.Range(-2.2f, 2.2f); }
        var ban = Items.BananaAhead(this);
        if (ban.HasValue && Mathf.Abs(ban.Value - lane) < 1.3f && DodgeRoll) laneTarget = ban.Value > 0f ? -2.4f : 2.4f;
        lane = Mathf.MoveTowards(lane, laneTarget, 2.2f * dt);
        // pads
        if (BoostT <= 0f) foreach (var pp in Track.Pads) if ((pp - pos).sqrMagnitude < 4f) { BoostT = 1.6f; Sfx.At("boost", pos, 0.6f); break; }
        // own items
        itemCool -= dt; ShellT -= dt;
        if (itemCool <= 0f && Race.State == RaceState.Racing)
        {
            itemCool = Random.Range(9f, 15f);
            Items.DropBanana(pos - fwd * 1.6f, this);
        }
        if (ShellT <= 0f && Race.State == RaceState.Racing && !Race.Safe)
        {
            ShellT = Random.Range(12f, 18f);
            float d = (player.Pos - pos).magnitude;
            if (d < 26f && Vector3.Dot((player.Pos - pos).normalized, fwd) > 0.3f) Items.FireShell(pos + fwd * 1.6f + Vector3.up * 0.5f, fwd, this, player);
        }
        // track progress
        Total = S;
        Place();
        Kart.Spin(cur * dt, Mathf.Clamp((lane - lastLane), -1f, 1f));
    }

    bool DodgeRoll => Random.value < 0.02f ? true : dodge;
    bool dodge = true;

    void Flames() { if (Time.frameCount % 3 == 0) Fx.Flame(pos - fwd * 1.0f + Vector3.up * 0.4f, -fwd); }

    public void ResetTo(float s, float ln)
    {
        S = s; lane = laneTarget = ln; cur = 0f; SpinT = 0f; BoostT = 0f; yawExtra = 0f; Hp = MaxHp; DeadT = 0f; InvulnT = 0f;
        fwd = Vector3.right; Kart.Show(true);
        Total = S; BaseProg = S;
        Place();
    }
}

public static class Fx
{
    static readonly Color[] flameCols = { new Color(1f, 0.85f, 0.2f), new Color(1f, 0.5f, 0.1f), new Color(1f, 0.25f, 0.05f), new Color(0.3f, 0.7f, 1f) };
    public static void Flame(Vector3 at, Vector3 dir)
    {
        for (int i = 0; i < 1; i++)
        {
            var c = Mix.Shape(PrimitiveType.Sphere, at + Random.insideUnitSphere * 0.05f, Vector3.one * Random.Range(0.07f, 0.15f), flameCols[Random.Range(0, 4)], false, "Flame");
            var rb = c.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.velocity = dir * Random.Range(2.5f, 5f) + Random.insideUnitSphere * 0.7f + Vector3.up * 0.4f;
            Object.Destroy(c, Random.Range(0.25f, 0.5f));
        }
    }

    /// <summary>Mix.Burst with smaller bits (the kart is close to the camera): size x0.55.</summary>
    public static void B(Vector3 pos, Color color, int count = 24, float speed = 8f, float size = 0.25f, float life = 2f) =>
        Mix.Burst(pos, color, Mathf.Max(1, (int)(count * 0.8f)), speed, size * 0.55f, life);

    public static void Stars(Vector3 at, int n = 12)
    {
        Fx.B(at, new Color(1f, 0.95f, 0.3f), n, 3f, 0.14f, 1.0f);
    }

    public static void Boom(Vector3 at, float size = 1f)
    {
        Fx.B(at, new Color(1f, 0.6f, 0.1f), (int)(24 * size), 5f * size, 0.2f, 1.1f);
        Fx.B(at, new Color(1f, 0.95f, 0.5f), (int)(12 * size), 3f * size, 0.14f, 0.8f);
        Fx.B(at, new Color(0.25f, 0.25f, 0.25f), (int)(10 * size), 3f * size, 0.22f, 1.4f);
        var l = Mix.Glow(at, new Color(1f, 0.6f, 0.2f), 7f * size, 5f); Object.Destroy(l.gameObject, 0.35f);
    }
}
