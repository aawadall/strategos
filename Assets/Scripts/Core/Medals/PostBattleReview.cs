// PostBattleReview.cs
// #467: end-of-fight stats + Merit medals (neutralized enemies, objective secured).
// Awards at decision only — not mid-fight.

using System.Collections.Generic;
using Strategos.Commands;
using Strategos.Objectives;
using Strategos.Units;

namespace Strategos.Medals
{
    /// <summary>Numbers shown on the post-battle card.</summary>
    public sealed class PostBattleStats
    {
        public string ScenarioName = string.Empty;
        public string OutcomeLine = string.Empty;
        public bool PlayerWon;
        public bool IsDraw;
        public int Tick;
        public int EnemyNeutralized;
        public int FriendlyLost;
        public int ObjectivesHeld;
        public int ObjectiveCount;
        public float FriendlyRemaining;
        public float EnemyRemaining;
    }

    /// <summary>Stats plus newly earned bars for this decision.</summary>
    public sealed class PostBattleReview
    {
        public PostBattleStats Stats = new();
        public List<BarMedalAward> Awards = new();
    }

    public static class PostBattleReviewer
    {
        /// <summary>
        /// Builds stats and Merit awards from a decided simulation.
        /// One <see cref="BarMedalIO.EnemyNeutralizedId"/> award with Count = kills;
        /// one <see cref="BarMedalIO.ObjectiveSecuredId"/> when the player holds ≥1 objective.
        /// </summary>
        public static PostBattleReview Build(Simulation sim, BarMedalCatalog catalog = null)
        {
            var review = new PostBattleReview();
            if (sim?.Scenario == null) return review;

            catalog ??= BarMedalIO.Load();
            var scenario = sim.Scenario;
            var player = scenario.PlayerSide;
            var stats = review.Stats;
            stats.ScenarioName = scenario.Name ?? string.Empty;
            stats.Tick = sim.Tick;

            var outcome = sim.Victory?.Outcome ?? default;
            stats.IsDraw = outcome.Decided && outcome.IsDraw;
            stats.PlayerWon = outcome.Decided && !outcome.IsDraw &&
                              player.IsValid && outcome.Winner == player;
            stats.OutcomeLine = outcome.Decided
                ? outcome.ToString()
                : "undecided";

            if (sim.Casualties != null && player.IsValid)
            {
                stats.FriendlyLost = sim.Casualties.CountFor(player);
                for (int i = 0; i < scenario.Sides.Count; i++)
                {
                    var side = scenario.Sides[i].Id;
                    if (!side.IsValid || side == player) continue;
                    stats.EnemyNeutralized += sim.Casualties.CountFor(side);
                }
            }

            if (sim.Victory != null)
            {
                stats.ObjectiveCount = sim.Victory.Objectives.Count;
                for (int i = 0; i < sim.Victory.Objectives.Count; i++)
                {
                    if (player.IsValid && sim.Victory.OwnerOfIndex(i) == player)
                        stats.ObjectivesHeld++;
                }

                if (player.IsValid)
                {
                    stats.FriendlyRemaining = sim.Victory.RemainingFraction(player, sim.Units);
                    float enemyRem = 0f;
                    int enemySides = 0;
                    for (int i = 0; i < scenario.Sides.Count; i++)
                    {
                        var side = scenario.Sides[i].Id;
                        if (!side.IsValid || side == player) continue;
                        enemyRem += sim.Victory.RemainingFraction(side, sim.Units);
                        enemySides++;
                    }
                    stats.EnemyRemaining = enemySides > 0 ? enemyRem / enemySides : 0f;
                }
            }

            // Medals: one bar per neutralized enemy (Count stacks on one award), plus objective.
            if (stats.EnemyNeutralized > 0 &&
                BarMedalIO.Find(catalog, BarMedalIO.EnemyNeutralizedId) != null)
            {
                review.Awards.Add(new BarMedalAward
                {
                    MedalId = BarMedalIO.EnemyNeutralizedId,
                    Count = stats.EnemyNeutralized,
                    Tick = stats.Tick,
                    ScenarioName = stats.ScenarioName,
                });
            }

            if (stats.ObjectivesHeld > 0 &&
                stats.PlayerWon &&
                BarMedalIO.Find(catalog, BarMedalIO.ObjectiveSecuredId) != null)
            {
                review.Awards.Add(new BarMedalAward
                {
                    MedalId = BarMedalIO.ObjectiveSecuredId,
                    Count = stats.ObjectivesHeld,
                    Tick = stats.Tick,
                    ScenarioName = stats.ScenarioName,
                });
            }

            return review;
        }
    }
}
