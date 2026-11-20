using UnityEngine;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Stairwell detail per flight: yellow anti-slip nosings on every step (worn away in the middle of the busy
    /// flights), wall brackets holding the wall-side handrails, and on each landing a board saying where you are.
    /// </summary>
    public static partial class MallBuilder
    {
        static void StairFlightDetail(Transform g, int sx, int sz, float y0, float half, int level, int levelCount)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(half / 0.18f));
            float rise = half / n, run = 4.5f / n;
            Material nosing = ArtLibrary.Plain("Stair_Nosing", new Color(0.8f, 0.62f, 0.1f), 0.25f);
            Material worn = ArtLibrary.Plain("Stair_Nosing_Worn", new Color(0.35f, 0.3f, 0.18f), 0.2f);
            bool busy = level == 1;   // ground to level 1 gets the most feet
            float wA = 2.9f - CoreT, cA = (CoreT + 2.9f) * 0.5f;
            float wB = CoreW - CoreT - 3.1f, cB = (3.1f + CoreW - CoreT) * 0.5f;
            for (int k = 0; k < n; k++)
            {
                float topA = y0 + (k + 1) * rise;
                float topB = y0 + half + (k + 1) * rise;
                Vector3 a = CoreP(sx, sz, cA, topA + 0.004f, 2.25f + k * run + 0.04f);
                Vector3 b = CoreP(sx, sz, cB, topB + 0.004f, 6.75f - k * run - 0.04f);
                if (busy && k % 2 == 0)
                {
                    // the middle third trodden down to bare grit
                    Detail("Stair nosing", g, a, new Vector3(wA, 0.008f, 0.06f), nosing);
                    Detail("Stair nosing (worn)", g, a + Vector3.up * 0.001f, new Vector3(wA * 0.35f, 0.008f, 0.062f), worn);
                    Detail("Stair nosing", g, b, new Vector3(wB, 0.008f, 0.06f), nosing);
                    Detail("Stair nosing (worn)", g, b + Vector3.up * 0.001f, new Vector3(wB * 0.35f, 0.008f, 0.062f), worn);
                }
                else
                {
                    Detail("Stair nosing", g, a, new Vector3(wA, 0.008f, 0.06f), nosing);
                    Detail("Stair nosing", g, b, new Vector3(wB, 0.008f, 0.06f), nosing);
                }
            }
            // brackets for the wall-side rails (x = 0.3 on flight A, x = CoreW - 0.3 on flight B)
            for (int k = 0; k < 3; k++)
            {
                float t = 0.15f + k * 0.35f;
                float la = Mathf.Lerp(2.25f, 6.75f, t), ha = y0 + 0.95f + half * t;
                Detail("Handrail bracket", g, CoreP(sx, sz, (CoreT + 0.3f) * 0.5f, ha - 0.04f, la), new Vector3(Mathf.Abs(0.3f - CoreT), 0.025f, 0.03f), metal);
                float lb = Mathf.Lerp(6.75f, 2.25f, t), hb = y0 + half + 0.95f + half * t;
                Detail("Handrail bracket", g, CoreP(sx, sz, (CoreW - CoreT + CoreW - 0.3f) * 0.5f, hb - 0.04f, lb), new Vector3(Mathf.Abs(0.3f - CoreT), 0.025f, 0.03f), metal);
            }
            // landing board on the far-end wall of this level's landing
            string[] names = { "CAR PARK", "GROUND FLOOR", "LEVEL 1", "LEVEL 2", "ROOF" };
            string here = level < names.Length ? names[level] : "";
            string down = level > 0 ? "DOWN: " + names[level - 1] : "";
            string up = level + 1 < levelCount ? "UP: " + names[level + 1] : "";
            Vector3 intoRoom = sz > 0 ? Vector3.forward : Vector3.back;
            Vector3 c = CoreP(sx, sz, 1.5f, y0 + 1.6f, CoreT + 0.02f);
            Detail("Landing board", g, c, new Vector3(1.1f, 0.6f, 0.015f), darkPanel);
            Sign(g, $"{here}\n\n{up}\n{down}", c + intoRoom * 0.012f, intoRoom, 1.0f, 0.016f, false);
        }
    }
}
