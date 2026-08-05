// IContentSource.cs
// #355 / #365: load-by-name seam for Scenario / CampaignChain / DoctrinePack / GlossaryPack.
//
// Static ScenarioIO / CampaignChainIO / TtpIO remain the JSON helpers; Resources-backed
// adapters implement this interface so a Workshop / remote source can replace them later
// without touching PLAY call sites that already go through Load(name).
//
// No paths on the interface — same rule as IGameStore / SaveId.

namespace Strategos.Content
{
    /// <summary>Named content loaded without knowing where the bytes live.</summary>
    public interface IContentSource<T>
    {
        /// <summary>Load by resource / pack name, or null if missing.</summary>
        T Load(string name);
    }
}
