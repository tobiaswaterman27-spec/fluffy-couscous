using UnityEngine;
using UnityEngine.Rendering;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// The food court kiosks, fitted out: a backlit menu board with real items and prices, the counter shutter
    /// pulled part-way down for the night, a till and a napkin dispenser, and each vendor's own kit on the back
    /// counter (kept clear of the middle, where things get left). Plus a tray-return station in the seating.
    /// </summary>
    public static partial class MallBuilder
    {
        static readonly string[][] KioskMenus =
        {
            new[] { "CLASSIC BURGER    4.49", "DOUBLE STACK      5.99", "CHICKEN BURGER    4.79", "FRIES            1.99", "MILKSHAKE         2.49" },
            new[] { "CHICKEN CHOW MEIN  5.20", "SWEET & SOUR       5.20", "EGG FRIED RICE     2.50", "SPRING ROLLS (3)   2.00", "PRAWN CRACKERS     1.20" },
            new[] { "MARGHERITA SLICE   2.50", "PEPPERONI SLICE    2.80", "GARLIC BREAD       1.90", "WHOLE PIZZA 12\"    9.99", "DIP               0.50" },
            new[] { "BEEF TACOS (2)     4.20", "CHICKEN BURRITO    5.50", "NACHOS             3.90", "QUESADILLA         4.60", "CHURROS            2.20" },
            new[] { "COD & CHIPS        6.50", "HADDOCK & CHIPS    6.90", "SAUSAGE & CHIPS    4.20", "MUSHY PEAS         1.10", "CURRY SAUCE        1.00" },
            new[] { "BERRY BLAST        3.20", "TROPICAL           3.20", "GREEN MACHINE      3.50", "PROTEIN SHAKE      3.90", "FRESH ORANGE       2.80" },
        };

        static void FoodCourtKioskDetail(Transform g, WingId w, float y, float end, int i, float v0, float v1)
        {
            var rng = new System.Random(900 + i);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Vector3 P(float u, float v, float h) => MallLayout.WingPoint(w, u, v, y + h);
            Vector3 fwd = MallLayout.WingForward(w), rt = MallLayout.WingRight(w);
            Quaternion face = Quaternion.LookRotation(-fwd);
            float vc = (v0 + v1) * 0.5f;
            float backTop = 0.95f;
            float uBack = end - 0.75f;
            // left and right working areas on the back counter (the middle stays clear)
            float lA = v0 + 0.8f, lB = vc - 0.55f, rA = vc + 0.55f, rB = v1 - 0.8f;

            Material steel = metal;
            Material dark = ArtLibrary.Plain("Kitchen_Black", new Color(0.06f, 0.06f, 0.07f), 0.45f, 0.3f);
            Material menuGlow = ArtLibrary.Plain("Menu_Backlight", new Color(0.12f, 0.1f, 0.08f), 0.5f, 0f, new Color(0.35f, 0.3f, 0.22f), true);
            Material redLamp = ArtLibrary.Plain("Heat_Lamp", new Color(0.5f, 0.1f, 0.05f), 0.5f, 0f, new Color(1.4f, 0.25f, 0.06f), false);
            Material oil = ArtLibrary.Plain("Fryer_Oil", new Color(0.35f, 0.25f, 0.06f), 0.95f);

            // ---- the menu board over the back counter, backlit (still on) ----
            Vector3 mb = P(end - 0.33f, vc, 2.45f);
            GameObject board = EditorBuild.Box("Menu board", g, mb, new Vector3(v1 - v0 - 1.4f, 0.95f, 0.06f), menuGlow, SurfaceType.Metal, face);
            Object.DestroyImmediate(board.GetComponent<Collider>());
            string menu = string.Join("\n", KioskMenus[i % KioskMenus.Length]);
            float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            TextMesh tm = EditorBuild.Label(g, menu, mb - fwd * 0.04f, yaw, 0.018f);
            tm.color = new Color(0.95f, 0.88f, 0.7f);
            tm.alignment = TextAlignment.Left;

            // ---- the counter shutter, part-way down (each kiosk a bit different), its lock box and pull hook ----
            float drop = i % 3 == 0 ? 1.0f : i % 3 == 1 ? 0.55f : 0.25f;   // how far down from the top (metres)
            GameObject sh = EditorBuild.Box("Counter shutter (part down)", g, P(end - 6.62f, vc, 3.2f - drop * 0.5f), new Vector3(v1 - v0 - 0.3f, drop, 0.05f), shutterMat, SurfaceType.Metal, face);
            Object.DestroyImmediate(sh.GetComponent<Collider>());
            Detail("Shutter bottom rail", g, P(end - 6.62f, vc, 3.2f - drop), new Vector3(v1 - v0 - 0.3f, 0.05f, 0.07f), steel, face);
            Detail("Shutter box", g, P(end - 6.55f, vc, 3.3f), new Vector3(v1 - v0 - 0.2f, 0.22f, 0.25f), wallService, face);

            // ---- on the front counter: till, napkins, a sauce caddy, a tip jar ----
            Vector3 till = P(end - 6.3f, v0 + 1.2f, 1.1f);
            Detail("Till base", g, till + Vector3.up * 0.05f, new Vector3(0.36f, 0.1f, 0.32f), dark, face);
            Detail("Till screen", g, till + Vector3.up * 0.25f - fwd * 0.02f, new Vector3(0.3f, 0.22f, 0.03f), dark, face * Quaternion.Euler(-15f, 0f, 0f));
            Detail("Card reader", g, till + rt * 0.3f + Vector3.up * 0.04f, new Vector3(0.08f, 0.08f, 0.14f), dark, face);
            Detail("Napkin dispenser", g, P(end - 6.3f, v1 - 1.3f, 1.18f), new Vector3(0.18f, 0.16f, 0.12f), steel, face);
            Cyl("Tip jar", g, P(end - 6.3f, v1 - 1.65f, 1.1f), P(end - 6.3f, v1 - 1.65f, 1.24f), 0.1f, glassMat);
            Material sauceR = ArtLibrary.Plain("Sauce_Red", new Color(0.6f, 0.06f, 0.04f), 0.7f);
            Material sauceY = ArtLibrary.Plain("Sauce_Yellow", new Color(0.8f, 0.65f, 0.1f), 0.7f);
            for (int k = 0; k < 3; k++)
            {
                Vector3 b = P(end - 6.32f, v1 - 1.0f + k * 0.07f, 1.1f);
                Cyl("Sauce bottle", g, b, b + Vector3.up * 0.18f, 0.055f, k == 1 ? sauceY : sauceR);
            }

            // ---- each vendor's kit ----
            switch (i % 6)
            {
                case 0:   // burgers: flat griddle, twin fryer, heat lamps over a pass
                    Detail("Griddle", g, P(uBack, (lA + lB) * 0.5f, backTop + 0.08f), new Vector3(lB - lA, 0.16f, 0.7f), steel, face);
                    Detail("Griddle plate", g, P(uBack, (lA + lB) * 0.5f, backTop + 0.17f), new Vector3(lB - lA - 0.1f, 0.02f, 0.6f), dark, face);
                    Detail("Fryer", g, P(uBack, (rA + rB) * 0.5f, backTop + 0.2f), new Vector3(rB - rA, 0.4f, 0.7f), steel, face);
                    for (int k = 0; k < 2; k++) Detail("Fryer well", g, P(uBack, rA + (rB - rA) * (0.27f + 0.46f * k), backTop + 0.405f), new Vector3((rB - rA) * 0.35f, 0.01f, 0.45f), oil, face);
                    for (int k = 0; k < 3; k++) Detail("Heat lamp", g, P(end - 1.0f, lA + k * 0.5f, 1.85f), new Vector3(0.35f, 0.06f, 0.2f), redLamp, face);
                    for (int k = 0; k < 5; k++) Detail("Burger box", g, P(uBack - 0.1f, rB - 0.15f, backTop + 0.45f + k * 0.08f), new Vector3(0.14f, 0.075f, 0.14f), ArtLibrary.Plain("Burger_Box", new Color(0.7f, 0.55f, 0.3f), 0.3f), face * Quaternion.Euler(0f, R(-8f, 8f), 0f));
                    break;
                case 1:   // wok station: three burners and woks, a rice cooker
                    for (int k = 0; k < 3; k++)
                    {
                        float v = lA + (lB - lA) * (0.18f + 0.32f * k);
                        Cyl("Wok burner", g, P(uBack, v, backTop), P(uBack, v, backTop + 0.12f), 0.42f, steel);
                        Cyl("Wok", g, P(uBack, v, backTop + 0.14f), P(uBack, v, backTop + 0.22f), 0.48f, dark);
                    }
                    Cyl("Rice cooker", g, P(uBack, (rA + rB) * 0.5f, backTop), P(uBack, (rA + rB) * 0.5f, backTop + 0.3f), 0.42f, ArtLibrary.Plain("Cooker_White", new Color(0.85f, 0.85f, 0.82f), 0.6f));
                    for (int k = 0; k < 4; k++) Detail("Noodle box", g, P(uBack - 0.15f, rB - 0.1f - k * 0.13f, backTop + 0.06f), new Vector3(0.1f, 0.12f, 0.1f), ArtLibrary.Plain("Noodle_Box", new Color(0.85f, 0.85f, 0.8f), 0.3f), face);
                    break;
                case 2:   // pizza: deck oven with a dark mouth, a peel leaning on it, boxes
                    Detail("Pizza oven", g, P(uBack - 0.05f, (lA + lB) * 0.5f, backTop + 0.5f), new Vector3(lB - lA, 1.0f, 0.85f), steel, face);
                    Detail("Pizza oven mouth", g, P(uBack - 0.48f, (lA + lB) * 0.5f, backTop + 0.42f), new Vector3((lB - lA) * 0.7f, 0.18f, 0.02f), dark, face);
                    Detail("Pizza oven handle", g, P(uBack - 0.52f, (lA + lB) * 0.5f, backTop + 0.58f), new Vector3((lB - lA) * 0.6f, 0.03f, 0.03f), steel, face);
                    Cyl("Pizza peel handle", g, P(uBack - 0.5f, lB + 0.15f, 0.1f), P(uBack - 0.55f, lB + 0.1f, 1.7f), 0.035f, woodMat);
                    for (int k = 0; k < 7; k++) Detail("Pizza box", g, P(uBack, (rA + rB) * 0.5f, backTop + 0.025f + k * 0.05f), new Vector3(0.36f, 0.045f, 0.36f), ArtLibrary.Plain("Pizza_Box", new Color(0.82f, 0.78f, 0.7f), 0.3f), face * Quaternion.Euler(0f, R(-6f, 6f), 0f));
                    break;
                case 3:   // tacos: hot plate, tortilla warmer, a rack of squeeze bottles
                    Detail("Hot plate", g, P(uBack, (lA + lB) * 0.5f, backTop + 0.06f), new Vector3(lB - lA, 0.12f, 0.6f), steel, face);
                    Cyl("Tortilla warmer", g, P(uBack, rA + 0.35f, backTop), P(uBack, rA + 0.35f, backTop + 0.16f), 0.4f, ArtLibrary.Plain("Warmer_Red", new Color(0.55f, 0.12f, 0.08f), 0.5f));
                    for (int k = 0; k < 6; k++)
                    {
                        Vector3 b = P(uBack, rB - 0.1f - k * 0.08f, backTop);
                        Cyl("Squeeze bottle", g, b, b + Vector3.up * 0.2f, 0.06f, k % 2 == 0 ? sauceR : ArtLibrary.Plain("Sauce_Green", new Color(0.15f, 0.45f, 0.12f), 0.7f));
                    }
                    break;
                case 4:   // fish bar: a long fryer range and a warm, lit glass cabinet
                    Detail("Fryer range", g, P(uBack, (lA + lB) * 0.5f, backTop + 0.25f), new Vector3(lB - lA, 0.5f, 0.75f), steel, face);
                    for (int k = 0; k < 3; k++) Detail("Fryer well", g, P(uBack, lA + (lB - lA) * (0.2f + 0.3f * k), backTop + 0.505f), new Vector3((lB - lA) * 0.22f, 0.01f, 0.5f), oil, face);
                    GameObject cab = Detail("Hot cabinet", g, P(uBack - 0.05f, (rA + rB) * 0.5f, backTop + 0.3f), new Vector3(rB - rA, 0.6f, 0.6f), glassMat, face);
                    Detail("Hot cabinet glow", g, P(uBack - 0.05f, (rA + rB) * 0.5f, backTop + 0.58f), new Vector3(rB - rA - 0.1f, 0.03f, 0.5f), redLamp, face);
                    Detail("Salt shaker", g, P(end - 6.3f, v1 - 1.9f, 1.15f), new Vector3(0.06f, 0.12f, 0.06f), steel, face);
                    break;
                case 5:   // smoothies: blenders in a row, fruit in a tray, a drinks fridge with a glass door (lit)
                    for (int k = 0; k < 4; k++)
                    {
                        float v = lA + (lB - lA) * (0.12f + 0.25f * k);
                        Detail("Blender base", g, P(uBack, v, backTop + 0.09f), new Vector3(0.18f, 0.18f, 0.2f), dark, face);
                        Cyl("Blender jug", g, P(uBack, v, backTop + 0.18f), P(uBack, v, backTop + 0.45f), 0.15f, glassMat);
                    }
                    Material[] fruit = { ArtLibrary.Plain("Fruit_Orange", new Color(0.85f, 0.45f, 0.06f), 0.5f), ArtLibrary.Plain("Fruit_Lime", new Color(0.35f, 0.6f, 0.12f), 0.5f), ArtLibrary.Plain("Fruit_Berry", new Color(0.35f, 0.05f, 0.18f), 0.5f) };
                    for (int k = 0; k < 14; k++) Ball("Fruit", g, P(end - 6.3f, v0 + 1.8f + R(-0.25f, 0.25f), 1.15f + R(0f, 0.05f)), R(0.07f, 0.1f), fruit[k % 3]);
                    Detail("Drinks fridge", g, P(end - 0.55f, rB - 0.1f, 0.95f), new Vector3(0.6f, 1.9f, 0.5f), dark, face);
                    Detail("Drinks fridge door", g, P(end - 0.82f, rB - 0.1f, 1.0f), new Vector3(0.52f, 1.6f, 0.02f), ArtLibrary.Plain("Fridge_Glow", new Color(0.3f, 0.4f, 0.45f), 0.9f, 0f, new Color(0.35f, 0.5f, 0.6f), false), face);
                    break;
            }
        }

        /// <summary>The food court's tray return station and its bins, between the seating and the kiosks.</summary>
        static void FoodCourtTrayStation(Transform g, WingId w, float y, float end)
        {
            Vector3 P(float u, float v, float h) => MallLayout.WingPoint(w, u, v, y + h);
            Quaternion face = Quaternion.LookRotation(MallLayout.WingForward(w));
            Material laminate = counterMat;
            Material trayMat = ArtLibrary.Plain("Tray_Brown", new Color(0.35f, 0.22f, 0.15f), 0.4f);
            float u = end - 8.6f;
            foreach (float v in new[] { -8f, 8f })
            {
                EditorBuild.Box("Tray return unit", g, P(u, v, 0.55f), new Vector3(1.6f, 1.1f, 0.55f), laminate, SurfaceType.Wood, face);
                for (int k = 0; k < 9; k++) Detail("Tray (stacked)", g, P(u, v - 0.35f, 1.11f + k * 0.022f), new Vector3(0.36f, 0.018f, 0.46f), trayMat, face * Quaternion.Euler(0f, (k % 3 - 1) * 3f, 0f));
                Detail("Bin opening", g, P(u - 0.28f, v + 0.4f, 0.75f), new Vector3(0.4f, 0.22f, 0.01f), blackMat, face);
                Sign(g, "TRAYS  ·  GENERAL WASTE  ·  RECYCLING", P(u - 0.29f, v, 0.98f), -MallLayout.WingForward(w), 1.5f, 0.012f, false);
                Sign(g, "PLEASE RETURN YOUR TRAY", P(u, v, 1.6f), -MallLayout.WingForward(w), 1.6f, 0.02f);
            }
        }
    }
}
