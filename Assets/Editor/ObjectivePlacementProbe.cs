// ObjectivePlacementProbe.cs
// #51 / #237–#238: feature-ref resolve; missing kind fails Validate; PushNorth SpotHeight.
//
// Menu:  Strategos > Probe Objective Placement
// Batch: -executeMethod Strategos.Editor.ObjectivePlacementProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Maps;
using Strategos.Objectives;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class ObjectivePlacementProbe
    {
        [MenuItem("Strategos/Probe Objective Placement")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckPushNorthResolves(log);
            bad += CheckMissingRefFailsValidate(log);
            bad += CheckGeneratorPlaceNearOrFallback(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[ObjectivePlacementProbe]\n" + log);
            else Debug.LogError("[ObjectivePlacementProbe]\n" + log);
        }

        private static int CheckPushNorthResolves(StringBuilder log)
        {
            var scenario = ScenarioSamples.PushNorth();
            var obj = scenario.Objectives[0];
            if (!obj.PlaceNearKind.HasValue || obj.PlaceNearKind.Value != MapPoiKind.SpotHeight)
            {
                log.AppendLine("  push-north: FAILED — expected PlaceNearKind SpotHeight");
                return 1;
            }

            var stub = obj.Cell;
            var map = scenario.GenerateMap();
            obj = scenario.Objectives[0];

            if (Vector2.Distance(obj.Cell, stub) < 0.01f &&
                !MapHasPoiAt(map, obj.Cell, MapPoiKind.SpotHeight))
            {
                // Allowed only if stub happened to sit on a SpotHeight — still must Validate.
            }

            var problems = scenario.Validate(UnitCatalogue.Default(), map);
            if (problems.Count > 0)
            {
                log.AppendLine("  push-north: FAILED — " + string.Join("; ", problems));
                return 1;
            }

            if (!ObjectivePlacement.TryResolve(obj, map, out var resolved, out _))
            {
                log.AppendLine("  push-north: FAILED — TryResolve after GenerateMap");
                return 1;
            }

            if (Vector2.Distance(obj.Cell, resolved) > 0.01f)
            {
                log.AppendLine("  push-north: FAILED — Cell != resolved POI");
                return 1;
            }

            log.AppendLine(
                $"  push-north: OK — RIDGE at ({obj.Cell.x:0.##},{obj.Cell.y:0.##}) via SpotHeight");
            return 0;
        }

        private static int CheckMissingRefFailsValidate(StringBuilder log)
        {
            var scenario = ScenarioSamples.PushNorth();
            scenario.Objectives[0].PlaceNearKind = MapPoiKind.Bridge; // unlikely on culture-off plains
            scenario.Objectives[0].PlaceNearName = "__no_such_bridge__";
            var map = MapGenerator.Generate(scenario.Map);
            // Do not Apply successfully — force ValidateRefs failure
            var problems = new System.Collections.Generic.List<string>();
            ObjectivePlacement.ValidateRefs(scenario, map, problems);
            if (problems.Count == 0)
            {
                log.AppendLine("  missing: FAILED — expected ValidateRefs problem");
                return 1;
            }

            log.AppendLine("  missing: OK — bad feature ref reports a problem");
            return 0;
        }

        private static int CheckGeneratorPlaceNearOrFallback(StringBuilder log)
        {
            var settings = new ScenarioGenerationSettings
            {
                Seed = 20260804,
                Width = 64,
                Height = 64,
                EnableErosion = false,
                PlaceNearKind = MapPoiKind.SpotHeight,
            };
            var catalogue = UnitCatalogue.Default();
            var scenario = ScenarioGenerator.Generate(settings, out var map, catalogue);
            var problems = ScenarioGenerator.ValidateGenerated(scenario, map, settings, catalogue);
            if (problems.Count > 0)
            {
                log.AppendLine("  generator: FAILED — " + string.Join("; ", problems));
                return 1;
            }

            var o = scenario.Objectives[0];
            string mode = o.PlaceNearKind.HasValue ? $"PlaceNear {o.PlaceNearKind}" : "stub fallback";
            log.AppendLine($"  generator: OK — ValidateGenerated ({mode}) @ ({o.Cell.x:0},{o.Cell.y:0})");
            return 0;
        }

        private static bool MapHasPoiAt(MapData map, Vector2 cell, MapPoiKind kind)
        {
            for (int i = 0; i < map.Pois.Count; i++)
            {
                var p = map.Pois[i];
                if (p.Kind != kind) continue;
                if (Vector2.Distance(p.Position, cell) < 0.5f) return true;
            }
            return false;
        }
    }
}
#endif
