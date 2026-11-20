using System.Collections.Generic;
using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Shops that are not shelves of stock, fitted as what they are. Fortune Teller's Den: draped walls, a round
    /// table with a crystal ball, tarot cards dealt out, candles burnt down, a beaded curtain over the back door.
    /// Nails by Nina: manicure stations with lamps and a wall of polish. Snapshot Studio: a paper backdrop, a camera
    /// on a tripod, softboxes and a stool, sample portraits on the walls.
    /// </summary>
    public static partial class MallBuilder
    {
        /// <returns>true if this unit was fitted here (so the generic furnishing is skipped).</returns>
        static bool SpecialShop(Transform g, MallUnit u, float y, int side)
        {
            switch (u.name)
            {
                case "Fortune Teller's Den": FortuneTeller(g, u, y, side); return true;
                case "Nails by Nina": NailBar(g, u, y, side); return true;
                case "Snapshot Studio": PhotoStudio(g, u, y, side); return true;
            }
            return SpecialShop2(g, u, y, side);
        }

        static void FortuneTeller(Transform g, MallUnit u, float y, int side)
        {
            WingId w = u.wing;
            Vector3 L(float du, float dv, float h) => MallLayout.WingPoint(w, u.u0 + du, side * dv, y + h);
            Vector3 fwd = MallLayout.WingForward(w), inShop = MallLayout.WingRight(w) * side;
            float len = u.u1 - u.u0, mid = len * 0.5f;
            var rng = new System.Random(u.code.GetHashCode());
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material velvet = ArtLibrary.Plain("Velvet_Purple", new Color(0.22f, 0.05f, 0.25f), 0.15f);
            Material cloth = ArtLibrary.Plain("Table_Cloth_Black", new Color(0.04f, 0.03f, 0.05f), 0.2f);
            Material gold = ArtLibrary.Plain("Fringe_Gold", new Color(0.7f, 0.55f, 0.2f), 0.6f, 0.9f);
            Material crystal = ArtLibrary.Plain("Crystal_Ball", new Color(0.5f, 0.55f, 0.75f), 0.98f, 0f, new Color(0.25f, 0.3f, 0.6f), false);
            Material wax = ArtLibrary.Plain("Candle_Wax", new Color(0.88f, 0.84f, 0.72f), 0.4f);
            Material bead = ArtLibrary.Plain("Bead_Red", new Color(0.55f, 0.08f, 0.1f), 0.8f);

            // drapes hung down both side walls and the back
            for (float du = 0.4f; du < len - 3.0f; du += 0.5f)
            {
                Detail("Drape", g, L(du, 13.4f, 2.0f), new Vector3(0.5f, 4.0f, 0.04f), velvet, Quaternion.LookRotation(-inShop) * Quaternion.Euler(0f, 0f, 0f));
                Detail("Drape fold", g, L(du + 0.25f, 13.36f, 2.0f), new Vector3(0.06f, 4.0f, 0.06f), velvet);
            }
            foreach (float du in new[] { 0.25f, len - 0.25f })
                Detail("Side drape", g, L(du, 10f, 2.0f), new Vector3(6.8f, 4.0f, 0.04f), velvet, Quaternion.LookRotation(fwd));

            // round table with a long cloth, the ball on its stand, the spread
            Vector3 tc = L(Mathf.Clamp(mid + 0.8f, 2.5f, len - 1.6f), 9.6f, 0f);
            Cyl("Reading table", g, tc, tc + Vector3.up * 0.74f, 1.1f, cloth, true);
            Cyl("Table fringe", g, tc + Vector3.up * 0.66f, tc + Vector3.up * 0.7f, 1.12f, gold);
            Cyl("Ball stand", g, tc + Vector3.up * 0.74f, tc + Vector3.up * 0.82f, 0.14f, gold);
            Ball("Crystal ball", g, tc + Vector3.up * 0.93f, 0.22f, crystal);
            var lg = new GameObject("Crystal ball glow");
            lg.transform.SetParent(g, false);
            lg.transform.localPosition = tc + Vector3.up * 1.0f;
            Light l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.range = 2.5f; l.intensity = 0.5f; l.color = new Color(0.55f, 0.6f, 1f); l.shadows = LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            string[] cards = { "THE TOWER", "DEATH", "THE MOON", "THE HANGED MAN", "TEN OF SWORDS" };
            for (int k = 0; k < 5; k++)
            {
                Vector3 p = tc + Vector3.up * 0.745f - inShop * 0.3f + fwd * (-0.32f + k * 0.16f);
                Detail("Tarot card", g, p, new Vector3(0.07f, 0.003f, 0.12f), whiteFixture, Quaternion.LookRotation(-inShop) * Quaternion.Euler(0f, R(-8f, 8f), 0f));
            }
            Sign(g, string.Join("   ", cards), tc + Vector3.up * 0.75f - inShop * 0.42f, Vector3.up, 0.8f, 0.004f, false);
            // candles burnt to stubs, wax run down
            foreach (Vector3 off in new[] { inShop * 0.35f + fwd * 0.3f, inShop * 0.3f - fwd * 0.35f, -inShop * 0.1f + fwd * 0.45f })
            {
                float hh = R(0.03f, 0.1f);
                Cyl("Candle stub", g, tc + Vector3.up * 0.74f + off, tc + Vector3.up * (0.74f + hh) + off, 0.04f, wax);
                Detail("Wax pool", g, tc + Vector3.up * 0.742f + off, new Vector3(0.09f, 0.003f, 0.08f), wax, Quaternion.Euler(0f, R(0f, 90f), 0f));
            }
            // her chair (high-backed) and the customer's, pulled out
            Vector3 hc = tc + inShop * 0.85f;
            EditorBuild.Box("Reader's chair", g, hc + Vector3.up * 0.25f, new Vector3(0.5f, 0.5f, 0.5f), velvet, SurfaceType.Carpet, Quaternion.LookRotation(-inShop));
            Detail("Reader's chair back", g, hc + Vector3.up * 0.95f + inShop * 0.23f, new Vector3(0.55f, 1.0f, 0.06f), velvet, Quaternion.LookRotation(-inShop));
            ChairProp(g, tc - inShop * 0.95f, Vector3.SignedAngle(Vector3.forward, inShop, Vector3.up) + 25f, false);
            // a beaded curtain over the stock room doorway (doorway at u1-2.7 .. u1-1.6)
            for (int k = 0; k < 12; k++)
                Detail("Bead string", g, MallLayout.WingPoint(w, u.u1 - 2.65f + k * 0.09f, side * 13.52f, y + 1.15f), new Vector3(0.015f, 1.9f, 0.015f), bead);
            Sign(g, "MADAME ZORA SEES ALL\nPALMS  -  CARDS  -  CRYSTAL\n£15", L(1.2f, 7.0f, 1.8f), -inShop, 1.0f, 0.02f);
        }

        static void NailBar(Transform g, MallUnit u, float y, int side)
        {
            WingId w = u.wing;
            Vector3 L(float du, float dv, float h) => MallLayout.WingPoint(w, u.u0 + du, side * dv, y + h);
            Vector3 fwd = MallLayout.WingForward(w), inShop = MallLayout.WingRight(w) * side;
            float len = u.u1 - u.u0;
            var rng = new System.Random(u.code.GetHashCode());
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material pink = ArtLibrary.Plain("Nail_Pink", new Color(0.9f, 0.6f, 0.7f), 0.6f);
            Material white = whiteFixture;
            Material seat = ArtLibrary.Plain("Nail_Seat", new Color(0.85f, 0.85f, 0.88f), 0.5f);
            Quaternion faceFwd = Quaternion.LookRotation(fwd);
            // manicure stations along the middle, each with a lamp, a towel and a few bottles
            int n = Mathf.Max(1, Mathf.FloorToInt((len - 3.5f) / 1.6f));
            for (int k = 0; k < n; k++)
            {
                float du = 1.2f + k * 1.6f;
                Vector3 c = L(du, 9.8f, 0f);
                EditorBuild.Box("Manicure table", g, c + Vector3.up * 0.38f, new Vector3(1.1f, 0.76f, 0.5f), white, SurfaceType.Wood, Quaternion.LookRotation(inShop));
                Detail("Hand rest", g, c + Vector3.up * 0.79f, new Vector3(0.25f, 0.05f, 0.15f), pink, Quaternion.LookRotation(inShop));
                Cyl("Lamp arm", g, c + Vector3.up * 0.76f + fwd * 0.4f, c + Vector3.up * 1.15f + fwd * 0.25f, 0.025f, white);
                Detail("Lamp head", g, c + Vector3.up * 1.15f + fwd * 0.15f, new Vector3(0.12f, 0.06f, 0.22f), white, faceFwd);
                for (int b = 0; b < 4; b++)
                    Cyl("Polish", g, c + Vector3.up * 0.76f - fwd * (0.3f + b * 0.05f) + inShop * 0.15f, c + Vector3.up * 0.83f - fwd * (0.3f + b * 0.05f) + inShop * 0.15f, 0.03f, ArtLibrary.Plain($"Polish_{b}", Color.HSVToRGB((b * 0.23f + k * 0.11f) % 1f, 0.75f, 0.75f), 0.95f));
                ChairProp(g, c - inShop * 0.6f, Vector3.SignedAngle(Vector3.forward, inShop, Vector3.up), false);
                Detail("Tech stool", g, c + inShop * 0.6f + Vector3.up * 0.25f, new Vector3(0.38f, 0.5f, 0.38f), seat);
            }
            // polish wall on the back partition: rows of tiny bottles
            var kit = new MeshKit();
            Quaternion wallFace = Quaternion.LookRotation(-inShop);   // x runs along the back partition
            kit.Frame(Vector3.zero, wallFace);
            Quaternion inv = Quaternion.Inverse(wallFace);
            for (int r = 0; r < 6; r++)
            {
                float h = 0.9f + r * 0.22f;
                kit.Box(inv * L((0.7f + len - 3.1f) * 0.5f, 13.45f, h), new Vector3(len - 3.8f, 0.015f, 0.08f), white);
                for (float du = 0.9f; du < len - 3.3f; du += 0.06f)
                    if (rng.NextDouble() > 0.1)
                        kit.Box(inv * L(du, 13.45f, h + 0.042f), new Vector3(0.032f, 0.065f, 0.032f), ArtLibrary.Plain($"Polish_{(int)(du * 7) % 8}", Color.HSVToRGB(((int)(du * 7) % 8) / 8f, 0.7f, 0.8f), 0.95f), 0f, false);
            }
            kit.Build(g, "Polish wall");
            Sign(g, "WALK-INS WELCOME\nGEL  -  ACRYLIC  -  PEDI", L(1.4f, 6.8f, 1.7f), -inShop, 1.0f, 0.02f);
        }

        static void PhotoStudio(Transform g, MallUnit u, float y, int side)
        {
            WingId w = u.wing;
            Vector3 L(float du, float dv, float h) => MallLayout.WingPoint(w, u.u0 + du, side * dv, y + h);
            Vector3 fwd = MallLayout.WingForward(w), inShop = MallLayout.WingRight(w) * side;
            float len = u.u1 - u.u0;
            Material backdrop = ArtLibrary.Plain("Backdrop_Grey", new Color(0.55f, 0.56f, 0.58f), 0.2f);
            Material softbox = ArtLibrary.Plain("Softbox", new Color(0.9f, 0.9f, 0.9f), 0.3f);
            // paper backdrop rolled down the far side wall and out across the floor (a sweep)
            Quaternion toBack = Quaternion.LookRotation(-fwd);
            Cyl("Backdrop roll", g, L(len - 0.32f, 8.4f, 2.8f), L(len - 0.32f, 10.9f, 2.8f), 0.14f, backdrop);
            Detail("Backdrop (wall)", g, L(len - 0.28f, 9.65f, 1.4f), new Vector3(2.5f, 2.8f, 0.01f), backdrop, toBack);
            Detail("Backdrop (floor)", g, L(len - 1.25f, 9.65f, 0.004f), new Vector3(2.5f, 0.005f, 1.9f), backdrop, toBack);
            Detail("Posing stool", g, L(len - 1.3f, 9.65f, 0.3f), new Vector3(0.35f, 0.6f, 0.35f), blackMat);
            // tripod and camera facing the backdrop
            Vector3 tp = L(Mathf.Max(1.2f, len - 4.6f), 9.65f, 0f);
            for (int k = 0; k < 3; k++)
            {
                Quaternion q = Quaternion.Euler(0f, k * 120f, 0f);
                Cyl("Tripod leg", g, tp + q * Vector3.forward * 0.35f, tp + Vector3.up * 1.2f, 0.025f, blackMat);
            }
            Detail("Camera body", g, tp + Vector3.up * 1.32f, new Vector3(0.14f, 0.1f, 0.08f), blackMat, Quaternion.LookRotation(fwd));
            Cyl("Camera lens", g, tp + Vector3.up * 1.32f + fwd * 0.04f, tp + Vector3.up * 1.32f + fwd * 0.16f, 0.08f, blackMat);
            // two softboxes on stands either side of the sweep
            foreach (float s in new[] { 8.3f, 11.0f })
            {
                Vector3 b = L(len - 2.7f, s, 0f);
                Cyl("Light stand", g, b, b + Vector3.up * 1.8f, 0.03f, blackMat);
                for (int k = 0; k < 3; k++) Cyl("Stand leg", g, b + Quaternion.Euler(0f, k * 120f, 0f) * Vector3.forward * 0.3f, b + Vector3.up * 0.4f, 0.02f, blackMat);
                Vector3 aim = (L(len - 1.3f, 9.65f, 1.2f) - (b + Vector3.up * 1.8f)).normalized;
                Detail("Softbox", g, b + Vector3.up * 1.8f, new Vector3(0.7f, 0.7f, 0.35f), blackMat, Quaternion.LookRotation(aim));
                Detail("Softbox diffuser", g, b + Vector3.up * 1.8f + aim * 0.18f, new Vector3(0.66f, 0.66f, 0.01f), softbox, Quaternion.LookRotation(aim));
            }
            // sample portraits on the side wall
            for (int k = 0; k < 3; k++)
            {
                Vector3 p = L(0.27f, 7.6f + k * 1.3f, 1.6f);
                Detail("Portrait frame", g, p, new Vector3(0.55f, 0.7f, 0.03f), blackMat, Quaternion.LookRotation(fwd));
                Detail("Portrait", g, p + fwd * 0.018f, new Vector3(0.45f, 0.6f, 0.005f), ArtLibrary.Plain($"Portrait_{k}", new Color(0.35f + 0.1f * k, 0.3f + 0.05f * k, 0.28f), 0.4f), Quaternion.LookRotation(fwd));
            }
            Sign(g, "PASSPORT PHOTOS WHILE U WAIT\nFAMILY PORTRAITS  -  PETS", L(1.6f, 6.8f, 1.7f), -inShop, 1.2f, 0.02f);
        }
    }
}
