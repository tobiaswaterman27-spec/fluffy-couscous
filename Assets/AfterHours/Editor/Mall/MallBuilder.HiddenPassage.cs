using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// The disused passage behind Screen 3: film cans stacked in towers against the outer wall, an old carbon-arc
    /// projector on a trolley under a dust sheet, rolled posters in a bin, a broken seat, one bare bulb on a flex,
    /// and a sign for a SCREEN 4 that the cinema never had.
    /// </summary>
    public static partial class MallBuilder
    {
        static void HiddenPassageDetail(Transform g, WingId w, float y)
        {
            Vector3 fwd = MallLayout.WingForward(w), rt = MallLayout.WingRight(w);
            Vector3 P(float u, float v, float h) => MallLayout.WingPoint(w, u, v, y + h);
            var rng = new System.Random(8008);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material canMat = ArtLibrary.Plain("Film_Can", new Color(0.45f, 0.47f, 0.48f), 0.55f, 0.8f);
            Material rust = ArtLibrary.Plain("Film_Can_Rust", new Color(0.4f, 0.22f, 0.1f), 0.3f, 0.5f);
            Material sheet = ArtLibrary.Plain("Dust_Sheet", new Color(0.7f, 0.68f, 0.62f), 0.15f);
            Material projGreen = ArtLibrary.Plain("Old_Projector", new Color(0.18f, 0.25f, 0.2f), 0.5f, 0.5f);
            Material bulb = ArtLibrary.Plain("Bare_Bulb", new Color(0.9f, 0.8f, 0.55f), 0.6f, 0f, new Color(1.6f, 1.2f, 0.6f), false);
            Material poster = ArtLibrary.Plain("Old_Poster_Roll", new Color(0.75f, 0.7f, 0.55f), 0.2f);
            Quaternion along = Quaternion.LookRotation(fwd);

            // towers of film cans against the outer wall, beyond the crates
            for (int t = 0; t < 5; t++)
            {
                float u = 115.6f + t * 0.9f;
                int n = 4 + rng.Next(9);
                for (int k = 0; k < n; k++)
                {
                    Vector3 c = P(u + R(-0.03f, 0.03f), 18.75f + R(-0.03f, 0.03f), 0.03f + k * 0.065f);
                    Cyl("Film can", g, c, c + Vector3.up * 0.06f, 0.42f, k % 5 == 3 ? rust : canMat);
                }
            }
            Cyl("Film can (rolled off)", g, P(120.8f, 17.6f, 0.21f) - fwd * 0.03f, P(120.8f, 17.6f, 0.21f) + fwd * 0.03f, 0.42f, rust);
            Detail("Film (unspooled)", g, P(120.2f, 17.4f, 0.004f), new Vector3(0.035f, 0.002f, 1.8f), blackMat, along * Quaternion.Euler(0f, 35f, 0f));

            // the old projector on its trolley under a dust sheet, half pulled off
            Vector3 pc = P(119.5f, 17.0f, 0f);
            EditorBuild.Box("Projector trolley", g, pc + Vector3.up * 0.35f, new Vector3(0.7f, 0.7f, 1.1f), metal, SurfaceType.Metal, along);
            Detail("Projector body", g, pc + Vector3.up * 1.0f, new Vector3(0.45f, 0.6f, 0.8f), projGreen, along);
            Cyl("Projector lens", g, pc + Vector3.up * 1.05f + fwd * 0.4f, pc + Vector3.up * 1.05f + fwd * 0.6f, 0.12f, blackMat);
            Cyl("Reel arm", g, pc + Vector3.up * 1.3f, pc + Vector3.up * 1.6f + fwd * 0.25f, 0.03f, projGreen);
            Cyl("Feed reel", g, pc + Vector3.up * 1.7f + fwd * 0.3f - rt * 0.02f, pc + Vector3.up * 1.7f + fwd * 0.3f + rt * 0.02f, 0.5f, canMat);
            Detail("Dust sheet", g, pc + Vector3.up * 1.2f - fwd * 0.2f, new Vector3(0.7f, 0.9f, 0.7f), sheet, along * Quaternion.Euler(8f, 0f, 4f));
            Detail("Dust sheet (fallen)", g, pc + Vector3.up * 0.02f - fwd * 0.75f + rt * 0.2f, new Vector3(0.9f, 0.03f, 0.7f), sheet, Quaternion.Euler(0f, 20f, 0f));

            // rolled posters in a bin, a broken seat
            Cyl("Poster bin", g, P(116.2f, 16.3f, 0f), P(116.2f, 16.3f, 0.6f), 0.4f, darkPanel);
            for (int k = 0; k < 7; k++)
                Cyl("Rolled poster", g, P(116.2f + R(-0.1f, 0.1f), 16.3f + R(-0.1f, 0.1f), 0.1f), P(116.2f + R(-0.25f, 0.25f), 16.3f + R(-0.25f, 0.25f), 1.0f + R(0f, 0.3f)), 0.07f, poster);
            Detail("Broken seat back", g, P(117.6f, 16.2f, 0.35f), new Vector3(0.5f, 0.6f, 0.1f), seatMat, along * Quaternion.Euler(-70f, 25f, 0f));
            Detail("Broken seat", g, P(117.9f, 16.5f, 0.06f), new Vector3(0.5f, 0.1f, 0.44f), seatMat, Quaternion.Euler(0f, 60f, 8f));

            // one bare bulb on a flex, near the hatch end
            Vector3 b = P(106.4f, 17.5f, 3.2f);
            Cyl("Flex", g, P(106.4f, 17.5f, 4.4f), b, 0.01f, blackMat);
            Ball("Bare bulb", g, b - Vector3.up * 0.05f, 0.08f, bulb);
            var lg = new GameObject("Bare bulb light");
            lg.transform.SetParent(g, false);
            lg.transform.localPosition = b - Vector3.up * 0.15f;
            Light l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.range = 4.5f; l.intensity = 0.35f; l.color = new Color(1f, 0.78f, 0.45f); l.shadows = LightShadows.Soft;
            l.lightmapBakeType = LightmapBakeType.Realtime;

            // a sign for the screen that was never there
            Vector3 s = P(105.32f, 17.45f, 2.3f);
            Detail("Old screen sign", g, s, new Vector3(1.6f, 0.4f, 0.04f), darkPanel, along);
            Sign(g, "SCREEN 4", s + fwd * 0.025f, fwd, 1.4f, 0.04f, false);
        }
    }
}
