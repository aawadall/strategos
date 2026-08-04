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
//
// #51: PlaceNearKind / PlaceNearName are authoring intent. Cell is the resolved centre the
// evaluator uses — ObjectivePlacement.Apply snaps Cell after map gen.

using System;
using UnityEngine;
using Strategos.Maps;
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

        /// <summary>
        /// Centre, in fractional cell coordinates. After <see cref="ObjectivePlacement.Apply"/>
        /// this is the resolved position (feature or authored coordinate).
        /// </summary>
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

        /// <summary>
        /// When set, <see cref="ObjectivePlacement"/> snaps <see cref="Cell"/> to the nearest
        /// matching <see cref="MapPoi"/> after map generation (#51 / #235). Null = coordinate
        /// only (existing authored behaviour).
        /// </summary>
        public MapPoiKind? PlaceNearKind;

        /// <summary>
        /// Optional case-insensitive substring against <see cref="MapPoi.Name"/>. Empty = any
        /// POI of <see cref="PlaceNearKind"/>.
        /// </summary>
        public string PlaceNearName = string.Empty;

        public bool Contains(Vector2 cell) =>
            (cell - Cell).sqrMagnitude <= RadiusCells * RadiusCells;

        public override string ToString() =>
            $"[{Id}] {Name} @ ({Cell.x:0},{Cell.y:0}) r{RadiusCells:0}" +
            (PlaceNearKind.HasValue ? $" near {PlaceNearKind}" : string.Empty);
    }
}
