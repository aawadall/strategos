// CombatProbe.cs
// Verifies engagement resolution headlessly, and prints the matrix as numbers.
//
// NUMBERS BEFORE PICTURES, the discipline MapContactSheet and MapMeshProbe already follow. A
// combat model is balanced by reading its outputs, so this probe's most useful product is not
// its pass/fail — it is the table it prints. Read the table when tuning; the assertions only
// catch the model breaking, not the model being wrong.
//
// The assertions worth having are the ones a spreadsheet cannot make: that terrain measurably
// matters, that simultaneity is real, that nothing accumulates outside the tick, and that the
// same seed produces byte-identical results.
//
// Menu:  Strategos > Probe Combat
// Batch: -executeMethod Strategos.Editor.CombatProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Combat;
using Strategos.Commands;
using Strategos.Maps;
using Strategos.Reports;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class CombatProbe
    {
        private static ActorId Blue => new(1);

        [MenuItem("Strategos/Probe Combat")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += ChartMatrix(log);
            bad += CheckTerrainMatters(log);
            bad += CheckDeterminism(log);
            bad += CheckSimultaneity(log);
            bad += CheckStateChanges(log);
            bad += CheckSuppressionDoesNotCancelOrders(log);
            bad += CheckEngagementRunsToConclusion(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[CombatProbe]\n" + log);
            else Debug.LogError("[CombatProbe]\n" + log);
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

        /// <summary>A unit of the given type, planted on a cell of the given landcover.</summary>
        private static UnitInstance Unit(int id, int side, string capabilityId,
            LandcoverClass standingOn, Vector2? at = null)
        {
            var cell = at ?? FindCover(standingOn);
            return new UnitInstance(new UnitId(id), new SideId(side),
                Strategos.NatoSymbols.SIDCCode.Empty.Raw, cell,
                $"U{id}", string.Empty, 100f, capabilityId);
        }

        /// <summary>First cell of the given landcover, scanned in a fixed order.</summary>
        private static Vector2 FindCover(LandcoverClass want)
        {
            var map = Map();
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    if (map.GetLandcover(x, y) == want) return new Vector2(x, y);
            return new Vector2(map.Width / 2f, map.Height / 2f);
        }

        // The bench: one fixed pair of cells, with the landcover under the defender stamped to
        // whatever a case wants.
        //
        // Hunting the map for a cell of each class was the obvious approach and it is wrong —
        // it varies elevation and distance along with the cover, so "forest halves incoming
        // fire" gets measured against a different slope on a different hill. Stamping holds
        // every other term fixed, which is the only way a single number can be attributed to
        // the one factor under test.
        private static readonly Vector2Int BenchDefender = new(100, 100);

        /// <summary>One exchange on the bench: geometry fixed, only the named terms varying.</summary>
        private static EngagementResult Bench(string attackerCaps, string defenderCaps,
            LandcoverClass cover, Posture posture, float metres, int tick = 1)
        {
            var map = Map();
            map.SetLandcover(BenchDefender.x, BenchDefender.y, cover);

            var defender = Unit(2, 2, defenderCaps, cover,
                new Vector2(BenchDefender.x, BenchDefender.y));
            defender.Posture = posture;

            var attacker = Unit(1, 1, attackerCaps, LandcoverClass.Open,
                new Vector2(BenchDefender.x + metres / map.Header.MetresPerCell, BenchDefender.y));

            return EngagementResolver.Resolve(attacker, defender, map, UnitCatalogue.Default(),
                tick, 1f);
        }

        private static Simulation NewDuel(out UnitInstance a, out UnitInstance b,
            float separationCells, string aCaps = UnitCatalogue.InfantryMech,
            string bCaps = UnitCatalogue.InfantryMotor)
        {
            var scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;

            var centre = new Vector2(Map().Width / 2f, Map().Height / 2f);
            a = Unit(1, 1, aCaps, LandcoverClass.Open, centre);
            b = Unit(2, 2, bCaps, LandcoverClass.Open, centre + new Vector2(separationCells, 0f));

            scenario.Units.Clear();
            scenario.Units.Add(a);
            scenario.Units.Add(b);

            var sim = new Simulation(scenario, Map());
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            return sim;
        }

        // ─── The table ────────────────────────────────────────────────────────

        /// <summary>
        /// The matrix the issue asks for: attacker and defender types across terrain classes,
        /// with the modifier breakdown that produced each number.
        /// </summary>
        private static int ChartMatrix(StringBuilder log)
        {
            var map = Map();
            var cat = UnitCatalogue.Default();

            string[] attackers = { UnitCatalogue.InfantryMech, UnitCatalogue.Armor,
                                   UnitCatalogue.Artillery };
            string[] defenders = { UnitCatalogue.InfantryFoot, UnitCatalogue.Armor };
            LandcoverClass[] covers = { LandcoverClass.Open, LandcoverClass.Forest,
                                        LandcoverClass.Urban };

            log.AppendLine("  engagement matrix at 300 m, defender halted, 100% strength:");
            log.AppendLine("    attacker         defender      terrain   dmg/min   minutes to kill");

            foreach (var ac in attackers)
                foreach (var dc in defenders)
                    foreach (var cover in covers)
                    {
                        var r = Bench(ac, dc, cover, Posture.Halted, 300f);
                        float perMinute = r.Breakdown.DamagePerMinute;
                        string ttk = perMinute > 0.01f ? $"{100f / perMinute,6:0.0}" : "     -";

                        log.AppendLine($"    {cat.Get(ac).Name,-22} {cat.Get(dc).Name,-16} " +
                                       $"{cover,-9} {perMinute,7:0.00}   {ttk}");
                    }

            // A model where everything dies in ten seconds or nothing ever dies is not a model.
            var probe = Bench(UnitCatalogue.InfantryMech, UnitCatalogue.InfantryFoot,
                LandcoverClass.Open, Posture.Halted, 300f);

            float minutes = 100f / Mathf.Max(0.0001f, probe.Breakdown.DamagePerMinute);
            if (minutes < 0.5f || minutes > 120f)
            {
                log.AppendLine($"  FAIL a plain infantry exchange takes {minutes:0.#} minutes " +
                               "to decide; outside the 0.5-120 band the model is not usable");
                return 1;
            }

            log.AppendLine($"  a plain infantry exchange in the open decides in {minutes:0.#} minutes");
            return 0;
        }

        // ─── Terrain measurably matters ───────────────────────────────────────

        private static int CheckTerrainMatters(StringBuilder log)
        {
            int bad = 0;
            var map = Map();
            var cat = UnitCatalogue.Default();

            float Damage(LandcoverClass cover, Posture posture) =>
                Bench(UnitCatalogue.InfantryMech, UnitCatalogue.InfantryFoot,
                    cover, posture, 300f).Breakdown.DamagePerMinute;

            float open = Damage(LandcoverClass.Open, Posture.Halted);
            float forest = Damage(LandcoverClass.Forest, Posture.Halted);
            float urban = Damage(LandcoverClass.Urban, Posture.Halted);
            float dugIn = Damage(LandcoverClass.Open, Posture.DugIn);
            float moving = Damage(LandcoverClass.Open, Posture.Moving);

            // The acceptance criterion: the same engagement in forest and in the open differs.
            // Asserted as a margin, not as inequality — a 2% difference is arithmetic noise
            // that a player would never see, and would satisfy a naive `<` test.
            if (forest > open * 0.8f)
            {
                log.AppendLine($"  FAIL forest ({forest:0.00}) barely differs from open " +
                               $"({open:0.00}); terrain must be worth taking");
                bad++;
            }

            if (urban > forest)
            {
                log.AppendLine($"  FAIL urban ({urban:0.00}) is worse cover than forest " +
                               $"({forest:0.00})");
                bad++;
            }

            if (dugIn > open * 0.7f)
            {
                log.AppendLine($"  FAIL digging in ({dugIn:0.00} vs {open:0.00}) is not worth " +
                               "the time it costs");
                bad++;
            }

            if (moving <= open)
            {
                log.AppendLine($"  FAIL moving ({moving:0.00}) is no more exposed than halted " +
                               $"({open:0.00})");
                bad++;
            }

            // Range must fall off, or engagement range is a binary and nothing else.
            float maxRange = cat.Get(UnitCatalogue.InfantryMech).EngagementRangeMetres;

            float dNear = Bench(UnitCatalogue.InfantryMech, UnitCatalogue.InfantryFoot,
                LandcoverClass.Open, Posture.Halted, maxRange * 0.05f).Breakdown.DamagePerMinute;
            float dFar = Bench(UnitCatalogue.InfantryMech, UnitCatalogue.InfantryFoot,
                LandcoverClass.Open, Posture.Halted, maxRange * 0.95f).Breakdown.DamagePerMinute;

            if (dFar >= dNear * 0.8f)
            {
                log.AppendLine($"  FAIL range barely matters: {dNear:0.00} close vs " +
                               $"{dFar:0.00} at 95% of maximum");
                bad++;
            }

            // Beyond the envelope, nothing at all.
            var out0 = Bench(UnitCatalogue.InfantryMech, UnitCatalogue.InfantryFoot,
                LandcoverClass.Open, Posture.Halted, maxRange * 1.5f);
            if (out0.Outcome != EngagementOutcome.OutOfRange || out0.Damage != 0f)
            {
                log.AppendLine($"  FAIL a target past maximum range resolved as {out0.Outcome} " +
                               $"for {out0.Damage:0.000} damage");
                bad++;
            }

            if (bad == 0)
                log.AppendLine($"  terrain: open {open:0.00} · forest {forest:0.00} · " +
                               $"urban {urban:0.00} · dug in {dugIn:0.00} · moving {moving:0.00} " +
                               $"| range {dNear:0.00} near vs {dFar:0.00} far");

            return bad;
        }

        // ─── Determinism ──────────────────────────────────────────────────────

        private static int CheckDeterminism(StringBuilder log)
        {
            int bad = 0;

            // The resolver itself: same inputs, same output, called twice.
            var a = Unit(1, 1, UnitCatalogue.InfantryMech, LandcoverClass.Open, new Vector2(20f, 20f));
            var b = Unit(2, 2, UnitCatalogue.InfantryFoot, LandcoverClass.Open, new Vector2(28f, 20f));

            var first = Bench(UnitCatalogue.InfantryMech, UnitCatalogue.InfantryFoot,
                LandcoverClass.Open, Posture.Halted, 300f, tick: 77);
            var second = Bench(UnitCatalogue.InfantryMech, UnitCatalogue.InfantryFoot,
                LandcoverClass.Open, Posture.Halted, 300f, tick: 77);

            if (first.Damage != second.Damage || first.Breakdown.Chance != second.Breakdown.Chance)
            {
                log.AppendLine($"  FAIL the resolver is not a function: {first.Damage:F9} then " +
                               $"{second.Damage:F9}");
                bad++;
            }

            // A different tick must roll differently, or the "random" term is a constant.
            var later = Bench(UnitCatalogue.InfantryMech, UnitCatalogue.InfantryFoot,
                LandcoverClass.Open, Posture.Halted, 300f, tick: 78);
            if (later.Breakdown.Chance == first.Breakdown.Chance)
            {
                log.AppendLine("  FAIL the chance term does not vary with the tick");
                bad++;
            }

            // Two units firing at each other on the same tick must not share a roll.
            float ab = EngagementResolver.ChanceFactor(5, a.Id, b.Id);
            float ba = EngagementResolver.ChanceFactor(5, b.Id, a.Id);
            if (ab == ba)
            {
                log.AppendLine("  FAIL an exchange draws one roll for both directions");
                bad++;
            }

            // And the whole simulation: same orders, same ground, byte-identical signature.
            string runA = RunDuel();
            string runB = RunDuel();

            if (runA != runB)
            {
                log.AppendLine("  FAIL two identical firefights diverged");
                log.AppendLine($"    A: {Truncate(runA)}");
                log.AppendLine($"    B: {Truncate(runB)}");
                bad++;
            }
            else
            {
                log.AppendLine($"  determinism: resolver is a function, rolls vary by tick and " +
                               $"direction, 200-tick firefight IDENTICAL across runs");
            }

            return bad;
        }

        private static string RunDuel()
        {
            var sim = NewDuel(out var a, out var b, 8f);
            sim.Issue(Command.Engage(Blue, a.Id, b.Id));
            sim.Issue(Command.Engage(new ActorId(2), b.Id, a.Id));
            sim.Step(200);
            return sim.Signature();
        }

        private static string Truncate(string s) => s.Length <= 110 ? s : s.Substring(0, 110) + "...";

        // ─── Simultaneity ─────────────────────────────────────────────────────

        /// <summary>
        /// Two identical units firing at each other must take identical damage. If resolution
        /// happened inside the unit loop, the first one would shoot an undamaged enemy and the
        /// second would shoot a weakened one, and the unit listed first would always win.
        /// </summary>
        private static int CheckSimultaneity(StringBuilder log)
        {
            int bad = 0;

            var sim = NewDuel(out var a, out var b, 6f,
                UnitCatalogue.InfantryMech, UnitCatalogue.InfantryMech);

            sim.Issue(Command.Engage(Blue, a.Id, b.Id));
            sim.Issue(Command.Engage(new ActorId(2), b.Id, a.Id));
            sim.Step(120);

            float drift = Mathf.Abs(a.Strength - b.Strength);

            // Not exactly equal: the chance term differs by direction on purpose, so a mirror
            // exchange still diverges a little. A systematic first-mover advantage would show
            // up far larger than the roll spread can explain.
            if (drift > 4f)
            {
                log.AppendLine($"  FAIL mirrored units ended {a.Strength:0.0}% vs {b.Strength:0.0}%, " +
                               $"a {drift:0.0}-point gap; resolution is not simultaneous");
                bad++;
            }
            else
            {
                log.AppendLine($"  simultaneity: mirrored units ended {a.Strength:0.0}% vs " +
                               $"{b.Strength:0.0}% after 120 ticks ({drift:0.0} apart)");
            }

            return bad;
        }

        // ─── State changes ────────────────────────────────────────────────────

        private static int CheckStateChanges(StringBuilder log)
        {
            int bad = 0;

            var sim = NewDuel(out var a, out var b, 8f);
            float ammoBefore = a.Supply.Ammunition;

            sim.Issue(Command.Engage(Blue, a.Id, b.Id));
            sim.Step(90);

            if (b.Strength >= 100f)
            {
                log.AppendLine($"  FAIL 90 ticks of fire left the target at {b.Strength:0.0}%");
                bad++;
            }

            if (b.Suppression <= 0f)
            {
                log.AppendLine("  FAIL the target under fire has no suppression");
                bad++;
            }

            if (a.Supply.Ammunition >= ammoBefore)
            {
                log.AppendLine($"  FAIL the shooter spent no ammunition ({a.Supply.Ammunition:0.0}%)");
                bad++;
            }

            // The unengaged half of the rule: suppression must come back down.
            sim.Issue(Command.Abort(Blue, a.Id));
            float peak = b.Suppression;
            sim.Step(60);

            if (b.Suppression >= peak)
            {
                log.AppendLine($"  FAIL suppression did not decay: {peak:0.0} then " +
                               $"{b.Suppression:0.0} a minute after the shooting stopped");
                bad++;
            }

            // One report per engagement, not one per tick — the same rule contacts obey.
            int engaged = 0;
            foreach (var r in sim.ReportLog.Entries)
                if (r.Kind == ReportKind.Engaged && r.Source == a.Id) engaged++;

            if (engaged != 1)
            {
                log.AppendLine($"  FAIL {engaged} Engaged reports for one engagement; " +
                               "reports are edges, not state");
                bad++;
            }

            if (bad == 0)
                log.AppendLine($"  state: target {b.Strength:0.0}% strength, suppression peaked " +
                               $"{peak:0.0} and fell to {b.Suppression:0.0}, shooter ammunition " +
                               $"{ammoBefore:0.0}% -> {a.Supply.Ammunition:0.0}%, " +
                               $"{engaged} Engaged report");

            return bad;
        }

        /// <summary>
        /// Suppression pins a unit but must not cancel its orders.
        ///
        /// Sustained fire drives suppression to the cap inside a minute, so a unit that came
        /// off worst in the opening exchange spends a while unable to shoot back. If that
        /// counted as failure, its engage order would be dropped permanently for a condition
        /// that clears in under a minute — and the unit would then sit there doing nothing
        /// while the enemy that pinned it walked away, which reads as an AI bug rather than a
        /// combat one.
        /// </summary>
        private static int CheckSuppressionDoesNotCancelOrders(StringBuilder log)
        {
            int bad = 0;

            // Armour on infantry: one-sided enough to pin the infantry quickly.
            var sim = NewDuel(out var strong, out var weak, 6f,
                UnitCatalogue.Armor, UnitCatalogue.InfantryFoot);

            sim.Issue(Command.Engage(Blue, strong.Id, weak.Id));
            var weakOrder = sim.Issue(Command.Engage(new ActorId(2), weak.Id, strong.Id));

            // Long enough to pin, short enough that the infantry is not yet dead.
            int ticks = 0;
            while (ticks < 90 && !weak.IsDestroyed) { sim.Step(); ticks++; }

            float pinned = weak.Suppression;
            var queue = sim.QueueOf(weak.Id);
            bool stillOrdered = queue != null && !queue.IsEmpty;

            if (weak.IsDestroyed)
            {
                log.AppendLine("  FAIL the pinned unit died before the assertion could run; " +
                               "shorten the window or weaken the shooter");
                return bad + 1;
            }

            if (pinned < 50f)
            {
                log.AppendLine($"  FAIL 90 ticks of one-sided fire only reached {pinned:0.0} " +
                               "suppression; the pin is not happening");
                bad++;
            }

            if (!stillOrdered)
            {
                log.AppendLine("  FAIL a suppressed unit lost its engage order");
                bad++;
            }

            // And it must resume once the pressure comes off.
            sim.Issue(Command.Abort(Blue, strong.Id));
            sim.Step(120);

            bool resumed = false;
            foreach (var r in sim.ReportLog.Entries)
                if (r.Kind == ReportKind.Engaged && r.Source == weak.Id &&
                    r.AboutCommand == weakOrder.Seq) resumed = true;

            if (weak.Suppression >= pinned)
            {
                log.AppendLine($"  FAIL suppression stayed at {weak.Suppression:0.0} two minutes " +
                               "after the shooting stopped");
                bad++;
            }

            if (!resumed)
            {
                log.AppendLine("  FAIL the pinned unit never opened fire under its standing order");
                bad++;
            }

            if (bad == 0)
                log.AppendLine($"  suppression: pinned to {pinned:0.0} in {ticks} ticks, kept its " +
                               $"order, recovered to {weak.Suppression:0.0} and fired");

            return bad;
        }

        /// <summary>
        /// A one-sided engagement must actually finish: the target destroyed, the order
        /// completed and taken off the queue, and a report for each. A firefight that never
        /// ends is the failure this catches, and it is invisible in a short run.
        /// </summary>
        private static int CheckEngagementRunsToConclusion(StringBuilder log)
        {
            int bad = 0;

            var sim = NewDuel(out var a, out var b, 4f, UnitCatalogue.Armor,
                UnitCatalogue.InfantryFoot);
            var order = sim.Issue(Command.Engage(Blue, a.Id, b.Id));

            int ticks = 0;
            while (ticks < 6000 && !b.IsDestroyed) { sim.Step(); ticks++; }

            if (!b.IsDestroyed)
            {
                log.AppendLine($"  FAIL armour firing on infantry at 100 m left it at " +
                               $"{b.Strength:0.0}% after {ticks} ticks");
                return bad + 1;
            }

            sim.Step(2);   // let the conclusion be noticed and reported

            var queue = sim.QueueOf(a.Id);
            if (queue != null && !queue.IsEmpty)
            {
                log.AppendLine("  FAIL the engage order outlived its target");
                bad++;
            }

            bool destroyed = false, completed = false;
            foreach (var r in sim.ReportLog.Entries)
            {
                if (r.Kind == ReportKind.Destroyed && r.Source == b.Id) destroyed = true;
                if (r.Kind == ReportKind.OrderCompleted && r.Source == a.Id &&
                    r.AboutCommand == order.Seq) completed = true;
            }

            if (!destroyed) { log.AppendLine("  FAIL nothing reported the unit destroyed"); bad++; }
            if (!completed) { log.AppendLine("  FAIL the engage order never reported completion"); bad++; }

            if (bad == 0)
                log.AppendLine($"  conclusion: infantry destroyed in {ticks} ticks " +
                               $"({ticks / 60f:0.#} min), order completed and dequeued");

            return bad;
        }
    }
}
#endif
