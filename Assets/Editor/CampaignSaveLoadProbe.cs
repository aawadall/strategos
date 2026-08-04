// CampaignSaveLoadProbe.cs
// #140: mid-campaign save carries the mutated CampaignChain (Outcomes / CarriedOverUnits)
// and OperationIndex; restore resumes the same op and can still CONTINUE into the next.
//
// Menu:  Strategos > Probe Campaign Save Load
// Batch: -executeMethod Strategos.Editor.CampaignSaveLoadProbe.Run

#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Campaigns;
using Strategos.Commands;
using Strategos.Persistence;
using Strategos.Persistence.Files;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class CampaignSaveLoadProbe
    {
        [MenuItem("Strategos/Probe Campaign Save Load")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckCampaignRoundTrip(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[CampaignSaveLoadProbe]\n" + log);
            else Debug.LogError("[CampaignSaveLoadProbe]\n" + log);
        }

        private static string TempDir() =>
            Path.Combine(Path.GetTempPath(), "strategos-campaign-save-probe-" + Guid.NewGuid());

        private static int CheckCampaignRoundTrip(StringBuilder log)
        {
            int bad = 0;
            var dir = TempDir();
            try
            {
                // #406: climb chain must load and Validate alongside Valley.
                var climb = CampaignChainIO.Load(CampaignSamples.ClimbName);
                if (climb == null)
                {
                    log.AppendLine("  FAIL shipped climb-campaign did not load");
                    return 1;
                }

                var climbProblems = climb.Validate(UnitCatalogue.Default());
                if (climbProblems.Count > 0)
                {
                    log.AppendLine($"  FAIL climb-campaign Validate: {climbProblems.Count} problem(s)");
                    foreach (var p in climbProblems) log.AppendLine($"    - {p}");
                    return 1;
                }

                log.AppendLine(
                    $"  shipped '{CampaignSamples.ClimbName}': {climb.Name}, {climb.Operations.Count} ops");

                var chain = CampaignChainIO.Load(CampaignSamples.ValleyName);
                if (chain == null)
                {
                    log.AppendLine("  FAIL shipped valley-campaign did not load");
                    return 1;
                }

                var problems = chain.Validate(UnitCatalogue.Default());
                if (problems.Count > 0)
                {
                    log.AppendLine($"  FAIL valley-campaign Validate: {problems.Count} problem(s)");
                    return 1;
                }

                // Op 0 → decide → carry into op 1 → step a bit → save mid-op 1.
                var sim0 = CampaignChainDriver.StartNext(chain, 0, UnitCatalogue.Default());
                WireUnattended(sim0);
                if (!RunToDecision(sim0))
                {
                    log.AppendLine("  FAIL operation 0 never decided");
                    return 1;
                }

                CampaignCarryOver.CarryOver(sim0, chain.Operations[0], chain.Operations[1], 6f);
                if (chain.Operations[1].CarriedOverUnits == null ||
                    chain.Operations[1].CarriedOverUnits.Count == 0)
                {
                    log.AppendLine("  FAIL carry-over left op 1 with no survivors");
                    return 1;
                }

                int carriedCount = chain.Operations[1].CarriedOverUnits.Count;
                var sim1 = CampaignChainDriver.StartNext(chain, 1, UnitCatalogue.Default());
                WireUnattended(sim1);
                sim1.Step(5);

                var store = new FileGameStore(dir);
                var record = new SaveRecord
                {
                    SaveId = "campaign-mid",
                    ScenarioName = sim1.Scenario.Name,
                    Tick = sim1.Tick,
                    SavedAtUtc = DateTime.UtcNow.ToString("o"),
                    Snapshot = sim1.Snapshot(),
                    CampaignName = chain.Name,
                    OperationIndex = 1,
                    CampaignChainJson = CampaignChainIO.ToJson(chain),
                };
                store.SaveAsync(record).GetAwaiter().GetResult();

                var load = store.LoadAsync("campaign-mid").GetAwaiter().GetResult();
                var loaded = load.Ok ? load.Value : null;
                if (loaded == null || string.IsNullOrEmpty(loaded.CampaignChainJson))
                {
                    log.AppendLine("  FAIL campaign save lost CampaignChainJson");
                    return 1;
                }

                if (loaded.OperationIndex != 1 || loaded.CampaignName != chain.Name)
                {
                    log.AppendLine(
                        $"  FAIL campaign columns: index={loaded.OperationIndex} name='{loaded.CampaignName}'");
                    bad++;
                }

                var restoredChain = CampaignChainIO.FromJson(loaded.CampaignChainJson);
                if (restoredChain == null ||
                    restoredChain.Operations[1].CarriedOverUnits.Count != carriedCount)
                {
                    log.AppendLine("  FAIL restored chain lost CarriedOverUnits on op 1");
                    bad++;
                }

                if (restoredChain.Operations[0].Outcome == OperationOutcome.Unplayed)
                {
                    log.AppendLine("  FAIL restored chain lost op 0 Outcome");
                    bad++;
                }

                var restored = Simulation.Restore(loaded.Snapshot, UnitCatalogue.Default());
                if (restored.Tick != sim1.Tick)
                {
                    log.AppendLine($"  FAIL restored tick {restored.Tick} != saved {sim1.Tick}");
                    bad++;
                }

                // CONTINUE after resume: carry into op 2 must still work.
                WireUnattended(restored);
                if (!RunToDecision(restored))
                {
                    log.AppendLine("  FAIL restored op 1 never decided");
                    bad++;
                }
                else
                {
                    CampaignCarryOver.CarryOver(restored, restoredChain.Operations[1],
                        restoredChain.Operations[2], 6f);
                    var sim2 = CampaignChainDriver.StartNext(restoredChain, 2,
                        UnitCatalogue.Default());
                    if (sim2 == null)
                    {
                        log.AppendLine("  FAIL StartNext op 2 after resume failed");
                        bad++;
                    }
                    else
                    {
                        log.AppendLine(
                            $"  campaign mid-save: op1 tick {loaded.Tick}, " +
                            $"{carriedCount} carried, CONTINUE → op2 ok");
                    }
                }
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }

            if (bad == 0 && !log.ToString().Contains("campaign mid-save:"))
                log.AppendLine("  campaign mid-save round trip  ok");
            return bad;
        }

        private static void WireUnattended(Simulation sim)
        {
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.EnableReactions();
            foreach (var u in sim.Scenario.Units) u.Roe = RulesOfEngagement.FireAtWill;
            var sides = new System.Collections.Generic.List<SideId>();
            foreach (var side in sim.Scenario.Sides) sides.Add(side.Id);
            sim.EnableDirector(sides);
        }

        private static bool RunToDecision(Simulation sim)
        {
            int limit = sim.Scenario.TimeLimitTicks > 0
                ? sim.Scenario.TimeLimitTicks + 60
                : 4000;
            while (sim.Tick < limit && !sim.IsOver) sim.Step();
            return sim.IsOver;
        }
    }
}
#endif
