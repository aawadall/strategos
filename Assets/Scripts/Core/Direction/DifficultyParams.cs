// DifficultyParams.cs
// #291 / #318: tunable knobs for reflex SideDirector — not a planning AI.
//
// EvaluationInterval / RetryInterval used to be constants on SideDirector. Aggression is the
// minimum Strength a unit may still be sent toward an objective (above break-contact floor).

using System;
using Strategos.Reactions;

namespace Strategos.Direction
{
    /// <summary>Parameter pack for <see cref="SideDirector"/> (#291).</summary>
    [Serializable]
    public struct DifficultyParams
    {
        /// <summary>Ticks between Decide passes. Higher = slower opponent.</summary>
        public int EvaluationInterval;

        /// <summary>Backoff after an order before the same unit may be retasked.</summary>
        public int RetryInterval;

        /// <summary>
        /// Units below this Strength are not sent forward. Floor for Normal matches
        /// <see cref="ReactionController.BreakStrengthPercent"/>.
        /// </summary>
        public float MinStrengthPercent;

        public static DifficultyParams Normal() => new()
        {
            EvaluationInterval = 20,
            RetryInterval = 300,
            MinStrengthPercent = ReactionController.BreakStrengthPercent,
        };

        public DifficultyParams Clamped()
        {
            var p = this;
            if (p.EvaluationInterval < 1) p.EvaluationInterval = 1;
            if (p.RetryInterval < 1) p.RetryInterval = 1;
            if (p.MinStrengthPercent < 0f) p.MinStrengthPercent = 0f;
            if (p.MinStrengthPercent > 100f) p.MinStrengthPercent = 100f;
            return p;
        }
    }

    /// <summary>Named difficulty ladder (#319).</summary>
    public enum AiDifficultyLevel
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
    }

    /// <summary>Personality pack on top of difficulty (#321).</summary>
    public enum AiPersonality
    {
        Balanced = 0,
        Aggressive = 1,
        Defensive = 2,
    }

    /// <summary>Resolves difficulty × personality into <see cref="DifficultyParams"/>.</summary>
    public static class AiPresets
    {
        public static DifficultyParams ForDifficulty(AiDifficultyLevel level) => level switch
        {
            AiDifficultyLevel.Easy => new DifficultyParams
            {
                EvaluationInterval = 40,
                RetryInterval = 600,
                MinStrengthPercent = 55f,
            },
            AiDifficultyLevel.Hard => new DifficultyParams
            {
                EvaluationInterval = 10,
                RetryInterval = 150,
                MinStrengthPercent = 20f,
            },
            _ => DifficultyParams.Normal(),
        };

        /// <summary>
        /// Applies personality on top of a difficulty base. Aggressive shortens intervals and
        /// lowers the strength floor; Defensive does the opposite; Balanced is identity.
        /// </summary>
        public static DifficultyParams ApplyPersonality(DifficultyParams baseParams,
            AiPersonality personality)
        {
            var p = baseParams;
            switch (personality)
            {
                case AiPersonality.Aggressive:
                    p.EvaluationInterval = Math.Max(1, (p.EvaluationInterval * 2) / 3);
                    p.RetryInterval = Math.Max(1, (p.RetryInterval * 2) / 3);
                    p.MinStrengthPercent = Math.Max(0f, p.MinStrengthPercent - 10f);
                    break;
                case AiPersonality.Defensive:
                    p.EvaluationInterval = (p.EvaluationInterval * 3) / 2;
                    if (p.EvaluationInterval < 1) p.EvaluationInterval = 1;
                    p.RetryInterval = (p.RetryInterval * 3) / 2;
                    if (p.RetryInterval < 1) p.RetryInterval = 1;
                    p.MinStrengthPercent = Math.Min(100f, p.MinStrengthPercent + 15f);
                    break;
            }

            return p.Clamped();
        }

        public static DifficultyParams Resolve(AiDifficultyLevel difficulty,
            AiPersonality personality) =>
            ApplyPersonality(ForDifficulty(difficulty), personality);
    }
}
