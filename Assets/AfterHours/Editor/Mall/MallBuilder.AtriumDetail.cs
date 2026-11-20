using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// The atrium, dressed: a proper fountain (coping, tiered bowls, nozzles, coins in the water), an inlaid tile ring
    /// round it, tall sale banners hanging into the void - and the Christmas installation the crew left half done:
    /// a big tree, half its baubles still in the boxes, a step ladder against it, rope barriers, and one string of
    /// fairy lights somebody left switched on.
    /// </summary>
    public static partial class MallBuilder
    {
        static Mesh coneMesh;

        /// <summary>A closed cone (base radius 1, height 1, base at y=0), shared by every cone.</summary>
        static Mesh ConeMesh()
        {
            if (coneMesh != null) return coneMesh;
            const int n = 20;
            var v = new System.Collections.Generic.List<Vector3>();
            var t = new System.Collections.Generic.List<int>();
            var nr = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                float a0 = i / (float)n * Mathf.PI * 2f, a1 = (i + 1) / (float)n * Mathf.PI * 2f, am = (a0 + a1) * 0.5f;
                Vector3 p0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)), p1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                Vector3 side = new Vector3(Mathf.Sin(am), 0.7f, Mathf.Cos(am)).normalized;
                int b = v.Count;
                v.Add(p1); v.Add(Vector3.up); v.Add(p0);          // (clockwise seen from outside: Unity's front faces)
                nr.Add(side); nr.Add(side); nr.Add(side);
                t.Add(b); t.Add(b + 1); t.Add(b + 2);
                b = v.Count;
                v.Add(p0); v.Add(Vector3.zero); v.Add(p1);
                nr.Add(Vector3.down); nr.Add(Vector3.down); nr.Add(Vector3.down);
                t.Add(b); t.Add(b + 1); t.Add(b + 2);
            }
            coneMesh = new Mesh { name = "Cone" };
            coneMesh.SetVertices(v); coneMesh.SetNormals(nr); coneMesh.SetTriangles(t, 0);
            coneMesh.RecalculateBounds();
            return coneMesh;
        }

        static GameObject Cone(string name, Transform parent, Vector3 baseCentre, float radius, float height, Material m)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = baseCentre;
            go.transform.localScale = new Vector3(radius, height, radius);
            go.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        static GameObject Ball(string name, Transform parent, Vector3 centre, float diameter, Material m, bool shadows = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = Vector3.one * diameter;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            go.isStatic = true;
            return go;
        }

        static void BuildAtriumDetail(Transform parent)
        {
            Transform g = Group("Atrium detail", parent);
            var rng = new System.Random(4242);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // ---------------- the fountain, finished properly ----------------
            {
                const float r = 4f;
                float side = 2f * r * Mathf.Tan(22.5f * Mathf.Deg2Rad);
                Material coping = ArtLibrary.Surface("Fountain_Coping", "concrete", 1f, new Color(0.78f, 0.76f, 0.72f), 1.2f, 1f);
                Material brass = ArtLibrary.Plain("Brass_Dull", new Color(0.55f, 0.45f, 0.25f), 0.65f, 1f);
                Material coinGold = ArtLibrary.Plain("Coin_Gold", new Color(0.75f, 0.6f, 0.25f), 0.8f, 1f);
                Material coinSilver = ArtLibrary.Plain("Coin_Silver", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f);
                for (int i = 0; i < 8; i++)
                {
                    Quaternion rot = Quaternion.Euler(0f, i * 45f, 0f);
                    // a wide stone lip to sit on, overhanging the basin wall both sides
                    EditorBuild.Box("Fountain coping", g, rot * new Vector3(0f, 0.585f, r - 0.2f), new Vector3(side + 0.1f, 0.07f, 0.56f), coping, SurfaceType.Concrete, rot);
                    // a brass nozzle on each face, aimed at the centre
                    Vector3 nz = rot * new Vector3(0f, 0.5f, r - 0.42f);
                    Cyl("Fountain nozzle", g, nz, nz + rot * new Vector3(0f, 0.04f, -0.12f), 0.05f, brass);
                }
                // three tiers of bowls on the column, dry now
                for (int k = 0; k < 3; k++)
                {
                    float y = 1.2f + k * 0.85f, rad = 1.5f - k * 0.45f;
                    Cyl("Fountain bowl", g, new Vector3(0f, y, 0f), new Vector3(0f, y + 0.12f, 0f), rad * 2f, coping);
                    Cyl("Fountain bowl lip", g, new Vector3(0f, y + 0.12f, 0f), new Vector3(0f, y + 0.18f, 0f), rad * 2f + 0.06f, coping);
                }
                Ball("Fountain finial", g, new Vector3(0f, 3.35f, 0f), 0.32f, brass, true);
                // coins on the bottom of the basin (people still throw them)
                for (int i = 0; i < 70; i++)
                {
                    float a = R(0f, Mathf.PI * 2f), d = Mathf.Sqrt(R(0.06f, 1f)) * (r - 0.75f);
                    if (d < 0.75f) d = 0.75f + R(0f, 0.3f);
                    Vector3 c = new Vector3(Mathf.Sin(a) * d, 0.255f, Mathf.Cos(a) * d);
                    GameObject coin = Cyl("Coin", g, c, c + new Vector3(R(-0.002f, 0.002f), 0.003f, 0f), R(0.018f, 0.028f), rng.NextDouble() < 0.35 ? coinGold : coinSilver);
                    coin.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                Sign(g, "PLEASE DO NOT\nCLIMB ON THE FOUNTAIN", new Vector3(0f, 0.75f, -r - 0.02f), Vector3.forward, 0.9f, 0.012f);
            }

            // ---------------- inlaid tile ring round the fountain, and a compass rose on the south approach ----------------
            {
                Material inlayDark = ArtLibrary.Plain("Floor_Inlay_Dark", new Color(0.18f, 0.2f, 0.22f), 0.85f);
                Material inlayBrass = ArtLibrary.Plain("Floor_Inlay_Brass", new Color(0.62f, 0.5f, 0.28f), 0.75f, 1f);
                const int seg = 32;
                for (int i = 0; i < seg; i++)
                {
                    float a = (i + 0.5f) / seg * 360f;
                    Quaternion rot = Quaternion.Euler(0f, a, 0f);
                    float len = 2f * Mathf.PI * 5.4f / seg;
                    Detail("Floor inlay ring", g, rot * new Vector3(0f, 0.004f, 5.4f), new Vector3(len + 0.02f, 0.006f, 0.6f), inlayDark, rot);
                    Detail("Floor inlay band", g, rot * new Vector3(0f, 0.006f, 5.75f), new Vector3(len + 0.02f, 0.006f, 0.05f), inlayBrass, rot);
                    Detail("Floor inlay band", g, rot * new Vector3(0f, 0.006f, 5.05f), new Vector3(len + 0.02f, 0.006f, 0.05f), inlayBrass, rot);
                }
                Vector3 cr = new Vector3(0f, 0.005f, -11.5f);
                for (int i = 0; i < 8; i++)
                {
                    Quaternion rot = Quaternion.Euler(0f, i * 45f, 0f);
                    float l = i % 2 == 0 ? 1.4f : 0.8f;
                    Detail("Compass point", g, cr + rot * new Vector3(0f, 0f, l * 0.5f), new Vector3(0.16f, 0.006f, l), i % 2 == 0 ? inlayBrass : inlayDark, rot);
                }
                Cyl("Compass centre", g, cr, cr + Vector3.up * 0.006f, 0.3f, inlayBrass);
            }

            // ---------------- sale banners hanging into the void ----------------
            {
                Material red = ArtLibrary.Plain("Banner_Red", new Color(0.55f, 0.07f, 0.08f), 0.2f);
                Material cream = ArtLibrary.Plain("Banner_Cream", new Color(0.86f, 0.83f, 0.74f), 0.2f);
                string[] words = { "AUTUMN\nSALE\n\nUP TO\n50%\nOFF", "LATE\nNIGHT\nSHOPPING\n\nTHURSDAYS\nTILL 9", "KESTREL\nFIELDS\n\nSHOP\nEAT\nMEET", "AUTUMN\nSALE\n\nUP TO\n50%\nOFF" };
                Vector3[] at = { new Vector3(-9.6f, 0f, -4f), new Vector3(9.6f, 0f, 4f), new Vector3(4f, 0f, -9.6f), new Vector3(-4f, 0f, 9.6f) };
                for (int i = 0; i < 4; i++)
                {
                    Vector3 p = at[i];
                    Vector3 inward = (new Vector3(-p.x, 0f, -p.z)).normalized;
                    inward = Mathf.Abs(inward.x) > Mathf.Abs(inward.z) ? new Vector3(Mathf.Sign(inward.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(inward.z));
                    Quaternion face = Quaternion.LookRotation(inward);
                    float top = MallLayout.RoofY - 0.5f, bottom = top - 6.5f;
                    Vector3 c = new Vector3(p.x, (top + bottom) * 0.5f, p.z);
                    GameObject cloth = EditorBuild.Box("Banner", g, c, new Vector3(1.6f, top - bottom, 0.02f), i % 2 == 0 ? red : cream, SurfaceType.Wood, face);
                    Object.DestroyImmediate(cloth.GetComponent<Collider>());
                    Cyl("Banner pole", g, c + Vector3.up * ((top - bottom) * 0.5f + 0.03f) - face * Vector3.right * 0.9f, c + Vector3.up * ((top - bottom) * 0.5f + 0.03f) + face * Vector3.right * 0.9f, 0.05f, metal);
                    Cyl("Banner pole", g, c - Vector3.up * ((top - bottom) * 0.5f + 0.03f) - face * Vector3.right * 0.85f, c - Vector3.up * ((top - bottom) * 0.5f + 0.03f) + face * Vector3.right * 0.85f, 0.04f, metal);
                    Decor("Banner wire", g, c + Vector3.up * ((top - bottom) * 0.5f) - new Vector3(0.006f, 0f, 0.006f), c + new Vector3(0.006f, MallLayout.RoofY - c.y, 0.006f), metal);
                    float yaw = Mathf.Atan2(-inward.x, -inward.z) * Mathf.Rad2Deg;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 view = inward * -s;     // text on both faces
                        TextMesh tm = EditorBuild.Label(g, words[i], c + inward * (0.012f * s) + Vector3.up * 0.3f, Mathf.Atan2(view.x, view.z) * Mathf.Rad2Deg, 0.075f);
                        tm.color = i % 2 == 0 ? new Color(0.95f, 0.9f, 0.8f) : new Color(0.45f, 0.08f, 0.08f);
                    }
                }
            }

            // ---------------- the Christmas tree, half put up ----------------
            {
                Transform tree = Group("Christmas installation (half done)", g);
                Vector3 tc = new Vector3(-5.6f, 0f, 5.6f);
                Material needles = ArtLibrary.Plain("Tree_Needles", new Color(0.07f, 0.17f, 0.09f), 0.15f);
                Material needlesDark = ArtLibrary.Plain("Tree_Needles_Dark", new Color(0.05f, 0.12f, 0.07f), 0.1f);
                Material trunk = ArtLibrary.Plain("Tree_Trunk_Brown", new Color(0.18f, 0.12f, 0.08f), 0.2f);
                Material skirt = ArtLibrary.Plain("Tree_Skirt", new Color(0.45f, 0.06f, 0.06f), 0.3f);
                Material[] baubles =
                {
                    ArtLibrary.Plain("Bauble_Red", new Color(0.6f, 0.05f, 0.05f), 0.9f, 0.6f),
                    ArtLibrary.Plain("Bauble_Gold", new Color(0.75f, 0.58f, 0.2f), 0.9f, 1f),
                    ArtLibrary.Plain("Bauble_Silver", new Color(0.75f, 0.76f, 0.8f), 0.9f, 1f),
                };
                Material bulb = ArtLibrary.Plain("Fairy_Light", new Color(1f, 0.8f, 0.45f), 0.5f, 0f, new Color(2.6f, 1.7f, 0.6f), false);
                Material bulbOff = ArtLibrary.Plain("Fairy_Light_Off", new Color(0.35f, 0.3f, 0.2f), 0.5f);
                Material cardboard = ArtLibrary.Plain("Cardboard", new Color(0.55f, 0.42f, 0.28f), 0.15f);
                Material rope = ArtLibrary.Plain("Barrier_Rope", new Color(0.45f, 0.05f, 0.08f), 0.3f);

                // base, trunk, tiers (each a little rotated so it isn't a perfect cone stack)
                Cyl("Tree stand", tree, tc, tc + Vector3.up * 0.35f, 1.0f, blackMat, true);
                Cyl("Tree skirt", tree, tc + Vector3.up * 0.01f, tc + Vector3.up * 0.03f, 2.6f, skirt);
                Cyl("Tree trunk", tree, tc, tc + Vector3.up * 1.0f, 0.3f, trunk);
                float[] tierY = { 0.7f, 1.9f, 3.0f, 4.0f, 4.9f, 5.7f };
                float[] tierR = { 1.9f, 1.6f, 1.3f, 1.0f, 0.72f, 0.45f };
                for (int k = 0; k < tierY.Length; k++)
                {
                    GameObject c = Cone("Tree tier", tree, tc + Vector3.up * tierY[k], tierR[k], 1.7f - k * 0.12f, k % 2 == 0 ? needles : needlesDark);
                    c.transform.localRotation = Quaternion.Euler(0f, k * 23f, 0f);
                }
                BoxMinMax("Tree base collider", tree, tc + new Vector3(-0.9f, 0f, -0.9f), tc + new Vector3(0.9f, 2.4f, 0.9f), needles, SurfaceType.Wood).GetComponent<Renderer>().enabled = false;
                // the star, never put on: lying in its box. A bare spike at the top.
                Cyl("Tree top spike", tree, tc + Vector3.up * 7.1f, tc + Vector3.up * 7.45f, 0.05f, trunk);
                // baubles: only the side the crew reached before they stopped (the lower half, the side facing the ladder)
                for (int i = 0; i < 46; i++)
                {
                    float y = R(0.9f, 4.2f);
                    float k = Mathf.InverseLerp(0.7f, 7.4f, y);
                    float rad = Mathf.Lerp(1.9f, 0.1f, k) * R(0.55f, 0.85f);
                    float a = R(-110f, 110f) * Mathf.Deg2Rad + Mathf.PI * 0.25f;
                    Ball("Bauble", tree, tc + new Vector3(Mathf.Sin(a) * rad, y - 0.1f, Mathf.Cos(a) * rad), R(0.09f, 0.14f), baubles[rng.Next(baubles.Length)], true);
                }
                // one string of fairy lights, spiralling up - left switched on (it glows; part of it has failed)
                Transform lights = Group("Fairy lights (left on)", tree);
                for (int i = 0; i < 120; i++)
                {
                    float u = i / 120f;
                    float y = Mathf.Lerp(0.9f, 6.8f, u);
                    float rad = Mathf.Lerp(1.75f, 0.2f, Mathf.InverseLerp(0.7f, 7.4f, y)) + 0.02f;
                    float a = u * Mathf.PI * 2f * 7f;
                    bool dead = u > 0.62f && u < 0.78f;
                    Ball("Fairy light", lights, tc + new Vector3(Mathf.Sin(a) * rad, y, Mathf.Cos(a) * rad), 0.035f, dead ? bulbOff : bulb);
                }
                var glowGo = new GameObject("Fairy light glow");
                glowGo.transform.SetParent(tree, false);
                glowGo.transform.localPosition = tc + Vector3.up * 2.6f;
                Light glow = glowGo.AddComponent<Light>();
                glow.type = LightType.Point; glow.range = 6f; glow.intensity = 0.9f; glow.color = new Color(1f, 0.72f, 0.38f);
                glow.shadows = LightShadows.None; glow.lightmapBakeType = LightmapBakeType.Realtime;

                // the step ladder they were using, still open against the tree
                {
                    Vector3 lb = tc + new Vector3(1.9f, 0f, -1.3f);
                    Quaternion lr = Quaternion.Euler(0f, -55f, 0f);
                    Vector3 Lp(float x, float y, float z) => lb + lr * new Vector3(x, y, z);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Cyl("Ladder stile", tree, Lp(s * 0.22f, 0f, -0.35f), Lp(s * 0.2f, 2.0f, 0f), 0.045f, metal, true);
                        Cyl("Ladder back leg", tree, Lp(s * 0.22f, 0f, 0.4f), Lp(s * 0.2f, 1.95f, 0.03f), 0.04f, metal);
                    }
                    for (int k = 1; k <= 5; k++)
                    {
                        float f = k / 6f;
                        Vector3 a = Lp(-0.22f, 0f, -0.35f) + (Lp(-0.2f, 2.0f, 0f) - Lp(-0.22f, 0f, -0.35f)) * f;
                        Vector3 b = Lp(0.22f, 0f, -0.35f) + (Lp(0.2f, 2.0f, 0f) - Lp(0.22f, 0f, -0.35f)) * f;
                        Detail("Ladder tread", tree, (a + b) * 0.5f, new Vector3(0.44f, 0.03f, 0.12f), metal, lr);
                    }
                    Detail("Ladder top", tree, Lp(0f, 2.02f, 0.01f), new Vector3(0.48f, 0.05f, 0.22f), blackMat, lr);
                }

                // boxes of decorations, some open, baubles spilling
                Vector3[] boxes = { tc + new Vector3(2.4f, 0f, 0.6f), tc + new Vector3(2.8f, 0f, -0.2f), tc + new Vector3(2.5f, 0.45f, 0.4f), tc + new Vector3(-0.4f, 0f, -2.6f) };
                for (int i = 0; i < boxes.Length; i++)
                {
                    Quaternion br = Quaternion.Euler(0f, R(-25f, 25f), 0f);
                    Vector3 bc = boxes[i];
                    EditorBuild.Box("Decorations box", tree, bc + Vector3.up * 0.22f, new Vector3(0.6f, 0.44f, 0.42f), cardboard, SurfaceType.Wood, br);
                    EditorBuild.Label(tree, "XMAS DECS\nATRIUM", bc + Vector3.up * 0.25f + br * new Vector3(0f, 0f, -0.215f), br.eulerAngles.y, 0.012f, false).color = new Color(0.1f, 0.1f, 0.1f);
                    if (i % 2 == 0)
                        for (int k = 0; k < 7; k++) Ball("Bauble (in box)", tree, bc + br * new Vector3(R(-0.2f, 0.2f), 0.44f + R(0f, 0.05f), R(-0.13f, 0.13f)), 0.11f, baubles[rng.Next(baubles.Length)]);
                }
                // the star, in an open box on the floor
                {
                    Vector3 sb = tc + new Vector3(-0.9f, 0f, -2.7f);
                    EditorBuild.Box("Star box", tree, sb + Vector3.up * 0.06f, new Vector3(0.6f, 0.12f, 0.6f), cardboard, SurfaceType.Wood);
                    Material star = ArtLibrary.Plain("Tree_Star", new Color(0.8f, 0.65f, 0.25f), 0.85f, 1f);
                    for (int k = 0; k < 5; k++)
                        Detail("Tree star point", tree, sb + new Vector3(0f, 0.15f, 0f) + Quaternion.Euler(0f, k * 72f, 0f) * new Vector3(0f, 0f, 0.13f), new Vector3(0.08f, 0.03f, 0.26f), star, Quaternion.Euler(0f, k * 72f, 0f));
                }
                // tinsel left in a heap, and loose baubles that rolled away (one smashed)
                for (int i = 0; i < 9; i++) Ball("Loose bauble", tree, tc + new Vector3(R(-2.5f, 3.4f), 0.05f, R(-3.2f, -1.8f)), 0.1f, baubles[rng.Next(baubles.Length)]);
                for (int i = 0; i < 12; i++)   // one smashed: thin curved shards
                    Detail("Bauble shard", tree, tc + new Vector3(1.2f + R(-0.25f, 0.25f), 0.006f, -2.9f + R(-0.25f, 0.25f)), new Vector3(R(0.02f, 0.05f), 0.004f, R(0.02f, 0.04f)), baubles[0], Quaternion.Euler(0f, R(0f, 360f), 0f));
                // rope barriers round it (posts + a sagging rope), a gap where the crew came and went
                int posts = 10;
                for (int i = 0; i < posts; i++)
                {
                    float a = i / (float)posts * Mathf.PI * 2f;
                    Vector3 p = tc + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 3.4f;
                    Cyl("Barrier post", tree, p, p + Vector3.up * 0.95f, 0.06f, ArtLibrary.Plain("Brass_Post", new Color(0.65f, 0.52f, 0.28f), 0.85f, 1f), true);
                    Cyl("Barrier base", tree, p, p + Vector3.up * 0.03f, 0.32f, blackMat);
                    if (i == 7) continue;                 // the gap
                    float a2 = (i + 1) / (float)posts * Mathf.PI * 2f;
                    Vector3 q = tc + new Vector3(Mathf.Sin(a2), 0f, Mathf.Cos(a2)) * 3.4f;
                    Vector3 mid = (p + q) * 0.5f + Vector3.up * 0.78f;
                    Cyl("Barrier rope", tree, p + Vector3.up * 0.9f, mid, 0.035f, rope);
                    Cyl("Barrier rope", tree, mid, q + Vector3.up * 0.9f, 0.035f, rope);
                }
                Sign(tree, "SANTA'S GROTTO\nOPENS SATURDAY", tc + new Vector3(3.6f, 1.3f, 1.9f), (new Vector3(1f, 0f, 0.5f)).normalized, 1.2f, 0.022f);
            }
        }
    }
}
