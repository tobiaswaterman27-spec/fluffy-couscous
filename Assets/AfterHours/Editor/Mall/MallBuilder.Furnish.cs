using System.Collections.Generic;
using UnityEngine;
using AfterHours.Core;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    public static partial class MallBuilder
    {
        /// <summary>
        /// Fills a room with fixtures for its style, never blocking doorways / entrances (keepClear).
        /// Everything is solid and collidable. Desks are solid blocks (no hiding underneath). Chairs and
        /// crates are light physics props that can be pushed (and later knocked about by Elliot).
        /// </summary>
        static void Furnish(Transform parent, AreaRect area, float y, FurnishStyle style, List<AreaRect> keepClear, int seed, Material accent)
        {
            if (area.Width < 1.5f || area.Depth < 1.5f) return;
            Transform g = Group("Fittings (" + style + ")", parent);
            switch (style)
            {
                case FurnishStyle.Shelving:
                    Rows(g, area, y, 2.4f, 0.6f, 1.9f, 2.3f, 1.3f, metal, SurfaceType.Metal, keepClear, seed % 2 == 0 ? stockBeauty : stockBoxes, seed);
                    break;
                case FurnishStyle.Rails:
                    Rows(g, area, y, 1.8f, 0.5f, 1.4f, 2.0f, 1.5f, railMat, SurfaceType.Metal, keepClear, stockClothes, seed);
                    break;
                case FurnishStyle.Tables:
                    PlaceGrid(g, area, y, 1.4f, 0.9f, 0.9f, 2.7f, accent, SurfaceType.Wood, keepClear, null);
                    break;
                case FurnishStyle.Department:
                    PlaceGrid(g, area, y, 1.8f, 1.2f, 1.0f, 3.8f, accent, SurfaceType.Wood, keepClear, null);
                    break;
                case FurnishStyle.Supermarket:
                    Rows(g, area, y, 8f, 1.0f, 1.8f, 3.0f, 2.2f, metal, SurfaceType.Metal, keepClear, stockGrocery, seed);
                    break;
                case FurnishStyle.Food:
                    PlaceGrid(g, area, y, 0.9f, 0.9f, 0.75f, 2.9f, woodMat, SurfaceType.Wood, keepClear, (centre) => { Chairs(g, centre, y); Leftovers(g, centre + Vector3.up * 0.75f); });
                    break;
                case FurnishStyle.Office:
                    PlaceGrid(g, area, y, 1.6f, 0.8f, 0.75f, 3.4f, woodMat, SurfaceType.Wood, keepClear, (centre) =>
                    {
                        Prop(g, "Office chair", centre + new Vector3(0f, 0f, -0.75f), new Vector3(0.5f, 0.9f, 0.5f), 8f, propMat, SurfaceType.Metal);
                        InteractiveFactory.AddDrawer(g, centre + new Vector3(0.45f, 0.6f, -0.15f), new Vector3(0.42f, 0.14f, 0.5f), Vector3.back, woodMat);
                    });
                    break;
                case FurnishStyle.Plant:
                    PlaceGrid(g, area, y, 2.4f, 1.6f, 2.2f, 4.6f, plantMat, SurfaceType.Metal, keepClear, null);
                    break;
                case FurnishStyle.Storage:
                    Rows(g, area, y, 3f, 0.8f, 2.2f, 2.5f, 1.4f, metal, SurfaceType.Metal, keepClear, cardboardMat, seed);
                    Crates(g, area, y, keepClear, seed);
                    break;
                case FurnishStyle.Toilets:
                    Toilets(g, area, y, keepClear);
                    break;
                case FurnishStyle.Leisure:
                    PlaceGrid(g, area, y, 0.8f, 0.9f, 1.8f, 1.9f, accent, SurfaceType.Metal, keepClear, null);
                    break;
            }
        }

        static bool Blocked(AreaRect r, List<AreaRect> keepClear)
        {
            if (keepClear == null) return false;
            AreaRect e = r.Expanded(0.45f);
            foreach (AreaRect k in keepClear)
            {
                if (e.Overlaps(k)) return true;
            }
            return false;
        }

        /// <summary>Long fixtures in rows along the room's longer axis, with aisles between them.</summary>
        static void Rows(Transform g, AreaRect area, float y, float length, float width, float height, float rowSpacing, float gap,
                         Material m, SurfaceType s, List<AreaRect> keepClear, Material stock = null, int seed = 0)
        {
            bool alongX = area.Width >= area.Depth;
            float longMin = alongX ? area.minX : area.minZ, longMax = alongX ? area.maxX : area.maxZ;
            float crossMin = alongX ? area.minZ : area.minX, crossMax = alongX ? area.maxZ : area.maxX;
            const float margin = 1.1f;

            for (float c = crossMin + margin + width * 0.5f; c <= crossMax - margin - width * 0.5f; c += rowSpacing)
            {
                for (float l = longMin + margin; l + length <= longMax - margin; l += length + gap)
                {
                    AreaRect r = alongX
                        ? new AreaRect(l, c - width * 0.5f, l + length, c + width * 0.5f)
                        : new AreaRect(c - width * 0.5f, l, c + width * 0.5f, l + length);
                    if (Blocked(r, keepClear)) continue;
                    BoxRect("Fixture", g, r, y, y + height, m, s);
                    if (stock != null) Stock(g, r, y, height, stock, seed++);
                }
            }
        }

        delegate void Extra(Vector3 centre);

        /// <summary>Free-standing items on a grid (tables, machines, cabinets).</summary>
        static void PlaceGrid(Transform g, AreaRect area, float y, float w, float d, float h, float spacing, Material m, SurfaceType s,
                         List<AreaRect> keepClear, Extra extra)
        {
            const float margin = 1.3f;
            for (float x = area.minX + margin + w * 0.5f; x <= area.maxX - margin - w * 0.5f; x += spacing)
            {
                for (float z = area.minZ + margin + d * 0.5f; z <= area.maxZ - margin - d * 0.5f; z += spacing)
                {
                    AreaRect r = new AreaRect(x - w * 0.5f, z - d * 0.5f, x + w * 0.5f, z + d * 0.5f);
                    if (Blocked(r.Expanded(0.6f), keepClear)) continue;
                    BoxRect("Fixture", g, r, y, y + h, m, s);
                    extra?.Invoke(new Vector3(x, y, z));
                }
            }
        }

        static void Chairs(Transform g, Vector3 tableCentre, float y)
        {
            Prop(g, "Chair", tableCentre + new Vector3(0f, 0f, 0.8f), new Vector3(0.45f, 0.85f, 0.45f), 5f, propMat, SurfaceType.Wood);
            Prop(g, "Chair", tableCentre + new Vector3(0f, 0f, -0.8f), new Vector3(0.45f, 0.85f, 0.45f), 5f, propMat, SurfaceType.Wood);
        }

        /// <summary>Whatever people left when the place emptied: a tray, a cup, napkins, a wrapper.</summary>
        static void Leftovers(Transform g, Vector3 tableTop)
        {
            int h = MallLayout.Hash((int)(tableTop.x * 10f), (int)(tableTop.z * 10f), 3, 77);
            if (h % 3 == 0) return;
            float yaw = h % 360;
            Detail("Tray", g, tableTop + new Vector3(0.05f, 0.01f, -0.05f), new Vector3(0.45f, 0.02f, 0.32f), h % 2 == 0 ? redMat : darkPanel, Quaternion.Euler(0f, yaw, 0f));
            Vector3 cup = tableTop + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0.12f, 0.02f, 0.05f);
            Cyl("Cup", g, cup, cup + Vector3.up * 0.13f, 0.08f, cupMat);
            if (h % 5 == 0) DecalQuad(g, tableTop + new Vector3(-0.15f, 0.025f, 0.1f), Vector3.up, 0.25f, 0.25f, yaw, decalLitter);
        }

        static void Crates(Transform g, AreaRect area, float y, List<AreaRect> keepClear, int seed)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < 4; i++)
            {
                float x = Mathf.Lerp(area.minX + 1.5f, area.maxX - 1.5f, (float)rng.NextDouble());
                float z = Mathf.Lerp(area.minZ + 1.5f, area.maxZ - 1.5f, (float)rng.NextDouble());
                float size = 0.4f + (float)rng.NextDouble() * 0.3f;
                AreaRect r = new AreaRect(x - size, z - size, x + size, z + size);
                if (Blocked(r, keepClear)) continue;
                Prop(g, "Crate", new Vector3(x, y, z), new Vector3(size, size, size), 6f + size * 20f, propMat, SurfaceType.Wood);
            }
        }

        /// <summary>A movable physics prop sitting on the floor at 'floorCentre'.</summary>
        static void Prop(Transform g, string name, Vector3 floorCentre, Vector3 size, float mass, Material m, SurfaceType s)
        {
            GameObject go = EditorBuild.Box(name, g, floorCentre + Vector3.up * (size.y * 0.5f + 0.01f), size, m, s, null, false);
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            go.AddComponent<PhysicsProp>().material = s;
            EditorBuild.SetLayerRecursive(go, GameLayers.PropLayer);
        }

        /// <summary>Toilet cubicles along one long wall, sinks along the opposite wall.</summary>
        static void Toilets(Transform g, AreaRect area, float y, List<AreaRect> keepClear)
        {
            bool alongX = area.Width >= area.Depth;
            float len = alongX ? area.Width : area.Depth;
            int count = Mathf.Clamp(Mathf.FloorToInt((len - 2f) / 1.1f), 0, 8);
            for (int i = 0; i <= count; i++)
            {
                float p = (alongX ? area.minX : area.minZ) + 1f + i * 1.1f;
                AreaRect wall = alongX
                    ? new AreaRect(p, area.maxZ - 1.6f, p + 0.05f, area.maxZ)
                    : new AreaRect(area.maxX - 1.6f, p, area.maxX, p + 0.05f);
                if (Blocked(wall, keepClear)) continue;
                BoxRect("Cubicle partition", g, wall, y, y + 2.0f, toiletMat, SurfaceType.Tile);
                if (i == count) continue;
                // WC pan at the back of the cubicle.
                AreaRect pan = alongX
                    ? new AreaRect(p + 0.35f, area.maxZ - 0.62f, p + 0.75f, area.maxZ - 0.05f)
                    : new AreaRect(area.maxX - 0.62f, p + 0.35f, area.maxX - 0.05f, p + 0.75f);
                BoxRect("WC", g, pan, y, y + 0.42f, porcelain, SurfaceType.Tile);
                AreaRect cistern = alongX
                    ? new AreaRect(p + 0.3f, area.maxZ - 0.2f, p + 0.8f, area.maxZ - 0.02f)
                    : new AreaRect(area.maxX - 0.2f, p + 0.3f, area.maxX - 0.02f, p + 0.8f);
                BoxRect("Cistern", g, cistern, y + 0.42f, y + 0.85f, porcelain, SurfaceType.Tile);
            }
            float s0 = (alongX ? area.minX : area.minZ) + 1.5f;
            float s1 = Mathf.Min(s0 + 4f, (alongX ? area.maxX : area.maxZ) - 1.5f);
            if (s1 - s0 > 1f)
            {
                AreaRect sinks = alongX
                    ? new AreaRect(s0, area.minZ, s1, area.minZ + 0.6f)
                    : new AreaRect(area.minX, s0, area.minX + 0.6f, s1);
                if (!Blocked(sinks, keepClear))
                {
                    BoxRect("Sinks", g, sinks, y, y + 0.9f, toiletMat, SurfaceType.Tile);
                    AreaRect mirror = alongX
                        ? new AreaRect(s0, area.minZ, s1, area.minZ + 0.02f)
                        : new AreaRect(area.minX, s0, area.minX + 0.02f, s1);
                    GameObject m = BoxRect("Mirror", g, mirror, y + 1.15f, y + 2.0f, mirrorMat, SurfaceType.Glass);
                    Object.DestroyImmediate(m.GetComponent<Collider>());
                    AreaRect dryer = alongX
                        ? new AreaRect(s1 + 0.4f, area.minZ, s1 + 0.7f, area.minZ + 0.22f)
                        : new AreaRect(area.minX, s1 + 0.4f, area.minX + 0.22f, s1 + 0.7f);
                    BoxRect("Hand dryer", g, dryer, y + 1.1f, y + 1.4f, whiteFixture, SurfaceType.Metal);
                }
            }
        }
    }
}
