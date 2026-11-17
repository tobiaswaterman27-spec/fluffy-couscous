using System.Collections.Generic;
using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    public static partial class MallBuilder
    {
        // Staff notices. Mostly ordinary... read enough of them and a picture forms.
        static readonly string[] StaffNotes =
        {
            "STAFF NOTICE\n\nThe service lift is\nOUT OF ORDER\nuse the stairs\n\n- Facilities",
            "REMINDER\n\nVent grilles in the\nservice corridors are\nNOT to be removed\nfor ANY reason.\n\n- Management",
            "Has anyone seen\nthe cleaning keys?\nLeft them on the\ntrolley by the bins\n- R",
            "MISSING\n\nGARY DOYLE\nnight porter\nlast seen 14 Feb\nloading bay\n\nplease call\n07700 900 412",
            "PEST CONTROL\nvisit cancelled\n\ncontractor refused\nto go in the ducts\nagain",
            "Christmas party\nsign up sheet\n\nDave x2\nSharon\nMo\nKaren",
            "FIRE DRILL\nevery Tuesday\n10:00\n\nassemble in\ncar park B",
            "Whoever keeps\nleaving food by\nthe vent in the\nback corridor\nPLEASE STOP\nit is attracting\nsomething",
            "NIGHT STAFF\n\ndo not prop the\nfire doors open\n\ndo not go into\nthe plant room\nalone",
            "LOST: grey cardigan\nstaff room\nreward: biscuits",
            "CLOSING DOWN\n\nfinal trading day\n31 March\n\nthank you for\n34 years",
            "Security - please\nlook at camera 5.\nit keeps cutting out.\nsomething in front\nof the lens?",
            "Noises in the ceiling\nover unit G14 again.\nMaintenance say\nit's the pipes.\n\nit is not the pipes",
            "Union meeting\nThursday 6pm\nstaff room\n\nredundancy terms",
        };

        /// <summary>
        /// Cork notice boards in the service corridors, with pinned sheets, sticky notes and a laminated rota.
        /// </summary>
        static void DressNotices(Transform parent)
        {
            Material cork = Mat("Cork", new Color(0.55f, 0.4f, 0.26f), 0.1f);
            Material frame = Mat("Board frame", new Color(0.4f, 0.38f, 0.35f), 0.4f, 0.6f);
            Material paper = Mat("Paper", new Color(0.86f, 0.84f, 0.78f), 0.1f);
            Material yellow = Mat("Sticky note", new Color(0.88f, 0.8f, 0.35f), 0.1f);
            Material pink = Mat("Paper pink", new Color(0.82f, 0.6f, 0.62f), 0.1f);
            int note = 0;
            foreach (MallFloor f in MallLayout.ShopFloors)
            {
                float y = MallLayout.FloorY(f);
                foreach (WingId w in MallLayout.Wings)
                    foreach (int side in new[] { -1, 1 })
                    {
                        int h = Mathf.Abs(MallLayout.Hash((int)f, (int)w, side + 5, 818));
                        if (h % 3 == 0) continue;
                        float u = 26.5f + (h % 5) * 0.6f;
                        Vector3 into = -MallLayout.WingRight(w) * side;      // from the corridor's outer wall, back towards the shops
                        Vector3 along = MallLayout.WingForward(w);
                        Vector3 c = MallLayout.WingPoint(w, u, side * (MallLayout.CorridorOuter - 0.02f), y + 1.55f);
                        Transform g = Group("Notice board", parent);
                        // board: frame, cork face
                        Vector3 half = along * 0.6f + Vector3.up * 0.42f;
                        Decor("Board frame", g, c - half, c + half + into * 0.025f, frame);
                        Vector3 face = c + into * 0.026f;
                        Vector3 inner = along * 0.56f + Vector3.up * 0.38f;
                        Decor("Cork", g, face - inner, face + inner + into * 0.004f, cork);
                        float yaw = Mathf.Atan2(-into.x, -into.z) * Mathf.Rad2Deg;   // text read by someone facing the wall
                        // 3-5 sheets
                        int count = 3 + h % 3;
                        for (int i = 0; i < count; i++)
                        {
                            float px = -0.42f + i * (0.84f / Mathf.Max(1, count - 1)) + ((h >> i) % 5 - 2) * 0.012f;
                            float py = ((h >> (i + 2)) % 2 == 0 ? 0.13f : -0.12f) + ((h >> (i + 3)) % 3 - 1) * 0.03f;
                            bool sticky = (h >> (i + 1)) % 4 == 0;
                            Vector2 size = sticky ? new Vector2(0.076f, 0.076f) : new Vector2(0.21f, 0.297f) * 0.85f;
                            Material m = sticky ? yellow : ((h >> i) % 7 == 0 ? pink : paper);
                            Vector3 sc = face + into * (0.006f + i * 0.0015f) + along * px + Vector3.up * py;
                            float tilt = ((h >> (i * 2)) % 9 - 4) * 1.4f;
                            GameObject sheet = Decor("Notice", g, Vector3.zero, Vector3.one, m);
                            sheet.transform.localPosition = sc;
                            sheet.transform.localRotation = Quaternion.LookRotation(-into, Vector3.up) * Quaternion.Euler(0f, 0f, tilt);
                            sheet.transform.localScale = new Vector3(size.x, size.y, 0.001f);
                            // drawing pin
                            Decor("Pin", g, sc + Vector3.up * (size.y * 0.42f) - along * 0.006f - Vector3.up * 0.006f,
                                sc + Vector3.up * (size.y * 0.42f) + along * 0.006f + Vector3.up * 0.006f + into * 0.008f, (i % 2 == 0) ? Mat("Pin red", new Color(0.7f, 0.1f, 0.08f), 0.7f) : Mat("Pin blue", new Color(0.1f, 0.2f, 0.6f), 0.7f));
                            if (sticky) continue;
                            string text = StaffNotes[note++ % StaffNotes.Length];
                            TextMesh t = EditorBuild.Label(g, text, sc + into * 0.0012f, yaw, 0.0022f);
                            t.transform.localRotation = Quaternion.LookRotation(-into, Vector3.up) * Quaternion.Euler(0f, 0f, tilt);
                            t.color = new Color(0.1f, 0.1f, 0.12f);
                        }
                    }
            }
        }
    }
}
