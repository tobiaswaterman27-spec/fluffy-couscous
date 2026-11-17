using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using AfterHours.Rendering;
using AfterHours.World;
using AfterHours.World.Layout;

namespace AfterHours.EditorTools
{
    /// <summary>
    /// Merges the mall's static geometry into a few thousand meshes (grouped by material and 40 m area),
    /// which is what lets integrated graphics draw it smoothly.
    ///
    /// - Boxes using a tiled surface material get "box-mapped" UVs in world space, so every texture keeps its
    ///   real size (a 60 cm tile is 60 cm on every floor, wall and ceiling) and lines up across neighbouring boxes.
    /// - Other meshes (cylinders, decals, details) keep their own UVs.
    /// - Large surfaces get lightmap UVs for baking; small details are lit by light probes instead.
    /// - Colliders are untouched: collision stays exactly as built. Objects that only existed to be seen are removed.
    /// </summary>
    public static class MeshCombiner
    {
        class Batch
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector4> t = new List<Vector4>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> tri = new List<int>();
            public Material material;
            public bool detail;
        }

        static readonly Vector3[] FaceNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        public static int Combine(Transform root, Transform output)
        {
            var batches = new Dictionary<(Material, int, bool), Batch>();
            var keep = new HashSet<Renderer>();
            foreach (FluorescentFlicker f in root.GetComponentsInChildren<FluorescentFlicker>(true))
            {
                if (f.panels != null) foreach (Renderer r in f.panels) if (r != null) keep.Add(r);
            }
            foreach (SignLightbox s in root.GetComponentsInChildren<SignLightbox>(true))
            {
                if (s.faces != null) foreach (Renderer r in s.faces) if (r != null) keep.Add(r);
            }

            // Phase 1: read everything (nothing is changed yet, so a failure leaves the scene intact).
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            var consumed = new List<MeshRenderer>();
            var created = new List<GameObject>();
            try
            {
                int done = 0;
                foreach (MeshRenderer r in renderers)
                {
                    done++;
                    if (done % 2000 == 0) EditorUtility.DisplayProgressBar("After Hours - optimising", "Reading meshes " + done + " / " + renderers.Length, done / (float)renderers.Length);
                    if (!Eligible(r, keep)) continue;
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    Mesh mesh = mf.sharedMesh;
                    Material mat = r.sharedMaterial;
                    bool cube = IsCube(mesh);
                    if (!cube && !mesh.isReadable) continue;

                    Bounds b = r.bounds;
                    bool detail = ArtLibrary.IsDetail(mat) || Mathf.Max(b.size.x, b.size.y, b.size.z) < 0.7f;
                    var key = (mat, CellKey(b.center), detail);
                    if (!batches.TryGetValue(key, out Batch batch))
                    {
                        batch = new Batch { material = mat, detail = detail };
                        batches[key] = batch;
                    }
                    if (cube) AddBoxMapped(batch, r.transform, ArtLibrary.TryGetTileSize(mat, out float tile) ? tile : 0f);
                    else AddGeneric(batch, mesh, r.transform.localToWorldMatrix);
                    consumed.Add(r);
                }

                // Phase 2: build the merged meshes.
                int n = 0;
                foreach (var kv in batches)
                {
                    Batch batch = kv.Value;
                    if (batch.v.Count == 0) continue;
                    if (n++ % 50 == 0) EditorUtility.DisplayProgressBar("After Hours - optimising", "Building merged meshes", n / (float)batches.Count);
                    created.Add(BuildObject(batch, output));
                }
            }
            catch (System.Exception e)
            {
                // Undo phase 2 and keep the original objects: the mall stays fully visible.
                foreach (GameObject go in created) if (go != null) Object.DestroyImmediate(go);
                EditorUtility.ClearProgressBar();
                Debug.LogError("[AfterHours] Mesh merging failed, keeping the unmerged mall (slower but correct): " + e);
                return 0;
            }

            // Phase 3: success - remove the now-duplicated visuals (colliders stay).
            var emptied = new List<GameObject>();
            foreach (MeshRenderer r in consumed)
            {
                if (r == null) continue;
                GameObject go = r.gameObject;
                MeshFilter mf = go.GetComponent<MeshFilter>();
                Object.DestroyImmediate(r);
                if (mf != null) Object.DestroyImmediate(mf);
                emptied.Add(go);
            }
            foreach (GameObject go in emptied)
            {
                if (go == null || go.transform.childCount > 0) continue;
                bool onlyTrivial = true;
                foreach (Component c in go.GetComponents<Component>())
                {
                    if (c is Transform || c is SurfaceMaterial) continue;
                    onlyTrivial = false;
                    break;
                }
                if (onlyTrivial) Object.DestroyImmediate(go);
            }
            EditorUtility.ClearProgressBar();
            return created.Count;
        }

        static GameObject BuildObject(Batch batch, Transform output)
        {
            var mesh = new Mesh { name = "Combined " + batch.material.name };
            if (batch.v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(batch.v);
            mesh.SetNormals(batch.n);
            mesh.SetTangents(batch.t);
            mesh.SetUVs(0, batch.uv);
            mesh.SetTriangles(batch.tri, 0);
            mesh.RecalculateBounds();
            if (!batch.detail)
            {
                UnwrapParam.SetDefaults(out UnwrapParam p);
                Unwrapping.GenerateSecondaryUVSet(mesh, p);
            }

            var go = new GameObject(mesh.name);
            go.transform.SetParent(output, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = batch.material;
            bool transparent = batch.material.renderQueue >= (int)RenderQueue.Transparent - 100;
            if (batch.detail)
            {
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                mr.receiveGI = ReceiveGI.LightProbes;
            }
            else
            {
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic);
                mr.receiveGI = ReceiveGI.Lightmaps;
            }
            mr.shadowCastingMode = transparent ? ShadowCastingMode.Off : ShadowCastingMode.On;
            return go;
        }

        static bool Eligible(MeshRenderer r, HashSet<Renderer> keep)
        {
            if (keep.Contains(r)) return false;
            GameObject go = r.gameObject;
            if (GameObjectUtility.GetStaticEditorFlags(go) == 0) return false;
            if (r.sharedMaterial == null || r.sharedMaterials.Length != 1) return false;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || mf.sharedMesh.subMeshCount != 1) return false;
            if (go.GetComponent<TextMesh>() != null) return false;
            if (go.GetComponentInParent<Rigidbody>() != null) return false;
            if (go.GetComponent<DoorSlot>() != null) return false;     // becomes a real door / shutter in Part 5
            if (go.GetComponentInParent<MovingPlatform>() != null) return false;
            return true;
        }

        static bool IsCube(Mesh m) => m.vertexCount == 24 && m.name.StartsWith("Cube");

        static int CellKey(Vector3 p)
        {
            int band = (int)MallZones.FloorAt(p.y) + 1;
            int cx = Mathf.FloorToInt((p.x + 200f) / 40f);
            int cz = Mathf.FloorToInt((p.z + 200f) / 40f);
            return band * 10000 + cx * 100 + cz;
        }

        /// <summary>
        /// Adds a scaled/rotated unit cube. tile &gt; 0: UVs projected in world space (texture keeps real size).
        /// tile = 0: each face shows the whole texture once (decals, signs, panels).
        /// </summary>
        static void AddBoxMapped(Batch b, Transform t, float tile)
        {
            Matrix4x4 m = t.localToWorldMatrix;
            Quaternion rot = t.rotation;
            foreach (Vector3 n in FaceNormals)
            {
                Vector3 a1, a2;
                if (n.x != 0f) { a1 = Vector3.up; a2 = Vector3.forward; }
                else if (n.y != 0f) { a1 = Vector3.right; a2 = Vector3.forward; }
                else { a1 = Vector3.right; a2 = Vector3.up; }

                Vector3 c0 = n * 0.5f - a1 * 0.5f - a2 * 0.5f;
                Vector3 c1 = n * 0.5f + a1 * 0.5f - a2 * 0.5f;
                Vector3 c2 = n * 0.5f + a1 * 0.5f + a2 * 0.5f;
                Vector3 c3 = n * 0.5f - a1 * 0.5f + a2 * 0.5f;
                Vector3 w0 = m.MultiplyPoint3x4(c0), w1 = m.MultiplyPoint3x4(c1), w2 = m.MultiplyPoint3x4(c2), w3 = m.MultiplyPoint3x4(c3);
                if (Vector3.Cross(w1 - w0, w3 - w0).sqrMagnitude < 1e-8f) continue; // degenerate face

                Vector3 wn = (rot * n).normalized;
                Vector3 an = new Vector3(Mathf.Abs(wn.x), Mathf.Abs(wn.y), Mathf.Abs(wn.z));
                Vector3 uAxis, vAxis;
                if (an.y >= an.x && an.y >= an.z) { uAxis = Vector3.right; vAxis = Vector3.forward; }
                else if (an.x >= an.z) { uAxis = Vector3.forward; vAxis = Vector3.up; }
                else { uAxis = Vector3.right; vAxis = Vector3.up; }

                Vector3 tan = (uAxis - wn * Vector3.Dot(uAxis, wn)).normalized;
                float w = Vector3.Dot(Vector3.Cross(wn, tan), vAxis) >= 0f ? 1f : -1f;
                Vector4 tangent = new Vector4(tan.x, tan.y, tan.z, w);

                if (tile <= 0f)
                {
                    tan = (w1 - w0).normalized;
                    w = Vector3.Dot(Vector3.Cross(wn, tan), (w3 - w0).normalized) >= 0f ? 1f : -1f;
                    tangent = new Vector4(tan.x, tan.y, tan.z, w);
                }
                int start = b.v.Count;
                Vector3[] corners = { w0, w1, w2, w3 };
                Vector2[] unit = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
                for (int k = 0; k < 4; k++)
                {
                    Vector3 p = corners[k];
                    b.v.Add(p);
                    b.n.Add(wn);
                    b.t.Add(tangent);
                    b.uv.Add(tile > 0f ? new Vector2(Vector3.Dot(p, uAxis) / tile, Vector3.Dot(p, vAxis) / tile) : unit[k]);
                }
                // Unity draws triangles whose corners run clockwise as seen from the front.
                bool ok = Vector3.Dot(Vector3.Cross(w1 - w0, w2 - w0), wn) > 0f;
                if (ok)
                {
                    b.tri.Add(start); b.tri.Add(start + 1); b.tri.Add(start + 2);
                    b.tri.Add(start); b.tri.Add(start + 2); b.tri.Add(start + 3);
                }
                else
                {
                    b.tri.Add(start); b.tri.Add(start + 2); b.tri.Add(start + 1);
                    b.tri.Add(start); b.tri.Add(start + 3); b.tri.Add(start + 2);
                }
            }
        }

        static void AddGeneric(Batch b, Mesh mesh, Matrix4x4 m)
        {
            Vector3[] verts = mesh.vertices;
            Vector3[] norms = mesh.normals;
            Vector4[] tans = mesh.tangents;
            Vector2[] uvs = mesh.uv;
            int[] tris = mesh.triangles;
            Matrix4x4 nm = m.inverse.transpose;
            bool flip = m.determinant < 0f;
            int start = b.v.Count;
            for (int i = 0; i < verts.Length; i++)
            {
                b.v.Add(m.MultiplyPoint3x4(verts[i]));
                b.n.Add(norms.Length == verts.Length ? nm.MultiplyVector(norms[i]).normalized : Vector3.up);
                if (tans.Length == verts.Length)
                {
                    Vector3 tt = m.MultiplyVector(new Vector3(tans[i].x, tans[i].y, tans[i].z)).normalized;
                    b.t.Add(new Vector4(tt.x, tt.y, tt.z, flip ? -tans[i].w : tans[i].w));
                }
                else b.t.Add(new Vector4(1f, 0f, 0f, 1f));
                b.uv.Add(uvs.Length == verts.Length ? uvs[i] : Vector2.zero);
            }
            for (int i = 0; i < tris.Length; i += 3)
            {
                if (flip)
                {
                    b.tri.Add(start + tris[i]); b.tri.Add(start + tris[i + 2]); b.tri.Add(start + tris[i + 1]);
                }
                else
                {
                    b.tri.Add(start + tris[i]); b.tri.Add(start + tris[i + 1]); b.tri.Add(start + tris[i + 2]);
                }
            }
        }
    }
}
