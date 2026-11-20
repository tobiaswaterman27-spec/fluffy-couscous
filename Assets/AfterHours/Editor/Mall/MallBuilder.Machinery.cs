using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// What used to be plain blocks: plant-room machinery (air handlers with gauges and lagged pipes up into the
    /// ceiling, pump sets on plinths with valve wheels, red pressure vessels) and arcade cabinets (marquee, screen
    /// still running its attract loop or dead, control panel with stick and buttons, coin door).
    /// </summary>
    public static partial class MallBuilder
    {
        /// <summary>The back-of-house room being furnished (null elsewhere): picks the kind of plant it gets.</summary>
        static string plantRoomLabel;

        static void PlantMachine(Transform g, AreaRect r, float y, int n)
        {
            if (plantRoomLabel != null && plantRoomLabel.Contains("Water")) { WaterTank(g, r, y, n); return; }
            if (plantRoomLabel != null && (plantRoomLabel.Contains("Electrical") || plantRoomLabel.Contains("Switch"))) { Switchgear(g, r, y, n); return; }
            int h = Mathf.Abs(MallLayout.Hash((int)(r.minX * 10f), (int)(r.minZ * 10f), n, 77));
            Vector2 c2 = r.Center;
            Vector3 c = new Vector3(c2.x, y, c2.y);
            float w = r.Width, d = r.Depth;
            float ceil = y + MallLayout.CeilingHeight - 0.1f;
            Material lagging = ArtLibrary.Plain("Pipe_Lagging", new Color(0.78f, 0.78f, 0.74f), 0.3f);
            Material pumpGreen = ArtLibrary.Plain("Pump_Green", new Color(0.12f, 0.35f, 0.2f), 0.6f);
            Material vesselRed = ArtLibrary.Plain("Vessel_Red", new Color(0.6f, 0.08f, 0.06f), 0.6f);
            Material gaugeFace = ArtLibrary.Plain("Gauge_Face", new Color(0.92f, 0.92f, 0.88f), 0.85f);
            Material valveRed = redMat;
            switch (h % 3)
            {
                case 0:   // air handler / boiler cabinet
                {
                    BoxRect("Air handling unit", g, r, y, y + 2.2f, plantMat, SurfaceType.Metal);
                    for (int k = 1; k < 3; k++)
                        Detail("Panel seam", g, c + new Vector3(-w * 0.5f + w * k / 3f, 1.1f, -d * 0.5f - 0.005f), new Vector3(0.012f, 2.1f, 0.01f), blackMat);
                    for (int k = 0; k < 2; k++)
                    {
                        Vector3 gp = c + new Vector3(-w * 0.3f + k * 0.35f, 1.5f, -d * 0.5f - 0.02f);
                        Cyl("Gauge", g, gp, gp - Vector3.forward * 0.04f, 0.15f, metal);
                        Detail("Gauge face", g, gp - Vector3.forward * 0.041f, new Vector3(0.11f, 0.11f, 0.004f), gaugeFace);
                        Detail("Gauge needle", g, gp - Vector3.forward * 0.044f, new Vector3(0.006f, 0.05f, 0.003f), blackMat, Quaternion.Euler(0f, 0f, (h + k * 97) % 140 - 70f));
                    }
                    Detail("Warning label", g, c + new Vector3(w * 0.25f, 1.4f, -d * 0.5f - 0.006f), new Vector3(0.22f, 0.16f, 0.004f), yellowPaint);
                    foreach (float px in new[] { -w * 0.3f, w * 0.3f })
                    {
                        Vector3 b = c + new Vector3(px, 2.2f, 0f);
                        Cyl("Lagged pipe", g, b, new Vector3(b.x, ceil, b.z), 0.22f, lagging);
                        Cyl("Pipe band", g, b + Vector3.up * 0.6f, b + Vector3.up * 0.66f, 0.24f, metal);
                    }
                    Sign(g, $"AHU {1 + h % 9}\nSUPPLY", c + new Vector3(0f, 1.95f, -d * 0.5f - 0.008f), Vector3.back, 0.6f, 0.014f, false);
                    break;
                }
                case 1:   // pump set on a plinth
                {
                    BoxRect("Pump plinth", g, r, y, y + 0.2f, concrete, SurfaceType.Concrete);
                    var col = new GameObject("Pump set (collider)");
                    col.transform.SetParent(g, false);
                    col.transform.localPosition = c + Vector3.up * 0.8f;
                    col.AddComponent<BoxCollider>().size = new Vector3(w, 1.2f, d);
                    col.isStatic = true;
                    for (int k = -1; k <= 1; k += 2)
                    {
                        Vector3 pc = c + new Vector3(k * w * 0.25f, 0.55f, 0f);
                        Cyl("Pump motor", g, pc + Vector3.forward * 0.1f, pc + Vector3.forward * (d * 0.5f - 0.15f), 0.42f, pumpGreen);
                        Cyl("Pump volute", g, pc - Vector3.forward * 0.25f, pc + Vector3.forward * 0.1f, 0.55f, pumpGreen);
                        Cyl("Pump fins", g, pc + Vector3.forward * 0.3f, pc + Vector3.forward * 0.45f, 0.46f, metal);
                        Vector3 riser = pc - Vector3.forward * 0.35f;
                        Cyl("Pump riser", g, riser, new Vector3(riser.x, ceil, riser.z), 0.16f, plantMat);
                        Vector3 valve = riser + Vector3.up * 0.9f;
                        Cyl("Valve body", g, valve - Vector3.up * 0.1f, valve + Vector3.up * 0.1f, 0.24f, metal);
                        Cyl("Valve stem", g, valve, valve - Vector3.forward * 0.25f, 0.03f, metal);
                        Cyl("Valve wheel", g, valve - Vector3.forward * 0.25f, valve - Vector3.forward * 0.27f, 0.3f, valveRed);
                    }
                    Cyl("Header pipe", g, c + new Vector3(-w * 0.5f, 1.45f, -0.35f), c + new Vector3(w * 0.5f, 1.45f, -0.35f), 0.2f, plantMat);
                    Sign(g, "PUMP A          PUMP B", c + new Vector3(0f, 0.12f, -d * 0.5f - 0.005f), Vector3.back, w - 0.2f, 0.012f, false);
                    break;
                }
                default:  // pressure vessels
                {
                    BoxRect("Vessel base", g, r, y, y + 0.08f, metal, SurfaceType.Metal);
                    for (int k = -1; k <= 1; k += 2)
                    {
                        Vector3 vc = c + new Vector3(k * w * 0.24f, 0f, 0f);
                        float dia = Mathf.Min(d, w * 0.45f) - 0.05f;
                        Cyl("Pressure vessel", g, vc + Vector3.up * 0.3f, vc + Vector3.up * 1.9f, dia, vesselRed, true);
                        Ball("Vessel dome", g, vc + Vector3.up * 1.9f, dia, vesselRed);
                        foreach (float a in new[] { 0f, 120f, 240f })
                        {
                            Vector3 leg = Quaternion.Euler(0f, a, 0f) * Vector3.forward * dia * 0.4f;
                            Cyl("Vessel leg", g, vc + leg + Vector3.up * 0.08f, vc + leg + Vector3.up * 0.4f, 0.05f, metal);
                        }
                        Vector3 top = vc + Vector3.up * (1.9f + dia * 0.45f);
                        Cyl("Vessel pipe", g, top, new Vector3(top.x, ceil, top.z), 0.08f, metal);
                        Vector3 gp = vc + new Vector3(0f, 1.2f, -dia * 0.5f - 0.02f);
                        Cyl("Gauge", g, gp, gp - Vector3.forward * 0.04f, 0.12f, metal);
                        Detail("Gauge face", g, gp - Vector3.forward * 0.041f, new Vector3(0.09f, 0.09f, 0.004f), gaugeFace);
                        Detail("Vessel plate", g, vc + new Vector3(0f, 0.9f, -dia * 0.5f - 0.004f), new Vector3(0.16f, 0.1f, 0.004f), metal);
                    }
                    break;
                }
            }
        }

        /// <summary>A sectional water tank on steel beams: panel grid, a ladder up the side, level gauge, pipes.</summary>
        static void WaterTank(Transform g, AreaRect r, float y, int n)
        {
            Vector2 c2 = r.Center;
            Vector3 c = new Vector3(c2.x, y, c2.y);
            float w = r.Width, d = r.Depth, H = 2.0f;
            Material panel = ArtLibrary.Plain("Tank_Panel", new Color(0.35f, 0.45f, 0.55f), 0.45f, 0.3f);
            Material beam = ArtLibrary.Plain("Tank_Beam", new Color(0.12f, 0.12f, 0.12f), 0.4f, 0.5f);
            BoxRect("Tank plinth beams", g, r, y, y + 0.25f, beam, SurfaceType.Metal);
            BoxRect("Water tank", g, r, y + 0.25f, y + 0.25f + H, panel, SurfaceType.Metal);
            // panel seams every metre, flanged
            for (float a = -w * 0.5f + 1f; a < w * 0.5f; a += 1f)
                foreach (float s in new[] { -1f, 1f })
                    Detail("Tank flange", g, c + new Vector3(a, 0.25f + H * 0.5f, s * (d * 0.5f + 0.015f)), new Vector3(0.05f, H, 0.03f), panel);
            foreach (float s in new[] { -1f, 1f })
                Detail("Tank flange", g, c + new Vector3(0f, 0.25f + H * 0.5f + 0f, s * (d * 0.5f + 0.015f)) + Vector3.up * 0f, new Vector3(w, 0.05f, 0.03f), panel);
            // level gauge tube and a ladder on one end
            Vector3 e = c + new Vector3(w * 0.5f + 0.06f, 0f, -d * 0.25f);
            Cyl("Level gauge", g, e + Vector3.up * 0.4f, e + Vector3.up * (H + 0.1f), 0.05f, glassMat);
            Cyl("Gauge water", g, e + Vector3.up * 0.4f, e + Vector3.up * (0.4f + H * 0.55f), 0.035f, waterMat);
            Vector3 l = c + new Vector3(w * 0.5f + 0.15f, 0f, d * 0.15f);
            foreach (float s in new[] { -0.2f, 0.2f }) Cyl("Ladder stile", g, l + Vector3.forward * s, l + Vector3.forward * s + Vector3.up * (H + 0.6f), 0.04f, metal);
            for (float hh = 0.3f; hh < H + 0.4f; hh += 0.3f) Cyl("Ladder rung", g, l + Vector3.up * hh - Vector3.forward * 0.2f, l + Vector3.up * hh + Vector3.forward * 0.2f, 0.025f, metal);
            // inlet and outlet pipes, a hatch on top
            Cyl("Inlet pipe", g, c + new Vector3(-w * 0.3f, 0.25f + H, 0f), c + new Vector3(-w * 0.3f, MallLayout.CeilingHeight - 0.1f, 0f), 0.12f, plantMat);
            Cyl("Outlet pipe", g, c + new Vector3(-w * 0.5f - 0.3f, 0.5f, 0f), c + new Vector3(-w * 0.5f, 0.5f, 0f), 0.15f, plantMat);
            Cyl("Outlet valve", g, c + new Vector3(-w * 0.5f - 0.2f, 0.4f, 0f), c + new Vector3(-w * 0.5f - 0.2f, 0.62f, 0f), 0.2f, metal);
            Cyl("Valve wheel", g, c + new Vector3(-w * 0.5f - 0.2f, 0.64f, 0f), c + new Vector3(-w * 0.5f - 0.2f, 0.66f, 0f), 0.3f, redMat);
            Detail("Tank hatch", g, c + new Vector3(w * 0.25f, 0.25f + H + 0.02f, 0f), new Vector3(0.6f, 0.04f, 0.6f), panel);
            Sign(g, $"TANK {(char)('A' + n % 4)}\nPOTABLE WATER", c + new Vector3(0f, 1.6f, -d * 0.5f - 0.035f), Vector3.back, 0.9f, 0.018f, false);
        }

        /// <summary>A switchgear line-up: tall grey cubicles with meters, isolator handles, indicator lamps and danger plates.</summary>
        static void Switchgear(Transform g, AreaRect r, float y, int n)
        {
            Vector2 c2 = r.Center;
            Vector3 c = new Vector3(c2.x, y, c2.y);
            float w = r.Width, d = r.Depth;
            Material grey = ArtLibrary.Plain("Switchgear_Grey", new Color(0.55f, 0.57f, 0.55f), 0.45f, 0.3f);
            Material lampG = ArtLibrary.Plain("Lamp_Green", new Color(0.1f, 0.4f, 0.1f), 0.6f, 0f, new Color(0.2f, 1.6f, 0.3f), false);
            Material lampR = ArtLibrary.Plain("Lamp_Red", new Color(0.4f, 0.05f, 0.05f), 0.6f, 0f, new Color(1.6f, 0.15f, 0.1f), false);
            Material meterFace = ArtLibrary.Plain("Meter_Face", new Color(0.92f, 0.92f, 0.88f), 0.8f);
            BoxRect("Switchgear", g, r, y, y + 2.1f, grey, SurfaceType.Metal);
            int cells = Mathf.Max(2, Mathf.RoundToInt(w / 0.6f));
            float cw = w / cells;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 n3 = Vector3.forward * s;
                Quaternion face = Quaternion.LookRotation(n3);
                for (int k = 0; k < cells; k++)
                {
                    Vector3 p = c + new Vector3(-w * 0.5f + (k + 0.5f) * cw, 0f, s * (d * 0.5f + 0.006f));
                    Detail("Cubicle seam", g, p + new Vector3(cw * 0.5f, 1.05f, 0f), new Vector3(0.01f, 2.0f, 0.01f), blackMat, face);
                    Detail("Meter", g, p + Vector3.up * 1.65f + n3 * 0.01f, new Vector3(0.14f, 0.14f, 0.02f), meterFace, face);
                    Detail("Meter needle", g, p + Vector3.up * 1.64f + n3 * 0.022f, new Vector3(0.005f, 0.06f, 0.003f), blackMat, face * Quaternion.Euler(0f, 0f, (k * 37 + n * 13) % 90 - 45f));
                    bool on = (k + n) % 5 != 0;
                    Detail("Indicator lamp", g, p + Vector3.up * 1.42f + Vector3.right * 0.08f + n3 * 0.015f, new Vector3(0.035f, 0.035f, 0.02f), on ? lampG : lampR, face);
                    Detail("Isolator handle", g, p + Vector3.up * 1.05f + n3 * 0.04f, new Vector3(0.05f, 0.18f, 0.06f), blackMat, face * Quaternion.Euler(0f, 0f, on ? 0f : 90f));
                    Detail("Danger plate", g, p + Vector3.up * 0.45f + n3 * 0.004f, new Vector3(0.16f, 0.1f, 0.003f), yellowPaint, face);
                }
            }
            Sign(g, $"LV BOARD {n % 3 + 1}\nDANGER 415 VOLTS", c + new Vector3(0f, 1.95f, -d * 0.5f - 0.012f), Vector3.back, w - 0.2f, 0.016f, false);
        }

        static readonly Color[] ArcadeColours =
        {
            new Color(0.5f, 0.05f, 0.4f), new Color(0.05f, 0.2f, 0.5f), new Color(0.6f, 0.3f, 0.02f), new Color(0.06f, 0.4f, 0.15f), new Color(0.08f, 0.08f, 0.1f),
        };
        static readonly string[] ArcadeTitles = { "STAR RAIDER", "TURBO DASH", "DUNGEON KNIGHT", "ROBO BRAWL", "ASTRO BLASTER", "NEON RACER", "GHOST MAZE", "SKY ACE" };

        static void ArcadeCabinet(Transform g, AreaRect r, float y, int n)
        {
            int h = Mathf.Abs(MallLayout.Hash((int)(r.minX * 10f), (int)(r.minZ * 10f), n, 31));
            Vector2 c2 = r.Center;
            Vector3 c = new Vector3(c2.x, y, c2.y);
            // rows alternate which way they face
            bool faceNeg = Mathf.RoundToInt(r.minZ / 1.9f) % 2 == 0;
            Vector3 f = faceNeg ? Vector3.back : Vector3.forward;
            Quaternion rot = Quaternion.LookRotation(f);
            float w = r.Width, d = r.Depth;
            Color col = ArcadeColours[h % ArcadeColours.Length];
            Material side = ArtLibrary.Plain($"Arcade_Side_{h % ArcadeColours.Length}", col, 0.5f);
            Material marquee = ArtLibrary.Plain($"Arcade_Marquee_{h % ArcadeColours.Length}", col * 0.8f, 0.6f, 0f, col * 2.2f + new Color(0.2f, 0.2f, 0.2f), false);
            bool dead = h % 4 == 0;
            Material scr = dead ? ArtLibrary.Plain("Arcade_Screen_Dead", new Color(0.02f, 0.02f, 0.025f), 0.95f)
                                : ArtLibrary.Plain($"Arcade_Screen_{h % 3}", new Color(0.03f, 0.05f, 0.08f), 0.95f, 0f, h % 3 == 0 ? new Color(0.2f, 0.5f, 1.2f) : h % 3 == 1 ? new Color(1.1f, 0.3f, 0.6f) : new Color(0.3f, 1.0f, 0.4f), false);

            // body (the collider), set back a little at the bottom
            GameObject body = BoxRect("Arcade cabinet", g, r, y, y + 1.8f, side, SurfaceType.Metal);
            body.name = "Arcade cabinet";
            Vector3 front = c + f * (d * 0.5f);
            Detail("Kick plate", g, front + Vector3.up * 0.25f + f * 0.005f, new Vector3(w - 0.02f, 0.5f, 0.01f), blackMat, rot);
            Detail("Coin door", g, front + Vector3.up * 0.55f + f * 0.008f, new Vector3(0.32f, 0.3f, 0.01f), metal, rot);
            foreach (float s in new[] { -0.07f, 0.07f })
            {
                Detail("Coin slot", g, front + Vector3.up * 0.6f + f * 0.014f + Vector3.Cross(Vector3.up, f) * s, new Vector3(0.025f, 0.06f, 0.006f), redMat, rot);
            }
            // control panel: sloping shelf out from the front
            Vector3 cp = front + Vector3.up * 0.95f + f * 0.16f;
            Detail("Control panel", g, cp, new Vector3(w - 0.04f, 0.06f, 0.36f), blackMat, rot * Quaternion.Euler(14f, 0f, 0f));
            Vector3 right = Vector3.Cross(Vector3.up, f);
            Cyl("Joystick", g, cp + Vector3.up * 0.03f - right * 0.2f, cp + Vector3.up * 0.13f - right * 0.2f, 0.02f, metal);
            Ball("Joystick ball", g, cp + Vector3.up * 0.15f - right * 0.2f, 0.05f, redMat);
            Material[] btn = { redMat, yellowPaint, greenMat, ArtLibrary.Plain("Button_Blue", new Color(0.1f, 0.2f, 0.7f), 0.6f) };
            for (int k = 0; k < 4; k++)
                Cyl("Button", g, cp + Vector3.up * 0.03f + right * (0.02f + k * 0.08f), cp + Vector3.up * 0.05f + right * (0.02f + k * 0.08f), 0.045f, btn[k]);
            // screen in a dark bezel, tilted back
            Vector3 sc = front + Vector3.up * 1.3f - f * 0.02f;
            Detail("Bezel", g, sc + f * 0.004f, new Vector3(w - 0.08f, 0.5f, 0.01f), blackMat, rot * Quaternion.Euler(-10f, 0f, 0f));
            Detail("Arcade screen", g, sc + f * 0.01f, new Vector3(w - 0.2f, 0.4f, 0.005f), scr, rot * Quaternion.Euler(-10f, 0f, 0f));
            // marquee over the top, lit
            Detail("Marquee", g, front + Vector3.up * 1.68f + f * 0.06f, new Vector3(w, 0.22f, 0.12f), marquee, rot);
            Sign(g, ArcadeTitles[h % ArcadeTitles.Length], front + Vector3.up * 1.68f + f * 0.125f, f, w - 0.1f, 0.016f, false);
            if (dead) Sign(g, "OUT OF ORDER", sc + f * 0.014f, f, w - 0.2f, 0.012f, false);
        }
    }
}
