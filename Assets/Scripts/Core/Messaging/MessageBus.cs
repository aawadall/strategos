// MessageBus.cs
// A topic: messages published here, delivered to subscribers a step later.
//
// This file is where three of the four delivery rules from docs/command-architecture.md are
// actually enforced. They are not style preferences — they decide whether the bus enables
// replay and lockstep or quietly prevents them.
//
//   1. Publish to the NEXT step. A message published during step N is delivered at N+1,
//      never within the step that produced it.
//   2. Dispatch SYNCHRONOUSLY in a declared, stable order.
//   3. Only the owner mutates. Not enforceable by a compiler; enforced by convention and
//      documented at every subscriber.
//   4. Subscribe for events, read for state — a rule for subscribers, not for the bus.
//
// Rule 1 is the one that looks like an inconvenience and is not. Without it, a report can
// trigger a reaction that publishes a command that triggers another report, all inside one
// step: unbounded, and ordered by whatever the dispatcher happened to reach first. With it,
// cascades are bounded and ordering is a property of the design rather than of the call
// stack. It is also the degenerate case of the order propagation delay Phase 5.2 wants — one
// step everywhere today, a function of echelon, distance and terrain later.
//
// GENERIC ON PURPOSE. The architecture has two topics — commands down, reports up — obeying
// the same four rules. Two hand-written copies would be two places for "publish to the next
// step" to drift, and a drift here does not throw: it produces a replay that diverges some
// number of steps after the change that caused it. One implementation, two instantiations.

using System;
using System.Collections.Generic;

namespace Strategos.Messaging
{
    /// <summary>
    /// A subscriber to a topic.
    ///
    /// Ordered by <see cref="Order"/>, low first, and ties broken by registration order, so
    /// dispatch is stable across runs and machines. Never sort subscribers by anything
    /// derived from a hash or a reference — that is exactly how a replay diverges.
    /// </summary>
    public sealed class Subscriber<T>
    {
        public string Name;
        public int Order;
        public Action<T> OnMessage;
    }

    public abstract class MessageBus<T>
    {
        private readonly List<Subscriber<T>> _subscribers = new();

        // Two buffers, swapped each step. Anything published while dispatching lands in the
        // next one, which is what makes rule 1 structural rather than a promise.
        private List<T> _inbox = new();
        private List<T> _delivering = new();

        private bool _dispatching;

        /// <summary>What this topic is called, for the re-entry message. Not behaviour.</summary>
        protected abstract string TopicName { get; }

        /// <summary>Messages awaiting delivery at the next step.</summary>
        public int PendingCount => _inbox.Count;

        /// <summary>Total messages delivered since construction. Diagnostic only.</summary>
        public int DeliveredCount { get; private set; }

        /// <summary>
        /// Messages published but not yet delivered, in publish order — the same order
        /// <see cref="Deliver"/> would hand them to subscribers.
        /// </summary>
        /// <remarks>
        /// #74 (save/load): a message published during the step a snapshot is taken sits here,
        /// invisible to <c>Simulation.Signature()</c> and to every log — it was published, so
        /// <c>CommandLog</c>/<c>ReportLog</c>/<c>DirectiveLog</c> already hold it, but it has not
        /// reached <see cref="Deliver"/> yet, so no consumer has acted on it. A snapshot that
        /// drops this loses exactly one step of in-flight orders/reports/directives on restore —
        /// invisible at the moment of restore and visible only once <see cref="Deliver"/> should
        /// have run and did not. Read-only: callers must not mutate the returned list.
        /// </remarks>
        public IReadOnlyList<T> Pending => _inbox;

        /// <summary>
        /// Replaces the pending inbox wholesale. Restore-only: for a live topic mid-dispatch,
        /// or for accumulating publishes normally, use <see cref="Publish"/>.
        /// </summary>
        public void LoadPending(IEnumerable<T> messages)
        {
            _inbox.Clear();
            if (messages != null) _inbox.AddRange(messages);
        }

        public void Subscribe(string name, int order, Action<T> handler)
        {
            if (handler == null) return;
            _subscribers.Add(new Subscriber<T> { Name = name, Order = order, OnMessage = handler });

            // Insertion sort by Order, preserving registration order within a tier. A stable
            // sort matters: List.Sort is introsort and is NOT stable, so equal orders could
            // permute between runs.
            for (int i = _subscribers.Count - 1; i > 0; i--)
            {
                if (_subscribers[i - 1].Order <= _subscribers[i].Order) break;
                (_subscribers[i - 1], _subscribers[i]) = (_subscribers[i], _subscribers[i - 1]);
            }
        }

        /// <summary>
        /// Queues a message for delivery at the next step.
        ///
        /// Never delivers inline, even when called from outside a dispatch — the timing has to
        /// be the same whoever publishes, or a command issued by the UI would arrive a step
        /// earlier than one issued by a reacting unit, and replay would depend on who asked.
        /// </summary>
        public void Publish(in T message) => _inbox.Add(message);

        /// <summary>
        /// Delivers everything published before this step, to every subscriber in order.
        ///
        /// Messages published during this call go to the next step's inbox.
        /// </summary>
        public void Deliver()
        {
            if (_dispatching)
                throw new InvalidOperationException(
                    $"{TopicName}.Deliver re-entered. A subscriber must publish, not deliver.");

            // Swap rather than copy, so publishing during dispatch cannot touch what is being
            // delivered.
            (_inbox, _delivering) = (_delivering, _inbox);

            if (_delivering.Count == 0) return;

            _dispatching = true;
            try
            {
                // Message order first, subscriber order within each message: every subscriber
                // sees message N before any subscriber sees N+1. The alternative — each
                // subscriber draining the whole batch — makes a subscriber's view of ordering
                // depend on its position in the list.
                for (int m = 0; m < _delivering.Count; m++)
                {
                    var message = _delivering[m];
                    for (int s = 0; s < _subscribers.Count; s++)
                        _subscribers[s].OnMessage(message);
                    DeliveredCount++;
                }
            }
            finally
            {
                _dispatching = false;
                _delivering.Clear();
            }
        }

        /// <summary>Drops undelivered messages. For resetting between replays.</summary>
        public void Reset()
        {
            _inbox.Clear();
            _delivering.Clear();
            DeliveredCount = 0;
        }
    }
}
