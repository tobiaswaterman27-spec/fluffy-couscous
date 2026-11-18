using UnityEditor;
using UnityEngine;
using AfterHours.Core;
using AfterHours.Hiding;
using AfterHours.Interaction;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Hiding places without doors: under shop counters (crawl in from the staff side), under office desks, and in
    /// clothes shops' changing rooms behind a half-drawn curtain. Elliot checks them like any other hiding place
    /// while he searches - and if he comes right up close and looks, he can see you.
    /// </summary>
    public static partial class MallBuilder
    {
        static HidingSpot OpenHidingSpot(Transform parent, Vector3 triggerCentre, Vector3 triggerSize, Quaternion rot,
                                         Vector3 inside, Vector3 outside, Vector3 facing, Stance stance, float quality, float exposedWithin)
        {
            var go = new GameObject("Hiding place (open)");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(triggerCentre, rot);
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = triggerSize;
            go.layer = GameLayers.InteractableLayer >= 0 ? GameLayers.InteractableLayer : 0;
            HidingSpot spot = go.AddComponent<HidingSpot>();
            spot.doors = new Door[0];
            spot.open = true;
            spot.exposedWithin = exposedWithin;
            spot.hideStance = stance;
            spot.insidePoint = inside;
            spot.outsidePoint = outside;
            spot.facingYaw = Quaternion.LookRotation(facing).eulerAngles.y;
            spot.quality = quality;
            spot.lookRange = 35f;
            return spot;
        }

        /// <summary>
        /// A shop counter, hollow on the staff side: top, front panel and ends. Customers see a solid counter;
        /// from behind you can crawl underneath it and hide. (c0 / c1 are opposite corners; 'back' points to the staff side.)
        /// </summary>
        static void HollowCounter(Transform parent, WingId w, float uA, float uB, float vFront, float vBack, float y, float top, int side)
        {
            float vf = side * vFront, vb = side * vBack;
            BoxMinMax("Counter top", parent, MallLayout.WingPoint(w, uA, vf, y + top - 0.04f), MallLayout.WingPoint(w, uB, vb, y + top), counterMat, SurfaceType.Wood);
            BoxMinMax("Counter front", parent, MallLayout.WingPoint(w, uA, vf, y), MallLayout.WingPoint(w, uB, side * (vFront + 0.05f), y + top - 0.04f), counterMat, SurfaceType.Wood);
            BoxMinMax("Counter end", parent, MallLayout.WingPoint(w, uA, vf, y), MallLayout.WingPoint(w, uA + 0.05f, vb, y + top - 0.04f), counterMat, SurfaceType.Wood);
            BoxMinMax("Counter end", parent, MallLayout.WingPoint(w, uB - 0.05f, vf, y), MallLayout.WingPoint(w, uB, vb, y + top - 0.04f), counterMat, SurfaceType.Wood);
            Detail("Counter shelf", parent, MallLayout.WingPoint(w, (uA + uB) * 0.5f, side * (vFront + vBack) * 0.5f, y + 0.55f), new Vector3(0.1f, 0.02f, 0.1f), counterMat);
            float um = (uA + uB) * 0.5f;
            Vector3 back = MallLayout.WingRight(w) * side;
            OpenHidingSpot(parent, MallLayout.WingPoint(w, um, side * (vFront + vBack) * 0.5f, y + 0.4f),
                new Vector3(Mathf.Abs(uB - uA) - 0.2f, 0.6f, Mathf.Abs(vBack - vFront) - 0.15f), Quaternion.LookRotation(MallLayout.WingRight(w)),
                MallLayout.WingPoint(w, um, side * (vFront + vBack) * 0.5f + side * 0.05f, y), MallLayout.WingPoint(w, um, side * (vBack + 0.7f), y),
                back, Stance.Crawling, 0.55f, 1.8f);
        }

        /// <summary>An office desk with knee space (top, side panels, a modesty panel at the back): you can crawl under it.</summary>
        static void OpenDesk(Transform parent, AreaRect r, float y)
        {
            float x0 = r.minX, x1 = r.maxX, z0 = r.minZ, z1 = r.maxZ, top = 0.75f;
            BoxMinMax("Desk top", parent, new Vector3(x0, y + top - 0.03f, z0), new Vector3(x1, y + top, z1), woodMat, SurfaceType.Wood);
            BoxMinMax("Desk side", parent, new Vector3(x0, y, z0), new Vector3(x0 + 0.03f, y + top - 0.03f, z1), woodMat, SurfaceType.Wood);
            BoxMinMax("Desk side", parent, new Vector3(x1 - 0.03f, y, z0), new Vector3(x1, y + top - 0.03f, z1), woodMat, SurfaceType.Wood);
            BoxMinMax("Desk modesty panel", parent, new Vector3(x0, y + 0.25f, z1 - 0.03f), new Vector3(x1, y + top - 0.03f, z1), woodMat, SurfaceType.Wood);
            Vector3 c = new Vector3((x0 + x1) * 0.5f - 0.25f, y, (z0 + z1) * 0.5f);
            OpenHidingSpot(parent, c + Vector3.up * 0.35f, new Vector3(0.7f, 0.5f, (z1 - z0) - 0.15f), Quaternion.identity,
                c + Vector3.forward * 0.05f, c + Vector3.back * 1.0f, Vector3.back, Stance.Crawling, 0.45f, 1.8f);
        }

        /// <summary>
        /// Two changing rooms against a shop's side wall (u1 end): partitions, a bench, a mirror, a hook, and a curtain
        /// pulled most of the way across. Stand inside behind it.
        /// </summary>
        static void ChangingRooms(Transform parent, MallUnit u, float y, int side)
        {
            WingId w = u.wing;
            Transform g = Group("Changing rooms", parent);
            float uWall = u.u1 - 0.1f, uOpen = u.u1 - 1.4f, h = 2.2f;
            Material curtain = ArtLibrary.Plain("Curtain_" + ((int)u.category % 3), new[] { new Color(0.25f, 0.22f, 0.3f), new Color(0.35f, 0.12f, 0.12f), new Color(0.2f, 0.25f, 0.22f) }[(int)u.category % 3], 0.1f, 0f);
            float[] vs = { 8.9f, 10.15f, 11.4f };
            Vector3 intoRoom = MallLayout.WingForward(w);
            for (int i = 0; i <= 2; i++)
                BoxMinMax("Changing room partition", g, MallLayout.WingPoint(w, uOpen, side * (vs[i] - 0.025f), y), MallLayout.WingPoint(w, uWall, side * (vs[i] + 0.025f), y + h), whiteFixture, SurfaceType.Wood);
            for (int i = 0; i < 2; i++)
            {
                float vm = (vs[i] + vs[i + 1]) * 0.5f;
                Detail("Changing room bench", g, MallLayout.WingPoint(w, uWall - 0.2f, side * vm, y + 0.45f), new Vector3(0.35f, 0.04f, 1.0f), woodMat, Quaternion.LookRotation(intoRoom));
                Detail("Changing room mirror", g, MallLayout.WingPoint(w, uWall - 0.01f, side * vm, y + 1.3f), new Vector3(0.5f, 1.3f, 0.01f), mirrorMat, Quaternion.LookRotation(-intoRoom));
                Cyl("Curtain rail", g, MallLayout.WingPoint(w, uOpen, side * vs[i], y + h - 0.05f), MallLayout.WingPoint(w, uOpen, side * vs[i + 1], y + h - 0.05f), 0.025f, metal);
                // the curtain, pulled across about two thirds (no collider: you walk through the gap)
                float drawn = 0.62f + 0.12f * ((MallLayout.Hash((int)u.u0, i, 3, 9) & 7) / 7f);
                float vA = vs[i] + 0.03f, vB = vs[i] + 0.03f + (vs[i + 1] - vs[i] - 0.06f) * drawn;
                Decor("Curtain", g, MallLayout.WingPoint(w, uOpen - 0.02f, side * vA, y + 0.12f), MallLayout.WingPoint(w, uOpen + 0.02f, side * vB, y + h - 0.08f), curtain);
                Vector3 inside = MallLayout.WingPoint(w, (uOpen + uWall) * 0.5f + 0.15f, side * (vm - 0.1f), y);
                OpenHidingSpot(g, MallLayout.WingPoint(w, (uOpen + uWall) * 0.5f, side * vm, y + 1.1f), new Vector3(1.0f, 1.6f, 0.9f),
                    Quaternion.LookRotation(intoRoom), inside, MallLayout.WingPoint(w, uOpen - 0.8f, side * vm, y), -intoRoom, Stance.Standing, 0.6f, 1.4f);
            }
        }
    }
}
