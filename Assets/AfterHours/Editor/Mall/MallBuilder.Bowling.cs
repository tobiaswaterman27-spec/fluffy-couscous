using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// The bowling alley, finished: gutters and foul lines, the aiming arrows and dots, ten pins standing on each
    /// pin deck (some lanes left mid-frame, pins down), the black pit behind them, a masking panel with the lane
    /// numbers, scoring screens over the approach still showing the last game, and balls sat on the returns.
    /// </summary>
    public static partial class MallBuilder
    {
        static void BowlingDetail(Transform g, WingId w, float y, float u0, float end)
        {
            Vector3 fwd = MallLayout.WingForward(w), rt = MallLayout.WingRight(w);
            Quaternion face = Quaternion.LookRotation(fwd), back = Quaternion.LookRotation(-fwd);
            Vector3 P(float u, float v, float h) => MallLayout.WingPoint(w, u, v, y + h);
            var rng = new System.Random(5150);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            Material gutter = ArtLibrary.Plain("Bowling_Gutter", new Color(0.12f, 0.12f, 0.13f), 0.7f, 0.6f);
            Material laneMark = ArtLibrary.Plain("Lane_Mark", new Color(0.12f, 0.06f, 0.04f), 0.6f);
            Material pinWhite = ArtLibrary.Plain("Pin_White", new Color(0.93f, 0.92f, 0.88f), 0.8f);
            Material pinRed = ArtLibrary.Plain("Pin_Stripe", new Color(0.7f, 0.06f, 0.05f), 0.8f);
            Material pit = ArtLibrary.Plain("Bowling_Pit", new Color(0.01f, 0.01f, 0.012f), 0.1f);
            Material mask = ArtLibrary.Plain("Masking_Panel", new Color(0.08f, 0.1f, 0.25f), 0.4f, 0f, new Color(0.06f, 0.08f, 0.3f), false);
            Material scoreGlow = ArtLibrary.Plain("Score_Screen", new Color(0.04f, 0.06f, 0.12f), 0.9f, 0f, new Color(0.15f, 0.3f, 0.7f), false);
            Material[] balls =
            {
                ArtLibrary.Plain("Ball_Blue", new Color(0.08f, 0.15f, 0.5f), 0.95f),
                ArtLibrary.Plain("Ball_Red", new Color(0.5f, 0.05f, 0.05f), 0.95f),
                ArtLibrary.Plain("Ball_Green", new Color(0.06f, 0.35f, 0.15f), 0.95f),
                ArtLibrary.Plain("Ball_Black", new Color(0.03f, 0.03f, 0.035f), 0.95f),
                ArtLibrary.Plain("Ball_Purple", new Color(0.3f, 0.08f, 0.4f), 0.95f),
            };

            float laneA = u0 + 8f, laneB = end - 1f;
            float deck = laneB - 1.1f;                 // head pin position
            int lane = 1;
            for (float v = -12f; v <= 12.1f; v += 4f, lane++)
            {
                // gutters either side, foul line, arrows, approach dots
                foreach (float s in new[] { -1f, 1f })
                    Detail("Gutter", g, P((laneA + laneB) * 0.5f, v + s * 0.73f, 0.012f), new Vector3(0.24f, 0.02f, laneB - laneA), gutter, face);
                Detail("Foul line", g, P(laneA + 0.3f, v, 0.042f), new Vector3(1.2f, 0.003f, 0.03f), laneMark, face);
                for (int k = -3; k <= 3; k++)
                {
                    float back3 = Mathf.Abs(k) * 0.15f;
                    Detail("Lane arrow", g, P(laneA + 4.6f + back3, v + k * 0.15f, 0.042f), new Vector3(0.04f, 0.003f, 0.16f), laneMark, face);
                    Detail("Approach dot", g, P(laneA - 1.5f, v + k * 0.15f, 0.003f), new Vector3(0.03f, 0.003f, 0.03f), laneMark, face);
                }

                // the pit and back curtain behind the deck
                Detail("Pin pit", g, P(laneB + 0.33f, v, 0.15f), new Vector3(1.7f, 0.3f, 0.64f), pit, face);
                Detail("Pit curtain", g, P(laneB + 0.66f, v, 0.9f), new Vector3(1.6f, 1.2f, 0.03f), pit, face);

                // ten pins: standing, or knocked about where the last frame stopped
                int state = Mathf.Abs(MallLayout.Hash(lane, 7, 11, 3)) % 4;   // 0,1 full rack; 2 some down; 3 almost all down
                int idx = 0;
                for (int row = 0; row < 4; row++)
                for (int c = 0; c <= row; c++, idx++)
                {
                    float pu = deck + row * 0.264f;
                    float pv = v + (c - row * 0.5f) * 0.305f;
                    bool down = state == 2 ? (idx % 3 == 1) : state == 3 && idx != 6 && idx != 9;
                    if (!down)
                    {
                        Cyl("Pin", g, P(pu, pv, 0.04f), P(pu, pv, 0.24f), 0.12f, pinWhite);
                        Cyl("Pin neck", g, P(pu, pv, 0.24f), P(pu, pv, 0.31f), 0.055f, pinRed);
                        Cyl("Pin head", g, P(pu, pv, 0.31f), P(pu, pv, 0.38f), 0.07f, pinWhite);
                    }
                    else
                    {
                        float a = R(0f, Mathf.PI * 2f);
                        Vector3 dir = (fwd * Mathf.Cos(a) + rt * Mathf.Sin(a));
                        Vector3 b = P(pu + R(-0.2f, 0.4f), pv + R(-0.25f, 0.25f), 0.1f);
                        if (idx % 4 == 0) b = P(laneB + 0.3f, pv, 0.36f);     // swept into the pit
                        Cyl("Pin (down)", g, b, b + dir * 0.34f, 0.12f, pinWhite);
                    }
                }

                // balls waiting on the return
                int nb = 2 + Mathf.Abs(MallLayout.Hash(lane, 3, 5, 9)) % 3;
                for (int k = 0; k < nb; k++)
                    Ball("Bowling ball", g, P(u0 + 6.25f + k * 0.24f, v + 1.5f, 1.01f), 0.22f, balls[(lane + k) % balls.Length], true);

                // scoring screen over the approach, facing the bowlers' seats, still on the last game
                Vector3 sc = P(u0 + 7.2f, v, 2.7f);
                Detail("Score screen", g, sc, new Vector3(1.3f, 0.75f, 0.08f), blackMat, back * Quaternion.Euler(12f, 0f, 0f));
                Detail("Score screen face", g, sc - fwd * 0.045f, new Vector3(1.2f, 0.66f, 0.01f), scoreGlow, back * Quaternion.Euler(12f, 0f, 0f));
                Cyl("Score screen hanger", g, sc + Vector3.up * 0.38f, P(u0 + 7.2f, v, 4.4f), 0.04f, metal);
                int s1 = 40 + Mathf.Abs(MallLayout.Hash(lane, 1, 1, 1)) % 120, s2 = 30 + Mathf.Abs(MallLayout.Hash(lane, 2, 2, 2)) % 120;
                Sign(g, $"LANE {lane}\nDAZ   {s1}\nKEL   {s2}\nFRAME {4 + lane % 6}", sc - fwd * 0.06f, -fwd, 1.1f, 0.016f, false);
            }

            // masking panel across all the lanes above the decks
            Vector3 mc = P(deck - 0.6f, 0f, 1.75f);
            Detail("Masking panel", g, mc, new Vector3(28f, 1.3f, 0.06f), mask, back);
            lane = 1;
            for (float v = -12f; v <= 12.1f; v += 4f, lane++)
                Sign(g, lane.ToString(), P(deck - 0.64f, v, 1.85f), -fwd, 0.6f, 0.06f, false);
            Sign(g, "STRIKE ZONE BOWL", P(deck - 0.64f, -14f + 0f, 2.25f) + rt * 0f, -fwd, 4f, 0.035f, false);
        }
    }
}
