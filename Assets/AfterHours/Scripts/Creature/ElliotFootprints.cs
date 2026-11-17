using UnityEngine;
using AfterHours.World;

namespace AfterHours.Creature
{
    /// <summary>
    /// Elliot is never clean. On hard floors his feet leave prints in blood: strong just after a kill, fainter
    /// the further he walks, never quite gone. Each print dries and fades over a couple of minutes, so a fresh
    /// trail means he was here a moment ago. (A small pool of quads, reused oldest-first.)
    /// </summary>
    public static class ElliotFootprints
    {
        const int PoolSize = 90;
        const float Life = 150f;
        static GameObject[] quads;
        static Renderer[] rends;
        static float[] born, strength;
        static int next;
        static MaterialPropertyBlock block;
        static float freshness = 0.5f;     // how much blood is on his feet (1 just after a kill)
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        static Ticker ticker;

        /// <summary>Called by the catch sequence: his feet are soaked again.</summary>
        public static void Resoak() => freshness = 1f;

        public static void Stamp(Vector3 foot, Vector3 heading, SurfaceType surface, bool left)
        {
            if (surface == SurfaceType.Carpet) return;                     // soaks in, nothing to see
            if (!AfterHours.Settings.SettingsService.CurrentOrDefault.gore) return;
            Material m = AfterHours.Gore.GoreKit.Instance != null ? AfterHours.Gore.GoreKit.Instance.printMaterial : null;
            if (m == null) return;
            if (quads == null) Init(m);
            freshness = Mathf.Max(0.18f, freshness * 0.985f);                // wears off with every step, never fully
            heading.y = 0f;
            if (heading.sqrMagnitude < 1e-4f) heading = Vector3.forward;
            int i = next; next = (next + 1) % PoolSize;
            GameObject q = quads[i];
            q.SetActive(true);
            q.transform.position = foot + Vector3.up * 0.014f;
            // lie flat, toes along the heading, splayed out a little per foot
            q.transform.rotation = Quaternion.LookRotation(Vector3.down, Quaternion.Euler(0f, left ? -8f : 8f, 0f) * heading.normalized);
            float size = Random.Range(0.3f, 0.34f);
            q.transform.localScale = new Vector3(size * 0.8f, size, 1f);
            born[i] = Time.time;
            strength[i] = freshness * Random.Range(0.75f, 1f);
            Apply(i, 0f);
        }

        static void Init(Material m)
        {
            quads = new GameObject[PoolSize]; rends = new Renderer[PoolSize];
            born = new float[PoolSize]; strength = new float[PoolSize];
            block = new MaterialPropertyBlock();
            var root = new GameObject("[Elliot footprints]");
            Object.DontDestroyOnLoad(root);
            ticker = root.AddComponent<Ticker>();
            for (int i = 0; i < PoolSize; i++)
            {
                GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(q.GetComponent<Collider>());
                q.name = "Print";
                q.transform.SetParent(root.transform, false);
                rends[i] = q.GetComponent<Renderer>();
                rends[i].sharedMaterial = m;
                rends[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rends[i].receiveShadows = true;
                q.SetActive(false);
                quads[i] = q;
            }
        }

        static void Apply(int i, float age)
        {
            // wet and dark at first, then dries browner and fades
            float k = strength[i] * Mathf.Clamp01(1f - age / Life) * (age < 20f ? 1f : Mathf.Lerp(1f, 0.7f, (age - 20f) / (Life - 20f)));
            Color c = Color.Lerp(new Color(1f, 1f, 1f), new Color(0.75f, 0.7f, 0.65f), Mathf.Clamp01(age / 60f));
            c.a = k;
            block.SetColor(ColorId, c);
            rends[i].SetPropertyBlock(block);
        }

        class Ticker : MonoBehaviour
        {
            float t;
            void Update()
            {
                t -= Time.deltaTime;
                if (t > 0f || quads == null) return;
                t = 1f;                                    // once a second is plenty
                for (int i = 0; i < PoolSize; i++)
                {
                    if (!quads[i].activeSelf) continue;
                    float age = Time.time - born[i];
                    if (age > Life) { quads[i].SetActive(false); continue; }
                    Apply(i, age);
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { quads = null; rends = null; next = 0; freshness = 0.5f; }
    }
}
