using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// The main entrance hall: two cash machines set in the side wall (one OUT OF SERVICE), a photo booth with its
    /// curtain half drawn, an umbrella-bag stand by the doors (a used bag dripping on the floor), wheelchairs to borrow,
    /// the opening hours on the wall by the doors, and a coin funnel for the children's hospital.
    /// </summary>
    public static partial class MallBuilder
    {
        static void EntranceHallDetail(Transform g, WingId w, float y, float u0, float end)
        {
            Vector3 fwd = MallLayout.WingForward(w), rt = MallLayout.WingRight(w);
            Quaternion toBack = Quaternion.LookRotation(-fwd);
            Vector3 P(float u, float v, float h) => MallLayout.WingPoint(w, u, v, y + h);
            float side = MallLayout.OuterWall;

            Material atmBody = ArtLibrary.Plain("ATM_Body", new Color(0.55f, 0.57f, 0.6f), 0.6f, 0.7f);
            Material atmScreen = ArtLibrary.Plain("ATM_Screen", new Color(0.05f, 0.1f, 0.18f), 0.9f, 0f, new Color(0.2f, 0.45f, 0.9f), false);
            Material atmDead = ArtLibrary.Plain("ATM_Screen_Off", new Color(0.03f, 0.03f, 0.035f), 0.9f);
            Material boothRed = ArtLibrary.Plain("PhotoBooth_Red", new Color(0.6f, 0.08f, 0.08f), 0.6f);
            Material curtain = ArtLibrary.Plain("PhotoBooth_Curtain", new Color(0.1f, 0.1f, 0.12f), 0.15f);
            Material bagMat = ArtLibrary.Plain("Umbrella_Bag", new Color(0.8f, 0.82f, 0.85f), 0.9f);
            Material chairBlue = ArtLibrary.Plain("Wheelchair_Seat", new Color(0.1f, 0.15f, 0.35f), 0.3f);

            // ---- cash machines in the west side wall, facing into the hall ----
            Quaternion fromWall = Quaternion.LookRotation(rt);
            for (int k = 0; k < 2; k++)
            {
                float u = u0 + 8f + k * 1.4f;
                Vector3 c = P(u, -side + 0.32f, 1.1f);
                EditorBuild.Box("Cash machine", g, c, new Vector3(0.9f, 2.2f, 0.36f), atmBody, SurfaceType.Metal, fromWall);
                Detail("Cash machine screen", g, P(u, -side + 0.505f, 1.38f), new Vector3(0.36f, 0.28f, 0.01f), k == 0 ? atmScreen : atmDead, fromWall * Quaternion.Euler(-12f, 0f, 0f));
                Detail("Cash machine keypad", g, P(u, -side + 0.56f, 1.02f), new Vector3(0.3f, 0.02f, 0.18f), metal, fromWall * Quaternion.Euler(-25f, 0f, 0f));
                Detail("Card slot", g, P(u + 0.28f, -side + 0.505f, 1.25f), new Vector3(0.1f, 0.03f, 0.01f), blackMat, fromWall);
                Detail("Cash slot", g, P(u, -side + 0.505f, 0.88f), new Vector3(0.36f, 0.04f, 0.01f), blackMat, fromWall);
                Detail("Cash machine hood", g, P(u, -side + 0.55f, 1.75f), new Vector3(0.9f, 0.04f, 0.3f), atmBody, fromWall);
                Sign(g, k == 0 ? "CASH MACHINE\nFREE WITHDRAWALS" : "SORRY\nOUT OF SERVICE", P(u, -side + 0.51f, 1.95f), rt, 0.8f, 0.016f, false);
            }

            // ---- photo booth on the east side ----
            {
                float u = u0 + 9.8f;
                Vector3 c = P(u, side - 0.75f, 1.05f);
                Quaternion r = Quaternion.LookRotation(-rt);
                EditorBuild.Box("Photo booth (back)", g, P(u, side - 0.38f, 1.05f), new Vector3(1.4f, 2.1f, 0.6f), boothRed, SurfaceType.Metal, r);
                EditorBuild.Box("Photo booth (side)", g, P(u - 0.68f, side - 1.0f, 1.05f), new Vector3(0.04f, 2.1f, 0.65f), boothRed, SurfaceType.Metal, r);
                Detail("Photo booth roof", g, P(u, side - 0.72f, 2.12f), new Vector3(1.42f, 0.06f, 1.3f), boothRed, r);
                Detail("Photo booth stool", g, P(u, side - 0.85f, 0.48f), new Vector3(0.36f, 0.06f, 0.36f), blackMat, r);
                Cyl("Photo booth stool post", g, P(u, side - 0.85f, 0f), P(u, side - 0.85f, 0.46f), 0.05f, metal);
                Detail("Booth curtain (half drawn)", g, P(u + 0.3f, side - 1.32f, 1.15f), new Vector3(0.75f, 1.55f, 0.02f), curtain, r);
                Cyl("Curtain rail", g, P(u - 0.66f, side - 1.33f, 1.95f), P(u + 0.66f, side - 1.33f, 1.95f), 0.025f, metal);
                Detail("Photo slot", g, P(u + 0.71f, side - 1.0f, 1.2f), new Vector3(0.02f, 0.12f, 0.08f), blackMat, r);
                Detail("Photo strip (uncollected)", g, P(u + 0.73f, side - 1.0f, 1.08f), new Vector3(0.005f, 0.2f, 0.05f), whiteFixture, r);
                Sign(g, "PHOTOS\n4 FOR £5", P(u, side - 1.36f, 2.0f), -rt, 1.2f, 0.022f, false);
                _ = c;
            }

            // ---- umbrella bag stand by the doors, and a used bag dripping on the floor ----
            {
                Vector3 b = P(end - 2.3f, 7.4f, 0f);
                Cyl("Umbrella stand post", g, b, b + Vector3.up * 1.05f, 0.06f, metal);
                Cyl("Umbrella stand base", g, b, b + Vector3.up * 0.03f, 0.4f, metal);
                Detail("Umbrella bag dispenser", g, b + Vector3.up * 1.15f, new Vector3(0.32f, 0.32f, 0.2f), ArtLibrary.Plain("Dispenser_Green", new Color(0.1f, 0.35f, 0.2f), 0.5f), toBack);
                Detail("Umbrella bag (hanging)", g, b + Vector3.up * 0.85f - fwd * 0.12f, new Vector3(0.1f, 0.42f, 0.01f), bagMat, toBack);
                Sign(g, "WET UMBRELLA?\nPLEASE TAKE A BAG", b + Vector3.up * 1.15f - fwd * 0.105f, -fwd, 0.3f, 0.008f, false);
                Detail("Used bag (floor)", g, P(end - 3.2f, 6.6f, 0.005f), new Vector3(0.12f, 0.01f, 0.8f), bagMat, Quaternion.Euler(0f, 30f, 0f));
                Detail("Drips (wet patch)", g, P(end - 3.1f, 6.7f, 0.004f), new Vector3(0.7f, 0.003f, 0.9f), ArtLibrary.Plain("Wet_Sheen", new Color(0.05f, 0.06f, 0.07f), 0.97f), Quaternion.Euler(0f, 20f, 0f));
            }

            // ---- wheelchairs to borrow, parked by the east corridor door ----
            Sign(g, "WHEELCHAIRS AVAILABLE\nPLEASE ASK AT INFORMATION", P(u0 + 6.0f, side - 0.32f, 1.7f), -rt, 1.2f, 0.016f);
            for (int k = 0; k < 2; k++)
            {
                Vector3 c = P(u0 + 5.6f + k * 0.75f, side - 0.8f, 0f);
                Quaternion r = Quaternion.LookRotation(-rt);
                Vector3 f = -rt, s = fwd;
                Detail("Wheelchair seat", g, c + Vector3.up * 0.5f, new Vector3(0.44f, 0.04f, 0.42f), chairBlue, r);
                Detail("Wheelchair back", g, c + Vector3.up * 0.75f - f * 0.22f, new Vector3(0.44f, 0.42f, 0.03f), chairBlue, r * Quaternion.Euler(-6f, 0f, 0f));
                foreach (float sd in new[] { -0.27f, 0.27f })
                {
                    Vector3 hub = c + s * sd + Vector3.up * 0.3f - f * 0.1f;
                    Cyl("Wheelchair wheel", g, hub - s * 0.015f, hub + s * 0.015f, 0.6f, blackMat);
                    Cyl("Wheelchair hand rim", g, hub + s * Mathf.Sign(sd) * 0.03f - s * 0.005f, hub + s * Mathf.Sign(sd) * 0.03f + s * 0.005f, 0.52f, metal);
                    Cyl("Wheelchair push handle", g, c + s * sd * 0.8f - f * 0.24f + Vector3.up * 0.55f, c + s * sd * 0.8f - f * 0.3f + Vector3.up * 1.0f, 0.03f, metal);
                    Cyl("Wheelchair castor", g, c + s * sd * 0.8f + f * 0.25f + Vector3.up * 0.06f - s * 0.02f, c + s * sd * 0.8f + f * 0.25f + Vector3.up * 0.06f + s * 0.02f, 0.12f, blackMat);
                    Cyl("Wheelchair footrest", g, c + s * sd * 0.6f + f * 0.2f + Vector3.up * 0.45f, c + s * sd * 0.6f + f * 0.32f + Vector3.up * 0.12f, 0.025f, metal);
                }
                var col = new GameObject("Wheelchair (collider)");
                col.transform.SetParent(g, false);
                col.transform.position = c + Vector3.up * 0.5f;
                col.transform.rotation = r;
                col.AddComponent<BoxCollider>().size = new Vector3(0.62f, 1.0f, 0.75f);
                col.isStatic = true;
            }

            // ---- opening hours by the doors ----
            {
                Vector3 c = P(end - 0.33f, -8.2f, 1.55f);
                Detail("Opening hours panel", g, c, new Vector3(1.0f, 1.2f, 0.02f), darkPanel, toBack);
                Sign(g, "OPENING HOURS\n\nMON - SAT   9:00 - 20:00\nSUNDAY   10:30 - 16:30\n\nCAR PARK CLOSES 30 MINS\nAFTER THE MALL\n\nNO ENTRY AFTER HOURS", c - fwd * 0.015f, -fwd, 0.9f, 0.014f, false);
            }

            // ---- coin funnel for the children's hospital ----
            {
                Vector3 c = P(u0 + 7f, 10.5f, 0f);
                EditorBuild.Box("Coin funnel stand", g, c + Vector3.up * 0.45f, new Vector3(0.7f, 0.9f, 0.7f), darkPanel, SurfaceType.Wood);
                Cyl("Coin funnel bowl", g, c + Vector3.up * 0.9f, c + Vector3.up * 1.05f, 1.1f, ArtLibrary.Plain("Funnel_Yellow", new Color(0.85f, 0.65f, 0.12f), 0.85f));
                Cyl("Coin funnel hole", g, c + Vector3.up * 1.05f, c + Vector3.up * 1.052f, 0.12f, blackMat);
                Sign(g, "SPIN A COIN FOR\nST. AGNES CHILDREN'S WARD", c + Vector3.up * 0.55f - fwd * 0.36f, -fwd, 0.6f, 0.012f, false);
                Detail("Coin slide", g, c + Vector3.up * 1.15f + rt * 0.45f, new Vector3(0.08f, 0.04f, 0.3f), metal, Quaternion.Euler(0f, 0f, 25f));
            }
        }
    }
}
