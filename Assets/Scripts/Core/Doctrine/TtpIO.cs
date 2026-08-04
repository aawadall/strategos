// TtpIO.cs
// Reading and writing doctrine packs as JSON, and the runtime library that loads them.
//
// WHY THIS IS DATA AND NOT CODE
// Drills are *content*. Three things follow from that, and each one on its own would be
// enough:
//
//   * Editing a drill should not need a recompile. The set has already been revised twice
//     while it was hard-coded, and each revision cost a build.
//   * Doctrine packs are a planned modding and DLC surface — docs/phases.md 9.3 lists
//     "custom units, terrain packs, doctrine packs" and docs/steam.md prices them. Shipping
//     drills as code would mean building the loader later anyway, against a model that had
//     never been round-tripped.
//   * Phase 5.4's authoring tool needs a format regardless. The model is stable enough now
//     to pick one, and Newtonsoft tolerates absent and unknown fields, so picking it early
//     costs little.
//
// WHY JSON AND NOT AN EMBEDDED DATABASE
// A pack is a few kilobytes, read whole, never queried and never written concurrently — none
// of what a database is for. Against that, JSON diffs in git, which matters for a repository
// that reviews content changes, and it needs no native plugin, which matters because WebGL is
// a build target and SQLite there is a fight. If doctrine ever grows to something that wants
// indexing, the loader is the only thing that changes.
//
// THE SAMPLES ARE THE AUTHORING SOURCE. DoctrineSamples holds the shipped set in code and
// `Strategos > Write Sample Drills` serialises it here — the same split ScenarioSamples and
// ScenarioIO already use. The app never reads the code path in a normal run.

using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using Strategos.Scenarios;

namespace Strategos.Doctrine
{
    /// <summary>
    /// A named set of drills, and the unit a mod or a DLC ships.
    /// </summary>
    /// <remarks>
    /// A pack rather than a file per drill, because drills are read as a set and a player
    /// installing doctrine installs a body of it, not one procedure. It also gives the source
    /// somewhere to live: a drill without provenance is an assertion.
    /// </remarks>
    public sealed class DoctrinePack
    {
        public string Name = string.Empty;

        /// <summary>Where this material comes from. Shown to the player; packs can disagree.</summary>
        public string Source = string.Empty;

        public Ttp[] Drills = System.Array.Empty<Ttp>();
    }

    public static class TtpIO
    {
        /// <summary>Resources sub-folder holding shipped doctrine packs.</summary>
        public const string ResourceFolder = "Doctrine";

        /// <summary>
        /// Deliberately the same shape as <c>ScenarioIO.Settings</c>.
        /// </summary>
        /// <remarks>
        /// Fields-only via the shared <c>FieldsOnlyResolver</c>, so computed members like
        /// <see cref="Ttp.MechanisedSteps"/> stay out of the file; enums by name, so a file
        /// survives an enum being reordered where an integer would silently become a different
        /// drill echelon; and <c>Vector2Converter</c>, without which Newtonsoft walks
        /// <c>Vector2.normalized</c> — itself a Vector2 — until it gives up. Figure points are
        /// Vector2, so that last one is not optional here.
        /// </remarks>
        private static JsonSerializerSettings Settings => new()
        {
            Formatting = Formatting.Indented,
            ContractResolver = new FieldsOnlyResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Converters =
            {
                new StringEnumConverter(),
                new Vector2Converter(),
            },
        };

        public static string ToJson(DoctrinePack pack) =>
            JsonConvert.SerializeObject(pack, Settings);

        public static DoctrinePack FromJson(string json) =>
            string.IsNullOrWhiteSpace(json)
                ? null
                : JsonConvert.DeserializeObject<DoctrinePack>(json, Settings);

        public static void SaveToFile(DoctrinePack pack, string path)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, ToJson(pack));
        }

        public static DoctrinePack LoadFromFile(string path) =>
            File.Exists(path) ? FromJson(File.ReadAllText(path)) : null;

        /// <summary>
        /// Loads a shipped pack by name. Thin-wraps
        /// <see cref="Strategos.Content.Resources.ResourcesDoctrinePackSource"/> (#355 / #366).
        /// </summary>
        public static DoctrinePack Load(string name) =>
            DefaultContentSource.Load(name);

        /// <summary>Default Resources-backed content source (#355 / #366).</summary>
        public static Strategos.Content.IContentSource<DoctrinePack> DefaultContentSource { get; } =
            new Strategos.Content.Resources.ResourcesDoctrinePackSource();
    }

    /// <summary>The drills available at runtime.</summary>
    public static class TtpLibrary
    {
        private static List<Ttp> _all;
        private static string _source;

        /// <summary>Where the loaded drills came from. Shown in the binder.</summary>
        public static string Source => _source ?? string.Empty;

        public static IReadOnlyList<Ttp> All => _all ??= LoadAll();

        public static Ttp Find(string code)
        {
            var all = All;
            for (int i = 0; i < all.Count; i++)
                if (string.Equals(all[i].Code, code, System.StringComparison.OrdinalIgnoreCase))
                    return all[i];
            return null;
        }

        /// <summary>Drops the cache. For the editor, after rewriting a pack.</summary>
        public static void Reload() { _all = null; _source = null; }

        /// <summary>
        /// Loads the shipped pack, falling back to the in-code set if it is missing.
        /// </summary>
        /// <remarks>
        /// **The fallback logs an error rather than passing silently.** A missing pack means
        /// the Resources folder did not ship, which is a packaging bug and exactly the kind
        /// that hides: the binder would look perfectly normal, because the samples are the
        /// same drills. Falling back keeps the app usable; the error is what stops the bug
        /// being invisible. Same reasoning as the note in Known gaps about green CI meaning
        /// nothing ran.
        /// </remarks>
        private static List<Ttp> LoadAll()
        {
            var pack = TtpIO.Load(DoctrineSamples.PackName);

            if (pack?.Drills == null || pack.Drills.Length == 0)
            {
                Debug.LogError(
                    $"[TtpLibrary] no doctrine pack at Resources/{TtpIO.ResourceFolder}/" +
                    $"{DoctrineSamples.PackName} — falling back to the in-code samples. " +
                    "Run Strategos > Write Sample Drills.");
                var samples = DoctrineSamples.Pack();
                _source = samples.Source + "  (in-code fallback)";
                return new List<Ttp>(samples.Drills);
            }

            _source = pack.Source;
            return new List<Ttp>(pack.Drills);
        }
    }
}
