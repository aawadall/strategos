// DrillBindability.cs
// #102: predict whether ExpandDrill would issue at least one mechanised step.
//
// Mirrors Simulation.Bind / NearestHostile without taking a Simulation reference. Threat is
// ground truth on purpose — ExpandDrill still binds that way — so a mask that agrees with
// ExpandDrill's accept/reject does not invent a second threat model. Belief-aligned binding
// waits until ExpandDrill itself reads contacts (#34).

using System.Collections.Generic;
using UnityEngine;
using Strategos.Doctrine;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Actions
{
    public static class DrillBindability
    {
        /// <summary>
        /// True when at least one mechanised step of <paramref name="drill"/> would bind for
        /// <paramref name="unit"/> under the same rules as <c>Simulation.ExpandDrill</c>.
        /// </summary>
        public static bool CanBindAnyMechanisedStep(
            UnitInstance unit,
            Ttp drill,
            IReadOnlyList<UnitInstance> units,
            Scenario scenario)
        {
            if (unit == null || drill?.Steps == null) return false;

            var threat = NearestHostile(unit, units, scenario);
            for (int i = 0; i < drill.Steps.Length; i++)
            {
                var step = drill.Steps[i];
                if (!step.IsMechanised) continue;
                if (WouldBind(unit, step, threat)) return true;
            }

            return false;
        }

        /// <summary>Nearest living hostile fighting unit, scenario order, strict distance ties.</summary>
        public static UnitInstance NearestHostile(
            UnitInstance unit,
            IReadOnlyList<UnitInstance> units,
            Scenario scenario)
        {
            if (unit == null || units == null) return null;

            var side = scenario?.FindSide(unit.Side);
            UnitInstance best = null;
            float bestSq = float.MaxValue;

            for (int i = 0; i < units.Count; i++)
            {
                var other = units[i];
                if (other == null || other.IsDestroyed || other.Id == unit.Id) continue;
                if (!Side.AreHostile(side, scenario?.FindSide(other.Side))) continue;

                float sq = (other.Cell - unit.Cell).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = other; }
            }

            return best;
        }

        /// <summary>Same cases as Simulation.Bind — success only, no Command allocation.</summary>
        public static bool WouldBind(UnitInstance unit, in TtpStep step, UnitInstance threat)
        {
            switch (step.Binding)
            {
                case StepBinding.Here:
                    return true;

                case StepBinding.AtThreat:
                    return threat != null;

                case StepBinding.TowardThreat:
                case StepBinding.AwayFromThreat:
                    if (threat == null) return false;
                    return (unit.Cell - threat.Cell).sqrMagnitude >= 0.0001f;
            }

            return false;
        }
    }
}
