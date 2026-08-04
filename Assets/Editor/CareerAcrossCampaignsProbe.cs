// CareerAcrossCampaignsProbe.cs
// #109 / #212–#215: career profile survives a finished chain; highland opens at Regiment
// with the same higher HQ still addressing the player.
//
// Menu:  Strategos > Probe Career Across Campaigns
// Batch: -executeMethod Strategos.Editor.CareerAcrossCampaignsProbe.Run

#if UNITY_EDITOR
using System.IO;
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
    public static class CareerAcrossCampaignsProbe
    {
        [MenuItem("Strategos/Probe Career Across Campaigns")]
        public static void Run()
        {
            EnsureFixtures();

            var log = new StringBuilder();
            int bad = 0;

            bad += CheckRoundTrip(log);
            bad += CheckStamp(log);
            bad += CheckTwoCampaignSmoke(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[CareerAcrossCampaignsProbe]\n" + log);
            else Debug.LogError("[CareerAcrossCampaignsProbe]\n" + log);
        }

        private static void EnsureFixtures()
        {
            string scenarios = Path.Combine(Application.dataPath, "Resources", "Scenarios");
            Directory.CreateDirectory(scenarios);
            string highlandPath = Path.Combine(scenarios,
                ScenarioSamples.HighlandOpeningName + ".json");
            var highland = ScenarioSamples.HighlandOpening();
            highland.Map.EnableErosion = true; // match shipped skirmish authoring for ShippedMapProbe
            ScenarioIO.SaveToFile(highland, highlandPath);
            AssetDatabase.Refresh();
            Debug.Log($"[CareerAcrossCampaignsProbe] wrote {highlandPath}");
        }

        private static int CheckRoundTrip(StringBuilder log)
        {
            var profile = new CareerProfile
            {
                CareerRankId = "regiment",
                FormationDesignation = "TF 1-7 IN",
                HigherFormation = "3 BDE",
            };
            var again = CareerProfileIO.FromJson(CareerProfileIO.ToJson(profile));
            if (again == null ||
                again.CareerRankId != "regiment" ||
                again.FormationDesignation != "TF 1-7 IN" ||
                again.HigherFormation != "3 BDE")
            {
                log.AppendLine("  round-trip: FAILED");
                return 1;
            }

            log.AppendLine("  round-trip: OK");
            return 0;
        }

        private static int CheckStamp(StringBuilder log)
        {
            var skirmish = ScenarioSamples.Skirmish();
            skirmish.Map.EnableErosion = false;
            var profile = CareerProfile.Default();
            profile.StampFormationFrom(skirmish);

            if (profile.FormationDesignation != "TF 1-7 IN" ||
                profile.HigherFormation != "3 BDE")
            {
                log.AppendLine(
                    $"  stamp: FAILED — got '{profile.FormationDesignation}' / " +
                    $"'{profile.HigherFormation}'");
                return 1;
            }

            log.AppendLine("  stamp: OK — TF 1-7 IN / 3 BDE");
            return 0;
        }

        private static int CheckTwoCampaignSmoke(StringBuilder log)
        {
            // Simulate end of valley: stamp BN seat, promote battalion → regiment.
            var valley = ScenarioSamples.Skirmish();
            valley.Map.EnableErosion = false;
            var profile = CareerProfile.Default();
            profile.CareerRankId = "battalion";
            profile.StampFormationFrom(valley);

            string rank = profile.CareerRankId;
            if (!RankGate.TryPromoteAfterCampaignWin(ref rank))
            {
                log.AppendLine("  smoke: FAILED — promote from battalion refused");
                return 1;
            }
            profile.CareerRankId = rank;

            if (profile.CareerRankId != "regiment" ||
                profile.HigherFormation != "3 BDE")
            {
                log.AppendLine(
                    $"  smoke: FAILED — after promote rank={profile.CareerRankId} " +
                    $"higher={profile.HigherFormation}");
                return 1;
            }

            var highland = ScenarioSamples.HighlandOpening();
            highland.Map.EnableErosion = false;
            var problems = highland.Validate(UnitCatalogue.Default());
            if (problems.Count > 0)
            {
                log.AppendLine($"  smoke: FAILED — highland invalid: {string.Join("; ", problems)}");
                return 1;
            }

            if (CommandScope.EffectivePlayerEchelon(highland) != Echelon.Regiment)
            {
                log.AppendLine(
                    $"  smoke: FAILED — highland seat " +
                    $"{CommandScope.EffectivePlayerEchelon(highland)}, expected Regiment");
                return 1;
            }

            if (CommandScope.EffectivePlayerEchelon(valley) ==
                CommandScope.EffectivePlayerEchelon(highland))
            {
                log.AppendLine("  smoke: FAILED — campaign 2 echelon did not differ");
                return 1;
            }

            if (!RankGate.Authorize(profile.CareerRankId, highland, out var allowProblem))
            {
                log.AppendLine($"  smoke: FAILED — regiment refused highland: {allowProblem}");
                return 1;
            }

            if (RankGate.Authorize("battalion", highland, out _))
            {
                log.AppendLine("  smoke: FAILED — battalion authorized for Regiment seat");
                return 1;
            }

            if (!highland.Directive.HasValue ||
                highland.Directive.Value.From != profile.HigherFormation)
            {
                log.AppendLine(
                    $"  smoke: FAILED — directive From " +
                    $"'{highland.Directive.Value.From}' != profile '{profile.HigherFormation}'");
                return 1;
            }

            // Publish path: Simulation ctor puts the directive on DirectiveLog.
            var map = highland.GenerateMap();
            var sim = new Simulation(highland, map, UnitCatalogue.Default());
            if (sim.DirectiveLog.Count < 1)
            {
                log.AppendLine("  smoke: FAILED — no directive published on highland start");
                return 1;
            }

            var published = sim.DirectiveLog[0];
            if (published.From != profile.HigherFormation ||
                published.TargetUnit.Value != 17)
            {
                log.AppendLine(
                    $"  smoke: FAILED — published From={published.From} " +
                    $"target={published.TargetUnit}");
                return 1;
            }

            // Chain load path for highland campaign.
            var chain = CampaignChainIO.Load(CampaignSamples.HighlandName);
            if (chain == null)
            {
                log.AppendLine("  smoke: FAILED — highland-campaign missing from Resources");
                return 1;
            }

            var chainProblems = chain.Validate(UnitCatalogue.Default());
            if (chainProblems.Count > 0)
            {
                log.AppendLine(
                    $"  smoke: FAILED — highland chain invalid: {string.Join("; ", chainProblems)}");
                return 1;
            }

            var started = CampaignChainDriver.StartNext(chain, 0, UnitCatalogue.Default());
            if (CommandScope.EffectivePlayerEchelon(started.Scenario) != Echelon.Regiment)
            {
                log.AppendLine("  smoke: FAILED — StartNext seat not Regiment");
                return 1;
            }

            log.AppendLine(
                "  smoke: OK — rank regiment, seat Regiment≠Battalion, " +
                "directive From=3 BDE, chain StartNext");
            return 0;
        }
    }
}
#endif
