using UnityEngine;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Escalator trim: rounded newel ends where the handrail turns under at the top and bottom, yellow comb-plate
    /// edges, skirt brushes down both sides of the steps, stainless deck caps on the balustrades, and the safety
    /// plate at each entry (hold the handrail, hold children, no buggies).
    /// </summary>
    public static partial class MallBuilder
    {
        static void EscalatorTrim(Transform g, string name, Vector3 bottom, Vector3 top, float width, Vector3 dir, Vector3 right, Vector3 normal, float length)
        {
            Material brush = ArtLibrary.Plain("Skirt_Brush", new Color(0.08f, 0.08f, 0.08f), 0.1f);
            Material steel = ArtLibrary.Plain("Escalator_Steel", new Color(0.72f, 0.73f, 0.75f), 0.75f, 1f);
            Vector3 flat = new Vector3(dir.x, 0f, dir.z).normalized;
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
            Vector3 mid = (bottom + top) * 0.5f;
            foreach (int sgn in new[] { -1, 1 })
            {
                Vector3 side = right * (sgn * (width * 0.5f + 0.06f));
                // newel ends: the handrail's round turn-under at each end
                foreach (Vector3 end in new[] { bottom - flat * 0.55f, top + flat * 0.55f })
                {
                    Vector3 c = end + side + Vector3.up * 0.95f;
                    Cyl(name + " newel", g, c - right * 0.06f, c + right * 0.06f, 0.36f, blackMat);
                    Cyl(name + " newel hub", g, c - right * 0.065f, c + right * 0.065f, 0.12f, steel);
                }
                // skirt brush strip along the step edge
                Detail(name + " skirt brush", g, mid + right * (sgn * (width * 0.5f - 0.015f)) + normal * 0.12f, new Vector3(0.03f, 0.05f, length), brush, rot);
                // deck cap along the bottom of the balustrade
                Detail(name + " inner deck", g, mid + right * (sgn * (width * 0.5f + 0.005f)) + normal * 0.2f, new Vector3(0.02f, 0.15f, length + 0.9f), steel, rot);
            }
            // yellow comb edges
            Detail(name + " comb edge", g, bottom - flat * 0.21f + Vector3.up * 0.014f, new Vector3(width - 0.06f, 0.006f, 0.05f), yellowPaint, Quaternion.LookRotation(flat));
            Detail(name + " comb edge", g, top + flat * 0.21f + Vector3.up * 0.014f, new Vector3(width - 0.06f, 0.006f, 0.05f), yellowPaint, Quaternion.LookRotation(flat));
            // safety plates facing anyone about to step on (bottom end faces back down the run, top end faces back up)
            foreach (bool atBottom in new[] { true, false })
            {
                Vector3 end = atBottom ? bottom - flat * 0.85f : top + flat * 0.85f;
                Vector3 viewer = atBottom ? -flat : flat;
                Vector3 c = end + right * (width * 0.5f + 0.06f) + Vector3.up * 0.7f;
                Detail(name + " safety plate", g, c, new Vector3(0.18f, 0.3f, 0.02f), whiteFixture, Quaternion.LookRotation(viewer));
                Sign(g, "HOLD THE\nHANDRAIL\n\nHOLD\nCHILDREN\n\nNO BUGGIES", c + viewer * 0.012f, viewer, 0.2f, 0.006f, false);
            }
        }
    }
}
