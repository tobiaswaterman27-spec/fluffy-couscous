using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using AfterHours.Flow;
using AfterHours.World;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Part 13: the game flow object (menu, opening, saving, ending), the main menu camera, and the daylight
    /// ending: the front of the shopping centre at dawn - wet tarmac, a few parked cars, dead lamp posts, a tree
    /// line in the mist, the glass entrance doors... and the dark lobby behind them.
    /// The ending set is built far from the mall and stays switched off until someone gets out.
    /// </summary>
    public static partial class MallBuilder
    {
        static readonly Vector3 EndingOrigin = new Vector3(0f, 0f, -1500f);

        static void BuildGameFlow(Transform parent)
        {
            var go = new GameObject("Game Flow");
            go.transform.SetParent(parent, false);
            go.AddComponent<SaveSystem>();
            GameFlow flow = go.AddComponent<GameFlow>();

            // Main menu camera: drifts around the atrium.
            var camGo = new GameObject("Menu camera");
            camGo.transform.SetParent(go.transform, false);
            camGo.transform.position = new Vector3(0f, 10f, -13f);
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;
            cam.depth = 5f;
            camGo.AddComponent<AudioListener>();
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            flow.menuCamera = cam;
            flow.menuOrbitCentre = new Vector3(0f, 3f, 0f);

            BuildEnding(go.transform, flow);
        }

        static GameObject EndBox(string name, Transform parent, Vector3 min, Vector3 max, Material m, SurfaceType s, bool collider = true, bool visible = true)
        {
            GameObject g = BoxMinMax(name, parent, EndingOrigin + min, EndingOrigin + max, m, s, false);
            if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
            if (!visible) Object.DestroyImmediate(g.GetComponent<Renderer>());
            return g;
        }

        static void BuildEnding(Transform parent, GameFlow flow)
        {
            var set = new GameObject("Ending (daylight)");
            set.transform.SetParent(parent, false);
            Transform g = set.transform;

            Material asphalt = carParkFloor;
            Material paving = concrete;
            Material facade = wallPaint;
            Material entranceGlass = TransparentMat("Glass_Entrance", new Color(0.5f, 0.58f, 0.62f, 0.3f));
            Material lobbyDark = ArtLibrary.Plain("Lobby_Dark", new Color(0.05f, 0.05f, 0.055f), 0.4f);
            Material treeMat = ArtLibrary.Plain("Tree_Dark", new Color(0.09f, 0.13f, 0.09f), 0.1f);
            Material trunkMat = ArtLibrary.Plain("Tree_Trunk", new Color(0.12f, 0.1f, 0.08f), 0.1f);

            // Ground: wet tarmac car park, pavement along the front.
            EndBox("Car park surface", g, new Vector3(-150f, -0.3f, -260f), new Vector3(150f, 0f, 0f), asphalt, SurfaceType.Concrete);
            EndBox("Pavement", g, new Vector3(-70f, 0f, -6f), new Vector3(70f, 0.15f, 0f), paving, SurfaceType.Concrete);
            for (int i = -6; i <= 6; i++)
                EndBox("Bay line", g, new Vector3(i * 6f - 0.05f, 0.002f, -40f), new Vector3(i * 6f + 0.05f, 0.012f, -30f), lineMat, SurfaceType.Concrete, false);

            // The front of the building, with the glass entrance.
            EndBox("Facade", g, new Vector3(-70f, 0f, 0f), new Vector3(-4.5f, 12f, 0.5f), facade, SurfaceType.Concrete);
            EndBox("Facade", g, new Vector3(4.5f, 0f, 0f), new Vector3(70f, 12f, 0.5f), facade, SurfaceType.Concrete);
            EndBox("Facade over entrance", g, new Vector3(-4.5f, 3.4f, 0f), new Vector3(4.5f, 12f, 0.5f), facade, SurfaceType.Concrete);
            EndBox("Canopy", g, new Vector3(-7f, 3.7f, -3.2f), new Vector3(7f, 4.05f, 0f), metal, SurfaceType.Metal);
            for (int i = 0; i < 3; i++)
            {
                float x0 = -4.5f + i * 3f;
                EndBox("Entrance glass", g, new Vector3(x0 + 0.04f, 0.15f, 0.2f), new Vector3(x0 + 2.96f, 3.38f, 0.26f), entranceGlass, SurfaceType.Glass);
                EndBox("Door frame", g, new Vector3(x0, 0.15f, 0.18f), new Vector3(x0 + 0.06f, 3.4f, 0.28f), metal, SurfaceType.Metal, false);
            }
            TextMesh name = EditorBuild.Label(g, "KESTREL FIELDS", EndingOrigin + new Vector3(0f, 6.4f, -0.05f), 0f, 0.42f);
            name.color = new Color(0.82f, 0.82f, 0.8f);
            TextMesh sub = EditorBuild.Label(g, "SHOPPING CENTRE", EndingOrigin + new Vector3(0f, 5.1f, -0.05f), 0f, 0.16f);
            sub.color = new Color(0.7f, 0.7f, 0.68f);
            // Something slightly wrong: the opening hours.
            EndBox("Opening hours plaque", g, new Vector3(5f, 1.2f, -0.03f), new Vector3(5.6f, 1.9f, 0f), whiteFixture, SurfaceType.Metal, false);
            EditorBuild.Label(g, "OPENING HOURS\nMon - Sun\n9:00 - 23:30\n\nWe never close\nour doors on you", EndingOrigin + new Vector3(5.3f, 1.55f, -0.04f), 0f, 0.012f).color = new Color(0.1f, 0.1f, 0.1f);

            // The dark lobby behind the glass.
            EndBox("Lobby floor", g, new Vector3(-6f, -0.2f, 0.5f), new Vector3(6f, 0.15f, 11f), floorTile, SurfaceType.Tile);
            EndBox("Lobby ceiling", g, new Vector3(-6f, 3.4f, 0.5f), new Vector3(6f, 3.6f, 11f), lobbyDark, SurfaceType.Concrete);
            EndBox("Lobby wall", g, new Vector3(-6.2f, 0f, 0.5f), new Vector3(-6f, 3.6f, 11f), lobbyDark, SurfaceType.Concrete);
            EndBox("Lobby wall", g, new Vector3(6f, 0f, 0.5f), new Vector3(6.2f, 3.6f, 11f), lobbyDark, SurfaceType.Concrete);
            EndBox("Lobby back", g, new Vector3(-6f, 0f, 11f), new Vector3(6f, 3.6f, 11.2f), lobbyDark, SurfaceType.Concrete);
            var lobbyLightGo = new GameObject("Lobby light");
            lobbyLightGo.transform.SetParent(g, false);
            lobbyLightGo.transform.position = EndingOrigin + new Vector3(0f, 3.1f, 6.5f);
            Light lobbyLight = lobbyLightGo.AddComponent<Light>();
            lobbyLight.type = LightType.Point;
            lobbyLight.range = 9f;
            lobbyLight.intensity = 0.7f;
            lobbyLight.color = new Color(0.8f, 0.9f, 1f);
            lobbyLight.shadows = LightShadows.Soft;
            lobbyLight.lightmapBakeType = LightmapBakeType.Realtime;

            // Where it stands (each time you look away, a little closer to the glass).
            Vector3[] pts = { new Vector3(1.1f, 0.16f, 9.2f), new Vector3(-1.4f, 0.16f, 6.2f), new Vector3(0.4f, 0.16f, 3.3f), new Vector3(0f, 0.16f, 0.95f) };
            flow.figurePoints = new Transform[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                var p = new GameObject("Figure point " + i);
                p.transform.SetParent(g, false);
                p.transform.SetPositionAndRotation(EndingOrigin + pts[i], Quaternion.Euler(0f, 180f, 0f));
                flow.figurePoints[i] = p.transform;
            }
            var glass = new GameObject("Hand on the glass");
            glass.transform.SetParent(g, false);
            glass.transform.position = EndingOrigin + new Vector3(0.25f, 1.75f, 0.3f);
            flow.glassPoint = glass.transform;

            // Parked cars left overnight, dead lamp posts, trees in the mist.
            Color[] carColours = { new Color(0.5f, 0.52f, 0.55f), new Color(0.15f, 0.2f, 0.3f), new Color(0.45f, 0.08f, 0.07f), new Color(0.75f, 0.75f, 0.72f), new Color(0.1f, 0.1f, 0.1f) };
            Vector3[] cars = { new Vector3(-21f, 0f, -35f), new Vector3(-9f, 0f, -35f), new Vector3(15f, 0f, -35f), new Vector3(-3f, 0f, -58f), new Vector3(27f, 0f, -58f) };
            for (int i = 0; i < cars.Length; i++)
            {
                Material paint = ArtLibrary.Plain("Car_Paint_" + i, carColours[i], 0.8f, 0.4f);
                Vector3 c = cars[i];
                EndBox("Car body", g, c + new Vector3(-0.9f, 0.3f, -2.2f), c + new Vector3(0.9f, 1.0f, 2.2f), paint, SurfaceType.Metal);
                EndBox("Car cabin", g, c + new Vector3(-0.8f, 1.0f, -1.1f), c + new Vector3(0.8f, 1.5f, 1.0f), darkGlass, SurfaceType.Glass);
                for (int w = 0; w < 4; w++)
                {
                    float wx = (w % 2 == 0 ? -0.92f : 0.92f), wz = (w < 2 ? -1.4f : 1.4f);
                    EndBox("Wheel", g, c + new Vector3(wx - 0.1f, 0f, wz - 0.33f), c + new Vector3(wx + 0.1f, 0.62f, wz + 0.33f), blackMat, SurfaceType.Metal, false);
                }
            }
            for (int i = -2; i <= 2; i++)
            {
                Vector3 b = EndingOrigin + new Vector3(i * 22f + 8f, 0f, -46f);
                Cyl("Lamp post", g, b, b + Vector3.up * 7f, 0.14f, metal, true);
                Decor("Lamp head", g, b + new Vector3(-0.4f, 6.9f, -0.15f), b + new Vector3(0.4f, 7.1f, 0.15f), metal);
            }
            for (int i = 0; i < 26; i++)
            {
                float x = -130f + i * 10.4f + (i * 37 % 5);
                Vector3 b = EndingOrigin + new Vector3(x, 0f, -150f - (i * 53 % 17));
                Cyl("Tree trunk", g, b, b + Vector3.up * 4f, 0.4f, trunkMat);
                GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.name = "Tree crown";
                crown.transform.SetParent(g, false);
                crown.transform.position = b + Vector3.up * (6f + i % 3);
                crown.transform.localScale = new Vector3(6f, 7f + i % 4, 6f);
                Object.DestroyImmediate(crown.GetComponent<Collider>());
                crown.GetComponent<Renderer>().sharedMaterial = treeMat;
            }

            // You can't wander off into the fog: invisible edges.
            EndBox("Edge", g, new Vector3(-35f, 0f, -70f), new Vector3(-34f, 4f, 0f), metal, SurfaceType.Concrete, true, false);
            EndBox("Edge", g, new Vector3(34f, 0f, -70f), new Vector3(35f, 4f, 0f), metal, SurfaceType.Concrete, true, false);
            EndBox("Edge", g, new Vector3(-35f, 0f, -71f), new Vector3(35f, 4f, -70f), metal, SurfaceType.Concrete, true, false);

            // Grey dawn.
            var sunGo = new GameObject("Dawn sun");
            sunGo.transform.SetParent(g, false);
            sunGo.transform.rotation = Quaternion.Euler(14f, 205f, 0f);
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.9f;
            sun.color = new Color(1f, 0.9f, 0.8f);
            sun.shadows = LightShadows.Soft;
            sun.lightmapBakeType = LightmapBakeType.Realtime;

            var spawn = new GameObject("Ending spawn");
            spawn.transform.SetParent(g, false);
            spawn.transform.SetPositionAndRotation(EndingOrigin + new Vector3(1.2f, 0.2f, -2.4f), Quaternion.Euler(0f, 180f, 0f));

            // Nothing out here is baked or uses the mall's light probes: lit by the sun and the sky only.
            foreach (Renderer r in set.GetComponentsInChildren<Renderer>(true))
            {
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            foreach (Transform tr in set.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(tr.gameObject, 0);

            // The daylight look (weight 0 until the ending).
            var volGo = new GameObject("Daylight post-processing");
            volGo.transform.SetParent(parent, false);
            Volume vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 100f;
            vol.weight = 0f;
            vol.sharedProfile = CreateDaylightProfile();

            set.SetActive(false);
            flow.endingSet = set;
            flow.endingSpawn = spawn.transform;
            flow.endingSun = sun;
            flow.lobbyLight = lobbyLight;
            flow.daylightVolume = vol;
        }

        static VolumeProfile CreateDaylightProfile()
        {
            const string path = EditorBuild.Root + "/Settings/DaylightPostProcess.asset";
            EditorBuild.EnsureFolder(EditorBuild.Root + "/Settings");
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            ColorAdjustments ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.value = 0.2f;
            ca.contrast.value = 4f;
            ca.saturation.value = -12f;
            ca.colorFilter.value = Color.white;
            AssetDatabase.AddObjectToAsset(ca, profile);
            WhiteBalance wb = profile.Add<WhiteBalance>(true);
            wb.temperature.value = -10f;
            wb.tint.value = 0f;
            AssetDatabase.AddObjectToAsset(wb, profile);
            Vignette v = profile.Add<Vignette>(true);
            v.intensity.value = 0.18f;
            AssetDatabase.AddObjectToAsset(v, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
