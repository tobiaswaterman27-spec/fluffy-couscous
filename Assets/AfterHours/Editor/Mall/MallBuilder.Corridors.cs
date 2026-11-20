using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Service corridor odds and ends against the outer wall, between the vent grilles: a fire hose reel cabinet in
    /// every corridor, and either the cleaner's trolley left mid-round (mop in a bucket of grey water, bin bag, sprays,
    /// a CLEANING IN PROGRESS sign) or a heap of dumped shop fittings waiting for a skip.
    /// </summary>
    public static partial class MallBuilder
    {
        static void DressServiceCorridors(Transform parent)
        {
            foreach (MallFloor f in MallLayout.ShopFloors)
            {
                float y = MallLayout.FloorY(f);
                foreach (WingId w in MallLayout.Wings)
                for (int side = -1; side <= 1; side += 2)
                {
                    Transform g = Group($"{MallLayout.FloorCode(f)} {w} corridor {(side < 0 ? "L" : "R")} odds", parent);
                    Vector3 inward = -MallLayout.WingRight(w) * side;
                    Quaternion face = Quaternion.LookRotation(inward);
                    float wallV = side * (MallLayout.CorridorOuter);
                    Vector3 P(float u, float h, float off) => MallLayout.WingPoint(w, u, wallV - side * off, y + h);
                    int h0 = Mathf.Abs(MallLayout.Hash((int)f, (int)w, side, 311));

                    // fire hose reel
                    Detail("Hose reel cabinet", g, P(79f, 1.25f, 0.12f), new Vector3(0.75f, 0.75f, 0.22f), redMat, face);
                    Detail("Hose reel glass", g, P(79f, 1.25f, 0.235f), new Vector3(0.6f, 0.6f, 0.01f), glassMat, face);
                    Cyl("Hose drum", g, P(79f, 1.25f, 0.06f), P(79f, 1.25f, 0.2f), 0.55f, redMat);
                    Cyl("Hose coil", g, P(79f, 1.25f, 0.08f), P(79f, 1.25f, 0.19f), 0.48f, ArtLibrary.Plain("Hose_Red", new Color(0.55f, 0.06f, 0.04f), 0.35f));
                    Sign(g, "FIRE HOSE REEL", P(79f, 1.75f, 0.24f), inward, 0.7f, 0.016f, false);

                    if (h0 % 3 == 0) CleanerTrolley(g, w, P(61f, 0f, 0.4f), face, inward, h0);
                    else if (h0 % 3 == 1) DumpedFittings(g, P(61f, 0f, 0.45f), face, inward, h0);
                }
            }
        }

        static void CleanerTrolley(Transform g, WingId w, Vector3 at, Quaternion face, Vector3 inward, int seed)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Vector3 along = Vector3.Cross(Vector3.up, inward);
            Material yellow = ArtLibrary.Plain("Trolley_Yellow", new Color(0.85f, 0.68f, 0.08f), 0.45f);
            Material grey = ArtLibrary.Plain("Trolley_Grey", new Color(0.3f, 0.32f, 0.34f), 0.4f);
            Material binBag = ArtLibrary.Plain("Bin_Bag", new Color(0.03f, 0.03f, 0.035f), 0.75f);
            Material dirtyWater = ArtLibrary.Plain("Dirty_Water", new Color(0.18f, 0.17f, 0.12f), 0.9f);
            Material mopHead = ArtLibrary.Plain("Mop_Head", new Color(0.75f, 0.72f, 0.62f), 0.1f);
            Material spray = ArtLibrary.Plain("Spray_Blue", new Color(0.15f, 0.35f, 0.75f), 0.6f);

            var col = new GameObject("Cleaning trolley (collider)");
            col.transform.SetParent(g, false);
            col.transform.localPosition = at + Vector3.up * 0.55f;
            col.transform.localRotation = face;
            col.AddComponent<BoxCollider>().size = new Vector3(1.5f, 1.1f, 0.6f);
            col.isStatic = true;

            // frame: a grey chassis, shelves, the bag hoop at one end
            Detail("Trolley base", g, at + Vector3.up * 0.12f, new Vector3(1.0f, 0.05f, 0.5f), grey, face);
            Detail("Trolley shelf", g, at + Vector3.up * 0.55f + along * 0.15f, new Vector3(0.55f, 0.04f, 0.45f), grey, face);
            Detail("Trolley top", g, at + Vector3.up * 0.95f + along * 0.15f, new Vector3(0.55f, 0.04f, 0.45f), grey, face);
            foreach (float s in new[] { -0.12f, 0.42f })
                Cyl("Trolley upright", g, at + along * s + inward * 0.2f + Vector3.up * 0.12f, at + along * s + inward * 0.2f + Vector3.up * 0.97f, 0.03f, grey);
            Cyl("Bag hoop post", g, at - along * 0.35f + Vector3.up * 0.12f, at - along * 0.35f + Vector3.up * 0.95f, 0.03f, yellow);
            Detail("Bin bag (full)", g, at - along * 0.35f + Vector3.up * 0.5f, new Vector3(0.42f, 0.7f, 0.42f), binBag, face * Quaternion.Euler(0f, 20f, 3f));
            foreach (float a in new[] { -0.45f, 0.45f })
            foreach (float b in new[] { -0.2f, 0.2f })
                Cyl("Castor", g, at + along * a + inward * b + Vector3.up * 0.04f - along * 0.02f, at + along * a + inward * b + Vector3.up * 0.04f + along * 0.02f, 0.08f, blackMat);
            // sprays and cloths on the top
            for (int k = 0; k < 4; k++)
            {
                Vector3 p = at + Vector3.up * 0.97f + along * (0.0f + k * 0.11f) + inward * R(-0.1f, 0.1f);
                Cyl("Spray bottle", g, p, p + Vector3.up * 0.22f, 0.07f, k == 2 ? yellow : spray);
                Detail("Trigger", g, p + Vector3.up * 0.25f, new Vector3(0.03f, 0.05f, 0.06f), whiteFixture, face);
            }
            Detail("Cloth", g, at + Vector3.up * 0.98f + along * 0.35f + inward * 0.18f, new Vector3(0.2f, 0.015f, 0.18f), ArtLibrary.Plain("Cloth_Pink", new Color(0.85f, 0.45f, 0.55f), 0.1f), face * Quaternion.Euler(0f, 30f, 0f));
            Detail("Toilet rolls", g, at + Vector3.up * 0.68f + along * 0.15f, new Vector3(0.4f, 0.22f, 0.3f), whiteFixture, face);

            // mop bucket beside it, mop standing in it
            Vector3 mb = at + along * 0.95f + inward * 0.05f;
            Detail("Mop bucket", g, mb + Vector3.up * 0.2f, new Vector3(0.45f, 0.38f, 0.32f), yellow, face);
            Detail("Bucket water", g, mb + Vector3.up * 0.33f, new Vector3(0.4f, 0.01f, 0.28f), dirtyWater, face);
            Detail("Wringer", g, mb + Vector3.up * 0.48f + along * 0.08f, new Vector3(0.22f, 0.18f, 0.3f), grey, face);
            Cyl("Mop handle", g, mb + Vector3.up * 0.3f - along * 0.05f, mb + Vector3.up * 1.45f - along * 0.2f + inward * 0.1f, 0.03f, ArtLibrary.Plain("Mop_Handle_Blue", new Color(0.15f, 0.3f, 0.7f), 0.4f));
            Detail("Mop head", g, mb + Vector3.up * 0.28f - along * 0.04f, new Vector3(0.2f, 0.12f, 0.2f), mopHead, face);
            // a wet trail leading off down the corridor, and the sign
            for (int k = 0; k < 6; k++)
                Detail("Mop streak", g, at + along * (1.5f + k * 0.55f) + inward * (0.6f + Mathf.Sin(k) * 0.3f) + Vector3.up * 0.003f, new Vector3(0.6f, 0.002f, 0.35f), ArtLibrary.Plain("Wet_Sheen", new Color(0.05f, 0.06f, 0.07f), 0.97f), Quaternion.LookRotation(along) * Quaternion.Euler(0f, R(-15f, 15f), 0f));
            WetFloorSign(g, at + along * 2.4f + inward * 1.2f, R(0f, 360f));
        }

        static void DumpedFittings(Transform g, Vector3 at, Quaternion face, Vector3 inward, int seed)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Vector3 along = Vector3.Cross(Vector3.up, inward);
            Material mdf = ArtLibrary.Plain("MDF_Panel", new Color(0.62f, 0.52f, 0.4f), 0.2f);
            var col = new GameObject("Dumped fittings (collider)");
            col.transform.SetParent(g, false);
            col.transform.localPosition = at + Vector3.up * 0.6f;
            col.transform.localRotation = face;
            col.AddComponent<BoxCollider>().size = new Vector3(2.2f, 1.2f, 0.7f);
            col.isStatic = true;
            // shelf panels leaning against the wall
            for (int k = 0; k < 4; k++)
                Detail("Shelf panel (leaning)", g, at + along * (-0.6f + k * 0.08f) - inward * 0.15f + Vector3.up * 0.9f, new Vector3(1.0f, 1.8f, 0.03f), k % 2 == 0 ? mdf : whiteFixture, face * Quaternion.Euler(-12f, 0f, 0f));
            // a gondola end, a broken display box, a headless mannequin torso
            Detail("Display cube (split)", g, at + along * 0.5f + Vector3.up * 0.25f, new Vector3(0.5f, 0.5f, 0.5f), whiteFixture, face * Quaternion.Euler(0f, 20f, 0f));
            Detail("Display cube", g, at + along * 0.55f + Vector3.up * 0.72f, new Vector3(0.4f, 0.4f, 0.4f), mdf, face * Quaternion.Euler(0f, -10f, 6f));
            GameObject torso = Cyl("Mannequin torso (dumped)", g, at + along * 0.95f + Vector3.up * 0.2f, at + along * 0.95f + Vector3.up * 0.8f + inward * 0.2f, 0.32f, whiteFixture);
            torso.transform.localScale = new Vector3(0.34f, torso.transform.localScale.y, 0.22f);
            Detail("Coat hangers (bundle)", g, at + along * 0.2f + inward * 0.2f + Vector3.up * 0.03f, new Vector3(0.45f, 0.05f, 0.25f), blackMat, face * Quaternion.Euler(0f, 35f, 0f));
            Detail("Price gun", g, at + along * 0.25f + inward * 0.35f + Vector3.up * 0.04f, new Vector3(0.06f, 0.08f, 0.16f), redMat, face * Quaternion.Euler(0f, 70f, 90f));
            Detail("Torn shop sign", g, at + along * -0.3f + inward * 0.25f + Vector3.up * 0.02f, new Vector3(1.2f, 0.02f, 0.3f), ArtLibrary.Plain("Old_Sign", new Color(0.2f, 0.25f, 0.5f), 0.4f), face * Quaternion.Euler(0f, R(-10f, 10f), 0f));
            Sign(g, "FOR SKIP - DO NOT USE", at + along * -0.6f + inward * 0.02f + Vector3.up * 1.3f, inward, 0.8f, 0.016f);
        }
    }
}
