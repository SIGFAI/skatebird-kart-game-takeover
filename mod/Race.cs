// The race director: start countdown, laps, ranking, the player's kart, item roulette, HUD, pop-up texts and the demo autopilot.
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;

public enum RaceState { Waiting, Countdown, Racing, Finished }

public static class Race
{
    public static RaceState State = RaceState.Waiting;
    public static PlayerRacer Me;
    public static readonly List<Rival> Rivals = new List<Rival>();
    public static readonly List<Racer> All = new List<Racer>();
    public static ItemKind Held = ItemKind.None;
    public static bool Rolling;
    public static bool Auto;        // the demo bot drives
    public static bool Safe;        // no rival shells (grace period)
    public const int Laps = 3;
    public static int LapsDone = -1, Rank = 4, Wins, RaceNo;

    const float GroundOff = 0.16f;
    static float prevS, rollEnd, useAt, spinYaw, stateT, finishAt, autoLane, readyAt;
    static ItemKind rollShow;
    static float rollSwap;
    static int countStep;
    static readonly System.Random rng = new System.Random();

    class PopText { public Vector3 pos; public string text; public Color color; public float born, life; }
    static readonly List<PopText> pops = new List<PopText>();

    public static void Pop(Vector3 at, string text, Color color, float life = 1.1f) => pops.Add(new PopText { pos = at, text = text, color = color, born = Time.unscaledTime, life = life });

    // ---------------------------------------------------------------- setup
    public static void Setup()
    {
        Track.Build();
        Track.BuildPads();
        Items.Build();
        Me = new PlayerRacer();
        Me.Kart = KartModel.Build("red", new Color(0.92f, 0.12f, 0.16f), 0);
        All.Add(Me);
        Rivals.Add(new Rival("FROGGO", "yellow", new Color(1f, 0.8f, 0.05f), 1, 12f, -1.5f, 7.2f));
        Rivals.Add(new Rival("BEAR-O", "blue", new Color(0.15f, 0.4f, 0.95f), 2, 6f, -1.5f, 7.5f));
        Rivals.Add(new Rival("ROBO-X", "green", new Color(0.2f, 0.8f, 0.3f), 3, 9f, 1.5f, 7.0f));
        foreach (var r in Rivals) All.Add(r);
        ToGrid();
        Mix.OnGui = DrawHud;
        G.OnTrick(OnTrick);
        readyAt = Time.unscaledTime;
    }

    static void ToGrid()
    {
        var p = Track.Point(4f, 1.5f); p.y = 1.16f;
        G.Teleport(p, Quaternion.LookRotation(Vector3.right));
        G.SetVelocity(Vector3.zero);
        Track.Nearest(p, out _);
        prevS = Track.Nearest(G.Pos, out _);
        Me.Total = prevS; Me.BaseProg = prevS; LapsDone = -1;
        Me.SpinT = Me.BoostT = Me.StarT = 0f;
        Held = ItemKind.None; Rolling = false;
        float[] ss = { 12f, 6f, 9f }, ll = { -1.5f, -1.5f, 1.5f };
        for (int i = 0; i < Rivals.Count; i++) Rivals[i].ResetTo(ss[i], ll[i]);
        Items.Clear();
        Items.RefillCoins();
        State = RaceState.Waiting;
    }

    public static void BeginCountdown()
    {
        if (State != RaceState.Waiting) return;
        State = RaceState.Countdown; stateT = 0f; countStep = 0;
    }

    // ---------------------------------------------------------------- per frame
    public static void Tick()
    {
        float dt = Time.unscaledDeltaTime;
        if (Me == null || G.Body == null) return;
        dt = Mathf.Min(dt, 0.05f);

        if (State == RaceState.Waiting && (Mix.DemoStarted || (!Mix.DemoMode && Time.unscaledTime - readyAt > 4f))) BeginCountdown();
        if (State == RaceState.Countdown) CountdownTick(dt);
        if (State == RaceState.Waiting || State == RaceState.Countdown) G.SetVelocity(Vector3.zero);

        // progress and laps (unrolled so a lap is a real lap)
        float s = Track.Nearest(G.Pos, out float lane);
        float d = s - prevS; float L = Track.Len;
        if (d < -L / 2f) d += L; else if (d > L / 2f) d -= L;
        Me.Total += d; prevS = s;
        int laps = Mathf.FloorToInt((Me.Total - Track.StartS) / L);
        if (laps > LapsDone && State == RaceState.Racing)
        {
            LapsDone = laps;
            if (LapsDone >= 1 && LapsDone < Laps) { Mix.Say("LAP " + (LapsDone + 1) + " / " + Laps, 1.8f, new Color(1f, 0.9f, 0.2f), 0.4f, 60); Sfx.At("beep", null, 0.6f, 1.3f); }
            if (LapsDone >= Laps) Finish();
        }
        else if (State != RaceState.Racing) LapsDone = Mathf.Max(LapsDone, laps);

        // players, rivals
        foreach (var r in Rivals) r.Tick(dt, Me);
        PlayerTick(dt);
        Items.Tick(dt, Me, Rivals);
        Collisions(dt);
        Ranking();
        CloudTick();

        // the item in hand
        if (Rolling)
        {
            if (Time.unscaledTime >= rollSwap) { rollShow = (ItemKind)(1 + rng.Next(4)); rollSwap = Time.unscaledTime + 0.07f; }
            if (Time.unscaledTime >= rollEnd) { Rolling = false; Held = Pick(); useAt = Time.unscaledTime + (Auto ? 0.9f : 4f); Sfx.At("beep", null, 0.5f, 1.8f); }
        }
        else if (Held != ItemKind.None && (Time.unscaledTime >= useAt || KeyUse()))
        {
            var k = Held; Held = ItemKind.None;
            Items.Use(k, Me);
        }

        // finished: show the result a few seconds, then a new race
        if (State == RaceState.Finished && Time.unscaledTime > finishAt) { RaceNo++; ToGrid(); BeginCountdown(); }
        AudioTick();
        pops.RemoveAll(p => Time.unscaledTime - p.born > p.life);
    }

    static AudioSource music, engine;
    static void AudioTick()
    {
        try
        {
            if (music == null)
            {
                var go = new GameObject("KartAudio");
                music = go.AddComponent<AudioSource>(); music.clip = Mix.Sound("snd/music.wav"); music.loop = true; music.volume = 0.3f; music.spatialBlend = 0f;
                engine = go.AddComponent<AudioSource>(); engine.clip = Mix.Sound("snd/engine.wav"); engine.loop = true; engine.volume = 0f; engine.spatialBlend = 0f;
                Object.DontDestroyOnLoad(go);
            }
            bool racing = State == RaceState.Racing || State == RaceState.Finished || State == RaceState.Countdown;
            if (racing && !music.isPlaying) music.Play();
            if (racing && !engine.isPlaying) engine.Play();
            float sp = G.Speed;
            engine.volume = Mathf.Lerp(engine.volume, State == RaceState.Racing ? Mathf.Clamp01(0.08f + sp * 0.02f) : 0.05f, 0.1f);
            engine.pitch = Mathf.Lerp(engine.pitch, 0.8f + sp * 0.07f + (Me.BoostT > 0f ? 0.4f : 0f), 0.1f);
        }
        catch (System.Exception e) { Mix.Warn("audio: " + e.Message); music = null; }
    }

    static bool KeyUse() { try { return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F); } catch { return false; } }

    static readonly ItemKind[] script = { ItemKind.Shell, ItemKind.Banana, ItemKind.Turbo, ItemKind.Shell, ItemKind.Star, ItemKind.Shell, ItemKind.Turbo };
    static int scriptAt;

    static ItemKind Pick()
    {
        if (Auto) return script[scriptAt++ % script.Length];   // the demo bot gets a lucky streak that shows every item
        int roll = rng.Next(100);
        if (roll < 34) return ItemKind.Turbo;
        if (roll < 60) return ItemKind.Banana;
        if (roll < 90) return ItemKind.Shell;
        return ItemKind.Star;
    }

    public static void StartRoulette() { Rolling = true; rollEnd = Time.unscaledTime + 1.0f; rollSwap = 0f; }

    public static void CoinRush()
    {
        Me.BoostT = Mathf.Max(Me.BoostT, 1.2f);
        Pop(Me.Pos + Vector3.up * 2.4f, "10 COINS! SPEED UP!", new Color(1f, 0.85f, 0.2f), 1.4f);
        Sfx.At("boost", null, 0.5f, 1.2f);
    }

    public static void OnPlayerHit(Racer victim)
    {
        Wins++;
        G.Screm();   // the bird screams on a hit: the game's own signature trick (scores points)
    }

    public static void OnRivalKo(Rival r)
    {
        Wins++;
        Mix.Say("K.O.!  " + r.Name, 1.6f, new Color(1f, 0.35f, 0.25f), 0.64f, 64);
        G.Screm();
    }

    static void OnTrick(Skatebirb.Gameplay.Trick t)
    {
        if (State != RaceState.Racing) return;
        Me.BoostT = Mathf.Min(Me.BoostT + 0.8f, 3.0f);
        Items.Coins += 2;
        Fx.B(Me.Pos + Vector3.up * 0.8f, new Color(1f, 0.85f, 0.2f), 14, 3f, 0.1f, 1f);
        Pop(Me.Pos + Vector3.up * 2.5f, "TRICK TURBO!", new Color(0.4f, 0.9f, 1f));
        Sfx.At("boost", null, 0.5f, 1.4f);
    }

    // ---------------------------------------------------------------- countdown
    static void CountdownTick(float dt)
    {
        stateT += dt;
        int step = Mathf.FloorToInt(stateT / 1.0f);
        if (step >= countStep && countStep < 4)
        {
            countStep = step + 1;
            switch (step)
            {
                case 0: Lamps(1, false); Mix.Say("3", 0.95f, new Color(1f, 0.3f, 0.3f), 0.35f, 140); Sfx.At("beep"); break;
                case 1: Lamps(2, false); Mix.Say("2", 0.95f, new Color(1f, 0.6f, 0.2f), 0.35f, 140); Sfx.At("beep"); break;
                case 2: Lamps(3, false); Mix.Say("1", 0.95f, new Color(1f, 0.9f, 0.2f), 0.35f, 140); Sfx.At("beep"); break;
                case 3:
                    Lamps(3, true);
                    Mix.Say("GO!", 1.4f, new Color(0.3f, 1f, 0.4f), 0.35f, 170);
                    Sfx.At("go"); Sfx.At("horn", Track.Cloud != null ? Track.Cloud.position : Me.Pos, 0.5f, 1.3f);
                    State = RaceState.Racing; LapsDone = -1;
                    Me.BoostT = Auto ? 1.4f : 0.7f;       // rocket start
                    Fx.B(Track.Point(Track.StartS, 0f) + Vector3.up * 3f, Color.white, 30, 4f, 0.12f, 1.4f);
                    break;
            }
        }
    }

    static void Lamps(int n, bool green)
    {
        for (int i = 0; i < 3; i++)
        {
            var g = Track.Lamps[i]; if (g == null) continue;
            Color c = i < n ? (green ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.15f, 0.1f)) : new Color(0.2f, 0.2f, 0.2f);
            var m = g.GetComponent<Renderer>().material;
            m.color = c;
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", i < n ? c * 2.2f : Color.black); }
        }
    }

    static void Finish()
    {
        State = RaceState.Finished; finishAt = Time.unscaledTime + 7f;
        Ranking();
        string[] nm = { "", "1ST PLACE!", "2ND PLACE!", "3RD PLACE!", "4TH PLACE" };
        Mix.Say("FINISH!", 2f, Color.white, 0.25f, 120);
        Mix.Say(nm[Mathf.Clamp(Rank, 1, 4)], 6f, Rank == 1 ? new Color(1f, 0.85f, 0.1f) : new Color(0.8f, 0.85f, 1f), 0.45f, 90);
        Sfx.At("fanfare", null, 1f);
        for (int i = 0; i < 4; i++)
            Mix.After(i * 0.35f, () => { Fx.B(Me.Pos + Vector3.up * 2f, Random.ColorHSV(0f, 1f, 0.8f, 1f, 1f, 1f), 24, 6f, 0.12f, 2.2f); G.Screm(); });
    }

    // ---------------------------------------------------------------- the player's kart
    static void PlayerTick(float dt)
    {
        var tr = Me.Kart.Root.transform;
        var f = G.Body.transform.forward; f.y = 0f;
        if (f.sqrMagnitude < 0.01f) f = Vector3.right;
        f.Normalize();
        float spinVel = 0f;
        if (Me.SpinT > 0f) { Me.SpinT -= dt; spinVel = 720f / 1.1f; spinYaw += spinVel * dt; if (Me.SpinT <= 0f) spinYaw = 0f; }
        tr.position = G.Pos + Vector3.down * GroundOff + Vector3.up * (Me.SpinT > 0f ? Mathf.Sin(Me.SpinT / 1.1f * Mathf.PI) * 0.3f : 0f);
        tr.rotation = Quaternion.LookRotation(f, Vector3.up) * Quaternion.Euler(0f, spinYaw, 0f);

        if (!Auto && State == RaceState.Racing)
        {
            // a player who wandered far from the circuit gets picked up by the cloud marshal
            float s1 = Track.Nearest(G.Pos, out float l1);
            offT = Mathf.Abs(l1) > Track.HalfW + 9f || G.Pos.y < 0f ? offT + dt : 0f;
            if (offT > 4f) Rescue(s1);
        }
        if (Auto && (State == RaceState.Racing || State == RaceState.Finished)) Autopilot(dt);
        else if (State == RaceState.Racing && !G.InAir && !G.Bailed && Me.SpinT <= 0f && G.Speed > 0.8f && G.Speed < 5f) G.Push(f * 2.5f * dt);   // gentle gas pedal

        if (Me.BoostT > 0f)
        {
            Me.BoostT -= dt;
            if (G.Speed < 13f && !G.Bailed) G.Push(f * 38f * dt);
            if (Time.frameCount % 2 == 0)
            {
                Fx.Flame(Me.Kart.Exhaust0.position, -f);
                Fx.Flame(Me.Kart.Exhaust1.position, -f);
            }
            Me.Kart.Spin(0f, 0f);
        }
        if (Me.StarT > 0f)
        {
            Me.StarT -= dt;
            Me.Kart.Tint(Color.HSVToRGB((Time.unscaledTime * 1.6f) % 1f, 0.8f, 1f));
            if (Time.frameCount % 3 == 0) Fx.B(Me.Pos + Vector3.up * 0.5f, Color.HSVToRGB(Random.value, 0.8f, 1f), 2, 2f, 0.1f, 0.6f);
            if (Me.StarT <= 0f) Me.Kart.Tint(null);
        }
        // boost pads
        if (Me.BoostT < 0.8f)
            foreach (var p in Track.Pads)
            {
                var dd = G.Pos - p; dd.y = 0f;
                if (dd.sqrMagnitude < 4f) { Me.BoostT = 1.6f; Sfx.At("boost", null, 0.7f); Pop(Me.Pos + Vector3.up * 2.2f, "BOOST PAD!", new Color(1f, 0.7f, 0.1f)); break; }
            }
        Me.Kart.Spin(G.Speed * dt, 0f);
    }

    static float offT, stuckT;
    /// <summary>The cloud marshal drops the kart back on the road (the bot left the track or got stuck).</summary>
    static void Rescue(float s)
    {
        Track.At(s + 2f, out var p, out var t);
        p.y = 1.16f;
        G.Teleport(p, Quaternion.LookRotation(t, Vector3.up));
        G.SetVelocity(t * 4f);
        Mix.Log("rescue at s " + s);
        Fx.B(p + Vector3.up * 0.8f, Color.white, 26, 3f, 0.3f, 1.2f);
        Pop(p + Vector3.up * 2.4f, "CLOUD RESCUE!", Color.white, 1.4f);
        Sfx.At("horn", p, 0.5f, 1.4f);
        offT = stuckT = 0f;
        prevS = Track.Nearest(p, out _);
    }

    static void Autopilot(float dt)
    {
        if (G.Bailed) { G.GetUp(); return; }
        float s0 = Track.Nearest(G.Pos, out float lane0);
        offT = Mathf.Abs(lane0) > Track.HalfW + 1.2f ? offT + dt : 0f;
        stuckT = (G.Speed < 1f && Me.SpinT <= 0f) ? stuckT + dt : 0f;
        if (offT > 1.5f || stuckT > 2.5f) { Mix.Log("rescue reason off " + offT.ToString("F1") + " stuck " + stuckT.ToString("F1") + " lane " + lane0.ToString("F1") + " speed " + G.Speed.ToString("F1") + " air " + G.InAir); Rescue(s0); return; }
        float s = s0; float lane = lane0;
        // aim for item box rows, otherwise the middle
        float wantLane = Items.SuggestLane(s);
        autoLane = Mathf.MoveTowards(autoLane, wantLane, 2.5f * dt);
        var tgt = Track.Point(s + 4.5f + G.Speed * 0.35f, autoLane);
        var want = tgt - G.Pos; want.y = 0f; want.Normalize();
        var f = G.Forward;
        float ang = Vector3.SignedAngle(f, want, Vector3.up);
        float turn = Mathf.Clamp(ang, -170f * dt, 170f * dt);
        var nf = Quaternion.AngleAxis(turn, Vector3.up) * f;
        float speed = Me.BoostT > 0f ? 12f : 5.0f;
        if (Me.SpinT > 0f) speed = 1.5f;
        var v = G.Velocity;
        if (!G.InAir) G.Body.rotation = Quaternion.LookRotation(nf, Vector3.up);
        var horiz = nf * Mathf.MoveTowards(new Vector3(v.x, 0f, v.z).magnitude, speed, 18f * dt);
        G.SetVelocity(new Vector3(horiz.x, v.y, horiz.z));
    }

    // ---------------------------------------------------------------- bumping
    static void Collisions(float dt)
    {
        foreach (var r in Rivals)
        {
            if (!r.Alive) continue;
            var d = r.Pos - G.Pos; d.y = 0f;
            float dist = d.magnitude;
            if (dist > 1.7f || dist < 0.01f) continue;
            var n = d / dist;
            bool hard = G.Speed > 5.5f || Me.StarT > 0f;
            if (Me.StarT > 0f) { r.Hit(3, "star", G.Pos); OnPlayerHit(r); }
            else if (hard && !r.Shielded)
            {
                r.Hit(1, "bump", G.Pos); OnPlayerHit(r);
                G.Push(-n * 2.5f);
                Fx.B(Vector3.Lerp(r.Pos, G.Pos, 0.5f) + Vector3.up * 0.7f, Color.white, 8, 3f, 0.1f, 0.5f);
            }
        }
    }

    static void Ranking()
    {
        int rank = 1;
        foreach (var r in Rivals) if (r.Total > Me.Total) rank++;
        if (rank != Rank) Mix.Log("rank " + rank + " lap " + LapsDone);
        Rank = rank;
    }

    static void CloudTick()
    {
        if (Track.Cloud == null) return;
        var p = Track.Cloud;
        float t = Time.unscaledTime;
        var b = p.position; Track.At(Track.StartS, out var sp, out _);
        p.position = new Vector3(sp.x, 6.2f + Mathf.Sin(t * 1.7f) * 0.18f, sp.z + Mathf.Sin(t * 0.8f) * 0.35f);
        if (Track.FlagT != null) Track.FlagT.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * (State == RaceState.Racing ? 9f : 3f)) * 25f, Mathf.Sin(t * 5f) * 10f);
        // the cloud watches the player
        if (Me != null) { var look = Me.Pos - p.position; look.y = 0f; if (look.sqrMagnitude > 0.1f) { var q = Quaternion.LookRotation(-look.normalized, Vector3.up); p.rotation = Quaternion.Slerp(p.rotation, q, 0.05f); } }
    }

    // ---------------------------------------------------------------- HUD
    static string Ord(int n) => n == 1 ? "1ST" : n == 2 ? "2ND" : n == 3 ? "3RD" : n + "TH";

    static Texture2D IconFor(ItemKind k)
    {
        switch (k)
        {
            case ItemKind.Turbo: return KartModel.Tex("tex/icon_turbo.png");
            case ItemKind.Banana: return KartModel.Tex("tex/icon_banana.png");
            case ItemKind.Shell: return KartModel.Tex("tex/icon_shell.png");
            case ItemKind.Star: return KartModel.Tex("tex/icon_star.png");
        }
        return null;
    }
    static string NameOf(ItemKind k) => k == ItemKind.Turbo ? "TURBO" : k == ItemKind.Banana ? "BANANA" : k == ItemKind.Shell ? "SHELL" : k == ItemKind.Star ? "STAR" : "";

    static void DrawHud()
    {
        if (Me == null) return;
        // rank + lap (top left)
        var gold = new Color(1f, 0.85f, 0.15f);
        Mix.Rect01(0.015f, 0.025f, 0.2f, 0.17f, new Color(0f, 0f, 0f, 0.45f));
        Mix.Label(Ord(Rank), 0.115f, 0.075f, 76, Rank == 1 ? gold : Color.white);
        Mix.Label("LAP " + Mathf.Clamp(LapsDone + 1, 1, Laps) + " / " + Laps, 0.115f, 0.155f, 34, Color.white);
        // coins
        Mix.Rect01(0.015f, 0.205f, 0.12f, 0.06f, new Color(0f, 0f, 0f, 0.45f));
        Mix.Icon(KartModel.Tex("tex/coin.png"), 0.035f, 0.235f, 0.05f);
        Mix.Label("x " + Items.Coins, 0.09f, 0.235f, 34, gold);
        // item slot (top centre)
        Mix.Rect01(0.43f, 0.02f, 0.14f, 0.2f, new Color(0f, 0f, 0f, 0.5f));
        var shown = Rolling ? rollShow : Held;
        if (shown != ItemKind.None)
        {
            Mix.Icon(IconFor(shown), 0.5f, 0.1f, 0.11f);
            Mix.Label(Rolling ? "?" : NameOf(shown), 0.5f, 0.19f, 30, Rolling ? Color.white : gold);
        }
        else Mix.Label("ITEM", 0.5f, 0.12f, 30, new Color(1f, 1f, 1f, 0.5f));
        if (Held != ItemKind.None && !Rolling && !Auto) Mix.Label("press E to use", 0.5f, 0.245f, 22, new Color(1f, 1f, 1f, 0.8f));
        // rival tags with health bars
        foreach (var r in Rivals)
        {
            if (!r.Alive) continue;
            var c = Camera.main; if (c == null) continue;
            var sp = c.WorldToScreenPoint(r.Pos + Vector3.up * 2.1f);
            if (sp.z <= 0.5f) continue;
            float x = sp.x / Screen.width, y = 1f - sp.y / Screen.height;
            Mix.Label(r.Name, x, y - 0.025f, 26, r.Color);
            Mix.Rect01(x - 0.035f, y - 0.006f, 0.07f, 0.014f, new Color(0f, 0f, 0f, 0.7f));
            Mix.Rect01(x - 0.035f + 0.001f, y - 0.005f, 0.068f * r.Hp / r.MaxHp, 0.012f, Color.Lerp(new Color(1f, 0.2f, 0.1f), new Color(0.3f, 1f, 0.3f), (float)r.Hp / r.MaxHp));
        }
        // pop-up texts
        for (int pi = 0; pi < pops.Count; pi++)
        {
            var p = pops[pi];
            float a = (Time.unscaledTime - p.born) / p.life;
            var col = p.color; col.a = 1f - a * a;
            Mix.WorldLabel(p.pos + Vector3.up * (a * 1.2f + (pops.Count - 1 - pi) * 0.75f), p.text, col, 40);
        }
    }
}
