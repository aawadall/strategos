// VictoryProbe.cs
// Verifies objectives and victory conditions headlessly.
//
// Every failure this guards against is silent. A condition that can never come true looks
// exactly like a scenario you are losing; an objective whose control never transfers looks
// like an enemy who will not give ground; a draw that is an unhandled case looks like a game
// that has hung. None of it throws.
//
// Menu:  Strategos > Probe Victory
// Batch: -executeMethod Strategos.Editor.VictoryProbe.Run

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Maps;
using Strategos.Objectives;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class VictoryProbe
    {
        [MenuItem("Strategos/Probe Victory")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckRoundTrip(log);
            bad += CheckSampleIsWinnable(log);
            bad += CheckControl(log);
            bad += CheckHoldDuration(log);
            bad += CheckVacatedGroundDoesNotHold(log);
            bad += CheckDestroy(log);
            bad += CheckDrawOnTimeout(log);
            bad += CheckPrecedence(log);
            bad += CheckValidation(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[VictoryProbe]\n" + log);
            else Debug.LogError("[VictoryProbe]\n" + log);
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

        /// <summary>Two units either side of one objective, with whatever conditions a test wants.</summary>
        private static Simulation NewContest(out UnitInstance blue, out UnitInstance red,
            Objective objective, IEnumerable<VictoryCondition> conditions, int timeLimit = 0)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;

            blue = new UnitInstance(new UnitId(1), new SideId(1),
                Strategos.NatoSymbols.SIDCCode.Empty.Raw, objective.Cell + new Vector2(40f, 0f),
                "BLUE", string.Empty, 100f, UnitCatalogue.InfantryMech);
            red = new UnitInstance(new UnitId(2), new SideId(2),
                Strategos.NatoSymbols.SIDCCode.Empty.Raw, objective.Cell - new Vector2(40f, 0f),
                "RED", string.Empty, 100f, UnitCatalogue.InfantryMech);

            scenario.Units.Clear();
            scenario.Units.Add(blue);
            scenario.Units.Add(red);

            scenario.Objectives.Clear();
            scenario.Objectives.Add(objective);
            scenario.Victory.Clear();
            if (conditions != null) scenario.Victory.AddRange(conditions);
            scenario.TimeLimitTicks = timeLimit;

            var sim = new Simulation(scenario, Map());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            return sim;
        }

        private static Objective Centre(int id = 1, float radius = 8f) => new()
        {
            Id = id,
            Name = "TEST",
            Cell = new Vector2(120f, 124f),
            RadiusCells = radius,
            InitialOwner = SideId.None,
        };

        // ─── Serialisation ────────────────────────────────────────────────────

        private static int CheckRoundTrip(StringBuilder log)
        {
            int bad = 0;
            var original = ScenarioSamples.Skirmish();
            var back = ScenarioIO.FromJson(ScenarioIO.ToJson(original));

            if (back == null)
            {
                log.AppendLine("  FAIL round trip produced null");
                return bad + 1;
            }

            if (back.Objectives.Count != original.Objectives.Count)
            {
                log.AppendLine($"  FAIL {back.Objectives.Count} objectives survived of " +
                               $"{original.Objectives.Count}");
                bad++;
            }
            else for (int i = 0; i < original.Objectives.Count; i++)
            {
                var (a, b) = (original.Objectives[i], back.Objectives[i]);
                if (a.Id != b.Id || a.Name != b.Name || a.Cell != b.Cell ||
                    !Mathf.Approximately(a.RadiusCells, b.RadiusCells) ||
                    a.InitialOwner != b.InitialOwner)
                { log.AppendLine($"  FAIL objective {i} changed across a round trip"); bad++; }
            }

            if (back.Victory.Count != original.Victory.Count)
            {
                log.AppendLine($"  FAIL {back.Victory.Count} conditions survived of " +
                               $"{original.Victory.Count}");
                bad++;
            }
            else for (int i = 0; i < original.Victory.Count; i++)
            {
                var (a, b) = (original.Victory[i], back.Victory[i]);
                bool same = a.Kind == b.Kind && a.Side == b.Side && a.Priority == b.Priority &&
                            a.HoldTicks == b.HoldTicks && a.DeadlineTick == b.DeadlineTick &&
                            Mathf.Approximately(a.StrengthThresholdPercent,
                                                b.StrengthThresholdPercent) &&
                            a.Description == b.Description &&
                            a.ObjectiveIds.Length == b.ObjectiveIds.Length;

                // An int[] that silently comes back empty is the classic serialiser failure,
                // and it turns a condition into one that can never come true.
                if (same)
                    for (int k = 0; k < a.ObjectiveIds.Length; k++)
                        if (a.ObjectiveIds[k] != b.ObjectiveIds[k]) same = false;

                if (!same)
                { log.AppendLine($"  FAIL victory condition {i} changed across a round trip"); bad++; }
            }

            if (back.TimeLimitTicks != original.TimeLimitTicks)
            { log.AppendLine("  FAIL time limit did not survive"); bad++; }

            if (bad == 0)
                log.AppendLine($"  round trip: {back.Objectives.Count} objective(s), " +
                               $"{back.Victory.Count} condition(s), limit t{back.TimeLimitTicks}");
            return bad;
        }

        /// <summary>The shipped sample must be winnable, and must validate.</summary>
        private static int CheckSampleIsWinnable(StringBuilder log)
        {
            int bad = 0;
            var sample = ScenarioSamples.Skirmish();

            var problems = sample.Validate();
            foreach (var p in problems)
            {
                log.AppendLine($"  FAIL sample invalid: {p}");
                bad++;
            }

            if (sample.Victory.Count == 0)
            {
                log.AppendLine("  FAIL the shipped sample has no way to end");
                bad++;
            }

            // Every side should have at least one way to win, or the scenario is unfair in a
            // way nobody would notice until they had played it.
            foreach (var side in sample.Sides)
            {
                bool canWin = false;
                foreach (var c in sample.Victory) if (c.Side == side.Id) canWin = true;
                if (!canWin)
                {
                    log.AppendLine($"  FAIL side '{side.Name}' has no victory condition");
                    bad++;
                }
            }

            if (bad == 0)
                log.AppendLine($"  sample: {sample.Objectives.Count} objective(s), " +
                               $"{sample.Victory.Count} condition(s), both sides can win");
            return bad;
        }

        // ─── Control ──────────────────────────────────────────────────────────

        private static int CheckControl(StringBuilder log)
        {
            int bad = 0;
            var objective = Centre();
            var sim = NewContest(out var blue, out var red, objective, null);

            if (sim.Victory == null)
            {
                log.AppendLine("  FAIL a scenario with objectives built no evaluator");
                return bad + 1;
            }

            sim.Step(10);
            if (sim.Victory.OwnerOf(1).IsValid)
            { log.AppendLine("  FAIL neutral ground was owned with nobody on it"); bad++; }

            // Blue walks on alone: uncontested presence takes it.
            blue.Cell = objective.Cell;
            sim.Step(10);
            if (sim.Victory.OwnerOf(1) != blue.Side)
            { log.AppendLine("  FAIL uncontested presence did not take the objective"); bad++; }

            // Red arrives: contested, so ownership must freeze rather than flip.
            red.Cell = objective.Cell + new Vector2(1f, 0f);
            sim.Step(10);
            if (sim.Victory.OwnerOf(1) != blue.Side)
            {
                log.AppendLine($"  FAIL contested ground changed hands to " +
                               $"{sim.Victory.OwnerOf(1)}; arriving is not taking");
                bad++;
            }

            // Blue leaves, red remains: now it transfers.
            blue.Cell = objective.Cell + new Vector2(60f, 0f);
            sim.Step(10);
            if (sim.Victory.OwnerOf(1) != red.Side)
            { log.AppendLine("  FAIL clearing the objective did not transfer it"); bad++; }

            // And red leaving does not hand it back — ground stays taken.
            red.Cell = objective.Cell - new Vector2(60f, 0f);
            sim.Step(10);
            if (sim.Victory.OwnerOf(1) != red.Side)
            { log.AppendLine("  FAIL ownership lapsed when the holder walked away"); bad++; }

            if (bad == 0)
                log.AppendLine("  control: neutral -> taken uncontested -> frozen while " +
                               "contested -> transferred when cleared -> sticky when vacated");
            return bad;
        }

        /// <summary>
        /// A hold clock must not run while the ground is abandoned.
        /// </summary>
        /// <remarks>
        /// **The case that was missing, and the reason the bug shipped.** CheckHoldDuration
        /// passes a unit into the objective and leaves it there, so it never exercised the
        /// difference between owning ground and standing on it — and ownership is sticky by
        /// design, so a hold measured against ownership counted time nobody was there.
        ///
        /// On the shipped skirmish that made the primary victory condition satisfiable by
        /// routing a unit through the crossroads for one tick and then doing nothing for ten
        /// minutes. A test that never leaves cannot fail on that, whatever else it asserts.
        /// </remarks>
        private static int CheckVacatedGroundDoesNotHold(StringBuilder log)
        {
            int bad = 0;
            var objective = Centre();
            const int hold = 200;

            var sim = NewContest(out var blue, out _, objective, new[]
            {
                new VictoryCondition
                {
                    Kind = VictoryKind.HoldObjectives, Side = new SideId(1),
                    ObjectiveIds = new[] { 1 }, HoldTicks = hold,
                    Description = "held long enough",
                },
            });

            // Long enough to be *seen*: control is sampled every EvaluationInterval ticks,
            // not continuously, so a three-tick visit can fall between two evaluations and
            // never register at all. That sampling is itself worth knowing — a unit can cross
            // an objective faster than the evaluator looks.
            blue.Cell = objective.Cell;
            sim.Step(VictoryEvaluator.EvaluationInterval * 2);

            blue.Cell = objective.Cell + new Vector2(objective.RadiusCells * 4f, 0f);
            sim.Step(hold * 2);

            if (sim.IsOver)
            {
                log.AppendLine($"  FAIL won at t{sim.Victory.Outcome.Tick} having stood in the " +
                               "objective for 3 ticks and then left — the hold clock is " +
                               "running on ownership, not occupation");
                return bad + 1;
            }

            // Ownership must still be sticky: the ground was taken and nobody else took it.
            if (sim.Victory.OwnerOfIndex(0) != blue.Side)
            {
                log.AppendLine($"  FAIL vacating handed the ground back: owner is " +
                               $"{sim.Victory.OwnerOfIndex(0)}, expected {blue.Side} — " +
                               "ownership should be sticky, only the hold clock should stop");
                bad++;
            }

            if (sim.Victory.HeldTicksOfIndex(0, sim.Tick) != 0)
            {
                log.AppendLine($"  FAIL held clock reads " +
                               $"{sim.Victory.HeldTicksOfIndex(0, sim.Tick)} on empty ground");
                bad++;
            }

            // And coming back restarts it rather than resuming.
            blue.Cell = objective.Cell;
            sim.Step(10);

            int resumed = sim.Victory.HeldTicksOfIndex(0, sim.Tick);
            if (resumed > 12)
            {
                log.AppendLine($"  FAIL returning resumed the old clock at {resumed} ticks " +
                               "rather than starting a new one");
                bad++;
            }

            if (bad == 0)
                log.AppendLine($"  vacated: touched, left for {hold * 2} ticks without " +
                               $"winning, ownership kept, clock restarted at {resumed}");
            return bad;
        }

        private static int CheckHoldDuration(StringBuilder log)
        {
            int bad = 0;
            var objective = Centre();
            const int hold = 200;

            var sim = NewContest(out var blue, out _, objective, new[]
            {
                new VictoryCondition
                {
                    Kind = VictoryKind.HoldObjectives, Side = new SideId(1),
                    ObjectiveIds = new[] { 1 }, HoldTicks = hold,
                    Description = "held long enough",
                },
            });

            blue.Cell = objective.Cell;

            // Must not win merely by arriving — that is the difference between an objective
            // and a finish line.
            sim.Step(hold / 2);
            if (sim.IsOver)
            {
                log.AppendLine($"  FAIL won after {sim.Tick} ticks of a {hold}-tick hold");
                return bad + 1;
            }

            for (int i = 0; i < hold + 40 && !sim.IsOver; i++) sim.Step();

            if (!sim.IsOver)
            { log.AppendLine("  FAIL holding for the full duration never won"); return bad + 1; }

            var outcome = sim.Victory.Outcome;
            if (outcome.Winner != blue.Side || outcome.Condition != VictoryKind.HoldObjectives)
            { log.AppendLine($"  FAIL wrong outcome: {outcome}"); bad++; }

            if (outcome.Tick < hold)
            { log.AppendLine($"  FAIL decided at t{outcome.Tick}, before {hold} ticks elapsed"); bad++; }

            // Once decided it must stop moving.
            int decidedAt = outcome.Tick;
            sim.Step(50);
            if (sim.Victory.Outcome.Tick != decidedAt)
            { log.AppendLine("  FAIL the outcome kept changing after the scenario ended"); bad++; }

            if (bad == 0) log.AppendLine($"  hold: {outcome}");
            return bad;
        }

        private static int CheckDestroy(StringBuilder log)
        {
            int bad = 0;
            var objective = Centre();
            var sim = NewContest(out var blue, out var red, objective, new[]
            {
                new VictoryCondition
                {
                    Kind = VictoryKind.DestroyEnemy, Side = new SideId(1),
                    StrengthThresholdPercent = 25f,
                    Description = "enemy broken",
                },
            });

            // Threshold is a share of STARTING strength, so knock the enemy down directly.
            sim.Step(10);
            if (sim.IsOver)
            { log.AppendLine("  FAIL won at full enemy strength"); return bad + 1; }

            red.Strength = 30f;
            sim.Step(10);
            if (sim.IsOver)
            { log.AppendLine("  FAIL won with the enemy still above the threshold"); bad++; }

            red.Strength = 20f;
            sim.Step(10);

            if (!sim.IsOver)
            { log.AppendLine("  FAIL the enemy fell below the threshold and nothing happened"); bad++; }
            else if (sim.Victory.Outcome.Winner != blue.Side)
            { log.AppendLine($"  FAIL wrong winner: {sim.Victory.Outcome}"); bad++; }

            if (bad == 0) log.AppendLine($"  destroy: {sim.Victory.Outcome}");
            return bad;
        }

        private static int CheckDrawOnTimeout(StringBuilder log)
        {
            int bad = 0;
            var objective = Centre();

            // A condition nobody can meet, and a short clock.
            var sim = NewContest(out _, out _, objective, new[]
            {
                new VictoryCondition
                {
                    Kind = VictoryKind.HoldObjectives, Side = new SideId(1),
                    ObjectiveIds = new[] { 1 }, HoldTicks = 100000,
                },
            }, timeLimit: 100);

            sim.Step(150);

            if (!sim.IsOver)
            { log.AppendLine("  FAIL the clock ran out and nothing ended"); return bad + 1; }

            var outcome = sim.Victory.Outcome;
            if (!outcome.IsDraw)
            { log.AppendLine($"  FAIL timeout produced a winner: {outcome}"); bad++; }
            if (outcome.Tick > 110)
            { log.AppendLine($"  FAIL draw declared at t{outcome.Tick}, limit was 100"); bad++; }

            if (bad == 0) log.AppendLine($"  timeout: {outcome}");
            return bad;
        }

        /// <summary>
        /// Two conditions true on the same evaluation must resolve by priority, not by which
        /// the loop reached first — otherwise the winner is decided by list order.
        /// </summary>
        private static int CheckPrecedence(StringBuilder log)
        {
            int bad = 0;
            var objective = Centre();

            var sim = NewContest(out var blue, out var red, objective, new[]
            {
                // Listed first, lower priority: red wins on attrition.
                new VictoryCondition
                {
                    Kind = VictoryKind.DestroyEnemy, Side = new SideId(2), Priority = 1,
                    StrengthThresholdPercent = 90f, Description = "attrition",
                },
                // Listed second, higher priority: blue wins on ground.
                new VictoryCondition
                {
                    Kind = VictoryKind.HoldObjectives, Side = new SideId(1), Priority = 10,
                    ObjectiveIds = new[] { 1 }, HoldTicks = 0, Description = "ground",
                },
            });

            blue.Cell = objective.Cell;   // satisfies the ground condition
            blue.Strength = 50f;          // and simultaneously the attrition one, for red

            sim.Step(20);

            if (!sim.IsOver)
            { log.AppendLine("  FAIL neither of two satisfied conditions fired"); return bad + 1; }

            if (sim.Victory.Outcome.Winner != blue.Side)
            {
                log.AppendLine($"  FAIL precedence ignored: {sim.Victory.Outcome} — the " +
                               "higher-priority condition should have won");
                bad++;
            }

            if (bad == 0) log.AppendLine($"  precedence: {sim.Victory.Outcome}");
            return bad;
        }

        // ─── Validation ───────────────────────────────────────────────────────

        private static int CheckValidation(StringBuilder log)
        {
            int bad = 0;

            bad += Expect(log, "condition naming a missing objective", s =>
            {
                s.Victory.Add(new VictoryCondition
                {
                    Kind = VictoryKind.HoldObjectives, Side = new SideId(1),
                    ObjectiveIds = new[] { 99 },
                });
            }, "which does not exist");

            bad += Expect(log, "condition with no objectives", s =>
            {
                s.Victory.Add(new VictoryCondition
                {
                    Kind = VictoryKind.HoldObjectives, Side = new SideId(1),
                    ObjectiveIds = new int[0],
                });
            }, "names no objectives");

            bad += Expect(log, "duplicate objective id", s =>
            {
                s.Objectives.Add(new Objective { Id = 1, Cell = new Vector2(10f, 10f) });
                s.Objectives.Add(new Objective { Id = 1, Cell = new Vector2(20f, 20f) });
            }, "Duplicate objective id");

            bad += Expect(log, "objective off the map", s =>
            {
                s.Objectives.Add(new Objective { Id = 7, Cell = new Vector2(9000f, 10f) });
            }, "is off the map");

            return bad;
        }

        private static int Expect(StringBuilder log, string what,
            System.Action<Scenario> corrupt, string fragment)
        {
            var s = ScenarioSamples.Skirmish();
            s.Objectives.Clear();
            s.Victory.Clear();
            corrupt(s);

            foreach (var p in s.Validate())
                if (p.Contains(fragment))
                {
                    log.AppendLine($"  validation catches {what,-38} -> \"{p}\"");
                    return 0;
                }

            log.AppendLine($"  FAIL validation missed {what}");
            return 1;
        }
    }
}
#endif
