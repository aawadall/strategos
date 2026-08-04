// PlayerPreferences.cs
// #307: player-facing options bag (settings screen). Distinct from save records (#355)
// and from the embedded player-store choice (#66) — this is a thin prefs document only.

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
    }
}
