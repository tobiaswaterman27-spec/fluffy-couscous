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
            Material skin = Mat("Elliot_Skin", new Color(0.72f, 0.72f, 0.70f), 0.45f);
            Material eye = Mat("Elliot_Eye", new Color(0.01f, 0.01f, 0.012f), 0.97f);
            Material mouth = Mat("Elliot_Mouth", new Color(0.06f, 0.02f, 0.02f), 0.8f);

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
            ElliotAudio audio = root.AddComponent<ElliotAudio>();
            audio.breathing = breath;
            audio.voice = voice;

            Transform rig = new GameObject("Rig").transform;
            rig.SetParent(root.transform, false);
            ElliotAnimator anim = root.AddComponent<ElliotAnimator>();
            anim.motor = motor;
            anim.audioOut = audio;

            // Joints. Limb meshes hang along -Y from their joint; spine / neck / head grow along +Y.
            anim.pelvis = Joint(rig, "Pelvis");
            Part(anim.pelvis, PrimitiveType.Capsule, new Vector3(0f, 0f, 0f), new Vector3(0.3f, 0.12f, 0.17f), skin, new Vector3(0f, 0f, 90f));
            anim.spine = Joint(rig, "Spine");
            Part(anim.spine, PrimitiveType.Capsule, new Vector3(0f, 0.3f, 0f), new Vector3(0.24f, 0.36f, 0.15f), skin);
            Part(anim.spine, PrimitiveType.Sphere, new Vector3(0f, 0.5f, 0.01f), new Vector3(0.36f, 0.26f, 0.19f), skin); // narrow ribcage
            anim.neck = Joint(rig, "Neck");
            Part(anim.neck, PrimitiveType.Capsule, new Vector3(0f, 0.1f, 0f), new Vector3(0.07f, 0.13f, 0.07f), skin);
            anim.head = Joint(rig, "Head");
            Part(anim.head, PrimitiveType.Sphere, new Vector3(0f, 0.17f, 0.01f), new Vector3(0.19f, 0.33f, 0.22f), skin);    // stretched skull
            Part(anim.head, PrimitiveType.Sphere, new Vector3(-0.046f, 0.205f, 0.095f), new Vector3(0.052f, 0.06f, 0.03f), eye);  // eyes: dark, no pupils,
            Part(anim.head, PrimitiveType.Sphere, new Vector3(0.048f, 0.19f, 0.096f), new Vector3(0.05f, 0.064f, 0.03f), eye);   // one set lower
            Part(anim.head, PrimitiveType.Cube, new Vector3(0f, 0.07f, 0.104f), new Vector3(0.09f, 0.008f, 0.01f), mouth);         // too-wide mouth slit
            anim.jaw = Joint(anim.head, "Jaw", new Vector3(0f, 0.075f, 0.02f));
            Part(anim.jaw, PrimitiveType.Sphere, new Vector3(0f, -0.04f, 0.04f), new Vector3(0.14f, 0.08f, 0.15f), skin);
            Part(anim.jaw, PrimitiveType.Cube, new Vector3(0f, -0.005f, 0.07f), new Vector3(0.08f, 0.012f, 0.06f), mouth);

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
                Transform th = Joint(rig, "Thigh" + side);
                Part(th, PrimitiveType.Capsule, new Vector3(0f, -0.34f, 0f), new Vector3(0.1f, 0.36f, 0.1f), skin);
                Transform sh = Joint(rig, "Shin" + side);
                Part(sh, PrimitiveType.Capsule, new Vector3(0f, -0.33f, 0f), new Vector3(0.08f, 0.35f, 0.08f), skin);
                Transform ft = Joint(rig, "Foot" + side);
                Part(ft, PrimitiveType.Cube, new Vector3(0f, -0.03f, 0.08f), new Vector3(0.08f, 0.05f, 0.27f), skin);
                if (s < 0) { anim.upperArmL = ua; anim.foreArmL = fa; anim.handL = hand; anim.thighL = th; anim.shinL = sh; anim.footL = ft; }
                else { anim.upperArmR = ua; anim.foreArmR = fa; anim.handR = hand; anim.thighR = th; anim.shinR = sh; anim.footR = ft; }
            }

            EditorBuild.SetLayerRecursive(root, GameLayers.CreatureLayer);
            return root;
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
            return root;
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
