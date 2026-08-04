// CampaignSamples.cs
// Names of shipped campaign chains under Resources/Campaigns — same role as ScenarioSamples.

namespace Strategos.Campaigns
{
    public static class CampaignSamples
    {
        /// <summary>
        /// Three-op valley chain (skirmish → push-north → skirmish). The #75 / #139 fixture.
        /// </summary>
        public const string ValleyName = "valley-campaign";

        /// <summary>
        /// One-op highland chain at regiment seat — #109 second theatre after valley promotion.
        /// </summary>
        public const string HighlandName = "highland-campaign";

        /// <summary>
        /// Three-op climb chain (Squad → Company → Battalion). The #403 / #406 fixture.
        /// </summary>
        public const string ClimbName = "climb-campaign";
    }
}
