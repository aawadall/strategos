// TrajectoryProbe.cs
// #106: exported obs is belief-only (ReportLog), and fog-leak twin stays identical.
//
// Menu:  Strategos > Probe Trajectory
// Batch: -executeMethod Strategos.Editor.TrajectoryProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Actions;
using Strategos.Commands;
using Strategos.Maps;
using Strategos.Observation;
using Strategos.Scenarios;
using Strategos.Trajectories;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class TrajectoryProbe
    {
        [MenuItem("Strategos/Probe Trajectory")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckExportRoundTrip(log);
            bad += CheckBeliefOnlyMatchesExport(log);
            bad += CheckFogLeakExport(log);
            bad += CheckTryFromCommand(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[TrajectoryProbe]\n" + log);
            else Debug.LogError("[TrajectoryProbe]\n" + log);
        }

        private static Simulation NewSim(out Scenario scenario, out MapData map)
        {
            scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            map = scenario.GenerateMap();
            var sim = new Simulation(scenario, map, UnitCatalogue.Default());
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
                if (u.Side == side && !sim.Hierarchy.IsFormation(u.Id)) return u;
            }
            return null;
        }

        private static int CheckExportRoundTrip(StringBuilder log)
        {
            var sim = NewSim(out var scenario, out var map);
            var blue = new SideId(1);
            var unit = FirstLeaf(sim, blue);
            if (SideActionSpace.TryToCommand(SideActionSpace.AdvanceIndex,
                    ActorId.ForSide(blue), unit, sim.Victory, out var cmd))
                sim.Issue(cmd);
            sim.Step(30);

            // Pristine scenario for replay — live scenario.Units are already at end cells.
            var start = ScenarioSamples.Skirmish();
            start.Map.EnableErosion = false;

            var traj = TrajectoryExporter.FromRecorded(sim, start, map, blue,
                ScenarioSamples.SkirmishName, maxTicks: 30);
            string json = TrajectoryExporter.ToJson(traj);
            var back = TrajectoryExporter.FromJson(json);

            if (back == null || back.Steps.Count != traj.Steps.Count)
            {
                log.AppendLine($"  json: FAILED — steps {traj.Steps.Count}→{back?.Steps.Count}");
                return 1;
            }
            if (back.ReportSignature != traj.ReportSignature)
            {
                log.AppendLine("  json: FAILED — ReportSignature lost");
                return 1;
            }

            log.AppendLine($"  json: OK — {traj.Steps.Count} steps, json={json.Length} chars, " +
                           $"side={traj.Side}");
            return 0;
        }

        private static int CheckBeliefOnlyMatchesExport(StringBuilder log)
        {
            var sim = NewSim(out _, out var map);
            var blue = new SideId(1);
            var unit = FirstLeaf(sim, blue);
            if (SideActionSpace.TryToCommand(SideActionSpace.AdvanceIndex,
                    ActorId.ForSide(blue), unit, sim.Victory, out var cmd))
                sim.Issue(cmd);
            sim.Step(20);

            var start = ScenarioSamples.Skirmish();
            start.Map.EnableErosion = false;

            var traj = TrajectoryExporter.FromRecorded(sim, start, map, blue,
                ScenarioSamples.SkirmishName, maxTicks: 20);

            // Twin replay on another pristine scenario — must match the exported buffers.
            var twinStart = ScenarioSamples.Skirmish();
            twinStart.Map.EnableErosion = false;
            var twin = new Simulation(twinStart, map, UnitCatalogue.Default());
            twin.AddExecutor(new MoveToExecutor());
            twin.AddExecutor(new EngageExecutor());
            twin.AddExecutor(new DefendExecutor());
            twin.AddExecutor(new ScreenExecutor());
            twin.AddExecutor(new GuardExecutor());
            twin.AddExecutor(new CoverExecutor());
            twin.AddExecutor(new DelayExecutor());

            int stepIdx = 0;
            int mismatches = 0;
            var at0 = TrajectoryExporter.EncodeBeliefOnly(
                blue, twin.Tick, map, twin.Units, sim.ReportLog.Entries, twin.Victory);
            if (!new SideObservation(traj.Steps[stepIdx].Observation).EqualsExact(at0))
            {
                log.AppendLine($"  belief: FAILED — tick 0 differ by " +
                               $"{new SideObservation(traj.Steps[0].Observation).DifferCount(at0)}");
                return 1;
            }
            stepIdx++;

            Replayer.Run(sim, twin, 20, (live, _) =>
            {
                if (stepIdx >= traj.Steps.Count) { mismatches++; return; }
                var oracle = TrajectoryExporter.EncodeBeliefOnly(
                    blue, live.Tick, map, live.Units, sim.ReportLog.Entries, live.Victory);
                var exported = new SideObservation(traj.Steps[stepIdx].Observation);
                if (!exported.EqualsExact(oracle))
                {
                    mismatches++;
                    log.AppendLine($"  belief: MISMATCH tick={live.Tick} differ={exported.DifferCount(oracle)}");
                }
                stepIdx++;
            });

            if (mismatches != 0 || stepIdx != traj.Steps.Count)
            {
                log.AppendLine($"  belief: FAILED — mismatches={mismatches}, stepped={stepIdx}, " +
                               $"exported={traj.Steps.Count}");
                return 1;
            }

            log.AppendLine($"  belief: OK — {traj.Steps.Count} steps match EncodeBeliefOnly via Replayer; " +
                           $"last reportsThrough={traj.Steps[traj.Steps.Count - 1].ReportCountThroughTick}");
            return 0;
        }

        private static int CheckFogLeakExport(StringBuilder log)
        {
            var blue = new SideId(1);

            Simulation Make(Vector2 redCell)
            {
                var sim = NewSim(out _, out _);
                for (int i = 0; i < sim.Units.Count; i++)
                {
                    var u = sim.Units[i];
                    u.Training = 100f;
                    if (u.Side == blue) u.Cell = new Vector2(40f, 40f);
                    else { u.Strength = 0f; u.DestroyedAtTick = 0; u.Cell = redCell; }
                }
                sim.Step(15);
                return sim;
            }

            // Wrecked red — no contacts; different wreck cells must not change belief obs.
            var a = Make(new Vector2(200f, 200f));
            var b = Make(new Vector2(220f, 180f));

            var oa = TrajectoryExporter.EncodeBeliefOnly(
                blue, a.Tick, a.Map, a.Units, a.ReportLog.Entries, a.Victory);
            var ob = TrajectoryExporter.EncodeBeliefOnly(
                blue, b.Tick, b.Map, b.Units, b.ReportLog.Entries, b.Victory);

            if (!oa.EqualsExact(ob))
            {
                log.AppendLine($"  fog-leak: FAILED — belief obs differ by {oa.DifferCount(ob)} " +
                               $"with ActiveContacts A={a.ActiveContacts} B={b.ActiveContacts}");
                return 1;
            }

            log.AppendLine($"  fog-leak: OK — identical belief obs; ActiveContacts=0; " +
                           $"reports A={a.ReportLog.Count} B={b.ReportLog.Count}");
            return 0;
        }

        private static int CheckTryFromCommand(StringBuilder log)
        {
            var sim = NewSim(out _, out _);
            var blue = new SideId(1);
            var unit = FirstLeaf(sim, blue);
            if (!SideActionSpace.TryToCommand(SideActionSpace.AdvanceIndex,
                    ActorId.ForSide(blue), unit, sim.Victory, out var cmd))
            {
                log.AppendLine("  from-cmd: FAILED — TryToCommand ADVANCE");
                return 1;
            }
            if (!SideActionSpace.TryFromCommand(cmd, unit, sim.Victory, out int idx) ||
                idx != SideActionSpace.AdvanceIndex)
            {
                log.AppendLine($"  from-cmd: FAILED — reverse ADVANCE → {idx}");
                return 1;
            }

            var drill = Command.Drill(ActorId.ForSide(blue), unit.Id, "T1");
            if (!SideActionSpace.TryFromCommand(drill, unit, sim.Victory, out int t1) || t1 != 0)
            {
                log.AppendLine($"  from-cmd: FAILED — T1 → {t1}");
                return 1;
            }

            log.AppendLine("  from-cmd: OK — ADVANCE and T1 reverse-map");
            return 0;
        }
    }
}
#endif
