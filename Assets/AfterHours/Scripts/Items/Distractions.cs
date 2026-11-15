using UnityEngine;
using AfterHours.Audio;
using AfterHours.World;

namespace AfterHours.Items
{
    /// <summary>
    /// A device you can set while holding it (press Interact with nothing in front of you): an alarm clock or an
    /// old phone. After a delay it rings for a while - a loud, repeating noise that draws Elliot to wherever it is.
    /// </summary>
    public class TimedDistraction : MonoBehaviour
    {
        public float delay = 8f;
        public float ringSeconds = 8f;
        public float noiseRadius = 30f;
        public bool isPhone;

        float armedAt = -1f;
        float pulseTimer;
        AudioSource source;
        static AudioClip ringClip, phoneClip;

        public bool Armed => armedAt >= 0f;

        public void Arm()
        {
            armedAt = Time.time;
            AudioPool.PlayAt(ProceduralSounds.Effect(ProceduralSounds.Sfx.Click), transform.position, 0.4f, 1.4f, 6f);
        }

        void Update()
        {
            if (armedAt < 0f) return;
            float t = Time.time - armedAt - delay;
            if (t < 0f) return;
            if (t > ringSeconds)
            {
                armedAt = -1f;
                if (source != null) source.Stop();
                return;
            }
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 1.5f;
                source.maxDistance = 45f;
                gameObject.AddComponent<AudioOcclusion>();
            }
            if (!source.isPlaying)
            {
                source.clip = isPhone ? (phoneClip != null ? phoneClip : phoneClip = Ring(true)) : (ringClip != null ? ringClip : ringClip = Ring(false));
                source.loop = true;
                source.Play();
            }
            pulseTimer -= Time.deltaTime;
            if (pulseTimer <= 0f)
            {
                pulseTimer = 0.8f;
                NoiseBus.Emit(new NoiseEvent(transform.position, noiseRadius, NoiseKind.Distraction, gameObject));
            }
        }

        static AudioClip Ring(bool phone)
        {
            const int rate = 44100;
            int n = rate * 2;
            float[] d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                bool on = phone ? (t % 2f) < 1.2f && ((t * 20f) % 1f) < 0.5f : ((t * 8f) % 1f) < 0.5f;
                float f = phone ? 880f + 440f * (((int)(t * 20f)) % 2) : 2400f;
                d[i] = on ? Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t)) * 0.35f : 0f;
            }
            AudioClip c = AudioClip.Create(phone ? "PhoneRing" : "AlarmRing", n, 1, rate, false);
            c.SetData(d, 0);
            return c;
        }
    }

    /// <summary>A glass bottle: thrown hard it shatters with a loud crash (and is gone - it's only a distraction).</summary>
    public class Shatterable : MonoBehaviour
    {
        public float breakSpeed = 5.5f;
        public float noiseRadius = 28f;

        void OnCollisionEnter(Collision c)
        {
            if (c.relativeVelocity.magnitude < breakSpeed) return;
            ItemPickup pick = GetComponent<ItemPickup>();
            if (pick != null && pick.IsHeld) return;
            Vector3 p = transform.position;
            AudioPool.PlayAt(ProceduralSounds.Impact(SurfaceType.Glass), p, 1f, 1.3f, 40f);
            AudioPool.PlayAt(ProceduralSounds.Impact(SurfaceType.Glass), p, 0.8f, 1.7f, 40f);
            NoiseBus.Emit(new NoiseEvent(p, noiseRadius, NoiseKind.Distraction, gameObject));
            for (int i = 0; i < 6; i++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "Glass shard";
                shard.transform.position = p + Random.insideUnitSphere * 0.08f;
                shard.transform.localScale = new Vector3(Random.Range(0.02f, 0.05f), 0.004f, Random.Range(0.02f, 0.05f));
                Renderer r = GetComponentInChildren<Renderer>();
                if (r != null) shard.GetComponent<Renderer>().sharedMaterial = r.sharedMaterial;
                Rigidbody rb = shard.AddComponent<Rigidbody>();
                rb.mass = 0.01f;
                rb.linearVelocity = Random.insideUnitSphere * 1.5f + Vector3.up;
            }
            Destroy(gameObject);
        }
    }
}
