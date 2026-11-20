using UnityEngine;
using AfterHours.Audio;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// FreshWay's front end and back wall: checkout lanes either side of the entrance (belt, scanner, till, bagging
    /// well, lane light, an abandoned basket or two), a stack of baskets by the doors, and a run of glass-door
    /// freezers along the back, still humming and lit, frost on the glass.
    /// </summary>
    public static partial class MallBuilder
    {
        static void SupermarketDetail(Transform g, WingId w, float y, float u0, float end)
        {
            Vector3 fwd = MallLayout.WingForward(w), rt = MallLayout.WingRight(w);
            Vector3 P(float u, float v, float h) => MallLayout.WingPoint(w, u, v, y + h);
            Quaternion face = Quaternion.LookRotation(fwd), back = Quaternion.LookRotation(-fwd);
            var rng = new System.Random(3131);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material belt = ArtLibrary.Plain("Checkout_Belt", new Color(0.05f, 0.05f, 0.055f), 0.35f);
            Material body = ArtLibrary.Plain("Checkout_Body", new Color(0.12f, 0.35f, 0.18f), 0.5f);
            Material scanner = ArtLibrary.Plain("Scanner_Glass", new Color(0.4f, 0.05f, 0.04f), 0.95f, 0f, new Color(0.9f, 0.1f, 0.05f), false);
            Material laneOn = ArtLibrary.Plain("Lane_Light_On", new Color(0.3f, 0.3f, 0.3f), 0.5f, 0f, new Color(1.6f, 1.6f, 1.5f), false);
            Material basketRed = ArtLibrary.Plain("Basket_Red", new Color(0.7f, 0.08f, 0.06f), 0.4f);
            Material frost = ArtLibrary.Plain("Freezer_Glass", new Color(0.55f, 0.65f, 0.72f), 0.9f, 0f, new Color(0.25f, 0.35f, 0.42f), false);
            Material[] goods = { ArtLibrary.Plain("Goods_A", new Color(0.7f, 0.6f, 0.2f), 0.4f), ArtLibrary.Plain("Goods_B", new Color(0.2f, 0.35f, 0.6f), 0.4f), ArtLibrary.Plain("Goods_C", new Color(0.65f, 0.15f, 0.1f), 0.4f), ArtLibrary.Plain("Goods_D", new Color(0.85f, 0.85f, 0.8f), 0.4f) };

            void Basket(Vector3 at, float yaw, bool withShopping)
            {
                Quaternion r = Quaternion.Euler(0f, yaw, 0f);
                Detail("Basket", g, at + Vector3.up * 0.12f, new Vector3(0.42f, 0.24f, 0.3f), basketRed, r);
                Detail("Basket handle", g, at + Vector3.up * 0.3f, new Vector3(0.36f, 0.02f, 0.02f), blackMat, r);
                if (withShopping)
                    for (int k = 0; k < 4; k++)
                        Detail("Shopping", g, at + r * new Vector3(R(-0.12f, 0.12f), 0.24f + k * 0.02f, R(-0.08f, 0.08f)), new Vector3(R(0.06f, 0.14f), R(0.08f, 0.16f), R(0.06f, 0.1f)), goods[rng.Next(goods.Length)], r * Quaternion.Euler(0f, R(-30f, 30f), R(-10f, 10f)));
            }

            // ---- checkout lanes (4 a side), facing the doors ----
            int lane = 1;
            foreach (float v0 in new[] { -14.8f, 7.0f })
            {
                for (int i = 0; i < 4; i++, lane++)
                {
                    float v = v0 + i * 2.6f;
                    float u = u0 + 2.6f;
                    EditorBuild.Box("Checkout", g, P(u, v, 0.45f), new Vector3(0.75f, 0.9f, 3.0f), body, SurfaceType.Metal, face);
                    Detail("Checkout belt", g, P(u + 0.55f, v, 0.905f), new Vector3(0.5f, 0.012f, 1.5f), belt, face);
                    Detail("Scanner", g, P(u - 0.15f, v, 0.91f), new Vector3(0.36f, 0.02f, 0.3f), scanner, face);
                    Detail("Till", g, P(u - 0.15f, v - 0.55f, 1.15f), new Vector3(0.3f, 0.25f, 0.06f), blackMat, Quaternion.LookRotation(rt) * Quaternion.Euler(-10f, 0f, 0f));
                    Detail("Card terminal", g, P(u - 0.25f, v + 0.45f, 1.0f), new Vector3(0.08f, 0.16f, 0.06f), blackMat, back * Quaternion.Euler(-30f, 0f, 0f));
                    Detail("Bagging well", g, P(u - 1.05f, v, 0.75f), new Vector3(0.6f, 0.04f, 0.7f), metal, face);
                    Detail("Bag hooks", g, P(u - 1.05f, v + 0.33f, 1.1f), new Vector3(0.6f, 0.03f, 0.03f), metal, face);
                    Detail("Divider bar", g, P(u + 0.6f, v - 0.1f, 0.93f), new Vector3(0.05f, 0.03f, 0.4f), basketRed, face * Quaternion.Euler(0f, 90f, 0f));
                    Cyl("Lane light pole", g, P(u + 1.3f, v - 0.3f, 0.9f), P(u + 1.3f, v - 0.3f, 2.1f), 0.04f, metal);
                    bool lit = i % 3 == 1;
                    Detail("Lane number light", g, P(u + 1.3f, v - 0.3f, 2.2f), new Vector3(0.25f, 0.25f, 0.06f), lit ? laneOn : whiteFixture, back);
                    Sign(g, lane.ToString(), P(u + 1.3f, v - 0.3f, 2.2f) - fwd * 0.035f, -fwd, 0.2f, 0.03f, false);
                    if (i % 2 == 0) Basket(P(u + 0.8f, v + 0.05f, 0.91f), R(-20f, 20f), true);
                    else if (rng.NextDouble() < 0.5) Basket(P(u - 0.5f, v + 0.9f, 0f), R(0f, 180f), false);
                    // a till receipt roll trailing
                    Detail("Receipt (trailing)", g, P(u - 0.25f, v - 0.55f, 0.6f), new Vector3(0.06f, 0.6f, 0.004f), whiteFixture, face * Quaternion.Euler(0f, 0f, 6f));
                }
            }
            // basket stack by the doors
            for (int k = 0; k < 9; k++) Detail("Stacked basket", g, P(u0 + 0.9f, 5.6f, 0.12f + k * 0.06f), new Vector3(0.42f, 0.24f, 0.3f), basketRed, face * Quaternion.Euler(0f, 90f + R(-3f, 3f), 0f));
            Sign(g, "BASKETS", P(u0 + 0.9f, 5.6f, 1.15f), -fwd, 0.6f, 0.02f);

            // ---- freezers along the back wall ----
            float uf = end - 1.4f;
            for (float v = -16.5f; v <= 16.6f; v += 1.6f)
            {
                EditorBuild.Box("Freezer", g, P(uf, v, 1.05f), new Vector3(1.55f, 2.1f, 0.95f), whiteFixture, SurfaceType.Metal, back);
                Detail("Freezer door", g, P(uf - 0.49f, v, 1.05f), new Vector3(1.45f, 1.8f, 0.03f), frost, back);
                Detail("Freezer handle", g, P(uf - 0.52f, v + 0.6f, 1.1f), new Vector3(0.03f, 0.6f, 0.03f), metal, back);
                for (int s = 0; s < 4; s++)
                {
                    float h = 0.35f + s * 0.42f;
                    Detail("Freezer shelf", g, P(uf - 0.1f, v, h), new Vector3(1.4f, 0.02f, 0.7f), metal, back);
                    for (float a = -0.6f; a < 0.6f; a += 0.22f)
                        if (rng.NextDouble() > 0.2)
                            Detail("Frozen goods", g, P(uf - 0.2f + R(-0.05f, 0.05f), v + a + 0.1f, h + 0.1f), new Vector3(0.18f, R(0.12f, 0.2f), 0.25f), goods[rng.Next(goods.Length)], back);
                }
                Detail("Freezer header", g, P(uf - 0.47f, v, 2.0f), new Vector3(1.5f, 0.14f, 0.02f), body, back);
            }
            PropHum(g, P(uf - 0.6f, -9f, 0.4f), ProceduralSounds.Ambience.Buzz, 0.14f, 9f);
            PropHum(g, P(uf - 0.6f, 9f, 0.4f), ProceduralSounds.Ambience.Buzz, 0.14f, 9f);
            Sign(g, "FROZEN", P(uf - 0.5f, -8f, 2.35f), -fwd, 3f, 0.05f, false);
            Sign(g, "ICE CREAM & DESSERTS", P(uf - 0.5f, 8f, 2.35f), -fwd, 4f, 0.04f, false);
            // hanging aisle signs, one over each aisle (same row maths as the gondola rows), numbered
            {
                string[] names = { "BREAD & BAKERY", "TINS & PACKETS", "PASTA & RICE", "CEREALS", "DRINKS", "CRISPS & SNACKS", "HOUSEHOLD", "PET FOOD", "BABY", "HEALTH & BEAUTY", "WORLD FOODS", "BISCUITS" };
                AreaRect inside = MallLayout.WingRect(w, u0 + 0.8f, end - 0.8f, -18.6f, 18.6f);
                bool alongX = inside.Width >= inside.Depth;
                float crossMin = alongX ? inside.minZ : inside.minX, crossMax = alongX ? inside.maxZ : inside.maxX;
                float longMid = alongX ? (inside.minX + inside.maxX) * 0.5f : (inside.minZ + inside.maxZ) * 0.5f;
                Vector3 aisleDir = alongX ? Vector3.right : Vector3.forward;
                Material aisleBoard = ArtLibrary.Plain("Aisle_Board", new Color(0.1f, 0.35f, 0.18f), 0.4f);
                int num = 1;
                for (float c = crossMin + 1.1f + 0.55f + 1.5f; c < crossMax - 1.1f; c += 3f)
                {
                    Vector3 pos = alongX ? new Vector3(longMid, y + 3.6f, c) : new Vector3(c, y + 3.6f, longMid);
                    float uu = Vector3.Dot(pos, fwd);
                    if (uu < u0 + 5f || uu > end - 3f) continue;
                    Quaternion q = Quaternion.LookRotation(aisleDir);
                    Detail("Aisle sign", g, pos, new Vector3(1.6f, 0.45f, 0.04f), aisleBoard, q);
                    string label = num + "  " + names[(num - 1) % names.Length];
                    Sign(g, label, pos + aisleDir * 0.025f, aisleDir, 1.5f, 0.022f, false);
                    Sign(g, label, pos - aisleDir * 0.025f, -aisleDir, 1.5f, 0.022f, false);
                    foreach (float s2 in new[] { -0.6f, 0.6f })
                        Cyl("Aisle sign wire", g, pos + Vector3.Cross(Vector3.up, aisleDir) * s2 + Vector3.up * 0.22f, pos + Vector3.Cross(Vector3.up, aisleDir) * s2 + Vector3.up * 0.9f, 0.006f, metal);
                    num++;
                }
            }
            // the condensation they leave on the floor
            for (int k = 0; k < 4; k++)
                Detail("Wet patch", g, P(uf - 1.0f, R(-15f, 15f), 0.004f), new Vector3(R(0.4f, 0.9f), 0.003f, R(0.3f, 0.7f)), ArtLibrary.Plain("Wet_Sheen", new Color(0.05f, 0.06f, 0.07f), 0.97f), Quaternion.Euler(0f, R(0f, 180f), 0f));
        }
    }
}
