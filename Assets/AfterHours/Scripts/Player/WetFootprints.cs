using System.Collections.Generic;
using UnityEngine;
using AfterHours.World;

namespace AfterHours.Player
{
    /// <summary>
    /// Walk through a puddle under a leak and your shoes are wet for the next dozen steps: dark, glossy prints on the
    /// floor, fainter with every step, drying out over a minute. Purely visual (one shared pool of quads).
    /// </summary>
    public class WetFootprints : MonoBehaviour
    {
        [Tooltip("A transparent, glossy decal material (its texture is replaced by a shoe print).")]
        public Material template;
        public int wetSteps = 12;
        public float dryAfter = 60f;

        PlayerMotor motor;
        float wet;

        class Print { public Renderer r; public float born; }
        static readonly List<Print> pool = new List<Print>();
        static int next;
        static Material printMat;
        static MaterialPropertyBlock block;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        const int PoolSize = 40;

        void Awake() { motor = GetComponent<PlayerMotor>(); }
        void OnEnable() { if (motor != null) motor.Footstep += OnStep; }
        void OnDisable() { if (motor != null) motor.Footstep -= OnStep; }

        void OnStep(FootstepInfo info)
        {
            Vector3 p = info.position;
            foreach (Drip d in Drip.All)
            {
                if (d == null) continue;
                Vector3 c = d.PuddleCentre;
                if (Mathf.Abs(c.y - p.y) < 0.6f && new Vector2(c.x - p.x, c.z - p.z).sqrMagnitude < d.puddleRadius * d.puddleRadius) { wet = 1f; return; }
            }
            if (wet <= 0f || info.mode == MoveMode.Crawl || info.mode == MoveMode.Airborne || template == null) return;
            float yaw = transform.eulerAngles.y;
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            Vector3 at = p + right * (info.foot == 0 ? -0.11f : 0.11f);
            int mask = Physics.DefaultRaycastLayers;
            foreach (int l in new[] { AfterHours.Core.GameLayers.PlayerLayer, AfterHours.Core.GameLayers.LocalBodyLayer, AfterHours.Core.GameLayers.CreatureLayer })
                if (l >= 0) mask &= ~(1 << l);
            if (!Physics.Raycast(at + Vector3.up * 0.4f, Vector3.down, out RaycastHit hit, 0.9f, mask, QueryTriggerInteraction.Ignore)) return;
            if (Vector3.Dot(hit.normal, Vector3.up) < 0.9f) return;
            Place(hit.point + hit.normal * 0.006f, yaw, info.foot, 0.6f * wet);
            wet -= 1f / Mathf.Max(1, wetSteps);
        }

        void Place(Vector3 at, float yaw, int foot, float alpha)
        {
            if (printMat == null)
            {
                printMat = new Material(template) { name = "Wet footprint" };
                Texture2D t = ShoeTexture();
                if (printMat.HasProperty("_BaseMap")) printMat.SetTexture("_BaseMap", t);
                printMat.mainTexture = t;
                if (printMat.HasProperty("_Cull")) printMat.SetFloat("_Cull", 0f);   // the left shoe is a mirrored quad
                block = new MaterialPropertyBlock();
            }
            Print pr;
            if (pool.Count < PoolSize)
            {
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Wet footprint";
                Destroy(q.GetComponent<Collider>());
                Renderer r = q.GetComponent<Renderer>();
                r.sharedMaterial = printMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pr = new Print { r = r };
                pool.Add(pr);
            }
            else
            {
                pr = pool[next];
                next = (next + 1) % PoolSize;
                if (pr.r == null) return;
            }
            Transform tr = pr.r.transform;
            tr.SetPositionAndRotation(at, Quaternion.Euler(90f, yaw + Random.Range(-6f, 6f) + (foot == 0 ? -4f : 4f), 0f));
            tr.localScale = new Vector3(foot == 0 ? -0.11f : 0.11f, 0.28f, 1f);   // mirrored for the left shoe
            pr.born = Time.time;
            pr.r.gameObject.SetActive(true);
            Tint(pr, alpha);
            prints[pr] = alpha;
        }

        static readonly Dictionary<Print, float> prints = new Dictionary<Print, float>();
        static float fadeTimer;

        void Update()
        {
            fadeTimer -= Time.deltaTime;
            if (fadeTimer > 0f || prints.Count == 0) return;
            fadeTimer = 0.5f;
            var dead = new List<Print>();
            foreach (var kv in prints)
            {
                Print pr = kv.Key;
                if (pr.r == null) { dead.Add(pr); continue; }
                float k = 1f - (Time.time - pr.born) / dryAfter;
                if (k <= 0f) { pr.r.gameObject.SetActive(false); dead.Add(pr); continue; }
                Tint(pr, kv.Value * k);
            }
            foreach (Print pr in dead) prints.Remove(pr);
        }

        static void Tint(Print pr, float alpha)
        {
            block.SetColor(BaseColorId, new Color(0.2f, 0.22f, 0.24f, alpha));
            pr.r.SetPropertyBlock(block);
        }

        /// <summary>A trainer's sole: wide forefoot with tread, a narrow waist, a round heel. White, shape in alpha.</summary>
        static Texture2D ShoeTexture()
        {
            const int W = 32, H = 80;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "Shoe print", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W * 2f - 1f, v = (y + 0.5f) / H;
                    // half-width of the sole along its length (toe at the top), slightly asymmetric (big toe side)
                    float half = v > 0.55f ? Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((v - 0.66f) / 0.34f, 2f))) * 0.95f
                               : v > 0.3f ? Mathf.Lerp(0.62f, 0.9f, (v - 0.3f) / 0.25f)
                               : Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((v - 0.2f) / 0.2f, 2f))) * 0.72f;
                    float uu = u + (v > 0.55f ? 0.08f * (v - 0.55f) / 0.45f : 0f);
                    float edge = Mathf.Clamp01((half - Mathf.Abs(uu)) * 8f);
                    // tread: bars across the forefoot and heel, a lighter waist (the arch barely touches)
                    float tread = Mathf.Repeat(v * 22f, 1f) < 0.55f ? 1f : 0.55f;
                    float arch = v > 0.3f && v < 0.52f ? 0.45f : 1f;
                    float n = Mathf.PerlinNoise(x * 0.35f, y * 0.35f) * 0.5f + 0.6f;
                    byte a = (byte)(255f * Mathf.Clamp01(edge * tread * arch * n));
                    px[y * W + x] = new Color32(255, 255, 255, a);
                }
            }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { pool.Clear(); prints.Clear(); next = 0; printMat = null; }
    }
}
