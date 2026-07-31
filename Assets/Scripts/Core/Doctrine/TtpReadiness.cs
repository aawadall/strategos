// TtpReadiness.cs
// How ready a unit is to execute a given drill.
//
// A drill is something a unit *trains on*, so "can this unit actually do 1A right now" is a
// question the binder should answer — otherwise it is a reference book in a command post that
// knows nothing about the command. It turns the binder from a glossary into a readiness view,
// which is a real command-post function rather than a UI flourish.
//
// T / P / U ARE THE REAL RATINGS. The Army rates proficiency on a task as Trained, needs
// Practice, or Untrained, and using that scale costs nothing over inventing one — the same
// argument that keeps the battle drills on their real numbers. It also reads correctly to
// anyone who has seen a training briefing.
//
// DERIVED, NOT AUTHORED. Nothing in the scenario format records which drills a unit has
// rehearsed, and adding a units-by-drills matrix to author by hand would be a lot of data
// for a judgement that is mostly mechanical: is this unit big enough for the task, and is it
// in a fit state to attempt it. Deriving means every scenario gets ratings for free and none
// of them can go stale against the unit's real condition. When training state becomes a real
// attribute — it is the missing half of doctrine profiles (docs/phases.md 3) — this becomes
// the fallback rather than the whole answer.

using Strategos.NatoSymbols;
using Strategos.Units;

namespace Strategos.Doctrine
{
    /// <summary>Proficiency on a task, on the Army's own scale.</summary>
    public enum DrillRating
    {
        /// <summary>Untrained on this task, or unable to attempt it.</summary>
        Untrained = 0,

        /// <summary>Needs practice. Capable, but not at standard.</summary>
        Practice = 1,

        /// <summary>Trained to standard.</summary>
        Trained = 2,
    }

    /// <summary>A unit's rating on one drill, and why.</summary>
    public readonly struct DrillReadiness
    {
        public readonly DrillRating Rating;

        /// <summary>Short reason, for the line under the rating. Never empty.</summary>
        public readonly string Reason;

        public DrillReadiness(DrillRating rating, string reason)
        {
            Rating = rating;
            Reason = reason;
        }

        /// <summary>The one-letter code, as a training brief writes it.</summary>
        public string Code => Rating switch
        {
            DrillRating.Trained => "T",
            DrillRating.Practice => "P",
            _ => "U",
        };
    }

    public static class TtpReadiness
    {
        /// <summary>Effectiveness at or above which a unit is at standard.</summary>
        private const float TrainedAt = 0.85f;

        /// <summary>Below this a unit cannot be expected to execute a drill at all.</summary>
        private const float UntrainedBelow = 0.55f;

        /// <summary>
        /// Rates a unit against a drill.
        /// </summary>
        /// <remarks>
        /// Two gates, in order, and the order matters. Echelon is checked first because it is
        /// categorical: a squad at full strength still cannot execute a platoon attack, and
        /// reporting it as "needs practice" would imply practice would fix it.
        ///
        /// Condition comes second and reads <see cref="UnitInstance.Effectiveness"/> rather
        /// than strength alone, because that already folds in readiness and suppression — the
        /// same number the combat model uses. A separate formula here would let the binder
        /// disagree with the fight the player is watching.
        /// </remarks>
        public static DrillReadiness Assess(Ttp drill, UnitInstance unit)
        {
            if (drill == null || unit == null)
                return new DrillReadiness(DrillRating.Untrained, "no unit");

            if (unit.IsDestroyed)
                return new DrillReadiness(DrillRating.Untrained, "combat ineffective");

            var unitEchelon = EchelonOf(unit);
            if (unitEchelon < drill.Echelon)
                return new DrillReadiness(DrillRating.Untrained,
                    $"above this unit's echelon ({drill.EchelonName.ToLowerInvariant()} task)");

            float effectiveness = unit.Effectiveness;

            if (effectiveness < UntrainedBelow)
                return new DrillReadiness(DrillRating.Untrained,
                    $"too degraded  ({effectiveness * 100f:0}% effective)");

            if (effectiveness < TrainedAt)
                return new DrillReadiness(DrillRating.Practice,
                    $"degraded  ({effectiveness * 100f:0}% effective)");

            // A unit larger than the drill's echelon executes it through its subordinates,
            // which is how a platoon runs a squad drill. Worth saying, because otherwise a
            // platoon rated T on a fire-team task looks like a mistake.
            return unitEchelon > drill.Echelon
                ? new DrillReadiness(DrillRating.Trained, "trained  ·  by subordinate element")
                : new DrillReadiness(DrillRating.Trained, "trained to standard");
        }

        /// <summary>
        /// The drill echelon a unit counts as.
        /// </summary>
        /// <remarks>
        /// Read from the unit's SIDC, which is where echelon actually lives — there is no
        /// separate field, and inventing one would give two answers that could disagree.
        /// Everything company and above collapses to Company: the drills here stop at platoon,
        /// and a brigade is not more able to run a fire-team drill than a company is.
        /// </remarks>
        public static DrillEchelon EchelonOf(UnitInstance unit)
        {
            var echelon = unit.ToSidcCode().Echelon;
            return echelon switch
            {
                Echelon.Team => DrillEchelon.Team,
                Echelon.Squad => DrillEchelon.Squad,
                Echelon.Section => DrillEchelon.Squad,
                Echelon.Platoon => DrillEchelon.Platoon,
                Echelon.None => DrillEchelon.Team,
                _ => DrillEchelon.Company,
            };
        }
    }
}
