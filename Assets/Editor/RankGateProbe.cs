// RankGateProbe.cs
// #76 / #225: under-rank refuses a BN scenario; BN allows; promotion climbs one rung.
//
// Menu:  Strategos > Probe Rank Gates
// Batch: -executeMethod Strategos.Editor.RankGateProbe.Run

#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.NatoSymbols;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class RankGateProbe
    {
        [MenuItem("Strategos/Probe Rank Gates")]
        public static void Run()
        {
            WriteSampleConfig();

            var log = new StringBuilder();
            int bad = 0;

            bad += CheckTable(log);
            bad += CheckRefusal(log);
            bad += CheckAllow(log);
            bad += CheckPromote(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[RankGateProbe]\n" + log);
            else Debug.LogError("[RankGateProbe]\n" + log);
        }

        private static void WriteSampleConfig()
        {
            string dir = Path.Combine(Application.dataPath, "Resources", RankAuthorityIO.ResourceFolder);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, RankAuthorityDefaults.ConfigName + ".json");
            RankAuthorityIO.SaveToFile(path, RankAuthorityDefaults.UsArmy());
            RankAuthorityIO.Reload();
            Debug.Log($"[RankGateProbe] wrote {path}");
        }

        private static int CheckTable(StringBuilder log)
        {
            var table = RankAuthorityIO.Current;
            if (table.Steps == null || table.Steps.Length < 3)
            {
                log.AppendLine($"  table: FAILED — {table.Steps?.Length ?? 0} steps");
                return 1;
            }

            if (table.MaxEchelonFor("company") != Echelon.Company ||
                table.MaxEchelonFor("battalion") != Echelon.Battalion)
            {
                log.AppendLine("  table: FAILED — company/battalion max echelon wrong");
                return 1;
            }

            log.AppendLine($"  table: OK — {table.Steps.Length} steps " +
                           $"(default '{RankAuthorityDefaults.DefaultRankId}')");
            return 0;
        }

        private static int CheckRefusal(StringBuilder log)
        {
            var skirmish = ScenarioSamples.Skirmish();
            var required = RankGate.RequiredEchelon(skirmish);
            if ((int)required < (int)Echelon.Battalion)
            {
                log.AppendLine($"  refuse: FAILED — skirmish required {required}, expected ≥ Battalion");
                return 1;
            }

            if (RankGate.Authorize("company", skirmish, out var problem))
            {
                log.AppendLine("  refuse: FAILED — company authorized for BN scenario");
                return 1;
            }

            if (string.IsNullOrEmpty(problem) || !problem.Contains("RANK GATE"))
            {
                log.AppendLine($"  refuse: FAILED — bad problem text '{problem}'");
                return 1;
            }

            log.AppendLine($"  refuse: OK — company blocked (requires {required}): {problem}");
            return 0;
        }

        private static int CheckAllow(StringBuilder log)
        {
            var skirmish = ScenarioSamples.Skirmish();
            if (!RankGate.Authorize("battalion", skirmish, out var problem))
            {
                log.AppendLine($"  allow: FAILED — battalion refused: {problem}");
                return 1;
            }

            log.AppendLine("  allow: OK — battalion may start skirmish");
            return 0;
        }

        private static int CheckPromote(StringBuilder log)
        {
            string rank = "battalion";
            if (!RankGate.TryPromoteAfterCampaignWin(ref rank))
            {
                log.AppendLine("  promote: FAILED — battalion did not promote");
                return 1;
            }

            if (rank != "regiment")
            {
                log.AppendLine($"  promote: FAILED — expected regiment, got '{rank}'");
                return 1;
            }

            if (RankAuthorityIO.Current.MaxEchelonFor(rank) != Echelon.Regiment)
            {
                log.AppendLine("  promote: FAILED — regiment max echelon wrong");
                return 1;
            }

            // Top of table should refuse further promotion.
            string top = "corps";
            if (RankGate.TryPromoteAfterCampaignWin(ref top))
            {
                log.AppendLine($"  promote: FAILED — corps promoted to '{top}'");
                return 1;
            }

            log.AppendLine("  promote: OK — battalion→regiment; corps stays put");
            return 0;
        }
    }
}
#endif
