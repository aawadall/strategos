// ProceduralSfx.cs
// #250 / #44: tiny in-memory UI clicks — no Resources files. Deterministic sine blips
// so PLAY stays audible before sourced OGG lands under docs/sfx-inventory.md ids.

using UnityEngine;

namespace Strategos.Audio
{
    /// <summary>Builds short mono clips for UI feedback.</summary>
    public static class ProceduralSfx
    {
        private const int SampleRate = 22050;

        /// <summary>~40 ms soft click (~880 Hz) for buttons / tabs.</summary>
        public static AudioClip Click() => Tone("ui-click", 880f, 0.04f, 0.22f);

        /// <summary>~55 ms slightly higher blip for unit select.</summary>
        public static AudioClip Select() => Tone("ui-select", 1320f, 0.055f, 0.18f);

        /// <summary>~70 ms mid confirmation for an accepted order (#251).</summary>
        public static AudioClip OrderIssued() => Tone("order-issued", 660f, 0.07f, 0.2f);

        /// <summary>~90 ms lower / dissonant pair for a refused order (#251).</summary>
        public static AudioClip OrderRejected() => DualTone("order-rejected", 220f, 185f, 0.09f, 0.24f);

        /// <summary>~80 ms noisy crack for opening fire (#252).</summary>
        public static AudioClip CombatFire() => NoiseBurst("combat-fire", 0.08f, 0.28f);

        private static AudioClip Tone(string name, float hz, float seconds, float peak)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * seconds));
            var data = new float[samples];
            float twoPiF = 2f * Mathf.PI * hz / SampleRate;
            for (int i = 0; i < samples; i++)
            {
                float t = samples == 1 ? 0f : i / (float)(samples - 1);
                float env = t < 0.15f ? t / 0.15f : 1f - ((t - 0.15f) / 0.85f);
                if (env < 0f) env = 0f;
                data[i] = Mathf.Sin(twoPiF * i) * peak * env;
            }

            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip DualTone(string name, float hzA, float hzB, float seconds, float peak)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * seconds));
            var data = new float[samples];
            float a = 2f * Mathf.PI * hzA / SampleRate;
            float b = 2f * Mathf.PI * hzB / SampleRate;
            for (int i = 0; i < samples; i++)
            {
                float t = samples == 1 ? 0f : i / (float)(samples - 1);
                float env = t < 0.1f ? t / 0.1f : 1f - ((t - 0.1f) / 0.9f);
                if (env < 0f) env = 0f;
                data[i] = (Mathf.Sin(a * i) + Mathf.Sin(b * i)) * 0.5f * peak * env;
            }

            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip NoiseBurst(string name, float seconds, float peak)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * seconds));
            var data = new float[samples];
            // Deterministic LCG — same crack every run, no System.Random.
            uint state = 0xA341316Cu;
            for (int i = 0; i < samples; i++)
            {
                state = state * 1664525u + 1013904223u;
                float n = ((state >> 8) & 0xFFFF) / 65535f * 2f - 1f;
                float t = samples == 1 ? 0f : i / (float)(samples - 1);
                float env = 1f - t;
                data[i] = n * peak * env * env;
            }

            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
