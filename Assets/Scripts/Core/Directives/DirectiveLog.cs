// DirectiveLog.cs
// The append-only record of every directive published. Exact shape of CommandLog/ReportLog —
// see those files for the reasoning this only restates in brief.
//
// NOTHING IS EVER REWRITTEN. Acknowledging a directive appends a SituationReport elsewhere
// (Simulation.AcknowledgeDirective); it does not touch this log. The same rule
// command-architecture.md:199 states for Abort applies here without qualification: a directive
// entry, once appended, is a fact about what was ordered from higher and stays exactly as
// received.

using System.Collections.Generic;
using System.Text;

namespace Strategos.Directives
{
    public sealed class DirectiveLog
    {
        private readonly List<Directive> _entries = new();

        /// <summary>Next sequence number to be assigned. Starts at 1; 0 means "unassigned".</summary>
        private ulong _nextSeq = 1;

        public int Count => _entries.Count;
        public IReadOnlyList<Directive> Entries => _entries;
        public Directive this[int i] => _entries[i];

        /// <summary>
        /// Stamps a directive with the next sequence number and records it.
        ///
        /// Sequence assignment lives here rather than at the publisher so there is exactly one
        /// writer and the total order cannot depend on who happened to publish first — same
        /// rule as CommandLog.Append and ReportLog.Append. Does not touch Id, which is
        /// author-assigned scenario data and stable before this is ever called.
        /// </summary>
        public Directive Append(Directive directive)
        {
            directive.Seq = _nextSeq++;
            _entries.Add(directive);
            return directive;
        }

        public void Clear()
        {
            _entries.Clear();
            _nextSeq = 1;
        }

        /// <summary>
        /// Deterministic signature of everything published, for the divergence test — folded
        /// into Simulation.Signature() the same way ReportLog.Signature() is.
        ///
        /// Covers the identifying and structural fields a replay must reproduce, not the free
        /// text (From/Intent/Constraints) that merely describes them — the same split
        /// ReportLog.Signature() and CommandQueue.AppendSignature make, which skip
        /// Command.DrillCode for the identical reason.
        /// </summary>
        public string Signature()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _entries.Count; i++)
            {
                var d = _entries[i];
                sb.Append(d.Id).Append(':')
                  .Append(d.Seq).Append(':')
                  .Append(d.Tick).Append(':')
                  .Append(d.TargetUnit.Value).Append(':')
                  .Append(d.DeadlineTick).Append(':');

                if (d.ObjectiveIds != null)
                    for (int o = 0; o < d.ObjectiveIds.Length; o++)
                        sb.Append(d.ObjectiveIds[o]).Append(',');

                sb.Append(';');
            }
            return sb.ToString();
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
