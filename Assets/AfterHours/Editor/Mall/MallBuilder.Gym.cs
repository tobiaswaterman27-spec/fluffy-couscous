using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Pulse Gym, fitted out as a gym rather than a shop: a row of treadmills facing the glass, exercise bikes
    /// behind them, a rubber-floored free-weights zone at the back with a mirror wall, dumbbell racks, benches and a
    /// squat rack, kettlebells, rolled mats and swiss balls, a water fountain and a towel bin.
    /// </summary>
    public static partial class MallBuilder
    {
        static void GymFloor(Transform g, WingId w, float y, float u0, float end)
        {
            Vector3 fwd = MallLayout.WingForward(w), rt = MallLayout.WingRight(w);
            Quaternion faceGlass = Quaternion.LookRotation(-fwd), faceBack = Quaternion.LookRotation(fwd);
            Vector3 P(float u, float v, float h) => MallLayout.WingPoint(w, u, v, y + h);
            var rng = new System.Random(7070);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            Material frame = ArtLibrary.Plain("Gym_Frame", new Color(0.12f, 0.12f, 0.13f), 0.55f, 0.6f);
            Material accent = ArtLibrary.Plain("Gym_Accent", new Color(0.85f, 0.2f, 0.05f), 0.5f);
            Material belt = ArtLibrary.Plain("Treadmill_Belt", new Color(0.04f, 0.04f, 0.045f), 0.15f);
            Material console = ArtLibrary.Plain("Gym_Console", new Color(0.03f, 0.05f, 0.08f), 0.9f, 0f, new Color(0.05f, 0.2f, 0.35f), false);
            Material rubber = ArtLibrary.Plain("Gym_Rubber_Floor", new Color(0.06f, 0.06f, 0.065f), 0.15f);
            Material pad = ArtLibrary.Plain("Gym_Pad", new Color(0.08f, 0.08f, 0.09f), 0.35f);
            Material iron = ArtLibrary.Plain("Iron_Plate", new Color(0.15f, 0.15f, 0.16f), 0.45f, 0.7f);
            Material chromeBar = ArtLibrary.Plain("Bar_Chrome", new Color(0.7f, 0.72f, 0.74f), 0.85f, 1f);

            void Collider(string n, Vector3 c, Vector3 size, Quaternion r)
            {
                var go = new GameObject(n);
                go.transform.SetParent(g, false);
                go.transform.localPosition = c; go.transform.localRotation = r;
                go.AddComponent<BoxCollider>().size = size;
                go.AddComponent<SurfaceMaterial>().type = SurfaceType.Metal;
                go.isStatic = true;
            }

            // ---- treadmills facing the shopfront glass ----
            for (float v = -16f; v <= 16.1f; v += 1.6f)
            {
                if (Mathf.Abs(v) < 2.5f) continue;   // the walkway in from the doors
                float u = u0 + 6.5f;
                Collider("Treadmill (collider)", P(u, v, 0.5f), new Vector3(0.85f, 1.0f, 1.95f), faceGlass);
                Detail("Treadmill deck", g, P(u, v, 0.12f), new Vector3(0.85f, 0.2f, 1.9f), frame, faceGlass);
                Detail("Treadmill belt", g, P(u + 0.05f, v, 0.225f), new Vector3(0.55f, 0.01f, 1.6f), belt, faceGlass);
                Detail("Treadmill hood", g, P(u - 0.85f, v, 0.25f), new Vector3(0.85f, 0.2f, 0.25f), accent, faceGlass);
                foreach (float s in new[] { -0.38f, 0.38f })
                {
                    Cyl("Treadmill upright", g, P(u - 0.85f, v + s, 0.25f), P(u - 0.75f, v + s, 1.3f), 0.06f, frame);
                    Cyl("Treadmill handrail", g, P(u - 0.75f, v + s, 1.05f), P(u - 0.25f, v + s, 1.0f), 0.04f, chromeBar);
                }
                Detail("Treadmill console", g, P(u - 0.78f, v, 1.35f), new Vector3(0.8f, 0.3f, 0.12f), frame, faceGlass * Quaternion.Euler(-25f, 0f, 0f));
                Detail("Treadmill screen", g, P(u - 0.72f, v, 1.37f), new Vector3(0.4f, 0.2f, 0.01f), console, faceGlass * Quaternion.Euler(-25f, 0f, 0f));
                if (rng.NextDouble() < 0.2) Detail("Towel (left)", g, P(u - 0.5f, v + 0.38f, 1.0f), new Vector3(0.05f, 0.3f, 0.3f), whiteFixture, faceGlass * Quaternion.Euler(0f, 0f, 10f));
            }

            // ---- exercise bikes behind them ----
            for (float v = -15.5f; v <= 15.6f; v += 1.4f)
            {
                if (Mathf.Abs(v) < 2.5f) continue;
                float u = u0 + 10.5f;
                Collider("Bike (collider)", P(u, v, 0.6f), new Vector3(0.6f, 1.2f, 1.2f), faceGlass);
                Detail("Bike base", g, P(u, v, 0.04f), new Vector3(0.5f, 0.08f, 1.1f), frame, faceGlass);
                Cyl("Bike frame", g, P(u + 0.35f, v, 0.08f), P(u, v, 0.75f), 0.08f, frame);
                Cyl("Bike flywheel", g, P(u - 0.3f, v - 0.04f, 0.4f), P(u - 0.3f, v + 0.04f, 0.4f), 0.5f, accent);
                Cyl("Bike seat post", g, P(u + 0.1f, v, 0.75f), P(u + 0.15f, v, 0.95f), 0.04f, chromeBar);
                Detail("Bike seat", g, P(u + 0.15f, v, 0.97f), new Vector3(0.2f, 0.06f, 0.28f), pad, faceGlass);
                Cyl("Bike column", g, P(u - 0.2f, v, 0.4f), P(u - 0.35f, v, 1.15f), 0.05f, frame);
                Detail("Bike bars", g, P(u - 0.35f, v, 1.16f), new Vector3(0.45f, 0.04f, 0.08f), frame, faceGlass);
                Detail("Bike console", g, P(u - 0.4f, v, 1.25f), new Vector3(0.2f, 0.12f, 0.03f), console, faceGlass * Quaternion.Euler(-30f, 0f, 0f));
                foreach (float s in new[] { -0.12f, 0.12f })
                    Detail("Bike pedal", g, P(u - 0.1f + s * 0.8f, v + s * 1.5f, 0.35f + s), new Vector3(0.1f, 0.025f, 0.06f), frame, faceGlass);
            }

            // ---- free weights zone at the back: rubber floor, mirror wall, racks ----
            float wz = end - 9f;
            Detail("Rubber floor", g, P((wz + end - 0.3f) * 0.5f, 0f, 0.006f), new Vector3(36f, 0.012f, end - 0.3f - wz), rubber, faceBack);
            Detail("Mirror wall", g, P(end - 0.32f, 0f, 1.3f), new Vector3(24f, 1.9f, 0.015f), mirrorMat, faceGlass);
            foreach (float v0 in new[] { -11f, 3f })
            {
                // dumbbell rack: two sloped tiers of pairs, 2.5 kg to 25 kg
                float u = end - 1.3f;
                Collider("Dumbbell rack (collider)", P(u, v0 + 4f, 0.45f), new Vector3(8f, 0.9f, 0.7f), faceBack);
                for (int k = 0; k <= 4; k++)
                    Detail("Rack leg", g, P(u, v0 + k * 2f, 0.4f), new Vector3(0.06f, 0.8f, 0.6f), frame, faceBack);
                for (int tier = 0; tier < 2; tier++)
                {
                    float h = 0.45f + tier * 0.35f, du = tier == 0 ? -0.15f : 0.15f;
                    Detail("Rack tier", g, P(u + du, v0 + 4f, h), new Vector3(8f, 0.04f, 0.3f), frame, faceBack * Quaternion.Euler(-12f, 0f, 0f));
                    for (int k = 0; k < 10; k++)
                    {
                        if (rng.NextDouble() < 0.12) continue;
                        float size = 0.12f + (tier * 10 + k) * 0.006f;
                        float vv = v0 + 0.35f + k * 0.75f;
                        foreach (float s in new[] { -0.12f, 0.12f })
                        {
                            Vector3 a = P(u + du, vv + s - 0.12f, h + 0.02f + size * 0.5f), b = P(u + du, vv + s + 0.12f, h + 0.02f + size * 0.5f);
                            Cyl("Dumbbell handle", g, a, b, 0.035f, chromeBar);
                            Cyl("Dumbbell head", g, a - (b - a).normalized * 0.06f, a, size, iron);
                            Cyl("Dumbbell head", g, b, b + (b - a).normalized * 0.06f, size, iron);
                        }
                    }
                }
            }
            // a couple of dumbbells left on the floor
            for (int k = 0; k < 3; k++)
            {
                Vector3 c = P(end - R(3f, 5.5f), R(-9f, 9f), 0.1f);
                Vector3 d = Quaternion.Euler(0f, R(0f, 180f), 0f) * Vector3.forward;
                Cyl("Dumbbell (floor)", g, c - d * 0.12f, c + d * 0.12f, 0.035f, chromeBar);
                Cyl("Dumbbell head", g, c - d * 0.18f, c - d * 0.12f, 0.2f, iron);
                Cyl("Dumbbell head", g, c + d * 0.12f, c + d * 0.18f, 0.2f, iron);
            }

            // benches and a squat rack in the middle of the zone
            foreach (float v in new[] { -6f, 0f, 6f })
            {
                float u = end - 5.2f;
                if (v == 0f)
                {
                    // squat rack: four posts, safeties, a loaded bar on the hooks
                    Collider("Squat rack (collider)", P(u, v, 1.15f), new Vector3(1.4f, 2.3f, 1.3f), faceBack);
                    foreach (float a in new[] { -0.6f, 0.6f })
                    foreach (float b in new[] { -0.55f, 0.55f })
                        Detail("Rack post", g, P(u + b, v + a, 1.15f), new Vector3(0.08f, 2.3f, 0.08f), frame, faceBack);
                    foreach (float a in new[] { -0.6f, 0.6f })
                    {
                        Detail("Rack top bar", g, P(u, v + a, 2.27f), new Vector3(0.08f, 0.08f, 1.18f), frame, faceBack);
                        Detail("Safety arm", g, P(u, v + a, 0.75f), new Vector3(0.06f, 0.06f, 1.18f), accent, faceBack);
                    }
                    Vector3 l = P(u - 0.55f, v - 1.1f, 1.45f), r = P(u - 0.55f, v + 1.1f, 1.45f);
                    Cyl("Barbell", g, l, r, 0.03f, chromeBar);
                    foreach (Vector3 e in new[] { l, r })
                    {
                        Vector3 dir = (e - (l + r) * 0.5f).normalized;
                        Cyl("Plate (20)", g, e - dir * 0.18f, e - dir * 0.13f, 0.45f, iron);
                        Cyl("Plate (10)", g, e - dir * 0.13f, e - dir * 0.1f, 0.3f, iron);
                    }
                }
                else
                {
                    Collider("Weight bench (collider)", P(u, v, 0.25f), new Vector3(0.35f, 0.5f, 1.25f), faceBack);
                    Detail("Bench pad", g, P(u, v, 0.45f), new Vector3(0.3f, 0.08f, 1.2f), pad, faceBack);
                    Detail("Bench frame", g, P(u, v, 0.2f), new Vector3(0.08f, 0.38f, 1.0f), frame, faceBack);
                    foreach (float s in new[] { -0.55f, 0.55f })
                        Detail("Bench foot", g, P(u + s, v, 0.03f), new Vector3(0.4f, 0.05f, 0.06f), frame, faceBack * Quaternion.Euler(0f, 90f, 0f));
                }
            }

            // kettlebells, rolled mats, swiss balls along the east side
            Material[] ballCols = { ArtLibrary.Plain("SwissBall_Blue", new Color(0.15f, 0.3f, 0.65f), 0.6f), ArtLibrary.Plain("SwissBall_Grey", new Color(0.4f, 0.4f, 0.42f), 0.6f) };
            for (int k = 0; k < 6; k++)
            {
                Vector3 c = P(end - 3f - k * 0.45f, 15.5f, 0f);
                Ball("Kettlebell", g, c + Vector3.up * 0.12f, 0.22f, iron);
                Cyl("Kettlebell handle", g, c + Vector3.up * 0.22f - rt * 0.06f, c + Vector3.up * 0.3f, 0.025f, iron);
            }
            for (int k = 0; k < 5; k++)
                Cyl("Rolled mat", g, P(end - 7f, 14.6f + k * 0.2f, 0f), P(end - 7f, 14.6f + k * 0.2f, 0.62f), 0.17f, k % 2 == 0 ? accent : pad);
            for (int k = 0; k < 3; k++)
                Ball("Swiss ball", g, P(end - 8.5f - k * 0.8f, 15.6f + (k % 2) * 0.4f, 0.33f), 0.65f, ballCols[k % 2], true);

            // water fountain and towel bin by the west wall
            Vector3 wf = P(u0 + 13f, -18.7f, 0f);
            EditorBuild.Box("Water fountain", g, wf + Vector3.up * 0.5f, new Vector3(0.45f, 1.0f, 0.4f), metal, SurfaceType.Metal, Quaternion.LookRotation(rt));
            Detail("Fountain basin", g, wf + Vector3.up * 1.0f + rt * 0.05f, new Vector3(0.4f, 0.04f, 0.32f), chromeBar, Quaternion.LookRotation(rt));
            Cyl("Towel bin", g, P(u0 + 14f, -18.7f, 0f), P(u0 + 14f, -18.7f, 0.8f), 0.5f, ArtLibrary.Plain("TowelBin_Grey", new Color(0.35f, 0.36f, 0.38f), 0.4f), true);
            for (int k = 0; k < 3; k++)
                Detail("Used towel", g, P(u0 + 14f + R(-0.1f, 0.1f), -18.7f + R(-0.1f, 0.1f), 0.82f + k * 0.04f), new Vector3(0.35f, 0.04f, 0.3f), whiteFixture, Quaternion.Euler(R(-10f, 10f), R(0f, 180f), R(-10f, 10f)));
            Sign(g, "PLEASE WIPE DOWN EQUIPMENT AFTER USE", P(u0 + 13.5f, -19.0f, 2.1f), rt, 2.4f, 0.025f);
            Sign(g, "PULSE GYM  -  NO PAIN NO GAIN", P(end - 0.33f, 0f, 2.6f), -fwd, 6f, 0.05f, false);
        }
    }
}
