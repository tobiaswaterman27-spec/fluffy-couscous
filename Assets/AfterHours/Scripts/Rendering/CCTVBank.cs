using UnityEngine;
using UnityEngine.Rendering.Universal;
using AfterHours.Core;

namespace AfterHours.Rendering
{
    /// <summary>
    /// The security office's monitor wall: real, live feeds from the cameras around the mall. Each camera renders
    /// a tiny low-resolution frame in turn (a few frames a second each, like a cheap multiplexed system), only
    /// while someone is in the room and the office has power. Elliot shows up on them. So do you.
    /// </summary>
    public class CCTVBank : MonoBehaviour
    {
        public Camera[] cameras;
        public Renderer[] screens;
        public TextMesh[] captions;
        public string[] names;
        public Light glow;
        [Tooltip("Index of a feed that shows only snow (a dead camera).")] public int deadFeed = -1;
        public int width = 192, height = 144;
        public float activeRange = 12f;

        RenderTexture[] targets;
        Material[] mats;
        int current = -1, next;
        float stepTimer, captionTimer, level;
        float glowBase;

        void Start()
        {
            int n = cameras != null ? cameras.Length : 0;
            targets = new RenderTexture[n];
            mats = new Material[screens != null ? screens.Length : 0];
            Shader sh = Shader.Find("AfterHours/CCTVScreen");
            int mask = ~0;
            int localBody = GameLayers.LocalBodyLayer;
            if (localBody >= 0) mask &= ~(1 << localBody);     // the local player's own body has no head
            for (int i = 0; i < n; i++)
            {
                Camera c = cameras[i];
                if (c == null) continue;
                targets[i] = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32) { name = "CCTV " + i, filterMode = FilterMode.Bilinear };
                c.targetTexture = targets[i];
                c.enabled = false;
                c.cullingMask = mask;
                c.nearClipPlane = 0.08f;
                c.farClipPlane = 70f;
                c.fieldOfView = 68f;
                var data = c.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.renderShadows = false;
                    data.renderPostProcessing = false;
                    data.antialiasing = AntialiasingMode.None;
                    data.requiresDepthTexture = false;
                    data.requiresColorTexture = false;
                }
            }
            for (int i = 0; i < mats.Length; i++)
            {
                if (screens[i] == null) continue;
                mats[i] = sh != null ? new Material(sh) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                if (i < n && targets[i] != null) mats[i].SetTexture("_BaseMap", targets[i]);
                mats[i].SetFloat("_Seed", i * 0.137f);
                mats[i].SetFloat("_Signal", i == deadFeed || i >= n ? 0f : 1f);
                screens[i].sharedMaterial = mats[i];
            }
            if (glow != null) glowBase = glow.intensity;
        }

        void Update()
        {
            bool powered = AfterHours.Power.PowerGrid.LightFactorAt(transform.position) > 0.5f;
            level = Mathf.MoveTowards(level, powered ? 1f : 0f, Time.deltaTime * (powered ? 0.8f : 5f));
            foreach (Material m in mats) if (m != null) m.SetFloat("_Level", level * (0.9f + 0.1f * Mathf.PerlinNoise(Time.time * 3f, 0f)));
            if (glow != null) glow.intensity = glowBase * level;
            if (captions != null) foreach (TextMesh t in captions) if (t != null) t.gameObject.SetActive(level > 0.3f);

            Camera viewer = Camera.main;
            bool near = powered && viewer != null && (viewer.transform.position - transform.position).sqrMagnitude < activeRange * activeRange;
            if (!near)
            {
                if (current >= 0 && cameras[current] != null) cameras[current].enabled = false;
                current = -1;
                return;
            }

            // one camera at a time, round robin
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f && cameras.Length > 0)
            {
                stepTimer = 0.07f;
                if (current >= 0 && cameras[current] != null) cameras[current].enabled = false;
                for (int k = 0; k < cameras.Length; k++)
                {
                    int i = (next + k) % cameras.Length;
                    if (i == deadFeed || cameras[i] == null) continue;
                    current = i;
                    cameras[i].enabled = true;
                    next = i + 1;
                    break;
                }
            }

            captionTimer -= Time.deltaTime;
            if (captionTimer <= 0f && captions != null)
            {
                captionTimer = 1f;
                float clock = 1f * 3600f + 13f * 60f + Time.timeSinceLevelLoad;   // the recorder's clock: 01:13 when you arrived
                int hh = (int)(clock / 3600f) % 24, mm = (int)(clock / 60f) % 60, ss = (int)clock % 60;
                string time = hh.ToString("00") + ":" + mm.ToString("00") + ":" + ss.ToString("00");
                for (int i = 0; i < captions.Length; i++)
                    if (captions[i] != null)
                        captions[i].text = (names != null && i < names.Length ? names[i] : "CAM " + (i + 1)) + "\n" + (i == deadFeed ? "NO SIGNAL" : time);
            }
        }

        void OnDestroy()
        {
            if (targets != null) foreach (RenderTexture t in targets) if (t != null) t.Release();
        }
    }
}
