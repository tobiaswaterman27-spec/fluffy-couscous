using System.Collections.Generic;
using UnityEngine;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    public static partial class MallBuilder
    {
        static readonly Dictionary<int, Mesh> plantMeshes = new Dictionary<int, Mesh>();

        /// <summary>
        /// An indoor plant nobody has watered since the mall shut: arching stems, long leaves that droop, a share
        /// of them browned and curled, and dead ones on the soil. Generated (4 variants), saved as mesh assets.
        /// Submeshes: 0 stems, 1 living leaves, 2 dead leaves.
        /// </summary>
        static Mesh PlantMesh(int variant)
        {
            if (plantMeshes.TryGetValue(variant, out Mesh m) && m != null) return m;
            var rng = new System.Random(4100 + variant);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var V = new List<Vector3>(); var N = new List<Vector3>(); var UV = new List<Vector2>();
            var stems = new List<int>(); var live = new List<int>(); var dead = new List<int>();
            float deadShare = 0.25f + variant * 0.12f;

            void Tube(List<Vector3> path, float r0, float r1)
            {
                const int sides = 5;
                int start = V.Count;
                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 t = (i < path.Count - 1 ? path[i + 1] - path[i] : path[i] - path[i - 1]).normalized;
                    Vector3 a = Vector3.Cross(t, Vector3.right).sqrMagnitude > 0.01f ? Vector3.Cross(t, Vector3.right).normalized : Vector3.Cross(t, Vector3.forward).normalized;
                    Vector3 b = Vector3.Cross(t, a);
                    float r = Mathf.Lerp(r0, r1, i / (float)(path.Count - 1));
                    for (int s = 0; s < sides; s++)
                    {
                        float ang = s * Mathf.PI * 2f / sides;
                        Vector3 n = a * Mathf.Cos(ang) + b * Mathf.Sin(ang);
                        V.Add(path[i] + n * r); N.Add(n); UV.Add(new Vector2(s / (float)sides, i / (float)path.Count));
                    }
                }
                for (int i = 0; i < path.Count - 1; i++)
                    for (int s = 0; s < sides; s++)
                    {
                        int a0 = start + i * sides + s, a1 = start + i * sides + (s + 1) % sides;
                        int b0 = a0 + sides, b1 = a1 + sides;
                        stems.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
                    }
            }

            void Leaf(Vector3 at, Vector3 dir, float length, float width, float droop, bool isDead)
            {
                // a long blade: midrib bends down along its length; dead ones curl and narrow
                const int segs = 6;
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
                if (side.sqrMagnitude < 0.01f) side = Vector3.right;
                int start = V.Count;
                Vector3 p = at;
                Vector3 d = dir.normalized;
                for (int i = 0; i <= segs; i++)
                {
                    float t = i / (float)segs;
                    float w = width * Mathf.Sin(Mathf.PI * Mathf.Clamp(t * 0.95f + 0.05f, 0f, 1f)) * (isDead ? 0.55f : 1f);
                    Vector3 curl = isDead ? Vector3.up * (w * 0.6f) : Vector3.up * (w * 0.15f);
                    Vector3 nrm = Vector3.Cross(d, side).normalized;
                    if (nrm.y < 0f) nrm = -nrm;
                    V.Add(p - side * w + curl); N.Add(nrm); UV.Add(new Vector2(0f, t));
                    V.Add(p); N.Add(nrm); UV.Add(new Vector2(0.5f, t));
                    V.Add(p + side * w + curl); N.Add(nrm); UV.Add(new Vector2(1f, t));
                    p += d * (length / segs);
                    d = (d + Vector3.down * droop * (0.4f + t)).normalized;
                }
                var list = isDead ? dead : live;
                for (int i = 0; i < segs; i++)
                {
                    int r = start + i * 3, q = r + 3;
                    list.AddRange(new[] { r, q, r + 1, r + 1, q, q + 1, r + 1, q + 1, r + 2, r + 2, q + 1, q + 2 });
                }
            }

            int stemCount = 5 + rng.Next(4);
            for (int s = 0; s < stemCount; s++)
            {
                float yaw = s * 360f / stemCount + R(-20f, 20f);
                float lean = R(8f, 30f);
                float height = R(0.7f, 1.5f);
                var path = new List<Vector3>();
                Vector3 pos = new Vector3(R(-0.06f, 0.06f), 0f, R(-0.06f, 0.06f));
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(lean, 0f, 0f) * Vector3.up;
                for (int i = 0; i < 6; i++)
                {
                    path.Add(pos);
                    pos += dir * (height / 5f);
                    dir = (dir + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 0.12f + Vector3.down * 0.04f * i).normalized;
                }
                Tube(path, 0.012f, 0.005f);
                // leaves from the upper half of the stem
                int leaves = 3 + rng.Next(4);
                for (int k = 0; k < leaves; k++)
                {
                    int at = 2 + rng.Next(path.Count - 2);
                    Vector3 ld = Quaternion.Euler(0f, yaw + R(-70f, 70f), 0f) * Quaternion.Euler(R(40f, 80f), 0f, 0f) * Vector3.up;
                    bool isDead = rng.NextDouble() < deadShare;
                    Leaf(path[at], ld, R(0.3f, 0.55f), R(0.035f, 0.06f), isDead ? R(0.5f, 0.9f) : R(0.15f, 0.35f), isDead);
                }
            }
            // dead leaves fallen on the soil
            for (int k = 0; k < 4 + variant * 2; k++)
            {
                Vector3 at = new Vector3(R(-0.3f, 0.3f), 0.01f, R(-0.3f, 0.3f));
                Leaf(at, Quaternion.Euler(0f, R(0f, 360f), 0f) * Vector3.forward, R(0.2f, 0.4f), R(0.03f, 0.05f), 0.02f, true);
            }

            m = new Mesh { name = "Plant " + variant };
            m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV);
            m.subMeshCount = 3;
            m.SetTriangles(stems, 0); m.SetTriangles(live, 1); m.SetTriangles(dead, 2);
            m.RecalculateBounds();
            string dir2 = EditorBuild.Root + "/Models/Plants";
            EditorBuild.EnsureFolder(dir2);
            string path2 = dir2 + "/plant_" + variant + ".asset";
            UnityEditor.AssetDatabase.DeleteAsset(path2);
            UnityEditor.AssetDatabase.CreateAsset(m, path2);
            plantMeshes[variant] = m;
            return m;
        }

        static Material TwoSided(Material m)
        {
            m.SetFloat("_Cull", 0f);
            m.doubleSidedGI = true;
            return m;
        }
    }
}
