// SideDirector.cs
// Gives a side a purpose, so it does something when nobody is playing it.
//
// #13 gave every unit reflexes. Reflexes are *reactions* — they answer fire and break contact,
// and a force made only of them sits on its start line indefinitely and contests nothing. This
// is the missing half: an intent for the side as a whole.
//
// THIS IS NOT AN AI, AND KEEPING IT SMALL IS WHAT STOPS IT BECOMING ONE. The entire behaviour
// is "go to the nearest objective we do not hold". It does not plan, manoeuvre, concentrate,
// feint, or know the player exists — and the moment it wants to know what the *player* is
// doing, it has left its scope. Phase 8 is where an opponent that thinks goes.
//
// That is enough, because everything underneath already works: two forces moving toward the
// same ground is all engagement, suppression, break-contact and objective control need in
// order to fire.
//
// IT ISSUES ORDINARY COMMANDS. Everything goes through Simulation.Issue, exactly as a player's
// clicks do — logged, replayable, visible in the order feed, indistinguishable in form. Not a
// private path into the units. That is the whole reason #9 made orders data rather than method
// calls, and it means a replay reproduces an unattended game as faithfully as a played one.

using System.Collections.Generic;
using UnityEngine;
using Strategos.Commands;
using Strategos.Objectives;
using Strategos.Reactions;
using Strategos.Units;

namespace Strategos.Direction
{
    public sealed class SideDirector
    {
        /// <summary>
        /// Ticks between re-evaluations.
        ///
        /// Not every tick: nothing this decides can change inside a few seconds, and a unit
        /// that has just been given an order needs time to act on it before being reconsidered.
        /// A constant rather than a setting, because changing it changes what the opponent does.
        /// </summary>
        public const int EvaluationInterval = 20;

        /// <summary>
        /// Ticks a unit is left alone after being given an order, before it may be given
        /// another.
        /// </summary>
        /// <remarks>
        /// **This is not politeness, it is the guard against an order storm.** A MoveTo whose
        /// destination cannot be reached fails on the tick it is issued, leaving the queue
        /// empty — so without a backoff the director finds the unit idle at the very next
        /// evaluation and reissues, for ever. The first run of `DirectorProbe` recorded 1080
        /// autonomous orders in an hour of simulated time, one per unit per evaluation, for a
        /// scenario in which nothing moved at all.
        ///
        /// It also bounds the command log, which is the thing a replay and an after-action
        /// review both read.
        /// </remarks>
        public const int RetryInterval = 300;

        private readonly Simulation _sim;
        private readonly List<SideId> _sides = new();

        // Last tick each unit was given an order. Lookup only, never iterated.
        private readonly Dictionary<int, int> _lastOrdered = new();

        /// <summary>Orders issued on the directed sides' own initiative. Diagnostic.</summary>
        public int OrdersIssued { get; private set; }

        public SideDirector(Simulation simulation, IEnumerable<SideId> sides)
        {
            _sim = simulation;
            if (sides != null) _sides.AddRange(sides);
        }

        public bool Directs(SideId side)
        {
            for (int i = 0; i < _sides.Count; i++) if (_sides[i] == side) return true;
            return false;
        }

        public void Evaluate()
        {
            if (_sim?.Victory == null || _sides.Count == 0) return;
            if (_sim.Tick % EvaluationInterval != 0) return;
            if (_sim.IsOver) return;

            // Scenario order, by index. Never a dictionary, and never a shuffled list.
            var units = _sim.Units;
            for (int i = 0; i < units.Count; i++) Direct(units[i]);
        }

        private void Direct(UnitInstance unit)
        {
            if (unit == null || unit.IsDestroyed) return;
            if (!Directs(unit.Side)) return;

            // Already busy. A unit with a plan is left alone — including one withdrawing under
            // #13's break-contact rule, which owns the queue until it finishes.
            var queue = _sim.QueueOf(unit.Id);
            if (queue != null && !queue.IsEmpty) return;

            // Spent units are not sent back. Without this, a unit that has just broken contact
            // finishes withdrawing, is found idle, and is ordered straight back into the fight
            // it fled — which reads as an opponent with no memory rather than as a decision.
            if (unit.Strength < ReactionController.BreakStrengthPercent) return;

            // Recently tasked. Either it is on its way, or the last attempt failed and
            // repeating it immediately will fail the same way.
            if (_lastOrdered.TryGetValue(unit.Id.Value, out int last) &&
                _sim.Tick - last < RetryInterval)
                return;

            var objective = NearestUnheld(unit);
            if (objective == null) return;

            _sim.Issue(Command.MoveTo(ActorId.ForSide(unit.Side), unit.Id, objective.Cell));
            _lastOrdered[unit.Id.Value] = _sim.Tick;
            OrdersIssued++;
        }

        /// <summary>
        /// The closest objective this unit's side does not already hold, or null.
        /// </summary>
        /// <remarks>
        /// Ground truth, deliberately and temporarily. Objectives are terrain features rather
        /// than enemies — a commander knows where the crossroads is without being told — so
        /// this does not breach the rule that units act only on what they have been reported.
        /// Which side currently *holds* one is closer to intelligence, and when the perceived
        /// world layer arrives this should read a belief instead.
        /// </remarks>
        private Objective NearestUnheld(UnitInstance unit)
        {
            var victory = _sim.Victory;
            Objective best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < victory.Objectives.Count; i++)
            {
                if (victory.OwnerOfIndex(i) == unit.Side) continue;

                var objective = victory.Objectives[i];
                float d = Vector2.Distance(unit.Cell, objective.Cell);

                // Strictly less than, so ties resolve to the earlier-authored objective rather
                // than to whichever the loop reached last.
                if (d < bestDistance) { bestDistance = d; best = objective; }
            }

            return best;
        }

        // ─── Save/load (#74) ─────────────────────────────────────────────────

        /// <summary>
        /// A copy of the last tick each unit was given an order on this side's own initiative.
        /// </summary>
        /// <remarks>
        /// **Not derivable, unlike <see cref="Reactions.ReactionController"/>'s picture.** A
        /// director-issued order and a player-issued one are both logged with the same
        /// <see cref="Commands.ActorId.ForSide"/>, so <c>CommandLog</c> cannot tell them apart —
        /// there is no way to reconstruct "which orders did the director itself issue" by
        /// reading the log back. Without this a restored director has no memory of
        /// <see cref="RetryInterval"/> and can reissue on the very next evaluation an order the
        /// original run was still waiting out — the order-storm #13 introduced this field to
        /// stop, reappearing after every load rather than never.
        /// </remarks>
        public Dictionary<int, int> SnapshotLastOrdered() => new(_lastOrdered);

        /// <summary>Overwrites <see cref="_lastOrdered"/> wholesale — restore-only.</summary>
        public void RestoreLastOrdered(IReadOnlyDictionary<int, int> lastOrdered)
        {
            _lastOrdered.Clear();
            if (lastOrdered != null)
                foreach (var kv in lastOrdered) _lastOrdered[kv.Key] = kv.Value;
        }
    }
}
