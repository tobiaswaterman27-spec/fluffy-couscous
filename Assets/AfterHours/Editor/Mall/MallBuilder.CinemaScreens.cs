using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Inside the screens: red velvet masking curtains either side of each screen, a green exit sign over each door,
    /// and in Screen 2 the projector never stopped - the screen glows a washed-out grey-blue and the beam hangs in
    /// the dust from the port above the door.
    /// </summary>
    public static partial class MallBuilder
    {
        static void CinemaScreenDetail(Transform g, WingId w, float y, float lobbyEnd, float end, (string, float, float, float, float)[] screens)
        {
            Vector3 fwd = MallLayout.WingForward(w);
            Vector3 P(float u, float v, float h) => MallLayout.WingPoint(w, u, v, y + h);
            Quaternion toSeats = Quaternion.LookRotation(-fwd), toScreen = Quaternion.LookRotation(fwd);
            Material velvet = ArtLibrary.Plain("Cinema_Curtain", new Color(0.35f, 0.04f, 0.05f), 0.2f);
            Material projected = ArtLibrary.Plain("Screen_Projecting", new Color(0.55f, 0.58f, 0.62f), 0.2f, 0f, new Color(0.55f, 0.62f, 0.72f), false);
            for (int i = 0; i < screens.Length; i++)
            {
                var s = screens[i];
                float v0 = s.Item2, v1 = s.Item3, vc = (v0 + v1) * 0.5f, doorV = (s.Item4 + s.Item5) * 0.5f;
                // curtains at the screen sides, gathered in folds
                foreach (float vv in new[] { v0 + 0.45f, v1 - 0.45f })
                    for (int k = 0; k < 4; k++)
                        Detail("Masking curtain", g, P(end - 0.7f - (k % 2) * 0.05f, vv + (vv < vc ? 1 : -1) * (k * 0.12f - 0.18f), 2.55f), new Vector3(0.14f, 4.1f, 0.08f), velvet, toSeats);
                // exit sign over the door (inside the screen)
                Detail("Exit sign", g, P(lobbyEnd + 0.16f, doorV, 2.45f), new Vector3(0.42f, 0.16f, 0.05f), exitSignMat, toScreen);
                if (i != 1) continue;
                // Screen 2: still projecting
                Detail("Projected image", g, P(end - 0.62f, vc, 2.65f), new Vector3(v1 - v0 - 1.3f, 3.2f, 0.005f), projected, toSeats);
                Vector3 port = P(lobbyEnd + 0.2f, vc + 2.2f, 4.0f);
                Vector3 target = P(end - 0.62f, vc, 2.65f);
                Material beam = AdditiveMaterial("Projector_Beam", "decal_light_beam.png", new Color(0.7f, 0.78f, 0.9f, 0.045f));
                Shaft(g, port, (target - port).normalized, Vector3.Distance(port, target), 2.4f, beam);
                var lg = new GameObject("Screen glow");
                lg.transform.SetParent(g, false);
                lg.transform.localPosition = P(end - 3f, vc, 2.6f);
                Light l = lg.AddComponent<Light>();
                l.type = LightType.Point; l.range = 12f; l.intensity = 0.55f; l.color = new Color(0.62f, 0.7f, 0.85f); l.shadows = LightShadows.None;
                l.lightmapBakeType = LightmapBakeType.Realtime;
            }
        }
    }
}
