// CommandLog.cs
// The append-only record of every order issued.
//
// The ordered command stream IS the log — there is no separate recording mechanism. That one
// fact is what makes four later things possible from one piece of work:
//
//   Replay              re-read the stream
//   Multiplayer         ship the stream (Phase 7)
//   AI training         expose the stream at a process boundary (Phase 8)
//   After-action review read the stream
//
// NOTHING IS EVER REWRITTEN. Abort appends an entry; it does not remove one. Replaying
// therefore reconstructs the fact that a plan was cut short and when, which is precisely what
// an after-action review needs in order to answer why a unit stopped short of its objective.

using System.Collections.Generic;
using System.Text;

namespace Strategos.Commands
{
    public sealed class CommandLog
    {
        private readonly List<Command> _entries = new();

        /// <summary>Next sequence number to be assigned. Starts at 1; 0 means "unassigned".</summary>
        private ulong _nextSeq = 1;

        public int Count => _entries.Count;
        public IReadOnlyList<Command> Entries => _entries;
        public Command this[int i] => _entries[i];

        /// <summary>
        /// Stamps a command with the next sequence number and records it.
        ///
        /// Sequence assignment lives here rather than at the publisher so there is exactly one
        /// writer and the total order cannot depend on who happened to publish first.
        /// </summary>
        public Command Append(Command command)
        {
            command.Seq = _nextSeq++;
            _entries.Add(command);
            return command;
        }

        /// <summary>Everything issued at or after <paramref name="tick"/>.</summary>
        public IEnumerable<Command> From(int tick)
        {
            foreach (var c in _entries)
                if (c.Tick >= tick) yield return c;
        }

        public void Clear()
        {
            _entries.Clear();
            _nextSeq = 1;
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
