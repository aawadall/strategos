// Replayer.cs
// The single definition of what "replay" means in this project.
//
// #94: there was no production replay path at all. Replay existed only as a shape reimplemented
// privately inside CommandProbe.CheckReplayDivergence — a Dictionary<int, List<Command>> built
// from CommandLog.Entries, fed into a fresh Simulation one tick behind, compared by Signature().
// That meant the project's only replay guarantee was guarding a mechanism nothing shipped. This
// file is what makes replay real: CommandProbe now calls it instead of keeping its own copy, and
// so does anything else that ever needs to replay a run (#74 among them).
//
// CONSUMES BOTH LOGS. A command alone is not everything that happened: #94 found that
// Simulation.AcknowledgeDirective — a player action — touched no log CommandLog-only replay
// could see, so a replayed run never acknowledged anything the original had, and the two
// Signature()s diverged. DirectiveResponseLog is the fix; this is what reads it back.
//
// PRESERVES THE t-1 DELIVERY DELAY. A command recorded at Tick T was issued while the original
// Simulation's own Tick was still T — one full Step() before its effect became visible, per
// MessageBus's "publish to the next step" rule. The same is true of a directive response: it is
// stamped with the Tick it happened on, mid-loop, before that iteration's Step() call. Replaying
// each at the loop iteration for T+1, before that iteration's Step(), reproduces the same
// timing on both logs alike.
//
// ACKNOWLEDGEMENTS REPLAY THROUGH Simulation.AcknowledgeDirective, NEVER BY RE-PUBLISHING THE
// REPORT DIRECTLY. That method holds the idempotence guard over _acknowledgedDirectives and is
// the only thing that rebuilds it (see its own remarks) — replaying around it would leave a
// replayed run's guard empty even though its ReportLog matched, which is exactly the gap #74
// needs closed to reconstruct state from a load.

using System.Collections.Generic;
using Strategos.Directives;

namespace Strategos.Commands
{
    public static class Replayer
    {
        /// <summary>
        /// Steps <paramref name="target"/> forward <paramref name="steps"/> times, feeding it
        /// only what <paramref name="recorded"/>'s <see cref="Simulation.Log"/> and
        /// <see cref="Simulation.DirectiveResponses"/> hold — nothing live, nothing the two
        /// simulations did not both see.
        /// </summary>
        /// <remarks>
        /// <paramref name="target"/> must be a fresh <see cref="Simulation"/> constructed from
        /// the same scenario as <paramref name="recorded"/>, before either has been stepped —
        /// same starting units, same directive(s) published at construction with the same
        /// <see cref="Directive.Seq"/> assignment. Without that, "the same DirectiveSeq names
        /// the same directive in both runs" stops holding and a replayed acknowledgement would
        /// resolve against the wrong thing, or nothing.
        /// </remarks>
        public static void Run(Simulation recorded, Simulation target, int steps)
        {
            var commandsByTick = GroupByTick(recorded.Log.Entries, static c => c.Tick);
            var responsesByTick = GroupByTick(recorded.DirectiveResponses.Entries, static r => r.Tick);

            for (int t = 1; t <= steps; t++)
            {
                int due = t - 1;

                // Commands in the recorded log's own order — that order is the authority, same
                // as CommandProbe's replay did before this file existed.
                if (commandsByTick.TryGetValue(due, out var commands))
                    foreach (var c in commands) target.Issue(c);

                // Responses in the same tick, in the order DirectiveResponseLog recorded them —
                // matters only when more than one directive is answered on the same tick, and
                // preserved because ReportLog.Append order is part of Signature().
                if (responsesByTick.TryGetValue(due, out var responses))
                    foreach (var r in responses) Apply(target, r);

                target.Step();
            }
        }

        private static void Apply(Simulation target, DirectiveResponse response)
        {
            // Acknowledge is the only kind that exists — #73 ships no DirectiveRefused, so
            // there is nothing else a DirectiveResponseKind can be today. Add the branch when
            // that lands, not before; see DirectiveResponseLog.cs.
            if (response.Kind != DirectiveResponseKind.Acknowledged) return;

            var directive = target.DirectiveLog.FindBySeq(response.DirectiveSeq);
            if (directive.HasValue) target.AcknowledgeDirective(directive.Value);
        }

        /// <summary>
        /// Groups a log's entries by tick, preserving each tick's own recorded order.
        /// </summary>
        /// <remarks>
        /// A Dictionary is used here purely as a keyed lookup (TryGetValue against a tick we
        /// already know), never enumerated — the same distinction
        /// docs/simulation-invariants.md draws for <c>_acknowledgedDirectives</c> and
        /// <c>_openingReported</c>. This method builds the dictionary once per replay by
        /// walking each log's own <c>List</c> in order, which is exactly as deterministic as the
        /// log itself.
        /// </remarks>
        private static Dictionary<int, List<T>> GroupByTick<T>(IReadOnlyList<T> entries,
            System.Func<T, int> tickOf)
        {
            var byTick = new Dictionary<int, List<T>>();
            for (int i = 0; i < entries.Count; i++)
            {
                int tick = tickOf(entries[i]);
                if (!byTick.TryGetValue(tick, out var list))
                    byTick[tick] = list = new List<T>();
                list.Add(entries[i]);
            }
            return byTick;
        }
    }
}
