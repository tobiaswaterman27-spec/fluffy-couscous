using UnityEngine;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// The "TO LET" units: a letting agent's board over the shutter, fly-posters pasted across it (layered, one
    /// half torn off), a scrawled tag, a pile of junk mail pushed back under the shutter, and a dead pigeon feather or two.
    /// </summary>
    public static partial class MallBuilder
    {
        static readonly string[] FlyPosters =
        {
            "LIVE MUSIC\nTHE HOLLOW PINES\nFRI 21 FEB", "CAR BOOT SALE\nEVERY SUNDAY\n7AM", "LOST CAT\nANSWERS TO 'BISCUIT'", "RAVE\nWAREHOUSE 9\nSAT 15 FEB",
            "GUITAR LESSONS\nALL AGES\nCALL DEAN", "CIRCUS IS COMING\nMARCH 2", "COMEDY NIGHT\nTHE RED LION", "MISSING\nGARY DOYLE\nNIGHT PORTER",
        };

        static void VacantFront(Transform g, MallUnit u, float y, int side)
        {
            WingId w = u.wing;
            Vector3 toConcourse = -MallLayout.WingRight(w) * side;
            Quaternion face = Quaternion.LookRotation(toConcourse);
            float faceV = side * (MallLayout.ConcourseHalf + 0.014f);
            Vector3 P(float uu, float h, float dv = 0f) => MallLayout.WingPoint(w, uu, faceV - side * dv, y + h);
            var rng = new System.Random(u.code.GetHashCode());
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            float mid = (u.u0 + u.u1) * 0.5f, len = u.u1 - u.u0 - 0.6f;

            // letting board over the top of the shutter
            Material board = ArtLibrary.Plain("Letting_Board", new Color(0.9f, 0.88f, 0.82f), 0.3f);
            Material agentRed = ArtLibrary.Plain("Agent_Red", new Color(0.65f, 0.08f, 0.08f), 0.4f);
            Detail("Letting board", g, P(mid, ShopOpeningHeight - 0.45f, 0.02f), new Vector3(Mathf.Min(2.4f, len - 0.4f), 0.7f, 0.03f), board, face);
            Detail("Letting board stripe", g, P(mid, ShopOpeningHeight - 0.18f, 0.04f), new Vector3(Mathf.Min(2.4f, len - 0.4f), 0.14f, 0.005f), agentRed, face);
            Sign(g, $"TO LET\n{1200 + Mathf.Abs(u.code.GetHashCode()) % 2400} SQ FT  -  RETAIL\nHARGREAVE & COLE  01632 960 417", P(mid, ShopOpeningHeight - 0.5f, 0.045f), toConcourse, Mathf.Min(2.2f, len - 0.5f), 0.022f, false);

            // fly-posters, overlapping, at head height
            Material[] paper = { ArtLibrary.Plain("Poster_Yellow", new Color(0.9f, 0.82f, 0.3f), 0.2f), ArtLibrary.Plain("Poster_Pink", new Color(0.9f, 0.5f, 0.6f), 0.2f), ArtLibrary.Plain("Poster_White", new Color(0.88f, 0.88f, 0.85f), 0.2f), ArtLibrary.Plain("Poster_Green", new Color(0.4f, 0.75f, 0.4f), 0.2f) };
            int count = 3 + rng.Next(4);
            for (int k = 0; k < count; k++)
            {
                float uu = Mathf.Lerp(u.u0 + 0.8f, u.u1 - 0.8f, (k + R(0.1f, 0.9f)) / count);
                float h = R(1.2f, 1.9f), tilt = R(-6f, 6f);
                float dv = 0.004f + k * 0.002f;
                bool torn = k == 1;
                Vector3 c = P(uu, h, dv);
                Detail(torn ? "Fly-poster (torn)" : "Fly-poster", g, torn ? c - Vector3.up * 0.15f : c, new Vector3(0.42f, torn ? 0.3f : 0.6f, 0.002f), paper[rng.Next(paper.Length)], face * Quaternion.Euler(0f, 0f, tilt));
                if (!torn) Sign(g, FlyPosters[rng.Next(FlyPosters.Length)], c + toConcourse * 0.003f, toConcourse, 0.38f, 0.011f, false);
            }
            // a tag sprayed across the lower shutter
            TextMesh tag = EditorBuild.Label(g, rng.Next(2) == 0 ? "ZEK" : "NITE", P(mid + R(-1f, 1f), 0.75f, 0.003f), Mathf.Atan2(-toConcourse.x, -toConcourse.z) * Mathf.Rad2Deg, 0.09f);
            tag.color = rng.Next(2) == 0 ? new Color(0.15f, 0.35f, 0.8f) : new Color(0.75f, 0.1f, 0.4f);
            tag.transform.rotation = face * Quaternion.Euler(0f, 180f, R(-12f, 8f));
            tag.transform.position += toConcourse * 0.002f;

            // junk mail pushed out from under the shutter
            for (int k = 0; k < 6; k++)
                Detail("Junk mail", g, MallLayout.WingPoint(w, mid + R(-len * 0.4f, len * 0.4f), faceV - side * R(0.05f, 0.4f), y + 0.004f), new Vector3(0.16f, 0.003f, 0.22f), paper[rng.Next(paper.Length)], Quaternion.Euler(0f, R(0f, 180f), 0f));
        }
    }
}
