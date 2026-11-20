using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// The service tunnel out to the pressure door: two lagged mains on brackets along the north wall, a cable tray
    /// along the south wall, painted distance markers every 20 m, light cages (some smashed), standing water in the dips,
    /// a hard hat and a torch-less torch holder, and a chalk tally someone kept on the wall.
    /// </summary>
    public static partial class MallBuilder
    {
        static void TunnelDetail(Transform g)
        {
            float y = MallLayout.CarParkY;
            const float z0 = -20f, z1 = -17f, x0 = -100.3f, x1 = -179.6f;
            var rng = new System.Random(6060);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material lagging = ArtLibrary.Plain("Pipe_Lagging", new Color(0.78f, 0.78f, 0.74f), 0.3f);
            Material pipeGreen = ArtLibrary.Plain("Pipe_Green", new Color(0.15f, 0.32f, 0.18f), 0.5f, 0.4f);
            Material tray = ArtLibrary.Plain("Cable_Tray", new Color(0.6f, 0.62f, 0.62f), 0.6f, 0.8f);
            Material cable = ArtLibrary.Plain("Cable_Black", new Color(0.03f, 0.03f, 0.03f), 0.5f);
            Material water = ArtLibrary.Plain("Wet_Sheen", new Color(0.05f, 0.06f, 0.07f), 0.97f);
            Material chalk = ArtLibrary.Plain("Chalk", new Color(0.85f, 0.85f, 0.82f), 0.1f);
            Material cage = ArtLibrary.Plain("Light_Cage", new Color(0.15f, 0.15f, 0.15f), 0.5f, 0.6f);

            float len = x0 - x1;
            Vector3 along = Vector3.left;
            // mains along the north wall (z1 side), on brackets every 3 m
            Cyl("Main (lagged)", g, new Vector3(x0, y + 2.3f, z1 - 0.3f), new Vector3(x1, y + 2.3f, z1 - 0.3f), 0.32f, lagging);
            Cyl("Main (green)", g, new Vector3(x0, y + 1.85f, z1 - 0.25f), new Vector3(x1, y + 1.85f, z1 - 0.25f), 0.2f, pipeGreen);
            for (float x = x0 - 1.5f; x > x1; x -= 3f)
            {
                Detail("Pipe bracket", g, new Vector3(x, y + 2.1f, z1 - 0.2f), new Vector3(0.06f, 0.65f, 0.4f), metal);
                Detail("Pipe saddle", g, new Vector3(x, y + 2.13f, z1 - 0.3f), new Vector3(0.08f, 0.03f, 0.36f), metal);
            }
            // cable tray along the south wall (z0 side), cables drooping out of it here and there
            Detail("Cable tray", g, new Vector3((x0 + x1) * 0.5f, y + 2.5f, z0 + 0.2f), new Vector3(len, 0.06f, 0.3f), tray);
            Detail("Cable bundle", g, new Vector3((x0 + x1) * 0.5f, y + 2.55f, z0 + 0.2f), new Vector3(len, 0.06f, 0.22f), cable);
            for (int k = 0; k < 4; k++)
            {
                float x = R(x1 + 5f, x0 - 5f);
                Cyl("Drooping cable", g, new Vector3(x, y + 2.5f, z0 + 0.25f), new Vector3(x - 1.2f, y + 1.8f, z0 + 0.3f), 0.025f, cable);
                Cyl("Drooping cable", g, new Vector3(x - 1.2f, y + 1.8f, z0 + 0.3f), new Vector3(x - 2.4f, y + 2.5f, z0 + 0.25f), 0.025f, cable);
            }
            // distance markers every 20 m, painted on the south wall
            for (int m = 20; m < len; m += 20)
            {
                Vector3 p = new Vector3(x0 - m, y + 1.2f, z0 + 0.01f);
                Detail("Distance marker", g, p, new Vector3(0.6f, 0.35f, 0.004f), yellowPaint, Quaternion.LookRotation(Vector3.forward));
                Sign(g, m + " m", p + Vector3.forward * 0.004f, Vector3.forward, 0.5f, 0.03f, false);
            }
            // cages round the tunnel lights (the lights are every 16 m from x = -110), two smashed
            int idx = 0;
            for (float x = -110f; x > -180f; x -= 16f, idx++)
            {
                Vector3 c = new Vector3(x, -1.3f, -18.5f);
                Detail("Bulkhead fitting", g, c + Vector3.up * 0.15f, new Vector3(0.35f, 0.1f, 0.2f), cage);
                bool smashed = idx == 2 || idx == 4;
                Detail(smashed ? "Bulkhead lens (smashed)" : "Bulkhead lens", g, c + Vector3.up * 0.07f, new Vector3(0.3f, 0.06f, 0.15f), smashed ? blackMat : whiteFixture);
                if (smashed)
                    for (int s = 0; s < 6; s++)
                        Detail("Glass shard", g, new Vector3(x + R(-0.5f, 0.5f), y + 0.004f, -18.5f + R(-0.6f, 0.6f)), new Vector3(R(0.02f, 0.06f), 0.003f, R(0.02f, 0.05f)), glassMat, Quaternion.Euler(0f, R(0f, 180f), 0f));
            }
            // standing water in the dips
            for (int k = 0; k < 6; k++)
                Detail("Tunnel puddle", g, new Vector3(R(x1 + 3f, x0 - 3f), y + 0.003f, R(-19.4f, -17.6f)), new Vector3(R(1.0f, 3.0f), 0.003f, R(0.5f, 1.4f)), water, Quaternion.Euler(0f, R(-10f, 10f), 0f));
            // a hard hat on its side, an empty torch bracket, a chalked tally on the wall
            Detail("Hard hat", g, new Vector3(-147.2f, y + 0.1f, -19.2f), new Vector3(0.3f, 0.18f, 0.24f), yellowPaint, Quaternion.Euler(0f, 30f, 80f));
            Detail("Torch bracket (empty)", g, new Vector3(-131f, y + 1.4f, z1 - 0.02f), new Vector3(0.12f, 0.2f, 0.04f), redMat);
            for (int k = 0; k < 4; k++)
            {
                Vector3 t = new Vector3(-163f - k * 0.08f, y + 1.5f, z0 + 0.008f);
                Detail("Tally mark", g, t, new Vector3(0.012f, 0.22f, 0.003f), chalk, Quaternion.LookRotation(Vector3.forward) * Quaternion.Euler(0f, 0f, R(-6f, 6f)));
            }
            Detail("Tally strike", g, new Vector3(-163.12f, y + 1.5f, z0 + 0.009f), new Vector3(0.4f, 0.012f, 0.003f), chalk, Quaternion.LookRotation(Vector3.forward) * Quaternion.Euler(0f, 0f, 25f));
            Sign(g, "NO ENTRY BEYOND\nTHIS POINT WITHOUT\nA PERMIT TO WORK", new Vector3(-104f, y + 1.7f, z1 - 0.02f), Vector3.back, 1.2f, 0.018f);
            _ = along;
        }
    }
}
