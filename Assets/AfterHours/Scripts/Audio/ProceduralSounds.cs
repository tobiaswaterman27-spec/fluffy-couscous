using System.Collections.Generic;
using UnityEngine;
using AfterHours.World;

namespace AfterHours.Audio
{
    /// <summary>
    /// Generates placeholder footstep and impact sounds in code so the project works with zero audio files.
    /// Each floor material gets several variations. Real recorded sounds replace these in Part 10;
    /// the calling code will not change.
    /// </summary>
    public static class ProceduralSounds
    {
        const int Rate = 44100;
        const int Variants = 4;

        struct Recipe
        {
            public float duration, attack, noiseDecay, lowpass, brightness, noiseLevel;
            public float thumpFreq, thumpLevel, thumpDecay;
            public float[] rings;
            public float ringLevel, ringDecay;
        }

        static readonly Dictionary<int, AudioClip[]> cache = new Dictionary<int, AudioClip[]>();

        public static AudioClip Footstep(SurfaceType surface, bool crawling)
        {
            int key = crawling ? 1000 + (int)surface : (int)surface;
            return Pick(key, () => crawling ? CrawlRecipe(surface) : StepRecipe(surface), "Step_" + surface + (crawling ? "_Crawl" : ""));
        }

        public static AudioClip Impact(SurfaceType surface)
        {
            int key = 2000 + (int)surface;
            return Pick(key, () =>
            {
                Recipe r = StepRecipe(surface);
                r.duration *= 1.8f;
                r.noiseDecay *= 1.6f;
                r.thumpFreq *= 0.75f;
                r.thumpDecay *= 1.8f;
                r.ringDecay *= 1.5f;
                r.ringLevel *= 1.3f;
                return r;
            }, "Impact_" + surface);
        }

        /// <summary>Seamless looping scrape for heavy objects dragged across the floor.</summary>
        public static AudioClip ScrapeLoop(SurfaceType material)
        {
            int key = 3000 + (int)material;
            if (cache.TryGetValue(key, out AudioClip[] cached) && cached[0] != null) return cached[0];

            bool metallic = material == SurfaceType.Metal || material == SurfaceType.VentMetal;
            int n = Rate * 2;
            float[] data = new float[n];
            var rng = new System.Random(key);
            float lp1 = 0f, lp2 = 0f, peak = 0.0001f;
            float coef = metallic ? 0.35f : 0.15f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 += coef * (white - lp1);
                lp2 += coef * (lp1 - lp2);
                // Uneven "stick-slip" grinding texture, periodic over the 2 s loop.
                float grind = 0.6f + 0.25f * Mathf.Sin(2f * Mathf.PI * 3f * t) + 0.15f * Mathf.Sin(2f * Mathf.PI * 11f * t + 1.3f);
                float sample = lp2 * grind;
                if (metallic) sample += 0.12f * Mathf.Sin(2f * Mathf.PI * 410f * t) * grind;
                data[i] = sample;
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            }
            // Crossfade the ends so the loop has no click.
            int fade = Rate / 10;
            for (int i = 0; i < fade; i++)
            {
                float a = i / (float)fade;
                data[i] = data[i] * a + data[n - fade + i] * (1f - a);
            }
            System.Array.Resize(ref data, n - fade);
            for (int i = 0; i < data.Length; i++) data[i] *= 0.8f / peak;

            AudioClip clip = AudioClip.Create("Scrape_" + material, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            cache[key] = new[] { clip };
            return clip;
        }

        static AudioClip Pick(int key, System.Func<Recipe> recipe, string name)
        {
            if (!cache.TryGetValue(key, out AudioClip[] clips) || clips[0] == null)
            {
                Recipe r = recipe();
                clips = new AudioClip[Variants];
                for (int i = 0; i < Variants; i++) clips[i] = Render(r, name + "_" + i, key * 31 + i * 7919);
                cache[key] = clips;
            }
            return clips[Random.Range(0, clips.Length)];
        }

        static Recipe StepRecipe(SurfaceType s)
        {
            switch (s)
            {
                case SurfaceType.Carpet:
                    return new Recipe { duration = 0.18f, attack = 0.004f, noiseDecay = 0.035f, lowpass = 0.07f, brightness = 0f, noiseLevel = 0.9f, thumpFreq = 70f, thumpLevel = 0.6f, thumpDecay = 0.03f };
                case SurfaceType.Concrete:
                    return new Recipe { duration = 0.25f, attack = 0.002f, noiseDecay = 0.03f, lowpass = 0.3f, brightness = 0.35f, noiseLevel = 0.8f, thumpFreq = 95f, thumpLevel = 0.7f, thumpDecay = 0.035f };
                case SurfaceType.Metal:
                    return new Recipe { duration = 0.45f, attack = 0.001f, noiseDecay = 0.02f, lowpass = 0.45f, brightness = 0.5f, noiseLevel = 0.7f, thumpFreq = 110f, thumpLevel = 0.5f, thumpDecay = 0.03f, rings = new[] { 430f, 1170f, 2380f, 3150f }, ringLevel = 0.5f, ringDecay = 0.16f };
                case SurfaceType.VentMetal:
                    return new Recipe { duration = 0.5f, attack = 0.002f, noiseDecay = 0.025f, lowpass = 0.35f, brightness = 0.3f, noiseLevel = 0.6f, thumpFreq = 80f, thumpLevel = 0.6f, thumpDecay = 0.05f, rings = new[] { 260f, 720f, 1530f }, ringLevel = 0.65f, ringDecay = 0.22f };
                case SurfaceType.Wood:
                    return new Recipe { duration = 0.22f, attack = 0.002f, noiseDecay = 0.03f, lowpass = 0.25f, brightness = 0.2f, noiseLevel = 0.7f, thumpFreq = 120f, thumpLevel = 0.7f, thumpDecay = 0.04f, rings = new[] { 380f, 900f }, ringLevel = 0.15f, ringDecay = 0.05f };
                case SurfaceType.Glass:
                    return new Recipe { duration = 0.3f, attack = 0.001f, noiseDecay = 0.015f, lowpass = 0.6f, brightness = 0.7f, noiseLevel = 0.6f, thumpFreq = 150f, thumpLevel = 0.3f, thumpDecay = 0.02f, rings = new[] { 2100f, 3400f, 5200f }, ringLevel = 0.3f, ringDecay = 0.09f };
                default: // Tile
                    return new Recipe { duration = 0.22f, attack = 0.001f, noiseDecay = 0.018f, lowpass = 0.5f, brightness = 0.6f, noiseLevel = 0.8f, thumpFreq = 140f, thumpLevel = 0.5f, thumpDecay = 0.02f, rings = new[] { 1800f, 2700f }, ringLevel = 0.12f, ringDecay = 0.03f };
            }
        }

        static Recipe CrawlRecipe(SurfaceType s)
        {
            Recipe r = new Recipe { duration = 0.32f, attack = 0.05f, noiseDecay = 0.09f, lowpass = 0.2f, brightness = 0.3f, noiseLevel = 0.8f };
            if (s == SurfaceType.Metal || s == SurfaceType.VentMetal)
            {
                r.rings = new[] { 240f, 690f };
                r.ringLevel = 0.35f;
                r.ringDecay = 0.18f;
                r.thumpFreq = 75f;
                r.thumpLevel = 0.35f;
                r.thumpDecay = 0.06f;
            }
            else if (s == SurfaceType.Carpet)
            {
                r.lowpass = 0.08f;
                r.brightness = 0f;
            }
            return r;
        }

        static AudioClip Render(Recipe r, string name, int seed)
        {
            var rng = new System.Random(seed);
            int n = Mathf.Max(64, (int)(r.duration * Rate));
            float[] data = new float[n];
            float lp1 = 0f, lp2 = 0f;
            float detune = 1f + (float)(rng.NextDouble() - 0.5) * 0.08f;
            float peak = 0.0001f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float attack = r.attack > 0f ? Mathf.Clamp01(t / r.attack) : 1f;

                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 += r.lowpass * (white - lp1);
                lp2 += r.lowpass * (lp1 - lp2);
                float noise = Mathf.Lerp(lp2, white - lp2, r.brightness);
                float sample = noise * Mathf.Exp(-t / Mathf.Max(0.001f, r.noiseDecay)) * r.noiseLevel * attack;

                if (r.thumpLevel > 0f)
                {
                    sample += Mathf.Sin(2f * Mathf.PI * r.thumpFreq * detune * t) * Mathf.Exp(-t / Mathf.Max(0.001f, r.thumpDecay)) * r.thumpLevel * attack;
                }

                if (r.rings != null && r.rings.Length > 0)
                {
                    float ring = 0f;
                    for (int k = 0; k < r.rings.Length; k++)
                    {
                        ring += Mathf.Sin(2f * Mathf.PI * r.rings[k] * detune * t + k) * Mathf.Exp(-t / (r.ringDecay / (1f + k * 0.3f)));
                    }
                    sample += ring / r.rings.Length * r.ringLevel * attack;
                }

                data[i] = sample;
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            }

            float gain = 0.9f / peak;
            int fade = Mathf.Min(n, Rate / 200);
            for (int i = 0; i < n; i++)
            {
                data[i] *= gain;
                int fromEnd = n - 1 - i;
                if (fromEnd < fade) data[i] *= fromEnd / (float)fade;
            }

            AudioClip clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ------------------------------------------------------------------ interaction one-shots

        public enum Sfx { Latch, Creak, Slam, Rattle, ShutterRoll, ShutterStop, DrawerSlide, MetalClank, Click, Unlock, Bump, Crunch, Tear, Splatter, Thud, Gasp }

        /// <summary>One-shot interaction sounds (doors, shutters, drawers, switches...). A few variations each.</summary>
        public static AudioClip Effect(Sfx kind)
        {
            int key = 5000 + (int)kind;
            if (!cache.TryGetValue(key, out AudioClip[] clips) || clips[0] == null)
            {
                clips = new AudioClip[3];
                for (int v = 0; v < clips.Length; v++) clips[v] = RenderEffect(kind, key * 13 + v);
                cache[key] = clips;
            }
            return clips[Random.Range(0, clips.Length)];
        }

        static AudioClip RenderEffect(Sfx kind, int seed)
        {
            var rng = new System.Random(seed);
            float seconds;
            switch (kind)
            {
                case Sfx.Creak: seconds = 1.1f; break;
                case Sfx.ShutterRoll: seconds = 1.0f; break;
                case Sfx.Slam: seconds = 0.9f; break;
                case Sfx.Rattle: seconds = 0.6f; break;
                case Sfx.DrawerSlide: seconds = 0.5f; break;
                case Sfx.Crunch: seconds = 0.7f; break;
                case Sfx.Tear: seconds = 0.9f; break;
                case Sfx.Splatter: seconds = 0.6f; break;
                case Sfx.Thud: seconds = 0.5f; break;
                case Sfx.Gasp: seconds = 0.8f; break;
                default: seconds = 0.35f; break;
            }
            int n = (int)(seconds * Rate);
            float[] d = new float[n];
            float lp = 0f, phase = 0f, peak = 0.0001f;
            float pitch = 0.9f + (float)rng.NextDouble() * 0.2f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                float s = 0f;
                switch (kind)
                {
                    case Sfx.Latch:
                    case Sfx.Click:
                        s = white * Mathf.Exp(-t * 90f) + 0.6f * Mathf.Sin(2f * Mathf.PI * 1800f * pitch * t) * Mathf.Exp(-t * 60f);
                        if (kind == Sfx.Latch && t > 0.08f) s += 0.7f * white * Mathf.Exp(-(t - 0.08f) * 80f);
                        break;
                    case Sfx.Unlock:
                        s = white * (Mathf.Exp(-t * 70f) + 0.8f * Mathf.Exp(-Mathf.Max(0f, t - 0.15f) * 70f) * (t > 0.15f ? 1f : 0f))
                            + 0.4f * Mathf.Sin(2f * Mathf.PI * 2400f * t) * Mathf.Exp(-t * 30f);
                        break;
                    case Sfx.Creak:
                        // Stick-slip hinge squeal: a wavering tone made of tiny pulses.
                        float f = (380f + 260f * Mathf.Sin(t * 2.2f + seed) + 60f * Mathf.Sin(t * 13f)) * pitch;
                        phase += f / Rate;
                        float pulse = Mathf.Pow(Mathf.Abs(Mathf.Sin(phase * Mathf.PI)), 18f);
                        s = (pulse * 0.9f + 0.1f * white) * Mathf.Sin(Mathf.PI * t / seconds);
                        break;
                    case Sfx.Slam:
                        lp += 0.08f * (white - lp);
                        s = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t * 9f) * 1.2f + lp * 4f * Mathf.Exp(-t * 14f)
                            + 0.3f * white * Mathf.Exp(-t * 40f);
                        break;
                    case Sfx.Rattle:
                        float tick = (t * 22f) % 1f;
                        s = white * Mathf.Exp(-tick * 25f) * (1f - t / seconds) + 0.3f * Mathf.Sin(2f * Mathf.PI * 900f * t) * Mathf.Exp(-tick * 30f);
                        break;
                    case Sfx.ShutterRoll:
                        float slat = (t * 18f) % 1f;
                        lp += 0.3f * (white - lp);
                        s = (lp * 1.5f + 0.6f * white * Mathf.Exp(-slat * 20f)) * (0.7f + 0.3f * Mathf.Sin(t * 30f))
                            + 0.25f * Mathf.Sin(2f * Mathf.PI * 520f * t) * Mathf.Exp(-slat * 12f);
                        break;
                    case Sfx.ShutterStop:
                    case Sfx.MetalClank:
                        s = 0.5f * white * Mathf.Exp(-t * 40f);
                        foreach (float fr in new[] { 310f, 845f, 1520f, 2390f }) s += 0.25f * Mathf.Sin(2f * Mathf.PI * fr * pitch * t) * Mathf.Exp(-t * 9f);
                        break;
                    case Sfx.DrawerSlide:
                        lp += 0.15f * (white - lp);
                        s = lp * 2f * Mathf.Sin(Mathf.PI * t / seconds) + (t > seconds * 0.85f ? white * Mathf.Exp(-(t - seconds * 0.85f) * 80f) : 0f);
                        break;
                    case Sfx.Bump:
                        s = Mathf.Sin(2f * Mathf.PI * 110f * t) * Mathf.Exp(-t * 25f) + 0.3f * white * Mathf.Exp(-t * 50f);
                        break;
                    case Sfx.Crunch:
                    {
                        // Bone: a dense cluster of sharp cracks over a wet, low squelch.
                        float grain = (t * (60f + 40f * pitch)) % 1f;
                        float crack = rng.NextDouble() < 0.03 ? 1f : 0f;
                        lp += 0.05f * (white - lp);
                        s = white * Mathf.Exp(-grain * 18f) * Mathf.Exp(-t * 4f) * 1.2f + crack * 2.5f * Mathf.Exp(-t * 3f)
                            + lp * 5f * Mathf.Exp(-t * 3f) + 0.5f * Mathf.Sin(2f * Mathf.PI * 60f * t) * Mathf.Exp(-t * 8f);
                        break;
                    }
                    case Sfx.Tear:
                    {
                        // Flesh tearing: filtered noise with a ragged, stretching rhythm.
                        lp += 0.25f * (white - lp);
                        float rip = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (14f + 20f * t) * t + white * 0.8f);
                        s = (lp * 2.5f * rip + 0.4f * white * rip) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / seconds)) ;
                        break;
                    }
                    case Sfx.Splatter:
                    {
                        lp += 0.1f * (white - lp);
                        float drops = rng.NextDouble() < 0.01 ? 1f : 0f;
                        s = lp * 4f * Mathf.Exp(-t * 9f) + drops * white * 2f * Mathf.Exp(-t * 4f);
                        break;
                    }
                    case Sfx.Thud:
                        lp += 0.04f * (white - lp);
                        s = Mathf.Sin(2f * Mathf.PI * 52f * t) * Mathf.Exp(-t * 11f) * 1.4f + lp * 5f * Mathf.Exp(-t * 16f);
                        break;
                    case Sfx.Gasp:
                    {
                        // A human gasp / choked cry: breathy noise with a short voiced formant.
                        lp += 0.3f * (white - lp);
                        phase += (210f + 80f * Mathf.Sin(t * 9f)) * pitch / Rate;
                        float voiced = Mathf.Sin(2f * Mathf.PI * phase) * Mathf.Clamp01(t / 0.08f) * Mathf.Exp(-t * 3f);
                        s = (lp * 1.4f + voiced * 0.6f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / seconds));
                        break;
                    }
                }
                d[i] = s;
                peak = Mathf.Max(peak, Mathf.Abs(s));
            }
            int fade = Mathf.Min(n, Rate / 100);
            for (int i = 0; i < n; i++)
            {
                d[i] *= 0.9f / peak;
                if (n - 1 - i < fade) d[i] *= (n - 1 - i) / (float)fade;
            }
            AudioClip clip = AudioClip.Create("Sfx_" + kind + "_" + seed, n, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        // ------------------------------------------------------------------ ambience loops

        public enum Ambience { FluorescentHum, Ventilation, Machinery, CarParkFans, Water, Buzz }

        /// <summary>Seamless looping ambience clip (generated once, cached).</summary>
        public static AudioClip AmbienceLoop(Ambience kind)
        {
            int key = 4000 + (int)kind;
            if (cache.TryGetValue(key, out AudioClip[] cached) && cached[0] != null) return cached[0];

            float seconds = kind == Ambience.Machinery ? 8f : 6f;
            int n = (int)(seconds * Rate);
            int fade = Rate / 4;
            float[] data = new float[n + fade];
            var rng = new System.Random(key);
            float lp1 = 0f, lp2 = 0f, brown = 0f, peak = 0.0001f;

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                brown = Mathf.Clamp(brown + white * 0.02f, -1f, 1f) * 0.999f;
                float sample = 0f;
                switch (kind)
                {
                    case Ambience.FluorescentHum:
                        // Mains hum: 100 Hz (twice the 50 Hz supply) with harmonics, plus a faint hiss.
                        sample = 0.5f * Mathf.Sin(2f * Mathf.PI * 100f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * 200f * t)
                               + 0.12f * Mathf.Sin(2f * Mathf.PI * 300f * t) + 0.05f * white;
                        break;
                    case Ambience.Buzz:
                        float buzzWave = Mathf.Sin(2f * Mathf.PI * 100f * t);
                        sample = Mathf.Sign(buzzWave) * Mathf.Pow(Mathf.Abs(buzzWave), 0.3f) * 0.4f + 0.2f * Mathf.Sin(2f * Mathf.PI * 1200f * t) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 3f * t));
                        break;
                    case Ambience.Ventilation:
                        lp1 += 0.05f * (white - lp1);
                        sample = lp1 * 3f * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * t / seconds)) + brown * 0.3f;
                        break;
                    case Ambience.Machinery:
                        lp1 += 0.02f * (white - lp1);
                        lp2 += 0.02f * (lp1 - lp2);
                        float thump = Mathf.Exp(-((t % 1f) * 9f)) * Mathf.Sin(2f * Mathf.PI * 45f * t);
                        sample = lp2 * 6f + 0.35f * Mathf.Sin(2f * Mathf.PI * 50f * t) + 0.3f * thump + 0.1f * Mathf.Sin(2f * Mathf.PI * 150f * t);
                        break;
                    case Ambience.CarParkFans:
                        lp1 += 0.03f * (white - lp1);
                        sample = lp1 * 4f + 0.2f * Mathf.Sin(2f * Mathf.PI * 75f * t) + brown * 0.5f;
                        break;
                    case Ambience.Water:
                        lp1 += 0.3f * (white - lp1);
                        sample = (white - lp1) * 0.6f * (0.7f + 0.3f * (float)rng.NextDouble());
                        break;
                }
                data[i] = sample;
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            }

            // Crossfade the extra tail into the start so the loop has no seam.
            for (int i = 0; i < fade; i++)
            {
                float a = i / (float)fade;
                data[i] = data[i] * a + data[n + i] * (1f - a);
            }
            System.Array.Resize(ref data, n);
            for (int i = 0; i < n; i++) data[i] *= 0.8f / peak;

            AudioClip clip = AudioClip.Create("Ambience_" + kind, n, 1, Rate, false);
            clip.SetData(data, 0);
            cache[key] = new[] { clip };
            return clip;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
        }
    }
}
