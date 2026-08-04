// SideEnv.cs
// #104: Reset → Step(actions) → (observation, reward, done) for one learning side.
//
// Lives under Strategos.SimEnv (not "Environment") so it does not shadow System.Environment.
//
// Wires #101 observation, #102 actions, and #103 reward around an existing Simulation.
// Episode Reset restores a start-of-episode SimulationSnapshot with a *cached* MapData so
// GenerateMap is not paid every episode (#74 restore path, #96 cost warning).
//
// STATE CHANGES ONLY THROUGH Issue / Simulation.Step. No queue or Cell short-circuits —
// that is the #94 failure mode on a new entry point. Illegal masked actions are skipped
// (not forced), so a policy that ignores the mask wastes a tick rather than corrupting state.

using System;
using System.Collections.Generic;
using Strategos.Actions;
using Strategos.Commands;
using Strategos.Maps;
using Strategos.Observation;
using Strategos.Persistence;
using Strategos.Reward;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.SimEnv
{
    public sealed class SideEnv
    {
        private readonly MapData _map;
        private readonly UnitCatalogue _catalogue;
        private readonly SideId _side;
        private readonly SimulationSnapshot _start;
        private readonly List<SideId> _directorSides = new();
        private readonly bool _enableReactions;

        private Simulation _sim;
        private SideRewardSnapshot _prevReward;

        /// <summary>The live simulation. Never null after construction / Reset.</summary>
        public Simulation Simulation => _sim;

        public SideId Side => _side;
        public MapData Map => _map;

        private SideEnv(
            MapData map,
            UnitCatalogue catalogue,
            SideId side,
            SimulationSnapshot start,
            IEnumerable<SideId> directorSides,
            bool enableReactions)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _catalogue = catalogue ?? UnitCatalogue.Default();
            _side = side;
            _start = start ?? throw new ArgumentNullException(nameof(start));
            _enableReactions = enableReactions;
            if (directorSides != null)
                foreach (var s in directorSides) _directorSides.Add(s);
        }

        /// <summary>
        /// Build an env from a fresh scenario: generate the map once, snapshot tick 0, Reset.
        /// </summary>
        public static SideEnv Create(
            Scenario scenario,
            SideId agentSide,
            UnitCatalogue catalogue = null,
            IEnumerable<SideId> opposingDirectorSides = null,
            bool enableReactions = true,
            bool enableErosion = false)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            scenario.Map.EnableErosion = enableErosion;
            catalogue ??= UnitCatalogue.Default();
            var map = scenario.GenerateMap();

            var boot = new Simulation(scenario, map, catalogue);
            BindBehaviour(boot, opposingDirectorSides, enableReactions, restoreFrom: null);
            var start = boot.Snapshot();

            var env = new SideEnv(map, catalogue, agentSide, start,
                opposingDirectorSides, enableReactions);
            env.Reset();
            return env;
        }

        /// <summary>Restore the start snapshot (cached map) and return the opening observation.</summary>
        public SideObservation Reset()
        {
            _sim = Simulation.Restore(_start, _catalogue, _map);
            BindBehaviour(_sim, _directorSides, _enableReactions, _start);
            _prevReward = SideRewardSnapshot.Capture(_side, _sim.Victory, _sim.Units);
            return Encode();
        }

        /// <summary>
        /// Issue zero or more masked actions for the agent side, advance one simulation tick,
        /// return observation / reward / done.
        /// </summary>
        public EnvStepResult Step(IReadOnlyList<SideActionChoice> actions)
        {
            if (_sim == null) throw new InvalidOperationException("SideEnv.Reset before Step");
            if (_sim.IsOver)
                return new EnvStepResult(Encode(), 0f, true);

            if (actions != null)
            {
                for (int i = 0; i < actions.Count; i++)
                    TryIssue(actions[i]);
            }

            _sim.Step();

            var current = SideRewardSnapshot.Capture(_side, _sim.Victory, _sim.Units);
            float reward = SideReward.Step(_side, _prevReward, current, _sim.Victory, _sim.IsOver);
            _prevReward = current;

            return new EnvStepResult(Encode(), reward, _sim.IsOver);
        }

        /// <summary>Convenience: one action this tick.</summary>
        public EnvStepResult Step(SideActionChoice action) =>
            Step(new[] { action });

        /// <summary>Convenience: no agent action — time and opposing policy still advance.</summary>
        public EnvStepResult Step() => Step(Array.Empty<SideActionChoice>());

        public SideObservation Observation() => Encode();

        public bool[] MaskFor(UnitId unit)
        {
            var u = _sim.UnitOf(unit);
            return SideActionMask.Encode(
                u, _sim.QueueOf(unit), _sim.Units, _sim.Scenario, _sim.Hierarchy, _sim.Victory);
        }

        private void TryIssue(in SideActionChoice choice)
        {
            var unit = _sim.UnitOf(choice.Unit);
            if (unit == null || unit.Side != _side) return;

            var mask = SideActionMask.Encode(
                unit, _sim.QueueOf(unit.Id), _sim.Units, _sim.Scenario,
                _sim.Hierarchy, _sim.Victory);
            if (choice.ActionIndex < 0 || choice.ActionIndex >= mask.Length) return;
            if (!mask[choice.ActionIndex]) return;

            if (!SideActionSpace.TryToCommand(choice.ActionIndex, ActorId.ForSide(_side),
                    unit, _sim.Victory, out var command))
                return;

            _sim.Issue(command);
        }

        private SideObservation Encode() =>
            SideObservationEncoder.Encode(
                _side, _sim.Tick, _map.Width, _map.Height,
                _sim.Units, _sim.ReportLog.Entries, _sim.Victory);

        private static void BindBehaviour(
            Simulation sim,
            IEnumerable<SideId> directorSides,
            bool enableReactions,
            SimulationSnapshot restoreFrom)
        {
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.AddExecutor(new DefendExecutor());
            sim.AddExecutor(new ScreenExecutor());
            sim.AddExecutor(new GuardExecutor());
            sim.AddExecutor(new CoverExecutor());
            sim.AddExecutor(new DelayExecutor());

            if (enableReactions)
            {
                sim.EnableReactions();
                if (restoreFrom != null) sim.RestoreReactionPicture(restoreFrom);
            }

            if (directorSides != null)
            {
                var list = new List<SideId>();
                foreach (var s in directorSides) list.Add(s);
                if (list.Count > 0)
                {
                    sim.EnableDirector(list);
                    if (restoreFrom != null) sim.RestoreDirectorMemory(restoreFrom);
                }
            }
        }
    }
}
