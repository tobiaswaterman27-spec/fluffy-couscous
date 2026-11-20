using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Car park fixtures: pay machines by the lift lobby with a zebra crossing to it, a trolley shelter with a row of
    /// nested trolleys (and a couple left loose), height-restriction bars hung on chains at the split-level ramps,
    /// fire points on some pillars, and a few emergency call points.
    /// </summary>
    public static partial class MallBuilder
    {
        static void CarParkFixtures(Transform g, Transform pillars)
        {
            float y = MallLayout.CarParkY;
            var rng = new System.Random(7123);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            Material payBlue = ArtLibrary.Plain("PayMachine_Blue", new Color(0.08f, 0.2f, 0.45f), 0.5f);
            Material screenGlow = ArtLibrary.Plain("PayMachine_Screen", new Color(0.1f, 0.2f, 0.15f), 0.9f, 0f, new Color(0.25f, 0.6f, 0.4f), false);
            Material zinc = ArtLibrary.Plain("Trolley_Zinc", new Color(0.62f, 0.64f, 0.66f), 0.65f, 0.85f);
            Material trolleyBlue = ArtLibrary.Plain("Trolley_Handle", new Color(0.1f, 0.25f, 0.6f), 0.5f);
            Material canopyMat = ArtLibrary.Plain("Canopy_Green", new Color(0.12f, 0.3f, 0.18f), 0.4f);

            // ---- pay machines either side of the lift lobby doors, and the crossing to them ----
            foreach (float px in new[] { -16.1f, -9.9f })
            {
                Vector3 b = new Vector3(px, y, 8.62f);
                EditorBuild.Box("Pay machine", g, b + Vector3.up * 0.85f, new Vector3(0.7f, 1.7f, 0.45f), payBlue, SurfaceType.Metal);
                Detail("Pay machine screen", g, b + new Vector3(0f, 1.3f, -0.235f), new Vector3(0.36f, 0.24f, 0.01f), screenGlow);
                Detail("Pay machine keypad", g, b + new Vector3(-0.12f, 1.0f, -0.24f), new Vector3(0.18f, 0.16f, 0.02f), metal, Quaternion.Euler(-20f, 0f, 0f));
                Detail("Coin slot", g, b + new Vector3(0.15f, 1.05f, -0.235f), new Vector3(0.08f, 0.12f, 0.01f), blackMat);
                Detail("Ticket slot", g, b + new Vector3(0.15f, 0.85f, -0.235f), new Vector3(0.14f, 0.03f, 0.01f), blackMat);
                Detail("Change cup", g, b + new Vector3(0f, 0.45f, -0.25f), new Vector3(0.25f, 0.14f, 0.05f), metal);
                Sign(g, "PAY HERE\nBEFORE RETURNING\nTO YOUR VEHICLE", b + new Vector3(0f, 1.62f, -0.235f), Vector3.back, 0.6f, 0.012f, false);
            }
            Material zebra = whiteFixture;
            for (int k = 0; k < 6; k++)
                Detail("Zebra stripe", g, new Vector3(-13f, y + 0.006f, 7.6f - k * 0.9f), new Vector3(2.4f, 0.004f, 0.45f), zebra);

            // ---- trolley shelter on a bay near the lobby ----
            float sx0 = -55.4f, sx1 = -48.6f, sz0 = 0.7f, sz1 = 4.5f;
            foreach (float x in new[] { sx0, (sx0 + sx1) * 0.5f, sx1 })
            foreach (float z in new[] { sz0, sz1 })
                Cyl("Shelter post", g, new Vector3(x, y, z), new Vector3(x, y + 2.3f, z), 0.08f, canopyMat, true);
            Detail("Shelter roof", g, new Vector3((sx0 + sx1) * 0.5f, y + 2.35f, (sz0 + sz1) * 0.5f), new Vector3(sx1 - sx0 + 0.3f, 0.06f, sz1 - sz0 + 0.4f), canopyMat);
            Detail("Shelter fascia", g, new Vector3((sx0 + sx1) * 0.5f, y + 2.22f, sz1 + 0.2f), new Vector3(sx1 - sx0 + 0.3f, 0.25f, 0.04f), canopyMat);
            Sign(g, "TROLLEYS", new Vector3((sx0 + sx1) * 0.5f, y + 2.22f, sz1 + 0.23f), Vector3.forward, 2f, 0.025f, false);
            foreach (float z in new[] { sz0 + 1.2f, sz1 - 1.2f })
                Cyl("Shelter guide rail", g, new Vector3(sx0, y + 0.8f, z), new Vector3(sx1, y + 0.8f, z), 0.05f, zinc);

            void Trolley(Vector3 at, float yaw)
            {
                Quaternion r = Quaternion.Euler(0f, yaw, 0f);
                Vector3 f = r * Vector3.forward, s = r * Vector3.right;
                Vector3 c = at + Vector3.up * 0.72f;
                Detail("Trolley basket floor", g, c - Vector3.up * 0.22f, new Vector3(0.52f, 0.02f, 0.85f), zinc, r);
                foreach (int a in new[] { -1, 1 })
                    Detail("Trolley basket side", g, c + s * a * 0.26f, new Vector3(0.015f, 0.44f, 0.85f), zinc, r);
                Detail("Trolley basket front", g, c + f * 0.42f, new Vector3(0.5f, 0.44f, 0.015f), zinc, r * Quaternion.Euler(-12f, 0f, 0f));
                Detail("Trolley basket back", g, c - f * 0.42f, new Vector3(0.52f, 0.44f, 0.015f), zinc, r);
                for (int k = -2; k <= 2; k++)
                    Detail("Trolley wire", g, c + s * k * 0.1f, new Vector3(0.006f, 0.45f, 0.86f), zinc, r);
                Cyl("Trolley handle", g, c - f * 0.5f + Vector3.up * 0.32f - s * 0.28f, c - f * 0.5f + Vector3.up * 0.32f + s * 0.28f, 0.04f, trolleyBlue);
                foreach (int a in new[] { -1, 1 })
                {
                    Cyl("Trolley chassis", g, at + s * a * 0.2f - f * 0.4f + Vector3.up * 0.12f, at + s * a * 0.2f + f * 0.35f + Vector3.up * 0.12f, 0.03f, zinc);
                    Cyl("Trolley leg", g, at + s * a * 0.22f - f * 0.42f + Vector3.up * 0.12f, c - f * 0.42f + s * a * 0.24f - Vector3.up * 0.2f, 0.025f, zinc);
                    foreach (float fb in new[] { -0.38f, 0.33f })
                        Cyl("Trolley castor", g, at + s * (a * 0.2f - 0.02f) + f * fb + Vector3.up * 0.05f, at + s * (a * 0.2f + 0.02f) + f * fb + Vector3.up * 0.05f, 0.1f, blackMat);
                }
                var col = new GameObject("Trolley (collider)");
                col.transform.SetParent(g, false);
                col.transform.position = at + Vector3.up * 0.5f;
                col.transform.rotation = r;
                col.AddComponent<BoxCollider>().size = new Vector3(0.56f, 1.0f, 0.95f);
                col.isStatic = true;
            }
            // a nested row (each one pushed into the back of the last)
            for (int k = 0; k < 6; k++) Trolley(new Vector3(sx1 - 0.6f - k * 0.3f, y, (sz0 + sz1) * 0.5f), -90f);
            // loose ones in the bays
            Trolley(new Vector3(-44.2f, y, 3.1f), R(30f, 70f));
            Trolley(new Vector3(-20.5f, y, -2.6f), R(150f, 200f));
            Trolley(new Vector3(6.3f, y, 18.4f), R(-40f, 40f));

            // ---- height bars at the split-level ramps (2.1 m) ----
            float raisedEdge = MallLayout.CarParkRaised.minZ;
            void HeightBar(Vector3 a, Vector3 b)
            {
                Vector3 d = (b - a);
                int n = Mathf.RoundToInt(d.magnitude / 0.5f);
                for (int k = 0; k < n; k++)
                    Cyl("Height bar", g, Vector3.Lerp(a, b, (float)k / n), Vector3.Lerp(a, b, (float)(k + 1) / n), 0.12f, k % 2 == 0 ? yellowPaint : blackMat);
                foreach (float t in new[] { 0.08f, 0.92f })
                {
                    Vector3 p = Vector3.Lerp(a, b, t);
                    for (float h = 0f; h < -0.5f - p.y - 0.05f; h += 0.09f)
                        Detail("Height bar chain link", g, p + Vector3.up * (h + 0.06f), new Vector3(0.025f, 0.08f, 0.05f), metal, Quaternion.Euler(0f, h * 1000f % 2f < 1f ? 0f : 90f, 0f));
                }
                Sign(g, "MAX HEADROOM 2.1m", Vector3.Lerp(a, b, 0.5f) + Vector3.up * 0.28f, (a - b).z == 0f ? Vector3.back : Vector3.left, 1.6f, 0.02f);
            }
            HeightBar(new Vector3(-69.6f, y + 2.1f, raisedEdge - 12.6f), new Vector3(-62.4f, y + 2.1f, raisedEdge - 12.6f));
            HeightBar(new Vector3(MallLayout.CarParkRaised.maxX + 12.6f, y + 2.1f, 60.4f), new Vector3(MallLayout.CarParkRaised.maxX + 12.6f, y + 2.1f, 67.6f));

            // ---- fire points and emergency call points on some pillars ----
            if (pillars == null) return;
            int idx = 0;
            foreach (Transform p in pillars)
            {
                if (p.name != "Pillar") continue;
                idx++;
                Bounds b = p.GetComponent<Renderer>().bounds;
                int h = MallLayout.Hash(Mathf.RoundToInt(b.center.x), Mathf.RoundToInt(b.center.z), 31, 7);
                Vector3 face = new Vector3(b.center.x, b.min.y, b.max.z + 0.01f);   // north face
                if (h % 11 == 0)
                {
                    Detail("Fire point board", g, face + new Vector3(0f, 1.55f, 0.01f), new Vector3(0.5f, 0.75f, 0.02f), redMat);
                    Sign(g, "FIRE POINT", face + new Vector3(0f, 1.82f, 0.03f), Vector3.forward, 0.45f, 0.014f, false);
                    Cyl("Extinguisher", g, face + new Vector3(-0.1f, 1.05f, 0.1f), face + new Vector3(-0.1f, 1.55f, 0.1f), 0.15f, redMat);
                    Detail("Extinguisher label", g, face + new Vector3(-0.1f, 1.3f, 0.176f), new Vector3(0.1f, 0.12f, 0.005f), whiteFixture);
                    Cyl("Extinguisher (foam)", g, face + new Vector3(0.12f, 1.05f, 0.1f), face + new Vector3(0.12f, 1.5f, 0.1f), 0.13f, redMat);
                    Detail("Extinguisher band", g, face + new Vector3(0.12f, 1.38f, 0.167f), new Vector3(0.11f, 0.06f, 0.005f), ArtLibrary.Plain("Foam_Cream", new Color(0.85f, 0.78f, 0.55f), 0.5f));
                }
                else if (h % 17 == 3)
                {
                    Detail("Help point", g, face + new Vector3(0f, 1.4f, 0.04f), new Vector3(0.24f, 0.36f, 0.08f), yellowPaint);
                    Detail("Help point button", g, face + new Vector3(0f, 1.36f, 0.085f), new Vector3(0.07f, 0.07f, 0.02f), redMat);
                    Detail("Help point grille", g, face + new Vector3(0f, 1.48f, 0.085f), new Vector3(0.12f, 0.06f, 0.01f), blackMat);
                    Sign(g, "HELP", face + new Vector3(0f, 1.63f, 0.085f), Vector3.forward, 0.2f, 0.012f, false);
                }
            }
            _ = idx;
        }
    }
}
