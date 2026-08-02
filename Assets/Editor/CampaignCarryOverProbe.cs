// CampaignCarryOverProbe.cs
// #75 chunk 2: CampaignCarryOver's own probe. Proves the two things the chunk-2 brief calls
// out by name — a wreck is excluded from CarriedOverUnits, and readiness recovery is exactly
// FatigueModel's formula, not a fork of it — plus the outcome mapping and the "no mutation of
// the passed-in Simulation" purity contract.
//
// Menu:  Strategos > Probe Campaign Carry Over
// Batch: -executeMethod Strategos.Editor.CampaignCarryOverProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Campaigns;
using Strategos.Commands;
using Strategos.NatoSymbols;
using Strategos.Objectives;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class CampaignCarryOverProbe
    {
        [MenuItem("Strategos/Probe Campaign Carry Over")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckDestroyedUnitExcludedAndReadinessRecovers(log);
            bad += CheckOutcomeWon(log);
            bad += CheckOutcomeLost(log);
            bad += CheckOutcomeDrew(log);
            bad += CheckUndecidedThrows(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[CampaignCarryOverProbe]\n" + log);
            else Debug.LogError("[CampaignCarryOverProbe]\n" + log);
        }

        // ─── Fixture ──────────────────────────────────────────────────────────

        /// <summary>
        /// One attacker, one victim, on the shipped skirmish ground, with a single
        /// low-threshold DestroyEnemy condition so the duel ends decisively as soon as the
        /// victim is broken — nothing about ground or the shipped conditions is under test
        /// here, only carry-over.
        /// </summary>
        private static Simulation NewDuel(SideId attackerSide, SideId victimSide,
            SideId playerSide, out UnitInstance attacker, out UnitInstance victim,
            out Scenario scenario)
        {
            scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var map = scenario.GenerateMap();

            attacker = new UnitInstance(new UnitId(1), attackerSide, SIDCCode.Empty.Raw,
                new Vector2(120f, 124f), "ATTACKER", "TEST", 100f, UnitCatalogue.Armor);
            victim = new UnitInstance(new UnitId(2), victimSide, SIDCCode.Empty.Raw,
                new Vector2(126f, 124f), "VICTIM", "TEST", 100f, UnitCatalogue.InfantryMotor);

            scenario.Units.Clear();
            scenario.Units.Add(attacker);
            scenario.Units.Add(victim);
            scenario.PlayerSide = playerSide;

            scenario.Objectives.Clear();
            scenario.Victory.Clear();
            scenario.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.DestroyEnemy, Side = attackerSide, Priority = 1,
                StrengthThresholdPercent = 5f, Description = "carry-over probe",
            });

            var sim = new Simulation(scenario, map, UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            return sim;
        }

        /// <summary>Steps until the victim is a wreck and the sim has decided, or gives up.</summary>
        private static bool RunToDecision(Simulation sim, UnitInstance attacker, UnitInstance victim)
        {
            sim.Issue(Command.Engage(ActorId.ForSide(attacker.Side), attacker.Id, victim.Id));

            for (int t = 0; t < 600 && !(victim.IsDestroyed && sim.IsOver); t++) sim.Step();

            return victim.IsDestroyed && sim.IsOver;
        }

        // ─── The main check: exclusion + exact readiness math + purity ──────────

        private static int CheckDestroyedUnitExcludedAndReadinessRecovers(StringBuilder log)
        {
            int bad = 0;
            // Deliberately small: the duel only costs the attacker a couple of readiness
            // points (a few minutes of engagement), so a large restHours would recover past
            // 100 and clamp — at which point almost any positive-but-wrong rate would still
            // land on the right answer by accident. A small, unclamped recovery is the only
            // value that actually exercises the arithmetic rather than just the floor.
            const float restHours = 0.1f;

            var sim = NewDuel(new SideId(1), new SideId(2), new SideId(1),
                out var attacker, out var victim, out _);

            if (!RunToDecision(sim, attacker, victim))
            {
                log.AppendLine("  FAIL fixture never reached a decided outcome with a wreck — " +
                               "the probe is not testing what it thinks");
                return bad + 1;
            }

            float attackerReadinessBeforeCarryOver = attacker.Readiness;
            float rate = sim.Catalogue.Get(UnitCatalogue.Armor).Fatigue.RecoveryPerHourResting;
            float expectedReadiness = Mathf.Min(100f,
                attackerReadinessBeforeCarryOver + rate * restHours);

            string signatureBefore = sim.Signature();

            var finished = new CampaignChainEntry();
            var next = new CampaignChainEntry();
            CampaignCarryOver.CarryOver(sim, finished, next, restHours);

            // Purity: the Simulation passed in must be untouched.
            string signatureAfter = sim.Signature();
            if (signatureAfter != signatureBefore)
            {
                log.AppendLine("  FAIL CarryOver mutated the Simulation passed to it — " +
                               "signature changed");
                bad++;
            }
            if (!Mathf.Approximately(attacker.Readiness, attackerReadinessBeforeCarryOver))
            {
                log.AppendLine($"  FAIL CarryOver mutated the live attacker's Readiness in " +
                               $"place: {attackerReadinessBeforeCarryOver:0.00} -> " +
                               $"{attacker.Readiness:0.00}");
                bad++;
            }

            // Wreck exclusion.
            bool victimCarried = false;
            UnitInstance carriedAttacker = null;
            foreach (var u in next.CarriedOverUnits)
            {
                if (u.Id == victim.Id) victimCarried = true;
                if (u.Id == attacker.Id) carriedAttacker = u;
            }

            if (victimCarried)
            {
                log.AppendLine("  FAIL the destroyed unit is present in CarriedOverUnits — " +
                               "a wreck walked into the next operation");
                bad++;
            }

            if (carriedAttacker == null)
            {
                log.AppendLine("  FAIL the surviving attacker is missing from CarriedOverUnits");
                return bad + 1;
            }

            if (next.CarriedOverUnits.Count != 1)
            {
                log.AppendLine($"  FAIL CarriedOverUnits has {next.CarriedOverUnits.Count} " +
                               "unit(s), expected exactly 1 (the survivor)");
                bad++;
            }

            // Strength carries over as-is, unchanged.
            if (!Mathf.Approximately(carriedAttacker.Strength, attacker.Strength))
            {
                log.AppendLine($"  FAIL survivor Strength changed: {attacker.Strength:0.00} -> " +
                               $"{carriedAttacker.Strength:0.00}");
                bad++;
            }

            // Readiness: the exact number, not just "it moved".
            if (!Mathf.Approximately(carriedAttacker.Readiness, expectedReadiness))
            {
                log.AppendLine($"  FAIL survivor Readiness {carriedAttacker.Readiness:0.000} " +
                               $"!= expected {expectedReadiness:0.000} " +
                               $"(from {attackerReadinessBeforeCarryOver:0.000} + " +
                               $"{rate:0.000}/h * {restHours}h, clamped at 100)");
                bad++;
            }

            if (carriedAttacker.Posture != Posture.Halted)
            {
                log.AppendLine($"  FAIL survivor Posture is {carriedAttacker.Posture}, expected " +
                               "Halted");
                bad++;
            }

            if (carriedAttacker.Cell != Vector2.zero)
            {
                log.AppendLine($"  FAIL survivor Cell is {carriedAttacker.Cell}, expected " +
                               "Vector2.zero (the ended map's position is meaningless on the " +
                               "next one)");
                bad++;
            }

            if (bad == 0)
                log.AppendLine($"  carry-over: victim (wreck) excluded, survivor carried at " +
                               $"{carriedAttacker.Strength:0}% strength, readiness " +
                               $"{attackerReadinessBeforeCarryOver:0.0} -> " +
                               $"{carriedAttacker.Readiness:0.0} over {restHours}h rest " +
                               $"(rate {rate:0.0}/h), Posture Halted, Cell reset, " +
                               "Simulation untouched");

            // Separately, the clamp: a long enough rest must land exactly on 100, not overshoot.
            var clampNext = new CampaignChainEntry();
            CampaignCarryOver.CarryOver(sim, new CampaignChainEntry(), clampNext, 1000f);
            float clampedReadiness = clampNext.CarriedOverUnits[0].Readiness;
            if (!Mathf.Approximately(clampedReadiness, 100f))
            {
                log.AppendLine($"  FAIL 1000h of rest gave {clampedReadiness:0.000}, expected " +
                               "exactly 100 (clamped)");
                bad++;
            }
            else
            {
                log.AppendLine("  clamp: 1000h of rest lands exactly on 100, does not overshoot  ok");
            }

            return bad;
        }

        // ─── Outcome mapping ──────────────────────────────────────────────────

        private static int CheckOutcomeWon(StringBuilder log)
        {
            int bad = 0;
            var sim = NewDuel(new SideId(1), new SideId(2), new SideId(1),
                out var attacker, out var victim, out _);

            if (!RunToDecision(sim, attacker, victim))
            {
                log.AppendLine("  FAIL 'won' fixture never decided");
                return bad + 1;
            }

            var finished = new CampaignChainEntry();
            CampaignCarryOver.CarryOver(sim, finished, new CampaignChainEntry(), 8f);

            if (finished.Outcome != OperationOutcome.Won)
            {
                log.AppendLine($"  FAIL expected Won (attacker's side == PlayerSide), got " +
                               $"{finished.Outcome} ({sim.Victory.Outcome})");
                bad++;
            }
            else
            {
                log.AppendLine($"  outcome: attacker's side == PlayerSide, decided " +
                               $"{sim.Victory.Outcome} -> Won  ok");
            }

            return bad;
        }

        private static int CheckOutcomeLost(StringBuilder log)
        {
            int bad = 0;
            // Attacker is OPFOR (side 2), victim is BLUFOR (side 1) and PlayerSide is BLUFOR —
            // so the player's own side is the one destroyed, and the winner is the other side.
            var sim = NewDuel(new SideId(2), new SideId(1), new SideId(1),
                out var attacker, out var victim, out _);

            if (!RunToDecision(sim, attacker, victim))
            {
                log.AppendLine("  FAIL 'lost' fixture never decided");
                return bad + 1;
            }

            var finished = new CampaignChainEntry();
            CampaignCarryOver.CarryOver(sim, finished, new CampaignChainEntry(), 8f);

            if (finished.Outcome != OperationOutcome.Lost)
            {
                log.AppendLine($"  FAIL expected Lost (winner != PlayerSide), got " +
                               $"{finished.Outcome} ({sim.Victory.Outcome})");
                bad++;
            }
            else
            {
                log.AppendLine($"  outcome: winner != PlayerSide, decided " +
                               $"{sim.Victory.Outcome} -> Lost  ok");
            }

            return bad;
        }

        private static int CheckOutcomeDrew(StringBuilder log)
        {
            int bad = 0;

            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var map = scenario.GenerateMap();

            var blue = new UnitInstance(new UnitId(1), new SideId(1), SIDCCode.Empty.Raw,
                new Vector2(120f, 124f), "BLUE", "TEST", 100f, UnitCatalogue.InfantryMech);
            var red = new UnitInstance(new UnitId(2), new SideId(2), SIDCCode.Empty.Raw,
                new Vector2(220f, 124f), "RED", "TEST", 100f, UnitCatalogue.InfantryMech);

            scenario.Units.Clear();
            scenario.Units.Add(blue);
            scenario.Units.Add(red);
            scenario.PlayerSide = blue.Side;

            scenario.Objectives.Clear();
            scenario.Objectives.Add(new Objective
            {
                Id = 1, Name = "TEST", Cell = new Vector2(170f, 124f),
                RadiusCells = 5f, InitialOwner = SideId.None,
            });
            scenario.Victory.Clear();
            scenario.Victory.Add(new VictoryCondition
            {
                // Unreachable in the time given: the two units never move or fight, so
                // nobody ever holds the objective, and the scenario can only end by timeout.
                Kind = VictoryKind.HoldObjectives, Side = blue.Side,
                ObjectiveIds = new[] { 1 }, HoldTicks = 100000,
                Description = "unreachable",
            });
            scenario.TimeLimitTicks = 40;

            var sim = new Simulation(scenario, map, UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());

            for (int t = 0; t < 100 && !sim.IsOver; t++) sim.Step();

            if (!sim.IsOver || !sim.Victory.Outcome.IsDraw)
            {
                log.AppendLine($"  FAIL 'drew' fixture did not produce a draw: " +
                               $"{sim.Victory?.Outcome}");
                return bad + 1;
            }

            var finished = new CampaignChainEntry();
            CampaignCarryOver.CarryOver(sim, finished, new CampaignChainEntry(), 8f);

            if (finished.Outcome != OperationOutcome.Drew)
            {
                log.AppendLine($"  FAIL expected Drew, got {finished.Outcome} " +
                               $"({sim.Victory.Outcome})");
                bad++;
            }
            else
            {
                log.AppendLine($"  outcome: timeout with nobody deciding, " +
                               $"{sim.Victory.Outcome} -> Drew  ok");
            }

            return bad;
        }

        // ─── Caller error ─────────────────────────────────────────────────────

        private static int CheckUndecidedThrows(StringBuilder log)
        {
            int bad = 0;

            // No objectives, no victory conditions at all: Simulation.Victory is null.
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;
            var map = scenario.GenerateMap();
            scenario.Objectives.Clear();
            scenario.Victory.Clear();
            var simNoVictory = new Simulation(scenario, map, UnitCatalogue.Default());

            bool threwForNullVictory = false;
            try
            {
                CampaignCarryOver.CarryOver(simNoVictory, new CampaignChainEntry(),
                    new CampaignChainEntry(), 8f);
            }
            catch (System.InvalidOperationException) { threwForNullVictory = true; }

            if (!threwForNullVictory)
            {
                log.AppendLine("  FAIL a Simulation with no VictoryEvaluator did not throw");
                bad++;
            }

            // Victory conditions exist, but nothing has been decided yet (Tick 0).
            var sim = NewDuel(new SideId(1), new SideId(2), new SideId(1),
                out _, out _, out _);

            bool threwForUndecided = false;
            try
            {
                CampaignCarryOver.CarryOver(sim, new CampaignChainEntry(),
                    new CampaignChainEntry(), 8f);
            }
            catch (System.InvalidOperationException) { threwForUndecided = true; }

            if (!threwForUndecided)
            {
                log.AppendLine("  FAIL an undecided Simulation (t0, VictoryEvaluator present " +
                               "but not decided) did not throw");
                bad++;
            }

            if (bad == 0)
                log.AppendLine("  caller error: both 'no evaluator' and 'undecided evaluator' " +
                               "throw InvalidOperationException rather than guessing an outcome");

            return bad;
        }
    }
}
#endif
