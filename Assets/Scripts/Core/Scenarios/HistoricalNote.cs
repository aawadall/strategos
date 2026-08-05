// HistoricalNote.cs
// #462 / #421: authored historical commentary on a Scenario (briefing / outcome).
// Distinct from Description and from Directive (orders from higher).

namespace Strategos.Scenarios
{
    /// <summary>When a historical note should surface in PLAY.</summary>
    public enum HistoricalNoteWhen
    {
        /// <summary>Shown at scenario start / on demand from pause.</summary>
        Briefing = 0,

        /// <summary>Shown after the fight (AAR / outcome) — v1 surfaces via pause too.</summary>
        Outcome = 1,
    }

    /// <summary>One historically-grounded note. Public fields for FieldsOnlyResolver.</summary>
    public sealed class HistoricalNote
    {
        public HistoricalNoteWhen When = HistoricalNoteWhen.Briefing;

        public string Text = string.Empty;
    }
}
