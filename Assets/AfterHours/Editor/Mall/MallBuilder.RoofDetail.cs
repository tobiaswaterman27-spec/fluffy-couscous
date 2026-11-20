using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// The open roof over the north-east block: a paved walkway from the stair core to the maintenance cage, fans and
    /// louvres on the plant units, a satellite dish and an aerial mast with its red obstruction lamp, copper lightning
    /// tape along the parapets, drain outlets in the puddles, and the staff smoking spot (a crate, a sand bucket of
    /// stubs, a crushed can) round the back of the core.
    /// </summary>
    public static partial class MallBuilder
    {
        static void RoofDetail(Transform g)
        {
            float y = MallLayout.RoofY;
            float q0 = MallLayout.OuterWall, q1 = MallLayout.QuadrantOuter;
            var rng = new System.Random(6611);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Vector3 P(float x, float h, float z) => new Vector3(x, y + h, z);

            Material slab = ArtLibrary.Plain("Roof_Paver", new Color(0.42f, 0.42f, 0.4f), 0.2f);
            Material copper = ArtLibrary.Plain("Lightning_Tape", new Color(0.45f, 0.25f, 0.12f), 0.5f, 0.9f);
            Material fanBlade = ArtLibrary.Plain("Fan_Blade", new Color(0.2f, 0.2f, 0.22f), 0.5f, 0.7f);
            Material redLamp = ArtLibrary.Plain("Aviation_Red", new Color(0.6f, 0.05f, 0.03f), 0.6f, 0f, new Color(4f, 0.25f, 0.1f), false);
            Material dishWhite = ArtLibrary.Plain("Dish_White", new Color(0.82f, 0.82f, 0.8f), 0.4f);
            Material crateMat = ArtLibrary.Plain("Crate_Blue", new Color(0.1f, 0.2f, 0.5f), 0.4f);

            // ---- paving slab walkway (core door -> along the south fence -> up to the cage) ----
            for (float x = 26f; x <= 41.3f; x += 0.65f)
                Detail("Walkway paver", g, P(x, 0.025f, 23.9f), new Vector3(0.6f, 0.05f, 0.6f), slab, Quaternion.Euler(0f, R(-2f, 2f), 0f));
            for (float z = 24.55f; z <= 41.8f; z += 0.65f)
                Detail("Walkway paver", g, P(41.6f, 0.025f, z), new Vector3(0.6f, 0.05f, 0.6f), slab, Quaternion.Euler(0f, R(-2f, 2f), 0f));

            // ---- plant unit 1: two big extract fans on top, louvres on the sides ----
            foreach (float fx in new[] { 35.3f, 37.7f })
            {
                Vector3 c = P(fx, 2.2f, 35.5f);
                Cyl("Fan shroud", g, c, c + Vector3.up * 0.35f, 1.25f, plantMat);
                Cyl("Fan hub", g, c + Vector3.up * 0.2f, c + Vector3.up * 0.3f, 0.2f, metal);
                float spin = R(0f, 90f);
                for (int k = 0; k < 4; k++)
                    Detail("Fan blade", g, c + Vector3.up * 0.25f, new Vector3(1.0f, 0.02f, 0.16f), fanBlade, Quaternion.Euler(0f, spin + k * 45f, 12f));
                for (int k = -2; k <= 2; k++)
                    Detail("Fan guard bar", g, c + Vector3.up * 0.36f + Vector3.right * k * 0.24f, new Vector3(0.015f, 0.015f, 1.2f), metal);
            }
            for (float h = 0.4f; h < 2.0f; h += 0.18f)
            {
                Detail("Louvre", g, P(36.5f, h, 33.97f), new Vector3(4.6f, 0.04f, 0.08f), metal, Quaternion.Euler(30f, 0f, 0f));
                Detail("Louvre", g, P(36.5f, h, 37.03f), new Vector3(4.6f, 0.04f, 0.08f), metal, Quaternion.Euler(-30f, 0f, 0f));
            }
            // plant unit 2: an access panel, a duct elbow going down through the roof
            Detail("Access panel", g, P(31.02f, 0.8f, 40.5f), new Vector3(0.02f, 1.1f, 0.9f), metal);
            Detail("Access panel handle", g, P(31.05f, 0.85f, 40.15f), new Vector3(0.03f, 0.18f, 0.04f), blackMat);
            Sign(g, "AHU 3\nISOLATE BEFORE OPENING", P(31.03f, 1.45f, 40.5f), Vector3.right, 0.8f, 0.012f, false);
            EditorBuild.Box("Duct", g, P(29.5f, 0.75f, 36.6f), new Vector3(1.0f, 1.5f, 2.8f), metal, SurfaceType.Metal);
            Detail("Duct flange", g, P(29.5f, 0.75f, 35.2f), new Vector3(1.1f, 1.6f, 0.06f), plantMat);
            Detail("Duct upstand", g, P(29.5f, 0.15f, 35.0f), new Vector3(1.3f, 0.3f, 0.5f), concrete);

            // ---- satellite dish on a frame by the west fence ----
            {
                Vector3 b = P(21.3f, 0f, 42.3f);
                Detail("Dish frame", g, b + Vector3.up * 0.05f, new Vector3(1.2f, 0.1f, 1.2f), plantMat);
                for (int k = 0; k < 4; k++) Detail("Dish ballast block", g, b + new Vector3((k & 1) * 0.9f - 0.45f, 0.18f, (k >> 1) * 0.9f - 0.45f), new Vector3(0.35f, 0.16f, 0.35f), concrete);
                Cyl("Dish pole", g, b, b + Vector3.up * 1.4f, 0.08f, metal);
                Vector3 dc = b + Vector3.up * 1.55f + new Vector3(0.1f, 0f, -0.1f);
                Quaternion aim = Quaternion.LookRotation(new Vector3(0.5f, 0.55f, -0.65f));
                Cyl("Dish", g, dc, dc + aim * Vector3.forward * 0.06f, 1.0f, dishWhite);
                Cyl("Dish arm", g, dc, dc + aim * Vector3.forward * 0.55f + Vector3.down * 0.1f, 0.025f, metal);
                Detail("Dish LNB", g, dc + aim * Vector3.forward * 0.58f + Vector3.down * 0.1f, new Vector3(0.07f, 0.07f, 0.12f), blackMat, aim);
                Cyl("Dish cable", g, b + Vector3.up * 1.2f, P(21.1f, 0.02f, 39.8f), 0.015f, blackMat);
            }

            // ---- aerial mast in the south-east corner, red obstruction lamp on top ----
            {
                Vector3 b = P(43.6f, 0f, 21.0f);
                Detail("Mast base", g, b + Vector3.up * 0.1f, new Vector3(0.8f, 0.2f, 0.8f), concrete);
                Cyl("Mast", g, b, b + Vector3.up * 7.5f, 0.12f, metal);
                for (int k = 1; k <= 3; k++)
                {
                    float h = 2f + k * 1.5f;
                    Cyl("Aerial element", g, b + Vector3.up * h - Vector3.right * 0.5f, b + Vector3.up * h + Vector3.right * 0.5f, 0.02f, metal);
                }
                foreach (Vector3 d in new[] { new Vector3(-2.5f, 0f, 0f), new Vector3(0f, 0f, 2.5f), new Vector3(-1.6f, 0f, -1.3f) })
                    Cyl("Guy wire", g, b + Vector3.up * 5f, b + d + Vector3.up * 0.05f, 0.01f, metal);
                Ball("Obstruction lamp", g, b + Vector3.up * 7.6f, 0.18f, redLamp);
                var lg = new GameObject("Obstruction lamp light");
                lg.transform.SetParent(g, false);
                lg.transform.localPosition = b + Vector3.up * 7.5f;
                Light l = lg.AddComponent<Light>();
                l.type = LightType.Point; l.range = 6f; l.intensity = 0.8f; l.color = new Color(1f, 0.15f, 0.08f);
                l.shadows = LightShadows.None; l.lightmapBakeType = LightmapBakeType.Realtime;
            }

            // ---- lightning tape along the parapet tops and down into the roof ----
            Detail("Lightning tape", g, P(q1 - 0.15f, 1.205f, (q0 + q1) * 0.5f), new Vector3(0.025f, 0.006f, q1 - q0), copper);
            Detail("Lightning tape", g, P((q0 + q1) * 0.5f, 1.205f, q1 - 0.15f), new Vector3(q1 - q0, 0.006f, 0.025f), copper);
            Detail("Lightning tape (down)", g, P(q1 - 0.31f, 0.6f, 30f), new Vector3(0.006f, 1.2f, 0.025f), copper);
            Detail("Lightning tape (down)", g, P(30f, 0.6f, q1 - 0.31f), new Vector3(0.025f, 1.2f, 0.006f), copper);

            // ---- drain outlets in the puddles ----
            foreach (var (x, z) in new[] { (24f, 40f), (36.5f, 26f), (42f, 41.5f), (27f, 29.5f) })
            {
                Cyl("Roof drain", g, P(x, 0f, z), P(x, 0.03f, z), 0.28f, blackMat);
                Detail("Drain leaf guard", g, P(x, 0.08f, z), new Vector3(0.18f, 0.1f, 0.18f), metal);
                Detail("Leaves (clogging)", g, P(x + 0.1f, 0.01f, z - 0.05f), new Vector3(0.35f, 0.012f, 0.25f), ArtLibrary.Plain("Dead_Leaves", new Color(0.25f, 0.18f, 0.08f), 0.2f), Quaternion.Euler(0f, R(0f, 180f), 0f));
            }

            // ---- the staff smoking spot behind the core ----
            {
                Vector3 c = P(26.6f, 0f, 20.4f);
                EditorBuild.Box("Upturned crate (seat)", g, c + Vector3.up * 0.16f, new Vector3(0.5f, 0.32f, 0.36f), crateMat, SurfaceType.Wood, Quaternion.Euler(0f, 18f, 0f));
                Vector3 bucket = c + new Vector3(0.65f, 0f, -0.15f);
                Cyl("Sand bucket", g, bucket, bucket + Vector3.up * 0.3f, 0.28f, redMat, true);
                Cyl("Sand", g, bucket + Vector3.up * 0.26f, bucket + Vector3.up * 0.28f, 0.26f, ArtLibrary.Plain("Sand", new Color(0.6f, 0.52f, 0.36f), 0.1f));
                Material stub = ArtLibrary.Plain("Cig_Stub", new Color(0.85f, 0.7f, 0.45f), 0.2f);
                for (int k = 0; k < 9; k++)
                {
                    Vector3 s = bucket + new Vector3(R(-0.09f, 0.09f), 0.295f, R(-0.09f, 0.09f));
                    Detail("Stub", g, s, new Vector3(0.012f, 0.012f, 0.035f), stub, Quaternion.Euler(R(-30f, 30f), R(0f, 180f), 0f));
                }
                for (int k = 0; k < 6; k++)
                    Detail("Stub (ground)", g, c + new Vector3(R(-0.6f, 1.2f), 0.006f, R(-0.6f, 0.6f)), new Vector3(0.012f, 0.012f, 0.035f), stub, Quaternion.Euler(0f, R(0f, 180f), 90f));
                Detail("Crushed can", g, c + new Vector3(-0.5f, 0.03f, 0.35f), new Vector3(0.07f, 0.05f, 0.11f), ArtLibrary.Plain("Can_Red", new Color(0.6f, 0.07f, 0.05f), 0.7f, 0.8f), Quaternion.Euler(0f, 40f, 80f));
                Sign(g, "NO SMOKING ON THE ROOF", P(25.32f, 1.8f, 21.4f), Vector3.right, 1.2f, 0.018f);
            }
        }
    }
}
