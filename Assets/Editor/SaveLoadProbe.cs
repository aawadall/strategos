// SaveLoadProbe.cs
// #74: proves a run can be saved mid-scenario and resumed with identical subsequent behaviour.
//
// THE ISSUE'S OWN WORDING IS NOT ENOUGH. #74 asks for "a round-trip signature comparison, not a
// field checklist" — right, and not sufficient on its own. Simulation.Signature() is a
// divergence oracle, not a completeness oracle: it was built to answer "did two runs of the
// same code diverge", and it deliberately omits state that is derivable or that cannot differ
// *within* one run. Both of those are exactly the state a snapshot is most likely to drop —
// _acknowledgedDirectives, ContactTracker's memory, VictoryEvaluator's starting-strength
// baseline, SideDirector's retry memory, and every UnitInstance field outside the six
// Signature() reads (Training, Roe, Supply beyond Ammunition, DestroyedAtTick). A snapshot
// probe built only on Signature() would pass while silently dropping every one of those.
//
// So this file runs three kinds of check, not one:
//   1. Round-trip: snapshot a stepped simulation, restore, Signature() must match.
//   2. Step-after-restore: step both further, Signature() must still match — the one that
//      catches derived state restored incorrectly, which can match at the moment of restore
//      and diverge on the very next tick.
//   3. One dedicated assertion per row of the state audit that Signature() does not cover,
//      compared directly rather than inferred from whether a signature happened to move.
//
// See docs/simulation-invariants.md's note on what Signature() does and does not cover, and the
// #74 PR description for the full audit table this file is built from.
//
// Menu:  Strategos > Probe Save Load
// Batch: -executeMethod Strategos.Editor.SaveLoadProbe.Run

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Commands;
using Strategos.Directives;
using Strategos.Objectives;
using Strategos.Persistence;
using Strategos.Persistence.Files;
using Strategos.Reports;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class SaveLoadProbe
    {
        [MenuItem("Strategos/Probe Save Load")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            bad += CheckRoundTrip(log);
            bad += CheckStepAfterRestore(log);
            bad += CheckAcknowledgementSurvives(log);
            bad += CheckContactMemorySurvives(log);
            bad += CheckUnitFieldsOutsideSignature(log);
            bad += CheckCommandLogHistoryFull(log);
            bad += CheckInFlightBusMessagesSurvive(log);
            bad += CheckVictoryStartingStrengthSurvives(log);
            bad += CheckDirectorMemorySurvives(log);
            bad += CheckReactionPictureSurvives(log);
            bad += CheckFileStoreRoundTrip(log);
            bad += CheckVersionRefusal(log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[SaveLoadProbe]\n" + log);
            else Debug.LogError("[SaveLoadProbe]\n" + log);
        }

        // ─── Fixtures ─────────────────────────────────────────────────────────

        private static Simulation NewSim(out Scenario scenario)
        {
            scenario = ScenarioSamples.Skirmish();
            scenario.Map.EnableErosion = false;   // dominant generation cost, unneeded here
            var map = scenario.GenerateMap();
            var sim = new Simulation(scenario, map);
            sim.AddExecutor(new MoveToExecutor());
            sim.AddExecutor(new EngageExecutor());
            sim.AddExecutor(new DefendExecutor());
            return sim;
        }

        private static ActorId Blue => new(1);
        private static ActorId Red => new(2);

        private static readonly UnitId BluCompany = new(1);   // A/1-7 IN
        private static readonly UnitId BluArmor = new(2);     // 1/A/2-69 AR
        private static readonly UnitId BluScout = new(3);     // SCT/1-7 IN, green
        private static readonly UnitId RedCompany = new(4);   // 3/2 MRR
        private static readonly UnitId RedArmor = new(5);     // 1/3/2 MRR
        private static readonly UnitId RedArty = new(6);      // BTY/2 MRR

        /// <summary>
        /// Commands issued at absolute tick <paramref name="tick"/>. Keyed by absolute tick and
        /// nothing else, so calling it against a freshly restored simulation for ticks after the
        /// snapshot reproduces the same continuation a player giving the same orders would.
        /// </summary>
        private static void Script(Simulation sim, int tick)
        {
            switch (tick)
            {
                case 1:
                    sim.Issue(Command.MoveTo(Blue, BluCompany, new Vector2(90f, 90f)));
                    sim.Issue(Command.MoveTo(Blue, BluScout, new Vector2(120f, 118f)));
                    sim.Issue(Command.MoveTo(Red, RedCompany, new Vector2(140f, 138f)));
                    break;
                case 3:
                    // Acknowledging is exactly the action #92/#74 flagged as replay-invisible
                    // unless _acknowledgedDirectives is reconstructed — see CheckAcknowledgementSurvives
                    // for the dedicated check; scripted here too so the round-trip/step-after-
                    // restore assertions exercise it as part of an ordinary run.
                    if (sim.DirectiveLog.Count > 0) sim.AcknowledgeDirective(sim.DirectiveLog[0]);
                    break;
                case 10:
                    sim.Issue(Command.MoveTo(Blue, BluArmor, new Vector2(85f, 80f)));
                    break;
                case 18:
                    sim.Issue(Command.Abort(Blue, BluCompany));
                    break;
                case 26:
                    // A second acknowledge after restore: idempotence must hold across the
                    // boundary too, not only within one continuous run.
                    if (sim.DirectiveLog.Count > 0) sim.AcknowledgeDirective(sim.DirectiveLog[0]);
                    sim.Issue(Command.MoveTo(Blue, BluCompany, new Vector2(100f, 100f)));
                    break;
                case 35:
                    sim.Issue(Command.CancelFrom(Red, RedCompany, 0));
                    sim.Issue(Command.MoveTo(Red, RedCompany, new Vector2(150f, 148f)));
                    break;
                case 45:
                    sim.Issue(Command.Hold(Blue, BluArmor));
                    break;
            }
        }

        private static Simulation RunScripted(int fromTickExclusive, int toTickInclusive,
            Simulation sim)
        {
            for (int t = fromTickExclusive + 1; t <= toTickInclusive; t++)
            {
                Script(sim, t);
                sim.Step();
            }
            return sim;
        }

        private static string Trim(string s, int max = 140) =>
            s.Length <= max ? s : s.Substring(0, max) + "...";

        // ─── 1. Round-trip ────────────────────────────────────────────────────

        private const int SnapshotTick = 25;
        private const int FinalTick = 55;

        private static int CheckRoundTrip(StringBuilder log)
        {
            int bad = 0;
            var recorded = NewSim(out _);
            RunScripted(0, SnapshotTick, recorded);

            var snap = recorded.Snapshot();
            var restored = Simulation.Restore(snap);
            restored.AddExecutor(new MoveToExecutor());
            restored.AddExecutor(new EngageExecutor());
            restored.AddExecutor(new DefendExecutor());

            string before = recorded.Signature();
            string after = restored.Signature();

            if (before != after)
            {
                log.AppendLine("  FAIL round-trip: Signature() diverged immediately after restore");
                log.AppendLine($"    recorded: {Trim(before)}");
                log.AppendLine($"    restored: {Trim(after)}");
                bad++;
            }
            if (restored.Tick != recorded.Tick)
            {
                log.AppendLine($"  FAIL Tick after restore is {restored.Tick}, expected {recorded.Tick}");
                bad++;
            }

            log.AppendLine($"  1. round-trip at tick {SnapshotTick}, {recorded.Log.Count} orders " +
                            $"issued so far: {(bad == 0 ? "IDENTICAL" : "DIVERGED")}");
            return bad;
        }

        // ─── 2. Step-after-restore ────────────────────────────────────────────

        private static int CheckStepAfterRestore(StringBuilder log)
        {
            int bad = 0;
            var recorded = NewSim(out _);
            RunScripted(0, SnapshotTick, recorded);

            var snap = recorded.Snapshot();
            var restored = Simulation.Restore(snap);
            restored.AddExecutor(new MoveToExecutor());
            restored.AddExecutor(new EngageExecutor());
            restored.AddExecutor(new DefendExecutor());

            // Continue BOTH from here with the same script, applied to each independently — a
            // fresh Simulation.Issue on the restored side, not a replay of anything logged.
            RunScripted(SnapshotTick, FinalTick, recorded);
            RunScripted(SnapshotTick, FinalTick, restored);

            string a = recorded.Signature();
            string b = restored.Signature();

            if (a != b)
            {
                log.AppendLine("  FAIL step-after-restore: signatures matched at restore but " +
                               $"diverged by tick {FinalTick}");
                log.AppendLine($"    recorded: {Trim(a)}");
                log.AppendLine($"    restored: {Trim(b)}");
                bad++;
            }

            log.AppendLine($"  2. step-after-restore: {SnapshotTick} -> {FinalTick}, " +
                            $"{FinalTick - SnapshotTick} further ticks each: " +
                            $"{(bad == 0 ? "IDENTICAL" : "DIVERGED")}");
            return bad;
        }

        // ─── 3. One assertion per audit row ───────────────────────────────────

        /// <summary>
        /// _acknowledgedDirectives (Simulation.cs:329) is deliberately outside Signature() —
        /// derivable from ReportLog, which Signature() already covers. But HasAcknowledged is a
        /// membership query nothing in Signature() exercises directly, and #92/handoff.md's own
        /// warning is exactly this: a loaded game that does not reconstruct it lets an
        /// already-acknowledged directive be acknowledged twice.
        /// </summary>
        private static int CheckAcknowledgementSurvives(StringBuilder log)
        {
            int bad = 0;
            var sim = NewSim(out _);
            var directive = sim.DirectiveLog[0];

            sim.Step();   // tick1: directive delivered
            sim.AcknowledgeDirective(directive);
            sim.Step(3);

            if (!sim.HasAcknowledged(directive.Seq))
            {
                log.AppendLine("  FAIL sanity: directive was not acknowledged before snapshot");
                return bad + 1;
            }

            int reportsBefore = sim.ReportLog.Count;
            var snap = sim.Snapshot();
            var restored = Simulation.Restore(snap);

            if (!restored.HasAcknowledged(directive.Seq))
            {
                log.AppendLine("  FAIL HasAcknowledged is false after restore — a restored game " +
                               "would let this directive be acknowledged a second time");
                bad++;
            }

            // The idempotence guard itself: acknowledging again after restore must append
            // nothing, exactly as it would not have on the original run.
            var second = restored.AcknowledgeDirective(directive);
            if (second != null)
            {
                log.AppendLine("  FAIL acknowledging again after restore appended a second " +
                               "DirectiveAcknowledged report — the guard did not survive restore");
                bad++;
            }
            if (restored.ReportLog.Count != reportsBefore)
            {
                log.AppendLine($"  FAIL restored ReportLog grew from {reportsBefore} to " +
                               $"{restored.ReportLog.Count} after a redundant acknowledge");
                bad++;
            }

            log.AppendLine($"  3a. acknowledgement: HasAcknowledged survives restore, redundant " +
                            $"acknowledge after restore appends nothing  {(bad == 0 ? "ok" : "FAILED")}");
            return bad;
        }

        /// <summary>
        /// ContactTracker (Simulation.cs:106) is the player's knowledge and is not in
        /// Signature() at all. A green observer's contact can be held, unpublished, in its
        /// internal _pending queue at the moment of a snapshot — restoring an empty tracker
        /// both forgets what has been seen AND silently drops a report that was already on its
        /// way, which nothing about the moment of restore reveals.
        /// </summary>
        private static int CheckContactMemorySurvives(StringBuilder log)
        {
            int bad = 0;
            var sim = NewSim(out _);

            // Every other unit stays at full training, so the scout is the only observer that
            // ever holds a report back — otherwise RedCompany's own 70%-trained observation of
            // the scout would add a second, unrelated held report and this check would no
            // longer be measuring one thing.
            foreach (var id in new[] { BluCompany, BluArmor, RedCompany, RedArmor, RedArty })
                sim.UnitOf(id).Training = 100f;

            // Maximum hesitation, deterministically: a held report is guaranteed rather than
            // timed against training's usual curve. Placed close to all three OPFOR leaves on
            // purpose — Recon's detection range covers more than one of them from here, so this
            // exercises several simultaneously held reports, not only one.
            var scout = sim.UnitOf(BluScout);
            scout.Training = 0f;
            scout.Cell = sim.UnitOf(RedCompany).Cell + new Vector2(2f, 0f);

            sim.Step();   // tick1: Sweep sees the contact(s), hesitation holds every report back

            var preSnap = sim.Snapshot();
            int expected = preSnap.ContactsPending.Count;
            if (expected == 0)
            {
                log.AppendLine("  FAIL sanity: expected at least 1 held contact report before " +
                               "snapshot, got 0 — the fixture did not force the case this check needs");
                return bad + 1;
            }
            // Fully-trained OPFOR units detecting the scout back publish immediately (zero
            // hesitation) — legitimate traffic, not part of what this check measures. Only the
            // scout's own observations are held, which preSnap.ContactsPending already isolates.
            int immediateBefore = 0;
            foreach (var r in preSnap.ReportLog) if (r.Kind == ReportKind.Contact) immediateBefore++;

            var restored = Simulation.Restore(preSnap);
            var postSnap = restored.Snapshot();

            if (postSnap.ContactsPending.Count != expected)
            {
                log.AppendLine($"  FAIL restored tracker holds {postSnap.ContactsPending.Count} " +
                               $"pending contact(s), expected {expected} — a held report was lost");
                bad++;
            }
            else
            {
                for (int i = 0; i < expected; i++)
                {
                    if (postSnap.ContactsPending[i].Due != preSnap.ContactsPending[i].Due ||
                        postSnap.ContactsPending[i].Report.Subject != preSnap.ContactsPending[i].Report.Subject)
                    {
                        log.AppendLine($"  FAIL restored held contact [{i}] does not match the " +
                                       $"original (due {postSnap.ContactsPending[i].Due} vs " +
                                       $"{preSnap.ContactsPending[i].Due})");
                        bad++;
                    }
                }
            }

            // The behavioural proof, run regardless of the direct comparison above: step both to
            // the tick every held report was originally due and confirm both actually deliver
            // all of them, at the same ticks — using preSnap's own due times, since a restored
            // tracker that lost its memory must not be allowed to hide behind a shorter loop.
            int maxDue = 0;
            for (int i = 0; i < expected; i++)
                if (preSnap.ContactsPending[i].Due > maxDue) maxDue = preSnap.ContactsPending[i].Due;

            while (sim.Tick < maxDue) sim.Step();
            while (restored.Tick < maxDue) restored.Step();

            int simContacts = CountContactReports(sim);
            int restoredContacts = CountContactReports(restored);
            int expectedTotal = immediateBefore + expected;
            if (simContacts != expectedTotal || restoredContacts != expectedTotal)
            {
                log.AppendLine($"  FAIL Contact reports delivered by tick {maxDue}: original " +
                               $"{simContacts}, restored {restoredContacts}, expected " +
                               $"{expectedTotal} ({immediateBefore} immediate + {expected} held) each");
                bad++;
            }
            if (sim.Signature() != restored.Signature())
            {
                log.AppendLine("  FAIL Signature() diverged once the held contact(s) came due");
                bad++;
            }

            log.AppendLine($"  3b. contact memory: {expected} hesitation-held report(s) " +
                            $"expected to survive restore and be delivered by tick {maxDue}  " +
                            $"{(bad == 0 ? "ok" : "FAILED")}");

            return bad;
        }

        private static int CountContactReports(Simulation sim)
        {
            int n = 0;
            foreach (var r in sim.ReportLog.Entries) if (r.Kind == ReportKind.Contact) n++;
            return n;
        }

        /// <summary>
        /// UnitInstance carries fields Signature() never reads: Training, Roe and three of
        /// SupplyLevels' four classes (only Ammunition is in the signature). None of these are
        /// in the six-field list at Simulation.cs's Signature() — this is the audit's own
        /// instruction to "check what exists; do not assume the list is complete."
        /// </summary>
        private static int CheckUnitFieldsOutsideSignature(StringBuilder log)
        {
            int bad = 0;
            var sim = NewSim(out _);
            var u = sim.UnitOf(BluCompany);

            u.Training = 42f;
            u.Roe = RulesOfEngagement.HoldFire;
            u.Supply.Rations = 61f;
            u.Supply.Water = 73f;
            u.Supply.Fuel = 12f;
            u.DestroyedAtTick = UnitInstance.Alive;

            sim.Step(2);

            var snap = sim.Snapshot();
            var restored = Simulation.Restore(snap);
            var ru = restored.UnitOf(BluCompany);

            if (ru == null) { log.AppendLine("  FAIL restored unit not found"); return bad + 1; }

            if (!Mathf.Approximately(ru.Training, 42f))
            { log.AppendLine($"  FAIL Training {ru.Training}, expected 42 — not in Signature(), " +
                              "would round-trip silently to its default"); bad++; }
            if (ru.Roe != RulesOfEngagement.HoldFire)
            { log.AppendLine($"  FAIL Roe {ru.Roe}, expected HoldFire — not in Signature() at all"); bad++; }
            if (!Mathf.Approximately(ru.Supply.Rations, 61f))
            { log.AppendLine($"  FAIL Supply.Rations {ru.Supply.Rations}, expected 61"); bad++; }
            if (!Mathf.Approximately(ru.Supply.Water, 73f))
            { log.AppendLine($"  FAIL Supply.Water {ru.Supply.Water}, expected 73"); bad++; }
            if (!Mathf.Approximately(ru.Supply.Fuel, 12f))
            { log.AppendLine($"  FAIL Supply.Fuel {ru.Supply.Fuel}, expected 12"); bad++; }

            log.AppendLine("  3c. unit fields outside Signature(): Training, Roe, Rations, " +
                            $"Water, Fuel all survive restore  {(bad == 0 ? "ok" : "FAILED")}");
            return bad;
        }

        /// <summary>
        /// CommandLog is the full order history, including entries no longer live in any
        /// queue — Signature() folds in the *queue's* signature, not the log's, so a completed
        /// or aborted order that has left the queue leaves no trace a signature comparison could
        /// miss noticing. Abort itself is the cheapest way to empty a queue on the very next
        /// step, so the log holds two entries while the live plan holds zero either way.
        /// </summary>
        private static int CheckCommandLogHistoryFull(StringBuilder log)
        {
            int bad = 0;
            var sim = NewSim(out _);

            sim.Issue(Command.MoveTo(Blue, BluCompany, new Vector2(90f, 90f)));
            sim.Step();
            sim.Issue(Command.Abort(Blue, BluCompany));
            sim.Step();

            if (!sim.QueueOf(BluCompany).IsEmpty)
            {
                log.AppendLine("  FAIL sanity: queue should be empty after the abort");
                return bad + 1;
            }
            int expected = sim.Log.Count;
            if (expected < 2)
            {
                log.AppendLine($"  FAIL sanity: expected at least 2 logged orders, got {expected}");
                return bad + 1;
            }

            var snap = sim.Snapshot();
            var restored = Simulation.Restore(snap);

            // Both queues are empty either way, so Signature() cannot distinguish "the log
            // round-tripped" from "the log was dropped" — this has to be read directly.
            if (restored.Log.Count != expected)
            {
                log.AppendLine($"  FAIL restored CommandLog holds {restored.Log.Count} entries, " +
                               $"expected {expected} — the completed/aborted history is real state " +
                               "Signature() does not cover (the live queue it reads is empty on " +
                               "both sides regardless)");
                bad++;
            }

            log.AppendLine($"  3d. command log history: {expected} entries (including a finished " +
                            $"MoveTo and its Abort) survive restore with an empty queue on both " +
                            $"sides  {(bad == 0 ? "ok" : "FAILED")}");
            return bad;
        }

        /// <summary>
        /// A command published during the very step a snapshot is taken sits in
        /// MessageBus.Pending, not yet delivered — already in CommandLog, but invisible to every
        /// consumer until Deliver() runs. Dropping it is the textbook "matches at restore,
        /// diverges on the next step" bug: the queue is empty on both sides at the moment of
        /// restore, so only stepping forward reveals it.
        /// </summary>
        private static int CheckInFlightBusMessagesSurvive(StringBuilder log)
        {
            int bad = 0;
            var sim = NewSim(out _);
            sim.Step();   // tick1, nothing published yet

            sim.Issue(Command.MoveTo(Blue, BluCompany, new Vector2(90f, 90f)));

            if (sim.QueueOf(BluCompany).Count != 0)
            {
                log.AppendLine("  FAIL sanity: order should not have reached the queue yet");
                return bad + 1;
            }

            var snap = sim.Snapshot();
            if (snap.CommandBusPending.Count != 1)
            {
                log.AppendLine($"  FAIL sanity: expected 1 in-flight command in the snapshot, " +
                               $"got {snap.CommandBusPending.Count}");
                return bad + 1;
            }

            var restored = Simulation.Restore(snap);
            restored.AddExecutor(new MoveToExecutor());

            if (restored.QueueOf(BluCompany).Count != 0)
            {
                log.AppendLine("  FAIL sanity: restored queue should still be empty before a step");
                bad++;
            }

            sim.AddExecutor(new MoveToExecutor());
            sim.Step();
            restored.Step();

            if (restored.QueueOf(BluCompany).Count != sim.QueueOf(BluCompany).Count)
            {
                log.AppendLine($"  FAIL after delivering, restored queue has " +
                               $"{restored.QueueOf(BluCompany).Count} entries, original has " +
                               $"{sim.QueueOf(BluCompany).Count} — the in-flight order was lost " +
                               "across restore");
                bad++;
            }
            if (sim.Signature() != restored.Signature())
            {
                log.AppendLine("  FAIL Signature() diverged once the in-flight order was delivered");
                bad++;
            }

            log.AppendLine("  3e. in-flight bus message: a command published on the snapshot's " +
                            $"own tick is still delivered next step  {(bad == 0 ? "ok" : "FAILED")}");
            return bad;
        }

        /// <summary>
        /// VictoryEvaluator._startingStrength (VictoryEvaluator.cs) is fixed once, at
        /// construction, from whichever units the constructor was handed. A restored
        /// Simulation's constructor runs again and would recompute it from whatever
        /// Scenario.Units currently holds — by save time, already damaged — silently changing
        /// what "reduced below 25%" means for the rest of the run without moving Signature() at
        /// the moment of restore at all.
        /// </summary>
        private static int CheckVictoryStartingStrengthSurvives(StringBuilder log)
        {
            int bad = 0;
            var sim = NewSim(out _);
            if (sim.Victory == null)
            {
                log.AppendLine("  FAIL sanity: skirmish has no VictoryEvaluator");
                return bad + 1;
            }

            // Damage OPFOR before the snapshot, so "current" and "starting" strength disagree —
            // the exact condition that makes a wrong recomputation observable.
            sim.UnitOf(RedCompany).Strength = 60f;
            sim.UnitOf(RedArmor).Strength = 60f;
            sim.UnitOf(RedArty).Strength = 60f;
            sim.Step(2);

            float trueRemaining = sim.Victory.RemainingFraction(new SideId(2), sim.Units);

            var snap = sim.Snapshot();
            var restored = Simulation.Restore(snap);

            if (restored.Victory == null)
            {
                log.AppendLine("  FAIL restored simulation has no VictoryEvaluator");
                return bad + 1;
            }

            float restoredRemaining = restored.Victory.RemainingFraction(new SideId(2), restored.Units);

            if (Mathf.Abs(trueRemaining - restoredRemaining) > 0.001f)
            {
                log.AppendLine($"  FAIL RemainingFraction after restore is {restoredRemaining:0.###}, " +
                               $"expected {trueRemaining:0.###} — starting strength was recomputed " +
                               "from already-damaged current strength instead of preserved");
                bad++;
            }

            log.AppendLine($"  3f. victory starting strength: OPFOR remaining fraction " +
                            $"{trueRemaining:0.###} preserved across restore  {(bad == 0 ? "ok" : "FAILED")}");
            return bad;
        }

        /// <summary>
        /// SideDirector._lastOrdered cannot be told apart from a player's own orders by reading
        /// CommandLog back — both are logged under the same ActorId.ForSide — so unlike
        /// ReactionController's picture it is not derivable and has to be carried directly.
        /// Losing it reopens the order-storm #13 built RetryInterval to close: a restored
        /// director with no memory can reissue on its very next evaluation an order the original
        /// run was still waiting out.
        /// </summary>
        private static int CheckDirectorMemorySurvives(StringBuilder log)
        {
            int bad = 0;
            var sim = NewSim(out _);
            var director = sim.EnableDirector(new[] { new SideId(2) });

            sim.Step(20);   // one evaluation interval: the director should have ordered something

            var before = director.SnapshotLastOrdered();
            if (before.Count == 0)
            {
                log.AppendLine("  FAIL sanity: director issued no orders to remember");
                return bad + 1;
            }

            var snap = sim.Snapshot();
            var restored = Simulation.Restore(snap);
            var restoredDirector = restored.EnableDirector(new[] { new SideId(2) });
            restored.RestoreDirectorMemory(snap);

            var after = restoredDirector.SnapshotLastOrdered();
            if (after.Count != before.Count)
            {
                log.AppendLine($"  FAIL restored director remembers {after.Count} unit(s), " +
                               $"expected {before.Count}");
                bad++;
            }
            else
            {
                foreach (var kv in before)
                {
                    if (!after.TryGetValue(kv.Key, out int tick) || tick != kv.Value)
                    {
                        log.AppendLine($"  FAIL unit {kv.Key} last ordered at {kv.Value} " +
                                       $"originally, {(after.TryGetValue(kv.Key, out var t) ? t.ToString() : "missing")} " +
                                       "after restore");
                        bad++;
                    }
                }
            }

            log.AppendLine($"  3g. director retry memory: {before.Count} unit(s) tracked, " +
                            $"survives restore  {(bad == 0 ? "ok" : "FAILED")}");
            return bad;
        }

        /// <summary>
        /// ReactionController's picture (who this unit believes is attacking it) is derivable —
        /// see ReactionController.RebuildFrom — but only if Simulation.RestoreReactionPicture is
        /// actually called with the right slice of ReportLog. This proves the wiring, not just
        /// the mechanism.
        /// </summary>
        private static int CheckReactionPictureSurvives(StringBuilder log)
        {
            int bad = 0;
            var sim = NewSim(out _);
            var reactions = sim.EnableReactions();

            var attacker = sim.UnitOf(RedCompany);
            var defender = sim.UnitOf(BluCompany);
            sim.Report(SituationReport.Engaged(attacker.Id, defender, sim.Tick, 0));
            sim.Step();   // delivered next step; reactions records it

            var before = reactions.AttackersOf(defender.Id);
            if (before.Count == 0)
            {
                log.AppendLine("  FAIL sanity: reaction controller did not record the attacker");
                return bad + 1;
            }

            var snap = sim.Snapshot();
            var restored = Simulation.Restore(snap);
            var restoredReactions = restored.EnableReactions();
            restored.RestoreReactionPicture(snap);

            var after = restoredReactions.AttackersOf(defender.Id);
            if (after.Count != before.Count || (after.Count > 0 && after[0] != before[0]))
            {
                log.AppendLine($"  FAIL restored picture for {defender.Id} has {after.Count} " +
                               $"attacker(s), expected {before.Count} matching {string.Join(",", before)}");
                bad++;
            }

            log.AppendLine($"  3h. reaction picture: {before.Count} known attacker(s) on " +
                            $"{defender.Id} survive restore  {(bad == 0 ? "ok" : "FAILED")}");
            return bad;
        }

        // ─── The file-backed store ─────────────────────────────────────────────

        private static string TempDir() =>
            Path.Combine(Path.GetTempPath(), "strategos-saveload-probe-" + Guid.NewGuid());

        private static int CheckFileStoreRoundTrip(StringBuilder log)
        {
            int bad = 0;
            var dir = TempDir();
            try
            {
                var sim = NewSim(out var scenario);
                sim.Step(5);
                var snap = sim.Snapshot();

                var store = new FileGameStore(dir);
                var record = new SaveRecord
                {
                    SaveId = "probe-1",
                    ScenarioName = scenario.Name,
                    Tick = sim.Tick,
                    SavedAtUtc = DateTime.UtcNow.ToString("o"),
                    Snapshot = snap,
                };
                store.Save(record);

                var loaded = store.Load("probe-1");
                if (loaded == null)
                {
                    log.AppendLine("  FAIL Load('probe-1') returned null after Save");
                    return bad + 1;
                }

                var restored = Simulation.Restore(loaded.Snapshot);
                if (restored.Signature() != sim.Signature())
                {
                    log.AppendLine("  FAIL Signature() diverged after a real file round trip " +
                                   "(Save -> Load -> Restore)");
                    bad++;
                }

                var listed = store.ListSaves();
                if (listed.Count != 1 || listed[0].SaveId != "probe-1")
                {
                    log.AppendLine($"  FAIL ListSaves returned {listed.Count} entrie(s), " +
                                   "expected exactly 1 named 'probe-1'");
                    bad++;
                }

                if (!store.Delete("probe-1"))
                {
                    log.AppendLine("  FAIL Delete('probe-1') returned false for a save that exists");
                    bad++;
                }
                if (store.Load("probe-1") != null)
                {
                    log.AppendLine("  FAIL Load('probe-1') still returns a record after Delete");
                    bad++;
                }

                log.AppendLine($"  file store: save -> load -> restore round trip identical, " +
                               $"list and delete both correct  {(bad == 0 ? "ok" : "FAILED")}");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
            return bad;
        }

        /// <summary>
        /// #74's acceptance criterion: a save states its version and refuses rather than
        /// misloads across an incompatible one. Exercised against a deliberately bad
        /// SaveRecord.FormatVersion, through the real file-backed store — not asserted in the
        /// abstract, since an untested refusal path is the same defect as an untested happy path.
        /// </summary>
        private static int CheckVersionRefusal(StringBuilder log)
        {
            int bad = 0;
            var dir = TempDir();
            try
            {
                var sim = NewSim(out var scenario);
                var store = new FileGameStore(dir);
                var record = new SaveRecord
                {
                    SaveId = "bad-version",
                    FormatVersion = SaveRecord.CurrentFormatVersion + 1,
                    ScenarioName = scenario.Name,
                    Tick = 0,
                    SavedAtUtc = DateTime.UtcNow.ToString("o"),
                    Snapshot = sim.Snapshot(),
                };
                store.Save(record);

                bool refused = false;
                try { store.Load("bad-version"); }
                catch (SaveVersionMismatchException) { refused = true; }

                if (!refused)
                {
                    log.AppendLine("  FAIL Load did not throw SaveVersionMismatchException for a " +
                                   $"save at version {record.FormatVersion} against current " +
                                   $"{SaveRecord.CurrentFormatVersion}");
                    bad++;
                }

                log.AppendLine($"  version refusal: a save at version {record.FormatVersion} " +
                               $"against current {SaveRecord.CurrentFormatVersion} is refused, " +
                               $"not misloaded  {(bad == 0 ? "ok" : "FAILED")}");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
            return bad;
        }
    }
}
#endif
