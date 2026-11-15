using System.Collections.Generic;
using UnityEngine;
using AfterHours.Core;
using AfterHours.Interaction;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Part 11: the ventilation ducts. On every shop floor, a sheet-metal duct runs along the outside of each
    /// service corridor (from the back-of-house block to the anchor store), with low grilles into the corridor and
    /// one into the anchor. You crawl through them; the metal is loud, dark and narrow.
    ///   Ground floor: 1.05 m wide x 1.0 m high - Elliot can squeeze in on all fours (it rarely does).
    ///   Upper floors: 0.8 m x 0.72 m - too small for it; it can only reach in from the grille.
    /// Some grilles are screwed shut, some loose, a few already hanging off.
    /// </summary>
    public static partial class MallBuilder
    {
        static readonly float[] VentU = { 52f, 70f, 88f, 109f };

        static float VentInnerHeight(MallFloor f) => f == MallFloor.Ground ? 1.0f : 0.72f;
        static float VentInnerWidth(MallFloor f) => f == MallFloor.Ground ? 1.05f : 0.8f;

        static float VentOpeningU(MallFloor f, WingId w, float u)
        {
            float end = MallLayout.AnchorEndFor(f, w);
            return Mathf.Min(u, end - 6f);
        }

        /// <summary>The grille openings to cut into a corridor outer wall (u ranges at floor level).</summary>
        static List<(float, float, float)> VentOpenings(MallFloor f, WingId w, int side)
        {
            var list = new List<(float, float, float)>();
            float h = VentInnerHeight(f);
            foreach (float u0 in VentU)
            {
                float u = VentOpeningU(f, w, u0);
                list.Add((u - 0.45f, u + 0.45f, h));
            }
            return list;
        }

        static void BuildVents(Transform parent)
        {
            Material shell = metal;
            Material grille = plantMat;
            foreach (MallFloor f in MallLayout.ShopFloors)
            {
                float y = MallLayout.FloorY(f);
                float H = VentInnerHeight(f), W = VentInnerWidth(f);
                const float t = 0.04f;
                foreach (WingId w in MallLayout.Wings)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Transform g = Group($"{MallLayout.FloorCode(f)} {w} duct {(side < 0 ? "L" : "R")}", parent);
                        float u0 = MallLayout.QuadrantOuter, u1 = MallLayout.AnchorEndFor(f, w);
                        float vIn = MallLayout.OuterWall, vOut = MallLayout.OuterWall + W;
                        float s = side;
                        float vA = Mathf.Min(s * vIn, s * (vOut + t)), vB = Mathf.Max(s * vIn, s * (vOut + t));

                        // Shell: floor, roof, outer side, end caps. (The corridor wall is the inner side.)
                        WingBox("Duct floor", g, w, u0, u1, vA, vB, y - t, y, shell, SurfaceType.Metal);
                        WingBox("Duct roof", g, w, u0, u1, vA, vB, y + H, y + H + t, shell, SurfaceType.Metal);
                        float o0 = Mathf.Min(s * vOut, s * (vOut + t)), o1 = Mathf.Max(s * vOut, s * (vOut + t));
                        WingBox("Duct side", g, w, u0, u1, o0, o1, y, y + H, shell, SurfaceType.Metal);
                        WingBox("Duct end", g, w, u0 - t, u0, vA, vB, y - t, y + H + t, shell, SurfaceType.Metal);
                        WingBox("Duct end", g, w, u1, u1 + t, vA, vB, y - t, y + H + t, shell, SurfaceType.Metal);

                        // Inside: seams / stiffening ribs, dust, the odd thing someone left behind.
                        int hseed = MallLayout.Hash((int)f, (int)w, side, 404);
                        float vMid = s * (vIn + W * 0.5f);
                        for (float u = u0 + 1.2f; u < u1 - 0.5f; u += 1.8f)
                        {
                            Vector3 lo = MallLayout.WingPoint(w, u - 0.025f, vA, y + H - 0.025f);
                            Vector3 hi = MallLayout.WingPoint(w, u + 0.025f, vB, y + H);
                            Decor("Duct seam", g, Vector3.Min(lo, hi), Vector3.Max(lo, hi), shell);
                            Vector3 so = MallLayout.WingPoint(w, u - 0.025f, s * (vOut - 0.02f), y);
                            Vector3 si = MallLayout.WingPoint(w, u + 0.025f, s * vOut, y + H);
                            Decor("Duct seam", g, Vector3.Min(so, si), Vector3.Max(so, si), shell);
                        }
                        for (float u = u0 + 3f; u < u1 - 2f; u += 4.5f)
                        {
                            int hh = MallLayout.Hash(hseed, (int)u, 1, 2);
                            if (hh % 3 == 0) continue;
                            Vector3 c = MallLayout.WingPoint(w, u + (hh % 7) * 0.2f, vMid, y + 0.003f);
                            DecalQuad(g, c, Vector3.up, W * 0.8f, Rand(0.8f, 2f), Rand(0f, 360f), hh % 2 == 0 ? decalGrime : decalDamp);
                        }
                        // A few dropped things (physics props would rattle; these are just flat debris).
                        for (int k = 0; k < 3; k++)
                        {
                            float u = Mathf.Lerp(u0 + 5f, u1 - 5f, ((hseed >> (k * 3)) & 7) / 7f);
                            Vector3 c = MallLayout.WingPoint(w, u, vMid + s * (k - 1) * 0.15f, y);
                            float yaw = MallLayout.WingFacingYaw(w) + k * 37f;
                            Detail(k == 0 ? "Old screw" : k == 1 ? "Torn filter" : "Cable tie", g, c + Vector3.up * 0.01f,
                                k == 1 ? new Vector3(0.35f, 0.012f, 0.28f) : new Vector3(0.04f, 0.012f, 0.012f), k == 1 ? darkPanel : metal, Quaternion.Euler(0f, yaw, 0f));
                        }

                        // Very dim light leaking in (and two weak lamps so it isn't pitch black).
                        foreach (float lu in new[] { 61f, 98f })
                        {
                            if (lu > u1 - 2f) continue;
                            PointLight(g, MallLayout.WingPoint(w, lu, vMid, y + H - 0.1f), 3.2f, 0.12f, WarmWhite);
                        }

                        // The duct volume (so the game knows who is "in the vents").
                        var vol = new GameObject("Duct volume");
                        vol.transform.SetParent(g, false);
                        vol.transform.position = MallLayout.WingPoint(w, (u0 + u1) * 0.5f, vMid, y + H * 0.5f);
                        vol.transform.rotation = Quaternion.LookRotation(MallLayout.WingForward(w));
                        vol.layer = 2; // Ignore Raycast
                        BoxCollider box = vol.AddComponent<BoxCollider>();
                        box.isTrigger = true;
                        box.size = new Vector3(W, H, u1 - u0);
                        VentDuct duct = vol.AddComponent<VentDuct>();
                        duct.creatureSized = f == MallFloor.Ground;

                        // Grilles on the corridor side of each opening.
                        Vector3 outward = -MallLayout.WingRight(w) * side;
                        foreach (float uv in VentU)
                        {
                            float u = VentOpeningU(f, w, uv);
                            int hh = MallLayout.Hash(hseed, (int)(u * 10f), 7, 3);
                            VentCover.State st = hh % 20 < 5 ? VentCover.State.Screwed : hh % 20 < 17 ? VentCover.State.Loose : VentCover.State.Removed;
                            Vector3 centre = MallLayout.WingPoint(w, u, side * (MallLayout.CorridorOuter + 0.0f), y + H * 0.5f);
                            InteractiveFactory.Vent(g, centre, outward, new Vector2(0.9f, H), st, grille);
                            // Dust streaks under the grille where air used to blow out.
                            DecalQuad(g, MallLayout.WingPoint(w, u, side * (MallLayout.CorridorOuter - 0.35f), y + 0.004f), Vector3.up, 1.1f, 0.6f, 0f, decalGrime);
                        }
                    }
                }
            }
        }
    }
}
