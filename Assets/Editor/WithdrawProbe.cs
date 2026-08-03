// WithdrawProbe.cs
// Withdraw expands to Abort + MoveTo away from a threat.
//
// Menu:  Strategos > Probe Withdraw
// Batch: -executeMethod Strategos.Editor.WithdrawProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Reactions;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class WithdrawProbe
    {
        [MenuItem("Strategos/Probe Withdraw")]
        public static void Run()
        {
            var log = new StringBuilder();
            bool ok = PullsAwayFromThreat(log);
            Debug.Log(log.ToString());
            Debug.Log(ok ? "[WithdrawProbe] PROBE PASSED" : "[WithdrawProbe] PROBE FAILED");
        }

        private static bool PullsAwayFromThreat(StringBuilder log)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var sim = new Simulation(scenario, scenario.GenerateMap(), UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.AddExecutor(new DelayExecutor());

            UnitInstance unit = null, hostile = null;
            for (int i = 0; i < sim.Units.Count; i++)
            {
                var u = sim.Units[i];
                if (u.Side.Value == 1) unit ??= u;
                else hostile ??= u;
            }
            if (unit == null || hostile == null)
            {
                log.AppendLine("  withdraw: FAILED, need friendly and hostile");
                return false;
            }

            unit.Cell = new Vector2(80f, 80f);
            hostile.Cell = new Vector2(100f, 80f);
            float startDist = Vector2.Distance(unit.Cell, hostile.Cell);

            sim.Issue(Command.Withdraw(ActorId.ForSide(unit.Side), unit.Id, hostile.Id));
            // Withdraw delivers next tick and expands; Abort+MoveTo the tick after.
            sim.Step(5 + (int)ReactionController.WithdrawCells);

            float endDist = Vector2.Distance(unit.Cell, hostile.Cell);
            if (endDist <= startDist + 5f)
            {
                log.AppendLine($"  withdraw: FAILED, distance {startDist:0.0} → {endDist:0.0} " +
                               "(expected pullback)");
                return false;
            }

            log.AppendLine($"  withdraw: distance {startDist:0.0} → {endDist:0.0} cells  ok");
            return true;
        }
    }
}
#endif
