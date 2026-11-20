using UnityEngine;
using AfterHours.World;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// The atrium information desk, staffed by nobody: a raised ledge on the public side with a bell and a
    /// "back in 5 minutes" card, leaflet racks, the staff side with a swivel chair pushed back, a monitor, a desk
    /// phone off its cradle, a lost-property tray, a radio charging dock (one slot empty), and a lanyard on the floor.
    /// </summary>
    public static partial class MallBuilder
    {
        static void InfoDeskDetail(Transform g)
        {
            Material ledge = ArtLibrary.Plain("Desk_Ledge", new Color(0.85f, 0.83f, 0.78f), 0.6f);
            Material screenOff = ArtLibrary.Plain("Monitor_Off", new Color(0.02f, 0.02f, 0.025f), 0.95f);
            Material screenOn = ArtLibrary.Plain("Monitor_Login", new Color(0.05f, 0.12f, 0.25f), 0.9f, 0f, new Color(0.2f, 0.35f, 0.75f), false);
            Material leaflet = ArtLibrary.Plain("Leaflet", new Color(0.9f, 0.85f, 0.6f), 0.3f);
            Material leaflet2 = ArtLibrary.Plain("Leaflet_Blue", new Color(0.35f, 0.55f, 0.85f), 0.3f);
            Material lanyard = ArtLibrary.Plain("Lanyard_Green", new Color(0.1f, 0.5f, 0.3f), 0.3f);
            Material brass = ArtLibrary.Plain("Bell_Brass", new Color(0.75f, 0.6f, 0.25f), 0.85f, 1f);

            // raised ledge along the public (+z) side
            EditorBuild.Box("Desk ledge", g, new Vector3(0f, 1.18f, -6.12f), new Vector3(5.2f, 0.04f, 0.32f), ledge, SurfaceType.Wood);
            for (int k = -2; k <= 2; k++) Detail("Ledge bracket", g, new Vector3(k * 1.2f, 1.13f, -6.15f), new Vector3(0.03f, 0.06f, 0.2f), metal);
            Cyl("Bell base", g, new Vector3(0.6f, 1.2f, -6.1f), new Vector3(0.6f, 1.22f, -6.1f), 0.09f, blackMat);
            Ball("Bell", g, new Vector3(0.6f, 1.24f, -6.1f), 0.08f, brass);
            Detail("Back soon card", g, new Vector3(-0.4f, 1.27f, -6.1f), new Vector3(0.22f, 0.14f, 0.005f), whiteFixture, Quaternion.Euler(-12f, 180f, 0f));
            Sign(g, "BACK IN\n5 MINUTES", new Vector3(-0.4f, 1.27f, -6.095f), Vector3.forward, 0.2f, 0.007f, false);
            Detail("Pen on chain", g, new Vector3(1.4f, 1.21f, -6.1f), new Vector3(0.14f, 0.012f, 0.012f), blackMat, Quaternion.Euler(0f, 25f, 0f));
            Detail("Visitor book", g, new Vector3(1.75f, 1.21f, -6.1f), new Vector3(0.3f, 0.02f, 0.22f), redMat, Quaternion.Euler(0f, -6f, 0f));

            // leaflet racks on the ends of the desk
            foreach (float x in new[] { -2.56f, 2.56f })
            {
                float s = Mathf.Sign(x);
                Detail("Leaflet rack", g, new Vector3(x, 0.65f, -7.0f), new Vector3(0.04f, 0.9f, 1.3f), glassMat);
                for (int r = 0; r < 3; r++)
                for (int c = 0; c < 4; c++)
                    Detail("Leaflet", g, new Vector3(x + s * 0.03f, 0.35f + r * 0.28f, -7.5f + c * 0.32f), new Vector3(0.01f, 0.2f, 0.1f), (r + c) % 2 == 0 ? leaflet : leaflet2, Quaternion.Euler(0f, 0f, s * -8f));
            }
            Sign(g, "WHAT'S ON", new Vector3(2.6f, 1.2f, -7.0f), Vector3.right, 0.8f, 0.02f, false);

            // staff side (-z): chair pushed back, monitor, phone off the hook, trays, radio dock
            OfficeChairProp(g, new Vector3(0.7f, 0f, -8.65f), 160f, 8f, SurfaceType.Metal);
            Detail("Monitor", g, new Vector3(-0.6f, 1.38f, -6.9f), new Vector3(0.55f, 0.34f, 0.03f), blackMat, Quaternion.Euler(0f, 180f, 0f));
            Detail("Monitor screen", g, new Vector3(-0.6f, 1.38f, -6.92f), new Vector3(0.5f, 0.3f, 0.004f), screenOn, Quaternion.Euler(0f, 180f, 0f));
            Sign(g, "SESSION LOCKED", new Vector3(-0.6f, 1.38f, -6.925f), Vector3.back, 0.3f, 0.008f, false);
            Cyl("Monitor stand", g, new Vector3(-0.6f, 1.1f, -6.86f), new Vector3(-0.6f, 1.22f, -6.86f), 0.04f, blackMat);
            Detail("Keyboard", g, new Vector3(-0.6f, 1.11f, -7.25f), new Vector3(0.45f, 0.02f, 0.15f), blackMat);
            Detail("Mouse", g, new Vector3(-0.22f, 1.11f, -7.25f), new Vector3(0.06f, 0.025f, 0.1f), blackMat);
            Detail("Desk phone", g, new Vector3(0.4f, 1.13f, -7.2f), new Vector3(0.2f, 0.06f, 0.22f), blackMat, Quaternion.Euler(-8f, 10f, 0f));
            Detail("Receiver (off hook)", g, new Vector3(0.62f, 1.13f, -7.35f), new Vector3(0.06f, 0.05f, 0.2f), blackMat, Quaternion.Euler(0f, 70f, 90f));
            Detail("Coiled cord", g, new Vector3(0.52f, 1.12f, -7.28f), new Vector3(0.15f, 0.012f, 0.03f), blackMat, Quaternion.Euler(0f, 40f, 0f));
            Detail("Lost property tray", g, new Vector3(1.6f, 1.14f, -7.3f), new Vector3(0.45f, 0.06f, 0.32f), blackMat);
            Detail("Lost: glove", g, new Vector3(1.5f, 1.17f, -7.3f), new Vector3(0.09f, 0.025f, 0.18f), ArtLibrary.Plain("Glove_Grey", new Color(0.3f, 0.3f, 0.32f), 0.1f), Quaternion.Euler(0f, 20f, 0f));
            Detail("Lost: keys", g, new Vector3(1.72f, 1.175f, -7.35f), new Vector3(0.06f, 0.01f, 0.04f), metal);
            Detail("Lost: child's hat", g, new Vector3(1.68f, 1.19f, -7.22f), new Vector3(0.16f, 0.06f, 0.14f), ArtLibrary.Plain("Hat_Pink", new Color(0.9f, 0.5f, 0.6f), 0.1f));
            Detail("Radio dock", g, new Vector3(-1.75f, 1.13f, -7.4f), new Vector3(0.5f, 0.06f, 0.14f), blackMat);
            for (int k = 0; k < 4; k++)
            {
                if (k == 2) continue;   // one radio gone
                Detail("Radio", g, new Vector3(-1.93f + k * 0.12f, 1.24f, -7.4f), new Vector3(0.06f, 0.16f, 0.04f), blackMat);
                Detail("Radio LED", g, new Vector3(-1.93f + k * 0.12f, 1.145f, -7.33f), new Vector3(0.012f, 0.012f, 0.004f), ArtLibrary.Plain("LED_Green", new Color(0.1f, 0.5f, 0.1f), 0.6f, 0f, new Color(0.2f, 1.5f, 0.3f), false));
            }
            Detail("Lanyard (floor)", g, new Vector3(-1.2f, 0.004f, -8.5f), new Vector3(0.02f, 0.004f, 0.45f), lanyard, Quaternion.Euler(0f, 35f, 0f));
            Detail("ID card", g, new Vector3(-1.08f, 0.006f, -8.32f), new Vector3(0.055f, 0.003f, 0.085f), whiteFixture, Quaternion.Euler(0f, 35f, 0f));
            _ = screenOff;
        }
    }
}
