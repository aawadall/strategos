// ISidePolicy.cs
// #100: the seam between "a side's decision-making" and the live Simulation object.
//
// Before this, SideDirector was the only side-level policy and it was not behind an interface
// -- it took a concrete Simulation in its constructor and reached straight into it. Swapping in
// a different policy (rule-based, scripted for a probe, or eventually a learned one -- #99)
// meant either subclassing SideDirector or writing another class coupled to Simulation the same
// way.
//
// THE SEAM ONLY WORKS IF AN IMPLEMENTATION NEVER NEEDS A Simulation REFERENCE. A signature like
// Decide(Simulation sim) would satisfy the compiler while keeping the exact coupling this
// interface exists to remove -- the issue names that failure mode directly. Decide takes a
// SideKnowledge instead: everything an implementation needs to act, gathered by Simulation and
// handed over as data. A policy hands back the commands it wants issued; it never calls
// Simulation.Issue itself, so it is structurally incapable of touching anything but its own
// argument.

using System.Collections.Generic;
using Strategos.Commands;
using Strategos.Units;

namespace Strategos.Direction
{
    /// <summary>
    /// A side's decision-making, decoupled from <see cref="Simulation"/>. <see cref="SideDirector"/>
    /// is the default implementation; nothing about the interface assumes it is the only one.
    /// </summary>
    public interface ISidePolicy
    {
        /// <summary>True if this policy directs the given side's units.</summary>
        bool Directs(SideId side);

        /// <summary>
        /// Given a side's knowledge at the current tick, return the commands to issue this
        /// evaluation. <see cref="Simulation"/> issues each one through
        /// <see cref="Simulation.Issue"/> itself, exactly as a player's click would --
        /// logged, replayable, indistinguishable in form. Never empty vs. null-sensitive:
        /// return an empty list (or null) when there is nothing to do this tick.
        /// </summary>
        IReadOnlyList<Command> Decide(SideKnowledge knowledge);
    }
}
