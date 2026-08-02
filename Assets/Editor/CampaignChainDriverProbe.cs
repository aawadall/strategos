// CampaignChainDriverProbe.cs
// #75 chunk 3: CampaignChainDriver's own probe. Proves a REAL two-operation round trip — not a
// unit test of MergeCarriedOver in isolation — by playing operation 0 unattended to a decision,
// carrying its survivors over, playing operation 1 (built by merging those survivors into its
// own authored scenario) unattended to a decision too, and asserting the merged Simulation's
// units actually reflect the merge rule: carried Strength/Readiness/Training/Roe/Supply for a
// matched Id, the new scenario's own authored Cell/ParentId for every unit including
// reinforcements. Also proves the "a defeat gets less rest" driver decision (folded into this
// chunk per its brief) and that an unmatched carried-over unit is a surfaced authoring error,
// never a silent drop.
//
// Reuses DirectorProbe's unattended-stepping pattern exactly (EnableErosion off where it is
// this probe's own fixture to control, MoveToExecutor + EngageExecutor, EnableReactions,
// EnableDirector, FireAtWill, step until IsOver) rather than reinventing it.
//
// Menu:  Strategos > Probe Campaign Chain Driver
// Batch: -executeMethod Strategos.Editor.CampaignChainDriverProbe.Run

#if UNITY_EDITOR
using System.Collections.Generic;
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
    public static class CampaignChainDriverProbe
    {
        [MenuItem("Strategos/Probe Campaign Chain Driver")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckTwoOperationRoundTrip(log);
            bad += CheckRestHoursByOutcome(log);
            bad += CheckUnmatchedCarriedOverUnitThrows(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[CampaignChainDriverProbe]\n" + log);
            else Debug.LogError("[CampaignChainDriverProbe]\n" + log);
        }

        // ─── Shared: DirectorProbe's unattended-stepping pattern, reused verbatim ────

        /// <summary>
        /// Wires executors, reactions and a director onto every side, sets FireAtWill so a
        /// meeting engagement is actually fought, then steps until the simulation decides or
        /// gives up. Exactly <see cref="DirectorProbe"/>'s <c>NewUnattended</c> /
        /// <c>CheckUnattendedScenarioDecides</c> shape, generalised to run on a
        /// <see cref="Simulation"/> <see cref="CampaignChainDriver.StartNext"/> already built
        /// rather than constructing one itself.
        /// </summary>
        private static bool RunToDecision(Simulation sim)
        {
            var scenario = sim.Scenario;
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.EnableReactions();

            foreach (var u in scenario.Units) u.Roe = RulesOfEngagement.FireAtWill;

            var sides = new List<SideId>();
            foreach (var side in scenario.Sides) sides.Add(side.Id);
            sim.EnableDirector(sides);

            int limit = scenario.TimeLimitTicks > 0 ? scenario.TimeLimitTicks + 60 : 4000;
            while (sim.Tick < limit && !sim.IsOver) sim.Step();

            return sim.IsOver;
        }

        // ─── 1. The real two-operation round trip ────────────────────────────

        private static int CheckTwoOperationRoundTrip(StringBuilder log)
        {
            int bad = 0;

            var chain = new CampaignChain { Name = "Valley Campaign — Two Operations" };
            chain.Operations.Add(new CampaignChainEntry
            {
                ScenarioName = ScenarioSamples.SkirmishName, Name = "Opening Contact",
            });
            chain.Operations.Add(new CampaignChainEntry
            {
                ScenarioName = ScenarioSamples.PushNorthName, Name = "Push North",
            });

            // ── Operation 0: skirmish, as authored, no merge (first entry). ──
            var sim0 = CampaignChainDriver.StartNext(chain, 0, UnitCatalogue.Default());

            if (sim0.Scenario.Units.Count == 0 || sim0.Units.Count == 0)
            {
                log.AppendLine("  FAIL operation 0's Simulation has no units — StartNext did " +
                               "not really load the skirmish scenario");
                return bad + 1;
            }

            // Snapshot pre-merge state for the assertions below: every leaf's Strength,
            // Readiness, Training, Roe, Supply going into carry-over, keyed by Id.
            var preCarryStrength = new Dictionary<int, float>();
            var preCarryReadinessBeforeRest = new Dictionary<int, float>();
            var preCarryTraining = new Dictionary<int, float>();
            var preCarryRoe = new Dictionary<int, RulesOfEngagement>();
            var preCarrySupply = new Dictionary<int, SupplyLevels>();

            if (!RunToDecision(sim0))
            {
                log.AppendLine($"  FAIL operation 0 (skirmish) never reached a decision after " +
                               $"{sim0.Tick} ticks");
                return bad + 1;
            }

            log.AppendLine($"  operation 0 (skirmish) decided: {sim0.Victory.Outcome} at " +
                            $"t{sim0.Tick}, {sim0.Director.OrdersIssued} autonomous order(s)");

            foreach (var u in sim0.Units)
            {
                preCarryStrength[u.Id.Value] = u.Strength;
                preCarryReadinessBeforeRest[u.Id.Value] = u.Readiness;
                preCarryTraining[u.Id.Value] = u.Training;
                preCarryRoe[u.Id.Value] = u.Roe;
                preCarrySupply[u.Id.Value] = u.Supply;
            }

            // ── Carry-over: operation 0's survivors -> operation 1's CarriedOverUnits. ──
            const float restHours = 6f;
            CampaignCarryOver.CarryOver(sim0, chain.Operations[0], chain.Operations[1], restHours);

            if (chain.Operations[1].CarriedOverUnits.Count == 0)
            {
                log.AppendLine("  FAIL operation 0 produced zero survivors — the round trip " +
                               "below cannot prove anything about merged state. Widen " +
                               "push-north's DestroyEnemy threshold or skirmish's fixture.");
                return bad + 1;
            }

            log.AppendLine($"  carried over {chain.Operations[1].CarriedOverUnits.Count} " +
                            $"survivor(s) into operation 1 with {restHours}h rest");

            // ── Operation 1: push-north, merged with operation 0's survivors. ──
            var sim1 = CampaignChainDriver.StartNext(chain, 1, UnitCatalogue.Default());
            var pushNorthAuthored = ScenarioSamples.PushNorth();   // for the authored baseline

            int matchedChecked = 0, reinforcementChecked = 0;
            bool anyMatchedUnitDivergedFromAuthoredDefault = false;

            foreach (var authoredUnit in pushNorthAuthored.Units)
            {
                var mergedUnit = sim1.Scenario.FindUnit(authoredUnit.Id);
                if (mergedUnit == null)
                {
                    log.AppendLine($"  FAIL operation 1's merged scenario is missing unit " +
                                   $"{authoredUnit.Id}, which push-north authors");
                    bad++;
                    continue;
                }

                // Cell and ParentId must ALWAYS be push-north's own authored values — for
                // every unit, matched or not — never CarriedOverUnits' Vector2.zero sentinel
                // and never operation 0's ParentId (7 for BLUFOR leaves, 8 for OPFOR's).
                if (mergedUnit.Cell != authoredUnit.Cell)
                {
                    log.AppendLine($"  FAIL unit {authoredUnit.Id} Cell is {mergedUnit.Cell}, " +
                                   $"expected push-north's own authored {authoredUnit.Cell} " +
                                   "(not the carried-over Vector2.zero sentinel)");
                    bad++;
                }
                if (mergedUnit.ParentId != authoredUnit.ParentId)
                {
                    log.AppendLine($"  FAIL unit {authoredUnit.Id} ParentId is " +
                                   $"{mergedUnit.ParentId}, expected push-north's own authored " +
                                   $"{authoredUnit.ParentId}");
                    bad++;
                }

                bool wasCarried = preCarryStrength.ContainsKey(authoredUnit.Id.Value) &&
                    !IsExcludedWreck(sim0, authoredUnit.Id);

                if (wasCarried)
                {
                    // Matched: Strength/Training/Roe/Supply carry unchanged; Readiness is
                    // pre-carry readiness plus restHours of FatigueModel recovery (exact
                    // arithmetic already proven by CampaignCarryOverProbe — here it is enough
                    // to confirm the MERGE actually applied the carried value, not that the
                    // recovery formula itself is right).
                    matchedChecked++;

                    if (!Mathf.Approximately(mergedUnit.Strength, preCarryStrength[authoredUnit.Id.Value]))
                    {
                        log.AppendLine($"  FAIL unit {authoredUnit.Id} merged Strength " +
                                       $"{mergedUnit.Strength:0.0} != carried " +
                                       $"{preCarryStrength[authoredUnit.Id.Value]:0.0}");
                        bad++;
                    }
                    if (mergedUnit.Readiness <= preCarryReadinessBeforeRest[authoredUnit.Id.Value] - 0.001f)
                    {
                        log.AppendLine($"  FAIL unit {authoredUnit.Id} merged Readiness " +
                                       $"{mergedUnit.Readiness:0.0} did not recover past its " +
                                       $"pre-rest {preCarryReadinessBeforeRest[authoredUnit.Id.Value]:0.0}");
                        bad++;
                    }
                    if (mergedUnit.Roe != preCarryRoe[authoredUnit.Id.Value])
                    {
                        log.AppendLine($"  FAIL unit {authoredUnit.Id} merged Roe " +
                                       $"{mergedUnit.Roe} != carried {preCarryRoe[authoredUnit.Id.Value]}");
                        bad++;
                    }
                    if (!Mathf.Approximately(mergedUnit.Training, preCarryTraining[authoredUnit.Id.Value]))
                    {
                        log.AppendLine($"  FAIL unit {authoredUnit.Id} merged Training " +
                                       $"{mergedUnit.Training:0.0} != carried " +
                                       $"{preCarryTraining[authoredUnit.Id.Value]:0.0}");
                        bad++;
                    }
                    var carriedSupply = preCarrySupply[authoredUnit.Id.Value];
                    if (!Mathf.Approximately(mergedUnit.Supply.Rations, carriedSupply.Rations) ||
                        !Mathf.Approximately(mergedUnit.Supply.Water, carriedSupply.Water) ||
                        !Mathf.Approximately(mergedUnit.Supply.Ammunition, carriedSupply.Ammunition) ||
                        !Mathf.Approximately(mergedUnit.Supply.Fuel, carriedSupply.Fuel))
                    {
                        log.AppendLine($"  FAIL unit {authoredUnit.Id} merged Supply != carried Supply");
                        bad++;
                    }
                    if (!Mathf.Approximately(mergedUnit.Strength, authoredUnit.Strength) ||
                        !Mathf.Approximately(mergedUnit.Readiness, authoredUnit.Readiness))
                        anyMatchedUnitDivergedFromAuthoredDefault = true;
                }
                else
                {
                    // Reinforcement (id 9) or an authored id-1..6 unit that operation 0 did not
                    // hand back a survivor for (destroyed, or never fielded): keeps its own
                    // authored state entirely.
                    reinforcementChecked++;

                    if (!Mathf.Approximately(mergedUnit.Strength, authoredUnit.Strength))
                    {
                        log.AppendLine($"  FAIL unmatched unit {authoredUnit.Id} Strength " +
                                       $"{mergedUnit.Strength:0.0} != its own authored " +
                                       $"{authoredUnit.Strength:0.0} — a reinforcement must keep " +
                                       "its own state untouched");
                        bad++;
                    }
                    if (!Mathf.Approximately(mergedUnit.Readiness, authoredUnit.Readiness))
                    {
                        log.AppendLine($"  FAIL unmatched unit {authoredUnit.Id} Readiness " +
                                       $"{mergedUnit.Readiness:0.0} != its own authored " +
                                       $"{authoredUnit.Readiness:0.0}");
                        bad++;
                    }
                }
            }

            if (matchedChecked == 0)
            {
                log.AppendLine("  FAIL no push-north unit matched a survivor — the merge path " +
                               "was never actually exercised");
                bad++;
            }
            if (reinforcementChecked == 0)
            {
                log.AppendLine("  FAIL no unmatched/reinforcement unit was checked — the " +
                               "\"keeps its own state\" half of the rule was never exercised");
                bad++;
            }

            // Guard against a vacuous pass: push-north's authored Strength/Readiness on units
            // 1-6 are the untouched scenario defaults (100/100), so if every matched survivor
            // also happened to sit at 100/100 (never engaged), the equality checks above would
            // pass whether or not the merge actually ran. skirmish's unattended duel is a real
            // meeting engagement, so at least one matched survivor must show combat's mark.
            if (matchedChecked > 0 && !anyMatchedUnitDivergedFromAuthoredDefault)
            {
                log.AppendLine("  FAIL every matched survivor sits at push-north's authored " +
                               "default (100/100) — this run cannot tell a real merge apart " +
                               "from one that silently did nothing");
                bad++;
            }

            if (bad == 0)
                log.AppendLine($"  merge: {matchedChecked} matched unit(s) carried " +
                                $"Strength/Readiness/Training/Roe/Supply, " +
                                $"{reinforcementChecked} unmatched unit(s) kept their own " +
                                "authored state, every unit's Cell/ParentId came from " +
                                "push-north's own authoring — not the carried sentinel");

            // ── Operation 1 must also actually be a live, playable Simulation — run it to a
            // decision too, proving this is a real round trip and not merged data nobody plays.
            if (!RunToDecision(sim1))
            {
                log.AppendLine($"  FAIL operation 1 (push-north, merged) never reached a " +
                               $"decision after {sim1.Tick} ticks");
                bad++;
            }
            else
            {
                log.AppendLine($"  operation 1 (push-north, merged) decided: " +
                                $"{sim1.Victory.Outcome} at t{sim1.Tick}, " +
                                $"{sim1.Director.OrdersIssued} autonomous order(s)");
            }

            return bad;
        }

        private static bool IsExcludedWreck(Simulation endedSim, UnitId id)
        {
            foreach (var u in endedSim.Units)
                if (u.Id == id) return u.IsDestroyed;
            return true;   // not in Units at all (e.g. a formation) -> never a leaf survivor
        }

        // ─── 2. Rest-hours-by-outcome: the whole "defeat cost" demonstration ──

        /// <summary>
        /// The folded-in "defeat gets less rest" decision: <c>CarryOver</c> already takes
        /// <c>restHours</c> as a caller-supplied parameter (chunk 2), so this is a value the
        /// *driver* chooses, not new logic. A Won duel gets a longer rest than a Lost duel
        /// (same geometry, sides swapped so the player's own side is the one destroyed) —
        /// asserts the surviving attacker's carried Readiness is higher after the longer rest.
        /// </summary>
        /// <remarks>
        /// The rest-hours pair is computed from the duel's own measured readiness deficit
        /// rather than fixed constants: CampaignCarryOverProbe already found a duel this short
        /// only costs the attacker a couple of readiness points, so a naive fixed 8h/1h pair
        /// both fully recover past the FatigueModel clamp at 100 — which passed on this probe's
        /// first run for the wrong reason (both landing on exactly 100, not because the longer
        /// rest recovered more). Scaling both to fractions of the actual deficit keeps the
        /// comparison inside the clamp, the same reasoning CampaignCarryOverProbe's own
        /// restHours=0.1f choice documents.
        /// </remarks>
        private static int CheckRestHoursByOutcome(StringBuilder log)
        {
            int bad = 0;

            var wonSim = NewDuel(new SideId(1), new SideId(2), new SideId(1),
                out var wonAttacker, out var wonVictim);
            var lostSim = NewDuel(new SideId(2), new SideId(1), new SideId(1),
                out var lostAttacker, out var lostVictim);

            if (!RunDuelToDecision(wonSim, wonAttacker, wonVictim) ||
                !RunDuelToDecision(lostSim, lostAttacker, lostVictim))
            {
                log.AppendLine("  FAIL one of the rest-hours fixtures never decided");
                return bad + 1;
            }

            float rate = wonSim.Catalogue.Get(UnitCatalogue.Armor).Fatigue.RecoveryPerHourResting;
            float wonDeficit = 100f - wonAttacker.Readiness;
            float lostDeficit = 100f - lostAttacker.Readiness;

            if (wonDeficit < 0.01f || lostDeficit < 0.01f)
            {
                log.AppendLine($"  FAIL fixture took no readiness damage (won deficit " +
                               $"{wonDeficit:0.000}, lost deficit {lostDeficit:0.000}) — cannot " +
                               "demonstrate a rest difference with nothing to recover from");
                return bad + 1;
            }

            // Longer rest recovers most (80%) of what was lost; shorter rest recovers a tenth
            // of that — both strictly below the 100 clamp, so the comparison below actually
            // exercises the arithmetic instead of two runs both landing on the ceiling.
            float wonRestHours = wonDeficit / rate * 0.8f;
            float lostRestHours = lostDeficit / rate * 0.08f;

            var wonFinished = new CampaignChainEntry();
            var wonNext = new CampaignChainEntry();
            CampaignCarryOver.CarryOver(wonSim, wonFinished, wonNext, wonRestHours);

            var lostFinished = new CampaignChainEntry();
            var lostNext = new CampaignChainEntry();
            CampaignCarryOver.CarryOver(lostSim, lostFinished, lostNext, lostRestHours);

            if (wonFinished.Outcome != OperationOutcome.Won)
            {
                log.AppendLine($"  FAIL 'won' fixture produced {wonFinished.Outcome}, not Won");
                bad++;
            }
            if (lostFinished.Outcome != OperationOutcome.Lost)
            {
                log.AppendLine($"  FAIL 'lost' fixture produced {lostFinished.Outcome}, not Lost");
                bad++;
            }
            if (bad > 0) return bad;

            float wonReadiness = wonNext.CarriedOverUnits[0].Readiness;
            float lostReadiness = lostNext.CarriedOverUnits[0].Readiness;

            // Same duel geometry mirrored across sides, so the pre-rest readiness cost of the
            // fight itself is the same either way — the only thing that should differ is the
            // rest, and a longer rest must land higher.
            if (wonReadiness <= lostReadiness)
            {
                log.AppendLine($"  FAIL Won ({wonRestHours:0.000}h rest) survivor Readiness " +
                               $"{wonReadiness:0.00} did not end up higher than Lost " +
                               $"({lostRestHours:0.000}h rest) survivor Readiness {lostReadiness:0.00}");
                bad++;
            }
            else
            {
                log.AppendLine($"  rest-by-outcome: Won ({wonRestHours:0.000}h) -> " +
                               $"{wonReadiness:0.00} readiness, Lost ({lostRestHours:0.000}h) -> " +
                               $"{lostReadiness:0.00} — a defeat costs the campaign more rest, " +
                               "demonstrated as a driver-chosen restHours value, no new logic " +
                               "in CampaignCarryOver itself");
            }

            return bad;
        }

        /// <summary>One attacker, one victim, on skirmish ground — same shape as
        /// CampaignCarryOverProbe's NewDuel, kept local rather than shared across two probe
        /// files for one fixture each uses slightly differently.</summary>
        private static Simulation NewDuel(SideId attackerSide, SideId victimSide,
            SideId playerSide, out UnitInstance attacker, out UnitInstance victim)
        {
            var scenario = ScenarioSamples.Skirmish();
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
                StrengthThresholdPercent = 5f, Description = "campaign chain driver probe",
            });

            var sim = new Simulation(scenario, map, UnitCatalogue.Default());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            return sim;
        }

        private static bool RunDuelToDecision(Simulation sim, UnitInstance attacker, UnitInstance victim)
        {
            sim.Issue(Command.Engage(ActorId.ForSide(attacker.Side), attacker.Id, victim.Id));
            for (int t = 0; t < 600 && !(victim.IsDestroyed && sim.IsOver); t++) sim.Step();
            return victim.IsDestroyed && sim.IsOver;
        }

        // ─── 3. Unmatched carried-over unit: surfaced, never dropped ─────────

        private static int CheckUnmatchedCarriedOverUnitThrows(StringBuilder log)
        {
            int bad = 0;
            var nextScenario = ScenarioSamples.PushNorth();

            // A unit id nothing in push-north authors at all.
            var stray = new UnitInstance(new UnitId(999), new SideId(1), SIDCCode.Empty.Raw,
                Vector2.zero, "GHOST COMPANY", "TEST", 80f, UnitCatalogue.InfantryMech);

            bool threw = false;
            string message = null;
            try
            {
                CampaignChainDriver.MergeCarriedOver(nextScenario, new List<UnitInstance> { stray });
            }
            catch (System.InvalidOperationException ex)
            {
                threw = true;
                message = ex.Message;
            }

            if (!threw)
            {
                log.AppendLine("  FAIL a carried-over unit with no match in the next scenario " +
                               "was silently dropped instead of surfaced");
                return bad + 1;
            }

            if (message == null || message.IndexOf("999", System.StringComparison.Ordinal) < 0)
            {
                log.AppendLine($"  FAIL threw, but the message does not name the unmatched " +
                               $"unit: \"{message}\"");
                bad++;
            }

            // Now the clean path: the same stray id ALSO present in the next scenario (as a
            // reinforcement authored under the same id) must merge without throwing.
            nextScenario.Units.Add(new UnitInstance(new UnitId(999), new SideId(1),
                SIDCCode.Empty.Raw, new Vector2(10f, 10f), "GHOST COMPANY", "TEST", 100f,
                UnitCatalogue.InfantryMech));

            Scenario merged = null;
            bool threwWhenMatched = false;
            try
            {
                merged = CampaignChainDriver.MergeCarriedOver(nextScenario,
                    new List<UnitInstance> { stray });
            }
            catch (System.InvalidOperationException) { threwWhenMatched = true; }

            if (threwWhenMatched || merged == null)
            {
                log.AppendLine("  FAIL adding a matching authored unit did not clear the " +
                               "authoring error — MergeCarriedOver still threw");
                bad++;
            }
            else if (bad == 0)
            {
                log.AppendLine("  authoring error: an unmatched carried-over unit (999) " +
                                $"throws InvalidOperationException naming it (\"...999...\"); " +
                                "adding a matching authored unit clears it — RED then GREEN");
            }

            return bad;
        }
    }
}
#endif
