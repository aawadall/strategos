// PlayerPreferences.cs
// #307: player-facing options bag (settings screen). Distinct from save records (#355)
// and from the embedded player-store choice (#66) — this is a thin prefs document only.
// #388: Fullscreen + windowed WxH for graphics (Settings UI in #389/#390; apply on boot #391).

namespace Strategos.Preferences
{
    /// <summary>Persisted player options. Defaults match a fresh install.</summary>
    public sealed class PlayerPreferences
    {
        /// <summary>Format of this prefs document — bump when fields rename or semantics change.</summary>
        public int FormatVersion = 1;

        /// <summary>
        /// GAMEPLAY stub (#307): when true, destructive palette actions should ask first.
        /// No consumer yet — the settings toggle is the round-trip proof.
        /// </summary>
        public bool ConfirmOrders = false;

        /// <summary>
        /// GRAPHICS (#388): borderless fullscreen when true; otherwise windowed at
        /// <see cref="WindowWidth"/>×<see cref="WindowHeight"/>. Applied on boot in #391;
        /// Settings wires in #389.
        /// </summary>
        public bool Fullscreen = false;

        /// <summary>Windowed width in pixels (default matches AppShell remembered size).</summary>
        public int WindowWidth = 1600;

        /// <summary>Windowed height in pixels (default matches AppShell remembered size).</summary>
        public int WindowHeight = 900;

        /// <summary>
        /// AUDIO (#264): master bus gain 0–1. Drives <c>AudioListener.volume</c>
        /// (<see cref="Strategos.Audio.AudioService.ApplyPreferences"/>).
        /// </summary>
        public float MasterVolume = 1f;

        /// <summary>Music bed gain 0–1 (menu loop / PLAY ambient).</summary>
        public float MusicVolume = 0.7f;

        /// <summary>One-shot SFX gain 0–1.</summary>
        public float SfxVolume = 1f;
    }
}
