using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Themed decor for the restaurants and cafes, all on the walls or hung from the ceiling so it never gets in the
    /// way of the tables: red paper lanterns (Golden Dragon), a wine rack and straw-covered bottles (Luigi's), pierced
    /// brass lamps (Spice Route), a flame-painted hood (Sizzle Grill), bunting and a teapot shelf (The Tea Room),
    /// coffee sacks and a chalkboard (the coffee shops), a giant pancake stack (Pancake Parlour), crates of fruit
    /// (Fresh Press), a bagel peg board (Bagel Barn).
    /// </summary>
    public static partial class MallBuilder
    {
        static void RestaurantDecor(Transform g, MallUnit u, float y, int side)
        {
            if (u.category != UnitCategory.Restaurant && u.category != UnitCategory.Cafe && u.category != UnitCategory.Food) return;
            WingId w = u.wing;
            Vector3 L(float du, float dv, float h) => MallLayout.WingPoint(w, u.u0 + du, side * dv, y + h);
            Vector3 fwd = MallLayout.WingForward(w), inShop = MallLayout.WingRight(w) * side;
            float len = u.u1 - u.u0;
            var rng = new System.Random(u.code.GetHashCode());
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Quaternion sideA = Quaternion.LookRotation(fwd), sideB = Quaternion.LookRotation(-fwd);
            // one mesh for the lot, in the shop's own frame: x across (v), y up, z along the mall (u)
            var kit = new MeshKit();
            kit.Frame(Vector3.zero, sideA);
            Quaternion inv = Quaternion.Inverse(sideA);
            void KBox(Vector3 at, Vector3 size, Material m, float yaw = 0f, bool shadows = true, float roll = 0f) => kit.Box(inv * at, size, m, yaw, shadows, roll);
            void KBar(Vector3 a, Vector3 b, float r, Material m, int sides) => kit.Bar(inv * a, inv * b, r, m, sides);
            void KDisc(Vector3 c, Vector3 n, float r, Material m, int sides) => kit.Disc(inv * c, inv * n, r, m, sides);
            void Hang(Vector3 at, float drop) => KBar(at + Vector3.up * drop, at, 0.006f, blackMat, 4);

            switch (u.name)
            {
                case "Golden Dragon":
                {
                    Material red = ArtLibrary.Plain("Lantern_Red", new Color(0.7f, 0.06f, 0.05f), 0.4f, 0f, new Color(0.5f, 0.05f, 0.02f), false);
                    Material gold = ArtLibrary.Plain("Lantern_Gold", new Color(0.8f, 0.6f, 0.15f), 0.7f, 0.9f);
                    for (float du = 1.2f; du < len - 0.8f; du += 1.6f)
                        foreach (float dv in new[] { 8.6f, 11.0f })
                        {
                            Vector3 c = L(du + R(-0.1f, 0.1f), dv, 3.4f);
                            Hang(c + Vector3.up * 0.3f, 1.0f);
                            Ball("Paper lantern", g, c, 0.42f, red);
                            KBox(c + Vector3.up * 0.22f, new Vector3(0.18f, 0.04f, 0.18f), gold);
                            KBox(c - Vector3.up * 0.22f, new Vector3(0.18f, 0.04f, 0.18f), gold);
                            KBar(c - Vector3.up * 0.24f, c - Vector3.up * 0.45f, 0.012f, red, 4);
                        }
                    Detail("Lucky cat", g, L(1.6f, 11.6f, 1.2f), new Vector3(0.14f, 0.2f, 0.12f), whiteFixture, Quaternion.LookRotation(-inShop));
                    Detail("Lucky cat paw (raised)", g, L(1.66f, 11.53f, 1.32f), new Vector3(0.04f, 0.08f, 0.04f), whiteFixture);
                    break;
                }
                case "Luigi's Trattoria":
                {
                    Material bottle = ArtLibrary.Plain("Wine_Bottle", new Color(0.08f, 0.18f, 0.08f), 0.9f);
                    Material straw = ArtLibrary.Plain("Chianti_Straw", new Color(0.75f, 0.62f, 0.35f), 0.15f);
                    // wine rack on the near side wall: diamond cells of bottle ends
                    KBox(L(0.3f, 9.6f, 1.6f), new Vector3(2.4f, 1.6f, 0.3f), woodMat);
                    for (int r = 0; r < 6; r++)
                        for (int c = 0; c < 9; c++)
                            if (rng.NextDouble() > 0.15)
                                KDisc(L(0.46f, 8.6f + c * 0.25f, 0.95f + r * 0.25f), fwd, 0.035f, bottle, 8);
                    // straw-covered bottles hung along a beam across the shop
                    Vector3 a = L(0.6f, 10.2f, 3.6f), b = L(len - 0.6f, 10.2f, 3.6f);
                    KBox((a + b) * 0.5f, new Vector3(0.12f, 0.12f, len - 1.2f), woodMat);
                    for (float t = 0.08f; t < 0.95f; t += 0.12f)
                    {
                        Vector3 p = Vector3.Lerp(a, b, t) - Vector3.up * 0.45f;
                        Hang(p + Vector3.up * 0.2f, 0.25f);
                        KBar(p - Vector3.up * 0.12f, p + Vector3.up * 0.06f, 0.07f, straw, 8);
                        KBar(p + Vector3.up * 0.06f, p + Vector3.up * 0.2f, 0.018f, bottle, 6);
                    }
                    break;
                }
                case "Spice Route Curry":
                {
                    Material brass = ArtLibrary.Plain("Lamp_Brass", new Color(0.7f, 0.5f, 0.2f), 0.75f, 1f);
                    Material glow = ArtLibrary.Plain("Lamp_Warm_Glow", new Color(0.8f, 0.5f, 0.2f), 0.5f, 0f, new Color(1.4f, 0.7f, 0.25f), false);
                    for (float du = 1.4f; du < len - 0.8f; du += 2.0f)
                    {
                        Vector3 c = L(du, 9.8f, 3.3f);
                        Hang(c + Vector3.up * 0.3f, 1.1f);
                        Ball("Brass lamp", g, c, 0.36f, brass);
                        Ball("Lamp glow", g, c - Vector3.up * 0.12f, 0.14f, glow);
                        KBar(c + Vector3.up * 0.18f, c + Vector3.up * 0.32f, 0.02f, brass, 6);
                    }
                    Detail("Tapestry", g, L(len - 0.29f, 9.6f, 2.4f), new Vector3(2.2f, 1.4f, 0.02f), ArtLibrary.Plain("Tapestry", new Color(0.55f, 0.15f, 0.2f), 0.2f), sideB);
                    break;
                }
                case "Sizzle Grill":
                {
                    Detail("Grill hood", g, L(len * 0.5f, 13.2f, 2.8f), new Vector3(Mathf.Min(3f, len - 3.6f), 0.7f, 0.6f), metal, Quaternion.LookRotation(-inShop));
                    Detail("Flame paint", g, L(len * 0.5f, 12.89f, 2.7f), new Vector3(Mathf.Min(2.6f, len - 4f), 0.35f, 0.01f), ArtLibrary.Plain("Flame_Paint", new Color(0.9f, 0.35f, 0.05f), 0.5f), Quaternion.LookRotation(-inShop));
                    Sign(g, "FLAME GRILLED", L(len * 0.5f, 12.88f, 2.95f), -inShop, 2f, 0.03f, false);
                    break;
                }
                case "The Tea Room":
                {
                    Color[] flags = { new Color(0.85f, 0.4f, 0.5f), new Color(0.5f, 0.7f, 0.85f), new Color(0.95f, 0.9f, 0.6f), new Color(0.6f, 0.8f, 0.55f) };
                    Vector3 a = L(0.3f, 7.0f, 3.2f), b = L(len - 0.3f, 12.8f, 3.2f);
                    for (float t = 0.04f; t < 0.97f; t += 0.05f)
                    {
                        Vector3 p = Vector3.Lerp(a, b, t) - Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.4f);
                        int fi = Mathf.RoundToInt(t * 20f) % flags.Length;
                        KBox(p - Vector3.up * 0.08f, new Vector3(0.12f, 0.14f, 0.004f), ArtLibrary.Plain($"Bunting_{fi}", flags[fi], 0.2f), Vector3.SignedAngle(Vector3.forward, inv * (b - a), Vector3.up) + 90f, false);
                    }
                    KBox(L(0.3f, 9.6f, 1.9f), new Vector3(2.2f, 0.03f, 0.3f), woodMat);
                    for (int k = 0; k < 6; k++)
                    {
                        Vector3 tp = L(0.36f, 8.7f + k * 0.36f, 2.0f);
                        Ball("Teapot", g, tp, 0.18f, ArtLibrary.Plain($"Teapot_{k % 3}", flags[k % flags.Length] * 0.9f, 0.85f));
                        KBar(tp + fwd * 0.08f, tp + fwd * 0.16f + Vector3.up * 0.05f, 0.012f, whiteFixture, 5);
                    }
                    break;
                }
                case "Daily Grind Coffee":
                case "Bean There Cafe":
                {
                    Material sack = ArtLibrary.Plain("Coffee_Sack", new Color(0.55f, 0.42f, 0.28f), 0.1f);
                    for (int k = 0; k < 4; k++)
                        KBox(L(len - 0.6f, 8.3f + k * 0.55f, 0.3f + (k % 2) * 0.05f), new Vector3(0.6f, 0.6f, 0.45f), sack, R(-10f, 10f), true, R(-4f, 4f));
                    Detail("Chalkboard", g, L(0.29f, 9.6f, 2.0f), new Vector3(1.6f, 1.0f, 0.03f), ArtLibrary.Plain("Chalkboard", new Color(0.08f, 0.1f, 0.09f), 0.2f), sideA);
                    Sign(g, "TODAY'S BLEND\nKENYAN AA\n\nFLAT WHITE  2.60\nLATTE  2.80\nHOT CHOC  2.40", L(0.31f, 9.6f, 2.0f), fwd, 1.4f, 0.016f, false);
                    break;
                }
                case "Pancake Parlour":
                {
                    Material pancake = ArtLibrary.Plain("Pancake", new Color(0.82f, 0.6f, 0.3f), 0.35f);
                    Material syrup = ArtLibrary.Plain("Syrup", new Color(0.45f, 0.2f, 0.05f), 0.95f);
                    Vector3 c = L(len * 0.5f, 6.35f, 3.4f);
                    for (int k = 0; k < 5; k++) KDisc(c + Vector3.up * (k * 0.12f - 0.24f) - inShop * 0.01f, -inShop, 0.5f + k % 2 * 0.03f, pancake, 16);
                    KDisc(c - inShop * 0.015f + Vector3.up * 0.2f, -inShop, 0.35f, syrup, 12);
                    break;
                }
                case "Fresh Press Juice":
                {
                    Color[] fruit = { new Color(0.9f, 0.5f, 0.05f), new Color(0.45f, 0.7f, 0.15f), new Color(0.8f, 0.1f, 0.1f), new Color(0.95f, 0.85f, 0.2f) };
                    for (int k = 0; k < 3; k++)
                    {
                        Vector3 cr = L(len - 0.55f, 8.3f + k * 0.8f, 0.2f);
                        KBox(cr, new Vector3(0.6f, 0.4f, 0.45f), woodMat);
                        Material fm = ArtLibrary.Plain($"Crate_Fruit_{k}", fruit[k % fruit.Length], 0.5f);
                        for (int f = 0; f < 9; f++) Ball("Fruit", g, cr + new Vector3(R(-0.2f, 0.2f), 0.22f + R(0f, 0.04f), R(-0.15f, 0.15f)), 0.09f, fm);
                    }
                    break;
                }
                case "Bagel Barn":
                {
                    Material bagel = ArtLibrary.Plain("Bagel", new Color(0.75f, 0.5f, 0.25f), 0.3f);
                    for (int r = 0; r < 3; r++)
                        for (int c = 0; c < 5; c++)
                        {
                            Vector3 p = L(0.36f, 8.6f + c * 0.45f, 1.5f + r * 0.35f);
                            KBar(p - fwd * 0.06f, p + fwd * 0.06f, 0.008f, metal, 4);
                            if (rng.NextDouble() < 0.6) KDisc(p + fwd * 0.03f, fwd, 0.08f, bagel, 10);
                        }
                    break;
                }
            }
            kit.Build(g, "Restaurant decor");
        }
    }
}
