// ControlMeasureKind.cs
// #161 / #160: APP-6D tactical-graphics families this project authors in scenarios.
//
// Point / line first (#162 / #163). Arrow and area follow as later children — the enum
// reserves their slots so JSON authored ahead of those issues deserialises without a
// FormatVersion bump, and unknown kinds stay drawable as "skip" rather than crash.

namespace Strategos.ControlMeasures
{
    public enum ControlMeasureKind
    {
        /// <summary>Named point — checkpoint (#162).</summary>
        Checkpoint = 0,

        /// <summary>Labelled polyline — phase line (#163).</summary>
        PhaseLine = 1,

        /// <summary>Polyline with echelon ticks — boundary (#163).</summary>
        Boundary = 2,

        // Reserved for #164 / #165 — not drawn until those land.
        AxisOfAdvance = 10,
        DirectionOfAttack = 11,
        BattlePosition = 20,
        EngagementArea = 21,
        KillZone = 22,
    }
}
