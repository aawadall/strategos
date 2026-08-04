// ClimbCampaignProbe.cs
// #408 / #403: two-op climb through CampaignChainDriver — Squad seat → carry → Company seat.
// Asserts PlayerEchelon escalates and CommandScope accepts the company HQ, not only leaf 1.
//
// Menu:  Strategos > Probe Climb Campaign
// Batch: -executeMethod Strategos.Editor.ClimbCampaignProbe.Run

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Campaigns;
using Strategos.Commands;
using Strategos.NatoSymbols;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class ClimbCampaignProbe
    {
        [MenuItem("Strategos/Probe Climb Campaign")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckSquadToCompanyClimb(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[ClimbCampaignProbe]\n" + log);
            else Debug.LogError("[ClimbCampaignProbe]\n" + log);
        }

        private static int CheckSquadToCompanyClimb(StringBuilder log)
        {
            int bad = 0;

            var chain = CampaignChainIO.Load(CampaignSamples.ClimbName);
            if (chain == null)
            {
                log.AppendLine("  FAIL shipped climb-campaign did not load");
                return 1;
            }

            var problems = chain.Validate(UnitCatalogue.Default());
            if (problems.Count > 0)
            {
                log.AppendLine($"  FAIL climb-campaign Validate: {problems.Count} problem(s)");
                foreach (var p in problems) log.AppendLine($"    - {p}");
                return 1;
            }

            // ── Op 0: Squad seat ──────────────────────────────────────────────
            var sim0 = CampaignChainDriver.StartNext(chain, 0, UnitCatalogue.Default());
            if (sim0.Scenario.PlayerEchelon != Echelon.Squad)
            {
                log.AppendLine(
                    $"  FAIL op 0 PlayerEchelon is {sim0.Scenario.PlayerEchelon}, expected Squad");
                return 1;
            }

            var leaf0 = sim0.Scenario.FindUnit(new UnitId(1));
            if (leaf0 == null || !CommandScope.CanAddress(sim0.Scenario, leaf0))
            {
                log.AppendLine("  FAIL op 0: friendly leaf 1 missing or not addressable at Squad");
                bad++;
            }

            if (!RunToDecision(sim0))
            {
                log.AppendLine(
                    $"  FAIL op 0 (climb-squad) never decided after {sim0.Tick} ticks");
                return bad + 1;
            }

            log.AppendLine(
                $"  op 0 (Squad) decided: {sim0.Victory.Outcome} at t{sim0.Tick}");

            CampaignCarryOver.CarryOver(sim0, chain.Operations[0], chain.Operations[1], 6f);
            if (chain.Operations[1].CarriedOverUnits == null ||
                chain.Operations[1].CarriedOverUnits.Count == 0)
            {
                log.AppendLine("  FAIL carry-over left op 1 with no survivors");
                return bad + 1;
            }

            log.AppendLine(
                $"  carried {chain.Operations[1].CarriedOverUnits.Count} survivor(s) → Company");

            // ── Op 1: Company seat ────────────────────────────────────────────
            var sim1 = CampaignChainDriver.StartNext(chain, 1, UnitCatalogue.Default());
            if (sim1.Scenario.PlayerEchelon != Echelon.Company)
            {
                log.AppendLine(
                    $"  FAIL op 1 PlayerEchelon is {sim1.Scenario.PlayerEchelon}, expected Company");
                bad++;
            }

            var companyHq = sim1.Scenario.FindUnit(new UnitId(11));
            if (companyHq == null)
            {
                log.AppendLine("  FAIL op 1 missing company HQ unit 11");
                bad++;
            }
            else if (!CommandScope.CanAddress(sim1.Scenario, companyHq))
            {
                log.AppendLine(
                    "  FAIL op 1: company HQ 11 not addressable at Company seat " +
                    $"(echelon {companyHq.ToSidcCode().Echelon})");
                bad++;
            }
            else
            {
                log.AppendLine(
                    $"  op 1 command scope: company HQ 11 addressable " +
                    $"({companyHq.ToSidcCode().Echelon})");
            }

            var leaf1 = sim1.Scenario.FindUnit(new UnitId(1));
            if (leaf1 == null || !CommandScope.CanAddress(sim1.Scenario, leaf1))
            {
                log.AppendLine("  FAIL op 1: leaf 1 missing or not addressable under Company");
                bad++;
            }

            if (CommandScope.EffectivePlayerEchelon(sim1.Scenario) != Echelon.Company)
            {
                log.AppendLine(
                    $"  FAIL EffectivePlayerEchelon is " +
                    $"{CommandScope.EffectivePlayerEchelon(sim1.Scenario)}, expected Company");
                bad++;
            }

            if (bad == 0)
                log.AppendLine("  climb Squad → Company through CampaignChainDriver  ok");
            return bad;
        }

        private static bool RunToDecision(Simulation sim)
        {
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.EnableReactions();
            foreach (var u in sim.Scenario.Units) u.Roe = RulesOfEngagement.FireAtWill;

            var sides = new List<SideId>();
            foreach (var side in sim.Scenario.Sides) sides.Add(side.Id);
            sim.EnableDirector(sides);

            int limit = sim.Scenario.TimeLimitTicks > 0
                ? sim.Scenario.TimeLimitTicks + 60
                : 4000;
            while (sim.Tick < limit && !sim.IsOver) sim.Step();
            return sim.IsOver;
        }
    }
}
#endif
