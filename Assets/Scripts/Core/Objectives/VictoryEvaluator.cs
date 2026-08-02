// VictoryEvaluator.cs
// Who holds what, and whether anyone has won yet.
//
// This is the thing that turns a simulation you watch into a game you can lose.
//
// HANDED ITS OBJECTIVES, NEVER FETCHING THEM. The evaluator takes objectives and conditions as
// constructor arguments rather than reaching into a Scenario. They are scenario data today and
// they will not stay that way — under #36 an objective is the content of a *directive* from a
// higher formation, so "the objectives currently in force for this side" has to be able to
// change mid-scenario. A constructor argument survives that; a static reach-in does not.
//
// EVALUATED ON A CADENCE, NOT EVERY TICK. Control is an O(objectives x units) sweep and nothing
// it decides can change inside a few seconds of simulated time. The interval is a constant
// rather than a setting because changing it changes when a hold-duration is satisfied, which is
// an outcome and not a preference.

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Strategos.Units;

namespace Strategos.Objectives
{
    /// <summary>How a scenario finished.</summary>
    public struct ScenarioOutcome
    {
        public bool Decided;

        /// <summary>The winner, or <see cref="SideId.None"/> for a draw.</summary>
        public SideId Winner;

        public VictoryKind Condition;
        public int Tick;
        public string Summary;

        public bool IsDraw => Decided && !Winner.IsValid;

        public override string ToString() =>
            !Decided ? "undecided"
            : IsDraw ? $"draw at t{Tick} — {Summary}"
            : $"{Winner} wins at t{Tick} ({Condition}) — {Summary}";
    }

    public sealed class VictoryEvaluator
    {
        /// <summary>Ticks between evaluations. See the file header for why it is a constant.</summary>
        public const int EvaluationInterval = 10;

        private readonly List<Objective> _objectives = new();
        private readonly List<VictoryCondition> _conditions = new();

        // Parallel to _objectives, by index. Arrays rather than a dictionary keyed by id: the
        // sweep walks them in order every evaluation, and index order is the authored order.
        private SideId[] _owner;
        private int[] _heldSince;

        /// <summary>
        /// Tick the owner's *continuous presence* began, or <see cref="NotOccupied"/>.
        /// </summary>
        /// <remarks>
        /// **Ownership and occupation are different facts and used to share one field.** Owner
        /// answers "who took this ground" and is deliberately sticky — walking out does not
        /// hand it back, which is what makes taking ground mean something. Occupation answers
        /// "who is standing in it now", and a hold clock has to run on the second.
        ///
        /// With only `_heldSince`, which moves only when *ownership* changes, `HoldTicks`
        /// measured "took it this long ago and nobody has taken it since" — so a unit routed
        /// through an objective for one tick won a ten-minute hold by leaving and doing
        /// nothing. On the shipped skirmish that was the primary victory condition, satisfiable
        /// by an accident of pathfinding, and invisible because the ring turns your colour
        /// either way.
        /// </remarks>
        private int[] _occupiedSince;

        /// <summary>No unit of the owning side is inside.</summary>
        public const int NotOccupied = -1;

        private readonly Dictionary<int, float> _startingStrength = new();

        /// <summary>Ticks after which an undecided scenario is a draw. Zero means no limit.</summary>
        public int TimeLimitTicks { get; }

        public ScenarioOutcome Outcome { get; private set; }
        public bool IsDecided => Outcome.Decided;

        public IReadOnlyList<Objective> Objectives => _objectives;

        public VictoryEvaluator(IEnumerable<Objective> objectives,
            IEnumerable<VictoryCondition> conditions,
            IReadOnlyList<UnitInstance> units,
            int timeLimitTicks = 0)
        {
            if (objectives != null) _objectives.AddRange(objectives);
            if (conditions != null) _conditions.AddRange(conditions);
            TimeLimitTicks = timeLimitTicks;

            _owner = new SideId[_objectives.Count];
            _heldSince = new int[_objectives.Count];
            _occupiedSince = new int[_objectives.Count];

            for (int i = 0; i < _objectives.Count; i++)
            {
                _owner[i] = _objectives[i].InitialOwner;
                _heldSince[i] = 0;

                // Not occupied until somebody is actually seen standing in it. An initial
                // owner has taken the ground on paper, which is not the same as being on it.
                _occupiedSince[i] = NotOccupied;
            }

            // Starting strength per side, captured once. "Reduced below 25%" has to mean 25% of
            // what the side began with; measured against its *current* total instead, a side
            // can never fall below a share of itself and the condition never fires.
            if (units != null)
                for (int i = 0; i < units.Count; i++)
                {
                    var u = units[i];
                    _startingStrength.TryGetValue(u.Side.Value, out float total);
                    _startingStrength[u.Side.Value] = total + u.Strength;
                }
        }

        /// <summary>
        /// How much of its starting combat power a side still has, 0–1.
        /// </summary>
        /// <remarks>
        /// Exposed for display, and measured against **the same baseline
        /// <see cref="VictoryKind.DestroyEnemy"/> tests**. That matters more than it looks: a
        /// force bar computed from some other denominator would move differently from the
        /// condition it appears to predict, and a player watching it would be misled about
        /// exactly the thing it exists to show.
        /// </remarks>
        public float RemainingFraction(SideId side, IReadOnlyList<UnitInstance> units)
        {
            if (!_startingStrength.TryGetValue(side.Value, out float start) || start <= 0f)
                return 0f;

            float now = 0f;
            for (int i = 0; i < units.Count; i++)
                if (units[i].Side == side) now += units[i].Strength;

            return Mathf.Clamp01(now / start);
        }

        // ─── Control ──────────────────────────────────────────────────────────

        /// <summary>Who holds an objective, by its position in the authored list.</summary>
        public SideId OwnerOfIndex(int i) => (uint)i < (uint)_owner.Length ? _owner[i] : SideId.None;

        public SideId OwnerOf(int objectiveId)
        {
            for (int i = 0; i < _objectives.Count; i++)
                if (_objectives[i].Id == objectiveId) return _owner[i];
            return SideId.None;
        }

        /// <summary>
        /// Recomputes who holds each objective.
        /// </summary>
        /// <remarks>
        /// **Uncontested presence takes control; contested ground freezes.** A side takes an
        /// objective by having a living unit inside it with no living enemy inside it. With
        /// both present, ownership does not change — which means taking ground requires
        /// clearing it, and an attacker who merely arrives has not taken anything.
        ///
        /// Ownership is also sticky: walking out does not hand it back. Ground stays taken
        /// until somebody else takes it, which is what makes holding worth doing.
        /// </remarks>
        private void UpdateControl(IReadOnlyList<UnitInstance> units, int tick)
        {
            for (int i = 0; i < _objectives.Count; i++)
            {
                var objective = _objectives[i];

                // Stable order, by index; never a dictionary. Two passes rather than a set:
                // the first side found present, and whether a second one is.
                SideId present = SideId.None;
                bool contested = false;

                for (int u = 0; u < units.Count; u++)
                {
                    var unit = units[u];
                    if (unit.IsDestroyed) continue;
                    if (!objective.Contains(unit.Cell)) continue;

                    if (!present.IsValid) present = unit.Side;
                    else if (present != unit.Side) { contested = true; break; }
                }

                // Occupation is about the *owner* being present, and is judged every tick
                // whatever ownership does. Contested counts as occupied for whoever owns it:
                // a defender fighting for its own ground has not abandoned it.
                bool ownerInside = OwnerIsInside(objective, _owner[i], units);
                if (!ownerInside) _occupiedSince[i] = NotOccupied;
                else if (_occupiedSince[i] == NotOccupied) _occupiedSince[i] = tick;

                if (contested || !present.IsValid) continue;

                if (_owner[i] != present)
                {
                    _owner[i] = present;
                    _heldSince[i] = tick;

                    // A new owner starts its clock now, and it is standing there by
                    // definition — uncontested presence is how it took the ground.
                    _occupiedSince[i] = tick;
                }
            }
        }

        /// <summary>Whether the owning side has a living unit inside the objective.</summary>
        private static bool OwnerIsInside(Objective objective, SideId owner,
            IReadOnlyList<UnitInstance> units)
        {
            if (!owner.IsValid) return false;

            for (int u = 0; u < units.Count; u++)
            {
                var unit = units[u];
                if (unit.IsDestroyed || unit.Side != owner) continue;
                if (objective.Contains(unit.Cell)) return true;
            }
            return false;
        }

        /// <summary>
        /// How long the owner has been standing in an objective, in ticks, or 0.
        /// </summary>
        /// <remarks>
        /// Exposed so a view can show the clock a hold condition depends on. It was invisible
        /// before, which is most of why a hold that never happened went unnoticed.
        /// </remarks>
        public int HeldTicksOfIndex(int i, int tick) =>
            (uint)i < (uint)_occupiedSince.Length && _occupiedSince[i] != NotOccupied
                ? tick - _occupiedSince[i]
                : 0;

        // ─── Evaluation ───────────────────────────────────────────────────────

        /// <summary>
        /// Advances control and tests every condition. Returns true once decided.
        /// </summary>
        public bool Evaluate(IReadOnlyList<UnitInstance> units, int tick)
        {
            if (Outcome.Decided) return true;
            if (tick % EvaluationInterval != 0) return false;

            UpdateControl(units, tick);

            // All conditions are tested, not the first that matches, because precedence is
            // decided by Priority rather than by position in the loop.
            VictoryCondition best = null;
            int bestIndex = -1;

            for (int i = 0; i < _conditions.Count; i++)
            {
                var condition = _conditions[i];
                if (!IsSatisfied(condition, units, tick)) continue;

                if (best == null || condition.Priority > best.Priority)
                {
                    best = condition;
                    bestIndex = i;
                }
            }

            if (best != null)
            {
                Outcome = new ScenarioOutcome
                {
                    Decided = true,
                    Winner = best.Side,
                    Condition = best.Kind,
                    Tick = tick,
                    Summary = string.IsNullOrEmpty(best.Description)
                        ? best.ToString() : best.Description,
                };
                return true;
            }

            // A timeout is an outcome, not an unhandled case. Reached only when nothing else
            // triggered, so a side whose SurviveUntil fires on this same tick still wins.
            if (TimeLimitTicks > 0 && tick >= TimeLimitTicks)
            {
                Outcome = new ScenarioOutcome
                {
                    Decided = true,
                    Winner = SideId.None,
                    Condition = VictoryKind.None,
                    Tick = tick,
                    Summary = $"Time limit reached at t{tick} with no side victorious.",
                };
                return true;
            }

            return false;
        }

        private bool IsSatisfied(VictoryCondition condition, IReadOnlyList<UnitInstance> units,
            int tick)
        {
            switch (condition.Kind)
            {
                case VictoryKind.DestroyEnemy:
                    return EnemyBroken(condition.Side, units, condition.StrengthThresholdPercent);

                case VictoryKind.HoldObjectives:
                    return HoldsAll(condition, tick);

                case VictoryKind.SurviveUntil:
                    return tick >= condition.DeadlineTick && HasLivingUnits(condition.Side, units);

                default:
                    return false;
            }
        }

        /// <summary>
        /// True when every side other than <paramref name="side"/> is below the threshold, as a
        /// share of what it started with.
        /// </summary>
        private bool EnemyBroken(SideId side, IReadOnlyList<UnitInstance> units, float thresholdPercent)
        {
            // Current totals, in unit order.
            var current = new Dictionary<int, float>();
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                current.TryGetValue(u.Side.Value, out float total);
                current[u.Side.Value] = total + u.Strength;
            }

            bool sawEnemy = false;

            // Iterate the *starting* record through the unit list, so nothing walks a
            // dictionary in an unspecified order.
            for (int i = 0; i < units.Count; i++)
            {
                var enemy = units[i].Side;
                if (enemy == side) continue;
                sawEnemy = true;

                if (!_startingStrength.TryGetValue(enemy.Value, out float start) || start <= 0f)
                    continue;

                current.TryGetValue(enemy.Value, out float now);
                if (now / start * 100f >= thresholdPercent) return false;
            }

            return sawEnemy;
        }

        private static bool HasLivingUnits(SideId side, IReadOnlyList<UnitInstance> units)
        {
            for (int i = 0; i < units.Count; i++)
                if (units[i].Side == side && !units[i].IsDestroyed) return true;
            return false;
        }

        private bool HoldsAll(VictoryCondition condition, int tick)
        {
            if (condition.ObjectiveIds == null || condition.ObjectiveIds.Length == 0) return false;

            foreach (int id in condition.ObjectiveIds)
            {
                int index = -1;
                for (int i = 0; i < _objectives.Count; i++)
                    if (_objectives[i].Id == id) { index = i; break; }

                if (index < 0) return false;                       // names an objective that does not exist
                if (_owner[index] != condition.Side) return false;

                // Occupation, not ownership. Ownership is sticky by design, so measuring the
                // hold against it counts time the ground was abandoned — which is the whole
                // of the bug this replaced.
                if (_occupiedSince[index] == NotOccupied) return false;
                if (tick - _occupiedSince[index] < condition.HoldTicks) return false;
            }

            return true;
        }

        // ─── Determinism ──────────────────────────────────────────────────────

        /// <summary>
        /// Control state, for the divergence test. Ownership is world state: a replay that puts
        /// units in the right places but hands an objective to the other side has diverged.
        /// </summary>
        public void AppendSignature(StringBuilder sb)
        {
            sb.Append("obj[");
            for (int i = 0; i < _owner.Length; i++)
                sb.Append(_owner[i].Value).Append(':').Append(_heldSince[i]).Append(':')
                  .Append(_occupiedSince[i]).Append(';');
            sb.Append(']');
            if (Outcome.Decided)
                sb.Append("win:").Append(Outcome.Winner.Value).Append(':').Append(Outcome.Tick);
        }

        // ─── Save/load (#74) ─────────────────────────────────────────────────
        //
        // _owner, _heldSince and _occupiedSince are already in AppendSignature, so a round-trip
        // comparison alone would catch a dropped one of those. _startingStrength is not, and
        // for a reason worth restating here: it is fixed once, at construction, from whichever
        // unit list the constructor was handed. A restored Simulation's constructor runs again
        // and would recompute it from the CURRENT (possibly already-damaged) strengths on the
        // scenario passed in — not the true tick-zero baseline the original evaluator captured
        // — silently changing what "reduced below 25%" means for the rest of the restored run
        // without moving Signature() at the moment of restore at all. It only shows up later,
        // in whether DestroyEnemy fires on the tick the original run would have, which is
        // exactly why the probe's step-after-restore assertion exists rather than trusting the
        // moment of restore alone.

        /// <summary>A copy of who currently owns each objective, by authored list index.</summary>
        public SideId[] SnapshotOwner() => (SideId[])_owner.Clone();

        /// <summary>A copy of when each objective's current owner took it, by authored list index.</summary>
        public int[] SnapshotHeldSince() => (int[])_heldSince.Clone();

        /// <summary>A copy of when the current owner's continuous presence began, by authored list index.</summary>
        public int[] SnapshotOccupiedSince() => (int[])_occupiedSince.Clone();

        /// <summary>A copy of each side's starting strength, captured once at construction.</summary>
        public Dictionary<int, float> SnapshotStartingStrength() => new(_startingStrength);

        /// <summary>
        /// Overwrites every field this evaluator owns — restore-only. Arrays are copied in by
        /// value rather than swapped in by reference, so a caller mutating its own copy
        /// afterwards cannot reach back into this evaluator.
        /// </summary>
        public void RestoreState(SideId[] owner, int[] heldSince, int[] occupiedSince,
            IReadOnlyDictionary<int, float> startingStrength, ScenarioOutcome outcome)
        {
            if (owner != null && owner.Length == _owner.Length) Array.Copy(owner, _owner, _owner.Length);
            if (heldSince != null && heldSince.Length == _heldSince.Length)
                Array.Copy(heldSince, _heldSince, _heldSince.Length);
            if (occupiedSince != null && occupiedSince.Length == _occupiedSince.Length)
                Array.Copy(occupiedSince, _occupiedSince, _occupiedSince.Length);

            _startingStrength.Clear();
            if (startingStrength != null)
                foreach (var kv in startingStrength) _startingStrength[kv.Key] = kv.Value;

            Outcome = outcome;
        }
    }
}
