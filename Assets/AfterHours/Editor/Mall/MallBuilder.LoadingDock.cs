using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// The loading dock floor in front of the two dock shutters: hazard edging on the levellers, rubber dock bumpers,
    /// a dock light on a swing arm by each door, a pallet truck left mid-floor, wrapped pallets and empty ones along the
    /// outer wall, roll cages, a strapped stack of flat cardboard, a broom and a bin. Kept off the shutter run, the
    /// dock controller and the room's item patch (by the divider door).
    /// </summary>
    public static partial class MallBuilder
    {
        static void LoadingDockDetail(Transform g, float y)
        {
            float q = MallLayout.QuadrantOuter, o = MallLayout.OuterWall;
            var rng = new System.Random(4401);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Vector3 P(float x, float h, float z) => new Vector3(x, y + h, z);

            Material rubber = ArtLibrary.Plain("Dock_Rubber", new Color(0.05f, 0.05f, 0.05f), 0.25f);
            Material palletWood = ArtLibrary.Plain("Pallet_Wood", new Color(0.55f, 0.43f, 0.28f), 0.2f);
            Material wrap = ArtLibrary.Plain("Shrink_Wrap", new Color(0.78f, 0.8f, 0.82f), 0.85f);
            Material jackRed = ArtLibrary.Plain("PalletTruck_Red", new Color(0.6f, 0.08f, 0.05f), 0.5f);
            Material cageMat = ArtLibrary.Plain("RollCage_Zinc", new Color(0.55f, 0.57f, 0.58f), 0.6f, 0.8f);
            Material lampAmber = ArtLibrary.Plain("DockLamp_Lens", new Color(0.6f, 0.5f, 0.3f), 0.7f, 0f, new Color(0.9f, 0.7f, 0.35f), false);
            Material stain = ArtLibrary.Plain("Oil_Stain", new Color(0.03f, 0.03f, 0.03f), 0.85f);

            // ---- each dock door: hazard edging, bumpers either side, a swing-arm dock lamp, painted bay number ----
            int bay = 1;
            foreach (var (z0, z1) in new[] { (21f, 25.5f), (27f, 31.5f) })
            {
                float zc = (z0 + z1) * 0.5f;
                // leveller lip hazard stripes (diagonal yellow/black blocks)
                for (float z = z0 + 0.35f; z < z1 - 0.35f; z += 0.4f)
                    Detail("Leveller hazard stripe", g, P(q - 3.02f, 0.055f, z), new Vector3(0.12f, 0.01f, 0.2f), yellowPaint, Quaternion.Euler(0f, 45f, 0f));
                Detail("Leveller edge", g, P(q - 3.0f, 0.03f, zc), new Vector3(0.08f, 0.06f, z1 - z0 - 0.6f), blackMat);
                // painted floor line in front of the door, and the bay number
                Detail("Dock bay line", g, P(q - 3.4f, 0.003f, z0 + 0.1f), new Vector3(2.6f, 0.005f, 0.1f), yellowPaint);
                Detail("Dock bay line", g, P(q - 3.4f, 0.003f, z1 - 0.1f), new Vector3(2.6f, 0.005f, 0.1f), yellowPaint);
                TextMesh num = EditorBuild.Label(g, $"BAY {bay}", P(q - 4.2f, 0.01f, zc), 0f, 0.12f);
                num.transform.rotation = Quaternion.Euler(90f, -90f, 0f);
                num.color = new Color(0.85f, 0.7f, 0.15f);
                // rubber bumpers on the wall each side of the opening
                foreach (float zb in new[] { z0 - 0.25f, z1 + 0.25f })
                {
                    if (zb > 31.8f) continue;
                    Detail("Dock bumper", g, P(q - 0.42f, 0.45f, zb), new Vector3(0.18f, 0.5f, 0.28f), rubber);
                    Detail("Dock bumper plate", g, P(q - 0.32f, 0.45f, zb), new Vector3(0.04f, 0.6f, 0.36f), metal);
                }
                // swing-arm dock lamp above the door frame
                float za = z0 - 0.45f;
                Detail("Dock lamp bracket", g, P(q - 0.34f, 3.2f, za), new Vector3(0.06f, 0.2f, 0.12f), plantMat);
                Cyl("Dock lamp arm", g, P(q - 0.34f, 3.2f, za), P(q - 1.3f, 3.15f, za + 0.5f), 0.04f, plantMat);
                Cyl("Dock lamp head", g, P(q - 1.3f, 3.15f, za + 0.5f), P(q - 1.38f, 3.0f, za + 0.62f), 0.24f, plantMat);
                Detail("Dock lamp lens", g, P(q - 1.38f, 2.99f, za + 0.62f), new Vector3(0.18f, 0.02f, 0.18f), lampAmber);
                // traffic lights beside the door (red lit: dock not ready)
                Vector3 tl = P(q - 0.3f, 1.7f, z1 + 0.6f > 31.8f ? z0 - 0.75f : z1 + 0.6f);
                Detail("Dock traffic light", g, tl, new Vector3(0.12f, 0.36f, 0.16f), blackMat);
                Detail("Dock traffic light red", g, tl + new Vector3(-0.065f, 0.08f, 0f), new Vector3(0.01f, 0.1f, 0.1f), ArtLibrary.Plain("DockLight_Red", new Color(0.5f, 0.05f, 0.03f), 0.6f, 0f, new Color(2.4f, 0.15f, 0.08f), false));
                Detail("Dock traffic light green", g, tl + new Vector3(-0.065f, -0.08f, 0f), new Vector3(0.01f, 0.1f, 0.1f), ArtLibrary.Plain("DockLight_Off", new Color(0.04f, 0.12f, 0.05f), 0.6f));
                bay++;
            }
            Sign(g, "DRIVERS MUST REPORT TO GOODS IN\nNO UNAUTHORISED PERSONS ON THE DOCK", P(q - 0.32f, 2.35f, 26.25f), Vector3.left, 1.3f, 0.016f);

            // ---- oil and tyre scuffs where the pallet truck runs ----
            for (int k = 0; k < 7; k++)
                Detail("Oil stain", g, P(R(39.5f, 43.5f), 0.004f, R(20.5f, 27.5f)), new Vector3(R(0.2f, 0.7f), 0.004f, R(0.15f, 0.5f)), stain, Quaternion.Euler(0f, R(0f, 180f), 0f));

            // ---- pallet truck, abandoned mid-floor with its handle up ----
            Vector3 pj = P(41.3f, 0f, 24.6f);
            Quaternion pjr = Quaternion.Euler(0f, 72f, 0f);
            Vector3 pf = pjr * Vector3.forward, pr = pjr * Vector3.right;
            foreach (float s in new[] { -0.27f, 0.27f })
                EditorBuild.Box("Pallet truck fork", g, pj + pr * s + pf * 0.55f + Vector3.up * 0.04f, new Vector3(0.16f, 0.08f, 1.15f), jackRed, SurfaceType.Metal, pjr);
            Detail("Pallet truck body", g, pj - pf * 0.1f + Vector3.up * 0.2f, new Vector3(0.7f, 0.3f, 0.2f), jackRed, pjr);
            Cyl("Pallet truck pump", g, pj - pf * 0.12f + Vector3.up * 0.3f, pj - pf * 0.12f + Vector3.up * 0.55f, 0.08f, metal);
            Cyl("Pallet truck handle", g, pj - pf * 0.12f + Vector3.up * 0.55f, pj - pf * 0.45f + Vector3.up * 1.2f, 0.04f, jackRed);
            Detail("Pallet truck grip", g, pj - pf * 0.47f + Vector3.up * 1.22f, new Vector3(0.3f, 0.05f, 0.05f), blackMat, pjr);
            foreach (float s in new[] { -0.14f, 0.14f })
                Cyl("Pallet truck wheel", g, pj - pf * 0.15f + pr * (s - 0.04f) + Vector3.up * 0.09f, pj - pf * 0.15f + pr * (s + 0.04f) + Vector3.up * 0.09f, 0.18f, blackMat);

            // ---- pallets along the outer wall: a wrapped load, a half-stripped one, a stack of empties ----
            void Pallet(Vector3 at, float yaw)
            {
                Quaternion r = Quaternion.Euler(0f, yaw, 0f);
                Vector3 f = r * Vector3.forward, s = r * Vector3.right;
                for (int k = -1; k <= 1; k++) Detail("Pallet block", g, at + s * k * 0.5f + Vector3.up * 0.05f, new Vector3(0.1f, 0.1f, 1.2f), palletWood, r);
                for (int k = 0; k < 5; k++) Detail("Pallet board", g, at + f * (-0.5f + k * 0.25f) + Vector3.up * 0.12f, new Vector3(1.0f, 0.03f, 0.12f), palletWood, r);
            }
            EditorBuild.Box("Pallets (stacked)", g, P(39.0f, 0.6f, 20.15f), new Vector3(1.2f, 1.2f, 1.0f), palletWood, SurfaceType.Wood, Quaternion.Euler(0f, 90f, 0f));
            for (int k = 0; k < 9; k++) Detail("Pallet edge", g, P(38.47f, 0.07f + k * 0.135f, 20.15f), new Vector3(0.02f, 0.03f, 1.0f), blackMat);
            Pallet(P(40.6f, 0f, 20.15f), 90f + R(-4f, 4f));
            EditorBuild.Box("Wrapped load", g, P(40.6f, 0.86f, 20.15f), new Vector3(1.1f, 1.45f, 0.95f), wrap, SurfaceType.Wood, Quaternion.Euler(0f, 90f + R(-4f, 4f), 0f));
            Pallet(P(42.2f, 0f, 20.2f), 90f + R(-6f, 6f));
            EditorBuild.Box("Half-unloaded boxes", g, P(42.0f, 0.43f, 20.1f), new Vector3(0.7f, 0.6f, 0.55f), cardboardMat, SurfaceType.Wood, Quaternion.Euler(0f, R(-8f, 8f), 0f));
            Detail("Torn wrap", g, P(42.6f, 0.2f, 20.6f), new Vector3(0.5f, 0.35f, 0.02f), wrap, Quaternion.Euler(20f, 30f, 10f));
            Detail("Torn wrap", g, P(42.9f, 0.01f, 21.2f), new Vector3(0.6f, 0.01f, 0.45f), wrap, Quaternion.Euler(0f, 50f, 0f));

            // ---- roll cages by the goods-in side, one tipped against another ----
            void RollCage(Vector3 at, float yaw, float tilt)
            {
                Quaternion r = Quaternion.Euler(0f, yaw, tilt);
                Vector3 f = r * Vector3.forward, s = r * Vector3.right, u = r * Vector3.up;
                Detail("Roll cage base", g, at + u * 0.15f, new Vector3(0.7f, 0.05f, 0.8f), cageMat, r);
                for (int a = -1; a <= 1; a += 2)
                for (int b = -1; b <= 1; b += 2)
                {
                    Cyl("Roll cage post", g, at + s * a * 0.34f + f * b * 0.39f + u * 0.15f, at + s * a * 0.34f + f * b * 0.39f + u * 1.75f, 0.025f, cageMat);
                    Cyl("Roll cage castor", g, at + s * a * 0.3f + f * b * 0.35f, at + s * a * 0.3f + f * b * 0.35f + u * 0.12f, 0.1f, blackMat);
                }
                for (int k = 1; k <= 4; k++)
                {
                    float h = 0.15f + k * 0.4f;
                    foreach (int a in new[] { -1, 1 })
                        Cyl("Roll cage rail", g, at + s * a * 0.34f - f * 0.39f + u * h, at + s * a * 0.34f + f * 0.39f + u * h, 0.015f, cageMat);
                    Cyl("Roll cage rail", g, at - s * 0.34f - f * 0.39f + u * h, at + s * 0.34f - f * 0.39f + u * h, 0.015f, cageMat);
                }
                var col = new GameObject("Roll cage (collider)");
                col.transform.SetParent(g, false);
                col.transform.position = at + u * 0.95f;
                col.transform.rotation = r;
                col.AddComponent<BoxCollider>().size = new Vector3(0.72f, 1.6f, 0.82f);
                col.isStatic = true;
            }
            RollCage(P(38.8f, 0f, 26.9f), 0f, 0f);
            RollCage(P(39.65f, 0f, 26.9f), 4f, 0f);
            RollCage(P(40.6f, 0.08f, 27.1f), -12f, -6f);
            for (int k = 0; k < 4; k++)
                EditorBuild.Box("Cage boxes", g, P(39.65f + R(-0.1f, 0.1f), 0.38f + k * 0.4f, 26.9f), new Vector3(0.6f, 0.34f, 0.65f), cardboardMat, SurfaceType.Wood, Quaternion.Euler(0f, R(-6f, 6f), 0f));

            // ---- flat-packed cardboard strapped for collection, a broom against it, a bin ----
            EditorBuild.Box("Baled cardboard", g, P(43.6f, 0.55f, 20.2f), new Vector3(1.2f, 1.1f, 0.8f), cardboardMat, SurfaceType.Wood);
            foreach (float s in new[] { -0.3f, 0.3f })
                Detail("Bale strap", g, P(43.6f + s, 0.55f, 20.2f), new Vector3(0.03f, 1.12f, 0.82f), blackMat);
            Cyl("Broom handle", g, P(43.05f, 0.05f, 20.75f), P(42.95f, 1.35f, 20.55f), 0.03f, ArtLibrary.Plain("Broom_Blue", new Color(0.1f, 0.25f, 0.55f), 0.4f));
            Detail("Broom head", g, P(43.15f, 0.06f, 20.95f), new Vector3(0.45f, 0.1f, 0.08f), blackMat, Quaternion.Euler(0f, -20f, 0f));
            Cyl("Dock bin", g, P(38.6f, 0f, 24.0f), P(38.6f, 0.95f, 24.0f), 0.6f, ArtLibrary.Plain("Bin_Grey", new Color(0.25f, 0.27f, 0.28f), 0.4f), true);
            Detail("Dock bin lid", g, P(38.6f, 0.98f, 24.0f), new Vector3(0.62f, 0.05f, 0.62f), blackMat);

            // ---- clipboard sheet pinned by the controller, and the dock light switch ----
            Detail("Delivery schedule", g, P(q - 0.31f, 1.5f, 27.1f), new Vector3(0.01f, 0.32f, 0.23f), whiteFixture);
            Sign(g, "DELIVERIES 14/02\n05:30  BAY 1  CHILLED\n06:15  BAY 2  AMBIENT\n07:00  BAY 1  GMT", P(q - 0.315f, 1.5f, 27.1f), Vector3.left, 0.22f, 0.006f, false);
            _ = o;
        }
    }
}
