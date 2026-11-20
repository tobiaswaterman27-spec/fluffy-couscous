using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Optika Opticians (a wall of frames on pegs, try-on mirrors, the eye chart lit at the end of the room and the
    /// test chair) and Snap Photo Lab (the print minilab humming with its screen on, prints hung to dry, an
    /// envelope rack of uncollected photos).
    /// </summary>
    public static partial class MallBuilder
    {
        static bool SpecialShop5(Transform g, MallUnit u, float y, int side)
        {
            switch (u.name)
            {
                case "Optika Opticians": Opticians(g, u, y, side); return true;
                case "Snap Photo Lab": PhotoLab(g, u, y, side); return true;
            }
            return SpecialShop6(g, u, y, side);
        }

        static void Opticians(Transform g, MallUnit u, float y, int side)
        {
            WingId w = u.wing;
            Vector3 L(float du, float dv, float h) => MallLayout.WingPoint(w, u.u0 + du, side * dv, y + h);
            Vector3 fwd = MallLayout.WingForward(w), inShop = MallLayout.WingRight(w) * side;
            float len = u.u1 - u.u0;
            var rng = new System.Random(u.code.GetHashCode());
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material chartGlow = ArtLibrary.Plain("Eye_Chart", new Color(0.9f, 0.9f, 0.88f), 0.4f, 0f, new Color(0.9f, 0.9f, 0.85f), false);
            Material frameMat = ArtLibrary.Plain("Frames_Panel", new Color(0.92f, 0.92f, 0.9f), 0.5f);
            Material[] frames = { blackMat, ArtLibrary.Plain("Frame_Tortoise", new Color(0.35f, 0.2f, 0.08f), 0.7f), ArtLibrary.Plain("Frame_Red", new Color(0.6f, 0.08f, 0.06f), 0.7f), metal };
            // frames walls down both sides
            foreach (float du in new[] { 0.28f, len - 0.28f })
            {
                Vector3 n = du < 1f ? fwd : -fwd;
                Quaternion q = Quaternion.LookRotation(n);
                Detail("Frames panel", g, L(du, 9.4f, 1.5f), new Vector3(2.6f, 1.6f, 0.02f), frameMat, q);
                for (int r = 0; r < 7; r++)
                for (int c = 0; c < 9; c++)
                {
                    if (rng.NextDouble() < 0.12) continue;
                    Vector3 p = L(du, 8.3f + c * 0.28f, 0.85f + r * 0.21f) + n * 0.04f;
                    Material m = frames[rng.Next(frames.Length)];
                    Detail("Lens L", g, p - Vector3.Cross(Vector3.up, n) * 0.035f, new Vector3(0.05f, 0.035f, 0.004f), m, q);
                    Detail("Lens R", g, p + Vector3.Cross(Vector3.up, n) * 0.035f, new Vector3(0.05f, 0.035f, 0.004f), m, q);
                    Detail("Bridge", g, p + Vector3.up * 0.008f, new Vector3(0.02f, 0.006f, 0.004f), m, q);
                }
                Detail("Try-on mirror", g, L(du, 11.0f, 1.5f) + n * 0.02f, new Vector3(0.35f, 0.5f, 0.01f), mirrorMat, q);
            }
            // eye-test: the chart lit on the back partition, the chair facing it, a phoropter on its arm
            float tdu = Mathf.Clamp(len * 0.5f, 3.9f, len - 3.6f);
            Vector3 chart = L(tdu, 13.47f, 1.5f);
            Detail("Eye chart", g, chart, new Vector3(0.45f, 0.65f, 0.02f), chartGlow, Quaternion.LookRotation(-inShop));
            Sign(g, "E\n\nF P\n\nT O Z\n\nL P E D\n\nP E C F D", chart - inShop * 0.012f, -inShop, 0.35f, 0.012f, false);
            Vector3 ch = L(tdu, 9.4f, 0f);
            EditorBuild.Box("Test chair", g, ch + Vector3.up * 0.3f, new Vector3(0.6f, 0.6f, 0.6f), blackMat, SurfaceType.Carpet, Quaternion.LookRotation(inShop));
            Detail("Test chair back", g, ch + Vector3.up * 0.85f - inShop * 0.28f, new Vector3(0.6f, 0.6f, 0.08f), blackMat, Quaternion.LookRotation(inShop));
            Cyl("Phoropter arm", g, ch + Vector3.up * 1.4f - inShop * 0.4f + fwd * 0.6f, ch + Vector3.up * 1.25f + inShop * 0.15f, 0.04f, whiteFixture);
            Detail("Phoropter", g, ch + Vector3.up * 1.22f + inShop * 0.2f, new Vector3(0.36f, 0.16f, 0.1f), blackMat, Quaternion.LookRotation(inShop));
            Cyl("Phoropter column", g, ch - inShop * 0.4f + fwd * 0.6f, ch + Vector3.up * 1.4f - inShop * 0.4f + fwd * 0.6f, 0.06f, whiteFixture, true);
            Sign(g, "EYE TESTS FREE\nWITH ANY FRAMES", L(1.2f, 6.8f, 1.8f), -inShop, 1.0f, 0.02f);
        }

        static void PhotoLab(Transform g, MallUnit u, float y, int side)
        {
            WingId w = u.wing;
            Vector3 L(float du, float dv, float h) => MallLayout.WingPoint(w, u.u0 + du, side * dv, y + h);
            Vector3 fwd = MallLayout.WingForward(w), inShop = MallLayout.WingRight(w) * side;
            float len = u.u1 - u.u0;
            var rng = new System.Random(u.code.GetHashCode());
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material minilab = ArtLibrary.Plain("Minilab", new Color(0.85f, 0.85f, 0.83f), 0.5f);
            Material screenOn = ArtLibrary.Plain("Minilab_Screen", new Color(0.05f, 0.1f, 0.15f), 0.9f, 0f, new Color(0.2f, 0.5f, 0.6f), false);
            Material print = ArtLibrary.Plain("Photo_Print", new Color(0.7f, 0.65f, 0.6f), 0.85f);
            Material envelope = ArtLibrary.Plain("Photo_Envelope", new Color(0.85f, 0.75f, 0.35f), 0.3f);
            // the minilab along the far wall
            Quaternion toBack = Quaternion.LookRotation(-fwd);
            Vector3 m = L(len - 0.7f, 9.6f, 0f);
            EditorBuild.Box("Minilab", g, m + Vector3.up * 0.65f, new Vector3(2.4f, 1.3f, 0.9f), minilab, SurfaceType.Metal, toBack);
            Detail("Minilab screen", g, m + Vector3.up * 1.15f - fwd * 0.46f + inShop * 0.6f, new Vector3(0.35f, 0.25f, 0.01f), screenOn, toBack);
            Sign(g, "PRINTING 37 / 48", m + Vector3.up * 1.15f - fwd * 0.466f + inShop * 0.6f, -fwd, 0.3f, 0.009f, false);
            Detail("Print tray", g, m + Vector3.up * 0.8f - fwd * 0.55f - inShop * 0.6f, new Vector3(0.4f, 0.04f, 0.25f), blackMat, toBack);
            for (int k = 0; k < 6; k++)
                Detail("Print (in tray)", g, m + Vector3.up * (0.83f + k * 0.004f) - fwd * 0.55f - inShop * 0.6f, new Vector3(0.15f, 0.003f, 0.1f), print, toBack * Quaternion.Euler(0f, R(-8f, 8f), 0f));
            // prints hung to dry on a line across the shop
            Vector3 la = L(1.0f, 10.6f, 2.2f), lb = L(len - 1.4f, 10.6f, 2.2f);
            Cyl("Drying line", g, la, lb, 0.006f, whiteFixture);
            for (float t = 0.08f; t < 0.95f; t += 0.07f)
            {
                Vector3 p = Vector3.Lerp(la, lb, t) - Vector3.up * 0.09f;
                Detail("Hung print", g, p, new Vector3(0.1f, 0.15f, 0.003f), print, Quaternion.LookRotation(inShop) * Quaternion.Euler(0f, 0f, R(-5f, 5f)));
                Detail("Peg", g, p + Vector3.up * 0.08f, new Vector3(0.012f, 0.03f, 0.01f), woodMat, Quaternion.LookRotation(inShop));
            }
            // uncollected envelopes in a rack by the till
            Vector3 rk = L(0.5f, 9.4f, 0.9f);
            Detail("Envelope rack", g, rk, new Vector3(1.2f, 1.0f, 0.3f), woodMat, Quaternion.LookRotation(fwd));
            for (int r = 0; r < 4; r++)
            for (int k = 0; k < 8; k++)
                if (rng.NextDouble() > 0.2)
                    Detail("Photo envelope", g, L(0.5f + 0.12f, 8.9f + k * 0.13f, 0.55f + r * 0.24f), new Vector3(0.12f, 0.2f, 0.02f), envelope, Quaternion.LookRotation(fwd) * Quaternion.Euler(R(-6f, 6f), 0f, 0f));
            Sign(g, "1 HOUR PRINTING\nDIGITAL  -  FILM  -  PASSPORT", L(1.2f, 6.8f, 1.8f), -inShop, 1.1f, 0.02f);
        }
    }
}
