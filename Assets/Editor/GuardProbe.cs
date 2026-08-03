// GuardProbe.cs
// Guarding: never ends, digs in like Defend, and sees a little further once prepared.
//
// Menu:  Strategos > Probe Guard
// Batch: -executeMethod Strategos.Editor.GuardProbe.Run

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Combat;
using Strategos.Commands;
using Strategos.Maps;
using Strategos.Reports;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class GuardProbe
    {
        [MenuItem("Strategos/Probe Guard")]
        public static void Run()
        {
            var log = new StringBuilder();
            bool ok = true;

            ok &= OrderDoesNotEnd(log);
            ok &= DigsInAndPays(log);
            ok &= SeesFurtherOncePrepared(log);

            Debug.Log(log.ToString());
            Debug.Log(ok ? "[GuardProbe] PROBE PASSED" : "[GuardProbe] PROBE FAILED");
        }

        private static Simulation Fresh(out UnitInstance unit, out UnitInstance hostile)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var sim = new Simulation(scenario, scenario.GenerateMap(), UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.AddExecutor(new DefendExecutor());
            sim.AddExecutor(new ScreenExecutor());
            sim.AddExecutor(new GuardExecutor());

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

        private static bool OrderDoesNotEnd(StringBuilder log)
        {
            var sim = Fresh(out var unit, out _);
            sim.Issue(Command.Guard(ActorId.ForSide(unit.Side), unit.Id));
            sim.Step(DefendExecutor.DigInTicks + 60);

            var queue = sim.QueueOf(unit.Id);
            if (queue == null || queue.IsEmpty || queue[0].Command.Kind != CommandKind.Guard)
            {
                log.AppendLine("  persistence: FAILED, guard left the queue or changed kind");
                return false;
            }

            if (unit.Posture != Posture.Guarding)
            {
                log.AppendLine($"  persistence: FAILED, posture is {unit.Posture}");
                return false;
            }

            log.AppendLine("  persistence: still guarding after dig-in  ok");
            return true;
        }

        private static bool DigsInAndPays(StringBuilder log)
        {
            var sim = Fresh(out var unit, out _);
            sim.Issue(Command.Guard(ActorId.ForSide(unit.Side), unit.Id));

            sim.Step(2);
            if (unit.Posture != Posture.Halted)
            {
                log.AppendLine($"  dig-in: FAILED, posture {unit.Posture} at t2, expected Halted");
                return false;
            }

            sim.Step(DefendExecutor.DigInTicks + 5);
            if (unit.Posture != Posture.Guarding)
            {
                log.AppendLine($"  dig-in: FAILED, posture {unit.Posture} after dig-in");
                return false;
            }

            float guard = EngagementResolver.PostureFactor(Posture.Guarding);
            float dug = EngagementResolver.PostureFactor(Posture.DugIn);
            float screen = EngagementResolver.PostureFactor(Posture.Screening);

            if (Mathf.Abs(guard - dug) > 0.001f)
            {
                log.AppendLine($"  dig-in: FAILED, Guarding fire {guard} != DugIn {dug}");
                return false;
            }

            if (guard >= screen)
            {
                log.AppendLine("  dig-in: FAILED, Guarding is not harder to hurt than Screening");
                return false;
            }

            log.AppendLine($"  dig-in: Guarding fire {guard:0.00} (= DugIn), Screening {screen:0.00}  ok");
            return true;
        }

        private static bool SeesFurtherOncePrepared(StringBuilder log)
        {
            var sim = Fresh(out var unit, out var hostile);
            if (unit == null || hostile == null)
            {
                log.AppendLine("  detection: FAILED, need one friendly and one hostile");
                return false;
            }

            var catalogue = UnitCatalogue.Default();
            var map = sim.Map;
            float metresPerCell = Mathf.Max(0.0001f, map.Header.MetresPerCell);

            unit.Cell = new Vector2(40f, 40f);
            unit.Posture = Posture.Halted;
            var caps = unit.Capabilities(catalogue);
            float baseCells = caps.DetectionRangeAt(unit.Landcover(map)) / metresPerCell;
            float guardCells = baseCells * UnitCapabilities.DetectionPostureFactor(Posture.Guarding);
            float gap = (baseCells + guardCells) * 0.5f;
            hostile.Cell = unit.Cell + new Vector2(gap, 0f);

            var pair = new List<UnitInstance> { unit, hostile };
            var tracker = new ContactTracker(sim.Scenario, pair);
            tracker.Sweep(map, catalogue, tick: 1, _ => { });
            if (tracker.ActiveContacts != 0)
            {
                log.AppendLine($"  detection: FAILED, Halted already saw at {gap:0.0} cells");
                return false;
            }

            sim.Issue(Command.Guard(ActorId.ForSide(unit.Side), unit.Id));
            sim.Step(DefendExecutor.DigInTicks + 5);

            if (unit.Posture != Posture.Guarding)
            {
                log.AppendLine($"  detection: FAILED, posture {unit.Posture} after dig-in");
                return false;
            }

            tracker = new ContactTracker(sim.Scenario, pair);
            tracker.Sweep(map, catalogue, tick: 3, _ => { });
            if (tracker.ActiveContacts == 0)
            {
                log.AppendLine($"  detection: FAILED, Guarding did not see at {gap:0.0} cells");
                return false;
            }

            float screenFactor = UnitCapabilities.DetectionPostureFactor(Posture.Screening);
            float guardFactor = UnitCapabilities.DetectionPostureFactor(Posture.Guarding);
            if (guardFactor >= screenFactor)
            {
                log.AppendLine("  detection: FAILED, Guard watch must be lighter than Screen");
                return false;
            }

            log.AppendLine($"  detection: Halted misses at {gap:0.0}, Guarding contacts " +
                           $"(×{guardFactor:0.00}, Screen ×{screenFactor:0.00})  ok");
            return true;
        }
    }
}
#endif
