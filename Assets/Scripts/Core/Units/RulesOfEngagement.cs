// RulesOfEngagement.cs
// Whether a unit is allowed to start a fight on its own.
//
// A unit that only ever acts on an explicit order is a puppet; a unit that shoots at anything
// it sees is uncontrollable. These three settings are the dial between those extremes, and
// they are what makes the game feel like command rather than micromanagement.
//
// ROE GOVERNS INITIATIVE, NOT PERMISSION TO FIRE. A unit on Hold Fire still carries out an
// engage order it was given — refusing a direct order would be a bug, not caution. What Hold
// Fire withholds is the unit's own decision to open fire.

namespace Strategos.Units
{
    public enum RulesOfEngagement
    {
        /// <summary>Never initiate, and do not return fire. Fires only when ordered to.</summary>
        HoldFire = 0,

        /// <summary>
        /// Engage only what has been reported firing on this unit. The default, because it is
        /// the setting that does the least surprising thing: a unit defends itself and does
        /// nothing else without being told.
        /// </summary>
        ReturnFire = 1,

        /// <summary>Engage any hostile reported in range.</summary>
        FireAtWill = 2,
    }
}
