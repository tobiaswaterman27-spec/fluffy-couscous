using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using AfterHours.Rendering;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    public static partial class MallBuilder
    {
        /// <summary>Space kept clear of desks in the security office for the monitor console.</summary>
        static AreaRect SecurityConsoleRect => new AreaRect(-44.7f, 21.6f, -42.6f, 28.4f);

        /// <summary>
        /// The security office's CCTV: a console along the outer wall with six monitors in a rack, wired to six
        /// cameras around the mall (bracket cameras you can spot under the ceilings). One camera died years ago.
        /// </summary>
        static void SecurityMonitors(Transform parent, float y)
        {
            Transform g = Group("CCTV", parent);
            float wallX = -44.7f;
            // console desk and the monitor rack above it
            BoxMinMax("CCTV console", g, new Vector3(wallX, y, 22.3f), new Vector3(wallX + 0.85f, y + 0.76f, 27.7f), counterMat, SurfaceType.Wood);
            Decor("Monitor rack", g, new Vector3(wallX, y + 0.76f, 22.4f), new Vector3(wallX + 0.45f, y + 2.05f, 27.6f), fixtureMat);
            Decor("Keyboard", g, new Vector3(wallX + 0.55f, y + 0.76f, 24.6f), new Vector3(wallX + 0.72f, y + 0.785f, 25.4f), darkPanel);
            Decor("Joystick", g, new Vector3(wallX + 0.55f, y + 0.76f, 25.8f), new Vector3(wallX + 0.68f, y + 0.86f, 25.93f), darkPanel);
            Decor("Logbook", g, new Vector3(wallX + 0.5f, y + 0.76f, 23.2f), new Vector3(wallX + 0.8f, y + 0.775f, 23.62f), Mat("Paper", new Color(0.86f, 0.84f, 0.78f), 0.1f));
            Decor("Mug", g, new Vector3(wallX + 0.62f, y + 0.76f, 26.7f), new Vector3(wallX + 0.7f, y + 0.86f, 26.78f), Mat("Mug", new Color(0.55f, 0.12f, 0.1f), 0.6f));

            var spots = new List<(string name, Vector3 pos, Vector3 look)>
            {
                ("CAM 01  ATRIUM", new Vector3(14.2f, MallLayout.FloorY(MallFloor.Level1) + 3.9f, 14.2f), new Vector3(0f, MallLayout.FloorY(MallFloor.Ground), 0f)),
                ("CAM 02  NORTH MALL", MallLayout.WingPoint(WingId.North, 90f, 4.8f, 3.9f), MallLayout.WingPoint(WingId.North, 34f, 0f, 0.6f)),
                ("CAM 03  EAST MALL L1", MallLayout.WingPoint(WingId.East, 90f, -4.8f, MallLayout.FloorY(MallFloor.Level1) + 3.9f), MallLayout.WingPoint(WingId.East, 34f, 0f, MallLayout.FloorY(MallFloor.Level1) + 0.6f)),
                ("CAM 04  ENTRANCE", MallLayout.WingPoint(WingId.South, 22f, 4.8f, 3.9f), MallLayout.WingPoint(WingId.South, 96f, 0f, 0.6f)),
                ("CAM 05  WEST MALL L2", MallLayout.WingPoint(WingId.West, 90f, 4.8f, MallLayout.FloorY(MallFloor.Level2) + 3.9f), MallLayout.WingPoint(WingId.West, 34f, 0f, MallLayout.FloorY(MallFloor.Level2) + 0.6f)),
                ("CAM 06  CAR PARK", new Vector3(-30f, MallLayout.CarParkY + 2.9f, -62f), new Vector3(8f, MallLayout.CarParkY, -24f)),
            };

            var cams = new Camera[spots.Count];
            var screens = new Renderer[spots.Count];
            var captions = new TextMesh[spots.Count];
            var names = new string[spots.Count];
            Material housing = Mat("CCTV housing", new Color(0.78f, 0.78f, 0.76f), 0.5f);
            for (int i = 0; i < spots.Count; i++)
            {
                var (name, pos, look) = spots[i];
                names[i] = name;
                // bracket camera in the mall
                var cam = new GameObject("CCTV camera - " + name);
                cam.transform.SetParent(g, false);
                cam.transform.localPosition = pos;
                cam.transform.localRotation = Quaternion.LookRotation(look - pos);
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Housing";
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.transform.SetParent(cam.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.02f, -0.16f);
                body.transform.localScale = new Vector3(0.11f, 0.1f, 0.3f);
                body.GetComponent<Renderer>().sharedMaterial = housing;
                body.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                Decor("Bracket", g, pos + new Vector3(-0.02f, 0.05f, -0.02f), pos + new Vector3(0.02f, 0.6f, 0.02f), housing);
                cams[i] = cam.AddComponent<Camera>();
                cams[i].enabled = false;

                // its monitor: 3 across, 2 high
                int col = i % 3, row = i / 3;
                float zc = 23.25f + col * 1.75f, yc = y + 1.12f + (1 - row) * 0.52f;
                Decor("Monitor", g, new Vector3(wallX + 0.04f, yc - 0.24f, zc - 0.34f), new Vector3(wallX + 0.5f, yc + 0.24f, zc + 0.34f), darkPanel);
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Screen";
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(g, false);
                quad.transform.localPosition = new Vector3(wallX + 0.505f, yc, zc);
                quad.transform.localRotation = Quaternion.LookRotation(Vector3.left);   // seen by someone looking towards the wall (-x)
                quad.transform.localScale = new Vector3(0.56f, 0.42f, 1f);
                screens[i] = quad.GetComponent<Renderer>();
                screens[i].shadowCastingMode = ShadowCastingMode.Off;
                TextMesh t = EditorBuild.Label(g, name, new Vector3(wallX + 0.51f, yc + 0.18f, zc - 0.25f), -90f, 0.0045f);
                t.anchor = TextAnchor.UpperLeft;
                t.alignment = TextAlignment.Left;
                t.color = new Color(0.85f, 0.95f, 0.88f);
                captions[i] = t;
            }

            var glowGo = new GameObject("Monitor glow");
            glowGo.transform.SetParent(g, false);
            glowGo.transform.localPosition = new Vector3(wallX + 1.2f, y + 1.3f, 25f);
            Light glow = glowGo.AddComponent<Light>();
            glow.type = LightType.Point; glow.range = 3.5f; glow.intensity = 0.6f; glow.color = new Color(0.7f, 0.9f, 0.8f);
            glow.shadows = LightShadows.None; glow.lightmapBakeType = LightmapBakeType.Realtime;

            var bank = g.gameObject.AddComponent<CCTVBank>();
            bank.cameras = cams;
            bank.screens = screens;
            bank.captions = captions;
            bank.names = names;
            bank.glow = glow;
            bank.deadFeed = 4;
        }
    }
}
