using UnityEditor;
using UnityEngine;
using MallHorror.Controls;
using MallHorror.Core;
using MallHorror.DevTools;
using MallHorror.Player;

namespace MallHorror.EditorTools
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
            bodyVisual.legs = MakeDebugCapsule("Legs", visualRoot.transform, bodyMat).transform;
            bodyVisual.legs.gameObject.SetActive(false);
            EditorBuild.SetLayerRecursive(visualRoot, bodyLayer);

            PlayerAnimationDriver anim = go.AddComponent<PlayerAnimationDriver>();
            anim.motor = motor;

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
