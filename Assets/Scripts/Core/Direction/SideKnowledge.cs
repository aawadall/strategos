// SideKnowledge.cs
// #100: what ISidePolicy.Decide is handed instead of a Simulation reference.
//
// Deliberately the minimal thing that lets SideDirector's actual decision -- go to the nearest
// unheld objective, once per evaluation interval, leaving busy or spent units alone -- still be
// made: the exact fields Evaluate/Direct used to read off Simulation directly (_sim.Tick,
// _sim.IsOver, _sim.Units, _sim.QueueOf, _sim.Victory), gathered here instead.
//
// NOT an observation encoding. #101 (still open at the time this was written) is what proposes
// a fixed-shape "a side's knowledge" built strictly from ContactTracker's published reports,
// never from ground truth -- Units and Victory below are still ground truth, exactly as
// SideDirector read them before this refactor (see its own NearestUnheld remark: "Ground truth,
// deliberately and temporarily"). If #101 lands, a policy wanting belief-correct knowledge
// should consume its type; this struct is not trying to be that, only to be the seam.
//
// A struct, not a class: built fresh once per Step() and handed to Decide by value, so nothing
// a policy does to its fields (it shouldn't, but nothing stops it) can be observed by anyone
// else holding the same knowledge.

using System.Collections.Generic;
using Strategos.Commands;
using Strategos.Objectives;
using Strategos.Units;

namespace Strategos.Direction
{
    public readonly struct SideKnowledge
    {
        /// <summary>The tick this knowledge was gathered on -- Simulation.Tick, unchanged.</summary>
        public readonly int Tick;

        /// <summary>True once a side has won or the scenario has timed out.</summary>
        public readonly bool IsOver;

        /// <summary>
        /// The units that fight, in scenario order -- Simulation.Units, never a formation and
        /// never a dictionary.
        /// </summary>
        public readonly IReadOnlyList<UnitInstance> Units;

        /// <summary>Objectives and their current ownership, or null for a scenario without any.</summary>
        public readonly VictoryEvaluator Victory;

        /// <summary>The live plan for a unit, or null if it has none -- Simulation.QueueOf.</summary>
        public readonly System.Func<UnitId, CommandQueue> QueueOf;

        public SideKnowledge(int tick, bool isOver, IReadOnlyList<UnitInstance> units,
            VictoryEvaluator victory, System.Func<UnitId, CommandQueue> queueOf)
        {
            Tick = tick;
            IsOver = isOver;
            Units = units;
            Victory = victory;
            QueueOf = queueOf;
        }
    }
}
