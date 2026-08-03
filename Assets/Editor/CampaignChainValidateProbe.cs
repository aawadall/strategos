// CampaignChainValidateProbe.cs
// #138: CampaignChain.Validate — good three-op fixture is clean; bad scenario names,
// unmatched carried-over ids, and Id-consistency collisions are reported.
//
// Menu:  Strategos > Probe Campaign Chain Validate
// Batch: -executeMethod Strategos.Editor.CampaignChainValidateProbe.Run

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Campaigns;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class CampaignChainValidateProbe
    {
        [MenuItem("Strategos/Probe Campaign Chain Validate")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckGoodThreeOpChain(log);
            bad += CheckUnknownScenarioName(log);
            bad += CheckUnmatchedCarriedOver(log);
            bad += CheckIdConsistencyCollision(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[CampaignChainValidateProbe]\n" + log);
            else Debug.LogError("[CampaignChainValidateProbe]\n" + log);
        }

        /// <summary>
        /// Same skirmish → push-north → skirmish shape as
        /// <see cref="CampaignChainDriverProbe"/>'s three-op fixture.
        /// </summary>
        private static CampaignChain ThreeOpChain()
        {
            var chain = new CampaignChain { Name = "Valley Campaign — Three Operations" };
            chain.Operations.Add(new CampaignChainEntry
            {
                ScenarioName = ScenarioSamples.SkirmishName, Name = "Opening Contact",
            });
            chain.Operations.Add(new CampaignChainEntry
            {
                ScenarioName = ScenarioSamples.PushNorthName, Name = "Push North",
            });
            chain.Operations.Add(new CampaignChainEntry
            {
                ScenarioName = ScenarioSamples.SkirmishName, Name = "Opening Contact, Second Tour",
            });
            return chain;
        }

        private static int CheckGoodThreeOpChain(StringBuilder log)
        {
            int bad = 0;
            var problems = ThreeOpChain().Validate(UnitCatalogue.Default());
            if (problems.Count != 0)
            {
                bad++;
                log.AppendLine($"  FAIL good three-op chain: expected 0 problems, got {problems.Count}");
                foreach (var p in problems) log.AppendLine($"    - {p}");
            }
            else
            {
                log.AppendLine("  good three-op chain validates clean  ok");
            }

            return bad;
        }

        private static int CheckUnknownScenarioName(StringBuilder log)
        {
            int bad = 0;
            var chain = new CampaignChain { Name = "Broken Names" };
            chain.Operations.Add(new CampaignChainEntry
            {
                ScenarioName = "no-such-scenario-xyz", Name = "Ghost Op",
            });

            var problems = chain.Validate(UnitCatalogue.Default());
            if (!AnyContains(problems, "did not load") && !AnyContains(problems, "no-such-scenario"))
            {
                bad++;
                log.AppendLine("  FAIL unknown scenario: expected a load failure message");
                foreach (var p in problems) log.AppendLine($"    - {p}");
            }
            else
            {
                log.AppendLine($"  unknown scenario reported ({problems.Count} problem(s))  ok");
            }

            return bad;
        }

        private static int CheckUnmatchedCarriedOver(StringBuilder log)
        {
            int bad = 0;
            var chain = new CampaignChain { Name = "Bad Carry" };
            var entry = new CampaignChainEntry
            {
                ScenarioName = ScenarioSamples.SkirmishName, Name = "With Phantom",
            };
            entry.CarriedOverUnits.Add(new UnitInstance(
                new UnitId(99999), new SideId(1), "SFGPUCI---B---", Vector2.zero,
                "PHANTOM", "", 50, UnitCatalogue.InfantryMech));
            chain.Operations.Add(entry);

            var problems = chain.Validate(UnitCatalogue.Default());
            if (!AnyContains(problems, "99999") && !AnyContains(problems, "carried-over"))
            {
                bad++;
                log.AppendLine("  FAIL unmatched carry: expected a carried-over mismatch message");
                foreach (var p in problems) log.AppendLine($"    - {p}");
            }
            else
            {
                log.AppendLine($"  unmatched carried-over id reported ({problems.Count} problem(s))  ok");
            }

            return bad;
        }

        private static int CheckIdConsistencyCollision(StringBuilder log)
        {
            int bad = 0;

            var skirmish = ScenarioIO.Load(ScenarioSamples.SkirmishName);
            var push = ScenarioIO.Load(ScenarioSamples.PushNorthName);
            if (skirmish == null || push == null)
            {
                log.AppendLine("  FAIL id-consistency: could not load skirmish / push-north");
                return 1;
            }

            // Mutate a shared id's Side in push-north so Validate must report a collision.
            // UnitId(2) is the tracked armor platoon in the three-op fixture — present in both.
            var pushJson = ScenarioIO.ToJson(push);
            var brokenPush = ScenarioIO.FromJson(pushJson);
            var shared = brokenPush.FindUnit(new UnitId(2));
            if (shared == null)
            {
                log.AppendLine("  FAIL id-consistency: push-north has no UnitId(2) to corrupt");
                return 1;
            }

            SideId other = default;
            foreach (var s in brokenPush.Sides)
            {
                if (s.Id != shared.Side) { other = s.Id; break; }
            }

            if (!other.IsValid)
            {
                log.AppendLine("  FAIL id-consistency: push-north has no opposing side to swap to");
                return 1;
            }

            shared.Side = other;

            var chain = new CampaignChain { Name = "Id Collision" };
            chain.Operations.Add(new CampaignChainEntry
            {
                ScenarioName = ScenarioSamples.SkirmishName, Name = "A",
            });
            chain.Operations.Add(new CampaignChainEntry
            {
                ScenarioName = ScenarioSamples.PushNorthName, Name = "B",
            });

            var problems = chain.Validate(UnitCatalogue.Default(), name =>
            {
                if (name == ScenarioSamples.SkirmishName) return skirmish;
                if (name == ScenarioSamples.PushNorthName) return brokenPush;
                return ScenarioIO.Load(name);
            });

            if (!AnyContains(problems, "Id-consistency"))
            {
                bad++;
                log.AppendLine("  FAIL id-consistency: expected a Side/Capability collision message");
                foreach (var p in problems) log.AppendLine($"    - {p}");
            }
            else
            {
                log.AppendLine($"  id-consistency collision reported ({problems.Count} problem(s))  ok");
            }

            return bad;
        }

        private static bool AnyContains(List<string> problems, string needle)
        {
            for (int i = 0; i < problems.Count; i++)
                if (problems[i] != null && problems[i].IndexOf(needle) >= 0)
                    return true;
            return false;
        }
    }
}
#endif
