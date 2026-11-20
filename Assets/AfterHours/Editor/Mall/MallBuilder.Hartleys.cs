using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Hartley's department store: a raised display plinth inside the doors with mannequins under a hanging SALE
    /// banner (ground floor), a free-standing store guide on each floor, and on the ground floor the old long-case
    /// clock the store has had since it opened - stopped at 3:17.
    /// </summary>
    public static partial class MallBuilder
    {
        static void HartleysFeature(Transform g, MallFloor f, WingId w, float y, float u0, float end)
        {
            Vector3 fwd = MallLayout.WingForward(w), rt = MallLayout.WingRight(w);
            Vector3 P(float u, float v, float h) => MallLayout.WingPoint(w, u, v, y + h);
            Quaternion toDoors = Quaternion.LookRotation(-fwd);
            Material plinth = ArtLibrary.Plain("Plinth_Black_Gloss", new Color(0.04f, 0.04f, 0.045f), 0.9f);
            Material banner = ArtLibrary.Plain("Sale_Banner", new Color(0.7f, 0.05f, 0.08f), 0.3f);
            Material walnut = ArtLibrary.Plain("Walnut", new Color(0.25f, 0.14f, 0.08f), 0.55f);
            Material brass = ArtLibrary.Plain("Clock_Brass", new Color(0.75f, 0.6f, 0.25f), 0.85f, 1f);
            Material dial = ArtLibrary.Plain("Clock_Dial", new Color(0.92f, 0.9f, 0.82f), 0.6f);

            if (f == MallFloor.Ground)
            {
                // display plinth with three mannequins facing the doors
                EditorBuild.Box("Display plinth", g, P(u0 + 6f, 0f, 0.15f), new Vector3(5.5f, 0.3f, 2.4f), plinth, SurfaceType.Wood, Quaternion.LookRotation(fwd));
                for (int k = -1; k <= 1; k++)
                    Mannequin(g, P(u0 + 6f + (k == 0 ? -0.3f : 0.2f), k * 1.6f, 0.3f), toDoors);
                Cyl("Banner wire", g, P(u0 + 6f, -2.8f, 2.6f), P(u0 + 6f, -2.8f, 4.4f), 0.01f, metal);
                Cyl("Banner wire", g, P(u0 + 6f, 2.8f, 2.6f), P(u0 + 6f, 2.8f, 4.4f), 0.01f, metal);
                Detail("Sale banner", g, P(u0 + 6f, 0f, 2.5f), new Vector3(5.8f, 0.8f, 0.02f), banner, toDoors);
                Sign(g, "WINTER SALE  -  UP TO 70% OFF", P(u0 + 6f, 0f, 2.5f) - fwd * 0.015f, -fwd, 5.4f, 0.05f, false);
                Sign(g, "WINTER SALE  -  UP TO 70% OFF", P(u0 + 6f, 0f, 2.5f) + fwd * 0.015f, fwd, 5.4f, 0.05f, false);

                // the long-case clock against the east wall, stopped
                Vector3 c = P(u0 + 9f, 18.6f, 0f);
                Quaternion face = Quaternion.LookRotation(-rt);
                EditorBuild.Box("Long-case clock", g, c + Vector3.up * 1.0f, new Vector3(0.55f, 2.0f, 0.35f), walnut, SurfaceType.Wood, face);
                Detail("Clock hood", g, c + Vector3.up * 2.15f, new Vector3(0.62f, 0.32f, 0.4f), walnut, face);
                Detail("Clock finial", g, c + Vector3.up * 2.36f, new Vector3(0.1f, 0.1f, 0.1f), brass, face);
                Vector3 dc = c + Vector3.up * 2.1f - rt * 0.205f;
                Cyl("Clock dial", g, dc, dc - rt * 0.01f, 0.34f, dial);
                Cyl("Clock bezel", g, dc + rt * 0.005f, dc - rt * 0.005f, 0.38f, brass);
                // 3:17 - hour hand just past the 3, minute hand on 17
                float hourA = (3f + 17f / 60f) / 12f * 360f, minA = 17f / 60f * 360f;
                Quaternion dialFace = Quaternion.LookRotation(-rt);
                Detail("Hour hand", g, dc - rt * 0.012f + (dialFace * (Quaternion.Euler(0f, 0f, -hourA) * Vector3.up)) * 0.05f, new Vector3(0.012f, 0.1f, 0.004f), blackMat, dialFace * Quaternion.Euler(0f, 0f, -hourA));
                Detail("Minute hand", g, dc - rt * 0.014f + (dialFace * (Quaternion.Euler(0f, 0f, -minA) * Vector3.up)) * 0.07f, new Vector3(0.008f, 0.14f, 0.004f), blackMat, dialFace * Quaternion.Euler(0f, 0f, -minA));
                Detail("Trunk window", g, c + Vector3.up * 1.2f - rt * 0.18f, new Vector3(0.24f, 0.7f, 0.01f), darkGlass, face);
                Cyl("Pendulum rod", g, c + Vector3.up * 1.55f - rt * 0.15f, c + Vector3.up * 1.0f - rt * 0.15f, 0.012f, brass);
                Cyl("Pendulum bob", g, c + Vector3.up * 0.98f - rt * 0.13f, c + Vector3.up * 0.98f - rt * 0.17f, 0.14f, brass);
                Sign(g, "HARTLEY'S  EST. 1931", c + Vector3.up * 0.4f - rt * 0.18f, -rt, 0.45f, 0.012f, false);
            }

            // hanging department signs
            string[] depts = f == MallFloor.Ground ? new[] { "BEAUTY", "WOMENSWEAR", "ACCESSORIES", "GIFTS" } : new[] { "MENSWEAR", "HOME", "BEDDING", "RESTAURANT" };
            Material hang = ArtLibrary.Plain("Dept_Sign", new Color(0.15f, 0.12f, 0.1f), 0.5f);
            for (int k = 0; k < depts.Length; k++)
            {
                Vector3 c = P(u0 + 11f + (k / 2) * 9f, k % 2 == 0 ? -9f : 9f, 3.7f);
                Detail("Department sign", g, c, new Vector3(2.2f, 0.45f, 0.05f), hang, toDoors);
                Sign(g, depts[k], c - fwd * 0.03f, -fwd, 2f, 0.04f, false);
                Sign(g, depts[k], c + fwd * 0.03f, fwd, 2f, 0.04f, false);
                foreach (float s2 in new[] { -0.9f, 0.9f })
                    Cyl("Sign wire", g, c + rt * s2 + Vector3.up * 0.22f, c + rt * s2 + Vector3.up * 0.8f, 0.006f, metal);
            }
            // store guide, free-standing, near the stair
            Vector3 sg = P(u0 + 4.6f, 9.5f, 0f);
            Detail("Store guide post", g, sg + Vector3.up * 0.9f, new Vector3(0.1f, 1.8f, 0.1f), metal, toDoors);
            Detail("Store guide board", g, sg + Vector3.up * 1.55f, new Vector3(0.9f, 1.0f, 0.04f), walnut, toDoors);
            Sign(g, "HARTLEY'S STORE GUIDE\n\nLEVEL 1\nMENSWEAR  -  HOME\nBEDDING  -  RESTAURANT\n\nGROUND\nBEAUTY  -  WOMENSWEAR\nACCESSORIES  -  GIFTS", sg + Vector3.up * 1.55f - fwd * 0.025f, -fwd, 0.8f, 0.012f, false);
        }
    }
}
