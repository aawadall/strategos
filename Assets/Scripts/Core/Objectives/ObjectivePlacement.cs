// ObjectivePlacement.cs
// #51 / #236: snap Objective.Cell from PlaceNearKind / PlaceNearName after map gen.
//
// Cell is what VictoryEvaluator uses. Feature refs are authoring intent only — resolve here,
// never inside the evaluator. Missing match leaves Cell unchanged and returns a problem for
// Validate (#237) or for ScenarioGenerator to clear the ref (#359 training fallback).

using System.Collections.Generic;
using UnityEngine;
using Strategos.Maps;
using Strategos.Scenarios;

namespace Strategos.Objectives
{
    public static class ObjectivePlacement
    {
        public static bool HasFeatureRef(Objective o) =>
            o != null && o.PlaceNearKind.HasValue;

        /// <summary>
        /// Resolve every feature-ref objective on <paramref name="scenario"/> against
        /// <paramref name="map"/>. Returns problems for refs that found no matching POI
        /// (Cell left as authored stub).
        /// </summary>
        public static List<string> Apply(Scenario scenario, MapData map)
        {
            var problems = new List<string>();
            if (scenario?.Objectives == null || map == null) return problems;

            for (int i = 0; i < scenario.Objectives.Count; i++)
            {
                var o = scenario.Objectives[i];
                if (!HasFeatureRef(o)) continue;

                if (!TryResolve(o, map, out var cell, out string err))
                {
                    problems.Add(err);
                    continue;
                }

                o.Cell = cell;
            }

            return problems;
        }

        /// <summary>
        /// Validate-only: every feature ref must match a POI on this map. Does not mutate.
        /// </summary>
        public static void ValidateRefs(Scenario scenario, MapData map, List<string> problems)
        {
            if (scenario?.Objectives == null || map == null || problems == null) return;

            for (int i = 0; i < scenario.Objectives.Count; i++)
            {
                var o = scenario.Objectives[i];
                if (!HasFeatureRef(o)) continue;
                if (!TryResolve(o, map, out _, out string err))
                    problems.Add(err);
            }
        }

        public static bool TryResolve(Objective o, MapData map, out Vector2 cell, out string error)
        {
            cell = o.Cell;
            error = null;
            if (o == null || map == null)
            {
                error = "Objective placement: null objective or map.";
                return false;
            }

            if (!o.PlaceNearKind.HasValue)
            {
                error = $"Objective {o.Id} has no feature ref.";
                return false;
            }

            var kind = o.PlaceNearKind.Value;
            string nameFilter = o.PlaceNearName ?? string.Empty;
            MapPoi best = null;
            float bestDist = float.PositiveInfinity;
            Vector2 hint = o.Cell;

            for (int i = 0; i < map.Pois.Count; i++)
            {
                var poi = map.Pois[i];
                if (poi == null || poi.Kind != kind) continue;
                if (nameFilter.Length > 0)
                {
                    string n = poi.Name ?? string.Empty;
                    if (n.IndexOf(nameFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }

                float d = (poi.Position - hint).sqrMagnitude;
                if (d >= bestDist) continue;
                bestDist = d;
                best = poi;
            }

            if (best == null)
            {
                error =
                    $"Objective {o.Id} '{o.Name}' PlaceNear {kind}" +
                    (nameFilter.Length > 0 ? $" name~'{nameFilter}'" : string.Empty) +
                    " matched no MapPoi on this map.";
                return false;
            }

            cell = best.Position;
            return true;
        }
    }
}
