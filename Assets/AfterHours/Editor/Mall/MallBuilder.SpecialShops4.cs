using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Cuts Barbers (hydraulic chairs facing a mirror wall, a striped pole by the door, hair swept into a heap),
    /// Bloom Florist (buckets of flowers wilting in the dark, petals dropped, a half-wrapped bouquet), Northgate
    /// Building Society (counter positions behind glass screens, a queue barrier, leaflet stand) and Key &amp; Heel
    /// Repairs (key-cutting machine and a board of blanks, a shoe last and soles).
    /// </summary>
    public static partial class MallBuilder
    {
        static bool SpecialShop4(Transform g, MallUnit u, float y, int side)
        {
            switch (u.name)
            {
                case "Cuts Barbers": Barbers(g, u, y, side); return true;
                case "Bloom Florist": Florist(g, u, y, side); return true;
                case "Northgate Building Society": BuildingSociety(g, u, y, side); return true;
                case "Key & Heel Repairs": KeyCutter(g, u, y, side); return true;
            }
            return false;
        }

        static void Barbers(Transform g, MallUnit u, float y, int side)
        {
            WingId w = u.wing;
            Vector3 L(float du, float dv, float h) => MallLayout.WingPoint(w, u.u0 + du, side * dv, y + h);
            Vector3 fwd = MallLayout.WingForward(w), inShop = MallLayout.WingRight(w) * side;
            float len = u.u1 - u.u0;
            var rng = new System.Random(u.code.GetHashCode());
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material leather = ArtLibrary.Plain("Barber_Leather", new Color(0.35f, 0.08f, 0.06f), 0.55f);
            Material chrome = ArtLibrary.Plain("Barber_Chrome", new Color(0.75f, 0.76f, 0.78f), 0.85f, 1f);
            Material hair = ArtLibrary.Plain("Hair_Clippings", new Color(0.12f, 0.09f, 0.06f), 0.2f);
            Quaternion faceWall = Quaternion.LookRotation(-fwd);
            // mirror wall on the far side, chairs facing it
            Detail("Mirror wall", g, L(len - 0.27f, 9.6f, 1.45f), new Vector3(3.2f, 1.3f, 0.01f), mirrorMat, faceWall);
            Detail("Mirror shelf", g, L(len - 0.38f, 9.6f, 0.8f), new Vector3(3.2f, 0.03f, 0.2f), chrome, faceWall);
            for (int k = 0; k < 3; k++)
            {
                float dv = 8.6f + k * 1.0f;
                Vector3 c = L(len - 1.4f, dv, 0f);
                Cyl("Barber chair base", g, c, c + Vector3.up * 0.05f, 0.55f, chrome);
                Cyl("Barber chair column", g, c, c + Vector3.up * 0.45f, 0.14f, chrome, true);
                Detail("Barber chair seat", g, c + Vector3.up * 0.55f, new Vector3(0.55f, 0.14f, 0.55f), leather, faceWall);
                Detail("Barber chair back", g, c + Vector3.up * 0.95f - fwd * 0.26f, new Vector3(0.55f, 0.7f, 0.1f), leather, faceWall * Quaternion.Euler(10f, 0f, 0f));
                Detail("Headrest", g, c + Vector3.up * 1.38f - fwd * 0.3f, new Vector3(0.25f, 0.14f, 0.08f), leather, faceWall);
                Detail("Footrest", g, c + Vector3.up * 0.2f + fwd * 0.38f, new Vector3(0.4f, 0.03f, 0.18f), chrome, faceWall);
                foreach (float s in new[] { -0.3f, 0.3f })
                    Detail("Armrest", g, c + Vector3.up * 0.75f + inShop * s, new Vector3(0.06f, 0.06f, 0.45f), chrome, faceWall);
                // clippers and a spray bottle on the shelf
                Detail("Clippers", g, L(len - 0.38f, dv + 0.2f, 0.83f), new Vector3(0.05f, 0.03f, 0.15f), blackMat, faceWall * Quaternion.Euler(0f, R(-20f, 20f), 0f));
                Cyl("Water spray", g, L(len - 0.38f, dv - 0.2f, 0.82f), L(len - 0.38f, dv - 0.2f, 1.0f), 0.06f, ArtLibrary.Plain("Spray_Blue", new Color(0.15f, 0.35f, 0.75f), 0.6f));
                // clippings under each chair
                for (int h = 0; h < 5; h++)
                    Detail("Hair", g, c + new Vector3(R(-0.35f, 0.35f), 0.003f, R(-0.35f, 0.35f)), new Vector3(R(0.05f, 0.14f), 0.003f, R(0.04f, 0.1f)), hair, Quaternion.Euler(0f, R(0f, 180f), 0f));
            }
            // the swept heap and the broom by it
            Detail("Hair heap", g, L(len * 0.45f, 10.4f, 0.02f), new Vector3(0.4f, 0.04f, 0.3f), hair);
            Cyl("Broom handle", g, L(len * 0.45f + 0.35f, 10.5f, 0.05f), L(len * 0.45f + 0.5f, 10.9f, 1.3f), 0.03f, woodMat);
            // barber's pole by the door, outside face of the shopfront
            Vector3 pole = L(0.4f, 6.45f, 1.6f);
            Cyl("Pole cap", g, pole + Vector3.up * 0.35f, pole + Vector3.up * 0.4f, 0.16f, chrome);
            for (int k = 0; k < 7; k++)
                Cyl("Pole stripe", g, pole + Vector3.up * (-0.35f + k * 0.1f), pole + Vector3.up * (-0.25f + k * 0.1f), 0.13f, k % 3 == 0 ? redMat : k % 3 == 1 ? whiteFixture : ArtLibrary.Plain("Pole_Blue", new Color(0.1f, 0.2f, 0.6f), 0.6f));
            Cyl("Pole cap", g, pole - Vector3.up * 0.4f, pole - Vector3.up * 0.35f, 0.16f, chrome);
            // waiting bench along the near wall
            EditorBuild.Box("Waiting bench", g, L(0.5f, 9.2f, 0.22f), new Vector3(0.45f, 0.44f, 1.8f), leather, SurfaceType.Carpet, Quaternion.LookRotation(inShop));
            Detail("Magazines", g, L(0.5f, 9.8f, 0.46f), new Vector3(0.22f, 0.03f, 0.3f), whiteFixture, Quaternion.Euler(0f, 12f, 0f));
            Sign(g, "WALK-INS ONLY\nSKIN FADE £14\nBEARD TRIM £8", L(1.2f, 6.8f, 1.8f), -inShop, 1.0f, 0.02f);
        }

        static void Florist(Transform g, MallUnit u, float y, int side)
        {
            WingId w = u.wing;
            Vector3 L(float du, float dv, float h) => MallLayout.WingPoint(w, u.u0 + du, side * dv, y + h);
            Vector3 fwd = MallLayout.WingForward(w), inShop = MallLayout.WingRight(w) * side;
            float len = u.u1 - u.u0;
            var rng = new System.Random(u.code.GetHashCode());
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material bucket = ArtLibrary.Plain("Bucket_Zinc", new Color(0.55f, 0.57f, 0.6f), 0.6f, 0.8f);
            Material stem = ArtLibrary.Plain("Flower_Stem", new Color(0.18f, 0.3f, 0.12f), 0.3f);
            Color[] heads = { new Color(0.6f, 0.08f, 0.12f), new Color(0.9f, 0.85f, 0.4f), new Color(0.85f, 0.5f, 0.6f), new Color(0.9f, 0.9f, 0.88f), new Color(0.5f, 0.25f, 0.6f) };
            Material wrap = ArtLibrary.Plain("Bouquet_Paper", new Color(0.85f, 0.8f, 0.7f), 0.3f);
            // tiered stands of buckets down both sides
            foreach (float du in new[] { 0.7f, len - 0.7f })
            {
                for (int tier = 0; tier < 3; tier++)
                {
                    float h = tier * 0.35f, dvBase = 8.0f;
                    Detail("Flower stand tier", g, L(du, dvBase + 1.5f, h + 0.3f), new Vector3(0.55f, 0.04f, 3.2f), woodMat, Quaternion.LookRotation(inShop));
                    for (int k = 0; k < 5; k++)
                    {
                        Vector3 b = L(du + (du < 1f ? 1 : -1) * tier * 0.12f, dvBase + 0.2f + k * 0.62f, h + 0.32f);
                        Cyl("Flower bucket", g, b, b + Vector3.up * 0.28f, 0.26f, bucket);
                        Color col = heads[(k + tier * 2) % heads.Length];
                        Material head = ArtLibrary.Plain($"Flower_{(k + tier * 2) % heads.Length}", col, 0.3f);
                        Material dead = ArtLibrary.Plain($"Flower_Dead_{(k + tier * 2) % heads.Length}", Color.Lerp(col, new Color(0.35f, 0.25f, 0.15f), 0.6f), 0.2f);
                        for (int f = 0; f < 6; f++)
                        {
                            // stems lean outward, the heads droop; a share have died
                            Vector3 tip = b + Vector3.up * R(0.5f, 0.75f) + new Vector3(R(-0.15f, 0.15f), 0f, R(-0.15f, 0.15f));
                            Cyl("Stem", g, b + Vector3.up * 0.25f, tip, 0.012f, stem);
                            bool wilted = rng.NextDouble() < 0.45;
                            Ball(wilted ? "Wilted head" : "Flower head", g, tip + (wilted ? Vector3.down * 0.05f + fwd * 0.03f : Vector3.zero), wilted ? 0.05f : 0.075f, wilted ? dead : head);
                        }
                    }
                }
            }
            // petals dropped on the floor
            for (int k = 0; k < 24; k++)
                Detail("Petal", g, L(R(1.0f, len - 1.0f), R(7.6f, 11.0f), 0.003f), new Vector3(0.03f, 0.002f, 0.025f), ArtLibrary.Plain($"Flower_Dead_{k % heads.Length}", Color.Lerp(heads[k % heads.Length], new Color(0.35f, 0.25f, 0.15f), 0.6f), 0.2f), Quaternion.Euler(0f, R(0f, 180f), 0f));
            // a half-wrapped bouquet left on the work bench, ribbon and scissors
            Vector3 wb = L(len * 0.5f, 10.2f, 0f);
            EditorBuild.Box("Work bench", g, wb + Vector3.up * 0.45f, new Vector3(1.6f, 0.9f, 0.7f), woodMat, SurfaceType.Wood, Quaternion.LookRotation(fwd));
            Detail("Bouquet wrap", g, wb + Vector3.up * 0.92f, new Vector3(0.5f, 0.01f, 0.6f), wrap, Quaternion.Euler(0f, 30f, 0f));
            for (int f = 0; f < 7; f++)
            {
                Vector3 a = wb + Vector3.up * 0.95f + new Vector3(R(-0.08f, 0.08f), 0f, R(-0.08f, 0.08f));
                Vector3 bt = a + Quaternion.Euler(0f, 30f, 0f) * new Vector3(R(-0.06f, 0.06f), 0.02f, 0.45f);
                Cyl("Bouquet stem", g, a, bt, 0.012f, stem);
                Ball("Rose", g, bt, 0.07f, ArtLibrary.Plain("Flower_0", heads[0], 0.3f));
            }
            Detail("Ribbon spool", g, wb + Vector3.up * 0.93f + fwd * 0.55f, new Vector3(0.1f, 0.04f, 0.1f), redMat);
            Detail("Florist scissors", g, wb + Vector3.up * 0.905f - fwd * 0.5f, new Vector3(0.05f, 0.01f, 0.18f), metal, Quaternion.Euler(0f, 60f, 0f));
            Detail("Order card", g, wb + Vector3.up * 0.905f - inShop * 0.2f, new Vector3(0.1f, 0.003f, 0.07f), whiteFixture);
            Sign(g, "FOR SARAH - 14 FEB\n'COLLECT 9AM'", wb + Vector3.up * 0.91f - inShop * 0.2f, Vector3.up, 0.09f, 0.003f, false);
        }

        static void BuildingSociety(Transform g, MallUnit u, float y, int side)
        {
            WingId w = u.wing;
            Vector3 L(float du, float dv, float h) => MallLayout.WingPoint(w, u.u0 + du, side * dv, y + h);
            Vector3 fwd = MallLayout.WingForward(w), inShop = MallLayout.WingRight(w) * side;
            float len = u.u1 - u.u0;
            Material navy = ArtLibrary.Plain("Bank_Navy", new Color(0.08f, 0.12f, 0.3f), 0.5f);
            Material screenGlass = TransparentMat("Bank_Screen", new Color(0.6f, 0.7f, 0.75f, 0.2f));
            Quaternion faceFront = Quaternion.LookRotation(-inShop);
            // a counter across the shop with glass screens and positions
            float a0 = Mathf.Max(3.8f, 1.0f), a1 = len - 3.4f;
            if (a1 - a0 > 1.2f)
            {
                Vector3 c = L((a0 + a1) * 0.5f, 10.4f, 0f);
                EditorBuild.Box("Bank counter", g, c + Vector3.up * 0.55f, new Vector3(a1 - a0, 1.1f, 0.7f), navy, SurfaceType.Wood, faceFront);
                Detail("Bank screen", g, c + Vector3.up * 1.6f - inShop * 0.2f, new Vector3(a1 - a0, 1.0f, 0.015f), screenGlass, faceFront);
                int n = Mathf.Max(1, Mathf.FloorToInt((a1 - a0) / 1.3f));
                for (int k = 0; k < n; k++)
                {
                    Vector3 p = L(a0 + (k + 0.5f) * (a1 - a0) / n, 10.4f, 1.1f);
                    Detail("Deal tray", g, p - inShop * 0.3f + Vector3.up * 0.01f, new Vector3(0.4f, 0.03f, 0.25f), metal, faceFront);
                    Detail("Position number", g, p - inShop * 0.21f + Vector3.up * 1.0f, new Vector3(0.2f, 0.15f, 0.01f), whiteFixture, faceFront);
                    Sign(g, (k + 1).ToString(), p - inShop * 0.22f + Vector3.up * 1.0f, -inShop, 0.15f, 0.025f, false);
                    if (k % 2 == 0) Sign(g, "POSITION CLOSED", p - inShop * 0.21f + Vector3.up * 0.4f, -inShop, 0.5f, 0.014f);
                }
            }
            // queue barrier and leaflet stand in front
            Cyl("Queue post", g, L(1.2f, 8.4f, 0f), L(1.2f, 8.4f, 0.95f), 0.06f, metal, true);
            Cyl("Queue post", g, L(3.2f, 8.4f, 0f), L(3.2f, 8.4f, 0.95f), 0.06f, metal, true);
            Detail("Queue belt", g, L(2.2f, 8.4f, 0.9f), new Vector3(2.0f, 0.05f, 0.008f), navy, Quaternion.LookRotation(inShop));
            Sign(g, "PLEASE WAIT HERE", L(1.2f, 8.4f, 1.1f), -inShop, 0.4f, 0.012f);
            Detail("Leaflet stand", g, L(len - 0.6f, 8.6f, 0.75f), new Vector3(0.4f, 1.5f, 0.3f), navy, Quaternion.LookRotation(-fwd));
            Sign(g, "SAVINGS\nMORTGAGES\nISAs", L(len - 0.76f, 8.6f, 1.2f), -fwd, 0.35f, 0.012f, false);
            Sign(g, "ACCOUNTS FROM £1\nINTEREST 1.25% AER", L(1.2f, 6.8f, 1.8f), -inShop, 1.0f, 0.02f);
        }

        static void KeyCutter(Transform g, MallUnit u, float y, int side)
        {
            WingId w = u.wing;
            Vector3 L(float du, float dv, float h) => MallLayout.WingPoint(w, u.u0 + du, side * dv, y + h);
            Vector3 fwd = MallLayout.WingForward(w), inShop = MallLayout.WingRight(w) * side;
            float len = u.u1 - u.u0;
            var rng = new System.Random(u.code.GetHashCode());
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material brass = ArtLibrary.Plain("Key_Brass", new Color(0.75f, 0.6f, 0.25f), 0.85f, 1f);
            Material machineGreen = ArtLibrary.Plain("Machine_Green", new Color(0.15f, 0.35f, 0.25f), 0.5f);
            Quaternion faceFront = Quaternion.LookRotation(-inShop);
            // service counter across the middle, the key machine and shoe work behind it
            Vector3 c = L(len * 0.5f + 0.6f, 9.6f, 0f);
            EditorBuild.Box("Repair counter", g, c + Vector3.up * 0.5f, new Vector3(Mathf.Min(3f, len - 3.6f), 1.0f, 0.6f), woodMat, SurfaceType.Wood, faceFront);
            Vector3 km = c + inShop * 0.9f;
            EditorBuild.Box("Key bench", g, km + Vector3.up * 0.45f, new Vector3(1.4f, 0.9f, 0.6f), machineGreen, SurfaceType.Metal, faceFront);
            Detail("Key cutting machine", g, km + Vector3.up * 1.05f, new Vector3(0.5f, 0.3f, 0.35f), machineGreen, faceFront);
            Cyl("Cutting wheel", g, km + Vector3.up * 1.12f - inShop * 0.18f - fwd * 0.05f, km + Vector3.up * 1.12f - inShop * 0.18f + fwd * 0.05f, 0.18f, metal);
            Detail("Brass dust", g, km + Vector3.up * 0.905f - inShop * 0.25f, new Vector3(0.25f, 0.003f, 0.15f), brass);
            Detail("Shoe last", g, km + Vector3.up * 0.98f + fwd * 0.5f, new Vector3(0.1f, 0.15f, 0.28f), metal, faceFront);
            Detail("Shoe (being soled)", g, km + Vector3.up * 1.1f + fwd * 0.5f, new Vector3(0.1f, 0.1f, 0.28f), ArtLibrary.Plain("Shoe_Brown", new Color(0.3f, 0.18f, 0.1f), 0.5f), faceFront);
            // board of key blanks on the back partition
            float bw = Mathf.Min(3f, len - 3.8f);
            Vector3 board = L((0.7f + len - 2.9f) * 0.5f, 13.47f, 1.6f);
            Detail("Key board", g, board, new Vector3(bw, 1.2f, 0.02f), darkPanel, faceFront);
            for (int r = 0; r < 8; r++)
            for (int k = 0; k < 16; k++)
            {
                if (rng.NextDouble() < 0.1) continue;
                Vector3 p = board + Vector3.up * (-0.5f + r * 0.14f) + fwd * (-1.2f + k * 0.16f) * (bw / 3f) - inShop * 0.015f;
                Detail("Key blank", g, p, new Vector3(0.03f, 0.07f, 0.004f), r % 3 == 2 ? metal : brass, faceFront);
            }
            Sign(g, "KEYS CUT WHILE U WAIT\nHEELS  -  WATCH BATTERIES\nENGRAVING", L(1.2f, 6.8f, 1.8f), -inShop, 1.1f, 0.02f);
        }
    }
}
