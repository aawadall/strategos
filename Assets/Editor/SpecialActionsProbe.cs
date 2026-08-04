// SpecialActionsProbe.cs
// #33 / #281: DigIn expands to Hold/Defend with the same dig-in clock and fire reduction;
// artillery (CanDigIn false) is refused.
//
// Menu:  Strategos > Probe Special Actions
// Batch: -executeMethod Strategos.Editor.SpecialActionsProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Combat;
using Strategos.Commands;
using Strategos.Scenarios;
using Strategos.SpecialActions;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class SpecialActionsProbe
    {
        [MenuItem("Strategos/Probe Special Actions")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckCapabilityFlag(log);
            bad += CheckDigInBridge(log);
            bad += CheckArtilleryRefuse(log);
            bad += CheckPaletteRow(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[SpecialActionsProbe]\n" + log);
            else Debug.LogError("[SpecialActionsProbe]\n" + log);
        }

        private static int CheckCapabilityFlag(StringBuilder log)
        {
            var cat = UnitCatalogue.Default();
            if (!cat.Get(UnitCatalogue.InfantryFoot).CanDigIn ||
                !cat.Get(UnitCatalogue.InfantryMech).CanDigIn)
            {
                log.AppendLine("  caps: FAILED — infantry must CanDigIn");
                return 1;
            }

            if (cat.Get(UnitCatalogue.Artillery).CanDigIn)
            {
                log.AppendLine("  caps: FAILED — artillery must not CanDigIn");
                return 1;
            }

            log.AppendLine("  caps: OK — infantry digs, artillery does not");
            return 0;
        }

        private static int CheckDigInBridge(StringBuilder log)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var sim = new Simulation(scenario, scenario.GenerateMap(), UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.AddExecutor(new DefendExecutor());

            UnitInstance unit = null;
            for (int i = 0; i < sim.Units.Count; i++)
            {
                var u = sim.Units[i];
                if (u.Capabilities(sim.Catalogue).CanDigIn)
                {
                    unit = u;
                    break;
                }
            }

            if (unit == null)
            {
                log.AppendLine("  dig-in: FAILED — no CanDigIn unit in skirmish");
                return 1;
            }

            var cmd = SpecialAction.TryCreate(ActionKind.DigIn, ActorId.ForSide(unit.Side),
                unit, sim.Catalogue);
            if (!cmd.HasValue || cmd.Value.Kind != CommandKind.Defend)
            {
                log.AppendLine("  dig-in: FAILED — TryCreate must emit Defend/Hold");
                return 1;
            }

            sim.Issue(cmd.Value);
            sim.Step(2);
            if (unit.Posture != Posture.Halted)
            {
                log.AppendLine($"  dig-in: FAILED — early posture {unit.Posture}");
                return 1;
            }

            sim.Step(DefendExecutor.DigInTicks);
            if (unit.Posture != Posture.DugIn)
            {
                log.AppendLine($"  dig-in: FAILED — after DigInTicks posture {unit.Posture}");
                return 1;
            }

            float halted = EngagementResolver.PostureFactor(Posture.Halted);
            float dug = EngagementResolver.PostureFactor(Posture.DugIn);
            if (dug >= halted)
            {
                log.AppendLine("  dig-in: FAILED — dug-in fire factor not reduced");
                return 1;
            }

            log.AppendLine(
                $"  dig-in: OK — DigInTicks={DefendExecutor.DigInTicks}, " +
                $"fire {dug:0.00} vs halted {halted:0.00} " +
                $"({(1f - dug / halted) * 100f:0}% less)");
            return 0;
        }

        private static int CheckArtilleryRefuse(StringBuilder log)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var sim = new Simulation(scenario, scenario.GenerateMap(), UnitCatalogue.Default());

            UnitInstance arty = null;
            for (int i = 0; i < sim.Units.Count; i++)
            {
                var u = sim.Units[i];
                if (u.CapabilityId == UnitCatalogue.Artillery)
                {
                    arty = u;
                    break;
                }
            }

            if (arty == null)
            {
                // Skirmish may not place artillery on both sides — synthesise a refuse case.
                var caps = sim.Catalogue.Get(UnitCatalogue.Artillery);
                if (caps.CanDigIn)
                {
                    log.AppendLine("  refuse: FAILED — artillery CanDigIn true");
                    return 1;
                }

                var fake = sim.Units[0];
                fake.CapabilityId = UnitCatalogue.Artillery;
                arty = fake;
            }

            var refused = SpecialAction.TryCreate(ActionKind.DigIn, ActorId.ForSide(arty.Side),
                arty, sim.Catalogue);
            if (refused.HasValue)
            {
                log.AppendLine("  refuse: FAILED — artillery DigIn should be null");
                return 1;
            }

            log.AppendLine("  refuse: OK — CanDigIn false yields no command");
            return 0;
        }

        private static int CheckPaletteRow(StringBuilder log)
        {
            if (!Strategos.UI.CommandPalette.TryGet(Strategos.UI.PaletteVerb.DigIn, out var def) ||
                def.Kind != CommandKind.Defend)
            {
                log.AppendLine("  palette: FAILED — DigIn row missing or wrong Kind");
                return 1;
            }

            log.AppendLine($"  palette: OK — DIG IN → {def.Kind} [{def.ShortcutLabel}]");
            return 0;
        }
    }
}
#endif
