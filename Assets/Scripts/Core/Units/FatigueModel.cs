// FatigueModel.cs
// What marching and fighting cost a unit, and what rest gives back.
//
// UnitInstance.Readiness has always been documented as falling "with movement, action and
// time without rest" and nothing has ever moved it. It was initialised to 100, multiplied
// into Effectiveness alongside strength and suppression, displayed in the details card, and
// held constant for the whole scenario. This is the missing input; every consumer already
// exists.
//
// DETERMINISTIC BY CONSTRUCTION. No random draw anywhere: cost is a function of posture,
// terrain and whether the unit was in an engagement this tick. That keeps replay intact
// without needing the stateless-seed pattern EngagementResolver uses, and it means a tired
// unit is tired for reasons a player can point at.
//
// NOT THE SAME THING AS TRAINING, and they must not be collapsed. Training is fixed for the
// battle and costs *time* — hesitation before acting. Fatigue changes during the battle and
// costs *capability* — everything Effectiveness gates. Keeping them apart is what makes
// "rotate the tired veterans out and put the green company in" a real decision instead of one
// number pretending to be two.

using UnityEngine;
using Strategos.Maps;

namespace Strategos.Units
{
    public static class FatigueModel
    {
        /// <summary>
        /// Readiness a unit can never be driven below by fatigue alone.
        /// </summary>
        /// <remarks>
        /// **A tired unit must not look like a destroyed one.** `Effectiveness` multiplies
        /// strength, readiness and suppression, so an unfloored readiness gives a fourth
        /// independent path to zero and a long march alone would render a unit combat
        /// ineffective without a shot being fired. The floor is what keeps exhaustion a
        /// serious degradation rather than a second way to die.
        ///
        /// Casualties are the only thing that should reach zero, and they have
        /// <see cref="UnitInstance.IsDestroyed"/> for it.
        /// </remarks>
        public const float MinReadiness = 25f;

        /// <summary>
        /// Most the going may multiply the cost of moving.
        /// </summary>
        /// <remarks>
        /// Difficulty is read from the unit's own landcover speed factor — an hour spent
        /// pushing through forest is more tiring than an hour on open ground, and the factor
        /// already says how much harder that ground is for *this* unit. Clamped because the
        /// factor approaches zero on ground a unit can barely cross, and an unclamped
        /// reciprocal would empty a unit in a handful of ticks at the edge of what it can
        /// traverse.
        /// </remarks>
        public const float MaxTerrainMultiplier = 2.5f;

        /// <summary>
        /// Applies one tick of fatigue or recovery to a unit.
        /// </summary>
        /// <param name="engaged">
        /// True when the unit fired or was fired at this tick — attacker *or* defender.
        /// Being shot at is as tiring as shooting, and a model that charged only the attacker
        /// would make the unit pinned in a ditch the freshest one on the field.
        /// </param>
        public static void Apply(UnitInstance unit, bool engaged, MapData map,
            UnitCatalogue catalogue, float secondsPerTick)
        {
            if (unit == null || unit.IsDestroyed) return;

            var rates = unit.Capabilities(catalogue).Fatigue;
            float hours = secondsPerTick / 3600f;

            float cost = 0f;

            if (unit.Posture == Posture.Moving)
                cost += rates.PerHourMoving * TerrainMultiplier(unit, map, catalogue) * hours;

            if (engaged)
                cost += rates.PerHourEngaged * hours;

            if (cost > 0f)
            {
                unit.Readiness = Mathf.Max(MinReadiness, unit.Readiness - cost);
                return;
            }

            // Resting: halted, and nobody shooting. Suppression is deliberately not a bar to
            // recovery — a unit that is pinned but unengaged is still catching its breath,
            // and suppression already has its own decay and its own term in Effectiveness.
            unit.Readiness = Mathf.Min(100f, unit.Readiness + rates.RecoveryPerHourResting * hours);
        }

        /// <summary>
        /// How much harder this ground is for this unit than open going.
        /// </summary>
        /// <remarks>
        /// The reciprocal of the landcover speed factor, so it needs no second table and
        /// cannot drift from the movement model: whatever ground slows a unit also tires it,
        /// in the same proportion and from the same authored number.
        /// </remarks>
        private static float TerrainMultiplier(UnitInstance unit, MapData map,
            UnitCatalogue catalogue)
        {
            if (map == null) return 1f;

            float factor = unit.Capabilities(catalogue).LandcoverFactor(unit.Landcover(map));
            if (factor <= 0.0001f) return MaxTerrainMultiplier;

            return Mathf.Clamp(1f / factor, 1f, MaxTerrainMultiplier);
        }
    }
}
