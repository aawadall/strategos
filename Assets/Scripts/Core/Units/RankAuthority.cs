// RankAuthority.cs
// #76 / #222: career rank → maximum command echelon (difficulty curve).
//
// Distinct from RankLadder (#38): that maps ORBAT echelon → shoulder-board display.
// This table answers "may this career rank command that echelon?" and "what is the next
// promotion?" Rank is stored on AppSession; display still derives from the live ORBAT.

using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using Strategos.NatoSymbols;
using Strategos.Scenarios;

namespace Strategos.Units
{
    /// <summary>One career rung and the highest echelon it may command.</summary>
    [Serializable]
    public struct RankAuthorityStep
    {
        public string Id;
        public string Title;
        public Echelon MaxEchelon;

        public RankAuthorityStep(string id, string title, Echelon maxEchelon) : this()
        {
            Id = id ?? string.Empty;
            Title = title ?? string.Empty;
            MaxEchelon = maxEchelon;
        }
    }

    /// <summary>Ordered low→high career ranks with max command echelons.</summary>
    [Serializable]
    public sealed class RankAuthorityTable
    {
        public string Name = "Rank authority";
        public RankAuthorityStep[] Steps = Array.Empty<RankAuthorityStep>();

        public RankAuthorityStep? Find(string rankId)
        {
            if (string.IsNullOrEmpty(rankId) || Steps == null) return null;
            for (int i = 0; i < Steps.Length; i++)
                if (string.Equals(Steps[i].Id, rankId, StringComparison.OrdinalIgnoreCase))
                    return Steps[i];
            return null;
        }

        public int IndexOf(string rankId)
        {
            if (string.IsNullOrEmpty(rankId) || Steps == null) return -1;
            for (int i = 0; i < Steps.Length; i++)
                if (string.Equals(Steps[i].Id, rankId, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        public Echelon MaxEchelonFor(string rankId)
        {
            var step = Find(rankId);
            return step?.MaxEchelon ?? Echelon.None;
        }

        public bool MayCommand(string rankId, Echelon required)
        {
            if (required == Echelon.None) return true;
            var max = MaxEchelonFor(rankId);
            if (max == Echelon.None) return false;
            return (int)max >= (int)required;
        }

        /// <summary>Advance one rung. Returns false at the top of the table.</summary>
        public bool TryPromote(ref string rankId)
        {
            int i = IndexOf(rankId);
            if (i < 0 || i + 1 >= Steps.Length) return false;
            rankId = Steps[i + 1].Id;
            return true;
        }
    }

    public static class RankAuthorityDefaults
    {
        public const string ConfigName = "rank-authority";

        /// <summary>Shipped scenarios put a battalion at the top of the player ORBAT.</summary>
        public const string DefaultRankId = "battalion";

        public static RankAuthorityTable UsArmy() => new()
        {
            Name = "US Army command authority",
            Steps = new[]
            {
                new RankAuthorityStep("platoon", "Platoon leader", Echelon.Platoon),
                new RankAuthorityStep("company", "Company commander", Echelon.Company),
                new RankAuthorityStep("battalion", "Battalion commander", Echelon.Battalion),
                new RankAuthorityStep("regiment", "Regiment / group commander", Echelon.Regiment),
                new RankAuthorityStep("brigade", "Brigade commander", Echelon.Brigade),
                new RankAuthorityStep("division", "Division commander", Echelon.Division),
                new RankAuthorityStep("corps", "Corps commander", Echelon.Corps),
            },
        };
    }

    /// <summary>JSON load for <see cref="RankAuthorityTable"/> — same pattern as RankLadderIO.</summary>
    public static class RankAuthorityIO
    {
        public const string ResourceFolder = "Config";

        static RankAuthorityTable _current;

        public static RankAuthorityTable Current
        {
            get
            {
                if (_current == null) Reload();
                return _current;
            }
        }

        public static void Reload()
        {
            var asset = Resources.Load<TextAsset>($"{ResourceFolder}/{RankAuthorityDefaults.ConfigName}");
            if (asset != null)
            {
                try
                {
                    _current = FromJson(asset.text) ?? RankAuthorityDefaults.UsArmy();
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[RankAuthorityIO] bad JSON, using defaults: {e.Message}");
                }
            }
            _current = RankAuthorityDefaults.UsArmy();
        }

        public static string ToJson(RankAuthorityTable table) =>
            JsonConvert.SerializeObject(table, Formatting.Indented, JsonSettings());

        public static RankAuthorityTable FromJson(string json) =>
            JsonConvert.DeserializeObject<RankAuthorityTable>(json, JsonSettings());

        public static void SaveToFile(string absolutePath, RankAuthorityTable table)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? ".");
            File.WriteAllText(absolutePath, ToJson(table));
        }

        static JsonSerializerSettings JsonSettings() => new()
        {
            Converters = { new StringEnumConverter() },
            NullValueHandling = NullValueHandling.Ignore,
        };
    }

    /// <summary>#76 / #223–#224: authorize a scenario against career rank; promote on win.</summary>
    public static class RankGate
    {
        /// <summary>
        /// Highest SIDC echelon on the player side's units (same rule as PlayView.CommandEchelon).
        /// </summary>
        public static Echelon RequiredEchelon(Scenario scenario)
        {
            if (scenario?.Units == null) return Echelon.None;

            var echelon = Echelon.None;
            for (int i = 0; i < scenario.Units.Count; i++)
            {
                var unit = scenario.Units[i];
                if (unit == null) continue;
                if (scenario.PlayerSide.IsValid && unit.Side != scenario.PlayerSide) continue;

                var e = unit.ToSidcCode().Echelon;
                if ((int)e > (int)echelon) echelon = e;
            }
            return echelon;
        }

        public static bool Authorize(
            string careerRankId, Scenario scenario, out string problem,
            RankAuthorityTable table = null)
        {
            problem = null;
            table ??= RankAuthorityIO.Current;
            var required = RequiredEchelon(scenario);
            if (table.MayCommand(careerRankId, required)) return true;

            var step = table.Find(careerRankId);
            string have = step?.Title ?? careerRankId ?? "(none)";
            problem =
                $"RANK GATE: {have} may command up to {table.MaxEchelonFor(careerRankId)}, " +
                $"scenario requires {required}";
            return false;
        }

        /// <summary>
        /// One promotion after a won multi-op campaign completes. No-op if already at top.
        /// </summary>
        public static bool TryPromoteAfterCampaignWin(ref string careerRankId,
            RankAuthorityTable table = null)
        {
            table ??= RankAuthorityIO.Current;
            return table.TryPromote(ref careerRankId);
        }
    }
}
