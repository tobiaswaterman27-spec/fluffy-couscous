using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Real shop fittings instead of solid blocks: shelving units with a plinth, back panel, shelves, price rails and
    /// hundreds of individual products (in facings, with gaps where things sold out and the odd one knocked over);
    /// clothes rails with garments hanging on hangers; display tables with folded stacks; glass counters; wall
    /// shelving; tills with screens and card machines.
    ///
    /// Each shop's fittings are built into one mesh per material (thousands of small boxes, a handful of objects),
    /// which the mesh merger then folds into the area batches as usual. Collision is a few plain boxes.
    /// </summary>
    public static partial class MallBuilder
    {
        // ------------------------------------------------------------------ a tiny mesh builder

        class MeshKit
        {
            class Part { public readonly List<Vector3> v = new List<Vector3>(); public readonly List<Vector3> n = new List<Vector3>(); public readonly List<Vector2> uv = new List<Vector2>(); public readonly List<int> t = new List<int>(); public bool shadows; }
            readonly Dictionary<Material, Part> parts = new Dictionary<Material, Part>();
            Vector3 origin = Vector3.zero;
            Quaternion frame = Quaternion.identity;

            /// <summary>Local coordinates from here on: x along, y up, z out (the fixture's own frame).</summary>
            public void Frame(Vector3 o, Quaternion r) { origin = o; frame = r; }

            Part P(Material m, bool shadows)
            {
                if (!parts.TryGetValue(m, out Part p)) parts[m] = p = new Part();
                p.shadows |= shadows;
                return p;
            }

            public void Box(Vector3 c, Vector3 size, Material m, float yaw = 0f, bool shadows = true, float roll = 0f)
            {
                Part p = P(m, shadows);
                Quaternion r = frame * Quaternion.Euler(0f, yaw, roll);
                Vector3 wc = origin + frame * c;
                Vector3 h = size * 0.5f;
                for (int f = 0; f < 6; f++)
                {
                    Vector3 n = f switch { 0 => Vector3.right, 1 => Vector3.left, 2 => Vector3.up, 3 => Vector3.down, 4 => Vector3.forward, _ => Vector3.back };
                    Vector3 a1 = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                    Vector3 a2 = Vector3.Cross(n, a1);
                    int b = p.v.Count;
                    for (int k = 0; k < 4; k++)
                    {
                        float s1 = (k == 1 || k == 2) ? 1f : -1f, s2 = k >= 2 ? 1f : -1f;
                        Vector3 lp = Vector3.Scale(n + a1 * s1 + a2 * s2, h);
                        p.v.Add(wc + r * lp);
                        p.n.Add(r * n);
                        p.uv.Add(new Vector2(s1 * 0.5f + 0.5f, s2 * 0.5f + 0.5f));
                    }
                    // (Unity draws clockwise faces: checked from outside)
                    p.t.Add(b); p.t.Add(b + 1); p.t.Add(b + 2);
                    p.t.Add(b); p.t.Add(b + 2); p.t.Add(b + 3);
                }
            }

            /// <summary>A round bar from a to b (local), no end caps.</summary>
            public void Bar(Vector3 a, Vector3 b, float radius, Material m, int sides = 8)
            {
                Part p = P(m, true);
                Vector3 wa = origin + frame * a, wb = origin + frame * b;
                Vector3 d = (wb - wa).normalized;
                Vector3 u = Vector3.Cross(d, Mathf.Abs(d.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 w = Vector3.Cross(d, u);
                int b0 = p.v.Count;
                for (int i = 0; i <= sides; i++)
                {
                    float an = i / (float)sides * Mathf.PI * 2f;
                    Vector3 n = u * Mathf.Cos(an) + w * Mathf.Sin(an);
                    p.v.Add(wa + n * radius); p.n.Add(n); p.uv.Add(new Vector2(i / (float)sides, 0f));
                    p.v.Add(wb + n * radius); p.n.Add(n); p.uv.Add(new Vector2(i / (float)sides, 1f));
                }
                for (int i = 0; i < sides; i++)
                {
                    int a0 = b0 + i * 2;
                    p.t.Add(a0); p.t.Add(a0 + 3); p.t.Add(a0 + 1);
                    p.t.Add(a0); p.t.Add(a0 + 2); p.t.Add(a0 + 3);
                }
            }

            public void Build(Transform parent, string name)
            {
                foreach (var kv in parts)
                {
                    Part p = kv.Value;
                    if (p.v.Count == 0) continue;
                    var mesh = new Mesh { name = name, indexFormat = p.v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                    mesh.SetVertices(p.v); mesh.SetNormals(p.n); mesh.SetUVs(0, p.uv); mesh.SetTriangles(p.t, 0);
                    mesh.RecalculateBounds();
                    mesh.RecalculateTangents();
                    var go = new GameObject(name);
                    go.transform.SetParent(parent, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    MeshRenderer mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = kv.Key;
                    mr.shadowCastingMode = p.shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                }
                parts.Clear();
            }
        }

        // ------------------------------------------------------------------ materials: packaging and garments

        static Material[] productMats, groceryMats, clothMats;
        static Material priceTagMat, shelfEdgeMat, pegboardMat, hangerMat;

        static Material[] Palette(string prefix, Color[] cols, float smooth)
        {
            var mats = new Material[cols.Length];
            for (int i = 0; i < cols.Length; i++)
            {
                mats[i] = ArtLibrary.Plain(prefix + i, cols[i], smooth, 0f);
                ArtLibrary.MarkDetail(mats[i]);
            }
            return mats;
        }

        static void FixtureMaterials()
        {
            if (productMats != null && productMats[0] != null) return;
            productMats = Palette("Product_", new[]
            {
                new Color(0.82f, 0.8f, 0.76f), new Color(0.12f, 0.12f, 0.13f), new Color(0.62f, 0.13f, 0.12f), new Color(0.15f, 0.3f, 0.55f),
                new Color(0.86f, 0.72f, 0.2f), new Color(0.2f, 0.45f, 0.3f), new Color(0.85f, 0.55f, 0.6f), new Color(0.45f, 0.35f, 0.6f),
                new Color(0.9f, 0.45f, 0.15f), new Color(0.35f, 0.55f, 0.7f), new Color(0.55f, 0.42f, 0.3f), new Color(0.92f, 0.9f, 0.88f),
            }, 0.55f);
            groceryMats = Palette("Grocery_", new[]
            {
                new Color(0.8f, 0.15f, 0.12f), new Color(0.95f, 0.8f, 0.2f), new Color(0.2f, 0.5f, 0.25f), new Color(0.15f, 0.35f, 0.7f),
                new Color(0.95f, 0.55f, 0.1f), new Color(0.9f, 0.9f, 0.85f), new Color(0.5f, 0.3f, 0.15f), new Color(0.6f, 0.15f, 0.4f),
                new Color(0.75f, 0.65f, 0.45f), new Color(0.1f, 0.1f, 0.12f),
            }, 0.6f);
            clothMats = Palette("Cloth_", new[]
            {
                new Color(0.1f, 0.1f, 0.12f), new Color(0.85f, 0.84f, 0.8f), new Color(0.18f, 0.22f, 0.35f), new Color(0.45f, 0.42f, 0.38f),
                new Color(0.55f, 0.15f, 0.15f), new Color(0.3f, 0.38f, 0.28f), new Color(0.7f, 0.6f, 0.45f), new Color(0.6f, 0.5f, 0.55f),
                new Color(0.25f, 0.4f, 0.55f), new Color(0.75f, 0.7f, 0.3f),
            }, 0.15f);
            priceTagMat = ArtLibrary.Plain("PriceTag", new Color(0.95f, 0.85f, 0.2f), 0.4f, 0f); ArtLibrary.MarkDetail(priceTagMat);
            shelfEdgeMat = ArtLibrary.Plain("ShelfEdge", new Color(0.9f, 0.9f, 0.88f), 0.5f, 0f); ArtLibrary.MarkDetail(shelfEdgeMat);
            pegboardMat = ArtLibrary.Plain("Pegboard", new Color(0.78f, 0.78f, 0.76f), 0.35f, 0.2f);
            hangerMat = ArtLibrary.Plain("Hanger", new Color(0.08f, 0.08f, 0.08f), 0.5f, 0f); ArtLibrary.MarkDetail(hangerMat);
        }

        static void FixtureCollider(Transform parent, Vector3 centre, Vector3 size, Quaternion rot, SurfaceType s)
        {
            var go = new GameObject("Fixture collider");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(centre, rot);
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            go.AddComponent<SurfaceMaterial>().type = s;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>The frame for a fixture filling rect r (long axis = local x), standing at y.</summary>
        static void RectFrame(AreaRect r, float y, out Vector3 centre, out Quaternion rot, out float length, out float depth)
        {
            bool alongX = r.Width >= r.Depth;
            centre = new Vector3((r.minX + r.maxX) * 0.5f, y, (r.minZ + r.maxZ) * 0.5f);
            rot = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
            length = alongX ? r.Width : r.Depth;
            depth = alongX ? r.Depth : r.Width;
        }

        // ------------------------------------------------------------------ products on a shelf

        /// <summary>Products along a shelf (local x from x0 to x1), standing on top at y, from the front edge z0 back to z1.</summary>
        static void ShelfProducts(MeshKit k, System.Random rng, float x0, float x1, float y, float z0, float z1, float maxH, Material[] pal, float sizeScale = 1f)
        {
            float x = x0 + 0.02f;
            int run = 0;
            Material m = pal[0];
            float w = 0.1f, h = 0.2f, d = 0.15f;
            float dir = Mathf.Sign(z1 - z0);
            float depthAvail = Mathf.Abs(z1 - z0);
            while (x < x1 - 0.05f)
            {
                if (run <= 0)
                {
                    // a new product line: how many facings, its size and packaging colour
                    run = 2 + rng.Next(7);
                    m = pal[rng.Next(pal.Length)];
                    w = (0.05f + (float)rng.NextDouble() * 0.14f) * sizeScale;
                    h = Mathf.Min(maxH - 0.02f, (0.08f + (float)rng.NextDouble() * 0.26f) * sizeScale);
                    d = Mathf.Min(depthAvail - 0.02f, (0.06f + (float)rng.NextDouble() * 0.2f) * sizeScale);
                    if (rng.NextDouble() < 0.08) { x += w * (1 + rng.Next(3)); run = 0; continue; }   // sold out: a gap
                }
                if (x + w > x1 - 0.02f) break;
                // each facing is a row going back (two or three deep), the front one pulled forward
                int deep = Mathf.Max(1, Mathf.Min(3, (int)(depthAvail / Mathf.Max(d, 0.05f))));
                for (int j = 0; j < deep; j++)
                {
                    float zc = z0 + dir * (0.01f + d * (j + 0.5f));
                    if (j == 0 && rng.NextDouble() < 0.025)
                    {
                        // one knocked over, lying on its side
                        k.Box(new Vector3(x + w * 0.5f, y + w * 0.5f, zc), new Vector3(h, w, d), m, (float)rng.NextDouble() * 30f - 15f, false, 0f);
                        break;
                    }
                    k.Box(new Vector3(x + w * 0.5f, y + h * 0.5f, zc), new Vector3(w * 0.96f, h, d * 0.96f), m, (float)(rng.NextDouble() - 0.5) * 3f, false);
                }
                x += w + 0.004f;
                run--;
            }
        }

        // ------------------------------------------------------------------ fixtures

        /// <summary>A gondola (free-standing shelving unit), double-sided, filling rect r.</summary>
        static void Gondola(MeshKit k, Transform g, AreaRect r, float y, float height, Material frameMat, Material[] pal, int seed, bool grocery)
        {
            FixtureMaterials();
            var rng = new System.Random(seed);
            RectFrame(r, y, out Vector3 c, out Quaternion rot, out float L, out float D);
            k.Frame(c, rot);
            float hx = L * 0.5f, hz = D * 0.5f;
            k.Box(new Vector3(0f, 0.06f, 0f), new Vector3(L, 0.12f, D - 0.02f), frameMat);                       // plinth
            k.Box(new Vector3(0f, height * 0.5f, 0f), new Vector3(L, height, 0.03f), pegboardMat);                // back panel
            k.Box(new Vector3(-hx + 0.012f, height * 0.5f, 0f), new Vector3(0.025f, height, D), frameMat);        // end panels
            k.Box(new Vector3(hx - 0.012f, height * 0.5f, 0f), new Vector3(0.025f, height, D), frameMat);
            int bays = Mathf.Max(1, Mathf.RoundToInt(L / 1.0f));
            for (int b = 1; b < bays; b++)
                k.Box(new Vector3(-hx + b * L / bays, height * 0.5f, 0f), new Vector3(0.03f, height, 0.05f), frameMat); // uprights
            float[] levels = grocery ? new[] { 0.12f, 0.5f, 0.86f, 1.2f, 1.52f } : new[] { 0.12f, 0.55f, 0.95f, 1.35f };
            for (int s = -1; s <= 1; s += 2)
            {
                float zf = s * (hz - 0.01f), zb = s * 0.03f;
                for (int i = 0; i < levels.Length; i++)
                {
                    float ly = levels[i];
                    if (ly > height - 0.25f) break;
                    float nextY = i + 1 < levels.Length ? Mathf.Min(levels[i + 1], height) : height;
                    if (i > 0)
                    {
                        k.Box(new Vector3(0f, ly - 0.01f, (zf + zb) * 0.5f), new Vector3(L - 0.05f, 0.02f, Mathf.Abs(zf - zb)), frameMat);      // shelf
                        k.Box(new Vector3(0f, ly - 0.02f, zf), new Vector3(L - 0.05f, 0.045f, 0.012f), shelfEdgeMat, 0f, false);                // price rail
                        for (float tx = -hx + 0.3f; tx < hx - 0.1f; tx += 0.35f + (float)rng.NextDouble() * 0.4f)
                            k.Box(new Vector3(tx, ly - 0.02f, zf + s * 0.008f), new Vector3(0.05f, 0.035f, 0.004f), priceTagMat, 0f, false);
                    }
                    float maxH = nextY - ly - 0.04f;
                    // shelves get a little emptier the higher up (and much emptier: nobody restocked tonight)
                    ShelfProducts(k, rng, -hx + 0.03f, hx - 0.03f, ly, zf - s * 0.01f, zb, maxH, pal, grocery ? 1.1f : 1f);
                }
            }
            // a header card on top
            k.Box(new Vector3(0f, height + 0.15f, 0f), new Vector3(Mathf.Min(1.2f, L * 0.6f), 0.28f, 0.02f), pal[rng.Next(pal.Length)], 0f, false);
            FixtureCollider(g, c + Vector3.up * (height * 0.5f), new Vector3(L, height, D), rot, SurfaceType.Metal);
        }

        /// <summary>Shelving against a wall: one-sided, deeper at the bottom, products facing 'face' (local +z).</summary>
        static void WallShelving(MeshKit k, Transform g, Vector3 centre, Quaternion rot, float length, float height, Material[] pal, int seed)
        {
            FixtureMaterials();
            var rng = new System.Random(seed);
            k.Frame(centre, rot);
            float hx = length * 0.5f, depth = 0.45f;
            k.Box(new Vector3(0f, height * 0.5f, 0.015f), new Vector3(length, height, 0.03f), pegboardMat);
            k.Box(new Vector3(0f, 0.06f, depth * 0.5f), new Vector3(length, 0.12f, depth), metal);
            int bays = Mathf.Max(1, Mathf.RoundToInt(length / 1.0f));
            for (int b = 0; b <= bays; b++)
                k.Box(new Vector3(-hx + b * length / bays, height * 0.5f, 0.05f), new Vector3(0.03f, height, 0.06f), metal);
            float[] levels = { 0.12f, 0.6f, 1.05f, 1.5f, 1.95f };
            for (int i = 0; i < levels.Length; i++)
            {
                float ly = levels[i];
                if (ly > height - 0.2f) break;
                float d = Mathf.Lerp(depth, 0.32f, i / 4f);
                if (i > 0)
                {
                    k.Box(new Vector3(0f, ly - 0.01f, d * 0.5f), new Vector3(length - 0.04f, 0.02f, d), metal);
                    k.Box(new Vector3(0f, ly - 0.02f, d), new Vector3(length - 0.04f, 0.045f, 0.012f), shelfEdgeMat, 0f, false);
                }
                float nextY = i + 1 < levels.Length ? Mathf.Min(levels[i + 1], height) : height;
                ShelfProducts(k, rng, -hx + 0.03f, hx - 0.03f, ly, d - 0.01f, 0.04f, nextY - ly - 0.04f, pal);
            }
            FixtureCollider(g, centre + rot * new Vector3(0f, height * 0.5f, depth * 0.5f), new Vector3(length, height, depth), rot, SurfaceType.Metal);
        }

        /// <summary>A clothes rail: chrome frame, garments hanging on black hangers, colours in runs.</summary>
        static void ClothesRail(MeshKit k, Transform g, AreaRect r, float y, int seed)
        {
            FixtureMaterials();
            var rng = new System.Random(seed);
            RectFrame(r, y, out Vector3 c, out Quaternion rot, out float L, out float D);
            k.Frame(c, rot);
            float hx = L * 0.5f - 0.05f, top = 1.35f;
            // frame: two T-legs and the bar
            for (int s = -1; s <= 1; s += 2)
            {
                k.Bar(new Vector3(s * hx, 0.03f, 0f), new Vector3(s * hx, top, 0f), 0.016f, railMat);
                k.Bar(new Vector3(s * hx, 0.03f, -0.28f), new Vector3(s * hx, 0.03f, 0.28f), 0.014f, railMat);
            }
            k.Bar(new Vector3(-hx, top, 0f), new Vector3(hx, top, 0f), 0.013f, railMat);
            // garments
            float x = -hx + 0.08f;
            int run = 0;
            Material m = clothMats[0];
            float glen = 0.7f;
            while (x < hx - 0.08f)
            {
                if (run <= 0)
                {
                    run = 3 + rng.Next(8);
                    m = clothMats[rng.Next(clothMats.Length)];
                    glen = rng.NextDouble() < 0.3 ? 1.0f + (float)rng.NextDouble() * 0.15f : 0.62f + (float)rng.NextDouble() * 0.12f;   // coats / dresses vs tops
                    if (rng.NextDouble() < 0.1) { x += 0.15f; run = 0; continue; }
                }
                float lean = (float)(rng.NextDouble() - 0.5) * 6f;
                k.Box(new Vector3(x, top - 0.05f, 0f), new Vector3(0.012f, 0.012f, 0.4f), hangerMat, 0f, false);                         // hanger
                k.Box(new Vector3(x, top - 0.06f - glen * 0.5f, 0f), new Vector3(0.025f, glen, 0.44f), m, lean, true);                 // garment
                k.Box(new Vector3(x, top - 0.06f - 0.06f, 0f), new Vector3(0.03f, 0.1f, 0.5f), m, lean, false);                          // shoulders
                x += 0.045f + (float)rng.NextDouble() * 0.02f;
                run--;
            }
            FixtureCollider(g, c + Vector3.up * (top * 0.5f), new Vector3(L, top, 0.6f), rot, SurfaceType.Metal);
        }

        /// <summary>A display table with folded stacks (clothes, towels, books).</summary>
        static void DisplayTable(MeshKit k, Transform g, AreaRect r, float y, Material topMat, int seed, bool books)
        {
            FixtureMaterials();
            var rng = new System.Random(seed);
            RectFrame(r, y, out Vector3 c, out Quaternion rot, out float L, out float D);
            k.Frame(c, rot);
            float h = 0.8f;
            k.Box(new Vector3(0f, h - 0.02f, 0f), new Vector3(L, 0.04f, D), topMat);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    k.Box(new Vector3(sx * (L * 0.5f - 0.06f), (h - 0.04f) * 0.5f, sz * (D * 0.5f - 0.06f)), new Vector3(0.05f, h - 0.04f, 0.05f), topMat);
            k.Box(new Vector3(0f, 0.15f, 0f), new Vector3(L - 0.15f, 0.02f, D - 0.15f), topMat);                // undershelf
            int nx = Mathf.Max(1, (int)(L / 0.38f)), nz = Mathf.Max(1, (int)(D / 0.32f));
            Material[] pal = books ? productMats : clothMats;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    if (rng.NextDouble() < 0.15) continue;
                    float px = -L * 0.5f + (i + 0.5f) * L / nx, pz = -D * 0.5f + (j + 0.5f) * D / nz;
                    Material m = pal[rng.Next(pal.Length)];
                    int count = 2 + rng.Next(5);
                    float sy = h;
                    for (int s = 0; s < count; s++)
                    {
                        float th = books ? 0.025f + (float)rng.NextDouble() * 0.03f : 0.035f;
                        Vector3 size = books ? new Vector3(0.17f, th, 0.24f) : new Vector3(0.3f, th, 0.24f);
                        if (books) m = pal[rng.Next(pal.Length)];
                        k.Box(new Vector3(px + (float)(rng.NextDouble() - 0.5) * 0.02f, sy + th * 0.5f, pz + (float)(rng.NextDouble() - 0.5) * 0.02f), size, m, (float)(rng.NextDouble() - 0.5) * 8f, false);
                        sy += th;
                    }
                }
            FixtureCollider(g, c + Vector3.up * (h * 0.5f), new Vector3(L, h, D), rot, SurfaceType.Wood);
        }

        /// <summary>A glass display counter: white cabinet, glass case on top with small things inside.</summary>
        static void GlassCounter(MeshKit k, Transform g, AreaRect r, float y, Material accent, int seed)
        {
            FixtureMaterials();
            var rng = new System.Random(seed);
            RectFrame(r, y, out Vector3 c, out Quaternion rot, out float L, out float D);
            k.Frame(c, rot);
            k.Box(new Vector3(0f, 0.45f, 0f), new Vector3(L, 0.9f, D), whiteFixture);
            k.Box(new Vector3(0f, 0.06f, 0f), new Vector3(L + 0.02f, 0.12f, D + 0.02f), accent);
            k.Box(new Vector3(0f, 0.905f, 0f), new Vector3(L, 0.01f, D), blackMat);
            // the glass case (separate object: transparent)
            for (int i = 0; i < (int)(L / 0.12f); i++)
            {
                if (rng.NextDouble() < 0.3) continue;
                float px = -L * 0.5f + 0.08f + i * 0.12f;
                Material m = productMats[rng.Next(productMats.Length)];
                float hh = 0.05f + (float)rng.NextDouble() * 0.12f;
                k.Bar(new Vector3(px, 0.91f, (float)(rng.NextDouble() - 0.5) * D * 0.5f), new Vector3(px, 0.91f + hh, (float)(rng.NextDouble() - 0.5) * D * 0.5f), 0.022f, m, 6);
            }
            Vector3 gc = c + Vector3.up * 1.08f;
            GameObject glass = EditorBuild.Box("Counter glass", g, gc, new Vector3(L - 0.02f, 0.34f, D - 0.02f), glassMat, SurfaceType.Glass, rot);
            Object.DestroyImmediate(glass.GetComponent<Collider>());
            FixtureCollider(g, c + Vector3.up * 0.62f, new Vector3(L, 1.25f, D), rot, SurfaceType.Glass);
        }

        /// <summary>The till point: counter top kit - screen on a stand, card machine, receipt printer, carrier bags.</summary>
        static void TillPoint(MeshKit k, Vector3 counterTop, Quaternion faceCustomer, int seed)
        {
            FixtureMaterials();
            var rng = new System.Random(seed);
            k.Frame(counterTop, faceCustomer);
            k.Box(new Vector3(0f, 0.03f, 0.05f), new Vector3(0.2f, 0.06f, 0.2f), blackMat);                     // base / cash drawer
            k.Bar(new Vector3(0f, 0.06f, 0f), new Vector3(0f, 0.3f, -0.02f), 0.015f, blackMat, 6);             // stand
            k.Box(new Vector3(0f, 0.38f, -0.03f), new Vector3(0.36f, 0.24f, 0.03f), blackMat, 0f, true, 0f);    // screen
            k.Box(new Vector3(0.3f, 0.04f, 0.12f), new Vector3(0.08f, 0.04f, 0.16f), blackMat, 15f);            // card machine
            k.Box(new Vector3(0.3f, 0.065f, 0.15f), new Vector3(0.06f, 0.005f, 0.05f), shelfEdgeMat, 15f, false);
            k.Box(new Vector3(-0.3f, 0.06f, 0.0f), new Vector3(0.14f, 0.12f, 0.18f), whiteFixture);              // receipt printer
            int bags = 3 + rng.Next(8);
            for (int i = 0; i < bags; i++)                                                                        // a stack of folded carrier bags
                k.Box(new Vector3(-0.6f, 0.004f + i * 0.006f, 0.05f), new Vector3(0.32f, 0.005f, 0.42f), productMats[11], (float)(rng.NextDouble() - 0.5) * 6f, false);
        }
    }
}
