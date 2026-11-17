using System.IO;
using System.Text;
using UnityEngine;
using AfterHours.Creature;

namespace AfterHours.Player
{
    /// <summary>
    /// The player's body: a sculpted, skinned person (hoodie, jeans, trainers; tools/elliotgen/person.py), built at
    /// runtime from Resources/Player, with its own skeleton (PlayerBodyAnimator moves it).
    ///
    /// Your own body (first person): you see your arms, hands, legs and feet; your head is not drawn for you but
    /// still casts your shadow. Everyone else: the whole body. Which person it is comes from Settings (or, for
    /// co-op players, from the network).
    /// </summary>
    public class PlayerBody : MonoBehaviour
    {
        public PlayerController controller;
        public Transform rig;
        public Transform[] bones;
        public string[] boneNames;

        SkinnedMeshRenderer body, head;
        Mesh bodyMesh, headMesh;
        bool builtLocal;
        string builtId;
        /// <summary>Which person this is (Resources/Player/Characters/&lt;id&gt;). Remote players: set by the network.</summary>
        public string characterId = "";
        /// <summary>Rest pose of every joint, in the body's own space (the animator reads proportions from it).</summary>
        public readonly System.Collections.Generic.Dictionary<string, (Vector3 pos, Quaternion rot)> Rest = new System.Collections.Generic.Dictionary<string, (Vector3, Quaternion)>();
        public int Version { get; private set; }
        /// <summary>Your own pick (single player and co-op).</summary>
        public static System.Func<string> LocalCharacterOverride;

        static readonly System.Collections.Generic.Dictionary<string, Material[]> mats = new System.Collections.Generic.Dictionary<string, Material[]>();

        public bool Ready => body != null;

        public Transform Bone(string name)
        {
            if (boneNames == null) return null;
            for (int i = 0; i < boneNames.Length; i++) if (boneNames[i] == name) return bones[i];
            return null;
        }

        void Start()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            Rebuild();
        }

        void LateUpdate()
        {
            // a proxy made by copying the local player starts out as a copy of the local body: rebuild it as a remote one
            bool local = controller != null && controller.isLocallyControlled;
            if (local)
            {
                string want = LocalCharacterOverride?.Invoke() ?? AfterHours.Settings.SettingsService.CurrentOrDefault.character;
                if (!string.IsNullOrEmpty(want)) characterId = want;
            }
            if (body == null || local != builtLocal || characterId != builtId) Rebuild();

        }

        void OnDestroy()
        {
            if (bodyMesh != null) Destroy(bodyMesh);
            if (headMesh != null) Destroy(headMesh);
        }

        static Material Lit(string tex, bool normal, bool tint)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(sh != null ? sh : Shader.Find("Standard"));
            Texture2D a = Resources.Load<Texture2D>(tex + "_A");
            if (a != null) { m.SetTexture("_BaseMap", a); m.mainTexture = a; }
            if (normal)
            {
                Texture2D n = Resources.Load<Texture2D>(tex + "_N");
                if (n != null) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
                Texture2D mk = Resources.Load<Texture2D>(tex + "_M");
                if (mk != null)
                {
                    m.SetTexture("_MetallicGlossMap", mk); m.EnableKeyword("_METALLICSPECGLOSSMAP");
                    m.SetTexture("_OcclusionMap", mk); m.EnableKeyword("_OCCLUSIONMAP");
                    m.SetFloat("_Smoothness", 1f);
                }
            }
            m.SetFloat("_Metallic", 0f);
            return m;
        }

        static string Str(BinaryReader r) => Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));

        void Rebuild()
        {
            if (string.IsNullOrEmpty(characterId)) characterId = PlayerCharacters.Default;
            string dir = "Player/Characters/" + characterId + "/player_" + characterId;
            TextAsset file = Resources.Load<TextAsset>(dir + "_model");
            if (file == null) { characterId = PlayerCharacters.Default; dir = "Player/Characters/" + characterId + "/player_" + characterId; file = Resources.Load<TextAsset>(dir + "_model"); }
            if (file == null) return;
            if (!mats.TryGetValue(characterId, out Material[] set))
            {
                Material eye = Lit(dir + "_eye", false, false); eye.SetFloat("_Smoothness", 0.92f);
                Material skinM = Lit(dir, true, false);
                set = new[] { skinM, skinM, eye };
                mats[characterId] = set;
            }
            Material clothMat = set[0], skinMat = set[1], eyeMat = set[2];
            // a different person: a different skeleton (proportions) - start the rig again
            if (builtId != null && builtId != characterId && rig != null) { Destroy(rig.gameObject); rig = null; bones = null; }
            foreach (Transform t in new[] { transform.Find("PlayerBodyMesh"), transform.Find("PlayerHeadMesh") })
                if (t != null) Destroy(t.gameObject);
            bool local = controller != null && controller.isLocallyControlled;

            using (var r = new BinaryReader(new MemoryStream(file.bytes)))
            {
                r.ReadBytes(4);
                int nb = r.ReadInt32();
                var names = new string[nb]; var parents = new int[nb]; var pos = new Vector3[nb]; var rot = new Quaternion[nb];
                for (int i = 0; i < nb; i++)
                {
                    names[i] = Str(r); parents[i] = r.ReadInt32();
                    pos[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    rot[i] = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                }
                Rest.Clear();
                for (int i = 0; i < nb; i++) Rest[names[i]] = (pos[i], rot[i]);
                // skeleton (kept across rebuilds)
                if (rig == null)
                {
                    Transform old = transform.Find("PlayerRig");
                    rig = old != null ? old : new GameObject("PlayerRig").transform;
                    rig.SetParent(transform, false);
                }
                if (bones == null || bones.Length != nb || bones[0] == null)
                {
                    bones = new Transform[nb];
                    for (int i = 0; i < nb; i++)
                    {
                        Transform existing = FindDeep(rig, names[i]);
                        Transform t = existing != null ? existing : new GameObject(names[i]).transform;
                        if (existing == null)
                        {
                            t.SetParent(parents[i] >= 0 ? bones[parents[i]] : rig, false);
                            if (parents[i] >= 0)
                            {
                                Quaternion inv = Quaternion.Inverse(rot[parents[i]]);
                                t.localPosition = inv * (pos[i] - pos[parents[i]]);
                                t.localRotation = inv * rot[i];
                            }
                            else { t.localPosition = pos[i]; t.localRotation = rot[i]; }
                        }
                        bones[i] = t;
                    }
                    boneNames = names;
                }
                var bind = new Matrix4x4[nb];
                for (int i = 0; i < nb; i++) bind[i] = Matrix4x4.TRS(pos[i], rot[i], Vector3.one).inverse;

                int nv = r.ReadInt32();
                var vp = new Vector3[nv]; var vn = new Vector3[nv]; var vt = new Vector4[nv]; var uv = new Vector2[nv];
                for (int i = 0; i < nv; i++) vp[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                for (int i = 0; i < nv; i++) vn[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                for (int i = 0; i < nv; i++) vt[i] = new Vector4(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                for (int i = 0; i < nv; i++) uv[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
                var bi = new int[nv * 4];
                for (int i = 0; i < nv * 4; i++) bi[i] = r.ReadUInt16();
                var bw = new BoneWeight[nv];
                for (int i = 0; i < nv; i++)
                    bw[i] = new BoneWeight { boneIndex0 = bi[i * 4], weight0 = r.ReadSingle(), boneIndex1 = bi[i * 4 + 1], weight1 = r.ReadSingle(),
                                             boneIndex2 = bi[i * 4 + 2], weight2 = r.ReadSingle(), boneIndex3 = bi[i * 4 + 3], weight3 = r.ReadSingle() };
                int ns = r.ReadInt32();
                var subs = new int[ns][];
                for (int s = 0; s < ns; s++)
                {
                    Str(r);
                    int n = r.ReadInt32();
                    subs[s] = new int[n];
                    for (int i = 0; i < n; i++) subs[s][i] = (int)r.ReadUInt32();
                }

                Mesh Make(string nm, int[] which)
                {
                    var m = new Mesh { name = nm, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    m.vertices = vp; m.normals = vn; m.tangents = vt; m.uv = uv; m.boneWeights = bw; m.bindposes = bind;
                    m.subMeshCount = which.Length;
                    for (int k = 0; k < which.Length; k++) m.SetTriangles(subs[which[k]], k, true);
                    return m;
                }
                SkinnedMeshRenderer Renderer(string nm, Mesh m, Material[] mats)
                {
                    var go = new GameObject(nm);
                    go.transform.SetParent(transform, false);
                    var smr = go.AddComponent<SkinnedMeshRenderer>();
                    smr.sharedMesh = m; smr.bones = bones; smr.rootBone = rig; smr.quality = SkinQuality.Bone4;
                    smr.localBounds = new Bounds(new Vector3(0f, 0.9f, 0f), new Vector3(2.4f, 2.4f, 2.4f));
                    smr.sharedMaterials = mats;
                    return smr;
                }
                if (bodyMesh != null) Destroy(bodyMesh);
                if (headMesh != null) Destroy(headMesh);
                headMesh = null;
                if (local)
                {
                    // you: body without the head (it would fill your view); the head still throws your shadow
                    bodyMesh = Make("Player body", new[] { 0, 1 });
                    body = Renderer("PlayerBodyMesh", bodyMesh, new[] { clothMat, skinMat });
                    headMesh = Make("Player head", new[] { 2, 3 });
                    head = Renderer("PlayerHeadMesh", headMesh, new[] { skinMat, eyeMat });
                    head.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
                else
                {
                    bodyMesh = Make("Player", new[] { 0, 1, 2, 3 });
                    body = Renderer("PlayerBodyMesh", bodyMesh, new[] { clothMat, skinMat, skinMat, eyeMat });
                    head = null;
                }
                builtLocal = local;
                builtId = characterId;
                // see from this person's own eye height (a short person sees the world from lower down)
                if (controller != null && controller.Motor != null && Rest.ContainsKey("EyeL"))
                {
                    PlayerMotor m = controller.Motor;
                    m.eyeBelowTop = Mathf.Clamp(m.standHeight - Rest["EyeL"].pos.y, 0.05f, 0.4f);
                }
                Version++;
            }
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++) { Transform f = FindDeep(t.GetChild(i), name); if (f != null) return f; }
            return null;
        }
    }
}
