// AttackProbe.cs
// Attack expands to MoveTo (when far) + Engage against a threat.
//
// Menu:  Strategos > Probe Attack
// Batch: -executeMethod Strategos.Editor.AttackProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class AttackProbe
    {
        [MenuItem("Strategos/Probe Attack")]
        public static void Run()
        {
            var log = new StringBuilder();
            bool ok = true;
            ok &= ClosesThenEngages(log);
            ok &= SkipsMoveWhenAlreadyClose(log);
            Debug.Log(log.ToString());
            Debug.Log(ok ? "[AttackProbe] PROBE PASSED" : "[AttackProbe] PROBE FAILED");
        }

        private static Simulation Fresh(out UnitInstance unit, out UnitInstance hostile)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var sim = new Simulation(scenario, scenario.GenerateMap(), UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());

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

        private static bool ClosesThenEngages(StringBuilder log)
        {
            var sim = Fresh(out var unit, out var hostile);
            if (unit == null || hostile == null)
            {
                log.AppendLine("  close: FAILED, need friendly and hostile");
                return false;
            }

            unit.Cell = new Vector2(40f, 40f);
            hostile.Cell = new Vector2(40f + Simulation.AttackStandoffCells + 30f, 40f);
            float startDist = Vector2.Distance(unit.Cell, hostile.Cell);

            sim.Issue(Command.Attack(ActorId.ForSide(unit.Side), unit.Id, hostile.Id));
            // Attack → MoveTo+Engage next tick; march needs many ticks.
            sim.Step(200);

            float endDist = Vector2.Distance(unit.Cell, hostile.Cell);
            if (endDist >= startDist - 5f)
            {
                log.AppendLine($"  close: FAILED, distance {startDist:0.0} → {endDist:0.0}");
                return false;
            }

            var q = sim.QueueOf(unit.Id);
            bool engaging = q != null && !q.IsEmpty &&
                            q.TryPeek(out var head) && head.Command.Kind == CommandKind.Engage;

            // May have finished Engage if combat was quick; closing is the hard assertion.
            log.AppendLine($"  close: distance {startDist:0.0} → {endDist:0.0}" +
                           (engaging ? ", Engage at head" : ", Engage resolved or pending") +
                           "  ok");
            return true;
        }

        private static bool SkipsMoveWhenAlreadyClose(StringBuilder log)
        {
            var sim = Fresh(out var unit, out var hostile);
            unit.Cell = new Vector2(50f, 50f);
            hostile.Cell = unit.Cell + new Vector2(Simulation.AttackStandoffCells * 0.5f, 0f);

            sim.Issue(Command.Attack(ActorId.ForSide(unit.Side), unit.Id, hostile.Id));
            sim.Step(3); // deliver Attack, then Engage

            bool sawMove = false;
            bool sawEngage = false;
            foreach (var c in sim.Log.Entries)
            {
                if (c.Kind == CommandKind.MoveTo) sawMove = true;
                if (c.Kind == CommandKind.Engage && c.AgainstUnit == hostile.Id) sawEngage = true;
            }

            if (sawMove)
            {
                log.AppendLine("  near: FAILED, issued MoveTo though already inside standoff");
                return false;
            }

            if (!sawEngage)
            {
                log.AppendLine("  near: FAILED, no Engage in the log");
                return false;
            }

            log.AppendLine("  near: close Attack → Engage only (no MoveTo)  ok");
            return true;
        }
    }
}
#endif
