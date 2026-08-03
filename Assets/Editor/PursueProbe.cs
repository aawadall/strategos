// PursueProbe.cs
// Pursue closes tighter than Attack (PursueStandoffCells) then Engages.
//
// Menu:  Strategos > Probe Pursue
// Batch: -executeMethod Strategos.Editor.PursueProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class PursueProbe
    {
        [MenuItem("Strategos/Probe Pursue")]
        public static void Run()
        {
            var log = new StringBuilder();
            bool ok = ClosesTighterThanAttack(log);
            Debug.Log(log.ToString());
            Debug.Log(ok ? "[PursueProbe] PROBE PASSED" : "[PursueProbe] PROBE FAILED");
        }

        private static bool ClosesTighterThanAttack(StringBuilder log)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var sim = new Simulation(scenario, scenario.GenerateMap(), UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());

            UnitInstance unit = null, hostile = null;
            for (int i = 0; i < sim.Units.Count; i++)
            {
                var u = sim.Units[i];
                if (u.Side.Value == 1) unit ??= u;
                else hostile ??= u;
            }
            if (unit == null || hostile == null)
            {
                log.AppendLine("  pursue: FAILED, need friendly and hostile");
                return false;
            }

            unit.Cell = new Vector2(40f, 40f);
            hostile.Cell = new Vector2(40f + 50f, 40f);

            if (!(Simulation.PursueStandoffCells < Simulation.AttackStandoffCells))
            {
                log.AppendLine("  pursue: FAILED, Pursue standoff must be tighter than Attack");
                return false;
            }

            sim.Issue(Command.Pursue(ActorId.ForSide(unit.Side), unit.Id, hostile.Id));
            sim.Step(400);

            float endDist = Vector2.Distance(unit.Cell, hostile.Cell);
            if (endDist > Simulation.AttackStandoffCells)
            {
                log.AppendLine($"  pursue: FAILED, ended at {endDist:0.0} cells — " +
                               $"expected inside Attack standoff ({Simulation.AttackStandoffCells})");
                return false;
            }

            bool sawEngage = false;
            foreach (var c in sim.Log.Entries)
                if (c.Kind == CommandKind.Engage && c.AgainstUnit == hostile.Id) sawEngage = true;

            if (!sawEngage)
            {
                log.AppendLine("  pursue: FAILED, no Engage in the log");
                return false;
            }

            log.AppendLine($"  pursue: closed to {endDist:0.0} cells " +
                           $"(Pursue {Simulation.PursueStandoffCells}, Attack {Simulation.AttackStandoffCells}), " +
                           "Engage issued  ok");
            return true;
        }
    }
}
#endif
