// SideRewardSnapshot.cs
// #103: the potential features a side's reward is shaped from — not the reward itself.
//
// Owned-objective count and force advantage are the two mid-run signals that already decide
// HoldObjectives / DestroyEnemy. Capturing them as a snapshot lets Step compute Φ(s') − Φ(s)
// without the reward function reaching into Simulation.

using System.Collections.Generic;
using Strategos.Objectives;
using Strategos.Units;

namespace Strategos.Reward
{
    /// <summary>Potential features for <see cref="SideReward"/> at one tick.</summary>
    public readonly struct SideRewardSnapshot
    {
        /// <summary>Objectives currently owned by the observing side.</summary>
        public readonly int OwnedObjectives;

        /// <summary>
        /// <c>RemainingFraction(side) − mean RemainingFraction(other sides)</c>.
        /// Positive when the side is the stronger residual force.
        /// </summary>
        public readonly float ForceAdvantage;

        public SideRewardSnapshot(int ownedObjectives, float forceAdvantage)
        {
            OwnedObjectives = ownedObjectives;
            ForceAdvantage = forceAdvantage;
        }

        /// <summary>
        /// Capture from victory + units. Ground-truth ownership and strength — the same facts
        /// victory itself uses. Observation fog is #101; reward evaluates the outcome surface.
        /// </summary>
        public static SideRewardSnapshot Capture(
            SideId side,
            VictoryEvaluator victory,
            IReadOnlyList<UnitInstance> units)
        {
            int owned = 0;
            if (victory?.Objectives != null)
            {
                for (int i = 0; i < victory.Objectives.Count; i++)
                    if (victory.OwnerOfIndex(i) == side) owned++;
            }

            float force = 0f;
            if (victory != null && units != null)
            {
                float friendly = victory.RemainingFraction(side, units);
                float enemySum = 0f;
                int enemySides = 0;

                for (int i = 0; i < units.Count; i++)
                {
                    var u = units[i];
                    if (u == null || u.Side == side) continue;

                    bool seen = false;
                    for (int j = 0; j < i; j++)
                    {
                        if (units[j] != null && units[j].Side == u.Side) { seen = true; break; }
                    }
                    if (seen) continue;

                    enemySum += victory.RemainingFraction(u.Side, units);
                    enemySides++;
                }

                float enemyMean = enemySides > 0 ? enemySum / enemySides : 0f;
                force = friendly - enemyMean;
            }

            return new SideRewardSnapshot(owned, force);
        }
    }
}
