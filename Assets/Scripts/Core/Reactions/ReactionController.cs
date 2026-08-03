// ReactionController.cs
// Who decides to fight, and when.
//
// #12 answered what happens when two units fight. This answers the prior question. It is the
// first genuinely autonomous behaviour in the project, so the seam matters more than the
// behaviour does.
//
// REFLEX, NOT INTELLIGENCE. Nothing here plans, manoeuvres or thinks about the enemy. It
// answers three questions per unit per tick — am I being shot at, is there something I am
// allowed to shoot, and should I be leaving — and it issues the same commands a player issues.
// Phase 8 is where an actual AI goes. Keeping this to reflexes is what stops it becoming an
// unbounded AI project.
//
// ═══ THE RULE THIS FILE EXISTS TO OBEY ═══
//
// A UNIT REACTS TO WHAT IT HAS BEEN TOLD, NEVER TO POLLED WORLD STATE. Everything this class
// knows about hostiles comes from situation reports. It never asks "is there an enemy within
// range" against ground truth, and it never reads another unit's position, strength or
// posture.
//
// That is not fastidiousness, it is the difference between C3 being a feature and being a
// rewrite. A unit that polls the world cannot be deceived, delayed or spoofed; when reports
// start arriving late, wrong or forged, reaction logic written that way has to be rebuilt
// rather than wrapped. Written against reports it needs no change at all — a stale contact
// simply *is* a stale belief, and the unit acts on it exactly as a real one would.
//
// The one thing a unit may read directly is ITSELF. Its own strength, suppression and
// ammunition are introspection, not observation: no message has to arrive for a company to
// know it is out of ammunition.

using System.Collections.Generic;
using UnityEngine;
using Strategos.Commands;
using Strategos.Reports;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Reactions
{
    public sealed class ReactionController
    {
        // ─── Break-contact thresholds ─────────────────────────────────────────
        //
        // Constants rather than per-unit fields, deliberately. Cruder than it will eventually
        // need to be, and three numbers a designer can reason about beat twelve nobody can.
        // They become per-capability the moment a unit type wants to be braver than another.

        /// <summary>Strength below which a unit disengages rather than fighting on.</summary>
        public const float BreakStrengthPercent = 35f;

        /// <summary>Ammunition below which a unit has nothing left to fight with.</summary>
        public const float BreakAmmunitionPercent = 5f;

        // SUPPRESSION IS DELIBERATELY NOT A TRIGGER, and the probe is what settled it.
        //
        // The first cut broke contact above 92 suppression, which read as reasonable and was
        // not: sustained fire saturates suppression near 100 within about fifteen seconds, so
        // every unit disengaged almost the moment it was shot at — the probe recorded one
        // leaving at 67.8% strength, barely scratched. No threshold below the cap avoids that,
        // because the cap is where suppression sits whenever anyone is shooting.
        //
        // It is also backwards on its own terms. Suppression models being pinned, and a pinned
        // unit is one that *cannot* move, not one that has decided to leave. Withdrawal under
        // effective fire is the hard case, not the automatic one.
        //
        // So a unit leaves when it is being destroyed or has run dry. Being pinned is what
        // stops it shooting back, which #12 already models.

        /// <summary>
        /// What a unit believes about the enemy. Built from reports and nothing else.
        /// </summary>
        private sealed class Picture
        {
            /// <summary>Hostiles believed present, in the order first reported.</summary>
            public readonly List<UnitId> Contacts = new();

            /// <summary>Where each contact was last reported, parallel to <see cref="Contacts"/>.</summary>
            public readonly List<Vector2> LastSeen = new();

            /// <summary>Units reported firing on this one, in the order first reported.</summary>
            public readonly List<UnitId> Attackers = new();

            public int IndexOf(UnitId id)
            {
                for (int i = 0; i < Contacts.Count; i++) if (Contacts[i] == id) return i;
                return -1;
            }

            public void Saw(UnitId id, Vector2 cell)
            {
                int i = IndexOf(id);
                if (i >= 0) { LastSeen[i] = cell; return; }
                Contacts.Add(id);
                LastSeen.Add(cell);
            }

            public void Lost(UnitId id)
            {
                int i = IndexOf(id);
                if (i < 0) return;
                Contacts.RemoveAt(i);
                LastSeen.RemoveAt(i);
            }

            public void AttackedBy(UnitId id)
            {
                for (int i = 0; i < Attackers.Count; i++) if (Attackers[i] == id) return;
                Attackers.Add(id);
            }
        }

        private readonly Simulation _sim;

        // Keyed by unit id. Lookup only — never iterated — so it cannot affect ordering. The
        // *lists inside* are what get walked, and they are in report order, which is stable.
        private readonly Dictionary<int, Picture> _pictures = new();

        /// <summary>Reports consumed. Diagnostic; the reactions are the interface.</summary>
        public int ReportsSeen { get; private set; }

        /// <summary>Commands this controller has issued on units' own initiative.</summary>
        public int OrdersIssued { get; private set; }

        public ReactionController(Simulation simulation)
        {
            _sim = simulation;

            // Order 50: after the unit layer at 0, which is the only subscriber permitted to
            // mutate anything, and before presentation observers.
            _sim?.Reports.Subscribe("reactions", 50, OnReport);
        }

        // ─── Building the picture ─────────────────────────────────────────────

        private Picture PictureOf(UnitId id)
        {
            if (_pictures.TryGetValue(id.Value, out var p)) return p;
            p = new Picture();
            _pictures[id.Value] = p;
            return p;
        }

        /// <summary>
        /// Folds a delivered report into the believer's picture.
        /// </summary>
        /// <remarks>
        /// Every unit gets its own picture rather than one shared per side. That costs more and
        /// is the point: a side-wide picture is a free intelligence network, and the moment
        /// comms are unreliable it would have to be unpicked. Sharing between units is a
        /// *feature* — Phase 5's reporting chain — not a default.
        /// </remarks>
        private void OnReport(SituationReport report)
        {
            ReportsSeen++;

            switch (report.Kind)
            {
                case ReportKind.Contact:
                    PictureOf(report.Source).Saw(report.Subject, report.Cell);
                    break;

                case ReportKind.ContactLost:
                    PictureOf(report.Source).Lost(report.Subject);
                    break;

                case ReportKind.Engaged:
                    // Being shot at is knowledge the target acquires for free — it does not
                    // need an observer to tell it. The *shooter* is what it may not know, and
                    // recording it here is exactly what makes Return Fire possible without
                    // anyone polling the world.
                    PictureOf(report.Subject).AttackedBy(report.Source);
                    break;

                case ReportKind.Destroyed:
                    // Nothing to shoot at any more. Drop it from every picture rather than
                    // leaving units aiming at a wreck.
                    Forget(report.Subject);
                    break;
            }
        }

        /// <summary>
        /// Removes a unit from every picture.
        ///
        /// Walks the units list rather than the picture dictionary. The result would be the
        /// same either way — removal is order-independent — but "never iterate a Dictionary"
        /// is a rule worth keeping unconditional, because the next person to add a line inside
        /// this loop will not stop to work out whether their line commutes.
        /// </summary>
        private void Forget(UnitId gone)
        {
            var units = _sim.Units;
            for (int u = 0; u < units.Count; u++)
            {
                if (!_pictures.TryGetValue(units[u].Id.Value, out var picture)) continue;

                picture.Lost(gone);

                var attackers = picture.Attackers;
                for (int i = attackers.Count - 1; i >= 0; i--)
                    if (attackers[i] == gone) attackers.RemoveAt(i);
            }
        }

        // ─── Deciding ─────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluates every unit's reflexes for this tick.
        /// </summary>
        /// <remarks>
        /// EVALUATION ORDER IS THE SCENARIO'S UNIT ORDER, and it is chosen rather than
        /// incidental — a declared, stable order is what lets a replay reproduce.
        ///
        /// It also carries no advantage, which is worth stating because the obvious worry is
        /// that whoever reacts first shoots first. It does not work out that way: a reaction
        /// issues a *command*, commands are delivered on the following step, and #12 resolves
        /// every shot in a tick against start-of-tick state. Two units that notice each other
        /// on the same tick open fire on the same tick and damage each other equally. The
        /// ordering here decides only the sequence of entries in the command log.
        /// </remarks>
        public void Evaluate()
        {
            if (_sim == null) return;

            var units = _sim.Units;
            for (int i = 0; i < units.Count; i++) EvaluateUnit(units[i]);
        }

        private void EvaluateUnit(UnitInstance unit)
        {
            if (unit == null || unit.IsDestroyed) return;

            var picture = PictureOf(unit.Id);
            var engagingNow = CurrentTarget(unit);

            // 1. Leaving. Checked first because a unit that should be withdrawing must not be
            //    given a fresh reason to stay. Reads only its own state — introspection, not
            //    observation.
            if (ShouldBreakContact(unit))
            {
                if (engagingNow.IsValid) BreakContact(unit, picture, engagingNow);
                return;
            }

            if (unit.Roe == RulesOfEngagement.HoldFire) return;

            // 2. Return fire. Anyone reported shooting at this unit, in the order they were
            //    reported — the one that opened fire first is the one being answered.
            var target = PickAttacker(picture);

            // 3. Initiative. Only Fire At Will starts something nobody started with it.
            if (!target.IsValid && unit.Roe == RulesOfEngagement.FireAtWill)
                target = PickContact(unit, picture);

            if (!target.IsValid) return;
            if (target == engagingNow) return;   // already doing it

            // Preempt: a unit fired on mid-march shoots back now and resumes the march after,
            // rather than answering when it arrives.
            _sim.Issue(Command.Engage(ActorId.ForSide(unit.Side), unit.Id, target, preempt: true));
            OrdersIssued++;
        }

        /// <summary>
        /// Disengages and withdraws away from the threat.
        /// </summary>
        /// <remarks>
        /// **Ceasing fire is not breaking contact.** The first cut only issued an Abort, and
        /// the probe showed what that is worth: the unit stopped shooting, stayed exactly where
        /// it was, and was destroyed anyway a few seconds later. A unit that has decided it is
        /// losing has to leave.
        ///
        /// The direction comes from where the threat was last *reported*, not from where it
        /// is — the same seam every other decision here uses. A unit withdrawing from a stale
        /// contact runs the wrong way, which is correct behaviour and not a bug to fix later.
        ///
        /// No route check. If the withdrawal is impassable the move fails and the unit is left
        /// halted and disengaged, which is the same place the Abort alone would have left it —
        /// so a failed withdrawal is never worse than not trying.
        /// </remarks>
        private void BreakContact(UnitInstance unit, Picture picture, UnitId threat)
        {
            var actor = ActorId.ForSide(unit.Side);
            _sim.Issue(Command.Abort(actor, unit.Id));
            OrdersIssued++;

            int i = picture.IndexOf(threat);
            if (i < 0) return;   // nothing to run from that this unit knows about

            Vector2 away = unit.Cell - picture.LastSeen[i];
            if (away.sqrMagnitude < 0.0001f) return;   // standing on it; no direction to pick

            var map = _sim.Map;
            Vector2 destination = unit.Cell + away.normalized * WithdrawCells;

            destination.x = Mathf.Clamp(destination.x, 0f, map.Width - 1f);
            destination.y = Mathf.Clamp(destination.y, 0f, map.Height - 1f);

            _sim.Issue(Command.MoveTo(actor, unit.Id, destination));
            OrdersIssued++;
        }

        /// <summary>
        /// How far a withdrawing unit pulls back, in cells.
        ///
        /// Far enough to leave the engagement envelope of most things that could have been
        /// shooting at it, and short enough that it is still a withdrawal rather than a rout
        /// across the map.
        /// </summary>
        public const float WithdrawCells = 40f;

        /// <summary>Own state only. No message has to arrive for a unit to know it is spent.</summary>
        private bool ShouldBreakContact(UnitInstance unit)
        {
            // A Cover order means accept the engagement for the main body. Check the whole
            // queue, not only the head: a return-fire Engage preempts onto the front and must
            // not unlock the break-contact reflex while Cover is still underneath.
            if (HasCoverOrder(unit)) return false;

            return unit.Strength < BreakStrengthPercent ||
                   unit.Supply.Ammunition < BreakAmmunitionPercent;
        }

        private bool HasCoverOrder(UnitInstance unit)
        {
            var queue = _sim.QueueOf(unit.Id);
            if (queue == null || queue.IsEmpty) return false;
            for (int i = 0; i < queue.Count; i++)
                if (queue[i].Command.Kind == CommandKind.Cover) return true;
            return false;
        }

        /// <summary>The unit this one is already engaging under its current plan, or None.</summary>
        private UnitId CurrentTarget(UnitInstance unit)
        {
            var queue = _sim.QueueOf(unit.Id);
            if (queue == null || queue.IsEmpty) return UnitId.None;
            if (!queue.TryPeek(out var head)) return UnitId.None;
            return head.Command.Kind == CommandKind.Engage ? head.Command.AgainstUnit : UnitId.None;
        }

        /// <summary>First still-live unit reported firing on this one.</summary>
        private UnitId PickAttacker(Picture picture)
        {
            for (int i = 0; i < picture.Attackers.Count; i++)
            {
                var candidate = picture.Attackers[i];
                var unit = _sim.UnitOf(candidate);
                if (unit != null && !unit.IsDestroyed) return candidate;
            }
            return UnitId.None;
        }

        /// <summary>
        /// The nearest known contact inside this unit's engagement envelope, **by where it was
        /// last reported** rather than by where it actually is.
        /// </summary>
        /// <remarks>
        /// Using the reported position is the whole seam. It costs nothing today, because
        /// reports are instant and truthful, and it means that when they stop being either,
        /// a unit will fire at where it last believed the enemy to be — which is the correct
        /// behaviour and would otherwise have to be retrofitted into every decision here.
        ///
        /// A live position is deliberately not consulted even though it is one call away.
        /// </remarks>
        private UnitId PickContact(UnitInstance unit, Picture picture)
        {
            var map = _sim.Map;
            if (map == null || picture.Contacts.Count == 0) return UnitId.None;

            float envelopeCells = unit.Capabilities(_sim.Catalogue).EngagementRangeMetres /
                                  Mathf.Max(0.0001f, map.Header.MetresPerCell);

            var best = UnitId.None;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < picture.Contacts.Count; i++)
            {
                var candidate = picture.Contacts[i];
                var other = _sim.UnitOf(candidate);
                if (other == null || other.IsDestroyed) continue;

                float d = Vector2.Distance(unit.Cell, picture.LastSeen[i]);
                if (d > envelopeCells) continue;

                // Strictly less than, so ties resolve to the earlier-reported contact rather
                // than to whichever the loop happened to reach last.
                if (d < bestDistance) { bestDistance = d; best = candidate; }
            }

            return best;
        }

        // ─── Save/load (#74) ─────────────────────────────────────────────────

        /// <summary>
        /// Rebuilds every unit's picture from reports already delivered, in delivery order.
        /// </summary>
        /// <remarks>
        /// **Derivable, not serialised.** <see cref="_pictures"/> is built by <see cref="OnReport"/>
        /// alone, so replaying the same reports through the same method reproduces it exactly —
        /// there is no separate fact here a save has to carry, only a computation a save can
        /// repeat. The caller must pass only reports the *original* simulation had actually
        /// delivered by the tick the snapshot was taken — not the full <c>ReportLog</c>, which
        /// also holds whatever was published on the snapshot's own tick and is still sitting in
        /// <c>Reports.Pending</c>, undelivered. Feeding those in early would hand a restored
        /// unit's reflexes a contact one tick before the original ever had it — the same
        /// off-by-one-step a snapshot taken mid-cascade always risks, restated here because
        /// this is the one place it would change what a unit *does* rather than merely what it
        /// remembers.
        /// </remarks>
        public void RebuildFrom(IEnumerable<SituationReport> deliveredReports)
        {
            if (deliveredReports == null) return;
            foreach (var r in deliveredReports) OnReport(r);
        }

        // ─── Inspection ───────────────────────────────────────────────────────

        /// <summary>Hostiles this unit currently believes are present. For the UI and probes.</summary>
        public IReadOnlyList<UnitId> ContactsOf(UnitId unit) =>
            _pictures.TryGetValue(unit.Value, out var p) ? p.Contacts : System.Array.Empty<UnitId>();

        /// <summary>Units reported firing on this one. For the UI and probes.</summary>
        public IReadOnlyList<UnitId> AttackersOf(UnitId unit) =>
            _pictures.TryGetValue(unit.Value, out var p) ? p.Attackers : System.Array.Empty<UnitId>();
    }
}
