// ScreenProbe.cs
// Screening: the order does not end, it does not dig in, and detection reaches further.
//
// Menu:  Strategos > Probe Screen
// Batch: -executeMethod Strategos.Editor.ScreenProbe.Run

#if UNITY_EDITOR
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
    public static class ScreenProbe
    {
        [MenuItem("Strategos/Probe Screen")]
        public static void Run()
        {
            var log = new StringBuilder();
            bool ok = true;

            ok &= OrderDoesNotEnd(log);
            ok &= DoesNotDigIn(log);
            ok &= SeesFurtherThanHalted(log);

            Debug.Log(log.ToString());
            Debug.Log(ok ? "[ScreenProbe] PROBE PASSED" : "[ScreenProbe] PROBE FAILED");
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

            unit = null;
            hostile = null;
            for (int i = 0; i < sim.Units.Count; i++)
            {
                var u = sim.Units[i];
                if (u.Side.Value == 1) { unit ??= u; }
                else { hostile ??= u; }
            }
            return sim;
        }

        private static bool OrderDoesNotEnd(StringBuilder log)
        {
            var sim = Fresh(out var unit, out _);
            sim.Issue(Command.Screen(ActorId.ForSide(unit.Side), unit.Id));
            sim.Step(600);

            var queue = sim.QueueOf(unit.Id);
            if (queue == null || queue.IsEmpty || queue[0].Command.Kind != CommandKind.Screen)
            {
                log.AppendLine("  persistence: FAILED, screen left the queue or changed kind");
                return false;
            }

            if (unit.Posture != Posture.Screening)
            {
                log.AppendLine($"  persistence: FAILED, posture is {unit.Posture}");
                return false;
            }

            log.AppendLine($"  persistence: still screening after 600 ticks  ok");
            return true;
        }

        private static bool DoesNotDigIn(StringBuilder log)
        {
            var sim = Fresh(out var unit, out _);
            sim.Issue(Command.Screen(ActorId.ForSide(unit.Side), unit.Id));
            sim.Step(DefendExecutor.DigInTicks + 30);

            if (unit.Posture == Posture.DugIn)
            {
                log.AppendLine("  trade-off: FAILED, screen dug in — that is Defend's benefit");
                return false;
            }

            float screenFire = EngagementResolver.PostureFactor(Posture.Screening);
            float haltedFire = EngagementResolver.PostureFactor(Posture.Halted);
            float dugFire = EngagementResolver.PostureFactor(Posture.DugIn);

            if (Mathf.Abs(screenFire - haltedFire) > 0.001f)
            {
                log.AppendLine($"  trade-off: FAILED, Screening fire factor {screenFire} " +
                               $"!= Halted {haltedFire}");
                return false;
            }

            log.AppendLine($"  trade-off: Screening fire {screenFire:0.00} (= Halted), " +
                           $"DugIn {dugFire:0.00}  ok");
            return true;
        }

        /// <summary>
        /// Place a hostile just beyond normal detection and inside Screening reach.
        /// </summary>
        private static bool SeesFurtherThanHalted(StringBuilder log)
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
            float screenCells = baseCells * UnitCapabilities.DetectionPostureFactor(Posture.Screening);
            float gap = (baseCells + screenCells) * 0.5f;
            hostile.Cell = unit.Cell + new Vector2(gap, 0f);

            // Tracker over just this pair — the skirmish ORBAT includes recon with a longer
            // arc that would otherwise contact through the gap meant for this observer.
            var pair = new System.Collections.Generic.List<UnitInstance> { unit, hostile };
            var tracker = new ContactTracker(sim.Scenario, pair);
            tracker.Sweep(map, catalogue, tick: 1, _ => { });
            if (tracker.ActiveContacts != 0)
            {
                log.AppendLine($"  detection: FAILED, Halted already saw at {gap:0.0} cells " +
                               $"(base {baseCells:0.0}, contacts {tracker.ActiveContacts})");
                return false;
            }

            sim.Issue(Command.Screen(ActorId.ForSide(unit.Side), unit.Id));
            sim.Step(2);

            if (unit.Posture != Posture.Screening)
            {
                log.AppendLine($"  detection: FAILED, posture {unit.Posture} after Screen");
                return false;
            }

            tracker = new ContactTracker(sim.Scenario, pair);
            tracker.Sweep(map, catalogue, tick: 3, _ => { });
            if (tracker.ActiveContacts == 0)
            {
                log.AppendLine($"  detection: FAILED, Screening did not see at {gap:0.0} cells " +
                               $"(screen reach {screenCells:0.0})");
                return false;
            }

            log.AppendLine($"  detection: Halted misses at {gap:0.0} cells, Screening contacts " +
                           $"{tracker.ActiveContacts} (base {baseCells:0.0} → screen {screenCells:0.0})  ok");
            return true;
        }
    }
}
#endif
