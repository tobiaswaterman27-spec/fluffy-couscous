using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using AfterHours.Audio;
using AfterHours.World;
using AfterHours.World.Layout;
using AfterHours.Rendering;

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
                Progress("Preparing materials", 0f);
                ProjectSetup.EnsureLayers();
                EditorBuild.EnsureFolder(EditorBuild.Root + "/Scenes");
                Walls.Clear();
                CreateMaterials();

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SetupLighting();

                Transform mall = Group("Mall", null);

                Progress("Floor plates and ceilings", 0.03f);
                BuildFloorPlates(Group("Floor Plates", mall));

                Progress("Atrium", 0.06f);
                BuildAtrium(Group("Atrium", mall));

                Progress("Shopping wings", 0.09f);
                Transform wings = Group("Wings", mall);
                foreach (MallFloor f in MallLayout.ShopFloors)
                {
                    foreach (WingId w in MallLayout.Wings)
                    {
                        BuildWing(Group($"{MallLayout.FloorCode(f)} {w} wing", wings), f, w);
                    }
                }

                Progress("Anchor stores", 0.15f);
                BuildAnchors(Group("Anchors", mall));

                Progress("Back of house", 0.18f);
                BuildQuadrants(Group("Back Of House", mall));
                BuildLockers(Group("Lockers & Cupboards", mall));

                Progress("Stairwells and lifts", 0.21f);
                BuildCores(Group("Stair Cores", mall));
                BuildLiftShafts(Group("Lift Shafts", mall));

                Progress("Car park", 0.24f);
                Transform carPark = Group("Car Park", mall);
                BuildCarPark(carPark);

                Progress("Roof", 0.27f);
                BuildRoof(Group("Roof", mall));

                Progress("Escape routes and directory", 0.29f);
                BuildEscapeMarkers(Group("Escape Routes", mall));
                DirectoryBuilder.Build(Group("Directory", mall));

                // ---------------- Part 4: detail, sound, lighting ----------------
                Transform details = Group("Details", mall);
                PrepareDressing();
                Progress("Detail: ceilings (lights, sprinklers, vents, CCTV...)", 0.32f);
                DressCeilings(Group("Ceilings", details));
                {
                    var pa = new GameObject("PA system");
                    pa.transform.SetParent(mall, false);
                    pa.AddComponent<AfterHours.Audio.PASystem>().speakers = paSpeakers.ToArray();
                }
                Progress("Detail: walls (skirting, sockets, extinguishers, adverts...)", 0.42f);
                DressWalls(Group("Walls", details));
                Progress("Detail: floors (stains, scuffs, gum, cracks...)", 0.5f);
                DressFloors(Group("Floors", details));
                Progress("Detail: litter, cardboard, trolleys, cobwebs, footprints", 0.545f);
                DressClutter(Group("Clutter", details));
                Progress("Detail: door frames and exit signs", 0.55f);
                DressDoorways(Group("Doorways", details));
                Progress("Power: switchboard, circuit areas, emergency lights", 0.553f);
                Transform power = Group("Power", mall);
                Transform board = BuildSwitchboard(power);
                BuildPowerZones(power);
                BuildMalfunctions(power, board);
                Progress("Ventilation ducts", 0.555f);
                BuildVents(Group("Vents", mall));
                Progress("Doors and shutters", 0.56f);
                BuildDoors(Group("Doors", mall));
                Progress("Items", 0.57f);
                BuildItems(Group("Items", mall));
                Progress("Escape routes (puzzles)", 0.58f);
                BuildEscapeRoutes(Group("Escape Route Puzzles", mall));
                Progress("Detail: concourses", 0.58f);
                DressConcourses(Group("Concourses", details));
                Progress("Detail: shop interiors", 0.61f);
                DressShopInteriors(Group("Shops", details));
                Progress("Detail: pipes, trays, handrails", 0.64f);
                DressServices(Group("Services", details));
                DressNotices(Group("Notices", details));
                DressRats(Group("Rats", mall));
                Progress("Detail: car park", 0.67f);
                DressCarPark(carPark);

                Progress("Atmosphere: light shafts", 0.715f);
                BuildAtmosphere(mall);
                BuildDrips(mall);
                Progress("Ambient sound", 0.72f);
                BuildAmbience(mall);

                Progress("Lighting, probes and post-processing", 0.75f);
                Transform lighting = Group("Lighting", null);
                LightingSetup.ApplyLightingSettings(false);
                LightingSetup.AddGlobalVolume(lighting);
                LightingSetup.AddLightProbes(lighting);
                LightingSetup.AddReflectionProbes(lighting);

                Progress("Optimising: merging meshes", 0.8f);
                int merged = MeshCombiner.Combine(mall, Group("Combined Meshes", null));
                Debug.Log("[AfterHours] Mall built. Merged visual meshes: " + merged);

                Progress("Elliot's navigation map (slow)", 0.9f);
                NavigationBaker.Bake(new Bounds(new Vector3(-30f, 6f, 0f), new Vector3(340f, 34f, 270f)));
                // Elliot starts in the car park, wandering, unaware anyone is in the building.
                ElliotFactory.CreateWithAI(null, new Vector3(30f, MallLayout.CarParkY + 0.05f, 70f), 180f);

                new GameObject("Noise Debug Gizmos").AddComponent<NoiseDebugGizmos>();
                Progress("Main menu, opening, saving and the ending", 0.97f);
                BuildGameFlow(Group("Game", null));
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
                "\n\nPress Play to explore (Esc > Teleport jumps around).\n\nFor the final look run After Hours > 5 - Bake Lighting (slow; leave it running).", "OK");
        }

        static void Progress(string what, float t)
        {
            EditorUtility.DisplayProgressBar("After Hours - building the mall", what, t);
        }

        // =====================================================================
        // Setup
        // =====================================================================

        static Material terrazzoMat, vinylMat, laminateMat, darkPanel, trofferMat, trofferDead, trofferFlicker, exitSignMat,
            redMat, whiteFixture, blackMat, yellowPaint, greenMat, mirrorMat, porcelain;

        static void CreateMaterials()
        {
            ArtLibrary.Reset();
            PrepareClutterMaterials();
            Color w = Color.white;
            floorTile = ArtLibrary.Surface("Floor_Porcelain", "floor_tile", 1.2f, w, 1f, 1f);
            terrazzoMat = ArtLibrary.Surface("Floor_Terrazzo", "terrazzo", 2f, w, 1f, 0.6f);
            vinylMat = ArtLibrary.Surface("Floor_Vinyl", "floor_vinyl", 2f, w, 1f, 0.5f);
            floorCarpet = ArtLibrary.Surface("Floor_Carpet", "carpet", 1f, w, 1f, 1f);
            concrete = ArtLibrary.Surface("Concrete", "concrete", 2f, w, 1f, 1f);
            carParkFloor = ArtLibrary.Surface("Floor_CarPark", "carpark_floor", 4f, w, 1f, 1f);
            wallPaint = ArtLibrary.Surface("Wall_Paint", "wall_paint", 2f, w, 1f, 0.6f);
            wallService = ArtLibrary.Surface("Wall_Blockwork", "wall_block", 2.64f, w, 1f, 1f);
            ceilingMat = ArtLibrary.Surface("Ceiling_Tiles", "ceiling_tile", 1.2f, w, 1f, 1f);
            metal = ArtLibrary.Surface("Metal_Brushed", "metal_brushed", 1f, w, 1f, 0.5f, 1f);
            railMat = metal;
            liftDoorMat = metal;
            shutterMat = ArtLibrary.Surface("Metal_Shutter", "shutter", 1.2f, w, 1f, 1.2f, 1f);
            woodMat = ArtLibrary.Surface("Wood_Veneer", "wood", 1.2f, w, 1f, 0.8f);
            laminateMat = ArtLibrary.Surface("Laminate", "laminate", 1f, w, 1f, 0.5f);
            counterMat = laminateMat;
            plantMat = ArtLibrary.Surface("Metal_Painted", "metal_painted", 1f, w, 1f, 1f, 1f);
            toiletMat = ArtLibrary.Surface("Tile_White", "tile_white", 0.6f, w, 1f, 1f);
            darkPanel = ArtLibrary.Surface("Panel_Dark", "plaster_dark", 1.5f, w, 1f, 0.5f);
            signMat = darkPanel;
            propMat = ArtLibrary.Surface("Prop_Wood", "wood", 0.8f, new Color(0.85f, 0.8f, 0.72f), 0.8f, 0.6f);
            seatMat = ArtLibrary.Surface("Seat_Fabric", "carpet", 0.5f, new Color(0.9f, 0.45f, 0.45f), 0.5f, 0.6f);

            glassMat = TransparentMat("Glass", new Color(0.55f, 0.65f, 0.7f, 0.22f));
            darkGlass = Mat("DarkGlass", new Color(0.03f, 0.035f, 0.04f), 0.95f, 0.2f);
            waterMat = Mat("Water", new Color(0.12f, 0.2f, 0.24f), 0.95f);
            screenMat = Mat("CinemaScreen", new Color(0.85f, 0.85f, 0.85f), 0.1f);
            lineMat = ArtLibrary.Plain("Paint_Line", new Color(0.82f, 0.74f, 0.32f), 0.25f);
            yellowPaint = ArtLibrary.Plain("Paint_HazardYellow", new Color(0.85f, 0.68f, 0.1f), 0.3f);
            redMat = ArtLibrary.Plain("Plastic_Red", new Color(0.65f, 0.06f, 0.05f), 0.55f);
            whiteFixture = ArtLibrary.Plain("Plastic_White", new Color(0.86f, 0.86f, 0.84f), 0.45f);
            blackMat = ArtLibrary.Plain("Plastic_Black", new Color(0.04f, 0.04f, 0.045f), 0.5f);
            greenMat = ArtLibrary.Plain("Plastic_Green", new Color(0.1f, 0.4f, 0.2f), 0.45f);
            porcelain = ArtLibrary.Plain("Porcelain", new Color(0.92f, 0.93f, 0.92f), 0.85f);
            mirrorMat = ArtLibrary.Plain("Mirror", new Color(0.85f, 0.87f, 0.9f), 0.97f, 1f);
            // Fluorescent panels: emissive (they also light the baked scene), dead ones and flickering ones.
            trofferMat = ArtLibrary.Plain("Light_Panel", new Color(0.9f, 0.92f, 0.95f), 0.6f, 0f, new Color(3.2f, 3.35f, 3.5f), true);
            trofferDead = ArtLibrary.Plain("Light_Panel_Dead", new Color(0.55f, 0.56f, 0.55f), 0.55f);
            trofferFlicker = ArtLibrary.Plain("Light_Panel_Flicker", new Color(0.9f, 0.92f, 0.95f), 0.6f, 0f, new Color(3.2f, 3.35f, 3.5f), false);
            exitSignMat = ArtLibrary.Plain("Exit_Sign", new Color(0.1f, 0.6f, 0.25f), 0.6f, 0f, new Color(0.25f, 1.6f, 0.5f), true);
            fixtureMat = trofferMat;
            foreach (Material m in new[] { lineMat, yellowPaint, redMat, whiteFixture, blackMat, greenMat, porcelain, mirrorMat, trofferMat, trofferDead, trofferFlicker, exitSignMat })
            {
                ArtLibrary.MarkDetail(m);
            }

            categoryMats.Clear();
            void C(UnitCategory c, Color col) => categoryMats[c] = ArtLibrary.Surface("Fixture_" + c, "laminate", 1f, col, 0.9f, 0.4f);
            C(UnitCategory.Fashion, new Color(0.75f, 0.55f, 0.62f));
            C(UnitCategory.Footwear, new Color(0.7f, 0.58f, 0.48f));
            C(UnitCategory.Electronics, new Color(0.45f, 0.55f, 0.72f));
            C(UnitCategory.Department, new Color(0.7f, 0.5f, 0.48f));
            C(UnitCategory.Supermarket, new Color(0.48f, 0.7f, 0.5f));
            C(UnitCategory.Food, new Color(0.85f, 0.65f, 0.4f));
            C(UnitCategory.Restaurant, new Color(0.8f, 0.5f, 0.38f));
            C(UnitCategory.Cafe, new Color(0.7f, 0.56f, 0.42f));
            C(UnitCategory.Services, new Color(0.6f, 0.62f, 0.66f));
            C(UnitCategory.Health, new Color(0.55f, 0.76f, 0.7f));
            C(UnitCategory.Books, new Color(0.66f, 0.6f, 0.46f));
            C(UnitCategory.Toys, new Color(0.88f, 0.72f, 0.4f));
            C(UnitCategory.Jewellery, new Color(0.82f, 0.76f, 0.52f));
            C(UnitCategory.Home, new Color(0.64f, 0.68f, 0.55f));
            C(UnitCategory.Sports, new Color(0.45f, 0.64f, 0.68f));
            C(UnitCategory.Music, new Color(0.6f, 0.46f, 0.7f));
            C(UnitCategory.Leisure, new Color(0.7f, 0.46f, 0.74f));
            C(UnitCategory.Cinema, new Color(0.4f, 0.34f, 0.52f));
            C(UnitCategory.Vacant, new Color(0.55f, 0.55f, 0.55f));
        }

        /// <summary>Floor finish for a zone (the slab underneath is cut along zone boundaries).</summary>
        static Material FloorFor(ZoneInfo z, out SurfaceType surface)
        {
            switch (z.kind)
            {
                case ZoneKind.Atrium: surface = SurfaceType.Tile; return terrazzoMat;
                case ZoneKind.ServiceCorridor:
                case ZoneKind.BackOfHouse:
                case ZoneKind.Passage: surface = SurfaceType.Tile; return vinylMat;
                case ZoneKind.Toilets: surface = SurfaceType.Tile; return toiletMat;
                case ZoneKind.Cinema: surface = SurfaceType.Carpet; return floorCarpet;
                case ZoneKind.Stairwell: surface = SurfaceType.Concrete; return concrete;
                default: surface = SurfaceType.Tile; return floorTile;
            }
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
            // A faint dark haze: big spaces fade into blackness with distance, the way they do at night.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.011f, 0.012f, 0.014f);
            RenderSettings.fogDensity = 0.014f;
            RenderQuality.Apply();

            // Moonlight: faint and blue, baked only (so it never lights the inside before baking). After
            // "5 - Bake Lighting" it falls through the atrium skylight, the glass entrance and the car park ramp.
            var moonGo = new GameObject("Moonlight");
            moonGo.transform.rotation = Quaternion.Euler(38f, 145f, 0f);
            Light moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.65f, 0.88f);
            moon.intensity = 0.3f;
            moon.shadows = LightShadows.Soft;
            moon.lightmapBakeType = LightmapBakeType.Baked;
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

        /// <summary>Every wall built is recorded so the detail pass can dress both faces.</summary>
        struct WallRecord
        {
            public Vector3 p0, p1;
            public float thickness, y0, y1;
            public List<Opening> openings;
            public Material material;
        }

        static readonly List<WallRecord> Walls = new List<WallRecord>();

        struct Opening
        {
            public float s0, s1, top, bottom;
            public Opening(float s0, float s1, float top)
            {
                this.s0 = Mathf.Min(s0, s1);
                this.s1 = Mathf.Max(s0, s1);
                // a raised opening (a vent with a sill) arrives encoded by RaisedTop: sill in cm x 1000 + top
                if (top >= 1000f)
                {
                    int cm = Mathf.FloorToInt(top / 1000f);
                    bottom = cm * 0.01f;
                    this.top = top - cm * 1000f;
                }
                else { bottom = 0f; this.top = top; }
            }
        }

        /// <summary>An opening whose bottom is 'sill' above the floor (packed into the usual 'top' value).</summary>
        static float RaisedTop(float sill, float top) => Mathf.Round(sill * 100f) * 1000f + top;

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
            Walls.Add(new WallRecord { p0 = p0, p1 = p1, thickness = thickness, y0 = y0, y1 = y1, openings = list, material = m });

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
                if (o.bottom > 0f) Segment(a, b, y0, y0 + o.bottom);
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

        static void PointLight(Transform parent, Vector3 pos, float range, float intensity, Color color, bool fixture = false)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            // No two old tubes are quite the same: a touch greener or pinker, a little brighter or dimmer.
            int h = MallLayout.Hash((int)(pos.x * 10f), (int)(pos.y * 10f), (int)(pos.z * 10f), 61);
            float v = (h % 1000) / 1000f;
            Color drift = v < 0.33f ? new Color(0.88f, 1f, 0.9f) : v < 0.66f ? new Color(1f, 0.93f, 0.96f) : Color.white;
            l.color = Color.Lerp(color, color * drift, 0.5f);
            l.intensity = intensity * (0.88f + 0.24f * ((h / 1000 % 100) / 100f));
            l.shadows = LightShadows.None;
            go.AddComponent<AfterHours.Rendering.BakeableLight>();
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
        static GameObject DoorMarker(Transform parent, string name, Vector3 floorCentre, DoorKind kind, float width, float height, bool locked, string note, string lockId = "")
        {
            var go = new GameObject("Door: " + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = floorCentre;
            Slot(go, kind, width, height, locked, note);
            go.GetComponent<DoorSlot>().lockId = lockId ?? "";
            return go;
        }
    }
}
