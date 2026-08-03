// DelayProbe.cs
// Delay holds until pressed, then converts to Withdraw.
//
// Menu:  Strategos > Probe Delay
// Batch: -executeMethod Strategos.Editor.DelayProbe.Run

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
    public static class DelayProbe
    {
        [MenuItem("Strategos/Probe Delay")]
        public static void Run()
        {
            var log = new StringBuilder();
            bool ok = true;
            ok &= HoldsUntilPressed(log);
            ok &= ConvertsToWithdraw(log);
            Debug.Log(log.ToString());
            Debug.Log(ok ? "[DelayProbe] PROBE PASSED" : "[DelayProbe] PROBE FAILED");
        }

        private static Simulation Fresh(out UnitInstance unit, out UnitInstance hostile)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var sim = new Simulation(scenario, scenario.GenerateMap(), UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.AddExecutor(new DelayExecutor());

            unit = null;
            hostile = null;
            for (int i = 0; i < sim.Units.Count; i++)
            {
                var u = sim.Units[i];
                if (u.Side.Value == 1) unit ??= u;
                else hostile ??= u;
            }
            return sim;
        }

        private static bool HoldsUntilPressed(StringBuilder log)
        {
            var sim = Fresh(out var unit, out _);
            sim.Issue(Command.Delay(ActorId.ForSide(unit.Side), unit.Id));
            sim.Step(120);

            var q = sim.QueueOf(unit.Id);
            if (q == null || q.IsEmpty || q[0].Command.Kind != CommandKind.Delay)
            {
                log.AppendLine("  hold: FAILED, Delay left the queue without pressure");
                return false;
            }

            log.AppendLine("  hold: Delay still running after 120 ticks at full strength  ok");
            return true;
        }

        private static bool ConvertsToWithdraw(StringBuilder log)
        {
            var sim = Fresh(out var unit, out var hostile);
            if (unit == null || hostile == null)
            {
                log.AppendLine("  convert: FAILED, need friendly and hostile");
                return false;
            }

            unit.Cell = new Vector2(80f, 80f);
            hostile.Cell = new Vector2(120f, 80f);
            float startDist = Vector2.Distance(unit.Cell, hostile.Cell);

            sim.Issue(Command.Delay(ActorId.ForSide(unit.Side), unit.Id));
            sim.Step(5);
            unit.Strength = ReactionController.BreakStrengthPercent - 5f;
            sim.Step(5 + (int)ReactionController.WithdrawCells);

            var q = sim.QueueOf(unit.Id);
            bool stillDelaying = q != null && !q.IsEmpty && q[0].Command.Kind == CommandKind.Delay;
            if (stillDelaying)
            {
                log.AppendLine("  convert: FAILED, still Delaying below break threshold");
                return false;
            }

            float endDist = Vector2.Distance(unit.Cell, hostile.Cell);
            if (endDist <= startDist + 2f)
            {
                log.AppendLine($"  convert: FAILED, did not pull back ({startDist:0.0} → {endDist:0.0})");
                return false;
            }

            log.AppendLine($"  convert: Delay → Withdraw, distance {startDist:0.0} → {endDist:0.0}  ok");
            return true;
        }
    }
}
#endif
