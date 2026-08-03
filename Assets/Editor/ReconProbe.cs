// ReconProbe.cs
// Recon expands to MoveTo (standoff) + Screen; ends Screening with detection stretch.
//
// Menu:  Strategos > Probe Recon
// Batch: -executeMethod Strategos.Editor.ReconProbe.Run

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Maps;
using Strategos.Reports;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class ReconProbe
    {
        [MenuItem("Strategos/Probe Recon")]
        public static void Run()
        {
            var log = new StringBuilder();
            bool ok = MovesThenScreens(log);
            Debug.Log(log.ToString());
            Debug.Log(ok ? "[ReconProbe] PROBE PASSED" : "[ReconProbe] PROBE FAILED");
        }

        private static bool MovesThenScreens(StringBuilder log)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var sim = new Simulation(scenario, scenario.GenerateMap(), UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.AddExecutor(new ScreenExecutor());

            UnitInstance unit = null, hostile = null;
            for (int i = 0; i < sim.Units.Count; i++)
            {
                var u = sim.Units[i];
                if (u.Side.Value == 1) unit ??= u;
                else hostile ??= u;
            }
            if (unit == null || hostile == null)
            {
                log.AppendLine("  recon: FAILED, need friendly and hostile");
                return false;
            }

            unit.Cell = new Vector2(40f, 40f);
            hostile.Cell = new Vector2(40f + Simulation.ReconStandoffCells + 40f, 40f);
            float startDist = Vector2.Distance(unit.Cell, hostile.Cell);

            sim.Issue(Command.Recon(ActorId.ForSide(unit.Side), unit.Id, hostile.Id));
            sim.Step(400);

            float endDist = Vector2.Distance(unit.Cell, hostile.Cell);
            if (endDist >= startDist - 5f)
            {
                log.AppendLine($"  recon: FAILED, did not close ({startDist:0.0} → {endDist:0.0})");
                return false;
            }

            if (unit.Posture != Posture.Screening)
            {
                log.AppendLine($"  recon: FAILED, posture {unit.Posture}, expected Screening");
                return false;
            }

            // Detection stretch vs Halted at the Screening range edge.
            var catalogue = UnitCatalogue.Default();
            var map = sim.Map;
            float metresPerCell = Mathf.Max(0.0001f, map.Header.MetresPerCell);
            var caps = unit.Capabilities(catalogue);
            float baseCells = caps.DetectionRangeAt(unit.Landcover(map)) / metresPerCell;
            float screenCells = baseCells * UnitCapabilities.DetectionPostureFactor(Posture.Screening);
            float gap = (baseCells + screenCells) * 0.5f;

            var observer = unit;
            observer.Posture = Posture.Halted;
            var subject = hostile;
            subject.Cell = observer.Cell + new Vector2(gap, 0f);
            var pair = new List<UnitInstance> { observer, subject };
            var tracker = new ContactTracker(sim.Scenario, pair);
            tracker.Sweep(map, catalogue, 1, _ => { });
            if (tracker.ActiveContacts != 0)
            {
                log.AppendLine("  recon: FAILED, Halted already saw at mid gap");
                return false;
            }

            observer.Posture = Posture.Screening;
            tracker = new ContactTracker(sim.Scenario, pair);
            tracker.Sweep(map, catalogue, 2, _ => { });
            if (tracker.ActiveContacts == 0)
            {
                log.AppendLine("  recon: FAILED, Screening did not extend detection");
                return false;
            }

            log.AppendLine($"  recon: closed {startDist:0.0}→{endDist:0.0}, Screening, " +
                           $"detection stretch at {gap:0.0} cells  ok");
            return true;
        }
    }
}
#endif
