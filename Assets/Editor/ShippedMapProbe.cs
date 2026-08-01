// ShippedMapProbe.cs
// Verifies the map the player actually loads — not the fourteen probes' map.
//
// #96: fourteen probes set `scenario.Map.EnableErosion = false` for speed, since erosion is
// the dominant generation cost and most of them do not care about terrain fidelity. But
// hydrology's fill and D8 routing run *after* erosion and depend on the surface it leaves, so
// disabling it changes where water pools, drains and gets classified — every one of those
// probes has been validating against a map the player never sees. #95 is the concrete result:
// `THE CROSSROADS` (now `OBJECTIVE ANVIL`) sits on `Forest` with erosion off and `Water` with
// it on, and the shipped scenario ships `EnableErosion: true`.
//
// This probe is deliberately the ONE place in the suite that reads `scenario.Map.EnableErosion`
// instead of overriding it. It does not replace the fourteen — they stay fast and erosion-off,
// which is safe precisely because this probe is the one thing that looks at the real map. Do
// not "fix" this by turning erosion on elsewhere or off here.
//
// Menu:  Strategos > Probe Shipped Map
// Batch: -executeMethod Strategos.Editor.ShippedMapProbe.Run

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Maps;
using Strategos.Movement;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class ShippedMapProbe
    {
        private const string ResourceDir = "Assets/Resources/Scenarios";

        [MenuItem("Strategos/Probe Shipped Map")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;
            var catalogue = UnitCatalogue.Default();

            var paths = ShippedScenarioPaths();
            if (paths.Count == 0)
            {
                log.AppendLine($"  FAIL no scenario JSON found under {ResourceDir}");
                bad++;
            }

            foreach (var path in paths)
                bad += CheckScenario(path, catalogue, log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[ShippedMapProbe]\n" + log);
            else Debug.LogError("[ShippedMapProbe]\n" + log);
        }

        private static List<string> ShippedScenarioPaths()
        {
            var found = new List<string>();
            if (!Directory.Exists(ResourceDir)) return found;
            found.AddRange(Directory.GetFiles(ResourceDir, "*.json"));
            found.Sort();
            return found;
        }

        // ─── Per scenario ─────────────────────────────────────────────────────

        private static int CheckScenario(string path, UnitCatalogue catalogue, StringBuilder log)
        {
            int bad = 0;
            string name = Path.GetFileNameWithoutExtension(path);
            var scenario = ScenarioIO.LoadFromFile(path);

            if (scenario == null)
            {
                log.AppendLine($"  FAIL '{name}' at {path} did not load");
                return bad + 1;
            }

            // THE WHOLE REASON THIS PROBE EXISTS: generate with the setting the scenario
            // ships, never override it. Every other probe sets this to false for speed.
            log.AppendLine($"--- {name}  (EnableErosion={scenario.Map.EnableErosion}) ---");
            var map = scenario.GenerateMap();

            // One MovementGrid per distinct capability, reused across all three checks —
            // building one is proportional to map area and there is no reason to pay for it
            // twice for the same (map, capability) pair.
            var grids = new Dictionary<string, MovementGrid>();
            MovementGrid GridFor(string capabilityId)
            {
                if (grids.TryGetValue(capabilityId, out var g)) return g;
                var caps = catalogue.Get(capabilityId);
                g = MovementGrid.Build(map, caps);
                grids[capabilityId] = g;
                return g;
            }

            var capabilityIds = new List<string>();
            var seenCaps = new HashSet<string>();
            foreach (var u in scenario.Units)
                if (seenCaps.Add(u.CapabilityId)) capabilityIds.Add(u.CapabilityId);

            bad += CheckObjectivesPassable(scenario, map, capabilityIds, GridFor, log);
            bad += CheckUnitsPassable(scenario, map, GridFor, log);
            bad += CheckObjectivesReachablePerSide(scenario, map, GridFor, log);

            return bad;
        }

        // ─── 1. Every objective's cell is passable ───────────────────────────
        //
        // Checked against every distinct capability id fielded in the scenario, since
        // passability is a property of the unit (UnitCapabilities.ImpassableLandcover), not
        // of the map alone. The landcover class is printed regardless of outcome — the number
        // is the point, not the verdict, as CombatProbe's table is.

        private static int CheckObjectivesPassable(Scenario scenario, MapData map,
            List<string> capabilityIds, System.Func<string, MovementGrid> gridFor,
            StringBuilder log)
        {
            int bad = 0;

            foreach (var objective in scenario.Objectives)
            {
                var cell = RoundCell(objective.Cell, map);
                var cover = map.GetLandcover(cell.x, cell.y);
                float slope = map.SampleSlopeDegrees(cell.x, cell.y);

                log.AppendLine($"  objective '{objective.Name}' at {cell}: " +
                                $"{LandcoverInfo.DisplayName(cover)}, {slope:0.#}deg");

                foreach (var capabilityId in capabilityIds)
                {
                    var grid = gridFor(capabilityId);
                    bool ok = grid != null && grid.Passable(cell.x, cell.y);
                    log.AppendLine($"    {capabilityId,-14} {(ok ? "passable" : "IMPASSABLE")}");
                    if (!ok)
                    {
                        log.AppendLine($"  FAIL objective '{objective.Name}' at {cell} is " +
                                        $"{LandcoverInfo.DisplayName(cover)}, which '{capabilityId}' " +
                                        "cannot occupy.");
                        bad++;
                    }
                }
            }

            // Zero objectives is a legal scenario state — Scenario.ValidateVictory only
            // requires ObjectiveIds on a HoldObjectives condition, not that the scenario
            // field itself be non-empty — so this is not a FAIL. But a loop over an empty
            // list prints nothing either way, and "nothing printed" is indistinguishable from
            // "checked and found zero problems" versus "never ran, or scenario.Objectives was
            // silently empty by a bug upstream." Match CheckUnitsPassable just below: state
            // the count so the difference is visible in the log, same as #96's whole point —
            // this probe exists so nothing about the real shipped map goes unstated.
            if (bad == 0)
                log.AppendLine($"  all {scenario.Objectives.Count} objective cell(s) passable");

            return bad;
        }

        // ─── 2. Every unit's start cell is passable ──────────────────────────

        private static int CheckUnitsPassable(Scenario scenario, MapData map,
            System.Func<string, MovementGrid> gridFor, StringBuilder log)
        {
            int bad = 0;

            foreach (var unit in scenario.Units)
            {
                var cell = RoundCell(unit.Cell, map);
                var grid = gridFor(unit.CapabilityId);
                bool ok = grid != null && grid.Passable(cell.x, cell.y);

                if (!ok)
                {
                    var cover = map.GetLandcover(cell.x, cell.y);
                    log.AppendLine($"  FAIL unit '{unit.Designation}' ({unit.CapabilityId}) " +
                                    $"starts at {cell} on {LandcoverInfo.DisplayName(cover)}, " +
                                    "which it cannot occupy.");
                    bad++;
                }
            }

            if (bad == 0)
                log.AppendLine($"  all {scenario.Units.Count} unit start cell(s) passable");

            return bad;
        }

        // ─── 3. Every objective reachable from every side ────────────────────
        //
        // A real PathFinder.Find, per side, using that side's own fighting units (the tree's
        // leaves — a formation does not move itself and never fights, see UnitHierarchy). An
        // objective no side can reach is a broken scenario; one only one side can reach is
        // worse, because it looks playable right up until the side that cannot get there
        // never contests it — which is exactly what #95 measured: 36 autonomous orders,
        // objective never reached, draw at the time limit.

        private static int CheckObjectivesReachablePerSide(Scenario scenario, MapData map,
            System.Func<string, MovementGrid> gridFor, StringBuilder log)
        {
            int bad = 0;
            var hierarchy = new UnitHierarchy(scenario.Units);

            foreach (var objective in scenario.Objectives)
            {
                var goal = RoundCell(objective.Cell, map);

                foreach (var side in scenario.Sides)
                {
                    int reachable = 0, total = 0;
                    var detail = new List<string>();

                    foreach (var leaf in hierarchy.Leaves)
                    {
                        if (leaf.Side != side.Id) continue;
                        total++;

                        var grid = gridFor(leaf.CapabilityId);
                        var start = RoundCell(leaf.Cell, map);
                        var result = PathFinder.Find(grid, start, goal);

                        if (result.Found)
                        {
                            reachable++;
                        }
                        else
                        {
                            bool goalPassable = grid != null && grid.Passable(goal.x, goal.y);
                            detail.Add($"{leaf.Designation} ({leaf.CapabilityId}) " +
                                       $"goalPassable={goalPassable} expanded={result.Expanded}");
                        }
                    }

                    log.AppendLine($"  side {side.Name,-8} -> '{objective.Name}': " +
                                    $"{reachable}/{total} leaf unit(s) reach it");

                    if (total > 0 && reachable == 0)
                    {
                        log.AppendLine($"  FAIL side {side.Name} cannot reach objective " +
                                        $"'{objective.Name}' with any unit: {string.Join("; ", detail)}");
                        bad++;
                    }
                }
            }

            return bad;
        }

        private static Vector2Int RoundCell(Vector2 cell, MapData map) => new(
            Mathf.Clamp(Mathf.RoundToInt(cell.x), 0, map.Width - 1),
            Mathf.Clamp(Mathf.RoundToInt(cell.y), 0, map.Height - 1));
    }
}
#endif
