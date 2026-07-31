// Objective.cs
// Ground worth taking, and the rule for who holds it.
//
// DEFINITION ONLY, NO RUNTIME STATE. Who currently holds an objective, and for how long, lives
// in VictoryEvaluator. The split is the same one UnitInstance's header describes and defers:
// here it is worth making immediately, because an objective's definition is authored in JSON
// and its control changes every few seconds. Storing both together would put a value that
// changes constantly into a file that should only ever change when a designer edits it.
//
// NOT NECESSARILY SCENARIO DATA FOR EVER. Objectives are authored in the scenario today. Under
// the command-chain model (#36) they arrive as the content of a *directive* from a higher
// formation, which is why nothing here reaches for a global list and why VictoryEvaluator is
// handed its objectives rather than fetching them.

using System;
using UnityEngine;
using Strategos.Units;

namespace Strategos.Objectives
{
    [Serializable]
    public sealed class Objective
    {
        /// <summary>Stable id, referenced by <see cref="VictoryCondition.ObjectiveIds"/>.</summary>
        public int Id;

        /// <summary>What it is called in a briefing — "HILL 232", "THE BRIDGE".</summary>
        public string Name = "Objective";

        /// <summary>Centre, in fractional cell coordinates like everything else positional.</summary>
        public Vector2 Cell;

        /// <summary>
        /// How close a unit must be to count as holding it, in cells.
        ///
        /// A radius rather than a polygon. A polygon is more expressive and needs an editor to
        /// author, a point-in-polygon test per unit per evaluation, and a rendering path — for
        /// a first playable that buys nothing a circle does not.
        /// </summary>
        public float RadiusCells = 6f;

        /// <summary>Who holds it at the start. <see cref="SideId.None"/> for neutral ground.</summary>
        public SideId InitialOwner;

        public bool Contains(Vector2 cell) =>
            (cell - Cell).sqrMagnitude <= RadiusCells * RadiusCells;

        public override string ToString() =>
            $"[{Id}] {Name} @ ({Cell.x:0},{Cell.y:0}) r{RadiusCells:0}";
    }
}
