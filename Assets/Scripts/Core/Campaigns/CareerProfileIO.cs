// CareerProfileIO.cs
// JSON for CareerProfile — same Newtonsoft shape as CampaignChainIO / RankAuthorityIO.

using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using Strategos.Scenarios;

namespace Strategos.Campaigns
{
    public static class CareerProfileIO
    {
        public const string ResourceFolder = "Config";
        public const string ConfigName = "career-profile";

        static CareerProfile _current;

        public static CareerProfile Current
        {
            get
            {
                if (_current == null) Reload();
                return _current;
            }
        }

        public static void Reload()
        {
            var asset = Resources.Load<TextAsset>($"{ResourceFolder}/{ConfigName}");
            if (asset != null)
            {
                try
                {
                    _current = FromJson(asset.text) ?? CareerProfile.Default();
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[CareerProfileIO] bad JSON, using defaults: {e.Message}");
                }
            }
            _current = CareerProfile.Default();
        }

        public static void SetCurrent(CareerProfile profile) =>
            _current = profile ?? CareerProfile.Default();

        public static string ToJson(CareerProfile profile) =>
            JsonConvert.SerializeObject(profile, Formatting.Indented, JsonSettings());

        public static CareerProfile FromJson(string json) =>
            JsonConvert.DeserializeObject<CareerProfile>(json, JsonSettings());

        public static void SaveToFile(string absolutePath, CareerProfile profile)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? ".");
            File.WriteAllText(absolutePath, ToJson(profile));
        }

        static JsonSerializerSettings JsonSettings() => new()
        {
            ContractResolver = new FieldsOnlyResolver(),
            Converters = { new StringEnumConverter() },
            NullValueHandling = NullValueHandling.Ignore,
        };
    }
}
