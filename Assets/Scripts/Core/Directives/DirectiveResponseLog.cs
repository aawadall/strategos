// DirectiveResponseLog.cs
// The append-only record of a formation's answers to directives from higher.
//
// #94: acknowledging a directive was invisible to replay. DirectiveLog records what was
// ORDERED from above; CommandLog records what was ORDERED to units; neither is the right place
// for what the PLAYER did about a directive, and DirectiveLog's own header says so directly —
// "Acknowledging a directive appends a SituationReport elsewhere... it does not touch this
// log", the same "nothing is ever rewritten" rule CommandLog holds. This is that "elsewhere",
// named and shaped so replay can read it.
//
// NAMED FOR WHAT IT IS, SHAPED FOR WHAT IS COMING. #73 shipped acknowledge-only on purpose —
// "Add DirectiveRefused and its handling together, or not at all" — but if refusal is ever
// added it is a second DirectiveResponseKind in this same log, not a second log and not a
// rename. A log called DirectiveAckLog would force exactly the rename
// docs/build-and-verify.md warns a CommandKind rename already costs this project (content
// migration, every shipped drill re-serialised) the day that happens.
//
// NOTHING IS EVER REWRITTEN, same rule as CommandLog, DirectiveLog and ReportLog. A second
// press of ACKNOWLEDGE on an already-acknowledged directive appends nothing — see
// Simulation.AcknowledgeDirective's idempotence guard — so what lands here is exactly what had
// an effect, in the order it happened.
//
// DELIBERATELY NOT FOLDED INTO Simulation.Signature(). An acknowledgement already produces a
// ReportKind.DirectiveAcknowledged report, which ReportLog.Signature() already covers (it
// includes AboutDirective) — folding this log in as well would double-count the same fact and
// move every archived signature baseline for no new coverage. See Simulation.Signature()'s own
// doc comment and docs/simulation-invariants.md for the same reasoning stated once each.

using System.Collections.Generic;
using System.Text;

namespace Strategos.Directives
{
    /// <summary>
    /// What a <see cref="DirectiveResponse"/> is.
    /// </summary>
    /// <remarks>
    /// ACKNOWLEDGE ONLY, DELIBERATELY — same rule <see cref="Reports.ReportKind"/> states for
    /// <see cref="Reports.ReportKind.DirectiveAcknowledged"/>. Add <c>Refused</c> the day a
    /// caller actually needs it and something reacts to it, not "for later".
    /// </remarks>
    public enum DirectiveResponseKind
    {
        None = 0,
        Acknowledged = 1,
    }

    /// <summary>
    /// One player action taken against a directive, as data — the counterpart to
    /// <see cref="Commands.Command"/> for the directive topic.
    /// </summary>
    public struct DirectiveResponse
    {
        /// <summary>
        /// Total order among responses ever recorded. Assigned by <see cref="DirectiveResponseLog"/>
        /// at append time, never by the caller — same rule every other log in this project holds.
        /// </summary>
        public ulong Seq;

        /// <summary>Simulation step the response was made on.</summary>
        public int Tick;

        /// <summary>
        /// Which directive, by <see cref="Directive.Seq"/> — not <see cref="Directive.Id"/>,
        /// because this is a runtime fact about a runtime-published directive, the same
        /// Seq-not-Id split <see cref="Reports.SituationReport.AboutDirective"/> makes.
        /// </summary>
        public ulong DirectiveSeq;

        public DirectiveResponseKind Kind;

        public override string ToString() => $"#{Seq} t{Tick} {Kind}(directive #{DirectiveSeq})";
    }

    /// <summary>
    /// The append-only record of every <see cref="DirectiveResponse"/>. Exact shape of
    /// <see cref="Commands.CommandLog"/> and <see cref="DirectiveLog"/> — see those files for
    /// the reasoning this only restates in brief.
    /// </summary>
    public sealed class DirectiveResponseLog
    {
        private readonly List<DirectiveResponse> _entries = new();

        /// <summary>Next sequence number to be assigned. Starts at 1; 0 means "unassigned".</summary>
        private ulong _nextSeq = 1;

        public int Count => _entries.Count;
        public IReadOnlyList<DirectiveResponse> Entries => _entries;
        public DirectiveResponse this[int i] => _entries[i];

        /// <summary>
        /// Stamps a response with the next sequence number and records it.
        ///
        /// Sequence assignment lives here rather than at the caller so there is exactly one
        /// writer and the total order cannot depend on who happened to call first — same rule
        /// as <see cref="Commands.CommandLog.Append"/>, <see cref="DirectiveLog.Append"/> and
        /// <see cref="Reports.ReportLog.Append"/>.
        /// </summary>
        public DirectiveResponse Append(DirectiveResponse response)
        {
            response.Seq = _nextSeq++;
            _entries.Add(response);
            return response;
        }

        public void Clear()
        {
            _entries.Clear();
            _nextSeq = 1;
        }

        /// <summary>
        /// Replaces the log wholesale with entries that already carry their own <see cref="DirectiveResponse.Seq"/>
        /// — restore-only. Recomputes <c>_nextSeq</c> from the highest one found, the same reason
        /// <see cref="Commands.CommandLog.RestoreEntries"/> does. #74: this is also what lets a
        /// restored <see cref="Commands.Simulation"/> rebuild <c>_acknowledgedDirectives</c>
        /// without re-publishing a <see cref="Reports.ReportKind.DirectiveAcknowledged"/> report
        /// that the restored <c>ReportLog</c> already holds.
        /// </summary>
        public void RestoreEntries(IEnumerable<DirectiveResponse> entries)
        {
            _entries.Clear();
            ulong maxSeq = 0;
            if (entries != null)
                foreach (var e in entries)
                {
                    _entries.Add(e);
                    if (e.Seq > maxSeq) maxSeq = e.Seq;
                }
            _nextSeq = maxSeq + 1;
        }

        /// <summary>
        /// Human-readable dump, oldest first. Useful in a probe failure; not a serialisation
        /// format.
        /// </summary>
        public string Dump(int max = 64)
        {
            var sb = new StringBuilder();
            int n = _entries.Count < max ? _entries.Count : max;
            for (int i = 0; i < n; i++) sb.Append("    ").AppendLine(_entries[i].ToString());
            if (_entries.Count > n) sb.Append("    ... ").Append(_entries.Count - n).AppendLine(" more");
            return sb.ToString();
        }
    }
}
