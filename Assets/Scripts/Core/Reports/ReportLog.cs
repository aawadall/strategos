// ReportLog.cs
// The append-only record of every report published.
//
// The counterpart to CommandLog: that one answers "what was this force ever told", this one
// answers "what did it ever know". An after-action review needs both, and the interesting
// question it exists to answer is the gap between them — the contact that was reported at
// T+412 and acted on at T+455.
//
// NOTHING IS EVER REWRITTEN, same as the command log. A ContactLost appends; it does not
// retract the Contact that preceded it. A report that was believed and then withdrawn is a
// fact about what the commander knew at the time, and erasing it would make the replay tidier
// and the review useless.
//
// NOT a duplicate of CommandLog with the type changed. Both were considered for a shared
// generic and it is not worth it: Append has to stamp a field and From has to read one, so a
// generic version needs either an interface (boxing, on a struct, per entry) or a pair of
// abstract accessors — more machinery than the sixty lines it would save. The *bus* is
// generic because its rules are subtle and drift silently; a log that appends to a list is
// neither.

using System.Collections.Generic;
using System.Text;

namespace Strategos.Reports
{
    public sealed class ReportLog
    {
        private readonly List<SituationReport> _entries = new();

        /// <summary>Next sequence number to be assigned. Starts at 1; 0 means "unassigned".</summary>
        private ulong _nextSeq = 1;

        public int Count => _entries.Count;
        public IReadOnlyList<SituationReport> Entries => _entries;
        public SituationReport this[int i] => _entries[i];

        /// <summary>
        /// Stamps a report with the next sequence number and records it.
        ///
        /// Sequence assignment lives here rather than at the publisher so there is exactly one
        /// writer and the total order cannot depend on who happened to publish first.
        /// </summary>
        public SituationReport Append(SituationReport report)
        {
            report.Seq = _nextSeq++;
            _entries.Add(report);
            return report;
        }

        /// <summary>Everything published at or after <paramref name="tick"/>.</summary>
        public IEnumerable<SituationReport> From(int tick)
        {
            foreach (var r in _entries)
                if (r.Tick >= tick) yield return r;
        }

        /// <summary>The most recent <paramref name="max"/> entries, newest first.</summary>
        public IEnumerable<SituationReport> Recent(int max)
        {
            int start = _entries.Count - 1;
            for (int i = start; i >= 0 && start - i < max; i--) yield return _entries[i];
        }

        public void Clear()
        {
            _entries.Clear();
            _nextSeq = 1;
        }

        /// <summary>
        /// Deterministic signature of everything published, for the divergence test.
        ///
        /// Covers the fields a replay must reproduce and not the ones that merely describe
        /// them: two runs that produce the same contacts in the same order at the same ticks
        /// have not diverged, whatever their entries stringify as.
        /// </summary>
        public string Signature()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _entries.Count; i++)
            {
                var r = _entries[i];
                sb.Append(r.Seq).Append(':')
                  .Append(r.Tick).Append(':')
                  .Append(r.ObservedTick).Append(':')
                  .Append(r.Source.Value).Append(':')
                  .Append((int)r.Kind).Append(':')
                  .Append((int)r.Confidence).Append(':')
                  .Append(r.Subject.Value).Append(':')
                  .Append(r.Cell.x.ToString("F4")).Append(',')
                  .Append(r.Cell.y.ToString("F4")).Append(':')
                  .Append(r.AboutCommand).Append(';');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Human-readable dump, oldest first. Useful in a probe failure and in an
        /// after-action review; not a serialisation format.
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
