// TrajectoryExporter.cs
// #106: build a Trajectory from CommandLog + ReportLog — export, not collection.
//
// Observations use SideObservationEncoder with ReportLog entries filtered by tick — never
// opposing UnitInstance.Cell for contacts (same fog discipline as #101).
//
// Offline path: Replayer.Run onto a fresh sim sharing the map, encoding after each Step.
// Own-unit cells come from the replayed sim (friendly ground truth); hostiles only from reports.

using System.Collections.Generic;
using Newtonsoft.Json;
using Strategos.Actions;
using Strategos.Commands;
using Strategos.Maps;
using Strategos.Observation;
using Strategos.Reports;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Trajectories
{
    public static class TrajectoryExporter
    {
        /// <summary>
        /// Replay <paramref name="recorded"/> onto a fresh sim sharing <paramref name="map"/>,
        /// capturing one step per tick for <paramref name="side"/>.
        /// </summary>
        /// <remarks>
        /// <paramref name="scenario"/> must be a <em>pristine</em> authored scenario (start
        /// cells, strength, etc.) — the same shape <see cref="Replayer.Run"/> requires.
        /// <see cref="UnitHierarchy"/> aliases <c>scenario.Units</c>; passing the live scenario
        /// after a run would start the replay from the ending cells.
        /// </remarks>
        public static Trajectory FromRecorded(
            Simulation recorded,
            Scenario scenario,
            MapData map,
            SideId side,
            string scenarioName,
            UnitCatalogue catalogue = null,
            int maxTicks = -1)
        {
            if (recorded == null) throw new System.ArgumentNullException(nameof(recorded));
            if (scenario == null) throw new System.ArgumentNullException(nameof(scenario));
            if (map == null) throw new System.ArgumentNullException(nameof(map));

            catalogue ??= UnitCatalogue.Default();
            int steps = maxTicks < 0 ? recorded.Tick : System.Math.Min(maxTicks, recorded.Tick);

            var target = new Simulation(scenario, map, catalogue);
            BindBare(target);

            var traj = new Trajectory
            {
                ScenarioName = scenarioName ?? string.Empty,
                Side = side.Value,
                ReportSignature = recorded.ReportLog.Signature(),
            };

            AppendStep(traj, side, target, map, recorded, commandsAtTick: -1);

            Replayer.Run(recorded, target, steps, (live, commandsAtTick) =>
                AppendStep(traj, side, live, map, recorded, commandsAtTick));

            return traj;
        }

        /// <summary>
        /// Encode using only reports with <c>Tick &lt;= tick</c> — belief-only reconstruction oracle.
        /// </summary>
        public static SideObservation EncodeBeliefOnly(
            SideId side,
            int tick,
            MapData map,
            IReadOnlyList<UnitInstance> units,
            IReadOnlyList<SituationReport> allReports,
            Objectives.VictoryEvaluator victory)
        {
            var filtered = new List<SituationReport>();
            if (allReports != null)
            {
                for (int i = 0; i < allReports.Count; i++)
                    if (allReports[i].Tick <= tick) filtered.Add(allReports[i]);
            }

            return SideObservationEncoder.Encode(
                side, tick, map.Width, map.Height, units, filtered, victory);
        }

        public static string ToJson(Trajectory trajectory, bool indented = true) =>
            JsonConvert.SerializeObject(trajectory, indented ? Formatting.Indented : Formatting.None);

        public static Trajectory FromJson(string json) =>
            JsonConvert.DeserializeObject<Trajectory>(json);

        private static void AppendStep(
            Trajectory traj,
            SideId side,
            Simulation live,
            MapData map,
            Simulation recorded,
            int commandsAtTick)
        {
            var obs = EncodeBeliefOnly(
                side, live.Tick, map, live.Units, recorded.ReportLog.Entries, live.Victory);

            var step = new TrajectoryStep
            {
                Tick = live.Tick,
                Observation = (float[])obs.Values.Clone(),
                ReportCountThroughTick = CountReportsThrough(recorded.ReportLog.Entries, live.Tick),
            };

            if (commandsAtTick >= 0)
            {
                var entries = recorded.Log.Entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    var c = entries[i];
                    if (c.Tick != commandsAtTick) continue;
                    var unit = live.UnitOf(c.TargetUnit);
                    if (unit == null || unit.Side != side) continue;
                    if (!SideActionSpace.TryFromCommand(c, unit, live.Victory, out int index))
                        continue;
                    step.Actions.Add(new TrajectoryAction
                    {
                        Unit = c.TargetUnit.Value,
                        Index = index,
                        Code = SideActionSpace.CodeAt(index),
                    });
                }
            }

            traj.Steps.Add(step);
        }

        private static int CountReportsThrough(IReadOnlyList<SituationReport> reports, int tick)
        {
            int n = 0;
            if (reports == null) return 0;
            for (int i = 0; i < reports.Count; i++)
                if (reports[i].Tick <= tick) n++;
            return n;
        }

        private static void BindBare(Simulation sim)
        {
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.AddExecutor(new DefendExecutor());
            sim.AddExecutor(new ScreenExecutor());
            sim.AddExecutor(new GuardExecutor());
            sim.AddExecutor(new CoverExecutor());
            sim.AddExecutor(new DelayExecutor());
        }
    }
}
