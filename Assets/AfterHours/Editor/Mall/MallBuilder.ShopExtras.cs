using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Small themed touches on top of the usual shelving, kept to the ceiling, the walls and the front corner:
    /// Party Planet's helium balloons bobbing at the ceiling (and three that have sunk to the floor), Turntable
    /// Music's vinyl crates and listening post, Rewind Video's NEW RELEASES wall, a cot mobile in Little Ones.
    /// </summary>
    public static partial class MallBuilder
    {
        static void ShopExtras(Transform g, MallUnit u, float y, int side)
        {
            WingId w = u.wing;
            Vector3 L(float du, float dv, float h) => MallLayout.WingPoint(w, u.u0 + du, side * dv, y + h);
            Vector3 fwd = MallLayout.WingForward(w), inShop = MallLayout.WingRight(w) * side;
            float len = u.u1 - u.u0;
            var rng = new System.Random(u.code.GetHashCode() ^ 0x5a5a);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Quaternion sideA = Quaternion.LookRotation(fwd);
            var kit = new MeshKit();
            kit.Frame(Vector3.zero, sideA);
            Quaternion inv = Quaternion.Inverse(sideA);
            void KBox(Vector3 at, Vector3 size, Material m, float yaw = 0f, bool shadows = true, float roll = 0f) => kit.Box(inv * at, size, m, yaw, shadows, roll);
            void KBar(Vector3 a, Vector3 b, float r, Material m, int sides) => kit.Bar(inv * a, inv * b, r, m, sides);

            switch (u.name)
            {
                case "Party Planet":
                {
                    Color[] cols = { new Color(0.85f, 0.1f, 0.15f), new Color(0.15f, 0.4f, 0.85f), new Color(0.95f, 0.8f, 0.15f), new Color(0.2f, 0.7f, 0.3f), new Color(0.75f, 0.3f, 0.75f), new Color(0.9f, 0.9f, 0.92f) };
                    Material ribbon = ArtLibrary.Plain("Balloon_Ribbon", new Color(0.85f, 0.85f, 0.85f), 0.6f);
                    for (int k = 0; k < 28; k++)
                    {
                        Vector3 b = L(R(0.6f, len - 0.6f), R(7.2f, 12.8f), MallLayout.CeilingHeight - 0.25f - R(0f, 0.15f));
                        Ball("Helium balloon", g, b, 0.3f, ArtLibrary.Plain($"Balloon_{k % cols.Length}", cols[k % cols.Length], 0.9f));
                        KBar(b - Vector3.up * 0.15f, b - Vector3.up * R(1.2f, 2.2f) + new Vector3(R(-0.1f, 0.1f), 0f, R(-0.1f, 0.1f)), 0.003f, ribbon, 3);
                    }
                    // three gone soft and sunk, ribbons trailing on the floor
                    for (int k = 0; k < 3; k++)
                    {
                        Vector3 b = L(R(1f, len - 1f), R(8f, 10.5f), 0.12f);
                        Ball("Sunk balloon", g, b, 0.24f, ArtLibrary.Plain($"Balloon_{k % cols.Length}", cols[k % cols.Length], 0.9f));
                        KBox(b + new Vector3(0f, -0.115f, 0f) + fwd * 0.4f, new Vector3(0.006f, 0.003f, 0.8f), ribbon, R(-40f, 40f), false);
                    }
                    break;
                }
                case "Turntable Music":
                {
                    Material crate = ArtLibrary.Plain("Vinyl_Crate", new Color(0.3f, 0.2f, 0.12f), 0.3f);
                    for (int c = 0; c < 2; c++)
                    {
                        Vector3 cc = L(len - 0.7f, 6.6f + c * 0.7f, 0.45f);
                        KBox(cc, new Vector3(0.6f, 0.9f, 0.45f), crate);
                        for (int s = 0; s < 18; s++)
                        {
                            if (rng.NextDouble() < 0.1) continue;
                            float tilt = s < 6 ? -14f : 0f;
                            KBox(cc + Vector3.up * 0.55f + inShop * (-0.25f + s * 0.028f), new Vector3(0.008f, 0.31f, 0.31f), ArtLibrary.Plain($"Sleeve_{(c * 18 + s) % 7}", Color.HSVToRGB(((c * 18 + s) % 7) / 7f, 0.5f, 0.6f), 0.4f), 0f, false, tilt);
                        }
                    }
                    // listening post on the side wall: headphones on a hook, a little turntable
                    KBox(L(len - 0.32f, 7.95f, 1.2f), new Vector3(0.5f, 0.06f, 0.4f), woodMat);
                    Cyl("Platter", g, L(len - 0.38f, 7.95f, 1.235f), L(len - 0.38f, 7.95f, 1.25f), 0.3f, blackMat);
                    Detail("Headphones", g, L(len - 0.3f, 7.6f, 1.55f), new Vector3(0.18f, 0.18f, 0.06f), blackMat, sideA);
                    break;
                }
                case "Rewind Video":
                {
                    Detail("New releases header", g, L(0.29f, 9.6f, 3.3f), new Vector3(2.4f, 0.3f, 0.02f), redMat, sideA);
                    Sign(g, "NEW RELEASES", L(0.305f, 9.6f, 3.3f), fwd, 2.2f, 0.035f, false);
                    for (int k = 0; k < 4; k++)
                    {
                        Vector3 p = L(0.3f, 8.7f + k * 0.6f, 2.65f);
                        Detail("Film poster", g, p, new Vector3(0.5f, 0.75f, 0.01f), ArtLibrary.Plain($"VideoPoster_{k}", Color.HSVToRGB(k * 0.27f % 1f, 0.6f, 0.35f), 0.4f), sideA);
                    }
                    Sign(g, "BE KIND, REWIND", L(1.4f, 6.8f, 1.8f), -inShop, 1.0f, 0.02f);
                    Detail("Late returns box", g, L(1.2f, 7.0f, 0.4f), new Vector3(0.45f, 0.8f, 0.4f), redMat, Quaternion.LookRotation(-inShop));
                    break;
                }
                case "Little Ones":
                {
                    Vector3 m = L(len * 0.5f, 9.8f, 3.4f);
                    KBar(m + Vector3.up * 0.9f, m, 0.006f, blackMat, 4);
                    KBox(m, new Vector3(0.6f, 0.02f, 0.02f), whiteFixture);
                    KBox(m, new Vector3(0.02f, 0.02f, 0.6f), whiteFixture);
                    Color[] pastel = { new Color(0.9f, 0.75f, 0.8f), new Color(0.7f, 0.85f, 0.95f), new Color(0.95f, 0.9f, 0.65f), new Color(0.75f, 0.9f, 0.75f) };
                    for (int k = 0; k < 4; k++)
                    {
                        Vector3 arm = Quaternion.Euler(0f, k * 90f, 0f) * Vector3.forward * 0.3f;
                        KBar(m + arm, m + arm - Vector3.up * 0.3f, 0.003f, blackMat, 3);
                        Ball(k % 2 == 0 ? "Mobile star" : "Mobile moon", g, m + arm - Vector3.up * 0.35f, 0.12f, ArtLibrary.Plain($"Pastel_{k}", pastel[k], 0.4f));
                    }
                    break;
                }
                default:
                    return;
            }
            kit.Build(g, "Shop extras");
        }
    }
}
