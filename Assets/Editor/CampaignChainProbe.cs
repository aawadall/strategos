// CampaignChainProbe.cs
// #75 chunk 1: the data shape's own probe. Narrow on purpose — this data has no behaviour yet,
// so there is no signature to compare and nothing to replay. All this asserts is that a
// hand-built CampaignChain round-trips through JSON with every field intact, including the
// outcome enum and the carried-over ORBAT state, and that the comparison actually catches a
// dropped field rather than passing by accident (see docs/build-and-verify.md's "a guard that
// skips when the fixture is stale is a guard that cannot fail").
//
// Menu:  Strategos > Probe Campaign Chain
// Batch: -executeMethod Strategos.Editor.CampaignChainProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Campaigns;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class CampaignChainProbe
    {
        [MenuItem("Strategos/Probe Campaign Chain")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckRoundTrip(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[CampaignChainProbe]\n" + log);
            else Debug.LogError("[CampaignChainProbe]\n" + log);
        }

        // ─── Fixture ──────────────────────────────────────────────────────────

        /// <summary>
        /// Three operations: one already won with a mauled carried-over company, one already
        /// lost with an empty carry-over (a later chunk's job to populate), one not yet played.
        /// Exercises every field on both types, including an outcome other than the default.
        /// </summary>
        private static CampaignChain ThreeOperationChain()
        {
            var chain = new CampaignChain
            {
                Name = "Valley Campaign",
                Description = "Three linked operations up the same valley.",
            };

            var opOne = new CampaignChainEntry
            {
                ScenarioName = "skirmish",
                Name = "Opening Contact",
                Outcome = OperationOutcome.Won,
            };
            // A company that came out of operation one mauled — carried strength and readiness
            // are exactly what the next chunk reads back in.
            opOne.CarriedOverUnits.Add(new UnitInstance(
                new UnitId(1), new SideId(1), "SFGPUCI---B---", new Vector2(58f, 72f),
                "A/1-7 IN", "1-7 IN", 62, UnitCatalogue.InfantryMech)
            {
                Training = 100f,
                Readiness = 71f,
                Suppression = 12f,
                Posture = Posture.Halted,
                ParentId = new UnitId(7),
            });
            chain.Operations.Add(opOne);

            var opTwo = new CampaignChainEntry
            {
                ScenarioName = "skirmish",
                Name = "Second Push",
                Outcome = OperationOutcome.Lost,
                // Deliberately empty: a defeat's carry-over is not this chunk's to compute.
            };
            chain.Operations.Add(opTwo);

            var opThree = new CampaignChainEntry
            {
                ScenarioName = "skirmish",
                Name = "Final Objective",
                Outcome = OperationOutcome.Unplayed,
            };
            chain.Operations.Add(opThree);

            return chain;
        }

        // ─── Round trip ───────────────────────────────────────────────────────

        private static int CheckRoundTrip(StringBuilder log)
        {
            int bad = 0;
            var original = ThreeOperationChain();

            string json = CampaignChainIO.ToJson(original);
            var back = CampaignChainIO.FromJson(json);

            if (back == null)
            {
                log.AppendLine("  FAIL round trip produced null");
                return bad + 1;
            }

            log.AppendLine($"  serialised to {json.Length} bytes, {original.Operations.Count} operations");

            bad += Compare(original, back, log);

            // Same stability check ScenarioProbe makes: serialising twice must give identical
            // text, or the format is not stable and diffs of committed chains become noise.
            if (CampaignChainIO.ToJson(back) != json)
            {
                log.AppendLine("  FAIL re-serialising the round trip produced different text");
                bad++;
            }

            return bad;
        }

        private static int Compare(CampaignChain a, CampaignChain b, StringBuilder log)
        {
            int bad = 0;

            if (a.FormatVersion != b.FormatVersion) { log.AppendLine("  FAIL format version"); bad++; }
            if (a.Name != b.Name) { log.AppendLine("  FAIL chain name"); bad++; }
            if (a.Description != b.Description) { log.AppendLine("  FAIL chain description"); bad++; }

            if (a.Operations.Count != b.Operations.Count)
            {
                log.AppendLine($"  FAIL operation count {a.Operations.Count} -> {b.Operations.Count}");
                return bad + 1;
            }

            for (int i = 0; i < a.Operations.Count; i++)
            {
                var (ea, eb) = (a.Operations[i], b.Operations[i]);

                if (ea.ScenarioName != eb.ScenarioName)
                { log.AppendLine($"  FAIL operation {i}: scenario name"); bad++; }
                if (ea.Name != eb.Name)
                { log.AppendLine($"  FAIL operation {i}: name"); bad++; }
                if (ea.Outcome != eb.Outcome)
                { log.AppendLine($"  FAIL operation {i}: outcome {ea.Outcome} -> {eb.Outcome}"); bad++; }

                if (ea.CarriedOverUnits.Count != eb.CarriedOverUnits.Count)
                {
                    log.AppendLine($"  FAIL operation {i}: carried-over unit count " +
                                   $"{ea.CarriedOverUnits.Count} -> {eb.CarriedOverUnits.Count}");
                    bad++;
                    continue;
                }

                for (int u = 0; u < ea.CarriedOverUnits.Count; u++)
                {
                    var (ua, ub) = (ea.CarriedOverUnits[u], eb.CarriedOverUnits[u]);
                    string where = $"operation {i} unit {u}";

                    if (ua.Id != ub.Id) { log.AppendLine($"  FAIL {where}: id"); bad++; }
                    if (ua.Side != ub.Side) { log.AppendLine($"  FAIL {where}: side"); bad++; }
                    if (ua.ParentId != ub.ParentId) { log.AppendLine($"  FAIL {where}: parent"); bad++; }
                    if (ua.Sidc != ub.Sidc) { log.AppendLine($"  FAIL {where}: sidc"); bad++; }
                    if (ua.Designation != ub.Designation) { log.AppendLine($"  FAIL {where}: designation"); bad++; }
                    if (ua.HigherFormation != ub.HigherFormation) { log.AppendLine($"  FAIL {where}: formation"); bad++; }
                    if (ua.CapabilityId != ub.CapabilityId) { log.AppendLine($"  FAIL {where}: capability id"); bad++; }
                    if (!Mathf.Approximately(ua.Strength, ub.Strength)) { log.AppendLine($"  FAIL {where}: strength"); bad++; }
                    if (!Mathf.Approximately(ua.Readiness, ub.Readiness)) { log.AppendLine($"  FAIL {where}: readiness"); bad++; }
                    if (!Mathf.Approximately(ua.Suppression, ub.Suppression)) { log.AppendLine($"  FAIL {where}: suppression"); bad++; }
                    if (ua.Posture != ub.Posture) { log.AppendLine($"  FAIL {where}: posture"); bad++; }
                    // Exact, not approximate — same reasoning ScenarioProbe gives: a position
                    // that drifts on every save would walk.
                    if (ua.Cell != ub.Cell) { log.AppendLine($"  FAIL {where}: cell {ua.Cell} -> {ub.Cell}"); bad++; }
                }
            }

            if (bad == 0)
                log.AppendLine($"  {a.Operations.Count} operations, " +
                               $"{a.Operations[0].CarriedOverUnits.Count} carried-over unit(s) " +
                               "on operation 0, every field matched");

            return bad;
        }
    }
}
#endif
