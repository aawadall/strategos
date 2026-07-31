// CommandQueue.cs
// One unit's plan: the orders it has been given and not yet finished.
//
// The topic is the transport; this is the plan. A unit takes the commands addressed to it off
// the bus and appends them here, and command n+1 begins when n completes.
//
// The queue holds only live entries — pending and executing. Completed and cancelled ones are
// dropped, because the queue answers "what is this unit going to do" and the *log* answers
// "what was it ever told". Keeping finished orders here would make every reader filter them
// and would slowly turn the plan into a history.

using System.Collections.Generic;
using UnityEngine;

namespace Strategos.Commands
{
    public enum CommandStatus
    {
        Pending = 0,
        Executing = 1,
        Completed = 2,
        Cancelled = 3,
        Failed = 4,
    }

    /// <summary>A command in a queue, with how far it has got.</summary>
    public struct QueuedCommand
    {
        public Command Command;
        public CommandStatus Status;

        /// <summary>Ticks this command has been executing. Executors use it to pace themselves.</summary>
        public int TicksExecuting;

        public override string ToString() => $"{Command} [{Status}]";
    }

    public sealed class CommandQueue
    {
        private readonly List<QueuedCommand> _entries = new();

        public int Count => _entries.Count;
        public bool IsEmpty => _entries.Count == 0;

        /// <summary>The command being executed, if any. Always index 0 when present.</summary>
        public bool TryPeek(out QueuedCommand entry)
        {
            if (_entries.Count == 0) { entry = default; return false; }
            entry = _entries[0];
            return true;
        }

        public QueuedCommand this[int i] => _entries[i];

        /// <summary>
        /// Read-only view of the live plan, in execution order. This is what a UI should read
        /// to draw an order — subscribe for events, read for state.
        /// </summary>
        public IReadOnlyList<QueuedCommand> Entries => _entries;

        public void Enqueue(in Command command)
        {
            _entries.Add(new QueuedCommand
            {
                Command = command,
                Status = CommandStatus.Pending,
                TicksExecuting = 0,
            });
        }

        /// <summary>
        /// Puts a command at the head of the plan, ahead of whatever was running.
        /// </summary>
        /// <remarks>
        /// This is what makes a reaction a reflex rather than an item on a to-do list. A unit
        /// marching across the map that is fired on has to shoot back *now*; appended to the
        /// back of its queue, it would return fire after finishing a twenty-minute march, by
        /// which time whoever shot at it is elsewhere.
        ///
        /// The displaced command goes back to Pending and keeps its <see cref="
        /// QueuedCommand.TicksExecuting"/>, so it resumes where it left off once the reflex is
        /// done — the player's orders are interrupted, never discarded. That distinction is the
        /// whole reason this exists rather than the reaction issuing an Abort.
        /// </remarks>
        public void InsertFront(in Command command)
        {
            if (_entries.Count > 0)
            {
                var displaced = _entries[0];
                if (displaced.Status == CommandStatus.Executing)
                {
                    displaced.Status = CommandStatus.Pending;
                    _entries[0] = displaced;
                }
            }

            _entries.Insert(0, new QueuedCommand
            {
                Command = command,
                Status = CommandStatus.Pending,
                TicksExecuting = 0,
            });
        }

        /// <summary>Marks the head as executing and returns it. No-op if already executing.</summary>
        public bool TryBegin(out QueuedCommand entry)
        {
            entry = default;
            if (_entries.Count == 0) return false;

            var e = _entries[0];
            if (e.Status == CommandStatus.Pending)
            {
                e.Status = CommandStatus.Executing;
                _entries[0] = e;
            }
            entry = e;
            return e.Status == CommandStatus.Executing;
        }

        /// <summary>Records another tick of execution against the head.</summary>
        public void Tick()
        {
            if (_entries.Count == 0) return;
            var e = _entries[0];
            if (e.Status != CommandStatus.Executing) return;
            e.TicksExecuting++;
            _entries[0] = e;
        }

        /// <summary>Removes the head, having finished one way or another.</summary>
        public void Finish()
        {
            if (_entries.Count > 0) _entries.RemoveAt(0);
        }

        /// <summary>
        /// Cancels the whole plan.
        ///
        /// <paramref name="includeExecuting"/> is the open decision recorded on the issue:
        /// militarily "abort" means stop now, but a unit mid-river-crossing cannot cleanly
        /// freeze. It is a parameter rather than a constant so interruptibility can become a
        /// property of the command without changing every caller.
        /// </summary>
        /// <returns>How many entries were cancelled.</returns>
        public int Abort(bool includeExecuting = true)
        {
            int start = 0;
            if (!includeExecuting && _entries.Count > 0 &&
                _entries[0].Status == CommandStatus.Executing)
                start = 1;

            int cancelled = _entries.Count - start;
            if (cancelled <= 0) return 0;

            _entries.RemoveRange(start, cancelled);
            return cancelled;
        }

        /// <summary>Cancels everything from <paramref name="index"/> onward. A FRAGO at finer grain.</summary>
        public int CancelFrom(int index)
        {
            index = Mathf.Max(0, index);
            if (index >= _entries.Count) return 0;
            int cancelled = _entries.Count - index;
            _entries.RemoveRange(index, cancelled);
            return cancelled;
        }

        public void Clear() => _entries.Clear();

        /// <summary>
        /// Deterministic signature of the live plan, for the divergence test. Includes
        /// everything a replay must reproduce and nothing that merely describes it.
        /// </summary>
        public void AppendSignature(System.Text.StringBuilder sb)
        {
            sb.Append('[');
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                sb.Append((int)e.Command.Kind).Append(':')
                  .Append((int)e.Status).Append(':')
                  .Append(e.TicksExecuting).Append(':')
                  .Append(e.Command.TargetCell.x.ToString("F4")).Append(',')
                  .Append(e.Command.TargetCell.y.ToString("F4")).Append(';');
            }
            sb.Append(']');
        }
    }
}
