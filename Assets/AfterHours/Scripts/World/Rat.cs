using UnityEngine;
using AfterHours.Audio;

namespace AfterHours.World
{
    /// <summary>
    /// A rat by the wall. It sits sniffing, whiskers twitching; when someone comes close it bolts along the wall,
    /// squeaking, and is gone into a gap. A while later (when nobody's looking) it's back. Its body is built from
    /// a few primitives at start; its squeak and the patter of its feet are synthesised.
    /// </summary>
    public class Rat : MonoBehaviour
    {
        [Tooltip("Which way along the wall it runs (world direction, flat).")] public Vector3 runDir = Vector3.forward;
        public int seed;
        enum State { Idle, Flee, Gone }
        State state = State.Idle;
        Transform body, head, tail;
        Vector3 home;
        Quaternion homeRot;
        float timer, fled, sniff;
        System.Random rng;
        AudioSource src;
        static Material fur, pink;
        static AudioClip squeak, patter;

        void Start()
        {
            rng = new System.Random(seed);
            home = transform.position; homeRot = transform.rotation;
            BuildBody();
            src = gameObject.AddComponent<AudioSource>();
            src.spatialBlend = 1f; src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 0.5f; src.maxDistance = 14f; src.playOnAwake = false;
            Clips();
        }

        void BuildBody()
        {
            if (fur == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                fur = new Material(lit) { color = new Color(0.16f, 0.13f, 0.11f) }; fur.SetFloat("_Smoothness", 0.25f);
                pink = new Material(lit) { color = new Color(0.55f, 0.38f, 0.36f) }; pink.SetFloat("_Smoothness", 0.4f);
            }
            body = Part(PrimitiveType.Sphere, transform, new Vector3(0f, 0.045f, 0f), new Vector3(0.085f, 0.07f, 0.17f), fur);
            head = Part(PrimitiveType.Sphere, transform, new Vector3(0f, 0.05f, 0.105f), new Vector3(0.055f, 0.05f, 0.085f), fur);
            Part(PrimitiveType.Sphere, head, new Vector3(-0.35f, 0.55f, -0.1f), new Vector3(0.35f, 0.35f, 0.12f), pink);
            Part(PrimitiveType.Sphere, head, new Vector3(0.35f, 0.55f, -0.1f), new Vector3(0.35f, 0.35f, 0.12f), pink);
            tail = new GameObject("Tail").transform; tail.SetParent(transform, false); tail.localPosition = new Vector3(0f, 0.03f, -0.085f);
            Part(PrimitiveType.Cylinder, tail, new Vector3(0f, 0f, -0.09f), new Vector3(0.012f, 0.09f, 0.012f), pink).localRotation = Quaternion.Euler(95f, 0f, 0f);
        }

        static Transform Part(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m)
        {
            GameObject g = GameObject.CreatePrimitive(t);
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g.transform;
        }

        void Update()
        {
            Camera cam = Camera.main;
            Vector3 eye = cam != null ? cam.transform.position : transform.position + Vector3.up * 100f;
            float d = Vector3.Distance(new Vector3(eye.x, transform.position.y, eye.z), transform.position);
            float dt = Time.deltaTime;
            switch (state)
            {
                case State.Idle:
                    sniff += dt;
                    head.localRotation = Quaternion.Euler(Mathf.Sin(sniff * 9f) * 4f - 6f, Mathf.Sin(sniff * 1.3f) * 25f, 0f);
                    tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(sniff * 0.8f) * 20f, 0f);
                    if (d < 5.5f)
                    {
                        state = State.Flee; fled = 0f;
                        Vector3 away = transform.position - eye; away.y = 0f;
                        Vector3 dir = runDir.normalized * (Vector3.Dot(away, runDir) >= 0f ? 1f : -1f);
                        transform.rotation = Quaternion.LookRotation(dir);
                        src.pitch = 0.9f + (float)rng.NextDouble() * 0.3f;
                        src.PlayOneShot(squeak, 0.6f);
                        src.clip = patter; src.loop = true; src.volume = 0.35f; src.Play();
                    }
                    break;
                case State.Flee:
                    float speed = 3.6f;
                    transform.position += transform.forward * speed * dt;
                    fled += speed * dt;
                    body.localPosition = new Vector3(Mathf.Sin(fled * 40f) * 0.006f, 0.045f + Mathf.Abs(Mathf.Sin(fled * 20f)) * 0.012f, 0f);
                    tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(fled * 25f) * 25f, 0f);
                    if (fled > 4f + (float)(seed % 3))
                    {
                        state = State.Gone; timer = 60f + (float)rng.NextDouble() * 120f;
                        src.Stop(); src.loop = false;
                        SetVisible(false);
                    }
                    break;
                case State.Gone:
                    timer -= dt;
                    if (timer > 0f) break;
                    // back at its spot, but only if nobody is close or looking
                    if (d < 12f) { timer = 10f; break; }
                    if (cam != null)
                    {
                        Vector3 vp = cam.WorldToViewportPoint(home);
                        if (vp.z > 0f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f && d < 30f) { timer = 5f; break; }
                    }
                    transform.SetPositionAndRotation(home, homeRot);
                    SetVisible(true);
                    state = State.Idle;
                    break;
            }
        }

        void SetVisible(bool v)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = v;
        }

        static void Clips()
        {
            if (squeak != null) return;
            const int rate = 22050;
            var rng = new System.Random(31);
            // squeak: three quick falling chirps
            int n = (int)(0.42f * rate); var d = new float[n]; float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                int k = (int)(t / 0.14f); float u = t - k * 0.14f;
                float env = u < 0.09f ? Mathf.Sin(Mathf.PI * u / 0.09f) : 0f;
                float f = 4200f - u * 9000f + 300f * Mathf.Sin(u * 400f) + k * 200f;
                ph += f / rate;
                d[i] = Mathf.Sin(ph * Mathf.PI * 2f) * env * 0.5f + (float)(rng.NextDouble() - 0.5) * env * 0.08f;
            }
            squeak = AudioClip.Create("rat_squeak", n, 1, rate, false); squeak.SetData(d, 0);
            // patter: fast, light claw ticks on a hard floor (loops)
            n = (int)(0.5f * rate); d = new float[n];
            for (int c = 0; c < 22; c++)
            {
                int at = (int)((c / 22f + (float)rng.NextDouble() * 0.01f) * n);
                for (int j = 0; j < 220 && at + j < n; j++) d[at + j] += (float)(rng.NextDouble() - 0.5) * Mathf.Exp(-j / 30f) * 0.6f;
            }
            patter = AudioClip.Create("rat_patter", n, 1, rate, false); patter.SetData(d, 0);
        }
    }
}
