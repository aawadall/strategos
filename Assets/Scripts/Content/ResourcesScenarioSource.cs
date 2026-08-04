// ResourcesScenarioSource.cs
// #355 / #365: Resources-backed IContentSource for Scenario.

using Strategos.Content;
using Strategos.Scenarios;
using UnityEngine;

namespace Strategos.Content.Resources
{
    public sealed class ResourcesScenarioSource : IContentSource<Scenario>
    {
        public Scenario Load(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var asset = UnityEngine.Resources.Load<TextAsset>(
                $"{ScenarioIO.ResourceFolder}/{name}");
            return asset == null ? null : ScenarioIO.FromJson(asset.text);
        }
    }
}
