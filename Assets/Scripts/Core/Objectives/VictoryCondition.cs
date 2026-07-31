// VictoryCondition.cs
// How a scenario ends.
//
// A flat struct-like class with a Kind discriminator, matching Command and SituationReport for
// the same reason: a handful of unused fields costs less than polymorphic serialisation in a
// file that has to round-trip identically.
//
// PRECEDENCE IS EXPLICIT AND MUST STAY THAT WAY. Two conditions can come true on the same
// evaluation — a side holds its objectives on the very tick the deadline passes — and "whichever
// the loop reached first" is a coin flip decided by list order. Priority is a field, ties break
// on list position, and both are documented rather than emergent.

using System;
using Strategos.Units;

namespace Strategos.Objectives
{
    public enum VictoryKind
    {
        None = 0,

        /// <summary>
        /// Reduce the enemy below a share of its starting combat power. Not "destroy every
        /// unit": a force at 20% has lost, and requiring annihilation turns the last five
        /// minutes of every scenario into mopping up.
        /// </summary>
        DestroyEnemy = 1,

        /// <summary>Hold every named objective, optionally for a duration.</summary>
        HoldObjectives = 2,

        /// <summary>Still be in the fight when the clock runs out. The defender's condition.</summary>
        SurviveUntil = 3,
    }

    [Serializable]
    public sealed class VictoryCondition
    {
        public VictoryKind Kind = VictoryKind.None;

        /// <summary>The side that <em>wins</em> if this comes true.</summary>
        public SideId Side;

        /// <summary>Shown when it triggers. Worth authoring: it is the game telling you why.</summary>
        public string Description = string.Empty;

        /// <summary>
        /// Higher wins when several trigger on the same evaluation. Equal priorities break on
        /// position in the scenario's list, which is stable and authored.
        /// </summary>
        public int Priority;

        // ─── Payload ──────────────────────────────────────────────────────────

        /// <summary>
        /// For <see cref="VictoryKind.DestroyEnemy"/>: the share of starting strength the
        /// enemy must be reduced below, as a percentage.
        /// </summary>
        public float StrengthThresholdPercent = 25f;

        /// <summary>For <see cref="VictoryKind.HoldObjectives"/>: every one of these.</summary>
        public int[] ObjectiveIds = Array.Empty<int>();

        /// <summary>
        /// For <see cref="VictoryKind.HoldObjectives"/>: how many consecutive ticks they must
        /// be held. Zero means holding them at any single evaluation is enough.
        ///
        /// Duration is what makes an objective a fight rather than a race — without it, driving
        /// a jeep through an undefended objective wins the scenario.
        /// </summary>
        public int HoldTicks;

        /// <summary>For <see cref="VictoryKind.SurviveUntil"/>: the tick to reach.</summary>
        public int DeadlineTick;

        public override string ToString()
        {
            string what = Kind switch
            {
                VictoryKind.DestroyEnemy => $"reduce enemy below {StrengthThresholdPercent:0}%",
                VictoryKind.HoldObjectives =>
                    $"hold {ObjectiveIds.Length} objective(s)" +
                    (HoldTicks > 0 ? $" for {HoldTicks} ticks" : string.Empty),
                VictoryKind.SurviveUntil => $"survive to t{DeadlineTick}",
                _ => Kind.ToString(),
            };
            return $"{Side} wins: {what}";
        }
    }
}
