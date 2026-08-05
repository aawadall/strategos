// ResourcesGlossaryPackSource.cs
// #205 / #124: Resources-backed IContentSource for GlossaryPack.

using Strategos.Content;
using Strategos.FieldManual;
using UnityEngine;

namespace Strategos.Content.Resources
{
    public sealed class ResourcesGlossaryPackSource : IContentSource<GlossaryPack>
    {
        public GlossaryPack Load(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var asset = UnityEngine.Resources.Load<TextAsset>(
                $"{GlossaryIO.ResourceFolder}/{name}");
            return asset == null ? null : GlossaryIO.FromJson(asset.text);
        }
    }
}
