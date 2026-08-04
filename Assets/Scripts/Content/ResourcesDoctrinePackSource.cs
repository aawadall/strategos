// ResourcesDoctrinePackSource.cs
// #355 / #366: Resources-backed IContentSource for DoctrinePack.

using Strategos.Content;
using Strategos.Doctrine;
using UnityEngine;

namespace Strategos.Content.Resources
{
    public sealed class ResourcesDoctrinePackSource : IContentSource<DoctrinePack>
    {
        public DoctrinePack Load(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var asset = UnityEngine.Resources.Load<TextAsset>(
                $"{TtpIO.ResourceFolder}/{name}");
            return asset == null ? null : TtpIO.FromJson(asset.text);
        }
    }
}
