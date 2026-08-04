// SideActionChoice.cs
// #104: one agent decision for SideEnv.Step — a unit and a SideActionSpace index.
//
// The env never mutates queues or cells itself; it turns this into Command via
// SideActionSpace.TryToCommand and Simulation.Issue only (#94).

using Strategos.Units;

namespace Strategos.SimEnv
{
    public readonly struct SideActionChoice
    {
        public readonly UnitId Unit;
        public readonly int ActionIndex;

        public SideActionChoice(UnitId unit, int actionIndex)
        {
            Unit = unit;
            ActionIndex = actionIndex;
        }
    }
}
