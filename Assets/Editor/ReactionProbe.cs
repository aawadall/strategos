// ReactionProbe.cs
// Verifies autonomous reaction headlessly: who fires, under which rules of engagement, and
// when they stop.
//
// These are worth more as regression tests than most of the project's probes. Reaction bugs
// are the kind that only ever surface as "it felt wrong" — a unit that answers fire three
// minutes late, or one that keeps fighting when it should have withdrawn, looks like a
// balance complaint rather than a defect. An assertion catches it on the tick.
//
// Menu:  Strategos > Probe Reactions
// Batch: -executeMethod Strategos.Editor.ReactionProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Maps;
using Strategos.Reactions;
using Strategos.Reports;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class ReactionProbe
    {
        private static ActorId Blue => new(1);
        private static ActorId Red => new(2);

        [MenuItem("Strategos/Probe Reactions")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckHoldFireNeverInitiates(log);
            bad += CheckReturnFireOnlyAnswers(log);
            bad += CheckFireAtWillInitiates(log);
            bad += CheckReflexInterruptsAMarch(log);
            bad += CheckBreakContact(log);
            bad += CheckMutualReactionIsFair(log);
            bad += CheckDeterminism(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[ReactionProbe]\n" + log);
            else Debug.LogError("[ReactionProbe]\n" + log);
        }

        // ─── Fixtures ─────────────────────────────────────────────────────────

        private static MapData _map;
        private static MapData Map()
        {
            if (_map != null) return _map;
            var s = ScenarioSamples.Skirmish();
            s.Map.EnableErosion = false;
            _map = s.GenerateMap();
            return _map;
        }

        /// <summary>
        /// Two units facing each other at a chosen separation, with reactions switched on.
        /// </summary>
        /// <remarks>
        /// Both are planted on cells of a known-good route rather than at the map centre. The
        /// centre of this map is a lake, and a unit standing in one cannot path anywhere — so
        /// every test involving movement failed for reasons that had nothing to do with
        /// reactions, and read as reaction bugs.
        /// </remarks>
        private static Simulation NewPair(out UnitInstance blue, out UnitInstance red,
            RulesOfEngagement blueRoe, RulesOfEngagement redRoe, float separationCells = 8f)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;

            var route = KnownGoodRoute();
            Vector2 bluePos = new(route[0].x, route[0].y);
            Vector2 redPos = bluePos + new Vector2(separationCells, 0f);

            for (int i = 0; i < route.Count; i++)
            {
                var candidate = new Vector2(route[i].x, route[i].y);
                if (Vector2.Distance(bluePos, candidate) < separationCells) continue;
                redPos = candidate;
                break;
            }

            blue = new UnitInstance(new UnitId(1), new SideId(1),
                Strategos.NatoSymbols.SIDCCode.Empty.Raw, bluePos, "BLUE", string.Empty, 100f,
                UnitCatalogue.InfantryMech) { Roe = blueRoe };

            red = new UnitInstance(new UnitId(2), new SideId(2),
                Strategos.NatoSymbols.SIDCCode.Empty.Raw, redPos,
                "RED", string.Empty, 100f, UnitCatalogue.InfantryMech) { Roe = redRoe };

            scenario.Units.Clear();
            scenario.Units.Add(blue);
            scenario.Units.Add(red);

            var sim = new Simulation(scenario, Map());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.EnableReactions();
            return sim;
        }

        /// <summary>
        /// The scenario's own start-to-finish route, whose cells are passable and reachable by
        /// construction — which the map centre is not, being a lake.
        /// </summary>
        private static System.Collections.Generic.List<Vector2Int> KnownGoodRoute()
        {
            var caps = UnitCatalogue.Default().Get(UnitCatalogue.InfantryMech);
            var grid = Strategos.Movement.MovementGrid.Build(Map(), caps);
            var found = Strategos.Movement.PathFinder.Find(grid,
                new Vector2Int(58, 72), new Vector2Int(180, 176));
            return found.Found ? found.Cells : null;
        }

        /// <summary>Tick on which a unit first opened fire, or −1.</summary>
        private static int FiredAt(Simulation sim, UnitId who)
        {
            foreach (var r in sim.ReportLog.Entries)
                if (r.Kind == ReportKind.Engaged && r.Source == who) return r.Tick;
            return -1;
        }

        // ─── Hold fire ────────────────────────────────────────────────────────

        private static int CheckHoldFireNeverInitiates(StringBuilder log)
        {
            int bad = 0;
            var sim = NewPair(out var blue, out var red,
                RulesOfEngagement.HoldFire, RulesOfEngagement.HoldFire);

            sim.Step(120);

            if (FiredAt(sim, blue.Id) >= 0 || FiredAt(sim, red.Id) >= 0)
            {
                log.AppendLine("  FAIL a unit on Hold Fire opened fire on its own");
                bad++;
            }

            // But a direct order must still be obeyed: ROE governs initiative, not permission.
            var order = sim.Issue(Command.Engage(Blue, blue.Id, red.Id));
            sim.Step(5);

            if (FiredAt(sim, blue.Id) < 0)
            {
                log.AppendLine("  FAIL a unit on Hold Fire refused an explicit engage order");
                bad++;
            }

            // And the unit it is shooting at, also on Hold Fire, must not shoot back.
            sim.Step(30);
            if (FiredAt(sim, red.Id) >= 0)
            {
                log.AppendLine("  FAIL a unit on Hold Fire returned fire");
                bad++;
            }

            if (bad == 0)
                log.AppendLine($"  hold fire: silent for 120 ticks, obeyed order #{order.Seq}, " +
                               "and the target still did not shoot back");
            return bad;
        }

        // ─── Return fire ──────────────────────────────────────────────────────

        private static int CheckReturnFireOnlyAnswers(StringBuilder log)
        {
            int bad = 0;
            var sim = NewPair(out var blue, out var red,
                RulesOfEngagement.ReturnFire, RulesOfEngagement.ReturnFire);

            // Neither has been shot at, so neither may start.
            sim.Step(120);

            if (FiredAt(sim, blue.Id) >= 0 || FiredAt(sim, red.Id) >= 0)
            {
                log.AppendLine("  FAIL Return Fire initiated a fight nobody started");
                return bad + 1;
            }

            // Now provoke one of them with an explicit order.
            sim.Issue(Command.Engage(Blue, blue.Id, red.Id));
            sim.Step(10);

            int blueFired = FiredAt(sim, blue.Id);
            int redFired = FiredAt(sim, red.Id);

            if (blueFired < 0)
            {
                log.AppendLine("  FAIL the ordered unit never fired");
                return bad + 1;
            }

            if (redFired < 0)
            {
                log.AppendLine("  FAIL a unit on Return Fire did not answer being shot at");
                bad++;
            }
            else if (redFired <= blueFired)
            {
                log.AppendLine($"  FAIL the answer (t{redFired}) came no later than the shot " +
                               $"that provoked it (t{blueFired})");
                bad++;
            }
            else if (redFired - blueFired > 4)
            {
                // Contact report at N, delivered N+1, order issued N+1, delivered N+2, fires
                // N+2. Anything much beyond that means a reaction is waiting on something.
                log.AppendLine($"  FAIL return fire took {redFired - blueFired} ticks to answer; " +
                               "a reflex should be two");
                bad++;
            }

            // The answer must be aimed at the right unit, from what it was told.
            var attackers = sim.Reactions.AttackersOf(red.Id);
            if (attackers.Count != 1 || attackers[0] != blue.Id)
            {
                log.AppendLine($"  FAIL the defender's attacker list holds {attackers.Count} " +
                               "entries and does not name the shooter");
                bad++;
            }

            if (bad == 0)
                log.AppendLine($"  return fire: silent until provoked, answered at t{redFired} " +
                               $"({redFired - blueFired} ticks after t{blueFired})");
            return bad;
        }

        // ─── Fire at will ─────────────────────────────────────────────────────

        private static int CheckFireAtWillInitiates(StringBuilder log)
        {
            int bad = 0;
            var sim = NewPair(out var blue, out var red,
                RulesOfEngagement.FireAtWill, RulesOfEngagement.HoldFire);

            sim.Step(30);

            int blueFired = FiredAt(sim, blue.Id);
            if (blueFired < 0)
            {
                log.AppendLine("  FAIL Fire At Will never initiated against a hostile in range");
                return bad + 1;
            }

            if (FiredAt(sim, red.Id) >= 0)
            {
                log.AppendLine("  FAIL the Hold Fire unit fired back");
                bad++;
            }

            // It must have learned about the enemy from a report, which means it cannot have
            // fired before one could have reached it: contact at t1, delivered t2, order
            // issued t2, delivered t3.
            if (blueFired < 3)
            {
                log.AppendLine($"  FAIL opened fire at t{blueFired}, sooner than a report could " +
                               "have arrived; something is reading world state");
                bad++;
            }

            var contacts = sim.Reactions.ContactsOf(blue.Id);
            if (contacts.Count != 1 || contacts[0] != red.Id)
            {
                log.AppendLine("  FAIL the shooter's picture does not name what it shot at");
                bad++;
            }

            // Out of engagement range it must hold, even though it can see that far.
            float envelope = UnitCatalogue.Default().Get(UnitCatalogue.InfantryMech)
                .EngagementRangeMetres / Map().Header.MetresPerCell;
            var far = NewPair(out var b2, out _, RulesOfEngagement.FireAtWill,
                RulesOfEngagement.HoldFire, envelope * 1.4f);
            far.Step(40);

            if (FiredAt(far, b2.Id) >= 0)
            {
                log.AppendLine("  FAIL Fire At Will engaged a contact outside its envelope");
                bad++;
            }

            if (bad == 0)
                log.AppendLine($"  fire at will: initiated at t{blueFired} from a report, and " +
                               "held its fire against a contact beyond range");
            return bad;
        }

        // ─── The reflex must interrupt, not queue ─────────────────────────────

        /// <summary>
        /// A unit fired on mid-march must shoot back now and resume the march afterwards.
        /// </summary>
        /// <remarks>
        /// Appended to the back of its queue instead, the reaction would be carried out after
        /// the march finished — which for a twenty-minute move means answering fire long after
        /// whoever shot at it has gone. That is the difference between a reflex and an item on
        /// a to-do list, and it is invisible unless something asserts it.
        /// </remarks>
        private static int CheckReflexInterruptsAMarch(StringBuilder log)
        {
            int bad = 0;

            // On the route, not at the map centre. The centre of this map is a lake, so a
            // march ordered from there fails to path and the unit never moves — which looks
            // exactly like the reflex bug this test is here to catch.
            var route = KnownGoodRoute();
            if (route == null || route.Count < 60)
            {
                log.AppendLine("  FAIL no route to march along");
                return bad + 1;
            }

            var sim = NewPair(out var blue, out var red,
                RulesOfEngagement.HoldFire, RulesOfEngagement.ReturnFire,
                separationCells: 0f);

            red.Cell = new Vector2(route[0].x, route[0].y);
            blue.Cell = red.Cell + new Vector2(6f, 0f);

            // Send RED on a long march down the route, then shoot at it.
            var destination = new Vector2(route[50].x, route[50].y);
            var march = sim.Issue(Command.MoveTo(Red, red.Id, destination));
            sim.Step(5);

            if (red.Posture != Posture.Moving)
            {
                log.AppendLine($"  FAIL the marching unit never started moving (posture " +
                               $"{red.Posture} at {red.Cell})");
                return bad + 1;
            }

            sim.Issue(Command.Engage(Blue, blue.Id, red.Id));
            sim.Step(12);

            int redFired = FiredAt(sim, red.Id);
            if (redFired < 0)
            {
                log.AppendLine("  FAIL a marching unit never returned fire");
                return bad + 1;
            }

            // The march must still be in the plan, behind the engagement.
            var queue = sim.QueueOf(red.Id);
            bool marchSurvives = false;
            for (int i = 0; i < queue.Count; i++)
                if (queue[i].Command.Seq == march.Seq) marchSurvives = true;

            if (!marchSurvives)
            {
                log.AppendLine("  FAIL returning fire discarded the player's march order; a " +
                               "reflex may interrupt orders, never delete them");
                bad++;
            }

            if (queue.Count > 0 && queue[0].Command.Kind != CommandKind.Engage)
            {
                log.AppendLine("  FAIL the reaction did not reach the head of the queue");
                bad++;
            }

            if (bad == 0)
                log.AppendLine($"  reflex: answered at t{redFired} while marching, and the march " +
                               $"order #{march.Seq} is still queued behind it");
            return bad;
        }

        // ─── Break contact ────────────────────────────────────────────────────

        private static int CheckBreakContact(StringBuilder log)
        {
            int bad = 0;

            // Armour against infantry, both willing: the infantry will be ground down until it
            // crosses a threshold, and must leave rather than fight to annihilation.
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;

            // On the route: the withdrawal has to have somewhere to go.
            var route = KnownGoodRoute();
            var armourCell = new Vector2(route[30].x, route[30].y);
            var infantryCell = new Vector2(route[24].x, route[24].y);

            var strong = new UnitInstance(new UnitId(1), new SideId(1),
                Strategos.NatoSymbols.SIDCCode.Empty.Raw, armourCell, "ARMOUR", string.Empty,
                100f, UnitCatalogue.Armor) { Roe = RulesOfEngagement.FireAtWill };
            var weak = new UnitInstance(new UnitId(2), new SideId(2),
                Strategos.NatoSymbols.SIDCCode.Empty.Raw, infantryCell,
                "INFANTRY", string.Empty, 100f, UnitCatalogue.InfantryFoot)
            { Roe = RulesOfEngagement.FireAtWill };

            scenario.Units.Clear();
            scenario.Units.Add(strong);
            scenario.Units.Add(weak);

            var sim = new Simulation(scenario, Map());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.EnableReactions();

            int brokeAt = -1;
            float strengthAtBreak = 0f;
            Vector2 cellAtBreak = weak.Cell;
            float rangeAtBreak = 0f;

            for (int i = 0; i < 900 && brokeAt < 0; i++)
            {
                sim.Step();
                var q = sim.QueueOf(weak.Id);
                bool engaging = q != null && !q.IsEmpty && q.TryPeek(out var h) &&
                                h.Command.Kind == CommandKind.Engage;
                if (!engaging && weak.Strength < 100f && !weak.IsDestroyed)
                {
                    // Captured here, not after the loop. Reading these at the end of the test
                    // reports the state a further thirty ticks of incoming fire later, which
                    // made a break at 32% look like one at 12%.
                    brokeAt = sim.Tick;
                    strengthAtBreak = weak.Strength;
                    cellAtBreak = weak.Cell;
                    rangeAtBreak = Vector2.Distance(weak.Cell, strong.Cell);
                }
            }

            if (weak.IsDestroyed)
            {
                log.AppendLine("  FAIL the weaker unit fought to annihilation instead of " +
                               "breaking contact");
                return bad + 1;
            }

            if (brokeAt < 0)
            {
                log.AppendLine($"  FAIL no break-contact after 900 ticks; the unit is at " +
                               $"{weak.Strength:0.0}% / {weak.Suppression:0.0} suppression");
                return bad + 1;
            }

            bool threshold =
                strengthAtBreak < ReactionController.BreakStrengthPercent ||
                weak.Supply.Ammunition < ReactionController.BreakAmmunitionPercent;

            if (!threshold)
            {
                log.AppendLine($"  FAIL broke contact at {strengthAtBreak:0.0}% / " +
                               $"{weak.Supply.Ammunition:0.0}% ammunition, no threshold crossed");
                bad++;
            }

            // It must have stood and fought first. Leaving the moment it is shot at is what
            // the suppression trigger produced, and it reads as sensible in a log line unless
            // something checks how much the unit had actually lost.
            if (strengthAtBreak > 80f)
            {
                log.AppendLine($"  FAIL disengaged at {strengthAtBreak:0.0}% strength, barely " +
                               "scratched; the threshold is firing far too eagerly");
                bad++;
            }

            // And it must stay disengaged rather than immediately re-committing.
            sim.Step(60);
            var after = sim.QueueOf(weak.Id);
            if (after != null && !after.IsEmpty && after.TryPeek(out var head) &&
                head.Command.Kind == CommandKind.Engage)
            {
                log.AppendLine("  FAIL the unit re-engaged straight after breaking contact");
                bad++;
            }

            // Breaking contact means leaving, not merely ceasing fire. Standing still while
            // disengaged just means being destroyed a few seconds later instead of now.
            float withdrew = Vector2.Distance(weak.Cell, cellAtBreak);
            float rangeNow = Vector2.Distance(weak.Cell, strong.Cell);

            if (withdrew < 1f)
            {
                log.AppendLine($"  FAIL disengaged but never moved; still {rangeNow:0.#} cells " +
                               "from what was shooting at it");
                bad++;
            }
            else if (rangeNow <= rangeAtBreak)
            {
                log.AppendLine($"  FAIL withdrew {withdrew:0.#} cells but ended up no further " +
                               $"from the threat ({rangeAtBreak:0.#} -> {rangeNow:0.#} cells)");
                bad++;
            }

            if (bad == 0)
                log.AppendLine($"  break contact: disengaged at t{brokeAt} on " +
                               $"{strengthAtBreak:0.0}% strength, withdrew {withdrew:0.#} cells " +
                               $"and opened the range from {rangeAtBreak:0.#} to {rangeNow:0.#}");
            return bad;
        }

        // ─── Fairness ─────────────────────────────────────────────────────────

        /// <summary>
        /// Two units that notice each other on the same tick must open fire on the same tick.
        /// </summary>
        /// <remarks>
        /// The obvious worry about evaluating reactions in a fixed order is that whoever is
        /// evaluated first shoots first. It does not work out that way — a reaction issues a
        /// command, commands are delivered on the following step, and #12 resolves every shot
        /// in a tick against start-of-tick state — but "it does not work out that way" is worth
        /// an assertion rather than an argument, because it would stop being true the moment
        /// somebody resolved a reaction inline to save a tick of latency.
        /// </remarks>
        private static int CheckMutualReactionIsFair(StringBuilder log)
        {
            int bad = 0;
            var sim = NewPair(out var blue, out var red,
                RulesOfEngagement.FireAtWill, RulesOfEngagement.FireAtWill);

            sim.Step(40);

            int blueFired = FiredAt(sim, blue.Id);
            int redFired = FiredAt(sim, red.Id);

            if (blueFired < 0 || redFired < 0)
            {
                log.AppendLine("  FAIL one of two mutually willing units never fired");
                return bad + 1;
            }

            if (blueFired != redFired)
            {
                log.AppendLine($"  FAIL evaluation order gave an advantage: blue opened at " +
                               $"t{blueFired}, red at t{redFired}");
                bad++;
            }

            float gap = Mathf.Abs(blue.Strength - red.Strength);
            if (gap > 4f)
            {
                log.AppendLine($"  FAIL mirrored units diverged by {gap:0.0} points " +
                               $"({blue.Strength:0.0}% vs {red.Strength:0.0}%)");
                bad++;
            }

            if (bad == 0)
                log.AppendLine($"  fairness: both opened fire at t{blueFired}, ending " +
                               $"{blue.Strength:0.0}% vs {red.Strength:0.0}%");
            return bad;
        }

        // ─── Determinism ──────────────────────────────────────────────────────

        private static int CheckDeterminism(StringBuilder log)
        {
            int bad = 0;

            string first = RunReactive(out int ordersA);
            string second = RunReactive(out int ordersB);

            if (first != second)
            {
                log.AppendLine($"  FAIL two identical reactive runs diverged " +
                               $"({ordersA} vs {ordersB} autonomous orders)");
                bad++;
            }
            else
            {
                log.AppendLine($"  determinism: 200 ticks with {ordersA} autonomous order(s) " +
                               "IDENTICAL across runs");
            }

            return bad;
        }

        private static string RunReactive(out int orders)
        {
            var sim = NewPair(out _, out _,
                RulesOfEngagement.FireAtWill, RulesOfEngagement.ReturnFire);
            sim.Step(200);
            orders = sim.Reactions.OrdersIssued;
            return sim.Signature();
        }
    }
}
#endif
