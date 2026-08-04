// AiDifficultyProbe.cs
// #291 / #322: Easy is slower and less aggressive than Hard; personality packs shift the
// knobs; a fixed-tick skirmish under Hard issues more director orders than Easy.
//
// Menu:  Strategos > Probe AI Difficulty
// Batch: -executeMethod Strategos.Editor.AiDifficultyProbe.Run

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Direction;
using Strategos.Scenarios;
using Strategos.UI;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class AiDifficultyProbe
    {
        [MenuItem("Strategos/Probe AI Difficulty")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckLadder(log);
            bad += CheckPersonality(log);
            bad += CheckSessionResolve(log);
            bad += CheckOrderCadence(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[AiDifficultyProbe]\n" + log);
            else Debug.LogError("[AiDifficultyProbe]\n" + log);
        }

        private static int CheckLadder(StringBuilder log)
        {
            var easy = AiPresets.ForDifficulty(AiDifficultyLevel.Easy);
            var normal = AiPresets.ForDifficulty(AiDifficultyLevel.Normal);
            var hard = AiPresets.ForDifficulty(AiDifficultyLevel.Hard);

            int bad = 0;
            if (!(easy.EvaluationInterval > normal.EvaluationInterval &&
                  normal.EvaluationInterval > hard.EvaluationInterval))
            {
                log.AppendLine(
                    $"  ladder eval: FAILED — Easy {easy.EvaluationInterval}, " +
                    $"Normal {normal.EvaluationInterval}, Hard {hard.EvaluationInterval}");
                bad++;
            }

            if (!(easy.RetryInterval > normal.RetryInterval &&
                  normal.RetryInterval > hard.RetryInterval))
            {
                log.AppendLine(
                    $"  ladder retry: FAILED — Easy {easy.RetryInterval}, " +
                    $"Normal {normal.RetryInterval}, Hard {hard.RetryInterval}");
                bad++;
            }

            if (!(easy.MinStrengthPercent > normal.MinStrengthPercent &&
                  normal.MinStrengthPercent > hard.MinStrengthPercent))
            {
                log.AppendLine(
                    $"  ladder strength: FAILED — Easy {easy.MinStrengthPercent}, " +
                    $"Normal {normal.MinStrengthPercent}, Hard {hard.MinStrengthPercent}");
                bad++;
            }

            if (normal.EvaluationInterval != SideDirector.EvaluationInterval ||
                normal.RetryInterval != SideDirector.RetryInterval)
            {
                log.AppendLine("  ladder normal: FAILED — Normal must match SideDirector consts");
                bad++;
            }

            if (bad == 0)
            {
                log.AppendLine(
                    $"  ladder: OK — Easy {easy.EvaluationInterval}/{easy.RetryInterval}/" +
                    $"{easy.MinStrengthPercent:0} → Hard {hard.EvaluationInterval}/" +
                    $"{hard.RetryInterval}/{hard.MinStrengthPercent:0}");
            }

            return bad;
        }

        private static int CheckPersonality(StringBuilder log)
        {
            var baseParams = DifficultyParams.Normal();
            var aggressive = AiPresets.ApplyPersonality(baseParams, AiPersonality.Aggressive);
            var defensive = AiPresets.ApplyPersonality(baseParams, AiPersonality.Defensive);
            var balanced = AiPresets.ApplyPersonality(baseParams, AiPersonality.Balanced);

            if (balanced.EvaluationInterval != baseParams.EvaluationInterval ||
                balanced.RetryInterval != baseParams.RetryInterval ||
                !Mathf.Approximately(balanced.MinStrengthPercent, baseParams.MinStrengthPercent))
            {
                log.AppendLine("  personality: FAILED — Balanced must be identity");
                return 1;
            }

            if (!(aggressive.EvaluationInterval < baseParams.EvaluationInterval &&
                  aggressive.RetryInterval < baseParams.RetryInterval &&
                  aggressive.MinStrengthPercent < baseParams.MinStrengthPercent))
            {
                log.AppendLine("  personality: FAILED — Aggressive should tighten intervals");
                return 1;
            }

            if (!(defensive.EvaluationInterval > baseParams.EvaluationInterval &&
                  defensive.RetryInterval > baseParams.RetryInterval &&
                  defensive.MinStrengthPercent > baseParams.MinStrengthPercent))
            {
                log.AppendLine("  personality: FAILED — Defensive should loosen intervals");
                return 1;
            }

            log.AppendLine(
                $"  personality: OK — Aggressive eval {aggressive.EvaluationInterval}, " +
                $"Defensive {defensive.EvaluationInterval}");
            return 0;
        }

        private static int CheckSessionResolve(StringBuilder log)
        {
            var session = new AppSession
            {
                AiDifficulty = AiDifficultyLevel.Hard,
                AiPersonality = AiPersonality.Aggressive,
            };
            var resolved = session.ResolvedDirectorParams();
            var expected = AiPresets.Resolve(AiDifficultyLevel.Hard, AiPersonality.Aggressive);
            if (resolved.EvaluationInterval != expected.EvaluationInterval ||
                resolved.RetryInterval != expected.RetryInterval ||
                !Mathf.Approximately(resolved.MinStrengthPercent, expected.MinStrengthPercent))
            {
                log.AppendLine("  session: FAILED — ResolvedDirectorParams mismatch");
                return 1;
            }

            log.AppendLine(
                $"  session: OK — Hard/Aggressive → eval {resolved.EvaluationInterval}, " +
                $"retry {resolved.RetryInterval}, min {resolved.MinStrengthPercent:0}");
            return 0;
        }

        private static int CheckOrderCadence(StringBuilder log)
        {
            int easyOrders = RunDirectorBudget(AiPresets.ForDifficulty(AiDifficultyLevel.Easy));
            int hardOrders = RunDirectorBudget(AiPresets.ForDifficulty(AiDifficultyLevel.Hard));

            if (hardOrders <= easyOrders)
            {
                log.AppendLine(
                    $"  cadence: FAILED — Hard issued {hardOrders}, Easy issued {easyOrders} " +
                    "(Hard must issue more in a fixed tick budget)");
                return 1;
            }

            log.AppendLine($"  cadence: OK — Easy {easyOrders} orders, Hard {hardOrders} orders");
            return 0;
        }

        /// <summary>
        /// Fixed tick budget, no combat reactions, both sides directed — isolates cadence.
        /// </summary>
        private static int RunDirectorBudget(DifficultyParams parms)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            // No timeout / no hold victory — keep Decide running for the full budget.
            scenario.TimeLimitTicks = 0;
            scenario.Victory.Clear();

            var map = scenario.GenerateMap();
            var sim = new Simulation(scenario, map, UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());

            var sides = new List<SideId>();
            foreach (var s in scenario.Sides) sides.Add(s.Id);
            sim.EnableDirector(sides, parms);

            const int Cap = 1200;
            for (int i = 0; i < Cap; i++) sim.Step();

            return sim.Director?.OrdersIssued ?? 0;
        }
    }
}
#endif
