using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Retail carts down the middle of some concourses: a painted barrow on big spoked wheels, four posts and a
    /// striped canopy, a sign board, the stock on its tiers - phone cases, sunglasses on a spinner, pick 'n' mix
    /// tubs, perfume testers. About half are covered for the night with a tarp roped down.
    /// </summary>
    public static partial class MallBuilder
    {
        static readonly string[] CartNames = { "CASE CRAZY", "SHADES", "SWEET SPOT", "SCENT BAR" };

        static void ConcourseCarts(Transform g, MallFloor f, WingId w, float y)
        {
            foreach (float u in new[] { 40f, 88f })
            {
                int h = Mathf.Abs(MallLayout.Hash((int)f, (int)w, (int)u, 919));
                if (h % 3 == 0) continue;   // no cart here
                Cart(g, w, y, u, h);
            }
        }

        static void Cart(Transform g, WingId w, float y, float u, int h)
        {
            Vector3 fwd = MallLayout.WingForward(w), rt = MallLayout.WingRight(w);
            Vector3 P(float du, float v, float hh) => MallLayout.WingPoint(w, u + du, v, y + hh);
            Quaternion face = Quaternion.LookRotation(fwd);
            var rng = new System.Random(h);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int type = h % 4;
            bool covered = (h / 4) % 2 == 0;
            Color[] paint = { new Color(0.1f, 0.25f, 0.2f), new Color(0.35f, 0.08f, 0.1f), new Color(0.12f, 0.15f, 0.35f), new Color(0.2f, 0.12f, 0.25f) };
            Material body = ArtLibrary.Plain($"Cart_Body_{type}", paint[type], 0.55f);
            Material trim = ArtLibrary.Plain("Cart_Trim_Gold", new Color(0.7f, 0.55f, 0.2f), 0.6f, 0.9f);
            Material stripeA = ArtLibrary.Plain("Canopy_Cream", new Color(0.88f, 0.84f, 0.72f), 0.2f);
            Material stripeB = ArtLibrary.Plain($"Canopy_Stripe_{type}", paint[type] * 1.4f, 0.2f);
            Material tarp = ArtLibrary.Plain("Cart_Tarp", new Color(0.12f, 0.2f, 0.32f), 0.35f);
            Material rope = ArtLibrary.Plain("Rope", new Color(0.6f, 0.55f, 0.4f), 0.15f);

            const float L = 2.0f, W = 1.0f;
            EditorBuild.Box("Cart", g, P(0f, 0f, 0.5f), new Vector3(W, 0.6f, L), body, SurfaceType.Wood, face);
            Detail("Cart top", g, P(0f, 0f, 0.83f), new Vector3(W + 0.1f, 0.05f, L + 0.1f), woodMat, face);
            Detail("Cart trim", g, P(0f, 0f, 0.78f), new Vector3(W + 0.02f, 0.03f, L + 0.02f), trim, face);
            // spoked wheels on one axle, a stand at the other end
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 hub = P(-0.45f, s * (W * 0.5f + 0.04f), 0.32f);
                Cyl("Cart wheel rim", g, hub - rt * 0.02f, hub + rt * 0.02f, 0.64f, body);
                Cyl("Cart wheel hub", g, hub - rt * 0.05f, hub + rt * 0.05f, 0.1f, trim);
                for (int k = 0; k < 6; k++)
                {
                    Quaternion sp = Quaternion.AngleAxis(k * 30f, rt);
                    Detail("Spoke", g, hub, new Vector3(0.02f, 0.58f, 0.02f), trim, sp * face);
                }
                Cyl("Cart leg", g, P(0.8f, s * (W * 0.5f - 0.08f), 0.2f), P(0.8f, s * (W * 0.5f - 0.08f), 0f), 0.06f, body);
            }
            Cyl("Cart handle", g, P(1.05f, -0.4f, 0.75f), P(1.05f, 0.4f, 0.75f), 0.04f, trim);

            // posts and canopy
            foreach (float a in new[] { -L * 0.5f + 0.06f, L * 0.5f - 0.06f })
            foreach (float s in new[] { -W * 0.5f + 0.05f, W * 0.5f - 0.05f })
                Cyl("Canopy post", g, P(a, s, 0.85f), P(a, s, 2.25f), 0.04f, trim);
            for (int k = 0; k < 8; k++)
                Detail("Canopy stripe", g, P(-L * 0.5f - 0.05f + (k + 0.5f) * (L + 0.1f) / 8f, 0f, 2.3f), new Vector3(W + 0.25f, 0.03f, (L + 0.1f) / 8f), k % 2 == 0 ? stripeA : stripeB, face * Quaternion.Euler(0f, 0f, 0f));
            for (int k = 0; k < 10; k++)
                foreach (float s in new[] { -1f, 1f })
                    Detail("Canopy scallop", g, P(-L * 0.5f + (k + 0.5f) * L / 10f, s * (W * 0.5f + 0.12f), 2.22f), new Vector3(0.01f, 0.14f, L / 10f - 0.02f), k % 2 == 0 ? stripeA : stripeB, face);
            Detail("Cart sign", g, P(0f, 0f, 2.5f), new Vector3(0.04f, 0.3f, 1.4f), body, face);
            foreach (float s in new[] { -1f, 1f })
                Sign(g, CartNames[type], P(0f, s * 0.025f, 2.5f), rt * s, 1.3f, 0.03f, false);

            if (covered)
            {
                // tarp pulled over the top tier and roped down
                Detail("Tarp", g, P(0f, 0f, 1.25f), new Vector3(W + 0.14f, 0.82f, L + 0.12f), tarp, face);
                Detail("Tarp (draped)", g, P(0f, 0f, 1.67f), new Vector3(W + 0.1f, 0.04f, L + 0.08f), tarp, face * Quaternion.Euler(0f, 0f, 2f));
                foreach (float a in new[] { -0.6f, 0.6f })
                    Detail("Rope", g, P(a, 0f, 1.25f), new Vector3(W + 0.17f, 0.86f, 0.02f), rope, face);
                Sign(g, "CLOSED", P(0.75f, -(W * 0.5f + 0.08f), 1.25f), -rt, 0.5f, 0.02f);
                return;
            }

            // display tier and stock
            Detail("Display tier", g, P(0f, 0f, 1.15f), new Vector3(W - 0.3f, 0.6f, L - 0.3f), body, face);
            Detail("Display tier top", g, P(0f, 0f, 1.46f), new Vector3(W - 0.25f, 0.03f, L - 0.25f), woodMat, face);
            switch (type)
            {
                case 0:   // phone cases on pegs, both faces
                    foreach (float s in new[] { -1f, 1f })
                        for (int k = 0; k < 7; k++)
                        for (int r = 0; r < 2; r++)
                            if (rng.NextDouble() > 0.15)
                                Detail("Phone case", g, P(-0.65f + k * 0.21f, s * (W * 0.5f - 0.14f), 1.0f + r * 0.25f), new Vector3(0.01f, 0.16f, 0.08f), ArtLibrary.Plain($"Case_{(k + r) % 5}", Color.HSVToRGB(((k * 3 + r) % 10) / 10f, 0.6f, 0.7f), 0.6f), face);
                    for (int k = 0; k < 5; k++) Detail("Charger box", g, P(-0.6f + k * 0.3f, 0f, 1.52f), new Vector3(0.12f, 0.1f, 0.18f), whiteFixture, face);
                    break;
                case 1:   // sunglasses spinner and a mirror
                    Cyl("Spinner pole", g, P(0.3f, 0f, 1.47f), P(0.3f, 0f, 2.15f), 0.03f, trim);
                    for (int k = 0; k < 12; k++)
                    {
                        float a = k * 30f * Mathf.Deg2Rad;
                        Vector3 p = P(0.3f, 0f, 1.6f + (k % 3) * 0.18f) + (rt * Mathf.Cos(a) + fwd * Mathf.Sin(a)) * 0.16f;
                        Detail("Sunglasses", g, p, new Vector3(0.13f, 0.04f, 0.01f), blackMat, Quaternion.LookRotation(rt * Mathf.Cos(a) + fwd * Mathf.Sin(a)));
                    }
                    Detail("Cart mirror", g, P(-0.6f, 0f, 1.75f), new Vector3(0.02f, 0.5f, 0.3f), mirrorMat, face);
                    break;
                case 2:   // pick 'n' mix tubs with scoops
                    for (int k = 0; k < 8; k++)
                    {
                        Vector3 p = P(-0.55f + (k % 4) * 0.36f, k < 4 ? -0.18f : 0.18f, 1.48f);
                        Detail("Sweet tub", g, p + Vector3.up * 0.07f, new Vector3(0.3f, 0.14f, 0.3f), glassMat, face);
                        Detail("Sweets", g, p + Vector3.up * 0.05f, new Vector3(0.27f, 0.09f, 0.27f), ArtLibrary.Plain($"Sweets_{k % 6}", Color.HSVToRGB((k % 6) / 6f, 0.75f, 0.85f), 0.5f), face);
                    }
                    Detail("Scoop", g, P(0.7f, 0f, 1.56f), new Vector3(0.06f, 0.04f, 0.14f), metal, face * Quaternion.Euler(0f, 30f, 0f));
                    Cyl("Paper bags", g, P(-0.75f, 0f, 1.48f), P(-0.75f, 0f, 1.6f), 0.18f, whiteFixture);
                    break;
                default:  // perfume testers on a mirrored tray
                    Detail("Mirror tray", g, P(0f, 0f, 1.48f), new Vector3(0.6f, 0.01f, 1.5f), mirrorMat, face);
                    for (int k = 0; k < 12; k++)
                    {
                        Vector3 p = P(-0.6f + (k % 6) * 0.24f, k < 6 ? -0.12f : 0.12f, 1.49f);
                        float hh = R(0.08f, 0.16f);
                        Detail("Perfume bottle", g, p + Vector3.up * hh * 0.5f, new Vector3(0.06f, hh, 0.05f), ArtLibrary.Plain($"Perfume_{k % 4}", new Color(0.8f, 0.6f + 0.1f * (k % 4), 0.4f + 0.12f * (k % 3)), 0.95f), face);
                        Cyl("Perfume cap", g, p + Vector3.up * hh, p + Vector3.up * (hh + 0.03f), 0.03f, trim);
                    }
                    for (int k = 0; k < 6; k++) Detail("Tester strip", g, P(0.8f, R(-0.2f, 0.2f), 0.86f), new Vector3(0.02f, 0.002f, 0.12f), whiteFixture, face * Quaternion.Euler(0f, R(0f, 180f), 0f));
                    break;
            }
        }
    }
}
