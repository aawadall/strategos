// ResourcesCampaignChainSource.cs
// #355 / #366: Resources-backed IContentSource for CampaignChain.

using Strategos.Campaigns;
using Strategos.Content;
using UnityEngine;

namespace Strategos.Content.Resources
{
    public sealed class ResourcesCampaignChainSource : IContentSource<CampaignChain>
    {
        public CampaignChain Load(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var asset = UnityEngine.Resources.Load<TextAsset>(
                $"{CampaignChainIO.ResourceFolder}/{name}");
            return asset == null ? null : CampaignChainIO.FromJson(asset.text);
        }
    }
}
