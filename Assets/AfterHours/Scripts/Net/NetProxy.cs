using UnityEngine;
using AfterHours.Controls;
using AfterHours.Core;
using AfterHours.Creature;
using AfterHours.Items;
using AfterHours.Player;
using AfterHours.Rendering;
using AfterHours.World;

namespace AfterHours.Net
{
    /// <summary>
    /// Another player, as seen on this machine: a plain, featureless figure in their colour, moved smoothly to where
    /// their own game says they are (interpolated ~100 ms behind, extrapolated briefly if packets are late).
    /// It is a real PlayerController, so Elliot sees, hears and catches it exactly like a local player.
    /// </summary>
    public class NetProxy : MonoBehaviour
    {
        public byte playerId;
        public PlayerController controller;
        public PlayerMotor motor;
        public Transform head;
        public VoicePlayer voice;
        public CapsuleCollider body;

        struct Snap
        {
            public float time;
            public Vector3 pos, vel, knee, feet;
            public float yaw, pitch, height, stride;
            public Stance stance;
            public MoveMode mode;
            public bool hidden, backward, legs;
            public SurfaceType surface;
        }

        readonly Snap[] buffer = new Snap[16];
        int count;
        const float Delay = 0.1f;

        /// <summary>Makes a remote-player body by copying the local player and stripping everything "local".</summary>
        public static NetProxy Create(PlayerController local, byte id, string name, Color colour)
        {
            GameObject go = Instantiate(local.gameObject, local.transform.position + Vector3.right * (1.5f + id), local.transform.rotation);
            go.name = "Player " + id + " (" + name + ")";
            PlayerController pc = go.GetComponent<PlayerController>();
            pc.isLocallyControlled = false;
            foreach (Camera c in go.GetComponentsInChildren<Camera>(true))
            {
                // Remove what depends on the camera first (immediately), then the camera itself.
                EyeAdaptation ea = c.GetComponent<EyeAdaptation>();
                if (ea != null) DestroyImmediate(ea);
                DustMotes dm = c.GetComponent<DustMotes>();
                if (dm != null) DestroyImmediate(dm);
            }
            foreach (AudioListener l in go.GetComponentsInChildren<AudioListener>(true)) Destroy(l);
            foreach (Camera c in go.GetComponentsInChildren<Camera>(true)) Destroy(c);
            LocalPlayerInput input = go.GetComponent<LocalPlayerInput>();
            if (input != null) Destroy(input);
            // Your breathing, heart, music and ambience belong to you, not to copies of you.
            foreach (MonoBehaviour m in go.GetComponents<MonoBehaviour>())
                if (m is AfterHours.Audio.PlayerAudio || m is AfterHours.Audio.TensionMusic || m is AfterHours.Audio.AmbientEvents) DestroyImmediate(m);

            // Visible to everyone (the local body layer is hidden from its owner's camera only), in their colour.
            Material mat = null;
            PlayerBodyVisual body = go.GetComponentInChildren<PlayerBodyVisual>(true);
            if (body != null)
            {
                foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
                {
                    r.gameObject.layer = 0;
                    if (mat == null) { mat = new Material(r.sharedMaterial); mat.color = colour; if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour); }
                    r.sharedMaterial = mat;
                }
            }
            // with the real body, the debug capsule stays hidden; the body wears their colour
            PlayerBody real = go.GetComponent<PlayerBody>();
            if (real != null)
            {
                if (body != null) foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            }
            NetProxy proxy = go.AddComponent<NetProxy>();
            proxy.playerId = id;
            proxy.controller = pc;
            proxy.motor = go.GetComponent<PlayerMotor>();
            GameObject headGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(headGo.GetComponent<Collider>());
            headGo.name = "Head";
            headGo.transform.SetParent(go.transform, false);
            headGo.transform.localScale = Vector3.one * 0.24f;
            if (mat != null) headGo.GetComponent<Renderer>().sharedMaterial = mat;
            if (real != null) headGo.GetComponent<Renderer>().enabled = false;   // only a voice source now
            proxy.head = headGo.transform;
            proxy.voice = headGo.AddComponent<VoicePlayer>();
            // Something solid to bump into (a little slimmer than a real body, so squeezing past is possible).
            proxy.body = go.AddComponent<CapsuleCollider>();
            proxy.body.radius = 0.24f;
            proxy.body.height = 1.8f;
            proxy.body.center = new Vector3(0f, 0.9f, 0f);
            return proxy;
        }

        public void Push(float time, Vector3 pos, float yaw, float pitch, Stance stance, float height, Vector3 vel, MoveMode mode, float stride,
                         bool hidden, bool backward, bool legs, Vector3 knee, Vector3 feet, SurfaceType surface)
        {
            if (count > 0 && time <= buffer[(count - 1) % buffer.Length].time) return;
            buffer[count % buffer.Length] = new Snap
            {
                time = time, pos = pos, yaw = yaw, pitch = pitch, stance = stance, height = height, vel = vel, mode = mode,
                stride = stride, hidden = hidden, backward = backward, legs = legs, knee = knee, feet = feet, surface = surface
            };
            count++;
        }

        float clockOffset = float.NaN;
        bool shownHidden;

        void Update()
        {
            if (count == 0 || controller == null) return;
            if (CatchSequence.IsBeingCaught(controller)) return; // the catch sequence moves the body
            Snap latest = buffer[(count - 1) % buffer.Length];
            if (float.IsNaN(clockOffset)) clockOffset = latest.time - Time.unscaledTime;
            clockOffset = Mathf.Lerp(clockOffset, latest.time - Time.unscaledTime, 0.02f);
            float renderTime = Time.unscaledTime + clockOffset - Delay;

            Snap a = latest, b = latest;
            float t = 0f;
            int n = Mathf.Min(count, buffer.Length);
            for (int i = 1; i < n; i++)
            {
                Snap older = buffer[(count - 1 - i) % buffer.Length];
                Snap newer = buffer[(count - i) % buffer.Length];
                if (older.time <= renderTime && renderTime <= newer.time)
                {
                    a = older;
                    b = newer;
                    t = Mathf.InverseLerp(older.time, newer.time, renderTime);
                    break;
                }
            }
            Vector3 pos;
            if (renderTime > latest.time) pos = latest.pos + latest.vel * Mathf.Min(renderTime - latest.time, 0.25f); // late: extrapolate a little
            else pos = Vector3.Lerp(a.pos, b.pos, t);
            float yaw = Mathf.LerpAngle(a.yaw, b.yaw, t);
            float height = Mathf.Lerp(a.height, b.height, t);
            Snap s = t < 0.5f ? a : b;
            controller.IsHidden = s.hidden;
            motor.ApplyRemoteState(pos, yaw, s.stance, height, Vector3.Lerp(a.vel, b.vel, t), s.mode, Mathf.Lerp(a.stride, b.stride, t),
                s.backward, s.legs, Vector3.Lerp(a.knee, b.knee, t), Vector3.Lerp(a.feet, b.feet, t), s.surface);
            if (body != null)
            {
                body.enabled = !s.hidden;
                body.height = Mathf.Max(0.5f, height);
                body.center = new Vector3(0f, body.height * 0.5f, 0f);
                body.radius = Mathf.Min(0.24f, body.height * 0.5f);
            }
            if (head != null)
            {
                head.gameObject.SetActive(!s.hidden);
                head.position = pos + Vector3.up * (height - 0.1f) + (s.legs ? Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 0.25f : Vector3.zero);
                head.rotation = Quaternion.Euler(Mathf.LerpAngle(a.pitch, b.pitch, t), yaw, 0f);
            }
            if (s.hidden != shownHidden)
            {
                shownHidden = s.hidden;
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = !s.hidden;
            }
        }
    }
}
