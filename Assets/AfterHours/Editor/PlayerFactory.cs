using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using AfterHours.Controls;
using AfterHours.Rendering;
using AfterHours.Core;
using AfterHours.DevTools;
using AfterHours.Interaction;
using AfterHours.Items;
using AfterHours.Player;

namespace AfterHours.EditorTools
{
    /// <summary>Builds the complete player object (and saves it as a prefab).</summary>
    public static class PlayerFactory
    {
        public const string PrefabPath = EditorBuild.Root + "/Prefabs/Player.prefab";

        public static GameObject CreatePlayer(Vector3 position, float yaw)
        {
            var go = new GameObject("Player");
            go.tag = "Player";
            EditorBuild.SetLayerRecursive(go, GameLayers.PlayerLayer);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            CharacterController cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.03f;
            cc.minMoveDistance = 0f;
            cc.slopeLimit = 46f;

            PlayerMotor motor = go.AddComponent<PlayerMotor>();
            PlayerLook look = go.AddComponent<PlayerLook>();
            go.AddComponent<LocalPlayerInput>();
            go.AddComponent<PlayerController>();

            go.AddComponent<AudioSource>();
            FootstepAudio steps = go.AddComponent<FootstepAudio>();
            go.AddComponent<WetFootprints>().template = ArtLibrary.Decal("Puddle", "puddle", Color.white, 0.95f);
            steps.motor = motor;

            // Camera rig
            var rig = new GameObject("CameraRig").transform;
            rig.SetParent(go.transform, false);
            rig.localPosition = new Vector3(0f, 1.65f, 0f);

            var camGo = new GameObject("PlayerCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rig, false);
            Camera cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 400f;
            cam.fieldOfView = 72f;
            camGo.AddComponent<AudioListener>();
            UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camData.dithering = true;
            // Default volumes plus the power-cut areas (on the trigger-zone layer, so they never block rays).
            camData.volumeLayerMask = 1 | (GameLayers.TriggerZoneLayer >= 0 ? 1 << GameLayers.TriggerZoneLayer : 0);
            camGo.AddComponent<EyeAdaptation>();
            camGo.AddComponent<DustMotes>().material = DustMaterial();
            int bodyLayer = GameLayers.LocalBodyLayer;
            if (bodyLayer >= 0) cam.cullingMask &= ~(1 << bodyLayer);

            look.cameraRig = rig;
            look.playerCamera = cam;

            // Debug body that shows the real collision shape (F5)
            Material bodyMat = EditorBuild.Material(EditorBuild.Root + "/Materials/Dev", "PlayerDebugBody", new Color(0.35f, 0.42f, 0.5f), 0.2f);
            var visualRoot = new GameObject("BodyVisual");
            visualRoot.transform.SetParent(go.transform, false);
            PlayerBodyVisual bodyVisual = visualRoot.AddComponent<PlayerBodyVisual>();
            bodyVisual.motor = motor;
            bodyVisual.torso = MakeDebugCapsule("Torso", visualRoot.transform, bodyMat).transform;
            bodyVisual.thigh = MakeDebugCapsule("Thigh", visualRoot.transform, bodyMat).transform;
            bodyVisual.shin = MakeDebugCapsule("Shin", visualRoot.transform, bodyMat).transform;
            bodyVisual.thigh.gameObject.SetActive(false);
            bodyVisual.shin.gameObject.SetActive(false);
            EditorBuild.SetLayerRecursive(visualRoot, bodyLayer);

            PlayerGrab grab = go.AddComponent<PlayerGrab>();
            grab.motor = motor;
            grab.look = look;

            PlayerInteractor interactor = go.AddComponent<PlayerInteractor>();
            interactor.motor = motor;
            interactor.look = look;
            interactor.grab = grab;

            var hand = new GameObject("HandAnchor").transform;
            hand.SetParent(camGo.transform, false);
            hand.localPosition = new Vector3(0.26f, -0.26f, 0.48f);
            hand.localRotation = Quaternion.Euler(10f, -15f, 0f);
            PlayerInventory inventory = go.AddComponent<PlayerInventory>();
            inventory.handAnchor = hand;
            inventory.look = look;
            inventory.motor = motor;
            inventory.interactor = interactor;

            PlayerAnimationDriver anim = go.AddComponent<PlayerAnimationDriver>();
            anim.motor = motor;
            anim.grab = grab;

            DevOverlay overlay = go.AddComponent<DevOverlay>();
            overlay.motor = motor;
            overlay.look = look;

            return go;
        }

        public static void SaveAsPrefab(GameObject player)
        {
            EditorBuild.EnsureFolder(EditorBuild.Root + "/Prefabs");
            PrefabUtility.SaveAsPrefabAssetAndConnect(player, PrefabPath, InteractionMode.AutomatedAction);
        }

        /// <summary>Soft additive specks for the dust in the air.</summary>
        static Material DustMaterial()
        {
            const string path = EditorBuild.Root + "/Materials/Dev/Dust_Mote.mat";
            EditorBuild.EnsureFolder(EditorBuild.Root + "/Materials/Dev");
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (m == null)
            {
                m = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(m, path);
            }
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(EditorBuild.Root + "/Textures/Generated/decal_dust_mote.png");
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject MakeDebugCapsule(string name, Transform parent, Material mat)
        {
            GameObject c = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            c.name = name;
            Object.DestroyImmediate(c.GetComponent<Collider>()); // visual only, never collides
            c.transform.SetParent(parent, false);
            c.GetComponent<Renderer>().sharedMaterial = mat;
            return c;
        }
    }
}
