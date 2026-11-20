using System.Collections.Generic;
using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Room-specific fittings along the far wall of the back-of-house outer rooms: the staff canteen's kitchenette
    /// (sink, kettle, microwave, a fridge covered in notes), the maintenance workshops' benches (vice, pegboard of
    /// tools, step ladder, toolbox), the staff exit lobby (clocking-in machine, coat hooks with a coat left behind,
    /// bench, notice board) and the records office's filing cabinets (one drawer open, papers out).
    /// </summary>
    public static partial class MallBuilder
    {
        static readonly AreaRect FarWallStrip = new AreaRect(23f, 43.0f, 36.5f, MallLayout.QuadrantOuter);

        /// <summary>Adds the room's far-wall fitting (if it has one) and keeps Furnish clear of it.</summary>
        static void StaffRoomFeature(Transform g, MallFloor f, int sx, int sz, float y, string label, List<AreaRect> clearB)
        {
            switch (label)
            {
                case "Staff Canteen": clearB.Add(FarWallStrip.Mirrored(sx, sz)); Kitchenette(g, sx, sz, y); break;
                case "Maintenance Workshop":
                case "Maintenance": clearB.Add(FarWallStrip.Mirrored(sx, sz)); Workbench(g, sx, sz, y, f == MallFloor.Ground); break;
                case "Staff Exit Lobby": StaffExitLobby(g, sx, sz, y); break;
                case "Records Office": clearB.Add(FarWallStrip.Mirrored(sx, sz)); FilingCabinets(g, sx, sz, y); break;
            }
        }

        static Vector3 Q(int sx, int sz, float x, float h, float z, float y) => new Vector3(sx * x, y + h, sz * z);

        static void Kitchenette(Transform g, int sx, int sz, float y)
        {
            float wallZ = MallLayout.QuadrantOuter - 0.3f;
            Vector3 inward = new Vector3(0f, 0f, -sz);
            Quaternion face = Quaternion.LookRotation(inward);
            Vector3 P(float x, float h, float dz) => Q(sx, sz, x, h, wallZ - dz, y);
            var rng = new System.Random(4242);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material unit = ArtLibrary.Plain("Kitchen_Unit", new Color(0.82f, 0.8f, 0.74f), 0.4f);
            Material worktop = ArtLibrary.Plain("Worktop", new Color(0.3f, 0.28f, 0.26f), 0.5f);
            Material note = ArtLibrary.Plain("Sticky_Note", new Color(0.95f, 0.88f, 0.3f), 0.2f);
            Material mug = ArtLibrary.Plain("Mug", new Color(0.85f, 0.85f, 0.82f), 0.7f);

            EditorBuild.Box("Kitchen units", g, P(27.5f, 0.45f, 0.3f), new Vector3(8f, 0.9f, 0.6f), unit, SurfaceType.Wood, face);
            Detail("Worktop", g, P(27.5f, 0.92f, 0.31f), new Vector3(8.05f, 0.04f, 0.64f), worktop, face);
            for (int k = 0; k < 8; k++)
            {
                Detail("Cupboard door line", g, P(23.5f + k, 0.45f, 0.605f), new Vector3(0.01f, 0.82f, 0.005f), blackMat, face);
                Detail("Cupboard handle", g, P(23.85f + k, 0.75f, 0.615f), new Vector3(0.12f, 0.015f, 0.015f), metal, face);
            }
            EditorBuild.Box("Wall cupboards", g, P(26.5f, 1.85f, 0.17f), new Vector3(6f, 0.7f, 0.34f), unit, SurfaceType.Wood, face);
            Detail("Sink bowl", g, P(25.2f, 0.9f, 0.32f), new Vector3(0.5f, 0.06f, 0.4f), metal, face);
            Detail("Sink (dark)", g, P(25.2f, 0.93f, 0.32f), new Vector3(0.44f, 0.01f, 0.34f), blackMat, face);
            Cyl("Tap", g, P(25.2f, 0.94f, 0.1f), P(25.2f, 1.2f, 0.1f), 0.03f, metal);
            Cyl("Tap spout", g, P(25.2f, 1.2f, 0.1f), P(25.2f, 1.18f, 0.28f), 0.025f, metal);
            for (int k = 0; k < 5; k++) Cyl("Mug (in sink)", g, P(25.05f + R(-0.1f, 0.25f), 0.92f, 0.3f + R(-0.1f, 0.1f)), P(25.05f + R(-0.1f, 0.25f), 1.01f, 0.3f + R(-0.1f, 0.1f)), 0.08f, mug);
            Detail("Drainer", g, P(25.95f, 0.95f, 0.32f), new Vector3(0.45f, 0.02f, 0.38f), metal, face);
            Detail("Microwave", g, P(28.2f, 1.1f, 0.3f), new Vector3(0.5f, 0.3f, 0.38f), whiteFixture, face);
            Detail("Microwave door", g, P(28.12f, 1.1f, 0.495f), new Vector3(0.32f, 0.22f, 0.01f), darkGlass, face);
            Detail("Microwave clock", g, P(28.38f, 1.17f, 0.495f), new Vector3(0.08f, 0.03f, 0.005f), ArtLibrary.Plain("Clock_Green", new Color(0.05f, 0.3f, 0.1f), 0.6f, 0f, new Color(0.2f, 1.4f, 0.3f), false), face);
            Cyl("Kettle", g, P(27.2f, 0.94f, 0.3f), P(27.2f, 1.18f, 0.3f), 0.17f, metal);
            Detail("Kettle handle", g, P(27.2f, 1.1f, 0.18f), new Vector3(0.03f, 0.18f, 0.06f), blackMat, face);
            Detail("Toaster", g, P(29.2f, 1.03f, 0.3f), new Vector3(0.3f, 0.18f, 0.18f), metal, face);
            Detail("Tea caddy", g, P(26.6f, 1.0f, 0.2f), new Vector3(0.12f, 0.15f, 0.12f), redMat, face);
            Detail("Milk carton (open)", g, P(29.9f, 1.03f, 0.35f), new Vector3(0.08f, 0.2f, 0.08f), whiteFixture, face);
            // fridge, notes all over it
            EditorBuild.Box("Fridge", g, P(32.4f, 0.9f, 0.34f), new Vector3(0.7f, 1.8f, 0.66f), whiteFixture, SurfaceType.Metal, face);
            Detail("Fridge handle", g, P(32.1f, 1.2f, 0.69f), new Vector3(0.03f, 0.5f, 0.04f), metal, face);
            for (int k = 0; k < 9; k++)
                Detail("Note", g, P(32.4f + R(-0.25f, 0.25f), R(0.9f, 1.65f), 0.675f), new Vector3(0.08f, 0.08f, 0.003f), k % 3 == 0 ? whiteFixture : note, face * Quaternion.Euler(0f, 0f, R(-10f, 10f)));
            Sign(g, "WHOSE MILK IS THIS??\nTHROW IT OUT", P(32.4f, 1.5f, 0.682f), inward, 0.5f, 0.007f, false);
            Sign(g, "PLEASE WASH YOUR OWN MUGS\nTHIS MEANS YOU", P(27f, 1.4f, 0.01f), inward, 1.2f, 0.016f);
            // notice board
            Detail("Notice board", g, P(34.8f, 1.5f, 0.01f), new Vector3(1.4f, 0.9f, 0.02f), ArtLibrary.Plain("Cork", new Color(0.6f, 0.45f, 0.28f), 0.15f), face);
            for (int k = 0; k < 7; k++)
                Detail("Pinned sheet", g, P(34.8f + R(-0.55f, 0.55f), 1.5f + R(-0.3f, 0.3f), 0.025f), new Vector3(R(0.15f, 0.24f), R(0.2f, 0.3f), 0.002f), whiteFixture, face * Quaternion.Euler(0f, 0f, R(-6f, 6f)));
            Sign(g, "CHRISTMAS PARTY\nSIGN UP HERE!", P(34.5f, 1.75f, 0.03f), inward, 0.3f, 0.008f, false);
        }

        static void Workbench(Transform g, int sx, int sz, float y, bool ground)
        {
            float wallZ = MallLayout.QuadrantOuter - 0.3f;
            Vector3 inward = new Vector3(0f, 0f, -sz);
            Quaternion face = Quaternion.LookRotation(inward);
            Vector3 P(float x, float h, float dz) => Q(sx, sz, x, h, wallZ - dz, y);
            var rng = new System.Random(ground ? 515 : 616);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material bench = ArtLibrary.Plain("Workbench_Top", new Color(0.45f, 0.35f, 0.22f), 0.25f);
            Material peg = ArtLibrary.Plain("Pegboard_Brown", new Color(0.55f, 0.45f, 0.32f), 0.2f);
            Material toolRed = ArtLibrary.Plain("Toolbox_Red", new Color(0.65f, 0.08f, 0.06f), 0.5f);
            Material handle = ArtLibrary.Plain("Tool_Handle", new Color(0.8f, 0.6f, 0.1f), 0.4f);
            Material outline = ArtLibrary.Plain("Shadow_Outline", new Color(0.08f, 0.08f, 0.08f), 0.2f);

            EditorBuild.Box("Workbench", g, P(27f, 0.45f, 0.4f), new Vector3(5f, 0.9f, 0.8f), plantMat, SurfaceType.Metal, face);
            Detail("Workbench top", g, P(27f, 0.92f, 0.4f), new Vector3(5.1f, 0.05f, 0.85f), bench, face);
            // vice on the end
            Detail("Vice body", g, P(24.9f, 1.02f, 0.75f), new Vector3(0.18f, 0.14f, 0.3f), ArtLibrary.Plain("Vice_Blue", new Color(0.12f, 0.25f, 0.5f), 0.5f), face);
            Cyl("Vice screw", g, P(24.9f, 1.0f, 0.9f), P(24.9f, 1.0f, 1.05f), 0.03f, metal);
            Cyl("Vice handle", g, P(24.8f, 1.0f, 1.05f), P(25.0f, 1.0f, 1.05f), 0.02f, metal);
            // pegboard with tools hung on their painted outlines (some outlines empty)
            Detail("Pegboard", g, P(27f, 1.7f, 0.01f), new Vector3(4.6f, 1.2f, 0.02f), peg, face);
            string[] kinds = { "Spanner", "Hammer", "Screwdriver", "Saw", "Pliers", "Spanner", "Screwdriver", "Hammer", "Spanner", "Mallet" };
            for (int k = 0; k < kinds.Length; k++)
            {
                float x = 25.0f + k * 0.44f, h = 1.6f + (k % 2) * 0.35f;
                Vector3 size = kinds[k] == "Saw" ? new Vector3(0.12f, 0.45f, 0.01f) : kinds[k] == "Hammer" || kinds[k] == "Mallet" ? new Vector3(0.1f, 0.32f, 0.02f) : new Vector3(0.04f, 0.26f, 0.015f);
                Detail("Tool outline", g, P(x, h, 0.022f), size + new Vector3(0.02f, 0.02f, -0.008f), outline, face);
                if (rng.NextDouble() < 0.25) continue;   // tool missing
                Detail(kinds[k], g, P(x, h, 0.035f), size, kinds[k] == "Saw" ? metal : handle, face * Quaternion.Euler(0f, 0f, R(-4f, 4f)));
            }
            Sign(g, "PUT IT BACK WHERE YOU FOUND IT", P(27f, 2.45f, 0.012f), inward, 2.4f, 0.025f, false);
            // on the bench: toolbox open, a drill, an oil can, rags
            Detail("Toolbox", g, P(28.6f, 1.05f, 0.45f), new Vector3(0.55f, 0.22f, 0.25f), toolRed, face);
            Detail("Toolbox tray (open)", g, P(28.6f, 1.2f, 0.62f), new Vector3(0.55f, 0.02f, 0.22f), toolRed, face * Quaternion.Euler(-60f, 0f, 0f));
            Detail("Drill body", g, P(27.4f, 1.02f, 0.5f), new Vector3(0.24f, 0.1f, 0.07f), ArtLibrary.Plain("Drill_Yellow", new Color(0.85f, 0.65f, 0.05f), 0.5f), face * Quaternion.Euler(0f, 30f, 0f));
            Detail("Drill grip", g, P(27.48f, 0.99f, 0.6f), new Vector3(0.06f, 0.05f, 0.14f), blackMat, face * Quaternion.Euler(0f, 30f, 0f));
            Cyl("Oil can", g, P(26.3f, 0.95f, 0.3f), P(26.3f, 1.12f, 0.3f), 0.1f, ArtLibrary.Plain("OilCan_Green", new Color(0.15f, 0.35f, 0.15f), 0.5f, 0.6f));
            Detail("Rag", g, P(25.9f, 0.95f, 0.55f), new Vector3(0.25f, 0.02f, 0.2f), ArtLibrary.Plain("Rag_Oily", new Color(0.4f, 0.35f, 0.3f), 0.15f), face * Quaternion.Euler(0f, 20f, 0f));
            // step ladder leaning on the wall, an extension reel on the floor
            Vector3 lb = P(31.4f, 0f, 0.6f), lt = P(31.4f, 2.0f, 0.06f);
            foreach (float s in new[] { -0.22f, 0.22f })
                Cyl("Ladder stile", g, lb + Vector3.right * s * sx, lt + Vector3.right * s * sx, 0.05f, metal);
            for (int k = 1; k <= 6; k++)
            {
                Vector3 p = Vector3.Lerp(lb, lt, k / 7f);
                Cyl("Ladder rung", g, p - Vector3.right * 0.22f, p + Vector3.right * 0.22f, 0.03f, metal);
            }
            Cyl("Extension reel", g, P(33f, 0.15f, 0.5f) - Vector3.right * 0.1f, P(33f, 0.15f, 0.5f) + Vector3.right * 0.1f, 0.3f, ArtLibrary.Plain("Reel_Orange", new Color(0.85f, 0.4f, 0.05f), 0.5f));
            Detail("Cable (trailing)", g, P(33.6f, 0.01f, 1.2f), new Vector3(0.02f, 0.02f, 1.4f), ArtLibrary.Plain("Cable_Orange", new Color(0.85f, 0.4f, 0.05f), 0.5f), Quaternion.Euler(0f, 25f, 0f));
            Sign(g, "PPE MUST BE WORN\nIN THIS AREA", P(34.6f, 1.7f, 0.01f), inward, 0.9f, 0.018f);
        }

        static void StaffExitLobby(Transform g, int sx, int sz, float y)
        {
            float wallZ = MallLayout.QuadrantOuter - 0.3f;
            Vector3 inward = new Vector3(0f, 0f, -sz);
            Quaternion face = Quaternion.LookRotation(inward);
            Vector3 P(float x, float h, float dz) => Q(sx, sz, x, h, wallZ - dz, y);
            Material coat = ArtLibrary.Plain("Coat_Navy", new Color(0.08f, 0.1f, 0.2f), 0.15f);
            Material scarf = ArtLibrary.Plain("Scarf_Red", new Color(0.6f, 0.1f, 0.08f), 0.1f);
            // clocking-in machine and card rack beside the exit
            Vector3 cm = P(35.3f, 1.4f, 0.12f);
            EditorBuild.Box("Clocking machine", g, cm, new Vector3(0.35f, 0.42f, 0.22f), ArtLibrary.Plain("Clock_Machine", new Color(0.6f, 0.6f, 0.58f), 0.5f), SurfaceType.Metal, face);
            Detail("Clock face", g, cm + inward * 0.115f + Vector3.up * 0.08f, new Vector3(0.18f, 0.12f, 0.005f), ArtLibrary.Plain("Clock_LCD", new Color(0.1f, 0.15f, 0.05f), 0.6f, 0f, new Color(0.4f, 0.8f, 0.2f), false), face);
            Sign(g, "03:17", cm + inward * 0.12f + Vector3.up * 0.08f, inward, 0.16f, 0.012f, false);
            Detail("Card slot", g, cm + inward * 0.115f - Vector3.up * 0.1f, new Vector3(0.12f, 0.02f, 0.005f), blackMat, face);
            Detail("Card rack", g, P(34.4f, 1.4f, 0.03f), new Vector3(0.6f, 0.8f, 0.04f), metal, face);
            for (int k = 0; k < 12; k++) Detail("Clock card", g, P(34.17f + (k % 4) * 0.15f, 1.15f + (k / 4) * 0.25f, 0.06f), new Vector3(0.09f, 0.18f, 0.004f), ArtLibrary.Plain("Clock_Card", new Color(0.85f, 0.8f, 0.6f), 0.2f), face);
            Sign(g, "CLOCK OUT BEFORE YOU LEAVE", P(34.8f, 2.0f, 0.01f), inward, 1.4f, 0.018f);
            // coat hooks with the one coat nobody came back for
            Detail("Coat rail", g, P(27.5f, 1.75f, 0.03f), new Vector3(3f, 0.1f, 0.03f), woodMat, face);
            for (int k = 0; k < 8; k++) Detail("Coat hook", g, P(26.2f + k * 0.38f, 1.72f, 0.08f), new Vector3(0.03f, 0.06f, 0.08f), metal, face);
            Detail("Coat (left behind)", g, P(27.34f, 1.25f, 0.14f), new Vector3(0.48f, 0.9f, 0.16f), coat, face);
            Detail("Coat sleeve", g, P(27.08f, 1.15f, 0.16f), new Vector3(0.1f, 0.6f, 0.1f), coat, face * Quaternion.Euler(0f, 0f, -6f));
            Detail("Scarf", g, P(28.1f, 1.45f, 0.1f), new Vector3(0.12f, 0.6f, 0.03f), scarf, face);
            // bench under the hooks, a pair of shoes under it
            EditorBuild.Box("Lobby bench", g, P(27.5f, 0.22f, 0.3f), new Vector3(2.4f, 0.44f, 0.4f), woodMat, SurfaceType.Wood, face);
            Detail("Shoe", g, P(27.0f, 0.05f, 0.75f), new Vector3(0.1f, 0.09f, 0.27f), blackMat, face * Quaternion.Euler(0f, 10f, 0f));
            Detail("Shoe", g, P(27.18f, 0.05f, 0.78f), new Vector3(0.1f, 0.09f, 0.27f), blackMat, face * Quaternion.Euler(0f, -5f, 0f));
            // notice board
            Detail("Notice board", g, P(31f, 1.5f, 0.01f), new Vector3(1.6f, 1.0f, 0.02f), ArtLibrary.Plain("Cork", new Color(0.6f, 0.45f, 0.28f), 0.15f), face);
            Sign(g, "NIGHT SECURITY ROTA\n\nMON  G. DOYLE\nTUE  G. DOYLE\nWED  -\nTHU  -\nFRI  -", P(30.7f, 1.55f, 0.025f), inward, 0.5f, 0.009f);
            Sign(g, "REMEMBER: NOBODY\nIN AFTER CLOSE.\nNO EXCEPTIONS.", P(31.45f, 1.55f, 0.025f), inward, 0.5f, 0.01f);
        }

        static void FilingCabinets(Transform g, int sx, int sz, float y)
        {
            float wallZ = MallLayout.QuadrantOuter - 0.3f;
            Vector3 inward = new Vector3(0f, 0f, -sz);
            Quaternion face = Quaternion.LookRotation(inward);
            Vector3 P(float x, float h, float dz) => Q(sx, sz, x, h, wallZ - dz, y);
            var rng = new System.Random(1717);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material cab = ArtLibrary.Plain("Filing_Grey", new Color(0.5f, 0.52f, 0.54f), 0.45f, 0.4f);
            Material folder = ArtLibrary.Plain("Folder_Manila", new Color(0.85f, 0.72f, 0.45f), 0.25f);
            for (int k = 0; k < 12; k++)
            {
                float x = 23.6f + k * 0.52f;
                EditorBuild.Box("Filing cabinet", g, P(x, 0.66f, 0.33f), new Vector3(0.48f, 1.32f, 0.62f), cab, SurfaceType.Metal, face);
                for (int d = 0; d < 4; d++)
                {
                    float h = 0.17f + d * 0.32f;
                    bool open = k == 5 && d == 2;
                    float out_ = open ? 0.45f : 0f;
                    Detail("Drawer front", g, P(x, h, 0.645f + out_), new Vector3(0.44f, 0.29f, 0.01f), cab, face);
                    Detail("Drawer handle", g, P(x, h + 0.06f, 0.66f + out_), new Vector3(0.14f, 0.02f, 0.02f), metal, face);
                    Detail("Drawer label", g, P(x, h + 0.1f, 0.651f + out_), new Vector3(0.08f, 0.04f, 0.003f), whiteFixture, face);
                    if (open)
                    {
                        Detail("Drawer (pulled out)", g, P(x, h, 0.42f + out_ * 0.5f), new Vector3(0.42f, 0.25f, 0.6f), cab, face);
                        for (int p = 0; p < 10; p++) Detail("Hanging file", g, P(x, h + 0.08f, 0.55f + p * 0.035f), new Vector3(0.38f, 0.22f, 0.006f), folder, face * Quaternion.Euler(R(-8f, 8f), 0f, 0f));
                    }
                }
                if (k % 4 == 1) for (int p = 0; p < 4; p++) Detail("Box file (on top)", g, P(x + R(-0.08f, 0.08f), 1.36f + p * 0.07f, 0.33f), new Vector3(0.3f, 0.07f, 0.38f), p % 2 == 0 ? folder : ArtLibrary.Plain("BoxFile_Blue", new Color(0.15f, 0.25f, 0.5f), 0.3f), face * Quaternion.Euler(0f, R(-10f, 10f), 0f));
            }
            // papers out across the floor in front of the open drawer
            for (int k = 0; k < 9; k++)
                Detail("Loose paper", g, P(23.6f + 5 * 0.52f + R(-0.8f, 0.8f), 0.003f, R(1.0f, 2.2f)), new Vector3(0.21f, 0.002f, 0.297f), whiteFixture, Quaternion.Euler(0f, R(0f, 180f), 0f));
            Sign(g, "RECORDS 1998 - 2019\nDO NOT REMOVE", P(26.5f, 1.85f, 0.01f), inward, 1.4f, 0.02f);
        }
    }
}
