// RankLadder.cs
// Echelon → commander's rank title and shoulder-board mark, as configurable data.
//
// Rank is the visible expression of which echelon the player commands (#38). It must be
// derived from that echelon, never set independently — two sources of truth for "what am I
// commanding" would drift, and the one on screen is the one the player would believe.
//
// NATIONAL, NOT GLOBAL. US bars-and-leaves and Soviet pips-and-stars are different geometries
// for the same echelons. A Side carries which ladder it uses; the mapping itself is JSON so a
// scenario can be right about its own forces without a switch statement in code.
//
// Procedural marks rather than Resources PNGs: one insignia is on screen at a time, UiSprites
// already bakes chrome this way, and shipping every rank × ladder as textures would buy a few
// microseconds of draw for kilobytes of store. Bake-on-demand and cache.

using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using Strategos.NatoSymbols;
using Strategos.Scenarios;

namespace Strategos.Units
{
    /// <summary>How the shoulder-board mark is drawn. Geometry, not artwork.</summary>
    public enum RankMark
    {
        /// <summary>Horizontal officer bars (US 2LT / 1LT / CPT).</summary>
        Bars = 0,

        /// <summary>Enlisted chevrons, point up.</summary>
        Chevrons = 1,

        /// <summary>Oak-leaf silhouette (US MAJ / LTC).</summary>
        Leaf = 2,

        /// <summary>Five-point star (US generals; also Soviet large stars).</summary>
        Star = 3,

        /// <summary>Small filled disc — Soviet junior-officer pips.</summary>
        Pip = 4,
    }

    /// <summary>One rung: the rank that typically commands at this echelon, for one ladder.</summary>
    [Serializable]
    public struct RankStep
    {
        public Echelon Echelon;
        public string Title;
        public RankMark Mark;
        public int Count;

        public RankStep(Echelon echelon, string title, RankMark mark, int count) : this()
        {
            Echelon = echelon;
            Title = title ?? string.Empty;
            Mark = mark;
            Count = count;
        }
    }

    /// <summary>A national (or doctrinal) ladder of ranks keyed by command echelon.</summary>
    [Serializable]
    public sealed class RankLadder
    {
        /// <summary>Stable id referenced by <see cref="Side.RankLadder"/>.</summary>
        public string Id = string.Empty;

        public string Name = string.Empty;

        public RankStep[] Steps = Array.Empty<RankStep>();

        /// <summary>
        /// The step for an echelon, or the nearest one at or below it.
        /// </summary>
        /// <remarks>
        /// Falls back downward so a ladder that omits an echelon still yields a mark rather
        /// than blank chrome — blank would look like a missing asset.
        /// </remarks>
        public RankStep For(Echelon echelon)
        {
            RankStep best = default;
            bool found = false;

            for (int i = 0; i < Steps.Length; i++)
            {
                if ((int)Steps[i].Echelon > (int)echelon) continue;
                if (found && (int)Steps[i].Echelon <= (int)best.Echelon) continue;
                best = Steps[i];
                found = true;
            }

            if (found) return best;
            return Steps.Length > 0
                ? Steps[0]
                : new RankStep(echelon, string.Empty, RankMark.Bars, 1);
        }
    }

    /// <summary>Catalogue of shipped ladders.</summary>
    [Serializable]
    public sealed class RankLadderPack
    {
        public string Name = string.Empty;
        public RankLadder[] Ladders = Array.Empty<RankLadder>();

        public RankLadder Find(string id)
        {
            if (string.IsNullOrEmpty(id) || Ladders == null) return null;
            for (int i = 0; i < Ladders.Length; i++)
                if (string.Equals(Ladders[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return Ladders[i];
            return null;
        }
    }

    /// <summary>In-code authoring source for the shipped ladders.</summary>
    public static class RankLadderDefaults
    {
        public const string ConfigName = "rank-ladders";
        public const string UsArmy = "us-army";
        public const string Soviet = "soviet";

        public static RankLadderPack Pack() => new()
        {
            Name = "Command rank ladders",
            Ladders = new[] { UsArmyLadder(), SovietLadder() },
        };

        /// <summary>
        /// Approximate US Army ranks for the commander at each APP-6D echelon.
        /// Approximate on purpose — real appointments vary; the ladder is a signal, not ORBAT law.
        /// </summary>
        public static RankLadder UsArmyLadder() => new()
        {
            Id = UsArmy,
            Name = "US Army",
            Steps = new[]
            {
                new RankStep(Echelon.Team,       "Sergeant",            RankMark.Chevrons, 3),
                new RankStep(Echelon.Squad,      "Staff Sergeant",      RankMark.Chevrons, 3),
                new RankStep(Echelon.Section,    "Staff Sergeant",      RankMark.Chevrons, 3),
                new RankStep(Echelon.Platoon,    "First Lieutenant",    RankMark.Bars,     1),
                new RankStep(Echelon.Company,    "Captain",             RankMark.Bars,     2),
                new RankStep(Echelon.Battalion,  "Lieutenant Colonel",  RankMark.Leaf,     1),
                new RankStep(Echelon.Regiment,   "Colonel",             RankMark.Star,     1),
                new RankStep(Echelon.Brigade,    "Colonel",             RankMark.Star,     1),
                new RankStep(Echelon.Division,   "Major General",       RankMark.Star,     2),
                new RankStep(Echelon.Corps,      "Lieutenant General",  RankMark.Star,     3),
                new RankStep(Echelon.Army,       "General",             RankMark.Star,     4),
                new RankStep(Echelon.ArmyGroup,  "General",             RankMark.Star,     4),
                new RankStep(Echelon.Theater,    "General",             RankMark.Star,     4),
            },
        };

        /// <summary>Soviet-pattern ranks — pips at company and below, stars from battalion up.</summary>
        public static RankLadder SovietLadder() => new()
        {
            Id = Soviet,
            Name = "Soviet / Russian",
            Steps = new[]
            {
                new RankStep(Echelon.Team,       "Sergeant",            RankMark.Chevrons, 2),
                new RankStep(Echelon.Squad,      "Senior Sergeant",     RankMark.Chevrons, 3),
                new RankStep(Echelon.Section,    "Senior Sergeant",     RankMark.Chevrons, 3),
                new RankStep(Echelon.Platoon,    "Lieutenant",          RankMark.Pip,      1),
                new RankStep(Echelon.Company,    "Captain",             RankMark.Pip,      4),
                new RankStep(Echelon.Battalion,  "Lieutenant Colonel",  RankMark.Star,     2),
                new RankStep(Echelon.Regiment,   "Colonel",             RankMark.Star,     3),
                new RankStep(Echelon.Brigade,    "Colonel",             RankMark.Star,     3),
                new RankStep(Echelon.Division,   "Major General",       RankMark.Star,     1),
                new RankStep(Echelon.Corps,      "Lieutenant General",  RankMark.Star,     2),
                new RankStep(Echelon.Army,       "Colonel General",     RankMark.Star,     3),
                new RankStep(Echelon.ArmyGroup,  "Army General",        RankMark.Star,     4),
                new RankStep(Echelon.Theater,    "Marshal",             RankMark.Star,     4),
            },
        };
    }

    /// <summary>Load / save / lookup for rank ladders.</summary>
    public static class RankLadderIO
    {
        public const string ResourceFolder = "Config";

        private static JsonSerializerSettings Settings => new()
        {
            Formatting = Formatting.Indented,
            ContractResolver = new FieldsOnlyResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new StringEnumConverter() },
        };

        public static string ToJson(RankLadderPack pack) =>
            JsonConvert.SerializeObject(pack, Settings);

        public static RankLadderPack FromJson(string json) =>
            string.IsNullOrWhiteSpace(json)
                ? null
                : JsonConvert.DeserializeObject<RankLadderPack>(json, Settings);

        public static void SaveToFile(RankLadderPack pack, string path)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, ToJson(pack));
        }

        private static RankLadderPack _loaded;

        public static RankLadderPack Current => _loaded ??= Load();

        public static void Reload() => _loaded = null;

        /// <summary>Resolves a ladder id, falling back to the US Army table.</summary>
        public static RankLadder Resolve(string id)
        {
            var pack = Current;
            var ladder = pack.Find(id);
            if (ladder != null) return ladder;

            ladder = pack.Find(RankLadderDefaults.UsArmy);
            if (ladder != null) return ladder;

            return RankLadderDefaults.UsArmyLadder();
        }

        private static RankLadderPack Load()
        {
            var asset = Resources.Load<TextAsset>(
                $"{ResourceFolder}/{RankLadderDefaults.ConfigName}");

            var pack = asset == null ? null : FromJson(asset.text);
            if (pack != null && pack.Ladders != null && pack.Ladders.Length > 0) return pack;

            Debug.LogError(
                $"[RankLadderIO] no pack at Resources/{ResourceFolder}/" +
                $"{RankLadderDefaults.ConfigName} — using in-code defaults. " +
                "Run Strategos > Write Sample Config.");
            return RankLadderDefaults.Pack();
        }
    }
}
