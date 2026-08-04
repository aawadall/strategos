// ScenarioIO.cs
// Reading and writing scenarios as JSON.
//
// WHY NEWTONSOFT AND NOT JsonUtility
// UnityEngine.JsonUtility cannot serialise Nullable<T>, and MapGenerationSettings has a
// `ReliefParameters? ParameterOverride` sitting in the middle of what a scenario must
// store. It also skips readonly fields, properties and dictionaries. Newtonsoft handles all
// of it and is already in the project (com.unity.nuget.newtonsoft-json, in the manifest).
//
// THE UNITY TYPE TRAP
// Newtonsoft serialises public fields *and* public properties, and Unity's maths types have
// properties that return their own type — Vector2.normalized is a Vector2, Color.linear is a
// Color. Serialising those recurses until the depth limit. Every Unity type stored in a
// scenario therefore needs an explicit converter; add one here before adding such a field.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Strategos.Scenarios
{
    public static class ScenarioIO
    {
        /// <summary>Resources sub-folder holding shipped scenarios.</summary>
        public const string ResourceFolder = "Scenarios";

        private static JsonSerializerSettings Settings => new()
        {
            Formatting = Formatting.Indented,

            // Fields only — see FieldsOnlyResolver.
            ContractResolver = new FieldsOnlyResolver(),

            // Omit nulls so a hand-authored file is not cluttered with absent optionals —
            // notably ParameterOverride, which is null for every scenario using a stock
            // relief profile.
            NullValueHandling = NullValueHandling.Ignore,

            // Enums by name. A scenario is meant to be read and edited by a person, and
            // "Profile": "Rolling" survives an enum being reordered where "Profile": 1
            // silently becomes a different terrain.
            Converters =
            {
                new StringEnumConverter(),
                new Vector2Converter(),
                new ColorConverter(),
            },
        };

        public static string ToJson(Scenario scenario) =>
            JsonConvert.SerializeObject(scenario, Settings);

        public static Scenario FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonConvert.DeserializeObject<Scenario>(json, Settings);
        }

        // ─── Files ────────────────────────────────────────────────────────────

        public static void SaveToFile(Scenario scenario, string path)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, ToJson(scenario));
        }

        public static Scenario LoadFromFile(string path) =>
            File.Exists(path) ? FromJson(File.ReadAllText(path)) : null;

        /// <summary>
        /// Loads a shipped scenario by name, e.g. <c>Load("skirmish")</c>.
        /// Thin-wraps <see cref="Strategos.Content.Resources.ResourcesScenarioSource"/> (#355).
        /// </summary>
        public static Scenario Load(string name) =>
            DefaultContentSource.Load(name);

        /// <summary>Default Resources-backed content source (#355 / #365).</summary>
        public static Strategos.Content.IContentSource<Scenario> DefaultContentSource { get; } =
            new Strategos.Content.Resources.ResourcesScenarioSource();
    }

    /// <summary>
    /// Serialises public **fields** and ignores properties entirely.
    ///
    /// Newtonsoft's default writes both, and every computed property on these types then
    /// lands in the file: <c>UnitId.IsValid</c> on every id, <c>GeoOrigin.IsNorthernHemisphere</c>,
    /// and — worst — <c>Scenario.IsValid</c>, which runs the whole validator and allocates a
    /// list every time a scenario is saved. All of it is derived, none of it can be read
    /// back, and it is noise in a file meant to be hand-edited.
    ///
    /// Fields-only also keeps the model serialiser-agnostic: it is the same rule Unity's own
    /// JsonUtility applies, so the data types need no attributes and a future computed
    /// property is excluded automatically rather than by remembering to mark it.
    /// </summary>
    internal sealed class FieldsOnlyResolver : DefaultContractResolver
    {
        protected override List<MemberInfo> GetSerializableMembers(Type objectType) =>
            objectType
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !f.IsInitOnly && !f.IsDefined(typeof(JsonIgnoreAttribute), true))
                .Cast<MemberInfo>()
                .ToList();
    }

    // ─── Unity type converters ────────────────────────────────────────────────

    /// <summary>
    /// Vector2 as <c>{ "x": .., "y": .. }</c>.
    ///
    /// Without this Newtonsoft walks <c>normalized</c>, which is itself a Vector2 with a
    /// <c>normalized</c>, and recurses until it gives up.
    /// </summary>
    public sealed class Vector2Converter : JsonConverter<Vector2>
    {
        public override void WriteJson(JsonWriter w, Vector2 v, JsonSerializer s)
        {
            w.WriteStartObject();
            w.WritePropertyName("x"); w.WriteValue(v.x);
            w.WritePropertyName("y"); w.WriteValue(v.y);
            w.WriteEndObject();
        }

        public override Vector2 ReadJson(JsonReader r, Type t, Vector2 existing,
            bool hasExisting, JsonSerializer s)
        {
            float x = 0f, y = 0f;
            while (r.Read() && r.TokenType != JsonToken.EndObject)
            {
                if (r.TokenType != JsonToken.PropertyName) continue;
                var name = (string)r.Value;
                r.Read();
                if (name == "x") x = Convert.ToSingle(r.Value);
                else if (name == "y") y = Convert.ToSingle(r.Value);
            }
            return new Vector2(x, y);
        }
    }

    /// <summary>Color as <c>{ "r": .., "g": .., "b": .., "a": .. }</c>. Same recursion reason.</summary>
    public sealed class ColorConverter : JsonConverter<Color>
    {
        public override void WriteJson(JsonWriter w, Color c, JsonSerializer s)
        {
            w.WriteStartObject();
            w.WritePropertyName("r"); w.WriteValue(c.r);
            w.WritePropertyName("g"); w.WriteValue(c.g);
            w.WritePropertyName("b"); w.WriteValue(c.b);
            w.WritePropertyName("a"); w.WriteValue(c.a);
            w.WriteEndObject();
        }

        public override Color ReadJson(JsonReader r, Type t, Color existing,
            bool hasExisting, JsonSerializer s)
        {
            float cr = 0f, cg = 0f, cb = 0f, ca = 1f;
            while (r.Read() && r.TokenType != JsonToken.EndObject)
            {
                if (r.TokenType != JsonToken.PropertyName) continue;
                var name = (string)r.Value;
                r.Read();
                switch (name)
                {
                    case "r": cr = Convert.ToSingle(r.Value); break;
                    case "g": cg = Convert.ToSingle(r.Value); break;
                    case "b": cb = Convert.ToSingle(r.Value); break;
                    case "a": ca = Convert.ToSingle(r.Value); break;
                }
            }
            return new Color(cr, cg, cb, ca);
        }
    }
}
