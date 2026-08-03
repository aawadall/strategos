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

        /// <summary>
        /// Hold this ground. A *state*, not a task: it never completes on its own.
        /// </summary>
        /// <remarks>
        /// **This was a control command and is now a world one**, which is the resolution of
        /// the complaint that Hold was a byte-for-byte copy of Abort. As a control command it
        /// emptied the queue and was therefore indistinguishable in effect from throwing the
        /// plan away; as a world command it occupies the queue, sets a posture and gives the
        /// unit something it is doing. "Stay here and keep watching" is a task, and a task
        /// needs an executor.
        ///
        /// Numbered in the world range deliberately — <see cref="CommandKindExtensions.IsControl"/>
        /// keys off the value, so the number is the declaration.
        /// </remarks>
        Defend = 3,

        /// <summary>
        /// Carry out a drill by its code.
        /// </summary>
        /// <remarks>
        /// **Expands rather than executes.** Like an order to a formation, a drill never
        /// reaches an executor: it is unpacked at delivery into the orders its steps become,
        /// each issued through the same path so the log records the drill *and* what it became
        /// — which is what lets a replay reconstruct "the commander called 2" separately from
        /// the three orders that answered it.
        /// </remarks>
        Drill = 4,

        /// <summary>
        /// Hold this ground to observe and report. A state like Defend, but it does not dig in
        /// — the payoff is detection reach, not incoming-fire reduction.
        /// </summary>
        Screen = 5,

        /// <summary>
        /// Hold a line — dig in, and keep a modest watch. Heavier than Screen, lighter than Cover.
        /// </summary>
        Guard = 6,

        // ─── Control commands (act on the queue, resolve at once) ───
        Abort = 100,
        CancelFrom = 101,
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
        /// Which drill, for <see cref="CommandKind.Drill"/>. The code a commander transmits.
        /// </summary>
        /// <remarks>
        /// The code rather than a resolved drill object, because the code is the thing that
        /// travels: it is what goes over a radio, what a player types, and what survives in a
        /// log that has to be readable a year later. Resolving it at delivery also means a
        /// replay uses whatever the library says now, which is the same rule scenarios follow.
        /// </remarks>
        public string DrillCode;

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

        /// <summary>Carry out a drill, by code. The addressee may be a formation.</summary>
        public static Command Drill(ActorId by, UnitId unit, string code, int tick = 0) => new()
        {
            Tick = tick, IssuedBy = by, TargetUnit = unit,
            Kind = CommandKind.Drill, DrillCode = code,
        };

        public static Command CancelFrom(ActorId by, UnitId unit, int index, int tick = 0) => new()
        {
            Tick = tick, IssuedBy = by, TargetUnit = unit,
            Kind = CommandKind.CancelFrom, Index = index,
        };

        /// <summary>
        /// Hold where you stand. Kept as a named constructor because it is what a player asks
        /// for, and because HOLD is a button; it produces a Defend.
        /// </summary>
        public static Command Hold(ActorId by, UnitId unit, int tick = 0) =>
            Defend(by, unit, tick);

        /// <summary>Occupy and hold the ground the unit is on.</summary>
        public static Command Defend(ActorId by, UnitId unit, int tick = 0) => new()
        {
            Tick = tick, IssuedBy = by, TargetUnit = unit, Kind = CommandKind.Defend,
        };

        /// <summary>Hold where you stand to observe — Screen, not Defend.</summary>
        public static Command Screen(ActorId by, UnitId unit, int tick = 0) => new()
        {
            Tick = tick, IssuedBy = by, TargetUnit = unit, Kind = CommandKind.Screen,
        };

        /// <summary>Hold a line — dig in and keep watch.</summary>
        public static Command Guard(ActorId by, UnitId unit, int tick = 0) => new()
        {
            Tick = tick, IssuedBy = by, TargetUnit = unit, Kind = CommandKind.Guard,
        };

        public override string ToString()
        {
            string what = Kind switch
            {
                CommandKind.MoveTo => $"MoveTo({TargetCell.x:0.#},{TargetCell.y:0.#})",
                CommandKind.Engage => $"Engage({AgainstUnit})",
                CommandKind.CancelFrom => $"CancelFrom({Index})",
                CommandKind.Drill => $"Drill({DrillCode})",
                _ => Kind.ToString(),
            };
            return $"#{Seq} t{Tick} {IssuedBy}->{TargetUnit} {what}";
        }
    }
}
