// SituationReport.cs
// An observation, as data. The other direction of the C2 loop.
//
// Commands travel down to an addressee; reports travel up and outward to whoever is
// listening. See docs/command-architecture.md for the reasoning; this file implements the
// message shape described there.
//
// Same shape decision as Command, for the same reason: a flat struct with a Kind
// discriminator rather than a class hierarchy. Polymorphic serialisation is a liability in a
// log that has to round-trip identically, and the payload is small enough that a few unused
// fields cost less than the machinery to avoid them.
//
// THIS IS THE SEAM AUTONOMOUS REACTION PLUGS INTO. A unit that asks "is there a hostile in
// range?" reads world state and can never be deceived, delayed or jammed. A unit that asks
// "have I received a contact report?" needs no change at all when reports start arriving
// late, wrong, or forged. That is why detection publishes rather than being polled, and it is
// the one rule in #15 that constrains everything downstream of it.

using System;
using UnityEngine;
using Strategos.Units;

namespace Strategos.Reports
{
    /// <summary>
    /// What a report is about.
    ///
    /// Numbered in groups the way <see cref="Commands.CommandKind"/> is: observations of
    /// somebody else in the 1s, a unit's own status in the 10s, and the 20s reserved for
    /// things nothing publishes yet. The grouping is not load-bearing today — it is so a
    /// consumer can filter by category without enumerating every member it does not know
    /// about, which is what a report feed and an intelligence layer both want.
    /// </summary>
    public enum ReportKind
    {
        None = 0,

        // ─── Observation: something was seen ───
        Contact = 1,
        ContactLost = 2,

        // ─── Own status: a unit's plan moved on ───
        Arrived = 10,
        OrderCompleted = 11,
        OrderFailed = 12,
        Halted = 13,

        // ─── Combat ───
        /// <summary>Fire has been opened. Published once per engagement, not per tick.</summary>
        Engaged = 20,
        /// <summary>Out of ammunition.</summary>
        Depleted = 21,
        /// <summary>Combat power spent. The unit remains in the scenario and does nothing.</summary>
        Destroyed = 22,
    }

    /// <summary>
    /// How sure the source is.
    ///
    /// Ascending, so <c>&gt;=</c> reads correctly and a fusion pass can take the maximum of
    /// several sources without a lookup table. Carries no behaviour yet — see the note on
    /// <see cref="SituationReport.Confidence"/>.
    /// </summary>
    public enum Confidence
    {
        Possible = 0,
        Suspected = 1,
        Confirmed = 2,
    }

    [Serializable]
    public struct SituationReport
    {
        /// <summary>
        /// Total order. A sequence number, **not** a timestamp — same rule as
        /// <see cref="Commands.Command.Seq"/>, and assigned by the log rather than the
        /// publisher so there is exactly one writer.
        /// </summary>
        public ulong Seq;

        /// <summary>Simulation step the report was published on.</summary>
        public int Tick;

        /// <summary>Who observed it.</summary>
        public UnitId Source;

        /// <summary>
        /// When it was observed. Equal to <see cref="Tick"/> today, because a unit reports the
        /// instant it sees something.
        ///
        /// Separate from Tick from the start because under C3 they come apart: a report
        /// delayed by distance, echelon or a jammed net arrives at a Tick later than the
        /// ObservedTick it carries, and a commander acting on a five-minute-old contact is the
        /// entire point of modelling the delay. A field that is currently a copy is cheap; a
        /// message format that cannot express staleness has to be changed everywhere at once.
        /// </summary>
        public int ObservedTick;

        public ReportKind Kind;

        /// <summary>
        /// How sure the source is. Always <see cref="Reports.Confidence.Confirmed"/> today —
        /// direct observation at range, with no misidentification model to be unsure about.
        ///
        /// Present now because Phase 5.3 fuses multiple sources, and a report that does not
        /// record how sure its source was cannot be fused afterwards.
        /// </summary>
        public Confidence Confidence;

        // ─── Payload ──────────────────────────────────────────────────────────

        /// <summary>
        /// Who the report is about: the observed unit for a contact, the source itself for a
        /// status report.
        ///
        /// **This is ground truth, and fog of war will have to change that.** A contact report
        /// naming the real <see cref="UnitId"/> hands the recipient a perfect identification
        /// of something it has merely seen at 1500 m. When identification becomes a thing that
        /// can be wrong, this field should become a *track* id — a thing the observer believes
        /// in — with the real unit resolvable only by the simulation. Recorded here rather
        /// than discovered later, because every consumer written against a truthful Subject is
        /// a consumer that has to be revisited.
        /// </summary>
        public UnitId Subject;

        /// <summary>The subject's side. Same ground-truth caveat as <see cref="Subject"/>.</summary>
        public SideId SubjectSide;

        /// <summary>Where, in fractional cell coordinates — matching MapData throughout.</summary>
        public Vector2 Cell;

        /// <summary>
        /// The command this concerns, by <see cref="Commands.Command.Seq"/>, for the status
        /// kinds. Zero when the report is not about an order.
        ///
        /// A sequence number rather than the command itself: the log already holds the order,
        /// and copying it in would give an after-action review two versions of one fact.
        /// </summary>
        public ulong AboutCommand;

        // ─── Construction ─────────────────────────────────────────────────────

        public static SituationReport Contact(UnitId source, UnitInstance subject, int tick) => new()
        {
            Tick = tick, ObservedTick = tick, Source = source,
            Kind = ReportKind.Contact, Confidence = Confidence.Confirmed,
            Subject = subject.Id, SubjectSide = subject.Side, Cell = subject.Cell,
        };

        public static SituationReport ContactLost(UnitId source, UnitInstance subject, int tick) => new()
        {
            Tick = tick, ObservedTick = tick, Source = source,
            Kind = ReportKind.ContactLost, Confidence = Confidence.Confirmed,
            Subject = subject.Id, SubjectSide = subject.Side, Cell = subject.Cell,
        };

        /// <summary>
        /// Fire opened on a hostile. Published once per engagement, not once per tick — a
        /// twenty-minute firefight is one report, the same edges-not-state rule contacts obey.
        /// </summary>
        public static SituationReport Engaged(UnitId source, UnitInstance subject, int tick,
            ulong aboutCommand = 0) => new()
        {
            Tick = tick, ObservedTick = tick, Source = source,
            Kind = ReportKind.Engaged, Confidence = Confidence.Confirmed,
            Subject = subject.Id, SubjectSide = subject.Side, Cell = subject.Cell,
            AboutCommand = aboutCommand,
        };

        /// <summary>A unit reporting on itself: arrival, completion, failure, a halt.</summary>
        public static SituationReport Status(ReportKind kind, UnitInstance unit, int tick,
            ulong aboutCommand = 0) => new()
        {
            Tick = tick, ObservedTick = tick, Source = unit.Id,
            Kind = kind, Confidence = Confidence.Confirmed,
            Subject = unit.Id, SubjectSide = unit.Side, Cell = unit.Cell,
            AboutCommand = aboutCommand,
        };

        /// <summary>True for the kinds that describe somebody else rather than the source.</summary>
        public bool IsObservation =>
            Kind == ReportKind.Contact ||
            Kind == ReportKind.ContactLost ||
            Kind == ReportKind.Engaged;

        public override string ToString()
        {
            string what = IsObservation
                ? $"{Kind}({Subject})"
                : Kind.ToString();
            string when = ObservedTick == Tick ? string.Empty : $" obs t{ObservedTick}";
            return $"#{Seq} t{Tick} {Source}: {what} @ ({Cell.x:0.#},{Cell.y:0.#}){when}";
        }
    }
}
