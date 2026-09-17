// PlayerPreferences.cs
// #307: player-facing options bag (settings screen). Distinct from save records (#355)
// and from the embedded player-store choice (#66) — this is a thin prefs document only.
// #388: Fullscreen + windowed WxH for graphics (Settings UI in #389/#390; apply on boot #391).
// #520: map render mode + layer toggles, mirroring MapRenderOptions.Default so a fresh
// install renders exactly what session-only options already did.

using Strategos.Maps;

namespace Strategos.Preferences
{
    /// <summary>Persisted player options. Defaults match a fresh install.</summary>
    public sealed class PlayerPreferences
    {
        /// <summary>Format of this prefs document — bump when fields rename or semantics change.</summary>
        public int FormatVersion = 2;

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

        /// <summary>
        /// VISUAL (#520): map presentation style. Session-only until now — Settings wiring
        /// is #521, views honouring the persisted value are #522.
        /// </summary>
        public MapRenderMode MapRenderMode = MapRenderMode.Topographic;

        /// <summary>Relief shading layer. Defaults match <see cref="MapRenderOptions.Default"/>.</summary>
        public bool DrawHillshade = true;

        /// <summary>Elevation contour lines.</summary>
        public bool DrawContours = true;

        /// <summary>Filled area features — woods, built-up, water.</summary>
        public bool DrawAreas = true;

        /// <summary>Linear features — drainage, rail, roads.</summary>
        public bool DrawLines = true;

        /// <summary>Settlement and spot-height point marks.</summary>
        public bool DrawPois = true;

        /// <summary>Text labels for point marks and contours.</summary>
        public bool DrawLabels = true;

        /// <summary>Coordinate reference grid overprint.</summary>
        public bool DrawGrid = true;
    }
}
