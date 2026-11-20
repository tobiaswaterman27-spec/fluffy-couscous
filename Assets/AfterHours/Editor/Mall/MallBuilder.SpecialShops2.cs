using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// More shops fitted as what they are. Wanderlust Travel: consultants' desks, brochure walls, a globe and
    /// sunny posters. Tan Republic: stand-up tanning booths (one still glowing blue) and a reception desk.
    /// Pet Palace: a wall of lit fish tanks bubbling away, and the cages - every door open, every one empty.
    /// </summary>
    public static partial class MallBuilder
    {
        static bool SpecialShop2(Transform g, MallUnit u, float y, int side)
        {
            switch (u.name)
            {
                case "Wanderlust Travel": TravelAgent(g, u, y, side); return true;
                case "Tan Republic": TanningSalon(g, u, y, side); return true;
                case "Pet Palace": PetShop(g, u, y, side); return true;
            }
            return SpecialShop3(g, u, y, side);
        }

        static void TravelAgent(Transform g, MallUnit u, float y, int side)
        {
            WingId w = u.wing;
            Vector3 L(float du, float dv, float h) => MallLayout.WingPoint(w, u.u0 + du, side * dv, y + h);
            Vector3 fwd = MallLayout.WingForward(w), inShop = MallLayout.WingRight(w) * side;
            float len = u.u1 - u.u0;
            var rng = new System.Random(u.code.GetHashCode());
            Material desk = ArtLibrary.Plain("Travel_Desk", new Color(0.12f, 0.35f, 0.55f), 0.5f);
            Material sea = ArtLibrary.Plain("Globe_Sea", new Color(0.1f, 0.3f, 0.6f), 0.7f);
            Material land = ArtLibrary.Plain("Globe_Land", new Color(0.35f, 0.5f, 0.2f), 0.4f);
            Quaternion faceFront = Quaternion.LookRotation(-inShop);
            // two consultant desks facing the door, client chairs in front
            foreach (float du in new[] { len * 0.35f, len * 0.7f })
            {
                Vector3 c = L(du, 9.6f, 0f);
                EditorBuild.Box("Consultant desk", g, c + Vector3.up * 0.37f, new Vector3(1.5f, 0.74f, 0.75f), desk, SurfaceType.Wood, faceFront);
                Detail("Desk monitor", g, c + Vector3.up * 0.98f + inShop * 0.15f, new Vector3(0.5f, 0.32f, 0.03f), blackMat, faceFront);
                Detail("Brochure fan", g, c + Vector3.up * 0.75f - inShop * 0.2f + fwd * 0.4f, new Vector3(0.3f, 0.02f, 0.22f), whiteFixture, faceFront * Quaternion.Euler(0f, 15f, 0f));
                OfficeChairProp(g, c + inShop * 0.75f, Vector3.SignedAngle(Vector3.forward, -inShop, Vector3.up) + 15f, 8f, SurfaceType.Metal);
                ChairProp(g, c - inShop * 0.8f - fwd * 0.35f, Vector3.SignedAngle(Vector3.forward, inShop, Vector3.up) + 10f, false);
                ChairProp(g, c - inShop * 0.8f + fwd * 0.35f, Vector3.SignedAngle(Vector3.forward, inShop, Vector3.up) - 10f, false);
            }
            // brochure walls on both sides
            foreach (float du in new[] { 0.28f, len - 0.28f })
            {
                Vector3 n = du < 1f ? fwd : -fwd;
                Quaternion q = Quaternion.LookRotation(n);
                for (int r = 0; r < 5; r++)
                for (int c = 0; c < 8; c++)
                {
                    Vector3 p = L(du, 7.4f + c * 0.42f, 0.7f + r * 0.32f) + n * 0.03f;
                    Detail("Brochure", g, p, new Vector3(0.3f, 0.26f, 0.025f), ArtLibrary.Plain($"Brochure_{(r + c) % 5}", Color.HSVToRGB(((r * 3 + c) % 10) / 10f, 0.55f, 0.85f), 0.3f), q * Quaternion.Euler(-8f, 0f, 0f));
                }
            }
            // the globe on its stand by the window
            Vector3 gb = L(len * 0.5f, 8.6f, 0f);
            Cyl("Globe stand", g, gb, gb + Vector3.up * 0.75f, 0.06f, metal);
            Ball("Globe", g, gb + Vector3.up * 1.05f, 0.55f, sea, true);
            for (int k = 0; k < 4; k++) Detail("Continent", g, gb + Vector3.up * 1.05f + Quaternion.Euler(0f, k * 90f + 20f, 0f) * Vector3.forward * 0.25f + Vector3.up * (k % 2 == 0 ? 0.1f : -0.08f), new Vector3(0.18f, 0.16f, 0.06f), land, Quaternion.Euler(0f, k * 90f + 20f, 0f));
            // posters on the back partition
            string[] places = { "SANTORINI\n7 NIGHTS £499", "BALI\nPARADISE AWAITS", "NEW YORK\nCITY BREAKS", "LAPLAND\nMEET SANTA!" };
            for (int k = 0; k < 4; k++)
            {
                float du = 0.9f + k * Mathf.Max(1.1f, (len - 4.5f) / 4f);
                if (du > len - 3.4f) break;
                Detail("Travel poster", g, L(du, 13.48f, 1.7f), new Vector3(0.8f, 1.1f, 0.01f), ArtLibrary.Plain($"Sunny_{k}", new Color(0.95f, 0.65f - k * 0.08f, 0.2f + k * 0.12f), 0.3f), faceFront);
                Sign(g, places[k], L(du, 13.46f, 1.4f), -inShop, 0.7f, 0.016f, false);
            }
        }

        static void TanningSalon(Transform g, MallUnit u, float y, int side)
        {
            WingId w = u.wing;
            Vector3 L(float du, float dv, float h) => MallLayout.WingPoint(w, u.u0 + du, side * dv, y + h);
            Vector3 fwd = MallLayout.WingForward(w), inShop = MallLayout.WingRight(w) * side;
            float len = u.u1 - u.u0;
            Material shell = ArtLibrary.Plain("Booth_Shell", new Color(0.85f, 0.85f, 0.88f), 0.7f);
            Material uvOn = ArtLibrary.Plain("UV_Tubes_On", new Color(0.3f, 0.35f, 0.9f), 0.6f, 0f, new Color(0.9f, 1.1f, 4.0f), false);
            Material uvOff = ArtLibrary.Plain("UV_Tubes_Off", new Color(0.25f, 0.25f, 0.3f), 0.6f);
            Material cubicle = ArtLibrary.Plain("Cubicle_Lilac", new Color(0.6f, 0.5f, 0.7f), 0.4f);
            // two cubicles across the back half, each with a stand-up booth
            int n = len >= 8f ? 2 : 1;
            for (int k = 0; k < n; k++)
            {
                float du = len - 1.6f - k * 2.6f;
                if (du < 3.9f) break;
                // cubicle partitions (clear of the till counter)
                if (du - 1.25f > Mathf.Min(3.5f, len - 3.4f) + 0.2f) Detail("Cubicle wall", g, L(du - 1.25f, 9.9f, 1.1f), new Vector3(2.8f, 2.2f, 0.05f), cubicle, Quaternion.LookRotation(fwd));
                Vector3 c = L(du, 9.6f, 0f);
                EditorBuild.Box("Tanning booth", g, c + Vector3.up * 1.1f, new Vector3(1.1f, 2.2f, 1.1f), shell, SurfaceType.Metal, Quaternion.LookRotation(-inShop));
                bool on = k == 0;
                Vector3 door = c - inShop * 0.56f;
                Detail("Booth door (ajar)", g, door + Vector3.up * 1.05f + fwd * 0.35f - inShop * 0.25f, new Vector3(0.7f, 2.0f, 0.04f), shell, Quaternion.LookRotation(-inShop) * Quaternion.Euler(0f, 40f, 0f));
                for (int t = -2; t <= 2; t++)
                    Detail("UV tube", g, c - inShop * 0.5f + fwd * (t * 0.12f) + Vector3.up * 1.1f, new Vector3(0.04f, 1.8f, 0.01f), on ? uvOn : uvOff, Quaternion.LookRotation(-inShop));
                if (on)
                {
                    var lg = new GameObject("Tanning booth glow");
                    lg.transform.SetParent(g, false);
                    lg.transform.localPosition = c - inShop * 0.9f + Vector3.up * 1.2f;
                    Light l = lg.AddComponent<Light>();
                    l.type = LightType.Point; l.range = 3.5f; l.intensity = 1.1f; l.color = new Color(0.45f, 0.5f, 1f); l.shadows = LightShadows.None;
                    l.lightmapBakeType = LightmapBakeType.Realtime;
                }
                Detail("Goggles", g, c - inShop * 0.9f + Vector3.up * 0.02f, new Vector3(0.12f, 0.03f, 0.05f), blackMat, Quaternion.Euler(0f, 30f, 0f));
                Detail("Towel (dropped)", g, c - inShop * 1.0f + fwd * 0.3f + Vector3.up * 0.01f, new Vector3(0.6f, 0.02f, 0.4f), whiteFixture, Quaternion.Euler(0f, 15f, 0f));
            }
            Sign(g, "MINUTES FROM £1.20\nCOURSES AVAILABLE\nNO UNDER 18s", L(1.2f, 6.8f, 1.7f), -inShop, 1.0f, 0.02f);
        }

        static void PetShop(Transform g, MallUnit u, float y, int side)
        {
            WingId w = u.wing;
            Vector3 L(float du, float dv, float h) => MallLayout.WingPoint(w, u.u0 + du, side * dv, y + h);
            Vector3 fwd = MallLayout.WingForward(w), inShop = MallLayout.WingRight(w) * side;
            float len = u.u1 - u.u0;
            var rng = new System.Random(u.code.GetHashCode());
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material tankWater = ArtLibrary.Plain("Tank_Water", new Color(0.15f, 0.35f, 0.4f), 0.95f, 0f, new Color(0.1f, 0.35f, 0.45f), false);
            Material gravel = ArtLibrary.Plain("Tank_Gravel", new Color(0.55f, 0.5f, 0.42f), 0.2f);
            Material weed = ArtLibrary.Plain("Tank_Weed", new Color(0.1f, 0.4f, 0.15f), 0.4f);
            Material cageWire = ArtLibrary.Plain("Cage_Wire", new Color(0.7f, 0.7f, 0.72f), 0.6f, 0.8f);
            Material fish = ArtLibrary.Plain("Fish_Orange", new Color(0.95f, 0.5f, 0.1f), 0.6f);
            // aquarium wall on the far side: 3 rows of tanks, lit and bubbling (the filters still run)
            float du0 = len - 0.45f;
            Quaternion toBack = Quaternion.LookRotation(-fwd);
            EditorBuild.Box("Aquarium rack", g, L(du0, 9.8f, 1.1f), new Vector3(3.2f, 2.2f, 0.6f), blackMat, SurfaceType.Metal, toBack);
            for (int r = 0; r < 3; r++)
            for (int c = 0; c < 4; c++)
            {
                Vector3 p = L(du0 - 0.31f, 8.6f + c * 0.78f, 0.45f + r * 0.68f);
                Detail("Fish tank", g, p, new Vector3(0.7f, 0.5f, 0.02f), tankWater, toBack);
                Detail("Tank gravel", g, p - Vector3.up * 0.22f - fwd * 0.005f, new Vector3(0.7f, 0.06f, 0.02f), gravel, toBack);
                Detail("Tank weed", g, p - Vector3.up * 0.08f - fwd * 0.008f + inShop * R(-0.2f, 0.2f), new Vector3(0.05f, 0.25f, 0.01f), weed, toBack);
                if (rng.NextDouble() < 0.6)
                    Detail("Fish", g, p + Vector3.up * R(-0.1f, 0.12f) - fwd * 0.01f + inShop * R(-0.25f, 0.25f), new Vector3(0.06f, 0.03f, 0.005f), fish, toBack);
            }
            var lg = new GameObject("Aquarium glow");
            lg.transform.SetParent(g, false);
            lg.transform.localPosition = L(du0 - 1.2f, 9.8f, 1.2f);
            Light l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.range = 4.5f; l.intensity = 0.8f; l.color = new Color(0.4f, 0.75f, 0.85f); l.shadows = LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            // a stack of small-animal cages along the near wall, every door hanging open, every one empty
            for (int r = 0; r < 2; r++)
            for (int c = 0; c < 3; c++)
            {
                Vector3 p = L(0.6f, 8.4f + c * 0.9f, 0.3f + r * 0.6f);
                Detail("Cage tray", g, p - Vector3.up * 0.24f, new Vector3(0.8f, 0.06f, 0.5f), ArtLibrary.Plain("Cage_Tray_Blue", new Color(0.2f, 0.35f, 0.6f), 0.4f), Quaternion.LookRotation(fwd));
                foreach (float a in new[] { -0.4f, 0.4f })
                    Detail("Cage side", g, p + inShop * a, new Vector3(0.01f, 0.45f, 0.5f), cageWire, Quaternion.LookRotation(fwd));
                Detail("Cage top", g, p + Vector3.up * 0.23f, new Vector3(0.8f, 0.01f, 0.5f), cageWire, Quaternion.LookRotation(fwd));
                Detail("Cage door (open)", g, p + fwd * 0.3f - Vector3.up * 0.05f, new Vector3(0.35f, 0.01f, 0.3f), cageWire, Quaternion.LookRotation(fwd) * Quaternion.Euler(70f, 0f, 0f));
                Detail("Sawdust", g, p - Vector3.up * 0.2f, new Vector3(0.75f, 0.02f, 0.45f), ArtLibrary.Plain("Sawdust", new Color(0.8f, 0.7f, 0.5f), 0.1f), Quaternion.LookRotation(fwd));
            }
            for (int k = 0; k < 8; k++)
                Detail("Sawdust (spilled)", g, L(R(0.8f, 2.4f), R(7.4f, 9.6f), 0.004f), new Vector3(R(0.1f, 0.3f), 0.004f, R(0.1f, 0.3f)), ArtLibrary.Plain("Sawdust", new Color(0.8f, 0.7f, 0.5f), 0.1f), Quaternion.Euler(0f, R(0f, 180f), 0f));
            // dog beds and a sack of feed slumped in the middle
            foreach (float du in new[] { len * 0.45f, len * 0.6f })
            {
                Cyl("Dog bed", g, L(du, 9.2f, 0f), L(du, 9.2f, 0.18f), 0.8f, ArtLibrary.Plain("DogBed_Red", new Color(0.6f, 0.15f, 0.12f), 0.15f));
                Cyl("Dog bed cushion", g, L(du, 9.2f, 0.05f), L(du, 9.2f, 0.12f), 0.6f, ArtLibrary.Plain("DogBed_Cream", new Color(0.85f, 0.8f, 0.7f), 0.1f));
            }
            Detail("Feed sack (torn)", g, L(len * 0.52f, 10.3f, 0.3f), new Vector3(0.5f, 0.6f, 0.3f), ArtLibrary.Plain("Feed_Sack", new Color(0.7f, 0.6f, 0.35f), 0.2f), Quaternion.LookRotation(fwd) * Quaternion.Euler(10f, 0f, 8f));
            for (int k = 0; k < 6; k++)
                Detail("Kibble", g, L(len * 0.52f + R(-0.3f, 0.3f), 10.0f + R(-0.4f, 0.1f), 0.01f), new Vector3(0.12f, 0.01f, 0.08f), ArtLibrary.Plain("Kibble", new Color(0.4f, 0.25f, 0.12f), 0.2f), Quaternion.Euler(0f, R(0f, 180f), 0f));
            Sign(g, "PLEASE DO NOT TAP\nON THE GLASS", L(du0 - 0.32f, 9.8f, 2.35f), -fwd, 1.4f, 0.02f, false);
        }
    }
}
