// EchelonSpans.cs
// How much ground a commander at a given echelon may have on screen — as configurable data.
//
// ZOOM IS A MECHANIC, NOT A CAMERA CONTROL. ROADMAP.md's central claim is that the echelon you
// command decides *how much of the command problem is present at all*: at fire team you are
// the unit and see what it sees; at corps you cannot see the front and issue intent rather
// than instructions. A view that let a corps commander zoom to a single platoon would quietly
// contradict that, because the natural thing to do with a platoon on screen is to order it.
//
// BANDS ARE CONTIGUOUS, NOT OVERLAPPING. The first cut gave every echelon a wide window and
// they overlapped heavily — a 500 m view was legal for a fire team, a squad, a section and a
// platoon alike, so the band said nothing about whose scale it was. Each echelon now owns a
// range of scale that begins where its subordinate's ends: zooming out past your band is
// exactly "that is your superior's scale", which is the statement the mechanic exists to make.
// EchelonProbe asserts contiguity, so a hand-edited table that leaves a gap or an overlap
// fails rather than silently making two echelons feel the same.
//
// CONFIGURABLE, because these are balance numbers and balance numbers are content. Same split
// as doctrine packs: the defaults below are the authoring source, `Strategos > Write Sample
// Config` serialises them to Resources/Config, and the game reads the JSON. Tuning the feel of
// an echelon should not need a recompile.

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using Strategos.NatoSymbols;
using Strategos.Scenarios;

namespace Strategos.Units
{
    /// <summary>The band of ground widths a commander at some echelon may view.</summary>
    public struct EchelonSpan
    {
        /// <summary>Which echelon this band belongs to.</summary>
        public Echelon Echelon;

        /// <summary>Narrowest view, in metres across. Below this is detail you are not owed.</summary>
        public float MinMetres;

        /// <summary>Widest view, in metres across. Beyond this is ground that is not yours.</summary>
        public float MaxMetres;

        public EchelonSpan(Echelon echelon, float min, float max) : this()
        {
            Echelon = echelon;
            MinMetres = min;
            MaxMetres = max;
        }

        /// <summary>
        /// The same band, clamped to a map that may be smaller than the echelon's reach.
        /// </summary>
        /// <remarks>
        /// A battalion on a 6.4 km sheet may see the whole sheet and no more: there is no
        /// ground beyond it, and letting the view widen past the map would put the sheet in a
        /// frame of nothing.
        ///
        /// **The band keeps its zoom *ratio* when the ceiling is clamped**, rather than only
        /// its floor. Clamping the top alone and leaving the bottom where it was gave a
        /// battalion 6000-6400 m on the shipped sheet — 1.1x, which is no zoom at all — and
        /// everything above regiment exactly none, because their floors were already past the
        /// map. The scenario that ships is commanded at battalion, so the feature was dead in
        /// the only place anyone would have met it, and the probe still passed because a
        /// collapsed range is technically still a range.
        ///
        /// Preserving the ratio keeps the statement the band is making — this is your scale —
        /// while measuring it against the ground that actually exists. A map too small for an
        /// echelon is a scenario mismatch (6.4 km is one company frontage; see Known gaps) and
        /// not something the view should punish the player for.
        /// </remarks>
        public EchelonSpan ClampedTo(float mapMetres)
        {
            if (mapMetres <= 0f || MinMetres <= 0f) return this;

            float max = Mathf.Min(MaxMetres, mapMetres);
            if (max >= MaxMetres) return this;

            float ratio = Mathf.Max(1.01f, MaxMetres / MinMetres);
            return new EchelonSpan(Echelon, max / ratio, max);
        }

        public override string ToString() => $"{Echelon}: {MinMetres:0} m to {MaxMetres:0} m";
    }

    /// <summary>The whole table, as a loadable document.</summary>
    public sealed class EchelonSpanTable
    {
        public string Name = string.Empty;

        /// <summary>One band per echelon, in ascending order of echelon.</summary>
        public EchelonSpan[] Spans = System.Array.Empty<EchelonSpan>();

        /// <summary>
        /// The band for an echelon, or the nearest one below it.
        /// </summary>
        /// <remarks>
        /// Falls back downward rather than returning nothing, so a table that omits an
        /// echelon still yields a sane view instead of an unbounded one — an unbounded zoom
        /// is precisely the failure this type exists to prevent, and it should not be what a
        /// typo produces.
        /// </remarks>
        public EchelonSpan For(Echelon echelon)
        {
            EchelonSpan best = default;
            bool found = false;

            for (int i = 0; i < Spans.Length; i++)
            {
                if ((int)Spans[i].Echelon > (int)echelon) continue;
                if (found && (int)Spans[i].Echelon <= (int)best.Echelon) continue;
                best = Spans[i];
                found = true;
            }

            if (found) return best;
            return Spans.Length > 0 ? Spans[0] : new EchelonSpan(echelon, 150f, 600f);
        }
    }

    /// <summary>The shipped table, in code. The authoring source, not the runtime one.</summary>
    public static class EchelonSpanDefaults
    {
        /// <summary>Resource name of the shipped table.</summary>
        public const string ConfigName = "echelon-spans";

        /// <summary>
        /// Contiguous bands: each echelon begins exactly where its subordinate ends.
        /// </summary>
        /// <remarks>
        /// Frontages, not arbitrary numbers — a rifle company holds something like a
        /// kilometre, a battalion a few, a brigade ten or more — but they are a first cut and
        /// are meant to be argued with. Read the table `EchelonProbe` prints rather than
        /// trusting the constants, and edit the JSON rather than this.
        ///
        /// Section deliberately shares Squad's band. APP-6D treats them as distinct echelons
        /// and most armies do not, so giving them separate scales would invent a difference
        /// the rest of the game does not model.
        /// </remarks>
        public static EchelonSpanTable Table() => new()
        {
            Name = "Contiguous frontage bands",
            Spans = new[]
            {
                new EchelonSpan(Echelon.None,      120f,    600f),
                new EchelonSpan(Echelon.Team,      120f,    600f),
                new EchelonSpan(Echelon.Squad,     600f,   1200f),
                new EchelonSpan(Echelon.Section,   600f,   1200f),
                new EchelonSpan(Echelon.Platoon,  1200f,   2500f),
                new EchelonSpan(Echelon.Company,  2500f,   6000f),
                new EchelonSpan(Echelon.Battalion, 6000f, 15000f),
                new EchelonSpan(Echelon.Regiment, 15000f, 30000f),
                new EchelonSpan(Echelon.Brigade,  15000f, 30000f),
                new EchelonSpan(Echelon.Division, 30000f, 80000f),
                new EchelonSpan(Echelon.Corps,    80000f, 200000f),
                new EchelonSpan(Echelon.Army,    200000f, 500000f),
            },
        };
    }

    /// <summary>Reading and writing the table, and the copy the game uses.</summary>
    public static class EchelonSpanIO
    {
        /// <summary>Resources sub-folder holding tunable configuration.</summary>
        public const string ResourceFolder = "Config";

        private static JsonSerializerSettings Settings => new()
        {
            Formatting = Formatting.Indented,
            ContractResolver = new FieldsOnlyResolver(),
            NullValueHandling = NullValueHandling.Ignore,

            // Enums by name: a table meant to be hand-edited must survive the echelon enum
            // being reordered, where an integer would silently become a different echelon.
            Converters = { new StringEnumConverter() },
        };

        public static string ToJson(EchelonSpanTable table) =>
            JsonConvert.SerializeObject(table, Settings);

        public static EchelonSpanTable FromJson(string json) =>
            string.IsNullOrWhiteSpace(json)
                ? null
                : JsonConvert.DeserializeObject<EchelonSpanTable>(json, Settings);

        public static void SaveToFile(EchelonSpanTable table, string path)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, ToJson(table));
        }

        private static EchelonSpanTable _loaded;

        /// <summary>The table the game is using. Loaded once.</summary>
        public static EchelonSpanTable Current => _loaded ??= Load();

        /// <summary>Drops the cache. For the editor, after rewriting the file.</summary>
        public static void Reload() => _loaded = null;

        private static EchelonSpanTable Load()
        {
            var asset = Resources.Load<TextAsset>(
                $"{ResourceFolder}/{EchelonSpanDefaults.ConfigName}");

            var table = asset == null ? null : FromJson(asset.text);
            if (table != null && table.Spans.Length > 0) return table;

            // Logged rather than passed over: a missing config means the Resources folder did
            // not ship, and the fallback is the same numbers, so the view would look entirely
            // normal while the file was doing nothing.
            Debug.LogError(
                $"[EchelonSpanIO] no table at Resources/{ResourceFolder}/" +
                $"{EchelonSpanDefaults.ConfigName} — using the in-code defaults. " +
                "Run Strategos > Write Sample Config.");
            return EchelonSpanDefaults.Table();
        }
    }
}
