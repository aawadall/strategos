// Command.cs
// An order, as data.
//
// See docs/command-architecture.md for the reasoning; this file implements the message shape
// described there. The short version of why an order is a message rather than a method call:
// four separate things need to observe or inject orders — drawing plans in progress, replay
// of saved battles, online multiplayer, and AI issuing orders through the same path a player
// does. Each is painful to retrofit onto direct calls and nearly free if an order is data.
//
// A flat struct with a Kind discriminator, not a class hierarchy. Polymorphic serialisation
// is a liability in a log that has to round-trip identically, and the payload is small enough
// that a few unused fields cost less than the machinery to avoid them.

using System;
using UnityEngine;
using Strategos.Units;

namespace Strategos.Commands
{
    /// <summary>
    /// Who issued an order. A side today; a specific player or AI later.
    ///
    /// Its own type rather than reusing SideId because the *commander* and the *side* stop
    /// being the same thing as soon as there is more than one player per side, or an AI
    /// sharing one.
    /// </summary>
    [Serializable]
    public struct ActorId : IEquatable<ActorId>
    {
        public int Value;

        public ActorId(int value) => Value = value;

        public static ActorId None => new(0);
        public bool IsValid => Value != 0;

        /// <summary>The actor commanding a given side, while that mapping is one to one.</summary>
        public static ActorId ForSide(SideId side) => new(side.Value);

        public bool Equals(ActorId other) => Value == other.Value;
        public override bool Equals(object o) => o is ActorId a && Equals(a);
        public override int GetHashCode() => Value;
        public override string ToString() => IsValid ? $"A{Value}" : "A-none";

        public static bool operator ==(ActorId a, ActorId b) => a.Value == b.Value;
        public static bool operator !=(ActorId a, ActorId b) => a.Value != b.Value;
    }

    /// <summary>
    /// What an order is.
    ///
    /// World commands act on the world and take time; control commands act on the *queue* and
    /// resolve immediately. The numbering keeps the two groups visibly apart — see
    /// <see cref="CommandKindExtensions.IsControl"/>. Separating them in the type system is
    /// what stops cancellation being threaded as a special case through every executor.
    /// </summary>
    public enum CommandKind
    {
        None = 0,

        // ─── World commands (take time, need an executor) ───
        MoveTo = 1,
        Engage = 2,

        // ─── Control commands (act on the queue, resolve at once) ───
        Abort = 100,
        CancelFrom = 101,
        Hold = 102,
    }

    public static class CommandKindExtensions
    {
        /// <summary>True for commands that operate on a queue rather than on the world.</summary>
        public static bool IsControl(this CommandKind kind) => (int)kind >= 100;
    }

    [Serializable]
    public struct Command
    {
        /// <summary>
        /// Total order. A sequence number, **not** a timestamp: wall-clock ordering differs
        /// between machines and defeats replay. Assigned by the log, not by the publisher.
        /// </summary>
        public ulong Seq;

        /// <summary>Simulation step the order takes effect on.</summary>
        public int Tick;

        /// <summary>
        /// Who ordered it. Earns its place today without any C3 justification: replay needs
        /// to answer "who ordered this", and multiplayer needs to know which player did.
        /// </summary>
        public ActorId IssuedBy;

        /// <summary>Addressee. <see cref="UnitId.None"/> when addressed to a group.</summary>
        public UnitId TargetUnit;

        /// <summary>
        /// Formation addressee, for later. Reserved now so partitioned routing and order
        /// decomposition are a later optimisation rather than a message-format change.
        /// </summary>
        public int TargetGroup;

        public CommandKind Kind;

        // ─── Payload ──────────────────────────────────────────────────────────

        /// <summary>Destination, for <see cref="CommandKind.MoveTo"/>. Fractional cell space.</summary>
        public Vector2 TargetCell;

        /// <summary>Queue index, for <see cref="CommandKind.CancelFrom"/>.</summary>
        public int Index;

        /// <summary>
        /// The unit being acted *upon*, for <see cref="CommandKind.Engage"/>.
        ///
        /// Distinct from <see cref="TargetUnit"/>, which is the addressee — the one being
        /// told. An engage order has both: "you, fire at him". Collapsing them into one field
        /// would make the addressee ambiguous the moment a command names a second unit, which
        /// is every interesting command from here on.
        /// </summary>
        public UnitId AgainstUnit;

        /// <summary>
        /// Go to the head of the queue rather than the back, interrupting whatever is running.
        ///
        /// For reflexes — a unit fired on mid-march returns fire now and resumes the march
        /// afterwards. On the message rather than implied by the kind, because the same Engage
        /// is a deliberate order when a player issues it and a reflex when a unit does, and
        /// only the issuer knows which.
        /// </summary>
        public bool Preempt;

        // ─── Construction ─────────────────────────────────────────────────────

        public static Command MoveTo(ActorId by, UnitId unit, Vector2 cell, int tick = 0) => new()
        {
            Tick = tick, IssuedBy = by, TargetUnit = unit,
            Kind = CommandKind.MoveTo, TargetCell = cell,
        };

        public static Command Engage(ActorId by, UnitId unit, UnitId against, int tick = 0,
            bool preempt = false) => new()
        {
            Tick = tick, IssuedBy = by, TargetUnit = unit,
            Kind = CommandKind.Engage, AgainstUnit = against, Preempt = preempt,
        };

        public static Command Abort(ActorId by, UnitId unit, int tick = 0) => new()
        {
            Tick = tick, IssuedBy = by, TargetUnit = unit, Kind = CommandKind.Abort,
        };

        public static Command CancelFrom(ActorId by, UnitId unit, int index, int tick = 0) => new()
        {
            Tick = tick, IssuedBy = by, TargetUnit = unit,
            Kind = CommandKind.CancelFrom, Index = index,
        };

        public static Command Hold(ActorId by, UnitId unit, int tick = 0) => new()
        {
            Tick = tick, IssuedBy = by, TargetUnit = unit, Kind = CommandKind.Hold,
        };

        public override string ToString()
        {
            string what = Kind switch
            {
                CommandKind.MoveTo => $"MoveTo({TargetCell.x:0.#},{TargetCell.y:0.#})",
                CommandKind.Engage => $"Engage({AgainstUnit})",
                CommandKind.CancelFrom => $"CancelFrom({Index})",
                _ => Kind.ToString(),
            };
            return $"#{Seq} t{Tick} {IssuedBy}->{TargetUnit} {what}";
        }
    }
}
