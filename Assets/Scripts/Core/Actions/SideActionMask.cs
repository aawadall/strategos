// SideActionMask.cs
// #102: which SideActionSpace indices are legal for one unit right now.
//
// Gates (all must pass): alive, idle queue, then per-action — drills need a known code,
// TtpReadiness not Untrained, and (leaves only) at least one bindable mechanised step;
// ADVANCE needs an unheld objective. Readiness is stricter than ExpandDrill on purpose:
// the sim still expands Untrained drills; the mask will not offer them to a policy.
//
// ROE is not gated here — ExpandDrill ignores it too.

using System.Collections.Generic;
using Strategos.Commands;
using Strategos.Doctrine;
using Strategos.Objectives;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Actions
{
    public static class SideActionMask
    {
        /// <summary>
        /// Fixed-length legality mask aligned with <see cref="SideActionSpace"/> indices.
        /// </summary>
        public static bool[] Encode(
            UnitInstance unit,
            CommandQueue queue,
            IReadOnlyList<UnitInstance> units,
            Scenario scenario,
            UnitHierarchy hierarchy,
            VictoryEvaluator victory)
        {
            var mask = new bool[SideActionSpace.Count];

            if (unit == null || unit.IsDestroyed) return mask;
            if (queue != null && !queue.IsEmpty) return mask;

            bool isFormation = hierarchy != null && hierarchy.IsFormation(unit.Id);
            bool hasAdvanceTarget = SideActionSpace.NearestUnheld(unit, victory) != null;

            for (int i = 0; i < SideActionSpace.Count; i++)
            {
                if (SideActionSpace.IsAdvance(i))
                {
                    mask[i] = hasAdvanceTarget;
                    continue;
                }

                var drill = TtpLibrary.Find(SideActionSpace.CodeAt(i));
                if (drill == null) continue;

                if (TtpReadiness.Assess(drill, unit).Rating == DrillRating.Untrained)
                    continue;

                if (!isFormation &&
                    !DrillBindability.CanBindAnyMechanisedStep(unit, drill, units, scenario))
                    continue;

                mask[i] = true;
            }

            return mask;
        }

        /// <summary>Count of legal actions — probe diagnostics.</summary>
        public static int LegalCount(bool[] mask)
        {
            if (mask == null) return 0;
            int n = 0;
            for (int i = 0; i < mask.Length; i++) if (mask[i]) n++;
            return n;
        }
    }
}
