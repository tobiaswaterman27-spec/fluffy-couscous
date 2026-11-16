using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AfterHours.Creature
{
    /// <summary>
    /// Elliot's real body: one sculpted, skinned mesh (about 76,000 triangles) with baked skin textures, built from
    /// the generated model file (tools/elliotgen). The skeleton's main joints are the ones ElliotAnimator moves; the
    /// file adds detail joints under them (chest, throat, upper lip, nostrils, brows, tongue, three joints in every
    /// finger, the thumbs and the toes), which ElliotAnimator drives from his voice.
    ///
    /// The mesh is rebuilt from the file whenever the object loads (in the editor too), so nothing large is stored
    /// in the scene.
    /// </summary>
    [ExecuteAlways]
    public class ElliotBody : MonoBehaviour
    {
        public TextAsset model;
        public Transform rig;
        public Material skin, bloodySkin, mouthParts;

        SkinnedMeshRenderer smr;
        Mesh mesh;
        bool bloody;
        public bool Built => smr != null;

        public struct BoneDef { public string name; public int parent; public Vector3 pos; public Quaternion rot; }
        public static readonly Dictionary<string, BoneDef> Rest = new Dictionary<string, BoneDef>();

        void OnEnable() { Build(); }

        void OnDisable()
        {
            if (smr != null)
            {
                if (Application.isPlaying) Destroy(smr.gameObject); else DestroyImmediate(smr.gameObject);
            }
            if (mesh != null)
            {
                if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            }
            smr = null;
            mesh = null;
        }

        /// <summary>After a kill: blood round the mouth, down the chest, over the hands.</summary>
        public void SetBloody(bool on)
        {
            bloody = on;
            if (smr == null) return;
            Material[] m = smr.sharedMaterials;
            if (m.Length > 0) m[0] = on && bloodySkin != null ? bloodySkin : skin;
            smr.sharedMaterials = m;
        }

        static string Str(BinaryReader r)
        {
            int n = r.ReadInt32();
            return Encoding.UTF8.GetString(r.ReadBytes(n));
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform f = FindDeep(t.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>The skeleton stored in a model file (names, parents, rest pose in the creature's own space).</summary>
        public static BoneDef[] ReadBones(byte[] bytes)
        {
            using (var r = new BinaryReader(new MemoryStream(bytes)))
            {
                if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "ELM1") return new BoneDef[0];
                int nb = r.ReadInt32();
                var defs = new BoneDef[nb];
                for (int i = 0; i < nb; i++)
                {
                    defs[i].name = Str(r);
                    defs[i].parent = r.ReadInt32();
                    defs[i].pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    defs[i].rot = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                }
                return defs;
            }
        }

        public void Build()
        {
            if (smr != null || model == null || rig == null) return;
            // a previous build that was saved by accident
            Transform old = transform.Find("Body");
            if (old != null) { if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject); }

            using (var r = new BinaryReader(new MemoryStream(model.bytes)))
            {
                if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "ELM1") { Debug.LogWarning("Elliot model: unknown format"); return; }
                int nb = r.ReadInt32();
                var defs = new BoneDef[nb];
                for (int i = 0; i < nb; i++)
                {
                    defs[i].name = Str(r);
                    defs[i].parent = r.ReadInt32();
                    defs[i].pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    defs[i].rot = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    Rest[defs[i].name] = defs[i];
                }
                int nv = r.ReadInt32();
                var pos = new Vector3[nv]; var nrm = new Vector3[nv]; var tan = new Vector4[nv]; var uv = new Vector2[nv];
                for (int i = 0; i < nv; i++) pos[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                for (int i = 0; i < nv; i++) nrm[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                for (int i = 0; i < nv; i++) tan[i] = new Vector4(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                for (int i = 0; i < nv; i++) uv[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
                var bi = new int[nv * 4];
                for (int i = 0; i < nv * 4; i++) bi[i] = r.ReadUInt16();
                var weights = new BoneWeight[nv];
                for (int i = 0; i < nv; i++)
                {
                    weights[i] = new BoneWeight
                    {
                        boneIndex0 = bi[i * 4], weight0 = r.ReadSingle(),
                        boneIndex1 = bi[i * 4 + 1], weight1 = r.ReadSingle(),
                        boneIndex2 = bi[i * 4 + 2], weight2 = r.ReadSingle(),
                        boneIndex3 = bi[i * 4 + 3], weight3 = r.ReadSingle(),
                    };
                }
                int ns = r.ReadInt32();
                var subs = new List<int[]>();
                for (int s = 0; s < ns; s++)
                {
                    Str(r);
                    int n = r.ReadInt32();
                    var idx = new int[n];
                    for (int i = 0; i < n; i++) idx[i] = (int)r.ReadUInt32();
                    subs.Add(idx);
                }

                // Joints: the main ones already exist (the factory made them); detail joints are created under their parents.
                var bones = new Transform[nb];
                var bind = new Matrix4x4[nb];
                for (int i = 0; i < nb; i++)
                {
                    Transform t = FindDeep(rig, defs[i].name);
                    if (t == null)
                    {
                        Transform parent = defs[i].parent >= 0 ? bones[defs[i].parent] : rig;
                        t = new GameObject(defs[i].name).transform;
                        t.SetParent(parent, false);
                        if (defs[i].parent >= 0)
                        {
                            BoneDef p = defs[defs[i].parent];
                            Quaternion inv = Quaternion.Inverse(p.rot);
                            t.localPosition = inv * (defs[i].pos - p.pos);
                            t.localRotation = inv * defs[i].rot;
                        }
                        else
                        {
                            t.localPosition = defs[i].pos;
                            t.localRotation = defs[i].rot;
                        }
                        t.gameObject.hideFlags = HideFlags.DontSave;
                        t.gameObject.layer = gameObject.layer;
                    }
                    bones[i] = t;
                    // bind pose in the creature's own space (the body renderer sits at the root, unrotated)
                    bind[i] = Matrix4x4.TRS(defs[i].pos, defs[i].rot, Vector3.one).inverse;
                }

                mesh = new Mesh { name = "Elliot", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
                mesh.vertices = pos;
                mesh.normals = nrm;
                mesh.tangents = tan;
                mesh.uv = uv;
                mesh.boneWeights = weights;
                mesh.bindposes = bind;
                mesh.subMeshCount = subs.Count;
                for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s, true);
                mesh.bounds = new Bounds(new Vector3(0f, 1.3f, 0f), new Vector3(2.4f, 3f, 2.4f));

                var go = new GameObject("Body") { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
                go.transform.SetParent(transform, false);
                smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.bones = bones;
                smr.rootBone = rig;
                smr.quality = SkinQuality.Bone4;
                smr.updateWhenOffscreen = false;
                smr.localBounds = new Bounds(new Vector3(0f, 1.3f, 0f), new Vector3(3f, 3.4f, 3f));
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                smr.receiveShadows = true;
                smr.sharedMaterials = new[] { bloody && bloodySkin != null ? bloodySkin : skin, mouthParts };
            }
        }
    }
}
