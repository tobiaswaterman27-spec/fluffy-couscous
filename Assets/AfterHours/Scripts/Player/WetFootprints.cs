using System.Collections.Generic;
using UnityEngine;
using AfterHours.World;

namespace AfterHours.Player
{
    /// <summary>
    /// Walk through a puddle under a leak and your shoes are wet for the next dozen steps: dark, glossy prints on the
    /// floor, fainter with every step, drying out over a minute. Elliot notices fresh ones and follows the trail
    /// (see <see cref="FreshPrintNear"/>). Works from how far you've walked, so it runs for every player in co-op
    /// (including the ones the host only sees as proxies). One shared pool of quads.
    /// </summary>
    public class WetFootprints : MonoBehaviour
    {
        [Tooltip("A transparent, glossy decal material (its texture is replaced by a shoe print).")]
        public Material template;
        public int wetSteps = 12;
        public float dryAfter = 60f;
        public float stepLength = 0.72f;

        float wet;
        Vector3 lastPos;
        float walked;
        int foot;
        bool hasLast;

        class Print { public Renderer r; public float born, strength; public Vector3 at; public GameObject owner; }
        static readonly List<Print> pool = new List<Print>();
        static int next;
        static Material printMat;
        static MaterialPropertyBlock block;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        const int PoolSize = 48;
        static float fadeTimer;

        /// <summary>
        /// The freshest wet print within 'radius' of 'from' that was made after 'newerThan' (Time.time) and is
        /// still less than 'maxAge' seconds old. Elliot uses this to follow a trail print by print.
        /// </summary>
        public static bool FreshPrintNear(Vector3 from, float radius, float newerThan, float maxAge, out Vector3 at, out float born)
        {
            at = Vector3.zero; born = -1f;
            float best = -1f;
            foreach (Print p in pool)
            {
                if (p.r == null || !p.r.gameObject.activeSelf || p.born <= newerThan || Time.time - p.born > maxAge) continue;
                if (Mathf.Abs(p.at.y - from.y) > 2.5f || (p.at - from).sqrMagnitude > radius * radius) continue;
                if (p.born > best) { best = p.born; at = p.at; born = p.born; }
            }
            return best > 0f;
        }

        void OnEnable() { hasLast = false; }

        void Update()
        {
            Vector3 p = transform.position;
            if (!hasLast) { lastPos = p; hasLast = true; }
            Vector3 d = p - lastPos; d.y = 0f;
            float moved = d.magnitude;
            lastPos = p;
            if (moved > 3f) { walked = 0f; FadeAll(); return; }   // teleported (respawn, load)
            if (moved > 0.0005f)
            {
                // standing in a leak's puddle soaks the shoes
                foreach (Drip dr in Drip.All)
                {
                    if (dr == null) continue;
                    Vector3 c = dr.PuddleCentre;
                    if (Mathf.Abs(c.y - p.y) < 0.6f && new Vector2(c.x - p.x, c.z - p.z).sqrMagnitude < dr.puddleRadius * dr.puddleRadius) { wet = 1f; walked = 0f; break; }
                }
                walked += moved;
                if (walked >= stepLength)
                {
                    walked -= stepLength;
                    foot = 1 - foot;
                    if (wet > 0f && template != null) Step(p, d / Mathf.Max(moved, 1e-4f));
                }
            }
            FadeAll();
        }

        void Step(Vector3 p, Vector3 dir)
        {
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            Vector3 at = p + right * (foot == 0 ? -0.11f : 0.11f);
            int mask = Physics.DefaultRaycastLayers;
            foreach (int l in new[] { AfterHours.Core.GameLayers.PlayerLayer, AfterHours.Core.GameLayers.LocalBodyLayer, AfterHours.Core.GameLayers.CreatureLayer })
                if (l >= 0) mask &= ~(1 << l);
            if (!Physics.Raycast(at + Vector3.up * 0.4f, Vector3.down, out RaycastHit hit, 0.9f, mask, QueryTriggerInteraction.Ignore)) return;
            if (Vector3.Dot(hit.normal, Vector3.up) < 0.9f) return;
            Place(hit.point + hit.normal * 0.006f, yaw, foot, 0.6f * wet);
            wet -= 1f / Mathf.Max(1, wetSteps);
        }

        void Place(Vector3 at, float yaw, int f, float alpha)
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
            if (pool.Count < PoolSize) { pr = new Print(); pool.Add(pr); }
            else { pr = pool[next]; next = (next + 1) % PoolSize; }
            if (pr.r == null)       // new, or its quad went with the last scene
            {
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Wet footprint";
                Destroy(q.GetComponent<Collider>());
                pr.r = q.GetComponent<Renderer>();
                pr.r.sharedMaterial = printMat;
                pr.r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Transform tr = pr.r.transform;
            tr.SetPositionAndRotation(at, Quaternion.Euler(90f, yaw + Random.Range(-6f, 6f) + (f == 0 ? -4f : 4f), 0f));
            tr.localScale = new Vector3(f == 0 ? -0.11f : 0.11f, 0.28f, 1f);
            pr.born = Time.time;
            pr.strength = alpha;
            pr.at = at;
            pr.owner = gameObject;
            pr.r.gameObject.SetActive(true);
            Tint(pr, alpha);
        }

        void FadeAll()
        {
            fadeTimer -= Time.deltaTime;
            if (fadeTimer > 0f) return;
            fadeTimer = 0.5f;
            foreach (Print pr in pool)
            {
                if (pr.r == null || !pr.r.gameObject.activeSelf) continue;
                float k = 1f - (Time.time - pr.born) / dryAfter;
                if (k <= 0f) { pr.r.gameObject.SetActive(false); continue; }
                Tint(pr, pr.strength * k);
            }
        }

        static void Tint(Print pr, float alpha)
        {
            if (block == null) block = new MaterialPropertyBlock();
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
                    float half = v > 0.55f ? Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((v - 0.66f) / 0.34f, 2f))) * 0.95f
                               : v > 0.3f ? Mathf.Lerp(0.62f, 0.9f, (v - 0.3f) / 0.25f)
                               : Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((v - 0.2f) / 0.2f, 2f))) * 0.72f;
                    float uu = u + (v > 0.55f ? 0.08f * (v - 0.55f) / 0.45f : 0f);
                    float edge = Mathf.Clamp01((half - Mathf.Abs(uu)) * 8f);
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
        static void ResetStatics() { pool.Clear(); next = 0; printMat = null; block = null; }
    }
}
