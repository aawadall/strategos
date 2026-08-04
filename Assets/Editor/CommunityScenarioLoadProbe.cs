// CommunityScenarioLoadProbe.cs
// #341: prove ScenarioIO loads arbitrary community JSON — no shipped-name allowlist.
//
// Batch: -executeMethod Strategos.Editor.CommunityScenarioLoadProbe.Run

#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Maps;
using Strategos.NatoSymbols;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class CommunityScenarioLoadProbe
    {
        [MenuItem("Strategos/Probe Community Scenario Load")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            // Name deliberately not in Resources/Scenarios — community packs are files.
            const string communityName = "community-pack-smoke-not-shipped";
            var scenario = BuildMinimal(communityName);

            string json = ScenarioIO.ToJson(scenario);
            var fromJson = ScenarioIO.FromJson(json);
            if (fromJson == null)
            {
                log.AppendLine("  FAIL FromJson returned null for community scenario");
                bad++;
            }
            else if (fromJson.Name != communityName)
            {
                log.AppendLine($"  FAIL FromJson name '{fromJson.Name}' != '{communityName}'");
                bad++;
            }
            else
            {
                log.AppendLine($"  FromJson ok: '{fromJson.Name}' ({fromJson.Units.Count} units)");
            }

            string tmp = Path.Combine(Path.GetTempPath(), communityName + ".json");
            try
            {
                ScenarioIO.SaveToFile(scenario, tmp);
                var fromFile = ScenarioIO.LoadFromFile(tmp);
                if (fromFile == null)
                {
                    log.AppendLine("  FAIL LoadFromFile returned null");
                    bad++;
                }
                else
                {
                    var problems = fromFile.Validate(UnitCatalogue.Default());
                    if (problems.Count > 0)
                    {
                        foreach (var p in problems)
                            log.AppendLine($"  FAIL community Validate: {p}");
                        bad += problems.Count;
                    }
                    else
                    {
                        log.AppendLine($"  LoadFromFile + Validate ok ({tmp})");
                    }
                }

                // Shipped Load(name) must not invent an allowlist failure — missing asset
                // returns null, not throw.
                var missing = ScenarioIO.Load(communityName);
                if (missing != null)
                {
                    log.AppendLine("  FAIL Load(non-shipped) unexpectedly found a Resources asset");
                    bad++;
                }
                else
                {
                    log.AppendLine("  Load(non-shipped) correctly returned null (no allowlist)");
                }
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[CommunityScenarioLoadProbe]\n" + log);
            else Debug.LogError("[CommunityScenarioLoadProbe]\n" + log);
        }

        private static Scenario BuildMinimal(string name)
        {
            var blue = new Side(new SideId(1), "BLUE", Affiliation.Friend);
            var red = new Side(new SideId(2), "RED", Affiliation.Hostile);
            var s = new Scenario
            {
                Name = name,
                Description = "Community pack smoke — not a shipped scenario.",
                Map = new MapGenerationSettings
                {
                    Name = "Smoke",
                    Seed = 1,
                    Width = 32,
                    Height = 32,
                    MetresPerCell = 25f,
                    Profile = ReliefProfile.Plains,
                    EnableErosion = false,
                    EnableCulture = false,
                },
            };
            s.Sides.Add(blue);
            s.Sides.Add(red);
            s.PlayerSide = blue.Id;

            var code = SIDCBuilder.Build(
                affiliation: Affiliation.Friend,
                echelon: Echelon.Company,
                entityCode: (int)LandEntityCode.Infantry,
                entityType: IconDecorator.VarStandard);
            s.Units.Add(new UnitInstance(new UnitId(1), blue.Id, code.Raw, new Vector2(8f, 8f),
                "A Co", "BN", 100, UnitCatalogue.InfantryFoot));

            var redCode = SIDCBuilder.Build(
                affiliation: Affiliation.Hostile,
                echelon: Echelon.Company,
                entityCode: (int)LandEntityCode.Infantry,
                entityType: IconDecorator.VarStandard);
            s.Units.Add(new UnitInstance(new UnitId(2), red.Id, redCode.Raw, new Vector2(24f, 24f),
                "B Co", "BN", 100, UnitCatalogue.InfantryFoot));

            return s;
        }
    }
}
#endif
