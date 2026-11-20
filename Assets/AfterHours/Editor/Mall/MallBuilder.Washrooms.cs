using UnityEngine;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Toilet fit-out: seats (some up, some down, one missing its lid), flush plates, roll holders - a few rolls
    /// empty, one unravelled across the floor - sanitary bins and coat hooks in the cubicles; soap dispensers over the
    /// basins, a pedal bin under the dryer, a floor drain and the cleaning rota (last signed off hours ago).
    /// </summary>
    public static partial class MallBuilder
    {
        static void CubicleDetail(Transform g, AreaRect area, bool alongX, float p, float y, int i)
        {
            int h = Mathf.Abs(MallLayout.Hash((int)(area.minX * 10f), (int)(area.minZ * 10f), i, 41));
            // a = along the row from the partition, d = distance out from the back wall
            Vector3 W(float a, float up, float d) => alongX ? new Vector3(p + a, y + up, area.maxZ - d) : new Vector3(area.maxX - d, y + up, p + a);
            Quaternion rot = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
            Vector3 alongDir = alongX ? Vector3.right : Vector3.forward;
            Material seat = ArtLibrary.Plain("WC_Seat", new Color(0.9f, 0.9f, 0.88f), 0.55f);
            Material chrome = ArtLibrary.Plain("Washroom_Chrome", new Color(0.75f, 0.76f, 0.78f), 0.85f, 1f);
            Material paper = ArtLibrary.Plain("Toilet_Paper", new Color(0.93f, 0.92f, 0.88f), 0.2f);

            // seat, and the lid down, up against the cistern, or gone
            Detail("WC seat", g, W(0.55f, 0.435f, 0.36f), new Vector3(0.38f, 0.025f, 0.46f), seat, rot);
            int lid = h % 5;
            if (lid <= 1) Detail("WC lid (down)", g, W(0.55f, 0.46f, 0.37f), new Vector3(0.39f, 0.02f, 0.46f), seat, rot);
            else if (lid <= 3) Detail("WC lid (up)", g, W(0.55f, 0.66f, 0.23f), new Vector3(0.39f, 0.44f, 0.02f), seat, rot * Quaternion.Euler(-8f, 0f, 0f));
            Detail("Flush plate", g, W(0.55f, 0.855f, 0.11f), new Vector3(0.14f, 0.008f, 0.08f), chrome, rot);
            Detail("Coat hook", g, W(0.55f, 1.65f, 0.02f), new Vector3(0.03f, 0.08f, 0.05f), chrome, rot);

            // roll holder on the partition, at sitting height
            Vector3 holder = W(0.08f, 0.72f, 0.78f);
            Detail("Roll holder", g, holder, new Vector3(0.05f, 0.04f, 0.16f), chrome, rot);
            int roll = h % 7;
            if (roll != 0)
                Cyl("Toilet roll", g, holder + alongDir * 0.03f, holder + alongDir * 0.13f, roll == 1 ? 0.045f : 0.11f, roll == 1 ? ArtLibrary.Plain("Roll_Tube", new Color(0.55f, 0.45f, 0.32f), 0.2f) : paper);
            if (roll == 3)
            {
                // unravelled: a sheet hanging down and a long tail across the floor, out under the door
                Detail("Paper (hanging)", g, holder + alongDir * 0.08f - Vector3.up * 0.37f + (alongX ? Vector3.back : Vector3.left) * 0.05f, new Vector3(0.1f, 0.68f, 0.004f), paper, alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f));
                for (int k = 0; k < 9; k++)
                {
                    Vector3 c = W(0.15f + Mathf.Sin(k * 0.9f) * 0.12f + k * 0.03f, 0.003f, 0.85f + k * 0.2f);
                    Detail("Paper (floor)", g, c, new Vector3(0.1f, 0.003f, 0.22f), paper, rot * Quaternion.Euler(0f, Mathf.Sin(k * 1.7f) * 25f, 0f));
                }
            }
            else if (roll == 5)
            {
                Cyl("Spare roll (floor)", g, W(0.85f, 0.055f, 0.85f), W(0.85f, 0.165f, 0.85f), 0.11f, paper);
            }

            // sanitary bin in the far corner
            if (h % 2 == 0)
            {
                Detail("Sanitary bin", g, W(0.92f, 0.22f, 0.25f), new Vector3(0.18f, 0.44f, 0.36f), whiteFixture, rot);
                Detail("Sanitary bin lid", g, W(0.92f, 0.45f, 0.25f), new Vector3(0.19f, 0.03f, 0.37f), ArtLibrary.Plain("Bin_Lid_Grey", new Color(0.5f, 0.52f, 0.55f), 0.4f), rot);
            }
        }

        static void WashroomDetail(Transform g, AreaRect area, bool alongX, float s0, float s1, float y)
        {
            Vector3 alongDir = alongX ? Vector3.right : Vector3.forward;
            Vector3 outDir = alongX ? Vector3.forward : Vector3.right;
            float wall = alongX ? area.minZ : area.minX;
            Vector3 W(float a, float up, float d) => alongX ? new Vector3(a, y + up, wall + d) : new Vector3(wall + d, y + up, a);
            Quaternion rot = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
            Material chrome = ArtLibrary.Plain("Washroom_Chrome", new Color(0.75f, 0.76f, 0.78f), 0.85f, 1f);
            Material soapPink = ArtLibrary.Plain("Soap_Pink", new Color(0.85f, 0.55f, 0.65f), 0.85f);
            int h = Mathf.Abs(MallLayout.Hash((int)(area.minX * 10f), (int)(area.minZ * 10f), 5, 61));

            // soap dispensers between the taps, under the mirror; one hangs off its bracket
            int n = 0;
            for (float t = s0 + 0.9f; t < s1 - 0.4f; t += 0.9f, n++)
            {
                bool broken = (h + n) % 4 == 0;
                Vector3 c = W(t, 1.05f, 0.05f);
                Detail("Soap dispenser", g, c, new Vector3(0.1f, 0.16f, 0.08f), whiteFixture, broken ? rot * Quaternion.Euler(0f, 0f, 25f) : rot);
                Detail("Soap window", g, c + outDir * 0.041f, new Vector3(0.05f, 0.06f, 0.004f), soapPink, rot);
                Detail("Soap drip", g, W(t, 0.905f, 0.18f), new Vector3(0.05f, 0.004f, 0.07f), soapPink, rot);
            }

            // pedal bin under the hand dryer, towels spilling
            Vector3 bin = W(s1 + 0.55f, 0f, 0.25f);
            Cyl("Pedal bin", g, bin, bin + Vector3.up * 0.5f, 0.32f, chrome, true);
            Detail("Pedal bin pedal", g, bin + outDir * 0.18f + Vector3.up * 0.03f, new Vector3(0.1f, 0.02f, 0.06f), blackMat, rot);
            Material towel = ArtLibrary.Plain("Paper_Towel", new Color(0.82f, 0.8f, 0.74f), 0.2f);
            for (int k = 0; k < 4; k++)
                Detail("Screwed-up towel", g, bin + outDir * (0.3f + k * 0.07f) + alongDir * (k % 2 == 0 ? 0.1f : -0.12f) + Vector3.up * 0.03f, new Vector3(0.08f, 0.06f, 0.07f), towel, Quaternion.Euler(k * 37f, k * 71f, k * 13f));

            // floor drain in the middle of the room
            Vector2 mid = area.Center;
            Detail("Floor drain", g, new Vector3(mid.x, y + 0.004f, mid.y), new Vector3(0.18f, 0.006f, 0.18f), chrome);
            for (int k = -2; k <= 2; k++)
                Detail("Drain slot", g, new Vector3(mid.x + k * 0.03f, y + 0.008f, mid.y), new Vector3(0.012f, 0.004f, 0.13f), blackMat);

            // the cleaning rota by the door end, the last tick at 21:00
            Vector3 rota = W(s0 - 0.75f, 1.45f, 0.012f);
            Detail("Cleaning rota board", g, rota, new Vector3(0.32f, 0.44f, 0.01f), whiteFixture, rot);
            Sign(g, "TOILET CHECKS\n\n09:00 OK  JM\n11:00 OK  JM\n13:00 OK  KD\n15:00 OK  KD\n17:00 OK  KD\n19:00 OK  GD\n21:00 OK  GD\n23:00", rota + outDir * 0.008f, outDir, 0.3f, 0.0065f, false);
        }
    }
}
