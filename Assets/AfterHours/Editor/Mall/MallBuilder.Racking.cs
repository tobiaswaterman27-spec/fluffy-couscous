using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Store-room racking in place of solid blocks: blue uprights, orange beams, chipboard decks, and cardboard
    /// stock in uneven runs - boxes of different sizes, some half pulled out, gaps where things were taken, and
    /// now and then one fallen off onto the floor. One invisible collider covers the bay.
    /// </summary>
    public static partial class MallBuilder
    {
        static void StorageRack(Transform g, AreaRect r, float y, int n)
        {
            var rng = new System.Random(8800 + n);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            bool alongX = r.Width >= r.Depth;
            float len = alongX ? r.Width : r.Depth, dep = alongX ? r.Depth : r.Width;
            Vector2 c2 = r.Center;
            Vector3 c = new Vector3(c2.x, y, c2.y);
            Vector3 along = alongX ? Vector3.right : Vector3.forward, across = alongX ? Vector3.forward : Vector3.right;
            Quaternion rot = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
            const float H = 2.2f;
            Material upright = ArtLibrary.Plain("Rack_Upright", new Color(0.1f, 0.22f, 0.5f), 0.45f, 0.5f);
            Material beam = ArtLibrary.Plain("Rack_Beam", new Color(0.85f, 0.4f, 0.06f), 0.45f, 0.5f);
            Material deck = ArtLibrary.Plain("Rack_Deck", new Color(0.62f, 0.52f, 0.38f), 0.2f);
            Material tape = ArtLibrary.Plain("Packing_Tape", new Color(0.7f, 0.55f, 0.32f), 0.6f);
            Material label = whiteFixture;

            var col = new GameObject("Racking (collider)");
            col.transform.SetParent(g, false);
            col.transform.localPosition = c + Vector3.up * H * 0.5f;
            col.transform.localRotation = rot;
            col.AddComponent<BoxCollider>().size = new Vector3(len, H, dep);
            col.AddComponent<SurfaceMaterial>().type = SurfaceType.Metal;
            col.isStatic = true;

            // frames: uprights at each end and in the middle, front and back, with diagonal bracing
            foreach (float a in new[] { -len * 0.5f + 0.04f, 0f, len * 0.5f - 0.04f })
            {
                foreach (float s in new[] { -dep * 0.5f + 0.04f, dep * 0.5f - 0.04f })
                    Detail("Rack upright", g, c + along * a + across * s + Vector3.up * H * 0.5f, new Vector3(0.07f, H, 0.07f), upright, rot);
                for (int k = 0; k < 3; k++)
                    Detail("Rack brace", g, c + along * a + Vector3.up * (0.4f + k * 0.7f), new Vector3(0.025f, 0.025f, dep * 1.15f), upright, rot * Quaternion.Euler(k % 2 == 0 ? 35f : -35f, 0f, 0f));
                Detail("Rack foot", g, c + along * a + Vector3.up * 0.005f, new Vector3(0.14f, 0.01f, dep), upright, rot);
            }
            float[] levels = { 0.12f, 0.82f, 1.52f, H - 0.03f };
            foreach (float lv in levels)
            {
                foreach (float s in new[] { -dep * 0.5f + 0.04f, dep * 0.5f - 0.04f })
                    Detail("Rack beam", g, c + across * s + Vector3.up * lv, new Vector3(len, 0.09f, 0.05f), beam, rot);
                Detail("Rack deck", g, c + Vector3.up * (lv + 0.05f), new Vector3(len - 0.08f, 0.018f, dep - 0.08f), deck, rot);
                // bay label on the front beam
                Detail("Bin label", g, c - across * (dep * 0.5f - 0.012f) + along * R(-len * 0.4f, len * 0.4f) + Vector3.up * lv, new Vector3(0.12f, 0.05f, 0.004f), label, rot);
            }

            // stock on each level (the top one gets the light, bulky stuff)
            for (int li = 0; li < levels.Length; li++)
            {
                float floor = levels[li] + 0.06f;
                float maxH = li < levels.Length - 1 ? levels[li + 1] - floor - 0.1f : 0.5f;
                float s = -len * 0.5f + 0.1f;
                while (s < len * 0.5f - 0.25f)
                {
                    float bw = R(0.28f, 0.62f);
                    if (s + bw > len * 0.5f - 0.08f) break;
                    if (rng.NextDouble() < 0.18) { s += bw + 0.05f; continue; }   // taken
                    float bh = R(0.22f, maxH), bd = R(0.35f, dep - 0.1f);
                    float pull = rng.NextDouble() < 0.12 ? R(0.1f, 0.25f) : 0f;      // half pulled out
                    float yaw = R(-4f, 4f);
                    Vector3 bc = c + along * (s + bw * 0.5f) - across * (dep * 0.5f - bd * 0.5f - 0.05f + pull) + Vector3.up * (floor + bh * 0.5f);
                    Detail("Stock box", g, bc, new Vector3(bw, bh, bd), cardboardMat, rot * Quaternion.Euler(0f, yaw, 0f));
                    Detail("Box tape", g, bc + Vector3.up * (bh * 0.5f + 0.002f), new Vector3(0.05f, 0.003f, bd), tape, rot * Quaternion.Euler(0f, yaw, 0f));
                    if (rng.NextDouble() < 0.3) Detail("Box label", g, bc - across * (bd * 0.5f + 0.002f), new Vector3(0.1f, 0.07f, 0.003f), label, rot * Quaternion.Euler(0f, yaw, 0f));
                    // a second, smaller box stacked on top now and then
                    if (bh < maxH * 0.5f && rng.NextDouble() < 0.4)
                    {
                        float h2 = R(0.15f, maxH - bh - 0.02f);
                        Detail("Stock box", g, bc + Vector3.up * (bh * 0.5f + h2 * 0.5f), new Vector3(bw * 0.8f, h2, bd * 0.8f), cardboardMat, rot * Quaternion.Euler(0f, yaw + R(-8f, 8f), 0f));
                    }
                    s += bw + R(0.02f, 0.08f);
                }
            }
            // one fallen off onto the floor in front, burst open
            if (n % 5 == 2)
            {
                Vector3 fc = c - across * (dep * 0.5f + 0.45f) + along * R(-len * 0.3f, len * 0.3f) + Vector3.up * 0.18f;
                Detail("Fallen box", g, fc, new Vector3(0.45f, 0.36f, 0.4f), cardboardMat, Quaternion.Euler(R(-15f, 15f), R(0f, 90f), R(70f, 100f)));
                for (int k = 0; k < 4; k++)
                    Detail("Spilled packet", g, fc + new Vector3(R(-0.4f, 0.4f), -0.17f, R(-0.4f, 0.4f)), new Vector3(R(0.08f, 0.16f), 0.03f, R(0.1f, 0.2f)), whiteFixture, Quaternion.Euler(0f, R(0f, 180f), 0f));
            }
        }
    }
}
