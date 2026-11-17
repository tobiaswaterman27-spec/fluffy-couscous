using System.Collections.Generic;
using UnityEngine;
using AfterHours.Audio;
using AfterHours.Core;
using AfterHours.Rendering;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    public static partial class MallBuilder
    {
        // =====================================================================
        // Car park: bays, arrows, pillar hazard bands, ceiling services, fans, light battens, stains, signs.
        // =====================================================================

        static List<AreaRect> CarParkKeepOut()
        {
            var r = new List<AreaRect>();
            foreach (var (sx, sz) in MallLayout.Quadrants) r.Add(MallLayout.CoreRect(sx, sz).Expanded(1f));
            foreach (AreaRect lift in MallLayout.LiftShafts()) r.Add(lift.Expanded(1f));
            r.Add(new AreaRect(-17f, 9f, -9f, 16.2f).Expanded(1f));       // lift lobby
            r.Add(new AreaRect(-100f, -52f, -84f, -23f));                  // plant rooms
            r.Add(new AreaRect(55f, -38f, 100f, -26f));                    // exit ramp
            r.Add(new AreaRect(-72f, 24f, -60f, 41f));                     // split-level ramps
            r.Add(new AreaRect(-41f, 58f, -26f, 70f));
            r.Add(new AreaRect(68f, 68f, 100f, 100f));                     // delivery bay
            return r;
        }

        static void DressCarPark(Transform parent)
        {
            Transform g = Group("Car park details", parent);
            AreaRect raised = MallLayout.CarParkRaised;
            List<AreaRect> keepOut = CarParkKeepOut();
            float FloorY(float x, float z) => raised.Contains(x, z) ? -2.5f : MallLayout.CarParkY;
            bool Blocked(AreaRect r) { foreach (AreaRect k in keepOut) if (k.Overlaps(r)) return true; return Mathf.Abs(r.minZ - raised.minZ) < 0.5f || Mathf.Abs(r.maxX - raised.maxX) < 0.5f; }

            // Bay rows straddle every second line of pillars; 6.4 m driving aisles between them.
            for (float zr = -96f; zr <= 96f; zr += 16f)
            {
                for (float x0 = -96f; x0 < 96f; x0 += 8f)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        AreaRect bayBlock = new AreaRect(x0, zr, x0 + 8f, zr + side * 5f);
                        if (Blocked(bayBlock) || (raised.Contains(x0 + 4f, zr + side * 2.5f) != raised.Contains(x0 + 4f, zr))) continue;
                        float fy = FloorY(x0 + 4f, zr + side * 2.5f) + 0.006f;
                        for (int b = 0; b <= 3; b++)
                        {
                            float lx = x0 + 0.4f + b * 2.4f;
                            Detail("Bay line", g, new Vector3(lx, fy, zr + side * 2.6f), new Vector3(0.1f, 0.004f, 4.6f), lineMat);
                        }
                        Detail("Bay end line", g, new Vector3(x0 + 4f, fy, zr + side * 5f), new Vector3(7.3f, 0.004f, 0.1f), lineMat);
                        for (int b = 0; b < 3; b++)
                        {
                            float cx = x0 + 1.6f + b * 2.4f;
                            int h = MallLayout.Hash((int)x0, (int)zr, side, b);
                            if (h % 4 == 0) DecalQuad(g, new Vector3(cx + Rand(-0.4f, 0.4f), fy, zr + side * 2.8f), Vector3.up, Rand(0.8f, 1.6f), Rand(0.8f, 1.6f), Rand(0f, 360f), decalOil);
                            if (h % 23 == 0)
                            {
                                Detail("Wheel stop", g, new Vector3(cx, fy + 0.06f, zr + side * 4.3f), new Vector3(1.6f, 0.12f, 0.15f), concrete);
                            }
                        }
                    }
                }
                // Direction arrows along the aisle.
                float az = zr + 8f;
                if (az > 96f) continue;
                for (float ax = -84f; ax <= 84f; ax += 24f)
                {
                    AreaRect ar = new AreaRect(ax - 1.5f, az - 1f, ax + 1.5f, az + 1f);
                    if (Blocked(ar)) continue;
                    float dirSign = Mathf.Repeat(zr, 32f) < 1f ? 1f : -1f;
                    float fy = FloorY(ax, az) + 0.006f;
                    Detail("Arrow shaft", g, new Vector3(ax, fy, az), new Vector3(1.8f, 0.004f, 0.22f), whiteFixture);
                    Detail("Arrow head", g, new Vector3(ax + dirSign * 0.75f, fy, az + 0.22f), new Vector3(0.8f, 0.004f, 0.2f), whiteFixture, Quaternion.Euler(0f, dirSign * 40f, 0f));
                    Detail("Arrow head", g, new Vector3(ax + dirSign * 0.75f, fy, az - 0.22f), new Vector3(0.8f, 0.004f, 0.2f), whiteFixture, Quaternion.Euler(0f, -dirSign * 40f, 0f));
                    if (MallLayout.Hash((int)ax, (int)az, 1, 9) % 3 == 0)
                    {
                        DecalQuad(g, new Vector3(ax + Rand(-6f, 6f), fy, az + Rand(-2f, 2f)), Vector3.up, 2.4f, 5f, 90f + Rand(-20f, 20f), decalTyre);
                    }
                }
            }

            // Hazard bands on every pillar.
            Transform pillars = null;
            foreach (Transform t in parent.GetComponentsInChildren<Transform>())
            {
                if (t.name == "Pillars") { pillars = t; break; }
            }
            if (pillars != null)
            {
                foreach (Transform p in pillars)
                {
                    if (p.name != "Pillar") continue;
                    Bounds b = p.GetComponent<Renderer>().bounds;
                    Detail("Pillar band", g, new Vector3(b.center.x, b.min.y + 0.45f, b.center.z), new Vector3(b.size.x + 0.012f, 0.9f, b.size.z + 0.012f), yellowPaint);
                    Detail("Pillar band", g, new Vector3(b.center.x, b.min.y + 0.95f, b.center.z), new Vector3(b.size.x + 0.014f, 0.1f, b.size.z + 0.014f), blackMat);
                }
            }

            // Ceiling services: drainage pipes, sprinkler mains, jet fans.
            for (float pz = -92f; pz <= 92f; pz += 16f)
            {
                for (float sx = -100f; sx < 100f; sx += 20f)
                {
                    AreaRect seg = new AreaRect(sx, pz - 0.2f, sx + 20f, pz + 0.2f);
                    if (Blocked(seg)) continue;
                    Cyl("Drain pipe", g, new Vector3(sx, -0.75f, pz), new Vector3(sx + 20f, -0.75f, pz), 0.15f, concrete);
                    Cyl("Sprinkler main", g, new Vector3(sx, -0.62f, pz + 1.2f), new Vector3(sx + 20f, -0.62f, pz + 1.2f), 0.07f, redMat);
                }
            }
            for (float fx = -80f; fx <= 80f; fx += 32f)
            {
                for (float fz = -88f; fz <= 88f; fz += 32f)
                {
                    if (Blocked(new AreaRect(fx - 1f, fz - 1f, fx + 1f, fz + 1f))) continue;
                    Detail("Jet fan housing", g, new Vector3(fx, -0.95f, fz), new Vector3(1.2f, 0.55f, 0.55f), plantMat);
                    Cyl("Jet fan", g, new Vector3(fx - 0.7f, -0.95f, fz), new Vector3(fx + 0.7f, -0.95f, fz), 0.5f, metal);
                    Detail("Jet fan hanger", g, new Vector3(fx, -0.6f, fz), new Vector3(0.6f, 0.2f, 0.08f), metal);
                }
            }

            // Light battens over the car park lights; flickering fittings replace some real-time lights.
            for (float x = -90f; x <= 90.1f; x += 22.5f)
            {
                for (float z = -90f; z <= 90.1f; z += 22.5f)
                {
                    int h = MallLayout.Hash((int)x, (int)z, 5, 5);
                    Vector3 c = new Vector3(x, -0.55f, z);
                    Vector3 size = new Vector3(1.5f, 0.08f, 0.14f);
                    if (h % 5 == 0) { Detail("Car park light (dead)", g, c, size, trofferDead); continue; }
                    if (h % 7 == 3)
                    {
                        GameObject panel = Detail("Car park light (faulty)", g, c, size, trofferFlicker);
                        var go = new GameObject("Faulty car park light");
                        go.transform.SetParent(g, false);
                        go.transform.localPosition = c + Vector3.down * 0.4f;
                        Light l = go.AddComponent<Light>();
                        l.type = LightType.Point;
                        l.range = 14f;
                        l.intensity = 1f;
                        l.color = CarParkLight;
                        l.shadows = LightShadows.None;
                        AudioSource buzz = go.AddComponent<AudioSource>();
                        buzz.playOnAwake = false;
                        FluorescentFlicker fl = go.AddComponent<FluorescentFlicker>();
                        fl.lightSource = l;
                        fl.panels = new[] { panel.GetComponent<Renderer>() };
                        fl.buzz = buzz;
                        fl.pattern = (FlickerPattern)(h % 3);
                        fl.seed = h;
                        continue;
                    }
                    Detail("Car park light", g, c, size, trofferMat);
                }
            }

            // Puddles with drains, stray cracks.
            for (int i = 0; i < 160; i++)
            {
                float x = Rand(-96f, 96f), z = Rand(-96f, 96f);
                if (Blocked(new AreaRect(x - 1f, z - 1f, x + 1f, z + 1f))) continue;
                float fy = FloorY(x, z) + 0.008f;
                if (i % 3 == 0)
                {
                    DecalQuad(g, new Vector3(x, fy, z), Vector3.up, Rand(1.5f, 3.5f), Rand(1.2f, 3f), Rand(0f, 360f), decalPuddle);
                    Detail("Drain grate", g, new Vector3(x + Rand(-0.5f, 0.5f), fy, z + Rand(-0.5f, 0.5f)), new Vector3(0.45f, 0.01f, 0.45f), blackMat);
                }
                else
                {
                    DecalQuad(g, new Vector3(x, fy, z), Vector3.up, Rand(1.5f, 3f), Rand(1.5f, 3f), Rand(0f, 360f), decalCrack);
                }
            }

            // Signs.
            CarParkSign(g, "EXIT  >", new Vector3(50f, -1.3f, -24f), Vector3.forward);
            CarParkSign(g, "EXIT  >", new Vector3(20f, -1.3f, -24f), Vector3.forward);
            CarParkSign(g, "LIFTS TO MALL", new Vector3(-13f, -1.3f, 2f), Vector3.forward);
            CarParkSign(g, "MAX SPEED 5 MPH", new Vector3(-30f, -1.3f, 0f), Vector3.right);
            CarParkSign(g, "STAIRS", new Vector3(22f, -1.3f, 14f), Vector3.forward);
            CarParkSign(g, "STAIRS", new Vector3(-22f, -1.3f, -14f), Vector3.back);
            CarParkSign(g, "NO UNAUTHORISED ACCESS", new Vector3(-85.7f, -2f, -38f), Vector3.left);
        }

        static void CarParkSign(Transform g, string text, Vector3 centre, Vector3 viewDir)
        {
            Detail("Sign hanger", g, centre + Vector3.up * 0.55f, new Vector3(0.04f, 0.8f, 0.04f), metal);
            Sign(g, text, centre, viewDir, 2.6f, 0.05f);
            Sign(g, text, centre + viewDir * 0.13f, -viewDir, 2.6f, 0.05f, false);
        }

        // =====================================================================
        // Concourses: vending machines, payphones, ATMs, plants, ad pillars, kiddie ride,
        // hanging direction signs, wet-floor signs.
        // =====================================================================

        static void DressConcourses(Transform parent)
        {
            Material vendFront = ArtLibrary.Plain("Vending_Front", new Color(0.5f, 0.6f, 0.75f), 0.8f, 0f, new Color(0.45f, 0.6f, 0.85f), false);
            Material vendBody = ArtLibrary.Plain("Vending_Body", new Color(0.55f, 0.12f, 0.12f), 0.5f);
            ArtLibrary.MarkDetail(vendFront);
            foreach (MallFloor f in MallLayout.ShopFloors)
            {
                float y = MallLayout.FloorY(f);
                foreach (WingId w in MallLayout.Wings)
                {
                    Transform g = Group($"{MallLayout.FloorCode(f)} {w} concourse details", parent);
                    Vector3 fwd = MallLayout.WingForward(w);
                    Quaternion yaw0 = Quaternion.LookRotation(fwd);

                    // Plants in the planters (same positions as the planters from Part 3).
                    int i = 0;
                    for (float u = 22f; u < MallLayout.WingEnd - 4f; u += 12f, i++)
                    {
                        if (i % 3 != 0) continue;
                        Vector3 c = MallLayout.WingPoint(w, u, 0f, y + 0.9f);
                        Plant(g, c, MallLayout.Hash((int)f, (int)w, i, 1));
                    }

                    // Vending machine and payphone flanking each passage doorway.
                    foreach (Passage p in MallLayout.Passages)
                    {
                        if (p.floor != f || p.wing != w) continue;
                        Vector3 inward = -MallLayout.WingRight(w) * p.side; // from the shop line towards the concourse
                        Quaternion face = Quaternion.LookRotation(inward);
                        Vector3 vm = MallLayout.WingPoint(w, p.u0 + 0.45f, p.side * (MallLayout.ConcourseHalf - 0.42f), y);
                        GameObject body = EditorBuild.Box("Vending machine", g, vm + Vector3.up * 0.92f, new Vector3(0.82f, 1.84f, 0.78f), vendBody, SurfaceType.Metal, face);
                        body.isStatic = true;
                        GameObject front = Detail("Vending machine front", g, vm + Vector3.up * 1.05f + inward * 0.392f, new Vector3(0.6f, 1.2f, 0.01f), vendFront, face);
                        // still on, all night: a cold glow on the floor, and the compressor's hum
                        int vh = Mathf.Abs(MallLayout.Hash((int)f, (int)w, (int)(p.u0 * 10f), 41));
                        var lgo = new GameObject("Vending glow");
                        lgo.transform.SetParent(g, false);
                        lgo.transform.localPosition = vm + Vector3.up * 1.1f + inward * 0.75f;
                        Light vl = lgo.AddComponent<Light>();
                        vl.type = LightType.Point; vl.range = 4.5f; vl.intensity = 0.9f; vl.color = new Color(0.62f, 0.75f, 1f);
                        vl.shadows = LightShadows.Soft; vl.lightmapBakeType = LightmapBakeType.Realtime;
                        var box = front.AddComponent<SignLightbox>();
                        box.faces = new[] { front.GetComponent<Renderer>() };
                        box.emission = new Color(0.45f, 0.6f, 0.85f) * 1.6f;
                        box.glow = vl;
                        if (vh % 2 == 0)
                        {
                            var mg = new GameObject("Moths");
                            mg.transform.SetParent(g, false);
                            mg.transform.localPosition = vm + Vector3.up * 1.3f + inward * 0.6f;
                            var mo = mg.AddComponent<AfterHours.World.Moths>(); mo.count = 4; mo.radius = 0.3f;
                        }
                        box.seed = vh;
                        box.state = vh % 6 == 0 ? SignLightbox.State.Dead : vh % 6 == 1 ? SignLightbox.State.Failing : SignLightbox.State.Lit;
                        if (box.state != SignLightbox.State.Dead)
                        {
                            var hum = new GameObject("Vending hum");
                            hum.transform.SetParent(g, false);
                            hum.transform.localPosition = vm + Vector3.up * 0.4f;
                            hum.AddComponent<AudioSource>().playOnAwake = false;
                            AmbientSound a = hum.AddComponent<AmbientSound>();
                            a.kind = ProceduralSounds.Ambience.Buzz; a.volume = 0.12f; a.maxDistance = 7f; a.maskingLevel = 0.15f;
                        }
                        Vector3 pp = MallLayout.WingPoint(w, p.u1 - 0.45f, p.side * (MallLayout.ConcourseHalf - 0.15f), y);
                        Detail("Payphone hood", g, pp + Vector3.up * 1.6f, new Vector3(0.6f, 0.9f, 0.3f), metal, face);
                        EditorBuild.Box("Payphone", g, pp + Vector3.up * 1.3f + inward * 0.05f, new Vector3(0.32f, 0.5f, 0.18f), blackMat, SurfaceType.Metal, face);
                    }

                    // Advert pillars down the middle of the concourse.
                    foreach (float u in new[] { 28f, 64f })
                    {
                        Vector3 c = MallLayout.WingPoint(w, u, 0f, y);
                        EditorBuild.Box("Advert pillar", g, c + Vector3.up * 1.1f, new Vector3(1.2f, 2.2f, 0.45f), darkPanel, SurfaceType.Metal, yaw0);
                        int ah = MallLayout.Hash((int)f, (int)w, (int)u, 7);
                        for (int s = -1; s <= 1; s += 2)
                        {
                            Vector3 n = fwd * s;
                            Detail("Advert pillar panel", g, c + Vector3.up * 1.2f + n * 0.226f, new Vector3(1.0f, 1.7f, 0.004f), adMaterials[(ah + s + 1) % adMaterials.Count], Quaternion.LookRotation(n));
                            EditorBuild.Label(g, AdSlogans[(ah + s + 2) % AdSlogans.Length], c + Vector3.up * 1.3f + n * 0.24f, Mathf.Atan2(-n.x, -n.z) * Mathf.Rad2Deg, 0.02f);
                        }
                    }

                    // Hanging direction signs.
                    foreach (float u in new[] { 40f, 76f })
                    {
                        Vector3 c = MallLayout.WingPoint(w, u, 0f, y + 3.55f);
                        MallUnit anchor = null;
                        foreach (MallUnit a in MallLayout.Units) if (a.isAnchor && a.floor == f && a.wing == w) anchor = a;
                        Detail("Sign rod", g, c + Vector3.up * 0.6f + MallLayout.WingRight(w) * 1.2f, new Vector3(0.03f, 0.9f, 0.03f), metal);
                        Detail("Sign rod", g, c + Vector3.up * 0.6f - MallLayout.WingRight(w) * 1.2f, new Vector3(0.03f, 0.9f, 0.03f), metal);
                        Sign(g, (anchor != null ? anchor.name.ToUpperInvariant() : "") + "   >", c, fwd, 3.2f, 0.045f);
                        Sign(g, "<   ATRIUM  -  LIFTS  -  EXITS", c + fwd * 0.13f, -fwd, 3.2f, 0.04f, false);
                    }

                    // ATM beside the anchor entrance.
                    {
                        Vector3 c = MallLayout.WingPoint(w, MallLayout.WingEnd - 0.4f, 5.6f, y);
                        Quaternion face = Quaternion.LookRotation(-fwd);
                        EditorBuild.Box("Cash machine", g, c + Vector3.up * 0.85f, new Vector3(0.8f, 1.7f, 0.6f), darkPanel, SurfaceType.Metal, face);
                        Detail("Cash machine screen", g, c + Vector3.up * 1.25f - fwd * 0.302f, new Vector3(0.32f, 0.24f, 0.005f), vendFront, face);
                    }

                    // Odd ones out: wet floor signs left behind.
                    int wh = MallLayout.Hash((int)f, (int)w, 77, 1);
                    if (wh % 2 == 0)
                    {
                        Vector3 c = MallLayout.WingPoint(w, 30f + wh % 50, Rand(-4f, 4f), y);
                        GameObject sign = EditorBuild.Box("Wet floor sign", g, c + Vector3.up * 0.33f, new Vector3(0.3f, 0.64f, 0.25f), yellowPaint, SurfaceType.Wood,
                            Quaternion.Euler(0f, Rand(0f, 360f), 0f), false);
                        Rigidbody rb = sign.AddComponent<Rigidbody>();
                        rb.mass = 1.5f;
                        sign.AddComponent<PhysicsProp>().material = SurfaceType.Wood;
                        EditorBuild.SetLayerRecursive(sign, GameLayers.PropLayer);
                        DecalQuad(g, c + Vector3.up * 0.01f + fwd * 0.8f, Vector3.up, 1.6f, 1.2f, Rand(0f, 360f), decalPuddle);
                    }
                }
            }

            // Kiddie ride near the atrium (ground floor).
            Material rideMat = ArtLibrary.Plain("KiddieRide", new Color(0.85f, 0.3f, 0.2f), 0.7f);
            Vector3 k = new Vector3(4f, 0f, 20f);
            EditorBuild.Box("Kiddie ride base", parent, k + Vector3.up * 0.2f, new Vector3(1.2f, 0.4f, 0.9f), darkPanel, SurfaceType.Metal);
            GameObject rideCar = EditorBuild.Box("Kiddie ride car", parent, k + Vector3.up * 0.75f, new Vector3(1.0f, 0.6f, 0.6f), rideMat, SurfaceType.Metal, null, false);
            Detail("Kiddie ride coin box", parent, k + new Vector3(0.55f, 0.95f, 0f), new Vector3(0.12f, 0.25f, 0.15f), metal);
            var bulbGo = new GameObject("Kiddie ride bulbs");
            bulbGo.transform.SetParent(parent, false);
            bulbGo.transform.localPosition = k + Vector3.up * 1.3f;
            Light bl = bulbGo.AddComponent<Light>();
            bl.type = LightType.Point; bl.range = 4f; bl.intensity = 1.2f; bl.color = new Color(1f, 0.75f, 0.4f);
            bl.shadows = LightShadows.None; bl.lightmapBakeType = LightmapBakeType.Realtime;
            // the component rocks the car, so it lives on a parent at the same spot
            var pivot = new GameObject("Kiddie ride");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = k;
            rideCar.transform.SetParent(pivot.transform, true);
            var ride = pivot.AddComponent<KiddieRide>();
            ride.car = rideCar.transform;
            ride.bulbs = bl;
        }

        static void Plant(Transform g, Vector3 top, int h)
        {
            var go = new GameObject("Plant (dying)");
            go.transform.SetParent(g, false);
            go.transform.localPosition = top;
            go.transform.localRotation = Quaternion.Euler(0f, h % 360, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = PlantMesh(Mathf.Abs(h) % 4);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[]
            {
                Mat("Plant stem", new Color(0.3f, 0.26f, 0.16f), 0.2f),
                TwoSided(Mat("Plant leaf", new Color(0.2f, 0.3f, 0.12f), 0.35f)),
                TwoSided(Mat("Plant leaf dead", new Color(0.42f, 0.33f, 0.18f), 0.15f)),
            };
            go.isStatic = true;
        }

        // =====================================================================
        // Shop interiors: mannequins, TVs, menu boards, tills, security gates.
        // =====================================================================

        static void DressShopInteriors(Transform parent)
        {
            Transform g = Group("Shop details", parent);
            foreach (MallUnit u in MallLayout.Units)
            {
                if (u.isAnchor || !u.HasInterior) continue;
                float y = MallLayout.FloorY(u.floor);
                WingId w = u.wing;
                int side = u.side;
                Vector3 inward = MallLayout.WingRight(w) * side;
                Quaternion faceOut = Quaternion.LookRotation(-inward);

                // Till on the counter.
                Vector3 till = MallLayout.WingPoint(w, u.u0 + 2f, side * 11.6f, y + 1.05f);
                Detail("Till", g, till + Vector3.up * 0.12f, new Vector3(0.35f, 0.24f, 0.3f), blackMat, faceOut);

                switch (u.category)
                {
                    case UnitCategory.Fashion:
                    case UnitCategory.Footwear:
                        for (int k = 0; k < 2; k++)
                        {
                            Vector3 m = MallLayout.WingPoint(w, u.u0 + 1.4f + k * (u.u1 - u.u0 - 2.8f), side * 7.4f, y);
                            Mannequin(g, m, faceOut);
                        }
                        SecurityGates(g, u, y);
                        break;
                    case UnitCategory.Electronics:
                        for (int k = 0; k < 3; k++)
                        {
                            Vector3 tv = MallLayout.WingPoint(w, u.u0 + 1.2f + k * 1.4f, side * 13.5f, y + 1.7f);
                            Detail("TV", g, tv - inward * 0.05f, new Vector3(1.0f, 0.62f, 0.08f), blackMat, faceOut);
                        }
                        SecurityGates(g, u, y);
                        break;
                    case UnitCategory.Food:
                    case UnitCategory.Cafe:
                    case UnitCategory.Restaurant:
                        Vector3 menu = MallLayout.WingPoint(w, u.u0 + 2.2f, side * 13.45f, y + 2.6f);
                        Detail("Menu board", g, menu - inward * 0.03f, new Vector3(2.4f, 0.9f, 0.04f), darkPanel, faceOut);
                        EditorBuild.Label(g, "MENU", menu - inward * 0.06f, Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg, 0.03f);
                        Detail("Coffee machine", g, till + Vector3.up * 0.25f + MallLayout.WingForward(w) * 0.8f, new Vector3(0.5f, 0.5f, 0.45f), metal, faceOut);
                        break;
                    case UnitCategory.Health:
                    case UnitCategory.Jewellery:
                        SecurityGates(g, u, y);
                        break;
                }
            }
        }

        static readonly string[] MannequinVariants = { "female_top", "male_bare", "female_dressed", "male_top" };
        static readonly Dictionary<string, Mesh> mannequinMeshes = new Dictionary<string, Mesh>();
        static readonly Color[] MannequinClothes =
        {
            new Color(0.12f, 0.12f, 0.14f), new Color(0.3f, 0.12f, 0.1f), new Color(0.6f, 0.58f, 0.52f), new Color(0.14f, 0.2f, 0.28f),
        };

        /// <summary>
        /// The mannequin meshes (tools/chargen/mannequins.py writes them as .bytes; here they become Unity mesh
        /// assets, so the scene keeps them). Null if the file is missing.
        /// </summary>
        static Mesh MannequinMesh(string variant)
        {
            if (mannequinMeshes.TryGetValue(variant, out Mesh cached) && cached != null) return cached;
            string dir = EditorBuild.Root + "/Models/Mannequin";
            var src = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(dir + "/mannequin_" + variant + ".bytes");
            if (src == null) return null;
            byte[] b = src.bytes;
            if (b.Length < 8 || b[0] != (byte)'M' || b[1] != (byte)'N' || b[2] != (byte)'Q' || b[3] != (byte)'1') return null;
            int o = 4;
            int nv = System.BitConverter.ToInt32(b, o); o += 4;
            var pos = new Vector3[nv]; var nrm = new Vector3[nv];
            for (int i = 0; i < nv; i++) { pos[i] = new Vector3(System.BitConverter.ToSingle(b, o), System.BitConverter.ToSingle(b, o + 4), System.BitConverter.ToSingle(b, o + 8)); o += 12; }
            for (int i = 0; i < nv; i++) { nrm[i] = new Vector3(System.BitConverter.ToSingle(b, o), System.BitConverter.ToSingle(b, o + 4), System.BitConverter.ToSingle(b, o + 8)); o += 12; }
            int ns = System.BitConverter.ToInt32(b, o); o += 4;
            var mesh = new Mesh { name = "Mannequin " + variant, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = pos; mesh.normals = nrm;
            mesh.subMeshCount = ns;
            for (int s = 0; s < ns; s++)
            {
                int n = System.BitConverter.ToInt32(b, o); o += 4;
                var idx = new int[n];
                for (int i = 0; i < n; i++) { idx[i] = (int)System.BitConverter.ToUInt32(b, o); o += 4; }
                mesh.SetTriangles(idx, s);
            }
            mesh.RecalculateBounds();
            EditorBuild.EnsureFolder(dir + "/Generated");
            string path = dir + "/Generated/mannequin_" + variant + ".asset";
            UnityEditor.AssetDatabase.DeleteAsset(path);
            UnityEditor.AssetDatabase.CreateAsset(mesh, path);
            mannequinMeshes[variant] = mesh;
            return mesh;
        }

        /// <summary>
        /// A fibreglass shop mannequin on its glass plate: a real figure (egg head, glossy finish), some dressed.
        /// Falls back to a simple stand-and-torso if the mesh files aren't there.
        /// </summary>
        static void Mannequin(Transform g, Vector3 floor, Quaternion face)
        {
            int h = Mathf.Abs(MallLayout.Hash((int)(floor.x * 10f), (int)(floor.z * 10f), (int)floor.y, 61));
            string variant = MannequinVariants[h % MannequinVariants.Length];
            Mesh mesh = MannequinMesh(variant);
            if (mesh == null)
            {
                Cyl("Mannequin stand", g, floor, floor + Vector3.up * 0.9f, 0.04f, metal);
                Detail("Mannequin base", g, floor + Vector3.up * 0.01f, new Vector3(0.35f, 0.02f, 0.35f), metal, face);
                GameObject torso = Cyl("Mannequin torso", g, floor + Vector3.up * 0.95f, floor + Vector3.up * 1.55f, 0.36f, whiteFixture, true);
                torso.transform.localScale = new Vector3(0.36f, 0.3f, 0.22f);
                torso.transform.localRotation = face;
                return;
            }
            // glass base plate and the steel rod up into the calf
            Detail("Mannequin plate", g, floor + Vector3.up * 0.008f, new Vector3(0.42f, 0.016f, 0.42f), darkGlass, face);
            var go = new GameObject("Mannequin (" + variant + ")");
            go.transform.SetParent(g, false);
            go.transform.localPosition = floor + Vector3.up * 0.016f;
            // most face the window; a few are turned a little, one or two look somewhere else entirely
            float turn = h % 7 == 0 ? (h % 2 == 0 ? 120f : -150f) : ((h >> 3) % 31 - 15);
            go.transform.localRotation = face * Quaternion.Euler(0f, turn, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            Material glass = Mat("Mannequin fibreglass", new Color(0.86f, 0.84f, 0.8f), 0.78f);
            Material cloth = Mat("Mannequin clothes " + (h % MannequinClothes.Length), MannequinClothes[h % MannequinClothes.Length], 0.15f);
            mr.sharedMaterials = mesh.subMeshCount > 1 ? new[] { glass, cloth } : new[] { glass };
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.9f, 0f); col.height = 1.8f; col.radius = 0.2f;
            go.AddComponent<MannequinWatch>().seed = h;
        }

        static void SecurityGates(Transform g, MallUnit u, float y)
        {
            float mid = (u.u0 + u.u1) * 0.5f;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 c = MallLayout.WingPoint(u.wing, mid + s * 0.75f, u.side * (MallLayout.ConcourseHalf + 1.3f), y + 0.8f);
                EditorBuild.Box("Security gate", g, c, new Vector3(0.08f, 1.6f, 0.6f), metal, SurfaceType.Metal,
                    Quaternion.LookRotation(MallLayout.WingRight(u.wing)));
            }
        }

        // =====================================================================
        // Service corridors: pipes and cable trays. Stairwells: handrails.
        // =====================================================================

        static void DressServices(Transform parent)
        {
            Transform g = Group("Services", parent);
            foreach (MallFloor f in MallLayout.ShopFloors)
            {
                float y = MallLayout.FloorY(f);
                foreach (WingId w in MallLayout.Wings)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        for (float u = 19.5f; u < MallLayout.WingEnd - 1f; u += 19f)
                        {
                            float u1 = Mathf.Min(u + 19f, MallLayout.WingEnd - 0.5f);
                            Cyl("Pipe", g, MallLayout.WingPoint(w, u, side * 18.6f, y + 4.25f), MallLayout.WingPoint(w, u1, side * 18.6f, y + 4.25f), 0.12f, plantMat);
                            Cyl("Pipe", g, MallLayout.WingPoint(w, u, side * 18.3f, y + 4.12f), MallLayout.WingPoint(w, u1, side * 18.3f, y + 4.12f), 0.07f, metal);
                            Vector3 a = MallLayout.WingPoint(w, u, side * 16.75f, y + 4.33f), b = MallLayout.WingPoint(w, u1, side * 16.75f, y + 4.33f);
                            Vector3 lo = Vector3.Min(a, b) - new Vector3(0.15f, 0.04f, 0.15f), hi = Vector3.Max(a, b) + new Vector3(0.15f, 0.04f, 0.15f);
                            Vector3 along = MallLayout.WingForward(w);
                            // Keep the tray 0.3 m wide across the corridor.
                            if (Mathf.Abs(along.x) > 0.5f) { lo.z = a.z - 0.15f; hi.z = a.z + 0.15f; }
                            else { lo.x = a.x - 0.15f; hi.x = a.x + 0.15f; }
                            Decor("Cable tray", g, lo, hi, metal);
                        }
                    }
                }
            }

            // Handrails in the stairwells (one on each flight's open side).
            foreach (var (sx, sz) in MallLayout.Quadrants)
            {
                bool toRoof = sx > 0 && sz > 0;
                var levels = new List<float> { MallLayout.CarParkY, 0f, MallLayout.FloorY(MallFloor.Level1), MallLayout.FloorY(MallFloor.Level2) };
                if (toRoof) levels.Add(MallLayout.RoofY);
                for (int i = 0; i < levels.Count - 1; i++)
                {
                    float y0 = levels[i], half = (levels[i + 1] - y0) * 0.5f;
                    Cyl("Handrail", g, CoreP(sx, sz, 2.82f, y0 + 0.95f, 2.25f), CoreP(sx, sz, 2.82f, y0 + half + 0.95f, 6.75f), 0.045f, metal);
                    Cyl("Handrail", g, CoreP(sx, sz, 3.18f, y0 + half + 0.95f, 6.75f), CoreP(sx, sz, 3.18f, y0 + 2f * half + 0.95f, 2.25f), 0.045f, metal);
                    Cyl("Handrail", g, CoreP(sx, sz, CoreW - 0.3f, y0 + half + 0.95f, 6.75f), CoreP(sx, sz, CoreW - 0.3f, y0 + 2f * half + 0.95f, 2.25f), 0.045f, metal);
                    Cyl("Handrail", g, CoreP(sx, sz, 0.3f, y0 + 0.95f, 2.25f), CoreP(sx, sz, 0.3f, y0 + half + 0.95f, 6.75f), 0.045f, metal);
                }
            }
        }

        // =====================================================================
        // Ambience: looping sounds and reverb zones.
        // =====================================================================

        static void BuildAmbience(Transform parent)
        {
            Transform g = Group("Ambience", parent);

            void Amb(Vector3 pos, ProceduralSounds.Ambience kind, float volume, float maxDistance, float masking)
            {
                var go = new GameObject("Ambience " + kind);
                go.transform.SetParent(g, false);
                go.transform.localPosition = pos;
                go.AddComponent<AudioSource>().playOnAwake = false;
                AmbientSound a = go.AddComponent<AmbientSound>();
                a.kind = kind;
                a.volume = volume;
                a.maxDistance = maxDistance;
                a.maskingLevel = masking;
            }

            void Rev(Vector3 pos, float min, float max, AudioReverbPreset preset)
            {
                var go = new GameObject("Reverb " + preset);
                go.transform.SetParent(g, false);
                go.transform.localPosition = pos;
                AudioReverbZone z = go.AddComponent<AudioReverbZone>();
                z.minDistance = min;
                z.maxDistance = max;
                z.reverbPreset = preset;
            }

            Amb(new Vector3(0f, 0.6f, 0f), ProceduralSounds.Ambience.Water, 0.45f, 24f, 0.35f);
            Rev(new Vector3(0f, 6f, 0f), 12f, 22f, AudioReverbPreset.Auditorium);

            foreach (MallFloor f in MallLayout.ShopFloors)
            {
                float y = MallLayout.FloorY(f);
                Amb(new Vector3(0f, y + 4.3f, 12f), ProceduralSounds.Ambience.FluorescentHum, 0.15f, 14f, 0.1f);
                foreach (WingId w in MallLayout.Wings)
                {
                    for (float u = 28f; u < 96f; u += 24f)
                    {
                        Amb(MallLayout.WingPoint(w, u, 0f, y + 4.3f), ProceduralSounds.Ambience.Ventilation, 0.2f, 16f, 0.15f);
                        Rev(MallLayout.WingPoint(w, u, 0f, y + 2f), 6f, 13f, AudioReverbPreset.Hallway);
                    }
                    for (int side = -1; side <= 1; side += 2)
                    {
                        for (float u = 30f; u < 96f; u += 30f)
                        {
                            Amb(MallLayout.WingPoint(w, u, side * 17.5f, y + 4.2f), ProceduralSounds.Ambience.Ventilation, 0.22f, 10f, 0.2f);
                            Rev(MallLayout.WingPoint(w, u, side * 17.5f, y + 2f), 2f, 6f, AudioReverbPreset.StoneCorridor);
                        }
                    }
                    Amb(MallLayout.WingPoint(w, 110f, 0f, y + 4f), ProceduralSounds.Ambience.FluorescentHum, 0.15f, 18f, 0.1f);
                }
                foreach (var (sx, sz) in MallLayout.Quadrants)
                {
                    MallLayout.QuadrantRoom ra = MallLayout.QuadrantRoomA(f, sx, sz), rb = MallLayout.QuadrantRoomB(f, sx, sz);
                    if (ra.style == FurnishStyle.Plant) Amb(new Vector3(sx * 36f, y + 2f, sz * 26f), ProceduralSounds.Ambience.Machinery, 0.7f, 28f, 0.75f);
                    if (rb.style == FurnishStyle.Plant) Amb(new Vector3(sx * 32f, y + 2f, sz * 38.5f), ProceduralSounds.Ambience.Machinery, 0.7f, 28f, 0.75f);
                    if (ra.type == FacilityType.Toilets) Rev(new Vector3(sx * 34f, y + 1.5f, sz * 26f), 3f, 8f, AudioReverbPreset.Bathroom);
                    Rev(new Vector3(sx * 22.3f, y + 1.5f, sz * 23.8f), 2f, 5f, AudioReverbPreset.Stoneroom);
                }
            }

            for (float x = -75f; x <= 75f; x += 50f)
            {
                for (float z = -75f; z <= 75f; z += 50f)
                {
                    Amb(new Vector3(x, -1f, z), ProceduralSounds.Ambience.CarParkFans, 0.3f, 32f, 0.3f);
                    Rev(new Vector3(x, -2f, z), 25f, 40f, AudioReverbPreset.ParkingLot);
                }
            }
            Amb(new Vector3(-93f, -2.5f, -31f), ProceduralSounds.Ambience.Machinery, 0.8f, 30f, 0.8f);
            Rev(new Vector3(-140f, -2.5f, -18.5f), 10f, 45f, AudioReverbPreset.SewerPipe);
        }
    }
}
