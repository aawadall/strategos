// CommandScope.cs
// #36 / #267–#268: the echelon band the player occupies as a node in the chain.
//
// Directives arrive from higher on DirectiveBus (#73). Orders go out on the command topic
// to addressees at or below the player's echelon. Units above that band stay on the ORBAT
// for rollup and for higher's picture; they are not commandable from this seat.

using Strategos.NatoSymbols;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Commands
{
    /// <summary>
    /// Resolves the player's command band and whether a unit may be addressed from it.
    /// </summary>
    public static class CommandScope
    {
        /// <summary>
        /// Authored <see cref="Scenario.PlayerEchelon"/> when set; otherwise the highest
        /// SIDC echelon on the player side (or on the map in hot-seat).
        /// </summary>
        public static Echelon EffectivePlayerEchelon(Scenario scenario)
        {
            if (scenario == null) return Echelon.None;
            if (scenario.PlayerEchelon != Echelon.None) return scenario.PlayerEchelon;
            return RankGate.OrbatMaxEchelon(scenario);
        }

        /// <summary>
        /// Whether <paramref name="unit"/> may receive an order from the player's seat.
        /// </summary>
        /// <remarks>
        /// Hot-seat (<see cref="Scenario.PlayerSide"/> unset) leaves every unit addressable.
        /// Targets on other sides are not gated here — opposing directors and reflexes issue
        /// through the same <see cref="Simulation.Issue"/> path and must keep working.
        /// On the player side, only units at or below the effective player echelon pass.
        /// </remarks>
        public static bool CanAddress(Scenario scenario, UnitInstance unit)
        {
            if (unit == null) return false;
            if (scenario == null || !scenario.PlayerSide.IsValid) return true;
            if (unit.Side != scenario.PlayerSide) return true;

            var band = EffectivePlayerEchelon(scenario);
            if (band == Echelon.None) return true;

            return (int)unit.ToSidcCode().Echelon <= (int)band;
        }
    }
}
