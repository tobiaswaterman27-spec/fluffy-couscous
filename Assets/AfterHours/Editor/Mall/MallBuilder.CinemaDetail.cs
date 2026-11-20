using UnityEngine;
using AfterHours.Audio;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Cinema fit-out: proper tip-up seats in place of the row blocks (the block's collider stays; a few seats are
    /// left down, the rest tipped up), armrests with cup holders, aisle step lights; in the lobby a popcorn machine
    /// still half full, a drinks fountain, ticket screens and printers, a belt-barrier queue, backlit posters for
    /// films that were showing, and the projection-booth ports above each screen door.
    /// </summary>
    public static partial class MallBuilder
    {
        static void CinemaSeats(Transform g, WingId w, GameObject row, float r0, float va, float vb, float top, int k)
        {
            Object.DestroyImmediate(row.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(row.GetComponent<MeshFilter>());
            Vector3 fwd = MallLayout.WingForward(w);
            Quaternion face = Quaternion.LookRotation(fwd);
            Vector3 P(float u, float v, float h) => MallLayout.WingPoint(w, u, v, h);
            Material plush = seatMat;
            Material armMat = ArtLibrary.Plain("Cinema_Arm", new Color(0.06f, 0.06f, 0.07f), 0.4f);
            Material stepLight = ArtLibrary.Plain("Step_Light", new Color(0.4f, 0.35f, 0.25f), 0.6f, 0f, new Color(0.9f, 0.7f, 0.35f), false);
            const float pitch = 0.6f;
            int n = Mathf.FloorToInt((vb - va) / pitch);
            float start = (va + vb) * 0.5f - n * pitch * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float vc = start + (i + 0.5f) * pitch;
                int h = Mathf.Abs(MallLayout.Hash((int)(r0 * 10f), (int)(vc * 10f), k, (int)w));
                bool down = h % 9 == 0;
                // back, leaning back a little
                Detail("Seat back", g, P(r0 + 0.32f, vc, top + 0.72f), new Vector3(pitch - 0.08f, 0.62f, 0.1f), plush, face * Quaternion.Euler(-10f, 0f, 0f));
                if (down) Detail("Seat (down)", g, P(r0 + 0.55f, vc, top + 0.42f), new Vector3(pitch - 0.1f, 0.1f, 0.44f), plush, face * Quaternion.Euler(4f, 0f, 0f));
                else Detail("Seat (tipped up)", g, P(r0 + 0.47f, vc, top + 0.6f), new Vector3(pitch - 0.1f, 0.42f, 0.1f), plush, face * Quaternion.Euler(-12f, 0f, 0f));
                if (h % 23 == 0) Detail("Popcorn carton (dropped)", g, P(r0 + 0.95f, vc + 0.1f, top + 0.06f), new Vector3(0.12f, 0.12f, 0.16f), ArtLibrary.Plain("Popcorn_Carton", new Color(0.8f, 0.15f, 0.12f), 0.3f), face * Quaternion.Euler(90f, h % 90, 0f));
            }
            for (int i = 0; i <= n; i++)
            {
                float va2 = start + i * pitch;
                Detail("Armrest standard", g, P(r0 + 0.5f, va2, top + 0.3f), new Vector3(0.05f, 0.6f, 0.5f), armMat, face);
                Detail("Armrest", g, P(r0 + 0.52f, va2, top + 0.63f), new Vector3(0.07f, 0.04f, 0.5f), armMat, face);
                Cyl("Cup holder", g, P(r0 + 0.74f, va2, top + 0.6f), P(r0 + 0.74f, va2, top + 0.66f), 0.08f, armMat);
            }
            // step lights on the riser face at both aisle ends
            foreach (float v in new[] { va - 0.6f, vb + 0.6f })
                Detail("Aisle step light", g, P(r0 + 1.3f + 0.005f, v, top - 0.12f), new Vector3(0.18f, 0.04f, 0.01f), stepLight, face);
        }

        static void CinemaLobbyDetail(Transform g, WingId w, float y, float lobbyEnd, (string, float, float, float, float)[] screens)
        {
            Vector3 fwd = MallLayout.WingForward(w);
            Quaternion face = Quaternion.LookRotation(fwd), back = Quaternion.LookRotation(-fwd);
            Vector3 P(float u, float v, float h) => MallLayout.WingPoint(w, u, v, y + h);
            Material red = redMat;
            Material popcorn = ArtLibrary.Plain("Popcorn", new Color(0.95f, 0.85f, 0.55f), 0.2f);
            Material warmGlass = ArtLibrary.Plain("Popcorn_Glass_Lit", new Color(0.55f, 0.45f, 0.25f), 0.95f, 0f, new Color(0.6f, 0.45f, 0.15f), false);
            Material screen = ArtLibrary.Plain("Till_Screen", new Color(0.05f, 0.08f, 0.1f), 0.9f, 0f, new Color(0.1f, 0.25f, 0.4f), false);

            // ---- popcorn machine on the snacks counter ----
            Vector3 pm = P(100.5f, 8.7f, 1.1f);
            Detail("Popcorn machine base", g, pm + Vector3.up * 0.1f, new Vector3(0.75f, 0.2f, 0.55f), red, face);
            Detail("Popcorn machine cabinet", g, pm + Vector3.up * 0.55f, new Vector3(0.75f, 0.7f, 0.55f), warmGlass, face);
            Detail("Popcorn (heap)", g, pm + Vector3.up * 0.32f, new Vector3(0.7f, 0.22f, 0.5f), popcorn, face);
            Detail("Popcorn machine top", g, pm + Vector3.up * 0.95f, new Vector3(0.78f, 0.12f, 0.58f), red, face);
            Detail("Popcorn kettle", g, pm + Vector3.up * 0.75f, new Vector3(0.22f, 0.14f, 0.22f), metal, face);
            PropHum(g, pm + Vector3.up * 0.5f, ProceduralSounds.Ambience.Buzz, 0.08f, 6f);
            Sign(g, "POPCORN", pm + Vector3.up * 0.95f - fwd * 0.3f, -fwd, 0.7f, 0.02f, false);
            for (int k = 0; k < 6; k++)
                Detail("Popcorn carton", g, P(100.45f, 7.6f - k * 0.18f, 1.18f), new Vector3(0.13f, 0.16f, 0.13f), ArtLibrary.Plain("Popcorn_Carton", new Color(0.8f, 0.15f, 0.12f), 0.3f), face);
            // ---- drinks fountain ----
            Vector3 df = P(100.55f, 4.1f, 1.1f);
            Detail("Drinks fountain", g, df + Vector3.up * 0.4f, new Vector3(0.9f, 0.8f, 0.5f), blackMat, face);
            Detail("Drinks fountain panel", g, df + Vector3.up * 0.62f - fwd * 0.26f, new Vector3(0.8f, 0.3f, 0.01f), ArtLibrary.Plain("Fountain_Panel", new Color(0.15f, 0.3f, 0.6f), 0.6f, 0f, new Color(0.15f, 0.35f, 0.8f), false), face);
            for (int k = 0; k < 4; k++)
                Detail("Drinks nozzle", g, df + Vector3.up * 0.36f - fwd * 0.2f + MallLayout.WingRight(w) * (-0.3f + k * 0.2f), new Vector3(0.05f, 0.08f, 0.05f), metal, face);
            Detail("Drip tray", g, df + Vector3.up * 0.03f - fwd * 0.2f, new Vector3(0.82f, 0.03f, 0.14f), metal, face);
            for (int k = 0; k < 3; k++)
                Cyl("Cup stack", g, P(100.5f, 5.0f + k * 0.17f, 1.1f), P(100.5f, 5.0f + k * 0.17f, 1.1f + 0.3f + k * 0.08f), 0.11f, whiteFixture);

            // ---- ticket counter: screens facing the queue, ticket printers ----
            foreach (float v in new[] { -7.6f, -4.6f })
            {
                Detail("Ticket screen", g, P(100.35f, v, 1.36f), new Vector3(0.38f, 0.26f, 0.03f), screen, back * Quaternion.Euler(-10f, 0f, 0f));
                Detail("Ticket screen stand", g, P(100.4f, v, 1.17f), new Vector3(0.06f, 0.14f, 0.06f), blackMat, face);
                Detail("Ticket printer", g, P(100.6f, v + 0.45f, 1.17f), new Vector3(0.22f, 0.14f, 0.26f), blackMat, face);
                Detail("Ticket (left in printer)", g, P(100.47f, v + 0.45f, 1.21f), new Vector3(0.07f, 0.004f, 0.14f), whiteFixture, face * Quaternion.Euler(-20f, 0f, 0f));
            }

            // ---- belt-barrier queue in front of the tickets ----
            Material belt = ArtLibrary.Plain("Queue_Belt", new Color(0.55f, 0.06f, 0.08f), 0.4f);
            var posts = new[] { (98.6f, -9.2f), (98.6f, -7.2f), (98.6f, -5.2f), (98.6f, -3.2f), (96.8f, -3.2f), (96.8f, -5.2f), (96.8f, -7.2f) };
            for (int k = 0; k < posts.Length; k++)
            {
                var (u, v) = posts[k];
                Cyl("Queue post", g, P(u, v, 0f), P(u, v, 0.95f), 0.06f, metal, true);
                Cyl("Queue post base", g, P(u, v, 0f), P(u, v, 0.03f), 0.32f, metal);
                if (k + 1 < posts.Length && k != 5)
                {
                    var (u2, v2) = posts[k + 1];
                    Vector3 a = P(u, v, 0.9f), b = P(u2, v2, 0.9f);
                    Detail("Queue belt", g, (a + b) * 0.5f, new Vector3(0.008f, 0.05f, (b - a).magnitude), belt, Quaternion.LookRotation(b - a));
                }
            }
            // one belt unhooked, hanging to the floor
            {
                Vector3 a = P(96.8f, -5.2f, 0.9f);
                Detail("Queue belt (unhooked)", g, a + MallLayout.WingRight(w) * -0.03f + Vector3.down * 0.45f, new Vector3(0.008f, 0.9f, 0.05f), belt, face);
            }

            // ---- posters between the screen doors, backlit; the projection ports over each door ----
            string[] films = { "UNDERTOW\n15", "THE QUIET FLOOR\n18", "LAST ORDERS\n12A", "MIDNIGHT ROUNDS\n15", "SALT & STONE\nPG", "GLASS HOUSES\n12A" };
            Color[] tints = { new Color(0.1f, 0.2f, 0.35f), new Color(0.3f, 0.06f, 0.05f), new Color(0.35f, 0.25f, 0.08f), new Color(0.06f, 0.08f, 0.15f), new Color(0.12f, 0.25f, 0.18f), new Color(0.25f, 0.25f, 0.28f) };
            float[] spots = { -17f, -9.6f, -4.2f, 4.2f, 7.8f, 15.5f };
            for (int k = 0; k < spots.Length; k++)
            {
                Vector3 c = P(lobbyEnd - 0.17f, spots[k], 1.7f);
                Detail("Poster frame", g, c, new Vector3(1.1f, 1.6f, 0.06f), metal, back);
                Material face2 = ArtLibrary.Plain($"Film_Poster_{k}", tints[k], 0.8f, 0f, tints[k] * 1.6f, false);
                Detail("Poster", g, c - fwd * 0.035f, new Vector3(1.0f, 1.5f, 0.01f), face2, back);
                Sign(g, films[k], c - fwd * 0.045f - Vector3.up * 0.45f, -fwd, 0.9f, 0.025f, false);
            }
            foreach (var s in screens)
            {
                float vc = (s.Item4 + s.Item5) * 0.5f;
                foreach (float dv in new[] { -2.2f, 2.2f })
                    Detail("Projection port", g, P(lobbyEnd + 0.14f, vc + dv, 4.0f), new Vector3(0.45f, 0.3f, 0.02f), darkGlass, face);
            }
        }
    }
}
