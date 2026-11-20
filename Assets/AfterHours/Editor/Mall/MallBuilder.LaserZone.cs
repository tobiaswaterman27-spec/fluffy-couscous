using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Laser Zone: the kit room before the arena - a rack of vests with their sensor pads still glowing on charge,
    /// phasers in cradles, a briefing screen with last night's scores, and the black-painted arena doorway with a
    /// UV strip and splashes of fluorescent paint around it.
    /// </summary>
    public static partial class MallBuilder
    {
        static bool SpecialShop6(Transform g, MallUnit u, float y, int side)
        {
            if (u.name != "Laser Zone") return false;
            WingId w = u.wing;
            Vector3 L(float du, float dv, float h) => MallLayout.WingPoint(w, u.u0 + du, side * dv, y + h);
            Vector3 fwd = MallLayout.WingForward(w), inShop = MallLayout.WingRight(w) * side;
            float len = u.u1 - u.u0;
            var rng = new System.Random(u.code.GetHashCode());
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material black = ArtLibrary.Plain("Arena_Black", new Color(0.02f, 0.02f, 0.025f), 0.2f);
            Material vest = ArtLibrary.Plain("Vest_Black", new Color(0.06f, 0.06f, 0.07f), 0.3f);
            Material padRed = ArtLibrary.Plain("Pad_Red", new Color(0.5f, 0.05f, 0.05f), 0.6f, 0f, new Color(2.2f, 0.15f, 0.1f), false);
            Material padBlue = ArtLibrary.Plain("Pad_Blue", new Color(0.05f, 0.1f, 0.5f), 0.6f, 0f, new Color(0.15f, 0.35f, 2.4f), false);
            Material uv = ArtLibrary.Plain("UV_Strip", new Color(0.3f, 0.1f, 0.6f), 0.6f, 0f, new Color(1.2f, 0.35f, 2.6f), false);
            Color[] neon = { new Color(0.2f, 1.0f, 0.3f), new Color(1.0f, 0.2f, 0.8f), new Color(1.0f, 0.9f, 0.1f), new Color(0.2f, 0.8f, 1.0f) };
            Quaternion faceRoom = Quaternion.LookRotation(-fwd);
            // the arena: the whole back partition painted black, a doorway framed by a UV strip
            // (in two pieces, either side of the doorway)
            float pa = len - 2.75f, pb = len - 1.55f;
            Detail("Arena wall paint", g, L((0.2f + pa) * 0.5f, 13.5f, 1.8f), new Vector3(pa - 0.2f, 3.6f, 0.01f), black, Quaternion.LookRotation(-inShop));
            Detail("Arena wall paint", g, L((pb + len - 0.2f) * 0.5f, 13.5f, 1.8f), new Vector3(len - 0.2f - pb, 3.6f, 0.01f), black, Quaternion.LookRotation(-inShop));
            Detail("Arena wall paint (over door)", g, L(len - 2.15f, 13.5f, 2.9f), new Vector3(1.2f, 1.4f, 0.01f), black, Quaternion.LookRotation(-inShop));
            Vector3 door = L(len - 2.15f, 13.5f, 0f);   // the stock-room doorway doubles as the arena entrance
            foreach (float s in new[] { -0.6f, 0.6f })
                Detail("UV strip (door)", g, door + fwd * s + Vector3.up * 1.05f - inShop * 0.02f, new Vector3(0.04f, 2.1f, 0.02f), uv, Quaternion.LookRotation(-inShop));
            Detail("UV strip (lintel)", g, door + Vector3.up * 2.12f - inShop * 0.02f, new Vector3(1.24f, 0.04f, 0.02f), uv, Quaternion.LookRotation(-inShop));
            Sign(g, "ARENA", door + Vector3.up * 2.4f - inShop * 0.03f, -inShop, 1.0f, 0.04f, false);
            for (int k = 0; k < 16; k++)
                Detail("Neon splash", g, L(R(0.8f, len - 3.4f), 13.48f, R(0.3f, 3.2f)), new Vector3(R(0.08f, 0.35f), R(0.05f, 0.25f), 0.004f),
                    ArtLibrary.Plain($"Neon_{k % 4}", neon[k % 4] * 0.4f, 0.5f, 0f, neon[k % 4] * 1.4f, false), Quaternion.LookRotation(-inShop) * Quaternion.Euler(0f, 0f, R(0f, 180f)));
            var lg = new GameObject("UV glow");
            lg.transform.SetParent(g, false);
            lg.transform.localPosition = door + Vector3.up * 1.6f - inShop * 0.8f;
            Light l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.range = 4f; l.intensity = 0.9f; l.color = new Color(0.55f, 0.25f, 1f); l.shadows = LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Realtime;

            // vest rack on the far side wall: vests on hooks, pads glowing (charging), alternate teams
            Vector3 rack = L(len - 0.3f, 9.4f, 0f);
            Detail("Vest rack rail", g, rack + Vector3.up * 1.9f, new Vector3(2.6f, 0.05f, 0.05f), metal, faceRoom);
            for (int k = 0; k < 8; k++)
            {
                Vector3 p = rack + inShop * (-1.15f + k * 0.33f) + Vector3.up * 1.45f - fwd * 0.12f;
                Detail("Vest", g, p, new Vector3(0.3f, 0.65f, 0.14f), vest, faceRoom);
                Material pad = k % 2 == 0 ? padRed : padBlue;
                Detail("Chest pad", g, p - fwd * 0.075f + Vector3.up * 0.1f, new Vector3(0.12f, 0.12f, 0.01f), pad, faceRoom);
                Detail("Shoulder pad", g, p + Vector3.up * 0.3f - fwd * 0.02f + inShop * 0.1f, new Vector3(0.06f, 0.04f, 0.08f), pad, faceRoom);
                Detail("Phaser", g, p - Vector3.up * 0.45f - fwd * 0.05f, new Vector3(0.06f, 0.12f, 0.3f), vest, faceRoom * Quaternion.Euler(-70f, 0f, 0f));
            }
            // briefing screen on the near side wall, with the scores
            Vector3 sc = L(0.29f, 9.6f, 1.9f);
            Detail("Briefing screen", g, sc, new Vector3(1.6f, 0.9f, 0.05f), blackMat, Quaternion.LookRotation(fwd));
            Detail("Briefing screen face", g, sc + fwd * 0.03f, new Vector3(1.5f, 0.82f, 0.005f), ArtLibrary.Plain("Briefing_Glow", new Color(0.04f, 0.04f, 0.1f), 0.9f, 0f, new Color(0.25f, 0.25f, 0.7f), false), Quaternion.LookRotation(fwd));
            Sign(g, "LAST GAME  22:40\n\nRED TEAM    4,250\nBLUE TEAM   3,980\n\nTOP GUN: GHOST_77", sc + fwd * 0.036f, fwd, 1.3f, 0.02f, false);
            Sign(g, "LASER TAG  -  PARTIES\nNO RUNNING IN THE ARENA", L(1.2f, 6.8f, 1.8f), -inShop, 1.1f, 0.02f);
            return true;
        }
    }
}
