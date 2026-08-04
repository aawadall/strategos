// ActionSpaceProbe.cs
// #102: SideActionMask gates match ExpandDrill accept/reject where they should, and
// readiness/busy/ADVANCE where the mask is deliberately stricter or separate.
//
// Menu:  Strategos > Probe Action Space
// Batch: -executeMethod Strategos.Editor.ActionSpaceProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Actions;
using Strategos.Commands;
using Strategos.Doctrine;
using Strategos.Reports;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class ActionSpaceProbe
    {
        [MenuItem("Strategos/Probe Action Space")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckVocabulary(log);
            bad += CheckNoThreatMasksT1Illegal(log);
            bad += CheckUntrainedMasked(log);
            bad += CheckBusyMasksAllIllegal(log);
            bad += CheckAdvanceGate(log);
            bad += CheckBindabilityAgreement(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[ActionSpaceProbe]\n" + log);
            else Debug.LogError("[ActionSpaceProbe]\n" + log);
        }

        // ─── Fixtures ─────────────────────────────────────────────────────────

        private static Simulation NewSim(out Scenario scenario)
        {
            scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var sim = new Simulation(scenario, scenario.GenerateMap(), UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.AddExecutor(new DefendExecutor());
            return sim;
        }

        private static UnitInstance FirstLeaf(Simulation sim, SideId side)
        {
            for (int i = 0; i < sim.Units.Count; i++)
            {
                var u = sim.Units[i];
                if (u.Side == side && !sim.Hierarchy.IsFormation(u.Id))
                    return u;
            }
            return null;
        }

        private static bool[] MaskOf(Simulation sim, UnitInstance unit) =>
            SideActionMask.Encode(
                unit, sim.QueueOf(unit.Id), sim.Units, sim.Scenario,
                sim.Hierarchy, sim.Victory);

        private static int IndexOfCode(string code)
        {
            for (int i = 0; i < SideActionSpace.Count; i++)
                if (SideActionSpace.CodeAt(i) == code) return i;
            return -1;
        }

        private static void ParkFarApart(Simulation sim)
        {
            var blue = new SideId(1);
            var red = new SideId(2);
            for (int i = 0; i < sim.Units.Count; i++)
            {
                var u = sim.Units[i];
                u.Training = 100f;
                if (u.Side == blue)
                {
                    u.Cell = new Vector2(40f, 40f);
                }
                else if (u.Side == red)
                {
                    // ExpandDrill's NearestHostile has no range cap — distance alone does not
                    // remove a bindable threat. Destroy red so Bind sees null.
                    u.Strength = 0f;
                    u.DestroyedAtTick = 0;
                    u.Cell = new Vector2(200f, 200f);
                }
            }
        }

        private static void ParkInContact(Simulation sim)
        {
            var blue = new SideId(1);
            for (int i = 0; i < sim.Units.Count; i++)
            {
                var u = sim.Units[i];
                u.Training = 100f;
                u.Cell = u.Side == blue ? new Vector2(40f, 40f) : new Vector2(48f, 40f);
            }
        }

        private static bool SawOrderFailed(Simulation sim, UnitId unit) 
        {
            var entries = sim.ReportLog.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var r = entries[i];
                if (r.Kind == ReportKind.OrderFailed && r.Subject == unit) return true;
            }
            return false;
        }

        // ─── Checks ───────────────────────────────────────────────────────────

        private static int CheckVocabulary(StringBuilder log)
        {
            SideActionSpace.Reload();
            int count = SideActionSpace.Count;
            string advance = SideActionSpace.CodeAt(SideActionSpace.AdvanceIndex);
            if (count != 13 || advance != SideActionSpace.AdvanceCode)
            {
                log.AppendLine($"  vocabulary: FAILED — Count={count}, ADVANCE='{advance}'");
                return 1;
            }
            if (IndexOfCode("T1") != 0 || IndexOfCode("7") < 0)
            {
                log.AppendLine("  vocabulary: FAILED — expected T1 at 0 and code 7 present");
                return 1;
            }

            log.AppendLine($"  vocabulary: OK — Count={count}, " +
                           $"T1={SideActionSpace.CodeAt(0)}, ADVANCE@{SideActionSpace.AdvanceIndex}");
            return 0;
        }

        /// <summary>
        /// No hostile in range of Bind: T1 (all threat-bound) masked illegal; ExpandDrill
        /// OrderFailed if forced.
        /// </summary>
        private static int CheckNoThreatMasksT1Illegal(StringBuilder log)
        {
            var sim = NewSim(out _);
            ParkFarApart(sim); // red wrecked — no bindable threat
            var unit = FirstLeaf(sim, new SideId(1));
            if (unit == null)
            {
                log.AppendLine("  no-threat: FAILED — no blue leaf");
                return 1;
            }

            if (DrillBindability.NearestHostile(unit, sim.Units, sim.Scenario) != null)
            {
                log.AppendLine("  no-threat: FAILED — fixture still has a NearestHostile");
                return 1;
            }

            int t1 = IndexOfCode("T1");
            var mask = MaskOf(sim, unit);
            if (mask[t1])
            {
                log.AppendLine("  no-threat: FAILED — T1 legal with no bindable threat");
                return 1;
            }

            sim.Issue(Command.Drill(ActorId.ForSide(unit.Side), unit.Id, "T1"));
            sim.Step(5);
            if (!SawOrderFailed(sim, unit.Id))
            {
                log.AppendLine("  no-threat: FAILED — forced T1 did not OrderFailed");
                return 1;
            }

            log.AppendLine($"  no-threat: OK — T1 masked illegal; ExpandDrill OrderFailed; " +
                           $"legal={SideActionMask.LegalCount(mask)}");
            return 0;
        }

        private static int CheckUntrainedMasked(StringBuilder log)
        {
            var sim = NewSim(out _);
            ParkInContact(sim);
            var unit = FirstLeaf(sim, new SideId(1));
            if (unit == null)
            {
                log.AppendLine("  untrained: FAILED — no blue leaf");
                return 1;
            }

            // Floor effectiveness below UntrainedBelow (0.55).
            unit.Strength = 20f;
            unit.Readiness = 100f;
            unit.Suppression = 0f;

            float eff = unit.Effectiveness;
            if (eff >= 0.55f)
            {
                log.AppendLine($"  untrained: FAILED — fixture Effectiveness={eff:0.00} not below 0.55");
                return 1;
            }

            var mask = MaskOf(sim, unit);
            int legalDrills = 0;
            for (int i = 0; i < SideActionSpace.AdvanceIndex; i++)
                if (mask[i]) legalDrills++;

            if (legalDrills != 0)
            {
                log.AppendLine($"  untrained: FAILED — {legalDrills} drills still legal at " +
                               $"Effectiveness={eff:0.00}");
                return 1;
            }

            log.AppendLine($"  untrained: OK — all drills masked at Effectiveness={eff:0.00}; " +
                           $"ADVANCE={mask[SideActionSpace.AdvanceIndex]}");
            return 0;
        }

        private static int CheckBusyMasksAllIllegal(StringBuilder log)
        {
            var sim = NewSim(out _);
            ParkInContact(sim);
            var unit = FirstLeaf(sim, new SideId(1));
            if (unit == null)
            {
                log.AppendLine("  busy: FAILED — no blue leaf");
                return 1;
            }

            sim.Issue(Command.MoveTo(ActorId.ForSide(unit.Side), unit.Id, unit.Cell + Vector2.right * 5f));
            sim.Step(1); // deliver onto queue

            if (sim.QueueOf(unit.Id).IsEmpty)
            {
                log.AppendLine("  busy: FAILED — queue still empty after MoveTo");
                return 1;
            }

            var mask = MaskOf(sim, unit);
            int legal = SideActionMask.LegalCount(mask);
            if (legal != 0)
            {
                log.AppendLine($"  busy: FAILED — {legal} actions legal while queue busy");
                return 1;
            }

            log.AppendLine($"  busy: OK — all {SideActionSpace.Count} actions illegal mid-order");
            return 0;
        }

        private static int CheckAdvanceGate(StringBuilder log)
        {
            // Legal: default skirmish has unheld objectives for blue.
            var simLegal = NewSim(out _);
            ParkFarApart(simLegal);
            var unit = FirstLeaf(simLegal, new SideId(1));
            var maskLegal = MaskOf(simLegal, unit);
            if (!maskLegal[SideActionSpace.AdvanceIndex])
            {
                log.AppendLine("  advance: FAILED — ADVANCE illegal with unheld objectives");
                return 1;
            }
            if (!SideActionSpace.TryToCommand(SideActionSpace.AdvanceIndex,
                    ActorId.ForSide(unit.Side), unit, simLegal.Victory, out var cmd) ||
                cmd.Kind != CommandKind.MoveTo)
            {
                log.AppendLine("  advance: FAILED — TryToCommand did not yield MoveTo");
                return 1;
            }

            // Illegal: every objective already owned by blue at start.
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var blue = new SideId(1);
            for (int i = 0; i < scenario.Objectives.Count; i++)
                scenario.Objectives[i].InitialOwner = blue;
            var simHeld = new Simulation(scenario, scenario.GenerateMap(), UnitCatalogue.Default());
            ParkFarApart(simHeld);
            var unitHeld = FirstLeaf(simHeld, blue);
            var maskHeld = MaskOf(simHeld, unitHeld);
            if (maskHeld[SideActionSpace.AdvanceIndex])
            {
                log.AppendLine("  advance: FAILED — ADVANCE legal when all objectives held");
                return 1;
            }
            if (SideActionSpace.TryToCommand(SideActionSpace.AdvanceIndex,
                    ActorId.ForSide(blue), unitHeld, simHeld.Victory, out _))
            {
                log.AppendLine("  advance: FAILED — TryToCommand succeeded with no unheld objective");
                return 1;
            }

            log.AppendLine($"  advance: OK — legal with unheld (MoveTo cell " +
                           $"{cmd.TargetCell.x:0},{cmd.TargetCell.y:0}); illegal when all held");
            return 0;
        }

        /// <summary>
        /// Mask legal drills ⊆ those ExpandDrill can issue ≥1 step for (T1 needs threat;
        /// code 6 has a Here step so stays legal without threat).
        /// </summary>
        private static int CheckBindabilityAgreement(StringBuilder log)
        {
            int bad = 0;

            // Without threat (red wrecked): T1 illegal; 6 legal (Defend Here).
            {
                var sim = NewSim(out _);
                ParkFarApart(sim);
                var unit = FirstLeaf(sim, new SideId(1));
                var mask = MaskOf(sim, unit);
                int t1 = IndexOfCode("T1");
                int d6 = IndexOfCode("6");

                bool t1Bind = DrillBindability.CanBindAnyMechanisedStep(
                    unit, TtpLibrary.Find("T1"), sim.Units, sim.Scenario);
                bool d6Bind = DrillBindability.CanBindAnyMechanisedStep(
                    unit, TtpLibrary.Find("6"), sim.Units, sim.Scenario);

                if (mask[t1] || t1Bind)
                {
                    log.AppendLine($"  bind-agree: FAILED — T1 without threat mask={mask[t1]} bind={t1Bind}");
                    bad++;
                }
                else if (!mask[d6] || !d6Bind)
                {
                    var r = TtpReadiness.Assess(TtpLibrary.Find("6"), unit);
                    log.AppendLine($"  bind-agree: FAILED — code 6 without threat " +
                                   $"mask={mask[d6]} bind={d6Bind} readiness={r.Code} ({r.Reason})");
                    bad++;
                }
                else
                {
                    log.AppendLine($"  bind-agree: OK (no threat) — T1 illegal, 6 legal " +
                                   $"(readiness {TtpReadiness.Assess(TtpLibrary.Find("6"), unit).Code})");
                }
            }

            // With threat: T1 legal for a trained leaf that can attempt it.
            {
                var sim = NewSim(out _);
                ParkInContact(sim);
                var unit = FirstLeaf(sim, new SideId(1));
                // Prefer a small echelon leaf if the first leaf is a company — T1 is Team task;
                // company is still Trained ("by subordinate element").
                var mask = MaskOf(sim, unit);
                int t1 = IndexOfCode("T1");
                bool t1Bind = DrillBindability.CanBindAnyMechanisedStep(
                    unit, TtpLibrary.Find("T1"), sim.Units, sim.Scenario);
                var ready = TtpReadiness.Assess(TtpLibrary.Find("T1"), unit);

                if (ready.Rating == DrillRating.Untrained)
                {
                    log.AppendLine($"  bind-agree: FAILED — fixture Untrained on T1 ({ready.Reason})");
                    bad++;
                }
                else if (!t1Bind || !mask[t1])
                {
                    log.AppendLine($"  bind-agree: FAILED — T1 with threat mask={mask[t1]} " +
                                   $"bind={t1Bind} ready={ready.Code}");
                    bad++;
                }
                else
                {
                    log.AppendLine($"  bind-agree: OK (in contact) — T1 legal; " +
                                   $"legal={SideActionMask.LegalCount(mask)}");
                }
            }

            return bad > 0 ? 1 : 0;
        }
    }
}
#endif
