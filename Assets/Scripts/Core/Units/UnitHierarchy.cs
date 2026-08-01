// UnitHierarchy.cs
// The ORBAT as a tree, indexed once over a flat unit list.
//
// UnitInstance.ParentId has existed since the unit model landed and, until now, was only ever
// *validated* — Scenario.Validate checked the parent existed and nothing read it. This is the
// thing that reads it.
//
// A FORMATION IS A UnitInstance THAT OWNS SUBORDINATES. Not a separate Formation type: that
// is what phases.md 3.1 describes, it matches reality, it is why every SIDC already carries an
// echelon, and the note on ParentId committed to it. A composite means a battalion can be
// ordered, drawn and reported on by the same code that handles a rifle platoon, which is the
// whole point — commanding at echelon should be the same verbs at a different altitude.
//
// AN AGGREGATE IS NOT A COMBATANT, and this is the rule that keeps the tree from breaking
// everything else. A battalion symbol *stands for* its companies; it does not fight beside
// them. Every consumer in Core enumerates units to answer "what can be seen, shot at, moved
// or counted", and if formations appeared in those lists a battalion would be detected
// separately from its companies, engaged separately, and counted separately for victory —
// every one of them double-counting the same troops. So:
//
//   * Simulation.Units keeps meaning what it has always meant — the things that fight — and
//     now returns leaves. Every existing consumer stays correct with no edit.
//   * Simulation.AllUnits is the new, explicit way to ask for formations as well.
//
// That asymmetry is deliberate. The dangerous list is the one you have to name.
//
// STATE ROLLS UP, IT IS NOT STORED TWICE. A formation's strength is the strength of what it
// owns. Storing it as well would give two answers that drift, and the one a player reads
// would be whichever the view happened to ask for.

using System.Collections.Generic;
using UnityEngine;

namespace Strategos.Units
{
    /// <summary>Parent and subordinate lookups over a unit list, plus rollup.</summary>
    public sealed class UnitHierarchy
    {
        private readonly Dictionary<int, UnitInstance> _byId = new();
        private readonly Dictionary<int, List<UnitInstance>> _children = new();
        private readonly List<UnitInstance> _all = new();
        private readonly List<UnitInstance> _leaves = new();
        private readonly List<UnitInstance> _roots = new();

        /// <summary>Every unit, formations included, in the order given.</summary>
        public IReadOnlyList<UnitInstance> All => _all;

        /// <summary>
        /// Units that actually fight — those with no subordinates.
        /// </summary>
        /// <remarks>
        /// Stable, index-based, and in the source list's order. Never derived from dictionary
        /// iteration: this list feeds ContactTracker's index and the divergence signature, and
        /// an unstable order there is the classic way a replay quietly stops reproducing.
        /// </remarks>
        public IReadOnlyList<UnitInstance> Leaves => _leaves;

        /// <summary>Units with no parent. The top of each side's ORBAT.</summary>
        public IReadOnlyList<UnitInstance> Roots => _roots;

        public UnitHierarchy(IReadOnlyList<UnitInstance> units)
        {
            if (units == null) return;

            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u == null) continue;
                _all.Add(u);
                _byId[u.Id.Value] = u;
            }

            // Children in source order, so decomposition hands out orders in a fixed sequence.
            for (int i = 0; i < _all.Count; i++)
            {
                var u = _all[i];
                if (!u.ParentId.IsValid || !_byId.ContainsKey(u.ParentId.Value)) continue;

                if (!_children.TryGetValue(u.ParentId.Value, out var list))
                    _children[u.ParentId.Value] = list = new List<UnitInstance>();
                list.Add(u);
            }

            for (int i = 0; i < _all.Count; i++)
            {
                var u = _all[i];
                if (!_children.ContainsKey(u.Id.Value)) _leaves.Add(u);
                if (!u.ParentId.IsValid || !_byId.ContainsKey(u.ParentId.Value)) _roots.Add(u);
            }
        }

        public UnitInstance Find(UnitId id) =>
            _byId.TryGetValue(id.Value, out var u) ? u : null;

        public UnitInstance ParentOf(UnitId id)
        {
            var u = Find(id);
            return u == null || !u.ParentId.IsValid ? null : Find(u.ParentId);
        }

        private static readonly List<UnitInstance> None = new();

        /// <summary>Immediate subordinates, in scenario order. Empty for a leaf.</summary>
        public IReadOnlyList<UnitInstance> SubordinatesOf(UnitId id) =>
            _children.TryGetValue(id.Value, out var list) ? list : None;

        public bool IsFormation(UnitId id) => _children.ContainsKey(id.Value);

        /// <summary>How many levels above a root this unit sits. A root is 0.</summary>
        public int DepthOf(UnitId id)
        {
            int depth = 0;
            var u = Find(id);

            // Bounded by the unit count, so a cycle that slipped past Scenario.Validate
            // terminates here rather than hanging the simulation.
            while (u != null && u.ParentId.IsValid && depth <= _all.Count)
            {
                u = Find(u.ParentId);
                depth++;
            }
            return depth;
        }

        /// <summary>
        /// Every fighting unit under this one, at any depth. The unit itself if it is a leaf.
        /// </summary>
        public void LeavesUnder(UnitId id, List<UnitInstance> into)
        {
            if (into == null) return;
            var unit = Find(id);
            if (unit == null) return;

            var subordinates = SubordinatesOf(id);
            if (subordinates.Count == 0) { into.Add(unit); return; }

            for (int i = 0; i < subordinates.Count; i++)
                LeavesUnder(subordinates[i].Id, into);
        }

        // ─── Rollup ───────────────────────────────────────────────────────────

        private readonly List<UnitInstance> _scratch = new();

        /// <summary>
        /// A formation's strength: the mean of the fighting units under it. A leaf's own.
        /// </summary>
        /// <remarks>
        /// Mean rather than sum, because strength is a percentage of establishment and a
        /// battalion of three full companies is at 100%, not 300%. Unweighted, which is a
        /// simplification worth naming: a company and a platoon count equally, where a real
        /// rollup would weight by establishment. There is no establishment figure in the model
        /// yet — when there is, this is the one place it changes.
        /// </remarks>
        public float StrengthOf(UnitId id) => Mean(id, u => u.Strength);

        /// <summary>A formation's readiness: the mean of the fighting units under it.</summary>
        public float ReadinessOf(UnitId id) => Mean(id, u => u.Readiness);

        /// <summary>
        /// Where a formation is: the centroid of what it owns.
        /// </summary>
        /// <remarks>
        /// Derived rather than stored, so a formation cannot be somewhere its troops are not.
        /// A stored position would be a second source of truth that drifts the moment anything
        /// moves, and the map would draw a battalion standing where it was an hour ago.
        /// </remarks>
        public Vector2 CellOf(UnitId id)
        {
            _scratch.Clear();
            LeavesUnder(id, _scratch);
            if (_scratch.Count == 0) return Find(id)?.Cell ?? Vector2.zero;

            Vector2 sum = Vector2.zero;
            for (int i = 0; i < _scratch.Count; i++) sum += _scratch[i].Cell;
            return sum / _scratch.Count;
        }

        /// <summary>True once every fighting unit under this one is destroyed.</summary>
        public bool IsDestroyed(UnitId id)
        {
            _scratch.Clear();
            LeavesUnder(id, _scratch);
            if (_scratch.Count == 0) return false;

            for (int i = 0; i < _scratch.Count; i++)
                if (!_scratch[i].IsDestroyed) return false;
            return true;
        }

        private float Mean(UnitId id, System.Func<UnitInstance, float> pick)
        {
            _scratch.Clear();
            LeavesUnder(id, _scratch);
            if (_scratch.Count == 0) return 0f;

            float sum = 0f;
            for (int i = 0; i < _scratch.Count; i++) sum += pick(_scratch[i]);
            return sum / _scratch.Count;
        }
    }
}
