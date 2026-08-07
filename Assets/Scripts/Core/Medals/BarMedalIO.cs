// BarMedalIO.cs
// #467: JSON catalogue + Resources load for bar medals.

using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Strategos.Scenarios;

namespace Strategos.Medals
{
    public static class BarMedalIO
    {
        public const string ResourceFolder = "Medals";
        public const string DefaultCatalogName = "alpha-medals";

        public const string EnemyNeutralizedId = "enemy-neutralized";
        public const string ObjectiveSecuredId = "objective-secured";

        private static JsonSerializerSettings Settings => new()
        {
            Formatting = Formatting.Indented,
            ContractResolver = new FieldsOnlyResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new StringEnumConverter() },
        };

        public static string ToJson(BarMedalCatalog catalog) =>
            JsonConvert.SerializeObject(catalog, Settings);

        public static BarMedalCatalog FromJson(string json) =>
            string.IsNullOrWhiteSpace(json)
                ? null
                : JsonConvert.DeserializeObject<BarMedalCatalog>(json, Settings);

        public static BarMedalCatalog Load(string name = DefaultCatalogName)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(
                $"{ResourceFolder}/{name}");
            return asset == null ? null : FromJson(asset.text);
        }

        public static BarMedalDef Find(BarMedalCatalog catalog, string id)
        {
            if (catalog?.Medals == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < catalog.Medals.Length; i++)
            {
                if (catalog.Medals[i] != null && catalog.Medals[i].Id == id)
                    return catalog.Medals[i];
            }
            return null;
        }

        public static void SaveToFile(BarMedalCatalog catalog, string path)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, ToJson(catalog));
        }
    }
}
