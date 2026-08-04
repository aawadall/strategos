// ScenarioGeneratorProbe.cs
// #334 / #352: generated scenario validates; SideEnv.Create can Reset on it.
//
// Menu:  Strategos > Probe Scenario Generator
// Batch: -executeMethod Strategos.Editor.ScenarioGeneratorProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.NatoSymbols;
using Strategos.Scenarios;
using Strategos.SimEnv;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class ScenarioGeneratorProbe
    {
        [MenuItem("Strategos/Probe Scenario Generator")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckMeetingValid(log);
            bad += CheckDefendAttackTemplates(log);
            bad += CheckForceRatio(log);
            bad += CheckSideEnvSmoke(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[ScenarioGeneratorProbe]\n" + log);
            else Debug.LogError("[ScenarioGeneratorProbe]\n" + log);
        }

        private static int CheckMeetingValid(StringBuilder log)
        {
            var settings = new ScenarioGenerationSettings
            {
                Seed = 20260804,
                Echelon = Echelon.Company,
                ForceRatio = 1f,
                Engagement = EngagementType.Meeting,
                Width = 64,
                Height = 64,
                EnableErosion = false,
            };
            var catalogue = UnitCatalogue.Default();
            var scenario = ScenarioGenerator.Generate(settings, out var map, catalogue);
            var problems = ScenarioGenerator.ValidateGenerated(scenario, map, settings, catalogue);
            if (problems.Count > 0)
            {
                log.AppendLine("  meeting: FAILED — " + string.Join("; ", problems));
                return 1;
            }

            log.AppendLine(
                $"  meeting: OK — '{scenario.Name}' units={scenario.Units.Count} " +
                $"victory={scenario.Victory.Count}");
            return 0;
        }

        private static int CheckDefendAttackTemplates(StringBuilder log)
        {
            int bad = 0;
            foreach (var eng in new[] { EngagementType.Defend, EngagementType.Attack })
            {
                var settings = new ScenarioGenerationSettings
                {
                    Seed = 42,
                    Engagement = eng,
                    Echelon = Echelon.Company,
                    Width = 64,
                    Height = 64,
                    EnableErosion = false,
                };
                var scenario = ScenarioGenerator.Generate(settings, out var map,
                    UnitCatalogue.Default());
                var problems = ScenarioGenerator.ValidateGenerated(
                    scenario, map, settings, UnitCatalogue.Default());
                if (problems.Count > 0)
                {
                    log.AppendLine($"  {eng}: FAILED — " + string.Join("; ", problems));
                    bad++;
                    continue;
                }

                bool hasSurvive = false, hasHold = false;
                for (int i = 0; i < scenario.Victory.Count; i++)
                {
                    var v = scenario.Victory[i];
                    if (v.Kind == Objectives.VictoryKind.SurviveUntil) hasSurvive = true;
                    if (v.Kind == Objectives.VictoryKind.HoldObjectives) hasHold = true;
                }

                if (!hasSurvive || !hasHold)
                {
                    log.AppendLine($"  {eng}: FAILED — expected SurviveUntil + HoldObjectives");
                    bad++;
                    continue;
                }

                var obj = scenario.Objectives[0];
                if (eng == EngagementType.Defend && obj.InitialOwner != scenario.PlayerSide)
                {
                    log.AppendLine("  Defend: FAILED — objective should be player-owned");
                    bad++;
                    continue;
                }

                if (eng == EngagementType.Attack && obj.InitialOwner == scenario.PlayerSide)
                {
                    log.AppendLine("  Attack: FAILED — objective should not be player-owned");
                    bad++;
                    continue;
                }

                log.AppendLine($"  {eng}: OK — templates + InitialOwner");
            }

            return bad;
        }

        private static int CheckForceRatio(StringBuilder log)
        {
            var settings = new ScenarioGenerationSettings
            {
                Seed = 7,
                ForceRatio = 1.5f,
                Echelon = Echelon.Battalion,
                Width = 64,
                Height = 64,
                EnableErosion = false,
                ForceRatioTolerance = 0.4f,
            };
            var scenario = ScenarioGenerator.Generate(settings, out var map,
                UnitCatalogue.Default());
            var problems = ScenarioGenerator.ValidateGenerated(
                scenario, map, settings, UnitCatalogue.Default());
            if (problems.Count > 0)
            {
                log.AppendLine("  ratio: FAILED — " + string.Join("; ", problems));
                return 1;
            }

            var hierarchy = new UnitHierarchy(scenario.Units);
            int friendly = 0, enemy = 0;
            foreach (var leaf in hierarchy.Leaves)
            {
                if (leaf.Side == scenario.PlayerSide) friendly++;
                else enemy++;
            }

            float actual = (float)enemy / friendly;
            if (Mathf.Abs(actual - 1.5f) > 0.4f)
            {
                log.AppendLine($"  ratio: FAILED — got {actual:0.##} want ~1.5");
                return 1;
            }

            log.AppendLine($"  ratio: OK — enemy/friendly leaves {enemy}/{friendly} = {actual:0.##}");
            return 0;
        }

        private static int CheckSideEnvSmoke(StringBuilder log)
        {
            var settings = new ScenarioGenerationSettings
            {
                Seed = 99,
                Engagement = EngagementType.Meeting,
                Echelon = Echelon.Company,
                Width = 64,
                Height = 64,
                EnableErosion = false,
            };
            var scenario = ScenarioGenerator.Generate(settings);
            try
            {
                var env = SideEnv.Create(scenario, scenario.PlayerSide, UnitCatalogue.Default(),
                    opposingDirectorSides: null,
                    enableReactions: true,
                    enableErosion: false);
                env.Reset();
                if (env.Simulation.Units.Count < 1)
                {
                    log.AppendLine("  SideEnv: FAILED — empty after Reset");
                    return 1;
                }

                // One idle step — done may stay false; just prove Step does not throw.
                env.Step();
                log.AppendLine(
                    $"  SideEnv: OK — Reset+Step on generated scenario " +
                    $"(units={env.Simulation.Units.Count})");
                return 0;
            }
            catch (System.Exception ex)
            {
                log.AppendLine("  SideEnv: FAILED — " + ex.Message);
                return 1;
            }
        }
    }
}
#endif
