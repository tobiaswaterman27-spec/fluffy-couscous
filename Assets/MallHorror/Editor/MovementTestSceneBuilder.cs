using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using MallHorror.Audio;
using MallHorror.Core;
using MallHorror.World;

namespace MallHorror.EditorTools
{
    /// <summary>
    /// Menu: Mall Horror > 2 - Build Movement Test Scene.
    /// Builds a 48 x 48 m test hall that checks every movement and collision case:
    /// floor materials, doorways (one too narrow), a low beam, a crouch tunnel, a crawlable vent with a bend,
    /// a vent that is too small, stairs, a walkable ramp, a too-steep slope, a narrow corridor,
    /// pillars, a 2 m drop, and pushable physics props.
    /// </summary>
    public static class MovementTestSceneBuilder
    {
        const string ScenePath = EditorBuild.Root + "/Scenes/MovementTest.unity";
        const string MatFolder = EditorBuild.Root + "/Materials/Dev";

        static Material floorTile, darkTile, carpet, concrete, metal, ventMetal, wood, wall, ceiling, accent, lightFixture, crate;

        [MenuItem("Mall Horror/2 - Build Movement Test Scene", priority = 2)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ProjectSetup.EnsureLayers();
            EditorBuild.EnsureFolder(EditorBuild.Root + "/Scenes");
            CreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();

            Transform env = new GameObject("Environment").transform;
            BuildRoom(env);
            BuildSurfacePads(env);
            BuildDoorways(env);
            BuildCrouchTests(env);
            BuildVents(env);
            BuildStairsAndSlopes(env);
            BuildCorridorAndPillars(env);
            BuildProps();
            BuildLights();

            new GameObject("Noise Debug Gizmos").AddComponent<NoiseDebugGizmos>();

            GameObject player = PlayerFactory.CreatePlayer(new Vector3(0f, 0.05f, -18f), 0f);
            PlayerFactory.SaveAsPrefab(player);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Selection.activeGameObject = player;

            EditorUtility.DisplayDialog("Mall Horror",
                "Movement test scene built and saved to\n" + ScenePath + "\n\nPress Play, then click inside the Game view.",
                "OK");
        }

        // ------------------------------------------------------------------

        static void CreateMaterials()
        {
            floorTile = EditorBuild.Material(MatFolder, "FloorTile", new Color(0.62f, 0.6f, 0.55f), 0.55f);
            darkTile = EditorBuild.Material(MatFolder, "FloorTileDark", new Color(0.3f, 0.3f, 0.3f), 0.6f);
            carpet = EditorBuild.Material(MatFolder, "Carpet", new Color(0.32f, 0.16f, 0.16f), 0.05f);
            concrete = EditorBuild.Material(MatFolder, "Concrete", new Color(0.45f, 0.45f, 0.44f), 0.15f);
            metal = EditorBuild.Material(MatFolder, "Metal", new Color(0.55f, 0.57f, 0.6f), 0.6f, 0.9f);
            ventMetal = EditorBuild.Material(MatFolder, "VentMetal", new Color(0.5f, 0.52f, 0.54f), 0.5f, 0.8f);
            wood = EditorBuild.Material(MatFolder, "Wood", new Color(0.45f, 0.3f, 0.18f), 0.3f);
            wall = EditorBuild.Material(MatFolder, "Wall", new Color(0.72f, 0.7f, 0.64f), 0.2f);
            ceiling = EditorBuild.Material(MatFolder, "Ceiling", new Color(0.6f, 0.6f, 0.58f), 0.1f);
            accent = EditorBuild.Material(MatFolder, "Accent", new Color(0.55f, 0.45f, 0.2f), 0.3f);
            crate = EditorBuild.Material(MatFolder, "Crate", new Color(0.55f, 0.42f, 0.25f), 0.2f);
            lightFixture = EditorBuild.Material(MatFolder, "LightFixture", Color.white, 0.5f, 0f, new Color(2.5f, 2.6f, 2.7f));
        }

        static void SetupLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.07f, 0.075f, 0.08f);
            RenderSettings.fog = false;
        }

        static void BuildRoom(Transform env)
        {
            Transform t = new GameObject("Room").transform;
            t.SetParent(env, false);
            EditorBuild.Box("Floor", t, new Vector3(0f, -0.1f, 0f), new Vector3(48f, 0.2f, 48f), floorTile, SurfaceType.Tile);
            EditorBuild.Box("Ceiling", t, new Vector3(0f, 4.1f, 0f), new Vector3(48f, 0.2f, 48f), ceiling, SurfaceType.Concrete);
            EditorBuild.Box("Wall N", t, new Vector3(0f, 2f, 24.1f), new Vector3(48.4f, 4f, 0.2f), wall, SurfaceType.Concrete);
            EditorBuild.Box("Wall S", t, new Vector3(0f, 2f, -24.1f), new Vector3(48.4f, 4f, 0.2f), wall, SurfaceType.Concrete);
            EditorBuild.Box("Wall E", t, new Vector3(24.1f, 2f, 0f), new Vector3(0.2f, 4f, 48.4f), wall, SurfaceType.Concrete);
            EditorBuild.Box("Wall W", t, new Vector3(-24.1f, 2f, 0f), new Vector3(0.2f, 4f, 48.4f), wall, SurfaceType.Concrete);
        }

        static void BuildSurfacePads(Transform env)
        {
            Transform t = new GameObject("Surface Pads").transform;
            t.SetParent(env, false);
            var pads = new (string name, float x, Material m, SurfaceType s)[]
            {
                ("CARPET", -10f, carpet, SurfaceType.Carpet),
                ("TILE", -4f, darkTile, SurfaceType.Tile),
                ("CONCRETE", 4f, concrete, SurfaceType.Concrete),
                ("METAL", 10f, metal, SurfaceType.Metal),
                ("WOOD", 16f, wood, SurfaceType.Wood),
            };
            foreach (var p in pads)
            {
                EditorBuild.Box("Pad " + p.name, t, new Vector3(p.x, 0.01f, -12f), new Vector3(4f, 0.02f, 4f), p.m, p.s);
                EditorBuild.Label(t, p.name, new Vector3(p.x, 0.8f, -14.3f), 0f);
            }
        }

        static void BuildDoorways(Transform env)
        {
            Transform t = new GameObject("Doorways").transform;
            t.SetParent(env, false);
            // Wall along z = -4 from x = -24 to -4, with three openings.
            var openings = new List<(float center, float width)> { (-20f, 0.9f), (-15f, 1.4f), (-10f, 0.55f) };
            const float z = -4f, height = 4f, doorHeight = 2.1f, thick = 0.2f;
            float cursor = -24f;
            foreach (var o in openings)
            {
                float left = o.center - o.width * 0.5f;
                float right = o.center + o.width * 0.5f;
                if (left > cursor)
                {
                    EditorBuild.Box("Wall segment", t, new Vector3((cursor + left) * 0.5f, height * 0.5f, z), new Vector3(left - cursor, height, thick), wall, SurfaceType.Concrete);
                }
                EditorBuild.Box("Lintel", t, new Vector3(o.center, (doorHeight + height) * 0.5f, z), new Vector3(o.width, height - doorHeight, thick), wall, SurfaceType.Concrete);
                cursor = right;
            }
            EditorBuild.Box("Wall segment", t, new Vector3((cursor + -4f) * 0.5f, height * 0.5f, z), new Vector3(-4f - cursor, height, thick), wall, SurfaceType.Concrete);

            EditorBuild.Label(t, "DOOR 0.9 m", new Vector3(-20f, 2.5f, z - 0.15f), 0f);
            EditorBuild.Label(t, "DOUBLE 1.4 m", new Vector3(-15f, 2.5f, z - 0.15f), 0f);
            EditorBuild.Label(t, "0.55 m\nTOO NARROW", new Vector3(-10f, 2.6f, z - 0.15f), 0f);
        }

        static void BuildCrouchTests(Transform env)
        {
            Transform t = new GameObject("Crouch Tests").transform;
            t.SetParent(env, false);

            // Low beam (bottom at 1.5 m): must crouch to pass.
            EditorBuild.Box("Beam post L", t, new Vector3(-2.2f, 0.75f, 2f), new Vector3(0.2f, 1.5f, 0.3f), accent, SurfaceType.Wood);
            EditorBuild.Box("Beam post R", t, new Vector3(0.2f, 0.75f, 2f), new Vector3(0.2f, 1.5f, 0.3f), accent, SurfaceType.Wood);
            EditorBuild.Box("Low beam", t, new Vector3(-1f, 1.6f, 2f), new Vector3(2.6f, 0.2f, 0.3f), accent, SurfaceType.Wood);
            EditorBuild.Label(t, "LOW BEAM 1.5 m", new Vector3(-1f, 2f, 1.8f), 0f);

            // Crouch tunnel: 1.6 m wide, 1.3 m high, 8 m long.
            EditorBuild.Box("Tunnel wall L", t, new Vector3(2.1f, 0.65f, 4f), new Vector3(0.2f, 1.3f, 8f), wall, SurfaceType.Concrete);
            EditorBuild.Box("Tunnel wall R", t, new Vector3(3.9f, 0.65f, 4f), new Vector3(0.2f, 1.3f, 8f), wall, SurfaceType.Concrete);
            EditorBuild.Box("Tunnel ceiling", t, new Vector3(3f, 1.4f, 4f), new Vector3(2f, 0.2f, 8f), concrete, SurfaceType.Concrete);
            EditorBuild.Label(t, "CROUCH TUNNEL\n1.3 m", new Vector3(3f, 2f, -0.1f), 0f);
        }

        static void BuildVents(Transform env)
        {
            Transform t = new GameObject("Vents").transform;
            t.SetParent(env, false);

            // Crawlable duct: interior 0.7 m wide x 0.65 m high (y 0.04 .. 0.69), with a 90 degree bend.
            EditorBuild.Box("Duct1 floor", t, new Vector3(8f, 0.02f, 3.35f), new Vector3(0.7f, 0.04f, 6.7f), ventMetal, SurfaceType.VentMetal);
            EditorBuild.Box("Duct1 top", t, new Vector3(8f, 0.715f, 3.35f), new Vector3(0.9f, 0.05f, 6.7f), ventMetal, SurfaceType.VentMetal);
            EditorBuild.Box("Duct1 side L", t, new Vector3(7.6f, 0.37f, 3.35f), new Vector3(0.1f, 0.74f, 6.7f), ventMetal, SurfaceType.VentMetal);
            EditorBuild.Box("Duct1 side R", t, new Vector3(8.4f, 0.37f, 3.0f), new Vector3(0.1f, 0.74f, 6.0f), ventMetal, SurfaceType.VentMetal);

            EditorBuild.Box("Duct2 floor", t, new Vector3(10.175f, 0.02f, 6.35f), new Vector3(3.65f, 0.04f, 0.7f), ventMetal, SurfaceType.VentMetal);
            EditorBuild.Box("Duct2 top", t, new Vector3(10.15f, 0.715f, 6.35f), new Vector3(3.7f, 0.05f, 0.9f), ventMetal, SurfaceType.VentMetal);
            EditorBuild.Box("Duct2 back", t, new Vector3(9.8f, 0.37f, 6.75f), new Vector3(4.4f, 0.74f, 0.1f), ventMetal, SurfaceType.VentMetal);
            EditorBuild.Box("Duct2 front", t, new Vector3(10.2f, 0.37f, 5.95f), new Vector3(3.6f, 0.74f, 0.1f), ventMetal, SurfaceType.VentMetal);
            EditorBuild.Label(t, "VENT 0.65 m\ncrawl (C)", new Vector3(8f, 1.2f, -0.1f), 0f);

            // Too small: 0.6 m wide, 0.4 m high, dead end. Must refuse entry.
            EditorBuild.Box("Small floor", t, new Vector3(14f, 0.02f, 1.5f), new Vector3(0.6f, 0.04f, 3f), ventMetal, SurfaceType.VentMetal);
            EditorBuild.Box("Small top", t, new Vector3(14f, 0.465f, 1.5f), new Vector3(0.8f, 0.05f, 3f), ventMetal, SurfaceType.VentMetal);
            EditorBuild.Box("Small side L", t, new Vector3(13.65f, 0.245f, 1.5f), new Vector3(0.1f, 0.49f, 3f), ventMetal, SurfaceType.VentMetal);
            EditorBuild.Box("Small side R", t, new Vector3(14.35f, 0.245f, 1.5f), new Vector3(0.1f, 0.49f, 3f), ventMetal, SurfaceType.VentMetal);
            EditorBuild.Box("Small end", t, new Vector3(14f, 0.245f, 3.05f), new Vector3(0.8f, 0.49f, 0.1f), ventMetal, SurfaceType.VentMetal);
            EditorBuild.Label(t, "VENT 0.4 m\nTOO SMALL", new Vector3(14f, 1f, -0.1f), 0f);

            // A dim light inside the bend so you can see in there.
            var l = new GameObject("Vent light").AddComponent<Light>();
            l.transform.SetParent(t, false);
            l.transform.localPosition = new Vector3(9.5f, 0.5f, 6.35f);
            l.type = LightType.Point;
            l.range = 2.5f;
            l.intensity = 0.6f;
            l.color = new Color(1f, 0.85f, 0.6f);
        }

        static void BuildStairsAndSlopes(Transform env)
        {
            Transform t = new GameObject("Stairs & Slopes").transform;
            t.SetParent(env, false);

            // 12 steps of 0.18 m rise / 0.3 m run up to a 2.16 m platform.
            for (int i = 0; i < 12; i++)
            {
                float top = 0.18f * (i + 1);
                EditorBuild.Box("Step " + (i + 1), t, new Vector3(-15f, top * 0.5f, 4f + 0.3f * i + 0.15f), new Vector3(2f, top, 0.3f), concrete, SurfaceType.Concrete);
            }
            EditorBuild.Box("Platform", t, new Vector3(-15f, 1.08f, 9.6f), new Vector3(6f, 2.16f, 4f), concrete, SurfaceType.Concrete);
            EditorBuild.Label(t, "STAIRS\n(platform edge = 2 m drop)", new Vector3(-15f, 1.2f, 3.8f), 0f);

            // 15 degree ramp down from the platform.
            float run = 2.16f / Mathf.Tan(15f * Mathf.Deg2Rad);
            EditorBuild.Ramp("Ramp 15deg", t, new Vector3(-15f, 2.16f, 11.6f), new Vector3(-15f, 0f, 11.6f + run), 2f, 0.3f, darkTile, SurfaceType.Tile);

            // 50 degree slope: too steep to walk up, you slide back.
            float steepRun = 2.5f / Mathf.Tan(50f * Mathf.Deg2Rad);
            EditorBuild.Ramp("Slope 50deg", t, new Vector3(-5f, 0f, 12f), new Vector3(-5f, 2.5f, 12f + steepRun), 2f, 0.3f, concrete, SurfaceType.Concrete);
            EditorBuild.Label(t, "50° SLOPE\nTOO STEEP", new Vector3(-5f, 1.2f, 11.6f), 0f);
        }

        static void BuildCorridorAndPillars(Transform env)
        {
            Transform t = new GameObject("Corridor & Pillars").transform;
            t.SetParent(env, false);

            // 1.2 m corridor, 16 m long.
            EditorBuild.Box("Corridor wall S", t, new Vector3(8f, 1.5f, 13.9f), new Vector3(16f, 3f, 0.2f), wall, SurfaceType.Concrete);
            EditorBuild.Box("Corridor wall N", t, new Vector3(8f, 1.5f, 15.3f), new Vector3(16f, 3f, 0.2f), wall, SurfaceType.Concrete);
            EditorBuild.Label(t, "1.2 m CORRIDOR", new Vector3(-0.3f, 2f, 14.6f), 90f);

            float[] xs = { 4f, 6.5f, 9f, 11.5f, 14f };
            float[] zs = { 18f, 20.5f };
            foreach (float x in xs)
            {
                foreach (float z in zs)
                {
                    EditorBuild.Box("Pillar", t, new Vector3(x, 2f, z), new Vector3(0.6f, 4f, 0.6f), concrete, SurfaceType.Concrete);
                }
            }
        }

        static void BuildProps()
        {
            Transform t = new GameObject("Physics Props").transform;
            int propLayer = GameLayers.PropLayer;

            PropBox(t, "Crate 5kg", new Vector3(17f, 0.2f, -8f), new Vector3(0.4f, 0.4f, 0.4f), 5f, crate, SurfaceType.Wood, propLayer);
            PropBox(t, "Crate 20kg", new Vector3(18.5f, 0.3f, -8f), new Vector3(0.6f, 0.6f, 0.6f), 20f, crate, SurfaceType.Wood, propLayer);
            PropBox(t, "Crate 45kg", new Vector3(20.5f, 0.4f, -8f), new Vector3(0.8f, 0.8f, 0.8f), 45f, crate, SurfaceType.Wood, propLayer);
            PropBox(t, "Metal bin 2kg", new Vector3(21f, 0.25f, -4f), new Vector3(0.35f, 0.5f, 0.35f), 2f, metal, SurfaceType.Metal, propLayer);
            PropBox(t, "Metal can 0.3kg", new Vector3(17f, 0.07f, -2f), new Vector3(0.07f, 0.12f, 0.07f), 0.3f, metal, SurfaceType.Metal, propLayer);

            // Compound collider: one rigidbody, five box colliders.
            var table = new GameObject("Table 15kg (compound collider)");
            table.transform.SetParent(t, false);
            table.transform.localPosition = new Vector3(18f, 0f, -4f);
            Rigidbody rb = table.AddComponent<Rigidbody>();
            rb.mass = 15f;
            table.AddComponent<PhysicsProp>().material = SurfaceType.Wood;
            table.AddComponent<SurfaceMaterial>().type = SurfaceType.Wood;
            EditorBuild.Box("Top", table.transform, new Vector3(0f, 0.745f, 0f), new Vector3(1.2f, 0.05f, 0.8f), wood, SurfaceType.Wood, null, false);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    EditorBuild.Box("Leg", table.transform, new Vector3(0.55f * sx, 0.36f, 0.35f * sz), new Vector3(0.05f, 0.72f, 0.05f), wood, SurfaceType.Wood, null, false);
                }
            }
            EditorBuild.SetLayerRecursive(table, propLayer);

            // Static shelf (not movable).
            EditorBuild.Box("Shelf (static)", t, new Vector3(22f, 1f, 4f), new Vector3(0.5f, 2f, 3f), wood, SurfaceType.Wood);
            EditorBuild.Label(t, "PUSH THE CRATES", new Vector3(19f, 1.6f, -9.5f), 0f);
        }

        static void PropBox(Transform parent, string name, Vector3 pos, Vector3 size, float mass, Material m, SurfaceType s, int layer)
        {
            GameObject go = EditorBuild.Box(name, parent, pos, size, m, s, null, false);
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            go.AddComponent<PhysicsProp>().material = s;
            EditorBuild.SetLayerRecursive(go, layer);
        }

        static void BuildLights()
        {
            Transform t = new GameObject("Lights").transform;
            float[] grid = { -14f, 0f, 14f };
            foreach (float x in grid)
            {
                foreach (float z in grid)
                {
                    bool shadows = Mathf.Abs(x) < 1f || Mathf.Abs(z) < 1f;
                    var go = new GameObject($"Ceiling light {x},{z}");
                    go.transform.SetParent(t, false);
                    go.transform.localPosition = new Vector3(x, 3.7f, z);
                    Light l = go.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.range = 16f;
                    l.intensity = 1.6f;
                    l.color = new Color(0.93f, 0.97f, 1f);
                    l.shadows = shadows ? LightShadows.Soft : LightShadows.None;

                    GameObject fixture = EditorBuild.Box("Fixture", go.transform, new Vector3(0f, 0.27f, 0f), new Vector3(1.2f, 0.04f, 0.3f), lightFixture, SurfaceType.Metal);
                    Object.DestroyImmediate(fixture.GetComponent<Collider>());
                    fixture.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        static void AddToBuildSettings(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (list.Exists(s => s.path == path)) return;
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
