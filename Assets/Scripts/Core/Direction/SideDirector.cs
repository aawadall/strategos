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
// calls, and it means a replay reproduces an unattended game as faithfully as a played one. Since
// #100 it does that at one remove: Decide *returns* the commands it wants issued rather than
// calling Issue itself, which is what lets it hold no Simulation reference at all — Simulation
// gathers a SideKnowledge and does the issuing.
//
// #100: the first, default implementation of ISidePolicy. Everything below used to read
// Simulation directly (_sim.Tick, _sim.IsOver, _sim.Units, _sim.QueueOf, _sim.Victory); it reads
// the same facts off SideKnowledge now, and holds no Simulation reference as a result.
//
// #291: EvaluationInterval / RetryInterval / MinStrengthPercent come from DifficultyParams
// rather than fixed constants, so Easy/Hard and personality packs can tune the same reflex.

using System.Collections.Generic;
using UnityEngine;
using Strategos.Commands;
using Strategos.Objectives;
using Strategos.Reactions;
using Strategos.Units;

namespace Strategos.Direction
{
    public sealed class SideDirector : ISidePolicy
    {
        /// <summary>Default evaluation cadence — same as <see cref="DifficultyParams.Normal"/>.</summary>
        public const int EvaluationInterval = 20;

        /// <summary>Default retry backoff — same as <see cref="DifficultyParams.Normal"/>.</summary>
        public const int RetryInterval = 300;

        private readonly List<SideId> _sides = new();

        // Last tick each unit was given an order. Lookup only, never iterated.
        private readonly Dictionary<int, int> _lastOrdered = new();

        /// <summary>Live knobs for this director (#291). Never null after construction.</summary>
        public DifficultyParams Params { get; }

        /// <summary>Orders issued on the directed sides' own initiative. Diagnostic.</summary>
        public int OrdersIssued { get; private set; }

        public SideDirector(IEnumerable<SideId> sides, DifficultyParams? parameters = null)
        {
            if (sides != null) _sides.AddRange(sides);
            Params = (parameters ?? DifficultyParams.Normal()).Clamped();
        }

        public bool Directs(SideId side)
        {
            for (int i = 0; i < _sides.Count; i++) if (_sides[i] == side) return true;
            return false;
        }

        public IReadOnlyList<Command> Decide(SideKnowledge knowledge)
        {
            if (knowledge.Victory == null || _sides.Count == 0) return System.Array.Empty<Command>();
            if (knowledge.Tick % Params.EvaluationInterval != 0) return System.Array.Empty<Command>();
            if (knowledge.IsOver) return System.Array.Empty<Command>();

            List<Command> orders = null;

            // Scenario order, by index. Never a dictionary, and never a shuffled list.
            var units = knowledge.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var order = Direct(units[i], knowledge);
                if (order.HasValue)
                {
                    orders ??= new List<Command>();
                    orders.Add(order.Value);
                }
            }

            return (IReadOnlyList<Command>)orders ?? System.Array.Empty<Command>();
        }

        private Command? Direct(UnitInstance unit, SideKnowledge knowledge)
        {
            if (unit == null || unit.IsDestroyed) return null;
            if (!Directs(unit.Side)) return null;

            // Already busy. A unit with a plan is left alone — including one withdrawing under
            // #13's break-contact rule, which owns the queue until it finishes.
            var queue = knowledge.QueueOf(unit.Id);
            if (queue != null && !queue.IsEmpty) return null;

            // Spent units are not sent back. Difficulty raises/lowers this floor (#291).
            if (unit.Strength < Params.MinStrengthPercent) return null;

            // Recently tasked. Either it is on its way, or the last attempt failed and
            // repeating it immediately will fail the same way.
            if (_lastOrdered.TryGetValue(unit.Id.Value, out int last) &&
                knowledge.Tick - last < Params.RetryInterval)
                return null;

            var objective = NearestUnheld(unit, knowledge.Victory);
            if (objective == null) return null;

            _lastOrdered[unit.Id.Value] = knowledge.Tick;
            OrdersIssued++;
            return Command.MoveTo(ActorId.ForSide(unit.Side), unit.Id, objective.Cell);
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
        private Objective NearestUnheld(UnitInstance unit, VictoryEvaluator victory)
        {
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
        /// <see cref="DifficultyParams.RetryInterval"/> and can reissue on the very next
        /// evaluation an order the original run was still waiting out — the order-storm #13
        /// introduced this field to stop, reappearing after every load rather than never.
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
