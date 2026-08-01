// Casualty.cs
// What was lost, when, and to whom.
//
// The report log has always carried the moment of destruction — Simulation publishes a
// Destroyed report on the crossing — but nothing summarised it, and a log is a stream rather
// than a return. A campaign that carries an ORBAT forward (#75) needs to answer "what did this
// operation cost" without replaying its own reports and inferring.
//
// RECORDED AT THE CROSSING, not derived afterwards. Strength reaching zero is a moment, and by
// the time anyone asks, the only surviving evidence is that the number is zero — which cannot
// distinguish a unit lost in the first minute from one ground down over an hour, and cannot
// name what killed it.

using System.Collections.Generic;

namespace Strategos.Units
{
    /// <summary>One unit lost.</summary>
    public readonly struct Casualty
    {
        public readonly UnitId Unit;
        public readonly SideId Side;

        /// <summary>Who inflicted it, or <see cref="UnitId.None"/> when nothing claimed it.</summary>
        public readonly UnitId By;

        public readonly int Tick;

        public Casualty(UnitId unit, SideId side, UnitId by, int tick)
        {
            Unit = unit;
            Side = side;
            By = by;
            Tick = tick;
        }

        public override string ToString() => $"t{Tick} {Unit} lost to {By}";
    }

    /// <summary>Every loss in a scenario, in the order they happened.</summary>
    public sealed class CasualtyLog
    {
        private readonly List<Casualty> _entries = new();

        /// <summary>Append-only, in tick order. The order is part of the replay signature.</summary>
        public IReadOnlyList<Casualty> Entries => _entries;

        public int Count => _entries.Count;

        public void Add(in Casualty casualty) => _entries.Add(casualty);

        public int CountFor(SideId side)
        {
            int n = 0;
            for (int i = 0; i < _entries.Count; i++) if (_entries[i].Side == side) n++;
            return n;
        }

        /// <summary>
        /// Deterministic signature, for the divergence test.
        /// </summary>
        /// <remarks>
        /// Losses are part of what a replay must reproduce: two runs that end with the same
        /// unit strengths but lost them in a different order, or to different killers, have
        /// diverged in everything a campaign would carry forward.
        /// </remarks>
        public void AppendSignature(System.Text.StringBuilder sb)
        {
            sb.Append('C').Append('[');
            for (int i = 0; i < _entries.Count; i++)
                sb.Append(_entries[i].Tick).Append(':')
                  .Append(_entries[i].Unit.Value).Append(':')
                  .Append(_entries[i].By.Value).Append(';');
            sb.Append(']');
        }
    }
}
