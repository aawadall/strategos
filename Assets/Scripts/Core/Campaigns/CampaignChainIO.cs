// CampaignChainIO.cs
// Reading and writing a CampaignChain as JSON. Mirrors Scenarios/ScenarioIO.cs exactly — same
// Newtonsoft settings, same reasons. See that file's header for why JsonUtility cannot be used
// (no Nullable<T>, skips readonly fields/properties/dictionaries) and why every Unity type
// needs an explicit converter (Newtonsoft walks properties too, and Vector2.normalized is a
// Vector2 — see ScenarioIO.Vector2Converter).
//
// REUSES ScenarioIO's resolver and converters rather than duplicating them. Both live under
// Assets/Scripts/, which is one assembly (Strategos.Runtime.asmdef) — FieldsOnlyResolver is
// `internal` and Vector2Converter/ColorConverter are public, and all three are visible from
// this namespace without changing their access. A CampaignChainEntry's CarriedOverUnits is a
// List<UnitInstance>, the exact same field shape Scenario.Units already round-trips through
// these settings, so nothing here needs a converter of its own.

using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using Strategos.Scenarios;

namespace Strategos.Campaigns
{
    public static class CampaignChainIO
    {
        /// <summary>Resources sub-folder holding shipped campaign chains.</summary>
        public const string ResourceFolder = "Campaigns";

        private static JsonSerializerSettings Settings => new()
        {
            Formatting = Formatting.Indented,

            // Fields only — see ScenarioIO.FieldsOnlyResolver's own remarks.
            ContractResolver = new FieldsOnlyResolver(),

            // Omit nulls for the same reason ScenarioIO does: a hand-authored file should not
            // carry absent optionals.
            NullValueHandling = NullValueHandling.Ignore,

            // Enums by name, same as ScenarioIO — "Outcome": "Won" survives an enum being
            // reordered where "Outcome": 1 silently becomes a different result.
            Converters =
            {
                new StringEnumConverter(),
                new Vector2Converter(),
                new ColorConverter(),
            },
        };

        public static string ToJson(CampaignChain chain) =>
            JsonConvert.SerializeObject(chain, Settings);

        public static CampaignChain FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonConvert.DeserializeObject<CampaignChain>(json, Settings);
        }

        // ─── Files ────────────────────────────────────────────────────────────

        public static void SaveToFile(CampaignChain chain, string path)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, ToJson(chain));
        }

        public static CampaignChain LoadFromFile(string path) =>
            File.Exists(path) ? FromJson(File.ReadAllText(path)) : null;

        /// <summary>
        /// Loads a shipped campaign chain by name. Thin-wraps
        /// <see cref="Strategos.Content.Resources.ResourcesCampaignChainSource"/> (#355 / #366).
        /// </summary>
        public static CampaignChain Load(string name) =>
            DefaultContentSource.Load(name);

        /// <summary>Default Resources-backed content source (#355 / #366).</summary>
        public static Strategos.Content.IContentSource<CampaignChain> DefaultContentSource { get; } =
            new Strategos.Content.Resources.ResourcesCampaignChainSource();
    }
}
