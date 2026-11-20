using UnityEngine;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Feature displays in the anchor shops. Toy Kingdom: a giant teddy slumped at the front, a train table with
    /// its loop of track and a stalled engine, a doll's house left open. Pro Sport: a trainer wall across the back,
    /// wire cages of footballs and basketballs, and a goal with its net sagging.
    /// </summary>
    public static partial class MallBuilder
    {
        static void ToyKingdomFeature(Transform g, WingId w, float y, float u0, float end)
        {
            Vector3 fwd = MallLayout.WingForward(w), rt = MallLayout.WingRight(w);
            Vector3 P(float u, float v, float h) => MallLayout.WingPoint(w, u, v, y + h);
            Quaternion face = Quaternion.LookRotation(-fwd);
            Material fur = ArtLibrary.Plain("Teddy_Fur", new Color(0.55f, 0.36f, 0.2f), 0.05f);
            Material furLight = ArtLibrary.Plain("Teddy_Muzzle", new Color(0.8f, 0.65f, 0.45f), 0.05f);
            Material eye = ArtLibrary.Plain("Teddy_Eye", new Color(0.02f, 0.02f, 0.02f), 0.95f);
            Material bow = ArtLibrary.Plain("Teddy_Bow", new Color(0.7f, 0.08f, 0.1f), 0.4f);
            Material tableTop = ArtLibrary.Plain("Train_Table", new Color(0.25f, 0.5f, 0.25f), 0.3f);
            Material track = ArtLibrary.Plain("Train_Track", new Color(0.55f, 0.4f, 0.25f), 0.4f);
            Material engine = ArtLibrary.Plain("Train_Engine", new Color(0.1f, 0.25f, 0.6f), 0.6f);
            Material housePink = ArtLibrary.Plain("DollHouse_Pink", new Color(0.9f, 0.7f, 0.75f), 0.4f);
            Material roof = ArtLibrary.Plain("DollHouse_Roof", new Color(0.5f, 0.15f, 0.12f), 0.4f);

            // giant teddy, sat with its back to the shelves, slumped a little to one side
            Vector3 t = P(u0 + 6.2f, -2.2f, 0f);
            Vector3 lean = Quaternion.AngleAxis(12f, fwd) * Vector3.up;
            EditorBuild.Box("Teddy (collider)", g, t + Vector3.up * 0.75f, new Vector3(1.4f, 1.5f, 1.3f), fur, SurfaceType.Carpet).GetComponent<Renderer>().enabled = false;
            Ball("Teddy body", g, t + lean * 0.65f, 1.25f, fur, true);
            Ball("Teddy belly", g, t + lean * 0.62f - fwd * 0.38f, 0.75f, furLight);
            Ball("Teddy head", g, t + lean * 1.55f - fwd * 0.08f, 0.85f, fur, true);
            Ball("Teddy muzzle", g, t + lean * 1.48f - fwd * 0.45f, 0.36f, furLight);
            Ball("Teddy nose", g, t + lean * 1.53f - fwd * 0.62f, 0.12f, eye);
            foreach (float s in new[] { -1f, 1f })
            {
                Ball("Teddy eye", g, t + lean * 1.7f - fwd * 0.4f + rt * s * 0.17f, 0.09f, eye);
                Ball("Teddy ear", g, t + lean * 1.95f + rt * s * 0.33f, 0.32f, fur);
                Ball("Teddy arm", g, t + lean * 0.75f + rt * s * 0.6f - fwd * 0.2f, 0.42f, fur);
                Ball("Teddy leg", g, t + Vector3.up * 0.22f + rt * s * 0.35f - fwd * 0.6f, 0.48f, fur);
                Ball("Teddy paw", g, t + Vector3.up * 0.22f + rt * s * 0.35f - fwd * 0.86f, 0.3f, furLight);
            }
            Detail("Teddy bow", g, t + lean * 1.15f - fwd * 0.38f, new Vector3(0.4f, 0.14f, 0.08f), bow, face);
            Sign(g, "HUG ME!", t + Vector3.up * 0.05f - fwd * 1.05f, -fwd, 0.6f, 0.025f);

            // train table: a loop of track, a few trees, the engine stopped on a bend, a carriage off the rails
            Vector3 tc = P(u0 + 6.5f, 2.3f, 0f);
            EditorBuild.Box("Train table", g, tc + Vector3.up * 0.3f, new Vector3(1.8f, 0.6f, 1.2f), tableTop, SurfaceType.Wood, Quaternion.LookRotation(fwd));
            for (int k = 0; k < 20; k++)
            {
                float a = k / 20f * Mathf.PI * 2f;
                Vector3 p = tc + Vector3.up * 0.61f + rt * Mathf.Cos(a) * 0.7f + fwd * Mathf.Sin(a) * 0.4f;
                Vector3 tan = (-rt * Mathf.Sin(a) * 0.7f + fwd * Mathf.Cos(a) * 0.4f).normalized;
                Detail("Track piece", g, p, new Vector3(0.12f, 0.015f, 0.24f), track, Quaternion.LookRotation(tan));
            }
            {
                float a = 0.3f;
                Vector3 p = tc + Vector3.up * 0.66f + rt * Mathf.Cos(a) * 0.7f + fwd * Mathf.Sin(a) * 0.4f;
                Vector3 tan = (-rt * Mathf.Sin(a) * 0.7f + fwd * Mathf.Cos(a) * 0.4f).normalized;
                Quaternion q = Quaternion.LookRotation(tan);
                Detail("Toy engine", g, p, new Vector3(0.1f, 0.09f, 0.2f), engine, q);
                Cyl("Engine funnel", g, p + tan * 0.06f + Vector3.up * 0.04f, p + tan * 0.06f + Vector3.up * 0.09f, 0.04f, blackMat);
                Detail("Carriage (derailed)", g, tc + Vector3.up * 0.65f + rt * 0.35f - fwd * 0.42f, new Vector3(0.1f, 0.08f, 0.18f), redMat, Quaternion.Euler(0f, 30f, 80f));
            }
            for (int k = 0; k < 5; k++)
            {
                Vector3 p = tc + Vector3.up * 0.6f + rt * (-0.15f + k * 0.08f) + fwd * (k % 2 == 0 ? 0.05f : -0.08f);
                Cyl("Toy tree", g, p, p + Vector3.up * 0.05f, 0.02f, track);
                Cone("Toy tree top", g, p + Vector3.up * 0.05f, 0.08f, 0.16f, greenMat);
            }

            // doll's house, front swung open
            Vector3 dh = P(u0 + 8.6f, 0f, 0f);
            EditorBuild.Box("Doll's house", g, dh + Vector3.up * 0.5f, new Vector3(1.0f, 1.0f, 0.55f), housePink, SurfaceType.Wood, face);
            Detail("Doll's house roof", g, dh + Vector3.up * 1.1f, new Vector3(1.05f, 0.05f, 0.62f), roof, face * Quaternion.Euler(0f, 0f, 0f));
            Detail("Doll's house floor", g, dh + Vector3.up * 0.5f - fwd * 0.05f, new Vector3(0.96f, 0.02f, 0.45f), whiteFixture, face);
            Detail("Doll's house front (open)", g, dh + Vector3.up * 0.5f - fwd * 0.3f + rt * 0.75f, new Vector3(0.02f, 1.0f, 0.5f), housePink, face * Quaternion.Euler(0f, 0f, 0f));
            for (int k = 0; k < 4; k++)
                Detail("Tiny furniture", g, dh + Vector3.up * (k < 2 ? 0.08f : 0.56f) - fwd * 0.1f + rt * (k % 2 == 0 ? -0.25f : 0.2f), new Vector3(0.12f, 0.1f, 0.1f), k % 2 == 0 ? redMat : woodMat, face);
            Detail("Doll (face down)", g, P(u0 + 8.0f, 0.7f, 0.04f), new Vector3(0.08f, 0.06f, 0.3f), housePink, Quaternion.Euler(0f, 35f, 0f));
        }

        static void ProSportFeature(Transform g, WingId w, float y, float u0, float end)
        {
            Vector3 fwd = MallLayout.WingForward(w), rt = MallLayout.WingRight(w);
            Vector3 P(float u, float v, float h) => MallLayout.WingPoint(w, u, v, y + h);
            Quaternion face = Quaternion.LookRotation(-fwd);
            var rng = new System.Random(2525);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            Material shelfWhite = whiteFixture;
            Material[] shoeCols = { ArtLibrary.Plain("Shoe_White", new Color(0.9f, 0.9f, 0.88f), 0.4f), ArtLibrary.Plain("Shoe_Black", new Color(0.05f, 0.05f, 0.06f), 0.4f), ArtLibrary.Plain("Shoe_Red", new Color(0.7f, 0.1f, 0.08f), 0.4f), ArtLibrary.Plain("Shoe_Blue", new Color(0.12f, 0.25f, 0.65f), 0.4f), ArtLibrary.Plain("Shoe_Neon", new Color(0.7f, 0.9f, 0.1f), 0.4f) };
            Material football = ArtLibrary.Plain("Football", new Color(0.92f, 0.92f, 0.9f), 0.5f);
            Material basketball = ArtLibrary.Plain("Basketball", new Color(0.8f, 0.35f, 0.08f), 0.3f);
            Material net = ArtLibrary.Plain("Goal_Net", new Color(0.85f, 0.85f, 0.85f), 0.2f);

            // trainer wall: angled shelves, one left shoe per slot (and the odd gap)
            Detail("Trainer wall backing", g, P(end - 0.33f, 0f, 1.6f), new Vector3(14f, 2.6f, 0.03f), darkPanel, face);
            var sk = new MeshKit();
            sk.Frame(Vector3.zero, face);
            Quaternion inv = Quaternion.Inverse(face);
            for (int row = 0; row < 6; row++)
            {
                float h = 0.5f + row * 0.4f;
                for (float v = -6.6f; v <= 6.6f; v += 0.55f)
                {
                    sk.Box(inv * P(end - 0.45f, v, h), new Vector3(0.38f, 0.015f, 0.22f), shelfWhite, 0f, false, 0f, -15f);
                    if (rng.NextDouble() < 0.12) continue;
                    Material m = shoeCols[rng.Next(shoeCols.Length)];
                    // side-on to the aisle, toe pointing along the wall
                    sk.Box(inv * P(end - 0.47f, v, h + 0.06f), new Vector3(0.28f, 0.09f, 0.1f), m);
                    sk.Box(inv * P(end - 0.47f, v, h + 0.015f), new Vector3(0.29f, 0.03f, 0.11f), shoeCols[0], 0f, false);
                }
            }
            sk.Build(g, "Trainer wall");
            Sign(g, "FOOTWEAR", P(end - 0.33f, 0f, 3.1f), -fwd, 4f, 0.06f, false);

            // ball cages near the entrance
            foreach (var (v, mat, dia) in new[] { (-3.2f, football, 0.22f), (3.2f, basketball, 0.24f) })
            {
                Vector3 c = P(u0 + 6f, v, 0f);
                EditorBuild.Box("Ball cage", g, c + Vector3.up * 0.5f, new Vector3(1.0f, 1.0f, 1.0f), metal, SurfaceType.Metal, face).GetComponent<Renderer>().enabled = false;
                for (int k = -2; k <= 2; k++)
                {
                    Detail("Cage wire", g, c + Vector3.up * 0.5f + rt * k * 0.24f - fwd * 0.5f, new Vector3(0.01f, 1.0f, 0.01f), metal, face);
                    Detail("Cage wire", g, c + Vector3.up * 0.5f + rt * k * 0.24f + fwd * 0.5f, new Vector3(0.01f, 1.0f, 0.01f), metal, face);
                    Detail("Cage wire", g, c + Vector3.up * 0.5f + fwd * k * 0.24f - rt * 0.5f, new Vector3(0.01f, 1.0f, 0.01f), metal, face);
                    Detail("Cage wire", g, c + Vector3.up * 0.5f + fwd * k * 0.24f + rt * 0.5f, new Vector3(0.01f, 1.0f, 0.01f), metal, face);
                }
                foreach (float hh in new[] { 0.02f, 0.5f, 0.98f })
                {
                    Detail("Cage ring", g, c + Vector3.up * hh - fwd * 0.5f, new Vector3(1.0f, 0.015f, 0.015f), metal, face);
                    Detail("Cage ring", g, c + Vector3.up * hh + fwd * 0.5f, new Vector3(1.0f, 0.015f, 0.015f), metal, face);
                    Detail("Cage ring", g, c + Vector3.up * hh - rt * 0.5f, new Vector3(0.015f, 0.015f, 1.0f), metal, face);
                    Detail("Cage ring", g, c + Vector3.up * hh + rt * 0.5f, new Vector3(0.015f, 0.015f, 1.0f), metal, face);
                }
                for (int k = 0; k < 22; k++)
                    Ball(mat == football ? "Football" : "Basketball", g, c + new Vector3(R(-0.38f, 0.38f), 0.12f + (k / 7) * 0.2f + R(0f, 0.05f), R(-0.38f, 0.38f)), dia, mat);
            }
            // two that got away
            Ball("Football (loose)", g, P(u0 + 3.6f, -1.4f, 0.11f), 0.22f, football);
            Ball("Basketball (loose)", g, P(u0 + 9.5f, 6.2f, 0.12f), 0.24f, basketball);

            // a goal on display, its net sagging off one post
            Vector3 gc = P(u0 + 8.6f, 0f, 0f);
            foreach (float s in new[] { -1.2f, 1.2f })
                Cyl("Goal post", g, gc + rt * s, gc + rt * s + Vector3.up * 1.5f, 0.06f, whiteFixture, true);
            Cyl("Crossbar", g, gc - rt * 1.2f + Vector3.up * 1.5f, gc + rt * 1.2f + Vector3.up * 1.5f, 0.06f, whiteFixture);
            Detail("Goal net (back)", g, gc + fwd * 0.8f + Vector3.up * 0.7f, new Vector3(2.4f, 1.4f, 0.01f), net, face * Quaternion.Euler(-12f, 0f, 0f));
            Detail("Goal net (sagging)", g, gc + fwd * 0.4f + rt * 1.0f + Vector3.up * 0.45f, new Vector3(0.6f, 0.9f, 0.01f), net, face * Quaternion.Euler(0f, 70f, 20f));
        }
    }
}
