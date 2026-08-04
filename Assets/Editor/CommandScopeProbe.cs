// CommandScopeProbe.cs
// #36 / #268: player echelon band — Company seat cannot Issue to BN parent; Company can.
// Also #267 validation and RankGate prefer authored PlayerEchelon.
//
// Menu:  Strategos > Probe Command Scope
// Batch: -executeMethod Strategos.Editor.CommandScopeProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.NatoSymbols;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class CommandScopeProbe
    {
        [MenuItem("Strategos/Probe Command Scope")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckDerive(log);
            bad += CheckValidate(log);
            bad += CheckRefuseBn(log);
            bad += CheckAllowCompany(log);
            bad += CheckRankGatePrefersAuthored(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[CommandScopeProbe]\n" + log);
            else Debug.LogError("[CommandScopeProbe]\n" + log);
        }

        private static Scenario CompanySeatSkirmish()
        {
            var s = ScenarioSamples.Skirmish();
            s.Map.EnableErosion = false;
            s.PlayerEchelon = Echelon.Company;
            return s;
        }

        private static int CheckDerive(StringBuilder log)
        {
            var s = ScenarioSamples.Skirmish();
            s.Map.EnableErosion = false;
            var e = CommandScope.EffectivePlayerEchelon(s);
            if (e != Echelon.Battalion)
            {
                log.AppendLine($"  derive: FAILED — expected Battalion, got {e}");
                return 1;
            }

            log.AppendLine($"  derive: OK — None → {e}");
            return 0;
        }

        private static int CheckValidate(StringBuilder log)
        {
            var s = CompanySeatSkirmish();
            var problems = s.Validate();
            if (problems.Count != 0)
            {
                log.AppendLine($"  validate: FAILED — {string.Join("; ", problems)}");
                return 1;
            }

            s.PlayerEchelon = Echelon.Division;
            problems = s.Validate();
            if (problems.Count == 0)
            {
                log.AppendLine("  validate: FAILED — Division seat with no Division unit accepted");
                return 1;
            }

            log.AppendLine($"  validate: OK — Company seat clean; Division rejected ({problems[0]})");
            return 0;
        }

        private static int CheckRefuseBn(StringBuilder log)
        {
            var s = CompanySeatSkirmish();
            var map = s.GenerateMap();
            var sim = new Simulation(s, map);
            // Blue BN is unit 7 in Skirmish.
            var bn = new UnitId(7);
            var actor = ActorId.ForSide(s.PlayerSide);
            var stamped = sim.Issue(Command.Hold(actor, bn));
            if (stamped.Seq != 0)
            {
                log.AppendLine($"  refuse BN: FAILED — Issue accepted Seq={stamped.Seq}");
                return 1;
            }

            if (sim.Log.Count != 0)
            {
                log.AppendLine($"  refuse BN: FAILED — log has {sim.Log.Count} entries");
                return 1;
            }

            var unit = sim.Hierarchy.Find(bn);
            if (unit == null || CommandScope.CanAddress(s, unit))
            {
                log.AppendLine("  refuse BN: FAILED — CanAddress true for BN under Company seat");
                return 1;
            }

            log.AppendLine("  refuse BN: OK — Seq=0, log empty, CanAddress false");
            return 0;
        }

        private static int CheckAllowCompany(StringBuilder log)
        {
            var s = CompanySeatSkirmish();
            var map = s.GenerateMap();
            var sim = new Simulation(s, map);
            var company = new UnitId(1);
            var actor = ActorId.ForSide(s.PlayerSide);
            var stamped = sim.Issue(Command.Hold(actor, company));
            if (stamped.Seq == 0)
            {
                log.AppendLine("  allow company: FAILED — Issue refused");
                return 1;
            }

            if (!CommandScope.CanAddress(s, sim.Hierarchy.Find(company)))
            {
                log.AppendLine("  allow company: FAILED — CanAddress false");
                return 1;
            }

            log.AppendLine($"  allow company: OK — Seq={stamped.Seq}");
            return 0;
        }

        private static int CheckRankGatePrefersAuthored(StringBuilder log)
        {
            var s = CompanySeatSkirmish();
            if (RankGate.RequiredEchelon(s) != Echelon.Company)
            {
                log.AppendLine($"  rank: FAILED — RequiredEchelon {RankGate.RequiredEchelon(s)}");
                return 1;
            }

            if (!RankGate.Authorize("company", s, out var problem))
            {
                log.AppendLine($"  rank: FAILED — company refused: {problem}");
                return 1;
            }

            log.AppendLine("  rank: OK — authored Company seat authorizes company rank");
            return 0;
        }
    }
}
#endif
