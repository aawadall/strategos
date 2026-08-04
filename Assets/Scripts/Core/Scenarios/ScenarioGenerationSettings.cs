// ScenarioGenerationSettings.cs
// #334 / #347: parameters for ScenarioGenerator — data only.
//
// Distinct from MapGenerationSettings (terrain) and from authored ScenarioSamples.
// ForceRatio is enemy leaf count / friendly leaf count. Objective feature placement
// stays #51 — cells are stubbed by the generator until then.

using System;
using Strategos.Maps;
using Strategos.NatoSymbols;

namespace Strategos.Scenarios
{
    /// <summary>Engagement shape that drives victory templates (#350).</summary>
    public enum EngagementType
    {
        /// <summary>Neutral objective; both sides Hold + Destroy.</summary>
        Meeting = 0,
        /// <summary>Friendly owns the objective; defender SurviveUntil + Hold.</summary>
        Defend = 1,
        /// <summary>Enemy owns the objective; attacker Hold + Destroy to seize.</summary>
        Attack = 2,
    }

    /// <summary>
    /// Inputs to <see cref="ScenarioGenerator.Generate"/>. Does not hold a Scenario —
    /// that is the output.
    /// </summary>
    [Serializable]
    public sealed class ScenarioGenerationSettings
    {
        public int Seed = 20260804;

        /// <summary>Top formation echelon for each side's root (Company / Battalion / …).</summary>
        public Echelon Echelon = Echelon.Company;

        /// <summary>Enemy leaf count ÷ friendly leaf count. 1 = equal; 1.5 = enemy half again.</summary>
        public float ForceRatio = 1f;

        public EngagementType Engagement = EngagementType.Meeting;

        public int Width = 64;
        public int Height = 64;
        public float MetresPerCell = 25f;
        public ReliefProfile Profile = ReliefProfile.Rolling;

        /// <summary>Training loops default off; fidelity checks can turn it on.</summary>
        public bool EnableErosion = false;
        public bool EnableCulture = false;

        public int TimeLimitTicks = 1800;
        public int HoldTicks = 300;

        /// <summary>Tolerance band around ForceRatio for <see cref="ScenarioGenerator.ValidateGenerated"/>.</summary>
        public float ForceRatioTolerance = 0.35f;

        public MapGenerationSettings ToMapSettings() => new()
        {
            Name = "Generated",
            Seed = Seed,
            Width = Width,
            Height = Height,
            MetresPerCell = MetresPerCell,
            Profile = Profile,
            EnableErosion = EnableErosion,
            EnableCulture = EnableCulture,
        };
    }
}
