using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// The forecourt beyond the glass: the Kestrel Fields totem sign (its lightbox half dead), an empty run of cycle
    /// stands, a litter bin and a smokers' bin by the canopy, planters with sodden shrubs, and across the road a
    /// pavement with a bus shelter whose advert panel still glows - and nobody waiting in it.
    /// </summary>
    public static partial class MallBuilder
    {
        static void ForecourtDetail(Transform g)
        {
            WingId w = WingId.South;
            float e = MallLayout.EntranceHallEnd;
            Vector3 fwd = MallLayout.WingForward(w), rt = MallLayout.WingRight(w);
            Vector3 P(float du, float v, float h) => MallLayout.WingPoint(w, e + du, v, h);
            Quaternion toMall = Quaternion.LookRotation(-fwd), outward = Quaternion.LookRotation(fwd);
            Material totem = ArtLibrary.Plain("Totem_Body", new Color(0.12f, 0.13f, 0.15f), 0.6f);
            Material totemFace = ArtLibrary.Plain("Totem_Face", new Color(0.25f, 0.3f, 0.35f), 0.7f, 0f, new Color(0.35f, 0.45f, 0.55f), false);
            Material totemDead = ArtLibrary.Plain("Totem_Face_Dead", new Color(0.12f, 0.14f, 0.16f), 0.7f);
            Material shrub = ArtLibrary.Plain("Shrub_Wet", new Color(0.08f, 0.16f, 0.07f), 0.55f);
            Material soil = ArtLibrary.Plain("Soil_Wet", new Color(0.08f, 0.06f, 0.04f), 0.7f);
            Material shelterGlass = TransparentMat("Shelter_Glass", new Color(0.5f, 0.6f, 0.65f, 0.18f));
            Material adGlow = ArtLibrary.Plain("Shelter_Advert", new Color(0.4f, 0.45f, 0.5f), 0.8f, 0f, new Color(1.2f, 1.3f, 1.45f), false);

            // ---- totem sign at the kerb, lightbox half dead ----
            Vector3 t = P(8.6f, -20f, 0f);
            Detail("Totem", g, t + Vector3.up * 2.2f, new Vector3(1.4f, 4.4f, 0.5f), totem, toMall);
            Detail("Totem face (lit)", g, t + Vector3.up * 3.3f - fwd * 0.26f, new Vector3(1.2f, 1.6f, 0.01f), totemFace, toMall);
            Detail("Totem face (dead)", g, t + Vector3.up * 1.6f - fwd * 0.26f, new Vector3(1.2f, 1.4f, 0.01f), totemDead, toMall);
            Sign(g, "KESTREL\nFIELDS", t + Vector3.up * 3.4f - fwd * 0.27f, -fwd, 1.1f, 0.05f, false);
            Sign(g, "SHOPPING CENTRE\n\nOVER 80 STORES\nFOOD COURT\nCINEMA  -  BOWLING", t + Vector3.up * 1.6f - fwd * 0.27f, -fwd, 1.1f, 0.022f, false);
            var tl = new GameObject("Totem glow");
            tl.transform.SetParent(g, false);
            tl.transform.localPosition = t + Vector3.up * 3.3f - fwd * 1.0f;
            Light l = tl.AddComponent<Light>();
            l.type = LightType.Point; l.range = 4f; l.intensity = 0.6f; l.color = new Color(0.65f, 0.8f, 1f); l.shadows = LightShadows.None; l.lightmapBakeType = LightmapBakeType.Realtime;

            // ---- cycle stands (empty) ----
            for (int k = 0; k < 5; k++)
            {
                float v = 13f + k * 1.1f;
                Vector3 a = P(6.5f, v, 0f), b = P(7.3f, v, 0f);
                Cyl("Cycle stand leg", g, a, a + Vector3.up * 0.75f, 0.05f, metal);
                Cyl("Cycle stand leg", g, b, b + Vector3.up * 0.75f, 0.05f, metal);
                Cyl("Cycle stand top", g, a + Vector3.up * 0.75f, b + Vector3.up * 0.75f, 0.05f, metal);
            }
            Detail("Cut lock (left on a stand)", g, P(6.9f, 15.2f, 0.6f), new Vector3(0.03f, 0.12f, 0.12f), ArtLibrary.Plain("Lock_Yellow", new Color(0.8f, 0.65f, 0.1f), 0.5f), toMall * Quaternion.Euler(0f, 0f, 30f));

            // ---- bins by the canopy ----
            Cyl("Litter bin", g, P(3.6f, 8.6f, 0f), P(3.6f, 8.6f, 0.95f), 0.55f, darkPanel);
            Cyl("Litter bin lid", g, P(3.6f, 8.6f, 0.95f), P(3.6f, 8.6f, 1.05f), 0.6f, blackMat);
            Cyl("Smokers' bin", g, P(3.6f, -8.6f, 0f), P(3.6f, -8.6f, 1.0f), 0.2f, metal);
            Cyl("Smokers' bin tray", g, P(3.6f, -8.6f, 1.0f), P(3.6f, -8.6f, 1.03f), 0.26f, blackMat);
            Detail("Drinks can (flattened)", g, P(4.4f, 7.6f, 0.01f), new Vector3(0.07f, 0.02f, 0.11f), ArtLibrary.Plain("Can_Silver", new Color(0.65f, 0.67f, 0.7f), 0.8f, 0.9f), Quaternion.Euler(0f, 40f, 0f));

            // ---- planters with sodden shrubs ----
            foreach (float v in new[] { -11.5f, 11.5f })
            {
                Detail("Forecourt planter", g, P(1.2f, v, 0.35f), new Vector3(2.2f, 0.7f, 1.0f), concrete, toMall);
                Detail("Planter soil", g, P(1.2f, v, 0.69f), new Vector3(2.05f, 0.02f, 0.85f), soil, toMall);
                for (int k = 0; k < 3; k++) Ball("Shrub", g, P(1.2f, v - 0.7f + k * 0.7f, 0.95f), 0.65f + 0.1f * k, shrub);
            }

            // ---- across the road: pavement and a bus shelter, its advert panel still lit ----
            Detail("Far pavement", g, P(32f, 0f, -0.1f), new Vector3(80f, 0.2f, 4f), concrete, outward);
            Detail("Far kerb", g, P(30.05f, 0f, -0.12f), new Vector3(80f, 0.22f, 0.15f), concrete, outward);
            Vector3 s = P(32.5f, -4f, 0f);
            foreach (float a in new[] { -1.8f, 1.8f })
            {
                Cyl("Shelter post", g, s + rt * a + fwd * 0.7f, s + rt * a + fwd * 0.7f + Vector3.up * 2.4f, 0.08f, metal);
                Cyl("Shelter post", g, s + rt * a - fwd * 0.5f, s + rt * a - fwd * 0.5f + Vector3.up * 2.4f, 0.08f, metal);
            }
            Detail("Shelter roof", g, s + Vector3.up * 2.45f, new Vector3(3.9f, 0.08f, 1.5f), metal, outward);
            Detail("Shelter back glass", g, s + fwd * 0.7f + Vector3.up * 1.3f, new Vector3(3.5f, 2.1f, 0.02f), shelterGlass, outward);
            Detail("Shelter end (advert)", g, s + rt * 1.8f + Vector3.up * 1.3f, new Vector3(0.12f, 1.9f, 1.15f), totem, outward);
            Detail("Advert panel", g, s + rt * 1.73f + Vector3.up * 1.3f, new Vector3(0.01f, 1.75f, 1.0f), adGlow, outward);
            Sign(g, "SUMMER SALE\nAT KESTREL FIELDS", s + rt * 1.72f + Vector3.up * 1.4f, -rt, 0.9f, 0.022f, false);
            Detail("Shelter seat", g, s + fwd * 0.5f + Vector3.up * 0.5f, new Vector3(2.0f, 0.06f, 0.25f), metal, outward);
            Detail("Bus stop flag", g, s - rt * 2.5f + Vector3.up * 2.6f, new Vector3(0.06f, 0.45f, 0.45f), ArtLibrary.Plain("BusStop_Red", new Color(0.65f, 0.08f, 0.06f), 0.5f), Quaternion.LookRotation(rt));
            Cyl("Bus stop pole", g, s - rt * 2.5f, s - rt * 2.5f + Vector3.up * 2.85f, 0.07f, metal);
            Sign(g, "N7  NIGHT SERVICE\nLAST BUS 23:40", s - rt * 2.5f + Vector3.up * 2.0f - fwd * 0.04f, -fwd, 0.35f, 0.01f);
            var sl = new GameObject("Shelter advert glow");
            sl.transform.SetParent(g, false);
            sl.transform.localPosition = s + rt * 1.2f + Vector3.up * 1.3f;
            Light l2 = sl.AddComponent<Light>();
            l2.type = LightType.Point; l2.range = 5f; l2.intensity = 0.7f; l2.color = new Color(0.8f, 0.88f, 1f); l2.shadows = LightShadows.None; l2.lightmapBakeType = LightmapBakeType.Realtime;
        }
    }
}
