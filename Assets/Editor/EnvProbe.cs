// EnvProbe.cs
// #104: SideEnv Reset/Step goes only through Issue, and Reset is signature-stable.
//
// Menu:  Strategos > Probe Environment
// Batch: -executeMethod Strategos.Editor.EnvProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Actions;
using Strategos.Commands;
using Strategos.SimEnv;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class EnvProbe
    {
        [MenuItem("Strategos/Probe Environment")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckResetSignatureStable(log);
            bad += CheckStepMatchesManualIssue(log);
            bad += CheckIllegalActionSkipped(log);
            bad += CheckDoneOnVictory(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[EnvProbe]\n" + log);
            else Debug.LogError("[EnvProbe]\n" + log);
        }

        private static SideEnv NewEnv(out SideId blue, out SideId red)
        {
            blue = new SideId(1);
            red = new SideId(2);
            var scenario = ScenarioSamples.Skirmish();
            // No opposing director in the fixture: director Issues would race the Issue-path
            // and mask-skip assertions. Opposing policy is optional on SideEnv.Create.
            return SideEnv.Create(scenario, blue, UnitCatalogue.Default(),
                opposingDirectorSides: null,
                enableReactions: true,
                enableErosion: false);
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

        private static int CheckResetSignatureStable(StringBuilder log)
        {
            var env = NewEnv(out _, out _);
            string a = env.Simulation.Signature();
            var obsA = env.Observation();
            env.Reset();
            string b = env.Simulation.Signature();
            var obsB = env.Observation();

            if (a != b)
            {
                log.AppendLine("  reset: FAILED — Signature diverged across Reset");
                return 1;
            }
            if (!obsA.EqualsExact(obsB))
            {
                log.AppendLine($"  reset: FAILED — observation differed by {obsA.DifferCount(obsB)}");
                return 1;
            }

            // Step both a fresh env and a reset env the same empty way — still match.
            env.Step();
            string after = env.Simulation.Signature();
            var env2 = NewEnv(out _, out _);
            env2.Step();
            if (after != env2.Simulation.Signature())
            {
                log.AppendLine("  reset: FAILED — post-Reset Step diverged from fresh env Step");
                return 1;
            }

            log.AppendLine($"  reset: OK — Signature stable; obs Length={SideObservationLen()}; " +
                           $"tick={env.Simulation.Tick}");
            return 0;
        }

        private static int SideObservationLen() =>
            Strategos.Observation.SideObservation.Length;

        /// <summary>
        /// Env Step(ADVANCE) must match manual Issue(MoveTo)+Step — same Command path (#94).
        /// </summary>
        private static int CheckStepMatchesManualIssue(StringBuilder log)
        {
            var env = NewEnv(out var blue, out _);
            var unit = FirstLeaf(env.Simulation, blue);
            if (unit == null)
            {
                log.AppendLine("  issue-path: FAILED — no blue leaf");
                return 1;
            }

            var mask = env.MaskFor(unit.Id);
            if (!mask[SideActionSpace.AdvanceIndex])
            {
                log.AppendLine("  issue-path: FAILED — ADVANCE illegal at start");
                return 1;
            }

            env.Step(new SideActionChoice(unit.Id, SideActionSpace.AdvanceIndex));
            string viaEnv = env.Simulation.Signature();

            // Manual twin.
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var map = scenario.GenerateMap();
            var manual = new Simulation(scenario, map, UnitCatalogue.Default());
            manual.AddExecutor(new MoveToExecutor());
            manual.AddExecutor(new EngageExecutor());
            manual.AddExecutor(new DefendExecutor());
            manual.AddExecutor(new ScreenExecutor());
            manual.AddExecutor(new GuardExecutor());
            manual.AddExecutor(new CoverExecutor());
            manual.AddExecutor(new DelayExecutor());
            manual.EnableReactions();
            // Match NewEnv: no director.

            var mUnit = FirstLeaf(manual, blue);
            if (!SideActionSpace.TryToCommand(SideActionSpace.AdvanceIndex,
                    ActorId.ForSide(blue), mUnit, manual.Victory, out var cmd))
            {
                log.AppendLine("  issue-path: FAILED — TryToCommand on manual twin");
                return 1;
            }
            manual.Issue(cmd);
            manual.Step();

            if (viaEnv != manual.Signature())
            {
                log.AppendLine("  issue-path: FAILED — env Step Signature ≠ manual Issue+Step");
                return 1;
            }

            log.AppendLine($"  issue-path: OK — ADVANCE via env matches Issue; tick={env.Simulation.Tick}");
            return 0;
        }

        private static int CheckIllegalActionSkipped(StringBuilder log)
        {
            var env = NewEnv(out var blue, out _);
            var unit = FirstLeaf(env.Simulation, blue);
            // Force busy then try ADVANCE — must not add a second command if mask clears.
            env.Step(new SideActionChoice(unit.Id, SideActionSpace.AdvanceIndex));
            int logCount = env.Simulation.Log.Count;
            // Mid-order: mask all false; Step with ADVANCE again should Issue nothing.
            env.Step(new SideActionChoice(unit.Id, SideActionSpace.AdvanceIndex));
            if (env.Simulation.Log.Count != logCount)
            {
                log.AppendLine($"  mask-skip: FAILED — log grew {logCount}→{env.Simulation.Log.Count} " +
                               "while unit should be busy");
                return 1;
            }

            log.AppendLine($"  mask-skip: OK — busy ADVANCE skipped; Log.Count={logCount}");
            return 0;
        }

        private static int CheckDoneOnVictory(StringBuilder log)
        {
            var env = NewEnv(out var blue, out var red);
            for (int i = 0; i < env.Simulation.Units.Count; i++)
                if (env.Simulation.Units[i].Side == red)
                    env.Simulation.Units[i].Strength = 0f;

            bool done = false;
            float lastR = 0f;
            for (int n = 0; n < 30 && !done; n++)
            {
                var result = env.Step();
                lastR = result.Reward;
                done = result.Done;
            }

            if (!done || env.Simulation.Victory.Outcome.Winner != blue)
            {
                log.AppendLine($"  done: FAILED — {env.Simulation.Victory.Outcome}");
                return 1;
            }
            if (lastR < 0.5f)
            {
                log.AppendLine($"  done: FAILED — terminal reward {lastR} too small for win");
                return 1;
            }

            log.AppendLine($"  done: OK — Done with r={lastR:0.###}; {env.Simulation.Victory.Outcome}");
            return 0;
        }
    }
}
#endif
