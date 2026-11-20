using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Sushi Go: a conveyor counter running round an island, plates still sat on the belt under their domes,
    /// stools along it. Scoops Ice Cream: a curved gelato cabinet glowing on the counter line, its tubs gone soft,
    /// a cone dispenser, and a few little round tables.
    /// </summary>
    public static partial class MallBuilder
    {
        static bool SpecialShop3(Transform g, MallUnit u, float y, int side)
        {
            switch (u.name)
            {
                case "Sushi Go": SushiBar(g, u, y, side); return true;
                case "Scoops Ice Cream": IceCreamParlour(g, u, y, side); return true;
            }
            return false;
        }

        static void SushiBar(Transform g, MallUnit u, float y, int side)
        {
            WingId w = u.wing;
            Vector3 L(float du, float dv, float h) => MallLayout.WingPoint(w, u.u0 + du, side * dv, y + h);
            Vector3 fwd = MallLayout.WingForward(w), inShop = MallLayout.WingRight(w) * side;
            float len = u.u1 - u.u0;
            var rng = new System.Random(u.code.GetHashCode());
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material counter = ArtLibrary.Plain("Sushi_Counter", new Color(0.55f, 0.4f, 0.25f), 0.5f);
            Material belt = ArtLibrary.Plain("Sushi_Belt", new Color(0.7f, 0.72f, 0.74f), 0.8f, 0.9f);
            Material[] plates = { ArtLibrary.Plain("Plate_Red", new Color(0.7f, 0.1f, 0.08f), 0.7f), ArtLibrary.Plain("Plate_Blue", new Color(0.15f, 0.3f, 0.7f), 0.7f), ArtLibrary.Plain("Plate_Green", new Color(0.15f, 0.5f, 0.2f), 0.7f), ArtLibrary.Plain("Plate_Gold", new Color(0.8f, 0.6f, 0.15f), 0.7f) };
            Material rice = ArtLibrary.Plain("Sushi_Rice", new Color(0.92f, 0.9f, 0.85f), 0.3f);
            Material salmon = ArtLibrary.Plain("Sushi_Salmon", new Color(0.95f, 0.5f, 0.35f), 0.6f);
            Material dome = ArtLibrary.Plain("Plate_Dome", new Color(0.8f, 0.85f, 0.9f), 0.95f);
            float a0 = 1.2f, a1 = len - 1.2f, v0 = 8.7f, v1 = 10.5f;
            Quaternion rot = Quaternion.LookRotation(fwd);
            Vector3 c = L((a0 + a1) * 0.5f, (v0 + v1) * 0.5f, 0f);
            float cl = a1 - a0, cw = v1 - v0;
            EditorBuild.Box("Conveyor island", g, c + Vector3.up * 0.45f, new Vector3(cw, 0.9f, cl), counter, SurfaceType.Wood, rot);
            Detail("Conveyor well", g, c + Vector3.up * 0.905f, new Vector3(cw - 0.8f, 0.01f, cl - 0.8f), blackMat, rot);
            // the belt: a band round the island top
            foreach (float s in new[] { -1f, 1f })
            {
                Detail("Belt", g, c + Vector3.up * 0.915f + inShop * s * (cw * 0.5f - 0.2f), new Vector3(0.22f, 0.012f, cl - 0.2f), belt, rot);
                Detail("Belt", g, c + Vector3.up * 0.915f + fwd * s * (cl * 0.5f - 0.2f), new Vector3(cw - 0.2f, 0.012f, 0.22f), belt, rot);
            }
            // plates on the belt
            for (float t = -cl * 0.5f + 0.4f; t < cl * 0.5f - 0.3f; t += 0.45f)
                foreach (float s in new[] { -1f, 1f })
                {
                    if (rng.NextDouble() < 0.35) continue;
                    Vector3 p = c + Vector3.up * 0.925f + inShop * s * (cw * 0.5f - 0.2f) + fwd * t;
                    Cyl("Sushi plate", g, p, p + Vector3.up * 0.015f, 0.17f, plates[rng.Next(plates.Length)]);
                    Detail("Nigiri", g, p + Vector3.up * 0.03f, new Vector3(0.05f, 0.03f, 0.08f), rice, rot);
                    Detail("Nigiri topping", g, p + Vector3.up * 0.05f, new Vector3(0.055f, 0.012f, 0.085f), salmon, rot);
                    if (rng.NextDouble() < 0.5) Ball("Plate dome", g, p + Vector3.up * 0.03f, 0.16f, dome);
                }
            // stools on the front side and the ends
            for (float t = -cl * 0.5f + 0.4f; t < cl * 0.5f - 0.2f; t += 0.6f)
            {
                Vector3 sp = c - inShop * (cw * 0.5f + 0.35f) + fwd * t;
                Cyl("Stool post", g, sp, sp + Vector3.up * 0.6f, 0.05f, metal, true);
                Cyl("Stool seat", g, sp + Vector3.up * 0.6f, sp + Vector3.up * 0.66f, 0.36f, blackMat);
            }
            // soy sauce, ginger, chopsticks in their pots at intervals
            for (float t = -cl * 0.5f + 0.7f; t < cl * 0.5f - 0.5f; t += 1.2f)
            {
                Vector3 p = c + Vector3.up * 0.905f - inShop * (cw * 0.5f - 0.05f) + fwd * t;
                Cyl("Soy bottle", g, p, p + Vector3.up * 0.12f, 0.05f, ArtLibrary.Plain("Soy", new Color(0.12f, 0.06f, 0.03f), 0.9f));
                Cyl("Chopstick pot", g, p + fwd * 0.1f, p + fwd * 0.1f + Vector3.up * 0.1f, 0.07f, blackMat);
                Detail("Order tablet", g, p - fwd * 0.12f + Vector3.up * 0.08f, new Vector3(0.18f, 0.12f, 0.02f), blackMat, Quaternion.LookRotation(-inShop) * Quaternion.Euler(-20f, 0f, 0f));
            }
            Sign(g, "PLATES FROM £1.90\nRED £2.90  BLUE £3.50  GOLD £4.90", L(1.0f, 6.8f, 1.8f), -inShop, 1.2f, 0.018f);
        }

        static void IceCreamParlour(Transform g, MallUnit u, float y, int side)
        {
            WingId w = u.wing;
            Vector3 L(float du, float dv, float h) => MallLayout.WingPoint(w, u.u0 + du, side * dv, y + h);
            Vector3 fwd = MallLayout.WingForward(w), inShop = MallLayout.WingRight(w) * side;
            float len = u.u1 - u.u0;
            var rng = new System.Random(u.code.GetHashCode());
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material cabinet = ArtLibrary.Plain("Gelato_Cabinet", new Color(0.85f, 0.85f, 0.85f), 0.6f);
            Material glow = ArtLibrary.Plain("Gelato_Glass", new Color(0.6f, 0.7f, 0.75f), 0.95f, 0f, new Color(0.45f, 0.55f, 0.6f), false);
            Color[] flavours = { new Color(0.95f, 0.9f, 0.75f), new Color(0.45f, 0.25f, 0.12f), new Color(0.95f, 0.55f, 0.65f), new Color(0.6f, 0.85f, 0.45f), new Color(0.9f, 0.75f, 0.2f), new Color(0.55f, 0.3f, 0.6f) };
            // gelato cabinet across the back, right of the till counter
            float a0 = Mathf.Min(3.8f, len - 3.6f), a1 = len - 3.4f;
            if (a1 - a0 > 1.0f)
            {
                Vector3 c = L((a0 + a1) * 0.5f, 11.6f, 0f);
                Quaternion face = Quaternion.LookRotation(-inShop);
                EditorBuild.Box("Gelato cabinet", g, c + Vector3.up * 0.5f, new Vector3(a1 - a0, 1.0f, 0.8f), cabinet, SurfaceType.Metal, face);
                Detail("Gelato glass", g, c + Vector3.up * 1.15f - inShop * 0.2f, new Vector3(a1 - a0 - 0.05f, 0.35f, 0.02f), glow, face * Quaternion.Euler(-35f, 0f, 0f));
                int n = Mathf.FloorToInt((a1 - a0 - 0.1f) / 0.3f);
                for (int k = 0; k < n; k++)
                {
                    Vector3 p = L(a0 + 0.2f + k * 0.3f, 11.6f, 1.0f);
                    Detail("Gelato tub", g, p, new Vector3(0.26f, 0.04f, 0.32f), metal, face);
                    Material m = ArtLibrary.Plain($"Gelato_{k % flavours.Length}", flavours[k % flavours.Length], 0.35f);
                    // soft, slumped mounds
                    Detail("Gelato", g, p + Vector3.up * 0.04f, new Vector3(0.24f, 0.06f, 0.3f), m, face * Quaternion.Euler(R(-4f, 4f), 0f, R(-4f, 4f)));
                    Detail("Scoop", g, p + Vector3.up * 0.08f + fwd * 0.05f, new Vector3(0.03f, 0.02f, 0.16f), metal, face * Quaternion.Euler(-30f, R(-30f, 30f), 0f));
                }
                Detail("Cone dispenser", g, L(a1 + 0.2f, 11.6f, 1.2f), new Vector3(0.15f, 0.4f, 0.15f), glow, face);
            }
            // little round tables and chairs by the window
            for (int k = 0; k < Mathf.Min(3, Mathf.FloorToInt(len / 2.6f)); k++)
            {
                Vector3 t = L(1.4f + k * 2.4f, 9.3f, 0f);
                Cyl("Cafe table post", g, t, t + Vector3.up * 0.72f, 0.06f, metal, true);
                Cyl("Cafe table top", g, t + Vector3.up * 0.72f, t + Vector3.up * 0.74f, 0.65f, ArtLibrary.Plain("Table_Pastel", new Color(0.9f, 0.7f, 0.75f), 0.5f));
                ChairProp(g, t + fwd * 0.55f, Vector3.SignedAngle(Vector3.forward, -fwd, Vector3.up), false);
                ChairProp(g, t - fwd * 0.55f, Vector3.SignedAngle(Vector3.forward, fwd, Vector3.up), false);
                if (k == 1)
                {
                    // a cup of something melted, tipped over
                    Cyl("Paper cup (tipped)", g, t + Vector3.up * 0.78f, t + Vector3.up * 0.78f + fwd * 0.1f, 0.08f, whiteFixture);
                    Detail("Melted puddle", g, t + Vector3.up * 0.742f + fwd * 0.16f, new Vector3(0.18f, 0.004f, 0.12f), ArtLibrary.Plain("Gelato_1", flavours[1], 0.35f));
                }
            }
            Sign(g, "1 SCOOP £2.50\n2 SCOOPS £3.90\nWAFFLE CONE +50p", L(1.0f, 6.8f, 1.8f), -inShop, 1.0f, 0.02f);
        }
    }
}
