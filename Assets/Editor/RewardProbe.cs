// RewardProbe.cs
// #103: SideReward terminal ±1/0, objective/force shaping, and no contact term.
//
// Menu:  Strategos > Probe Reward
// Batch: -executeMethod Strategos.Editor.RewardProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Reward;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class RewardProbe
    {
        [MenuItem("Strategos/Probe Reward")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckPotentialMath(log);
            bad += CheckObjectiveShaping(log);
            bad += CheckForceShaping(log);
            bad += CheckNoContactApi(log);
            bad += CheckTerminalWinLoss(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[RewardProbe]\n" + log);
            else Debug.LogError("[RewardProbe]\n" + log);
        }

        private static Simulation NewSim()
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var sim = new Simulation(scenario, scenario.GenerateMap(), UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            return sim;
        }

        private static int CheckPotentialMath(StringBuilder log)
        {
            var a = new SideRewardSnapshot(ownedObjectives: 0, forceAdvantage: 0f);
            var b = new SideRewardSnapshot(ownedObjectives: 1, forceAdvantage: 0f);
            float d = SideReward.Potential(b) - SideReward.Potential(a);
            float expect = SideReward.ObjectiveWeight;
            if (Mathf.Abs(d - expect) > 1e-6f)
            {
                log.AppendLine($"  potential: FAILED — ΔΦ={d}, expected {expect}");
                return 1;
            }

            var c = new SideRewardSnapshot(0, 1f);
            float df = SideReward.Potential(c) - SideReward.Potential(a);
            if (Mathf.Abs(df - SideReward.ForceWeight) > 1e-6f)
            {
                log.AppendLine($"  potential: FAILED — force ΔΦ={df}, expected {SideReward.ForceWeight}");
                return 1;
            }

            log.AppendLine($"  potential: OK — w_obj={SideReward.ObjectiveWeight}, " +
                           $"w_force={SideReward.ForceWeight}");
            return 0;
        }

        /// <summary>Taking an objective raises Φ and thus step reward.</summary>
        private static int CheckObjectiveShaping(StringBuilder log)
        {
            var sim = NewSim();
            var blue = new SideId(1);
            var red = new SideId(2);

            // Park red far away wrecked so they cannot contest; put blue on first objective.
            var obj = sim.Victory.Objectives[0];
            for (int i = 0; i < sim.Units.Count; i++)
            {
                var u = sim.Units[i];
                if (u.Side == red) { u.Strength = 0f; u.DestroyedAtTick = 0; continue; }
                u.Cell = obj.Cell;
            }

            var before = SideRewardSnapshot.Capture(blue, sim.Victory, sim.Units);
            // Ownership samples every EvaluationInterval — step enough to take.
            sim.Step(VictoryEvaluatorEvalTicks());
            var after = SideRewardSnapshot.Capture(blue, sim.Victory, sim.Units);

            if (after.OwnedObjectives <= before.OwnedObjectives)
            {
                log.AppendLine($"  objective: FAILED — owned {before.OwnedObjectives}→{after.OwnedObjectives} " +
                               $"(expected an increase; owner[0]={sim.Victory.OwnerOfIndex(0)})");
                return 1;
            }

            float r = SideReward.Step(blue, before, after, sim.Victory, episodeDone: false);
            if (r <= 0f)
            {
                log.AppendLine($"  objective: FAILED — step reward {r} not positive after taking ground");
                return 1;
            }

            log.AppendLine($"  objective: OK — owned {before.OwnedObjectives}→{after.OwnedObjectives}, " +
                           $"r={r:0.####}");
            return 0;
        }

        private static int VictoryEvaluatorEvalTicks() =>
            Strategos.Objectives.VictoryEvaluator.EvaluationInterval + 2;

        /// <summary>Damaging the enemy raises force advantage / reward.</summary>
        private static int CheckForceShaping(StringBuilder log)
        {
            var sim = NewSim();
            var blue = new SideId(1);
            var red = new SideId(2);

            var before = SideRewardSnapshot.Capture(blue, sim.Victory, sim.Units);
            for (int i = 0; i < sim.Units.Count; i++)
                if (sim.Units[i].Side == red) sim.Units[i].Strength = 10f;

            var after = SideRewardSnapshot.Capture(blue, sim.Victory, sim.Units);
            float r = SideReward.Step(blue, before, after, sim.Victory, episodeDone: false);
            if (r <= 0f || after.ForceAdvantage <= before.ForceAdvantage)
            {
                log.AppendLine($"  force: FAILED — adv {before.ForceAdvantage:0.###}→" +
                               $"{after.ForceAdvantage:0.###}, r={r}");
                return 1;
            }

            log.AppendLine($"  force: OK — adv {before.ForceAdvantage:0.###}→" +
                           $"{after.ForceAdvantage:0.###}, r={r:0.####}");
            return 0;
        }

        /// <summary>
        /// Contact is not an input to SideReward — the issue's named failure mode.
        /// Guard: API surface has no report/contact parameter; identical snapshots → r=0.
        /// </summary>
        private static int CheckNoContactApi(StringBuilder log)
        {
            // Reflective guard: Step signature must not take ReportLog / SituationReport.
            var step = typeof(SideReward).GetMethod(nameof(SideReward.Step));
            if (step == null)
            {
                log.AppendLine("  contact: FAILED — Step method missing");
                return 1;
            }
            foreach (var p in step.GetParameters())
            {
                var n = p.ParameterType.FullName ?? "";
                if (n.Contains("Report") || n.Contains("Contact"))
                {
                    log.AppendLine($"  contact: FAILED — Step takes {p.ParameterType.Name} " +
                                   "(contact shaping must not exist)");
                    return 1;
                }
            }

            var snap = new SideRewardSnapshot(1, 0.2f);
            float r = SideReward.Step(new SideId(1), snap, snap, victory: null, episodeDone: false);
            if (r != 0f)
            {
                log.AppendLine($"  contact: FAILED — identical snapshots gave r={r}");
                return 1;
            }

            log.AppendLine("  contact: OK — Step has no report/contact arg; ΔΦ=0 on identical snaps");
            return 0;
        }

        private static int CheckTerminalWinLoss(StringBuilder log)
        {
            var sim = NewSim();
            var blue = new SideId(1);
            var red = new SideId(2);

            // Wipe red below DestroyEnemy threshold and step until decided.
            for (int i = 0; i < sim.Units.Count; i++)
                if (sim.Units[i].Side == red) sim.Units[i].Strength = 0f;

            var prev = SideRewardSnapshot.Capture(blue, sim.Victory, sim.Units);
            float terminal = 0f;
            for (int n = 0; n < 50 && !sim.IsOver; n++)
            {
                sim.Step(Strategos.Objectives.VictoryEvaluator.EvaluationInterval);
                var cur = SideRewardSnapshot.Capture(blue, sim.Victory, sim.Units);
                terminal = SideReward.Step(blue, prev, cur, sim.Victory, sim.IsOver);
                prev = cur;
            }

            if (!sim.IsOver || sim.Victory.Outcome.Winner != blue)
            {
                log.AppendLine($"  terminal: FAILED — not a blue win ({sim.Victory.Outcome})");
                return 1;
            }
            if (terminal < SideReward.TerminalWin - 0.2f)
            {
                // Allow small negative shaping if force already maxed; terminal must dominate.
                log.AppendLine($"  terminal: FAILED — last step r={terminal}, expected near +1");
                return 1;
            }

            // Loss for red on the same outcome.
            float loss = SideReward.Step(red, prev, prev, sim.Victory, episodeDone: true);
            if (Mathf.Abs(loss - SideReward.TerminalLoss) > 1e-5f)
            {
                log.AppendLine($"  terminal: FAILED — red terminal r={loss}, expected {SideReward.TerminalLoss}");
                return 1;
            }

            log.AppendLine($"  terminal: OK — blue win r≈{terminal:0.###}, red loss r={loss}; " +
                           $"{sim.Victory.Outcome}");
            return 0;
        }
    }
}
#endif
