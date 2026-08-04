// ActionKind.cs
// #33 / #278: special actions units may perform beyond move/shoot — dig, bridge, clear.
//
// Distinct from CommandKind (what the simulation queues) and from SideActionSpace (#102 RL
// vocabulary). A special action expands into an ordinary world command when issued; DigIn
// bridges to Hold/Defend so dig-in prep and half-fire stay one path.

using Strategos.Commands;
using Strategos.Units;

namespace Strategos.SpecialActions
{
    /// <summary>Named special actions. Reserved values are stubs until their epics land.</summary>
    public enum ActionKind
    {
        None = 0,
        DigIn = 1,
        Bridge = 2,
        Clear = 3,
    }

    /// <summary>
    /// Expands a special action into a <see cref="Command"/> the simulation already understands.
    /// </summary>
    public static class SpecialAction
    {
        /// <summary>
        /// Returns the world command for <paramref name="kind"/>, or null when the unit cannot
        /// perform it (capability gate) or the kind is not yet wired.
        /// </summary>
        public static Command? TryCreate(ActionKind kind, ActorId by, UnitInstance unit,
            UnitCatalogue catalogue)
        {
            if (unit == null || unit.IsDestroyed) return null;
            if (kind == ActionKind.None) return null;

            var caps = unit.Capabilities(catalogue ?? UnitCatalogue.Default());

            switch (kind)
            {
                case ActionKind.DigIn:
                    if (!caps.CanDigIn) return null;
                    return Command.Hold(by, unit.Id);

                default:
                    return null;
            }
        }
    }
}
