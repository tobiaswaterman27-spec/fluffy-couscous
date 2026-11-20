using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// VoltMart: the TV walls down both sides (three rows of sets, mostly dark, a few still lit on the blue
    /// NO SIGNAL screen, one cracked), price tags under each, and a peg wall of cables, chargers and headphones
    /// across the back with a tech-help counter.
    /// </summary>
    public static partial class MallBuilder
    {
        static void ElectronicsAnchorDetail(Transform g, WingId w, float y, float u0, float end)
        {
            Vector3 fwd = MallLayout.WingForward(w), rt = MallLayout.WingRight(w);
            Vector3 P(float u, float v, float h) => MallLayout.WingPoint(w, u, v, y + h);
            var rng = new System.Random(9191);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material bezel = ArtLibrary.Plain("TV_Bezel", new Color(0.03f, 0.03f, 0.035f), 0.6f);
            Material off = ArtLibrary.Plain("TV_Off", new Color(0.015f, 0.015f, 0.02f), 0.95f);
            Material blue = ArtLibrary.Plain("TV_NoSignal", new Color(0.05f, 0.1f, 0.4f), 0.9f, 0f, new Color(0.15f, 0.3f, 1.3f), false);
            Material tag = ArtLibrary.Plain("Price_Tag_Yellow", new Color(0.95f, 0.8f, 0.1f), 0.3f);
            Material pegboard = ArtLibrary.Plain("Pegboard", new Color(0.85f, 0.85f, 0.82f), 0.3f);
            Material blister = ArtLibrary.Plain("Blister_Pack", new Color(0.7f, 0.75f, 0.8f), 0.85f);
            Material crack = ArtLibrary.Plain("TV_Cracked", new Color(0.2f, 0.22f, 0.25f), 0.95f);

            foreach (int side in new[] { -1, 1 })
            {
                float vWall = side * (MallLayout.OuterWall - 0.32f);
                Quaternion face = Quaternion.LookRotation(-rt * side);
                Vector3 outN = -rt * side;
                // a long shelf unit the TVs sit on / hang above
                EditorBuild.Box("TV wall plinth", g, P((u0 + 3f + end - 2f) * 0.5f, vWall + outN.magnitude * 0f - side * 0.25f, 0.4f), new Vector3(end - 5f, 0.8f, 0.5f), bezel, SurfaceType.Wood, face);
                int idx = 0;
                for (int row = 0; row < 3; row++)
                {
                    float tvW = row == 0 ? 1.45f : row == 1 ? 1.15f : 0.9f;
                    float tvH = tvW * 0.58f;
                    float hc = row == 0 ? 0.8f + 0.12f + tvH * 0.5f : row == 1 ? 1.95f : 2.85f;
                    for (float u = u0 + 3.6f; u + tvW < end - 2f; u += tvW + 0.25f, idx++)
                    {
                        Vector3 c = P(u + tvW * 0.5f, vWall - side * (row == 0 ? 0.25f : 0.03f), hc);
                        Detail("TV", g, c, new Vector3(tvW, tvH, 0.05f), bezel, face);
                        int h = Mathf.Abs(MallLayout.Hash(idx, row, side, 55));
                        Material screen = h % 9 == 0 ? blue : (h % 23 == 5 ? crack : off);
                        Detail("TV screen", g, c + outN * 0.027f, new Vector3(tvW - 0.04f, tvH - 0.04f, 0.004f), screen, face);
                        if (screen == blue) Sign(g, "NO SIGNAL", c + outN * 0.03f, outN, tvW * 0.5f, 0.012f, false);
                        if (row == 0)
                        {
                            Detail("TV stand", g, c - Vector3.up * (tvH * 0.5f + 0.05f) - outN * 0.02f, new Vector3(0.3f, 0.1f, 0.18f), bezel, face);
                            Detail("Price tag", g, P(u + tvW * 0.5f, vWall - side * 0.505f, 0.6f), new Vector3(0.2f, 0.1f, 0.004f), tag, face);
                            Sign(g, "£" + (199 + h % 600) + ".99", P(u + tvW * 0.5f, vWall - side * 0.508f, 0.6f), outN, 0.18f, 0.012f, false);
                        }
                        else Detail("Wall bracket", g, c - outN * 0.04f, new Vector3(0.25f, 0.2f, 0.03f), bezel, face);
                    }
                }
            }

            // peg wall of accessories across the back, and the tech-help counter in front of it
            Quaternion toFront = Quaternion.LookRotation(-fwd);
            Detail("Peg wall", g, P(end - 0.33f, 0f, 1.5f), new Vector3(16f, 2.2f, 0.03f), pegboard, toFront);
            Material[] packs = { blister, ArtLibrary.Plain("Pack_Red", new Color(0.6f, 0.08f, 0.06f), 0.5f), ArtLibrary.Plain("Pack_Black", new Color(0.05f, 0.05f, 0.06f), 0.5f), ArtLibrary.Plain("Pack_Blue", new Color(0.1f, 0.25f, 0.6f), 0.5f) };
            var pk = new MeshKit();
            pk.Frame(Vector3.zero, toFront);
            Quaternion inv = Quaternion.Inverse(toFront);
            for (float v = -7.6f; v <= 7.6f; v += 0.4f)
            {
                for (float h = 0.7f; h <= 2.3f; h += 0.4f)
                {
                    if (rng.NextDouble() < 0.15) continue;   // picked clean
                    pk.Bar(inv * P(end - 0.35f, v, h + 0.12f), inv * P(end - 0.5f, v, h + 0.13f), 0.004f, metal, 5);
                    int count = 1 + rng.Next(3);
                    for (int k = 0; k < count; k++)
                        pk.Box(inv * P(end - 0.37f - k * 0.03f, v, h), new Vector3(0.14f, 0.22f, 0.025f), packs[rng.Next(packs.Length)], 0f, false, R(-3f, 3f));
                }
            }
            pk.Build(g, "Accessory packs");
            Sign(g, "CABLES  ·  CHARGERS  ·  HEADPHONES  ·  CASES", P(end - 0.34f, 0f, 2.75f), -fwd, 8f, 0.04f, false);
            EditorBuild.Box("Tech help counter", g, P(end - 3.2f, 0f, 0.55f), new Vector3(4f, 1.1f, 0.7f), whiteFixture, SurfaceType.Wood, toFront);
            Detail("Counter top", g, P(end - 3.2f, 0f, 1.115f), new Vector3(4.1f, 0.03f, 0.8f), blackMat, toFront);
            Detail("Repair laptop (open)", g, P(end - 3.2f, -0.8f, 1.14f), new Vector3(0.36f, 0.02f, 0.25f), bezel, toFront);
            Detail("Repair laptop screen", g, P(end - 3.33f, -0.8f, 1.27f), new Vector3(0.36f, 0.24f, 0.01f), bezel, toFront * Quaternion.Euler(-15f, 0f, 0f));
            Detail("Phone (screen out)", g, P(end - 3.1f, 0.6f, 1.135f), new Vector3(0.08f, 0.01f, 0.16f), bezel, toFront * Quaternion.Euler(0f, 20f, 0f));
            Detail("Repair ticket", g, P(end - 3.1f, 0.85f, 1.132f), new Vector3(0.08f, 0.002f, 0.12f), whiteFixture, toFront * Quaternion.Euler(0f, -10f, 0f));
            Sign(g, "TECH HELP", P(end - 3.2f, 0f, 0.8f) - fwd * 0.36f, -fwd, 2f, 0.04f, false);
        }
    }
}
