// ControlMeasureKind.cs
// #161 / #160: APP-6D tactical-graphics families this project authors in scenarios.
//
// Point / line (#162 / #163), arrows (#164), areas (#165). Numeric slots are stable so
// older JSON deserialises without a FormatVersion bump; unknown values fail validation.

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

        /// <summary>Axis of advance — role in <see cref="AxisOfAdvanceRole"/> (#164).</summary>
        AxisOfAdvance = 10,

        /// <summary>Direction of attack arrow (#164).</summary>
        DirectionOfAttack = 11,

        /// <summary>Retirement arrow (#164).</summary>
        Retirement = 12,

        /// <summary>Counterattack arrow (#164).</summary>
        Counterattack = 13,

        /// <summary>Battle position polygon (#165).</summary>
        BattlePosition = 20,

        /// <summary>Engagement area polygon (#165).</summary>
        EngagementArea = 21,

        /// <summary>Kill zone polygon (#165).</summary>
        KillZone = 22,
    }

    /// <summary>APP-6D axis-of-advance variants — same kind, different stroke.</summary>
    public enum AxisOfAdvanceRole
    {
        Main = 0,
        Supporting = 1,
        Deception = 2,
    }
}
