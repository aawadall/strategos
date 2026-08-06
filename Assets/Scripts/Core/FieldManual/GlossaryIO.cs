// GlossaryIO.cs
// #205 / #124: field-manual glossary pack — JSON shape + Resources loader.
// Read-only browser is #206; this file is data only until then.

using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Strategos.Scenarios;

namespace Strategos.FieldManual
{
    /// <summary>One glossary entry. Public fields for <see cref="FieldsOnlyResolver"/>.</summary>
    public sealed class GlossaryTerm
    {
        /// <summary>Stable id (kebab or short code), e.g. <c>move-to</c> or <c>orbat</c>.</summary>
        public string Id = string.Empty;

        public string Title = string.Empty;

        /// <summary>Plain-language body shown in the manual.</summary>
        public string Body = string.Empty;

        /// <summary>
        /// Optional drill codes from the doctrine pack (e.g. <c>T1</c>). Empty when the term
        /// is not drill-tied. Binder and field-manual UI resolve these via
        /// <see cref="GlossaryIO.TermsForDrill"/> (#207).
        /// </summary>
        public string[] DrillRefs = System.Array.Empty<string>();
    }

    /// <summary>Named glossary pack — the unit a mod or the shipped alpha ships.</summary>
    public sealed class GlossaryPack
    {
        public string Name = string.Empty;

        /// <summary>Provenance shown to the player.</summary>
        public string Source = string.Empty;

        public GlossaryTerm[] Terms = System.Array.Empty<GlossaryTerm>();
    }

    /// <summary>JSON helpers and Resources load for glossary packs (#205).</summary>
    public static class GlossaryIO
    {
        public const string ResourceFolder = "FieldManual";

        /// <summary>Shipped alpha pack resource name (no extension).</summary>
        public const string DefaultPackName = "alpha-glossary";

        private static JsonSerializerSettings Settings => new()
        {
            Formatting = Formatting.Indented,
            ContractResolver = new FieldsOnlyResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new StringEnumConverter() },
        };

        public static string ToJson(GlossaryPack pack) =>
            JsonConvert.SerializeObject(pack, Settings);

        public static GlossaryPack FromJson(string json) =>
            string.IsNullOrWhiteSpace(json)
                ? null
                : JsonConvert.DeserializeObject<GlossaryPack>(json, Settings);

        public static void SaveToFile(GlossaryPack pack, string path)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, ToJson(pack));
        }

        public static GlossaryPack LoadFromFile(string path) =>
            File.Exists(path) ? FromJson(File.ReadAllText(path)) : null;

        public static GlossaryPack Load(string name) =>
            DefaultContentSource.Load(name);

        public static Strategos.Content.IContentSource<GlossaryPack> DefaultContentSource { get; } =
            new Strategos.Content.Resources.ResourcesGlossaryPackSource();

        /// <summary>
        /// Terms that cite <paramref name="drillCode"/> in <see cref="GlossaryTerm.DrillRefs"/>
        /// (#207 — binder → glossary).
        /// </summary>
        public static GlossaryTerm[] TermsForDrill(GlossaryPack pack, string drillCode)
        {
            if (pack?.Terms == null || string.IsNullOrWhiteSpace(drillCode))
                return System.Array.Empty<GlossaryTerm>();

            var list = new System.Collections.Generic.List<GlossaryTerm>();
            foreach (var t in pack.Terms)
            {
                if (t?.DrillRefs == null) continue;
                foreach (var r in t.DrillRefs)
                {
                    if (string.Equals(r, drillCode, System.StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(t);
                        break;
                    }
                }
            }
            return list.ToArray();
        }
    }
}
