// Kart Game Takeover: the skateboard becomes a go-kart, a race circuit with a start gantry and a cloud marshal appears
// on the playground, three rival drivers race the bird, item boxes hand out turbo, bananas, shells and stars,
// and every skate trick feeds the kart's turbo.
using System.Collections;
using Sigf.Kit;
using UnityEngine;

public class SigfMod : MixMod
{
    public override void OnLoad() => G.StartLevel = "playground";

    public override void OnReady()
    {
        Race.Setup();
        Mix.Say("KART GAME TAKEOVER", 3.5f, new Color(1f, 0.85f, 0.1f), 0.12f, 84);
    }

    public override void OnUpdate() => Race.Tick();

    /// <summary>True on the middle of a straight (a jump here lands on the road).</summary>
    static bool OnStraight()
    {
        float s = Track.Nearest(G.Pos, out _);
        float St = Track.Straight, arc = Mathf.PI * Track.R;
        return (s > 3f && s < St - 9f) || (s > St + arc + 3f && s < 2f * St + arc - 9f);
    }

    public override IEnumerator Demo()
    {
        Race.Auto = true;
        Mix.Say("KART GAME TAKEOVER", 3.5f, new Color(1f, 0.85f, 0.1f), 0.12f, 84);
        // wait for the lights to go green, then keep the bird busy: a flip every few seconds feeds the turbo
        while (Race.State != RaceState.Racing) yield return null;
        yield return Mix.Wait(5f);
        for (int i = 0; i < 12; i++)
        {
            if (G.Bailed) G.GetUp();
            else if (!G.InAir && OnStraight()) { G.Launch(4.5f); yield return Mix.Wait(0.25f); G.Flip(i % 2 == 0 ? "Kickflip" : "Heelflip"); }
            yield return Mix.Wait(5.5f);
            for (int w = 0; w < 400 && !OnStraight(); w++) yield return null;
        }
    }
}
