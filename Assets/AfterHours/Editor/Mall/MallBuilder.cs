using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using AfterHours.Audio;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Menu: After Hours > 3 - Build Mall.
    /// Builds the complete fixed mall layout (blockout: real geometry and collision, simple materials)
    /// from <see cref="MallLayout"/>. Part 4 replaces the simple materials with the realistic look.
    /// The work is split over several files (MallBuilder.*.cs).
    /// </summary>
    public static partial class MallBuilder
    {
        public const string ScenePath = EditorBuild.Root + "/Scenes/Mall.unity";
        const string MatFolder = EditorBuild.Root + "/Materials/Blockout";

        static Material floorTile, floorCarpet, concrete, wallPaint, wallService, ceilingMat, metal, shutterMat, glassMat,
            darkGlass, signMat, railMat, woodMat, counterMat, fixtureMat, carParkFloor, waterMat, screenMat, seatMat,
            plantMat, toiletMat, liftDoorMat, propMat, lineMat;
        static readonly Dictionary<UnitCategory, Material> categoryMats = new Dictionary<UnitCategory, Material>();

        [MenuItem("After Hours/3 - Build Mall", priority = 3)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("After Hours", "Stop Play mode first (press the Play button again), then try again.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                Progress("Preparing", 0f);
                ProjectSetup.EnsureLayers();
                EditorBuild.EnsureFolder(EditorBuild.Root + "/Scenes");
                CreateMaterials();

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SetupLighting();

                Transform mall = Group("Mall", null);

                Progress("Floor plates and ceilings", 0.05f);
                BuildFloorPlates(Group("Floor Plates", mall));

                Progress("Atrium", 0.15f);
                BuildAtrium(Group("Atrium", mall));

                Progress("Shopping wings", 0.25f);
                Transform wings = Group("Wings", mall);
                foreach (MallFloor f in MallLayout.ShopFloors)
                {
                    foreach (WingId w in MallLayout.Wings)
                    {
                        BuildWing(Group($"{MallLayout.FloorCode(f)} {w} wing", wings), f, w);
                    }
                }

                Progress("Anchor stores", 0.5f);
                BuildAnchors(Group("Anchors", mall));

                Progress("Back of house", 0.6f);
                BuildQuadrants(Group("Back Of House", mall));

                Progress("Stairwells and lifts", 0.7f);
                BuildCores(Group("Stair Cores", mall));
                BuildLiftShafts(Group("Lift Shafts", mall));

                Progress("Car park", 0.8f);
                BuildCarPark(Group("Car Park", mall));

                Progress("Roof", 0.9f);
                BuildRoof(Group("Roof", mall));

                Progress("Escape routes and directory", 0.95f);
                BuildEscapeMarkers(Group("Escape Routes", mall));
                DirectoryBuilder.Build(Group("Directory", mall));

                new GameObject("Noise Debug Gizmos").AddComponent<NoiseDebugGizmos>();
                DevLocation start = MallLayout.DevLocations[0];
                GameObject player = PlayerFactory.CreatePlayer(start.position, start.yaw);

                EditorSceneManager.SaveScene(scene, ScenePath);
                AddSceneFirstInBuild(ScenePath);
                Selection.activeGameObject = player;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.DisplayDialog("After Hours",
                "Mall built and saved to\n" + ScenePath +
                "\n\nPress Play to explore. Esc > Teleport (dev) jumps around the building.", "OK");
        }

        static void Progress(string what, float t)
        {
            EditorUtility.DisplayProgressBar("After Hours - building the mall", what, t);
        }

        // =====================================================================
        // Setup
        // =====================================================================

        static void CreateMaterials()
        {
            floorTile = Mat("FloorTile", new Color(0.66f, 0.64f, 0.58f), 0.55f);
            floorCarpet = Mat("Carpet", new Color(0.3f, 0.2f, 0.22f), 0.05f);
            concrete = Mat("Concrete", new Color(0.46f, 0.46f, 0.45f), 0.12f);
            wallPaint = Mat("WallPaint", new Color(0.78f, 0.75f, 0.68f), 0.15f);
            wallService = Mat("WallService", new Color(0.6f, 0.62f, 0.6f), 0.1f);
            ceilingMat = Mat("Ceiling", new Color(0.72f, 0.72f, 0.7f), 0.05f);
            metal = Mat("Metal", new Color(0.55f, 0.56f, 0.58f), 0.55f, 0.8f);
            shutterMat = Mat("Shutter", new Color(0.5f, 0.52f, 0.54f), 0.45f, 0.7f);
            glassMat = TransparentMat("Glass", new Color(0.55f, 0.65f, 0.7f, 0.22f));
            darkGlass = Mat("DarkGlass", new Color(0.03f, 0.035f, 0.04f), 0.95f, 0.2f);
            signMat = Mat("SignPanel", new Color(0.12f, 0.12f, 0.14f), 0.5f);
            railMat = Mat("Railing", new Color(0.7f, 0.7f, 0.72f), 0.7f, 0.9f);
            woodMat = Mat("Wood", new Color(0.42f, 0.29f, 0.18f), 0.3f);
            counterMat = Mat("Counter", new Color(0.55f, 0.5f, 0.45f), 0.4f);
            fixtureMat = EditorBuild.Material(MatFolder, "LightFixture", Color.white, 0.5f, 0f, new Color(2.2f, 2.3f, 2.4f));
            carParkFloor = Mat("CarParkFloor", new Color(0.36f, 0.36f, 0.35f), 0.2f);
            waterMat = Mat("Water", new Color(0.12f, 0.2f, 0.24f), 0.95f);
            screenMat = Mat("CinemaScreen", new Color(0.85f, 0.85f, 0.85f), 0.1f);
            seatMat = Mat("Seat", new Color(0.35f, 0.08f, 0.1f), 0.2f);
            plantMat = Mat("Machinery", new Color(0.35f, 0.4f, 0.38f), 0.4f, 0.6f);
            toiletMat = Mat("ToiletTile", new Color(0.8f, 0.82f, 0.8f), 0.6f);
            liftDoorMat = Mat("LiftDoor", new Color(0.6f, 0.6f, 0.62f), 0.7f, 0.9f);
            propMat = Mat("Prop", new Color(0.5f, 0.42f, 0.3f), 0.25f);
            lineMat = Mat("PaintLine", new Color(0.8f, 0.75f, 0.3f), 0.2f);

            categoryMats.Clear();
            void C(UnitCategory c, Color col) => categoryMats[c] = Mat("Cat_" + c, col, 0.35f);
            C(UnitCategory.Fashion, new Color(0.55f, 0.35f, 0.45f));
            C(UnitCategory.Footwear, new Color(0.5f, 0.38f, 0.3f));
            C(UnitCategory.Electronics, new Color(0.3f, 0.4f, 0.55f));
            C(UnitCategory.Department, new Color(0.45f, 0.3f, 0.3f));
            C(UnitCategory.Supermarket, new Color(0.3f, 0.5f, 0.32f));
            C(UnitCategory.Food, new Color(0.62f, 0.45f, 0.25f));
            C(UnitCategory.Restaurant, new Color(0.6f, 0.32f, 0.22f));
            C(UnitCategory.Cafe, new Color(0.5f, 0.38f, 0.28f));
            C(UnitCategory.Services, new Color(0.4f, 0.42f, 0.45f));
            C(UnitCategory.Health, new Color(0.35f, 0.55f, 0.5f));
            C(UnitCategory.Books, new Color(0.45f, 0.4f, 0.3f));
            C(UnitCategory.Toys, new Color(0.62f, 0.5f, 0.25f));
            C(UnitCategory.Jewellery, new Color(0.6f, 0.55f, 0.35f));
            C(UnitCategory.Home, new Color(0.45f, 0.48f, 0.38f));
            C(UnitCategory.Sports, new Color(0.3f, 0.45f, 0.48f));
            C(UnitCategory.Music, new Color(0.42f, 0.3f, 0.5f));
            C(UnitCategory.Leisure, new Color(0.5f, 0.3f, 0.55f));
            C(UnitCategory.Cinema, new Color(0.25f, 0.2f, 0.35f));
            C(UnitCategory.Vacant, new Color(0.35f, 0.35f, 0.35f));
        }

        static Material Mat(string name, Color c, float smooth, float metallic = 0f)
        {
            return EditorBuild.Material(MatFolder, name, c, smooth, metallic);
        }

        /// <summary>See-through material (URP Lit, transparent surface). Falls back to opaque if not URP.</summary>
        static Material TransparentMat(string name, Color c)
        {
            Material m = EditorBuild.Material(MatFolder, name, c, 0.95f, 0f);
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                EditorUtility.SetDirty(m);
            }
            return m;
        }

        static void SetupLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.13f, 0.135f, 0.14f); // blockout: readable; Part 4 makes it dark
            RenderSettings.fog = false;
        }

        static void AddSceneFirstInBuild(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == path);
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // =====================================================================
        // Geometry helpers
        // =====================================================================

        static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go.transform;
        }

        static GameObject BoxMinMax(string name, Transform parent, Vector3 min, Vector3 max, Material m, SurfaceType s, bool isStatic = true)
        {
            Vector3 lo = Vector3.Min(min, max);
            Vector3 hi = Vector3.Max(min, max);
            return EditorBuild.Box(name, parent, (lo + hi) * 0.5f, hi - lo, m, s, null, isStatic);
        }

        static GameObject BoxRect(string name, Transform parent, AreaRect r, float y0, float y1, Material m, SurfaceType s, bool isStatic = true)
        {
            return BoxMinMax(name, parent, new Vector3(r.minX, y0, r.minZ), new Vector3(r.maxX, y1, r.maxZ), m, s, isStatic);
        }

        static GameObject Decor(string name, Transform parent, Vector3 min, Vector3 max, Material m)
        {
            GameObject go = BoxMinMax(name, parent, min, max, m, SurfaceType.Metal);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>
        /// A horizontal slab covering 'region' except the 'holes' (atrium void, shafts...). The remaining area
        /// is cut into as few rectangles as possible, each with an exactly fitting box collider.
        /// </summary>
        static void Slab(string name, Transform parent, AreaRect region, IList<AreaRect> holes, float topY, float thickness, Material m, SurfaceType s)
        {
            var clipped = new List<AreaRect>();
            var xs = new List<float> { region.minX, region.maxX };
            var zs = new List<float> { region.minZ, region.maxZ };
            if (holes != null)
            {
                foreach (AreaRect h in holes)
                {
                    if (!h.Overlaps(region)) continue;
                    var c = new AreaRect(Mathf.Max(h.minX, region.minX), Mathf.Max(h.minZ, region.minZ),
                                         Mathf.Min(h.maxX, region.maxX), Mathf.Min(h.maxZ, region.maxZ));
                    if (c.Width < 0.001f || c.Depth < 0.001f) continue;
                    clipped.Add(c);
                    xs.Add(c.minX); xs.Add(c.maxX);
                    zs.Add(c.minZ); zs.Add(c.maxZ);
                }
            }
            xs = SortedUnique(xs);
            zs = SortedUnique(zs);

            for (int j = 0; j < zs.Count - 1; j++)
            {
                int runStart = -1;
                for (int i = 0; i <= xs.Count - 1; i++)
                {
                    bool solid = false;
                    if (i < xs.Count - 1)
                    {
                        float cx = (xs[i] + xs[i + 1]) * 0.5f;
                        float cz = (zs[j] + zs[j + 1]) * 0.5f;
                        solid = true;
                        foreach (AreaRect h in clipped)
                        {
                            if (h.Contains(cx, cz)) { solid = false; break; }
                        }
                    }
                    if (solid && runStart < 0) runStart = i;
                    if (!solid && runStart >= 0)
                    {
                        BoxMinMax(name, parent, new Vector3(xs[runStart], topY - thickness, zs[j]), new Vector3(xs[i], topY, zs[j + 1]), m, s);
                        runStart = -1;
                    }
                }
            }
        }

        static List<float> SortedUnique(List<float> values)
        {
            values.Sort();
            var result = new List<float>();
            foreach (float v in values)
            {
                if (result.Count == 0 || v - result[result.Count - 1] > 0.001f) result.Add(v);
            }
            return result;
        }

        struct Opening
        {
            public float s0, s1, top;
            public Opening(float s0, float s1, float top)
            {
                this.s0 = Mathf.Min(s0, s1);
                this.s1 = Mathf.Max(s0, s1);
                this.top = top;
            }
        }

        /// <summary>
        /// A straight, axis-aligned wall from p0 to p1 (its centre line), y0..y1 high, with doorway/window openings.
        /// Opening positions are distances from p0 along the wall; 'top' is the opening height above y0.
        /// </summary>
        static void WallSpan(string name, Transform parent, Vector3 p0, Vector3 p1, float thickness, float y0, float y1,
                             List<Opening> openings, Material m, SurfaceType s)
        {
            Vector3 d = p1 - p0;
            d.y = 0f;
            float length = d.magnitude;
            if (length < 0.01f) return;
            Vector3 dir = d / length;
            bool alongX = Mathf.Abs(dir.x) > Mathf.Abs(dir.z);

            var list = openings != null ? new List<Opening>(openings) : new List<Opening>();
            list.Sort((a, b) => a.s0.CompareTo(b.s0));

            void Segment(float a, float b, float ya, float yb)
            {
                if (b - a < 0.005f || yb - ya < 0.005f) return;
                Vector3 c = p0 + dir * ((a + b) * 0.5f);
                c.y = (ya + yb) * 0.5f;
                Vector3 size = alongX ? new Vector3(b - a, yb - ya, thickness) : new Vector3(thickness, yb - ya, b - a);
                EditorBuild.Box(name, parent, c, size, m, s);
            }

            float cursor = 0f;
            foreach (Opening o in list)
            {
                float a = Mathf.Clamp(o.s0, 0f, length);
                float b = Mathf.Clamp(o.s1, 0f, length);
                if (a > cursor) Segment(cursor, a, y0, y1);
                if (y0 + o.top < y1) Segment(a, b, y0 + o.top, y1);
                cursor = Mathf.Max(cursor, b);
            }
            if (cursor < length) Segment(cursor, length, y0, y1);
        }

        /// <summary>Wall running along a wing (constant v), u0..u1. Openings are given as u ranges.</summary>
        static void WingWallU(string name, Transform parent, WingId w, float y, float u0, float u1, float v, float thickness,
                              List<(float a, float b, float top)> openingsU, Material m, SurfaceType s, float height = MallLayout.WallTop)
        {
            var o = new List<Opening>();
            if (openingsU != null) foreach (var op in openingsU) o.Add(new Opening(op.a - u0, op.b - u0, op.top));
            WallSpan(name, parent, MallLayout.WingPoint(w, u0, v, 0f), MallLayout.WingPoint(w, u1, v, 0f), thickness, y, y + height, o, m, s);
        }

        /// <summary>Wall running across a wing (constant u), v0..v1 (v0 &lt; v1). Openings are given as v ranges.</summary>
        static void WingWallV(string name, Transform parent, WingId w, float y, float u, float v0, float v1, float thickness,
                              List<(float a, float b, float top)> openingsV, Material m, SurfaceType s, float height = MallLayout.WallTop)
        {
            var o = new List<Opening>();
            if (openingsV != null) foreach (var op in openingsV) o.Add(new Opening(op.a - v0, op.b - v0, op.top));
            WallSpan(name, parent, MallLayout.WingPoint(w, u, v0, 0f), MallLayout.WingPoint(w, u, v1, 0f), thickness, y, y + height, o, m, s);
        }

        static GameObject WingBox(string name, Transform parent, WingId w, float u0, float u1, float v0, float v1, float y0, float y1, Material m, SurfaceType s, bool isStatic = true)
        {
            return BoxRect(name, parent, MallLayout.WingRect(w, u0, u1, v0, v1), y0, y1, m, s, isStatic);
        }

        static void Railing(string name, Transform parent, Vector3 a, Vector3 b, float y)
        {
            Vector3 d = b - a;
            d.y = 0f;
            if (d.magnitude < 0.05f) return;
            bool alongX = Mathf.Abs(d.x) > Mathf.Abs(d.z);
            Vector3 lo = Vector3.Min(a, b), hi = Vector3.Max(a, b);
            const float t = 0.04f;
            if (alongX)
            {
                BoxMinMax(name + " glass", parent, new Vector3(lo.x, y, a.z - t), new Vector3(hi.x, y + 1.0f, a.z + t), glassMat, SurfaceType.Glass);
                BoxMinMax(name + " rail", parent, new Vector3(lo.x, y + 1.0f, a.z - 0.05f), new Vector3(hi.x, y + 1.08f, a.z + 0.05f), railMat, SurfaceType.Metal);
            }
            else
            {
                BoxMinMax(name + " glass", parent, new Vector3(a.x - t, y, lo.z), new Vector3(a.x + t, y + 1.0f, hi.z), glassMat, SurfaceType.Glass);
                BoxMinMax(name + " rail", parent, new Vector3(a.x - 0.05f, y + 1.0f, lo.z), new Vector3(a.x + 0.05f, y + 1.08f, hi.z), railMat, SurfaceType.Metal);
            }
        }

        /// <summary>A stopped escalator: a walkable 30 degree incline with side balustrades (moving steps in Part 12).</summary>
        static void Escalator(string name, Transform parent, Vector3 bottom, Vector3 top, float width)
        {
            Transform g = Group(name, parent);
            EditorBuild.Ramp(name + " treads", g, bottom, top, width, 0.45f, metal, SurfaceType.Metal);
            Vector3 dir = (top - bottom).normalized;
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
            Vector3 right = rot * Vector3.right;
            Vector3 normal = rot * Vector3.up;
            float length = Vector3.Distance(bottom, top);
            Vector3 mid = (bottom + top) * 0.5f;
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                Vector3 c = mid + right * (sgn * (width * 0.5f + 0.06f)) + normal * 0.45f;
                EditorBuild.Box(name + " balustrade", g, c, new Vector3(0.1f, 1.3f, length + 1.0f), railMat, SurfaceType.Metal, rot);
            }
        }

        static void PointLight(Transform parent, Vector3 pos, float range, float intensity, Color color, bool fixture = true)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.color = color;
            l.shadows = LightShadows.None;
            if (fixture)
            {
                GameObject f = Decor("Fixture", go.transform, new Vector3(-0.6f, 0.25f, -0.15f), new Vector3(0.6f, 0.29f, 0.15f), fixtureMat);
                f.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static readonly Color FluorescentWhite = new Color(0.92f, 0.96f, 1f);
        static readonly Color WarmWhite = new Color(1f, 0.9f, 0.75f);

        /// <summary>A sign with text, readable by someone looking along 'viewDir'.</summary>
        static void Sign(Transform parent, string text, Vector3 centre, Vector3 viewDir, float width, float textSize, bool panel = true)
        {
            float yaw = Mathf.Atan2(viewDir.x, viewDir.z) * Mathf.Rad2Deg;
            if (panel)
            {
                Vector3 across = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                Vector3 half = across * (width * 0.5f) + viewDir * 0.04f + Vector3.up * (textSize * 2.2f);
                Vector3 a = centre - half, b = centre + half;
                Decor("Sign panel", parent, a + viewDir * 0.06f, b + viewDir * 0.06f, signMat);
            }
            EditorBuild.Label(parent, text, centre - viewDir * 0.01f, yaw, textSize);
        }

        static void Slot(GameObject host, DoorKind kind, float width, float height, bool locked, string note)
        {
            DoorSlot slot = host.AddComponent<DoorSlot>();
            slot.kind = kind;
            slot.width = width;
            slot.height = height;
            slot.startsLocked = locked;
            slot.note = note;
        }

        /// <summary>Marker object at a doorway (centre of the opening at floor level).</summary>
        static GameObject DoorMarker(Transform parent, string name, Vector3 floorCentre, DoorKind kind, float width, float height, bool locked, string note)
        {
            var go = new GameObject("Door: " + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = floorCentre;
            Slot(go, kind, width, height, locked, note);
            return go;
        }
    }
}
