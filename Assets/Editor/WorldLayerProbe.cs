// WorldLayerProbe.cs
// #34 / #277: spawn changes Signature; HazardBlocking blocks PathFinder; despawn restores
// passability; twin runs stay signature-equal (replay-safe).
//
// Menu:  Strategos > Probe World Layer
// Batch: -executeMethod Strategos.Editor.WorldLayerProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Maps;
using Strategos.Movement;
using Strategos.Scenarios;
using Strategos.Units;
using Strategos.World;

namespace Strategos.Editor
{
    public static class WorldLayerProbe
    {
        [MenuItem("Strategos/Probe World Layer")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckSpawnSignature(log);
            bad += CheckBlocksMovement(log);
            bad += CheckDespawn(log);
            bad += CheckDeterminism(log);
            bad += CheckDrawerPixels(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[WorldLayerProbe]\n" + log);
            else Debug.LogError("[WorldLayerProbe]\n" + log);
        }

        private static Simulation Fresh()
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var map = scenario.GenerateMap();
            var sim = new Simulation(scenario, map, UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            return sim;
        }

        private static int CheckSpawnSignature(StringBuilder log)
        {
            var sim = Fresh();
            string before = sim.Signature();
            int id = sim.SpawnWorldObject(WorldObjectKind.HazardBlocking, new Vector2Int(40, 40));
            string after = sim.Signature();
            if (id <= 0 || before == after)
            {
                log.AppendLine("  signature: FAILED — spawn must change Signature");
                return 1;
            }

            log.AppendLine($"  signature: OK — spawn id={id} changed Signature");
            return 0;
        }

        private static int CheckBlocksMovement(StringBuilder log)
        {
            var sim = Fresh();
            var caps = UnitCatalogue.Default().Get(UnitCatalogue.InfantryFoot);
            var grid = MovementGrid.Build(sim.Map, caps, sim.World);

            // Find a passable cell that is not a unit start, place hazard, assert impassable.
            Vector2Int cell = default;
            bool found = false;
            for (int y = 20; y < sim.Map.Height - 20 && !found; y++)
            for (int x = 20; x < sim.Map.Width - 20 && !found; x++)
            {
                if (!grid.Passable(x, y)) continue;
                cell = new Vector2Int(x, y);
                found = true;
            }

            if (!found)
            {
                log.AppendLine("  block: FAILED — no passable cell for hazard");
                return 1;
            }

            sim.SpawnWorldObject(WorldObjectKind.HazardBlocking, cell, lifetimeTicks: -1);
            if (grid.Passable(cell.x, cell.y))
            {
                log.AppendLine($"  block: FAILED — {cell} still Passable after hazard");
                return 1;
            }

            // Neighbour to neighbour path through the hazard cell must fail or avoid it.
            var from = cell + new Vector2Int(-2, 0);
            var to = cell + new Vector2Int(2, 0);
            if (!grid.Passable(from.x, from.y) || !grid.Passable(to.x, to.y))
            {
                // Fall back: just assert BlocksMovement
                if (!sim.World.BlocksMovement(cell.x, cell.y))
                {
                    log.AppendLine("  block: FAILED — BlocksMovement false");
                    return 1;
                }
                log.AppendLine($"  block: OK — {cell} impassable (neighbour path skipped)");
                return 0;
            }

            var path = PathFinder.Find(grid, from, to);
            if (path.Found && path.Cells != null && path.Cells.Count > 0)
            {
                for (int i = 0; i < path.Cells.Count; i++)
                {
                    if (path.Cells[i] == cell)
                    {
                        log.AppendLine("  block: FAILED — path walks through hazard");
                        return 1;
                    }
                }
            }

            log.AppendLine($"  block: OK — {cell} impassable; path avoids or empty");
            return 0;
        }

        private static int CheckDespawn(StringBuilder log)
        {
            var sim = Fresh();
            var caps = UnitCatalogue.Default().Get(UnitCatalogue.InfantryFoot);
            var grid = MovementGrid.Build(sim.Map, caps, sim.World);

            Vector2Int cell = new(50, 50);
            for (int r = 0; r < 40; r++)
            {
                int x = 50 + (r % 10), y = 50 + (r / 10);
                if (grid.Passable(x, y)) { cell = new Vector2Int(x, y); break; }
            }

            int id = sim.SpawnWorldObject(WorldObjectKind.HazardBlocking, cell);
            if (grid.Passable(cell.x, cell.y))
            {
                log.AppendLine("  despawn: FAILED — not blocked after spawn");
                return 1;
            }

            if (!sim.DespawnWorldObject(id) || !grid.Passable(cell.x, cell.y))
            {
                log.AppendLine("  despawn: FAILED — cell not passable after Despawn");
                return 1;
            }

            log.AppendLine($"  despawn: OK — {cell} passable again");
            return 0;
        }

        private static int CheckDeterminism(StringBuilder log)
        {
            string RunOnce()
            {
                var sim = Fresh();
                sim.SpawnWorldObject(WorldObjectKind.HazardBlocking, new Vector2Int(60, 60), 50);
                for (int i = 0; i < 10; i++) sim.Step();
                return sim.Signature();
            }

            string a = RunOnce();
            string b = RunOnce();
            if (a != b)
            {
                log.AppendLine("  determinism: FAILED — twin runs diverged");
                return 1;
            }

            log.AppendLine("  determinism: OK — twin signatures match");
            return 0;
        }

        private static int CheckDrawerPixels(StringBuilder log)
        {
            var sim = Fresh();
            var cell = new Vector2Int(sim.Map.Width / 2, sim.Map.Height / 2);
            sim.SpawnWorldObject(WorldObjectKind.HazardBlocking, cell);

            var options = MapRenderOptions.Default;
            options.PixelsPerCell = 2f;
            var pixels = MapRasterizer.RenderPixels(sim.Map, options, out var view);
            var before = (Color32[])pixels.Clone();
            WorldObjectDrawer.Draw(pixels, view, sim.World.Objects);

            bool changed = false;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].r != before[i].r || pixels[i].g != before[i].g ||
                    pixels[i].b != before[i].b || pixels[i].a != before[i].a)
                {
                    changed = true;
                    break;
                }
            }

            if (!changed)
            {
                log.AppendLine("  draw: FAILED — drawer wrote no pixels");
                return 1;
            }

            log.AppendLine("  draw: OK — hazard mark changed sheet pixels");
            return 0;
        }
    }
}
#endif
