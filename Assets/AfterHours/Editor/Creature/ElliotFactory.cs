using UnityEditor;
using UnityEngine;
using AfterHours.Core;
using AfterHours.Creature;

namespace AfterHours.EditorTools
{
    /// <summary>Builds Elliot: skeleton of joints with smooth body parts, movement capsule, animator and sound.</summary>
    public static class ElliotFactory
    {
        const string MatFolder = EditorBuild.Root + "/Materials/Creature";

        public static GameObject Create(Transform parent, Vector3 position, float yaw, string name = "Elliot")
        {
            var root = new GameObject(name);
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            EditorBuild.SetLayerRecursive(root, GameLayers.CreatureLayer);

            CharacterController cc = root.AddComponent<CharacterController>();
            cc.height = 2.55f;
            cc.radius = 0.32f;
            cc.center = new Vector3(0f, 1.275f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.45f;
            cc.skinWidth = 0.04f;
            cc.minMoveDistance = 0f;
            ElliotMotor motor = root.AddComponent<ElliotMotor>();

            var voiceGo = new GameObject("Voice");
            voiceGo.transform.SetParent(root.transform, false);
            voiceGo.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            AudioSource breath = voiceGo.AddComponent<AudioSource>();
            breath.playOnAwake = false;
            AudioSource voice = voiceGo.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voiceGo.AddComponent<AfterHours.Audio.AudioOcclusion>();
            ElliotAudio audio = root.AddComponent<ElliotAudio>();
            audio.breathing = breath;
            audio.voice = voice;

            Transform rig = new GameObject("Rig").transform;
            rig.SetParent(root.transform, false);
            ElliotAnimator anim = root.AddComponent<ElliotAnimator>();
            anim.motor = motor;
            anim.audioOut = audio;

            // His body: the sculpted, skinned model (tools/elliotgen) when it is there; the old primitive body if not.
            TextAsset model = AssetDatabase.LoadAssetAtPath<TextAsset>(EditorBuild.Root + "/Models/Elliot/elliot_model.bytes");
            if (model != null) SculptedBody(root, rig, anim, model);
            else PrimitiveBody(rig, anim);

            EditorBuild.SetLayerRecursive(root, GameLayers.CreatureLayer);
            return root;
        }

        /// <summary>The original body made of primitives (used only if the sculpted model file is missing).</summary>
        static void PrimitiveBody(Transform rig, ElliotAnimator anim)
        {
            // Textured skin: grey-yellow, blotched, veined, unevenly wet (tools/texgen/details.py).
            Material skin = ArtLibrary.Surface("Elliot_Skin", "skin_pale", 0.4f, new Color(0.95f, 0.95f, 0.93f), 1f, 1.2f);
            skin.SetTextureScale("_BaseMap", new Vector2(2f, 3f));
            if (skin.HasProperty("_BumpMap")) skin.SetTextureScale("_BumpMap", new Vector2(2f, 3f));
            Material nail = Mat("Elliot_Nail", new Color(0.18f, 0.16f, 0.13f), 0.7f);
            Material shadowSkin = Mat("Elliot_Hollow", new Color(0.45f, 0.43f, 0.42f), 0.35f);
            Material eye = Mat("Elliot_Eye", new Color(0.01f, 0.01f, 0.012f), 0.97f);
            Material mouth = Mat("Elliot_Mouth", new Color(0.06f, 0.02f, 0.02f), 0.8f);

            // Joints. Limb meshes hang along -Y from their joint; spine / neck / head grow along +Y.
            anim.pelvis = Joint(rig, "Pelvis");
            Part(anim.pelvis, PrimitiveType.Capsule, new Vector3(0f, 0f, 0f), new Vector3(0.3f, 0.12f, 0.17f), skin, new Vector3(0f, 0f, 90f));
            anim.spine = Joint(rig, "Spine");
            Part(anim.spine, PrimitiveType.Capsule, new Vector3(0f, 0.3f, 0f), new Vector3(0.24f, 0.36f, 0.15f), skin);
            Part(anim.spine, PrimitiveType.Sphere, new Vector3(0f, 0.5f, 0.01f), new Vector3(0.36f, 0.26f, 0.19f), skin); // narrow ribcage
            // Emaciated: every rib, the knobs of the spine, collarbones, hip bones showing through.
            for (int i = 0; i < 6; i++)
            {
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    float yy = 0.37f + i * 0.045f, half = 0.11f - i * 0.004f;
                    Part(anim.spine, PrimitiveType.Capsule, new Vector3(sd * 0.075f, yy, 0.075f - Mathf.Abs(i - 2.5f) * 0.004f),
                        new Vector3(0.016f, half, 0.016f), skin, new Vector3(0f, sd * -28f, 90f + sd * 8f));
                }
            }
            for (int i = 0; i < 9; i++)
                Part(anim.spine, PrimitiveType.Sphere, new Vector3(0f, 0.08f + i * 0.065f, -0.078f), new Vector3(0.032f, 0.028f, 0.03f), skin);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Part(anim.spine, PrimitiveType.Capsule, new Vector3(sd * 0.09f, 0.63f, 0.05f), new Vector3(0.018f, 0.09f, 0.018f), skin, new Vector3(0f, 0f, sd * 80f));
                Part(anim.pelvis, PrimitiveType.Sphere, new Vector3(sd * 0.11f, 0.03f, 0.06f), new Vector3(0.06f, 0.05f, 0.04f), skin);
                Part(anim.spine, PrimitiveType.Sphere, new Vector3(sd * 0.06f, 0.3f, 0.06f), new Vector3(0.08f, 0.12f, 0.03f), shadowSkin); // sunken belly sides
            }
            anim.neck = Joint(rig, "Neck");
            Part(anim.neck, PrimitiveType.Capsule, new Vector3(0f, 0.1f, 0f), new Vector3(0.07f, 0.13f, 0.07f), skin);
            anim.head = Joint(rig, "Head");
            Part(anim.head, PrimitiveType.Sphere, new Vector3(0f, 0.17f, 0.01f), new Vector3(0.19f, 0.33f, 0.22f), skin);    // stretched skull
            Part(anim.head, PrimitiveType.Sphere, new Vector3(-0.046f, 0.205f, 0.095f), new Vector3(0.052f, 0.06f, 0.03f), eye);  // eyes: dark, no pupils,
            Part(anim.head, PrimitiveType.Sphere, new Vector3(0.048f, 0.19f, 0.096f), new Vector3(0.05f, 0.064f, 0.03f), eye);   // one set lower
            Part(anim.head, PrimitiveType.Cube, new Vector3(0f, 0.07f, 0.104f), new Vector3(0.09f, 0.008f, 0.01f), mouth);         // too-wide mouth slit
            Part(anim.head, PrimitiveType.Sphere, new Vector3(0f, 0.245f, 0.085f), new Vector3(0.17f, 0.045f, 0.07f), skin);     // heavy brow
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Part(anim.head, PrimitiveType.Sphere, new Vector3(sd * 0.07f, 0.15f, 0.085f), new Vector3(0.05f, 0.032f, 0.04f), skin);       // cheekbones
                Part(anim.head, PrimitiveType.Sphere, new Vector3(sd * 0.075f, 0.12f, 0.06f), new Vector3(0.04f, 0.05f, 0.04f), shadowSkin);  // hollow cheeks
            }
            anim.jaw = Joint(anim.head, "Jaw", new Vector3(0f, 0.075f, 0.02f));
            Part(anim.jaw, PrimitiveType.Sphere, new Vector3(0f, -0.04f, 0.04f), new Vector3(0.14f, 0.08f, 0.15f), skin);
            Part(anim.jaw, PrimitiveType.Cube, new Vector3(0f, -0.005f, 0.07f), new Vector3(0.08f, 0.012f, 0.06f), mouth);

            // Inside the mouth: a dark wet throat and gums, and far too many thin, uneven teeth (seen up close when it
            // screams in your face and bites).
            Material gum = Mat("Elliot_Gum", new Color(0.32f, 0.07f, 0.08f), 0.85f);
            Material tooth = Mat("Elliot_Tooth", new Color(0.78f, 0.74f, 0.6f), 0.6f);
            Part(anim.head, PrimitiveType.Sphere, new Vector3(0f, 0.06f, 0.06f), new Vector3(0.11f, 0.07f, 0.09f), mouth);  // throat
            Part(anim.head, PrimitiveType.Cube, new Vector3(0f, 0.078f, 0.085f), new Vector3(0.1f, 0.012f, 0.05f), gum);   // upper gum
            Part(anim.jaw, PrimitiveType.Cube, new Vector3(0f, 0.004f, 0.066f), new Vector3(0.1f, 0.012f, 0.05f), gum);    // lower gum
            for (int i = 0; i < 11; i++)
            {
                float a = (i / 10f - 0.5f) * 2.4f;              // around the arch of the mouth
                float x = Mathf.Sin(a) * 0.05f, z = 0.07f + Mathf.Cos(a) * 0.035f;
                float len = 0.014f + ((i * 37) % 7) * 0.003f;   // uneven lengths
                float tilt = ((i * 53) % 9 - 4) * 3f;
                Part(anim.head, PrimitiveType.Capsule, new Vector3(x, 0.07f - len * 0.5f, z), new Vector3(0.006f, len, 0.006f), tooth, new Vector3(tilt, 0f, -tilt));
                Part(anim.jaw, PrimitiveType.Capsule, new Vector3(x * 0.95f, 0.01f + len * 0.45f, z - 0.004f), new Vector3(0.006f, len * 0.9f, 0.006f), tooth, new Vector3(-tilt, 0f, tilt));
            }

            for (int s = -1; s <= 1; s += 2)
            {
                string side = s < 0 ? "L" : "R";
                Transform ua = Joint(rig, "UpperArm" + side);
                Part(ua, PrimitiveType.Capsule, new Vector3(0f, -0.28f, 0f), new Vector3(0.075f, 0.3f, 0.075f), skin);
                Transform fa = Joint(rig, "ForeArm" + side);
                Part(fa, PrimitiveType.Capsule, new Vector3(0f, -0.28f, 0f), new Vector3(0.062f, 0.3f, 0.062f), skin);
                Transform hand = Joint(rig, "Hand" + side);
                Part(hand, PrimitiveType.Cube, new Vector3(0f, -0.06f, 0f), new Vector3(0.07f, 0.12f, 0.022f), skin);
                for (int f = 0; f < 4; f++)
                {
                    float x = -0.027f + f * 0.018f;
                    Part(hand, PrimitiveType.Capsule, new Vector3(x, -0.2f + (f == 0 || f == 3 ? 0.02f : 0f), 0f), new Vector3(0.012f, 0.085f, 0.012f), skin);
                }
                Part(fa, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.075f, 0.08f, 0.075f), skin);          // bony elbow
                Part(hand, PrimitiveType.Sphere, new Vector3(0f, -0.005f, 0f), new Vector3(0.06f, 0.05f, 0.035f), skin); // wrist knob
                for (int f = 0; f < 4; f++)
                {
                    float x = -0.027f + f * 0.018f;
                    float tip = -0.29f + (f == 0 || f == 3 ? 0.02f : 0f);
                    Part(hand, PrimitiveType.Sphere, new Vector3(x, -0.15f + (f == 0 || f == 3 ? 0.02f : 0f), 0f), new Vector3(0.016f, 0.018f, 0.016f), skin); // knuckles
                    Part(hand, PrimitiveType.Capsule, new Vector3(x, tip, 0.002f), new Vector3(0.009f, 0.025f, 0.007f), nail);  // long dark nails
                }
                Transform th = Joint(rig, "Thigh" + side);
                Part(th, PrimitiveType.Capsule, new Vector3(0f, -0.34f, 0f), new Vector3(0.1f, 0.36f, 0.1f), skin);
                Transform sh = Joint(rig, "Shin" + side);
                Part(sh, PrimitiveType.Capsule, new Vector3(0f, -0.33f, 0f), new Vector3(0.08f, 0.35f, 0.08f), skin);
                Part(sh, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.095f, 0.1f, 0.1f), skin);           // knee
                Transform ft = Joint(rig, "Foot" + side);
                Part(ft, PrimitiveType.Cube, new Vector3(0f, -0.03f, 0.08f), new Vector3(0.08f, 0.05f, 0.27f), skin);
                for (int toe = 0; toe < 4; toe++)  // long toes
                    Part(ft, PrimitiveType.Capsule, new Vector3(-0.027f + toe * 0.018f, -0.045f, 0.24f), new Vector3(0.014f, 0.035f, 0.014f), skin, new Vector3(90f, 0f, 0f));
                if (s < 0) { anim.upperArmL = ua; anim.foreArmL = fa; anim.handL = hand; anim.thighL = th; anim.shinL = sh; anim.footL = ft; }
                else { anim.upperArmR = ua; anim.foreArmR = fa; anim.handR = hand; anim.thighR = th; anim.shinR = sh; anim.footR = ft; }
            }

        }

        /// <summary>
        /// Joints placed in the model's rest pose, and the ElliotBody component that skins the sculpted mesh onto them
        /// (it also adds the detail joints: lips, nostrils, brows, throat, chest, tongue, fingers, toes).
        /// </summary>
        static void SculptedBody(GameObject root, Transform rig, ElliotAnimator anim, TextAsset model)
        {
            ElliotBody.BoneDef[] defs = ElliotBody.ReadBones(model.bytes);
            var made = new System.Collections.Generic.Dictionary<string, Transform>();
            foreach (ElliotBody.BoneDef d in defs)
            {
                bool main = d.parent < 0 || d.name == "Jaw";
                if (!main) continue;
                Transform t = new GameObject(d.name).transform;
                if (d.parent < 0)
                {
                    t.SetParent(rig, false);
                    t.localPosition = d.pos;
                    t.localRotation = d.rot;
                }
                else
                {
                    ElliotBody.BoneDef p = defs[d.parent];
                    t.SetParent(made[p.name], false);
                    Quaternion inv = Quaternion.Inverse(p.rot);
                    t.localPosition = inv * (d.pos - p.pos);
                    t.localRotation = inv * d.rot;
                }
                made[d.name] = t;
            }
            Transform J(string n) => made.TryGetValue(n, out Transform t) ? t : null;
            anim.pelvis = J("Pelvis"); anim.spine = J("Spine"); anim.neck = J("Neck"); anim.head = J("Head"); anim.jaw = J("Jaw");
            anim.upperArmL = J("UpperArmL"); anim.foreArmL = J("ForeArmL"); anim.handL = J("HandL");
            anim.upperArmR = J("UpperArmR"); anim.foreArmR = J("ForeArmR"); anim.handR = J("HandR");
            anim.thighL = J("ThighL"); anim.shinL = J("ShinL"); anim.footL = J("FootL");
            anim.thighR = J("ThighR"); anim.shinR = J("ShinR"); anim.footR = J("FootR");

            Texture2D normal = Tex("elliot_body_N.png");
            ElliotBody body = root.AddComponent<ElliotBody>();
            body.model = model;
            body.rig = rig;
            body.skin = SkinMaterial("Elliot_Skin", Tex("elliot_body_A.jpg"), normal, Tex("elliot_body_M.png"));
            body.mouthParts = SkinMaterial("Elliot_MouthParts", Tex("elliot_mouth_A.png"), null, Tex("elliot_mouth_M.png"));
            body.Build();
        }

        static Texture2D Tex(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>(EditorBuild.Root + "/Textures/Creature/" + file);

        /// <summary>URP Lit with the baked colour, normal and mask (G = cavity occlusion, A = wetness / smoothness).</summary>
        static Material SkinMaterial(string name, Texture2D albedo, Texture2D normal, Texture2D mask)
        {
            EditorBuild.EnsureFolder(MatFolder);
            string path = MatFolder + "/" + name + ".mat";
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else if (sh != null && m.shader != sh) m.shader = sh;
            m.SetColor("_BaseColor", Color.white);
            if (albedo != null) { m.SetTexture("_BaseMap", albedo); if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", albedo); }
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            else m.DisableKeyword("_NORMALMAP");
            if (mask != null)
            {
                m.SetTexture("_MetallicGlossMap", mask); m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetTexture("_OcclusionMap", mask); m.EnableKeyword("_OCCLUSIONMAP");
                m.SetFloat("_OcclusionStrength", 1f);
                m.SetFloat("_Smoothness", 1f);
                m.SetFloat("_SmoothnessTextureChannel", 0f);
            }
            m.SetFloat("_Metallic", 0f);
            m.enableInstancing = false;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Elliot with its full AI (senses, memory, brain, path finding).</summary>
        public static GameObject CreateWithAI(Transform parent, Vector3 position, float yaw, string name = "Elliot")
        {
            GameObject root = Create(parent, position, yaw, name);
            var agent = root.AddComponent<UnityEngine.AI.NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.9f;
            agent.speed = 6f;
            agent.angularSpeed = 0f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.3f;
            ElliotSenses senses = root.AddComponent<ElliotSenses>();
            senses.animator = root.GetComponent<ElliotAnimator>();
            ElliotMemory memory = root.AddComponent<ElliotMemory>();
            ElliotBrain brain = root.AddComponent<ElliotBrain>();
            brain.motor = root.GetComponent<ElliotMotor>();
            brain.animator = senses.animator;
            brain.senses = senses;
            brain.memory = memory;
            brain.audioOut = root.GetComponent<ElliotAudio>();
            AddGoreKit(root);
            return root;
        }

        /// <summary>Blood materials used by the catch sequence (decals, droplets, blood on the camera).</summary>
        public static void AddGoreKit(GameObject root)
        {
            var kit = root.AddComponent<AfterHours.Gore.GoreKit>();
            kit.poolMaterial = ArtLibrary.Decal("BloodPool", "blood_pool", Color.white, 0.85f);
            kit.spatterMaterial = ArtLibrary.Decal("BloodSpatter", "blood_spatter", Color.white, 0.7f);
            kit.smearMaterial = ArtLibrary.Decal("BloodSmear", "blood_smear", Color.white, 0.55f);
            kit.lensTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(EditorBuild.Root + "/Textures/Generated/decal_blood_lens.png");

            EditorBuild.EnsureFolder(MatFolder);
            string path = MatFolder + "/Blood_Drop.mat";
            Material drop = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (drop == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
                drop = new Material(sh);
                AssetDatabase.CreateAsset(drop, path);
            }
            if (drop.HasProperty("_BaseColor")) drop.SetColor("_BaseColor", new Color(0.45f, 0.02f, 0.03f, 1f));
            EditorUtility.SetDirty(drop);
            kit.dropMaterial = drop;
        }

        static Material Mat(string name, Color c, float smooth)
        {
            return EditorBuild.Material(MatFolder, name, c, smooth);
        }

        static Transform Joint(Transform parent, string name, Vector3? local = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local ?? Vector3.zero;
            return t;
        }

        static void Part(Transform joint, PrimitiveType type, Vector3 offset, Vector3 scale, Material m, Vector3? euler = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = joint.name + " mesh";
            go.transform.SetParent(joint, false);
            go.transform.localPosition = offset;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = m;
        }
    }
}
