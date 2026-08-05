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

        private static AudioClip Tone(string name, float hz, float seconds, float peak)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * seconds));
            var data = new float[samples];
            float twoPiF = 2f * Mathf.PI * hz / SampleRate;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)(samples - 1);
                // Linear attack/decay envelope — avoids a hard edge click on the clip itself.
                float env = t < 0.15f ? t / 0.15f : 1f - ((t - 0.15f) / 0.85f);
                if (env < 0f) env = 0f;
                data[i] = Mathf.Sin(twoPiF * i) * peak * env;
            }

            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
