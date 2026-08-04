// FileGameStore.cs
// The file-backed IGameStore — WebGL's fallback and, per #74, the only implementation this
// change ships. The embedded-database implementation (#66's SQLite-or-LiteDB question) is a
// follow-up; if this file is ever tempted to grow a query, that is the sign the follow-up has
// arrived, not a reason to add one here.
//
// #355: SaveAsync / LoadAsync / … return StoreResult via Task.FromResult — file IO stays on
// the calling thread today; a remote store can await without changing the interface.
//
// LIVES OUTSIDE Core ON PURPOSE. Core/Persistence/IGameStore.cs defines the seam and the data
// it carries; Core/Commands/Simulation.cs knows how to turn itself into a SimulationSnapshot
// and back. Neither of those files reads or writes a byte. This one does, and it is the only
// one that needs to know a save is a JSON file on disk rather than a row in a database or an
// IndexedDB entry.
//
// ONE FILE PER SAVE, NAME = SAVE ID. The simplest thing that satisfies IGameStore: Save writes
// a whole file, Load reads a whole file, ListSaves is a directory listing. A save is a few
// units' worth of state plus a handful of logs — kilobytes, not the megabytes a scenario's
// terrain would be — so "read it whole" costs nothing, the same reasoning ScenarioIO gives for
// scenarios being JSON rather than a database.
//
// REUSES ScenarioIO's CONVERTERS. Vector2Converter, ColorConverter and FieldsOnlyResolver solve
// the identical "Newtonsoft walks a Unity type's own properties and recurses" problem this
// project already fixed once; a second copy here would be the same fix drifting from the
// original the first time either one changed.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Strategos.Persistence;
using Strategos.Scenarios;
using UnityEngine;

namespace Strategos.Persistence.Files
{
    public sealed class FileGameStore : IGameStore
    {
        private const string Extension = ".save.json";

        private readonly string _directory;

        /// <summary><c>Application.persistentDataPath</c>/Saves — survives a rebuild, per-user on every target.</summary>
        public static string DefaultDirectory => Path.Combine(Application.persistentDataPath, "Saves");

        public FileGameStore(string directory)
        {
            _directory = string.IsNullOrWhiteSpace(directory)
                ? throw new ArgumentException("A FileGameStore needs a directory.", nameof(directory))
                : directory;
        }

        private string PathFor(string saveId) =>
            Path.Combine(_directory, SanitizeFileName(saveId) + Extension);

        public Task<StoreResult> SaveAsync(SaveRecord record)
        {
            try
            {
                if (record == null)
                    return Task.FromResult(StoreResult.Failed("A save needs a record."));
                if (string.IsNullOrWhiteSpace(record.SaveId))
                    return Task.FromResult(StoreResult.Failed("A save needs a SaveId."));

                Directory.CreateDirectory(_directory);
                File.WriteAllText(PathFor(record.SaveId), ToJson(record));
                return Task.FromResult(StoreResult.Success());
            }
            catch (Exception ex)
            {
                return Task.FromResult(StoreResult.Failed(ex.Message));
            }
        }

        public Task<StoreResult<SaveRecord>> LoadAsync(string saveId)
        {
            try
            {
                var path = PathFor(saveId);
                if (!File.Exists(path))
                    return Task.FromResult(StoreResult<SaveRecord>.NotFound($"No save '{saveId}'."));

                var record = FromJson(File.ReadAllText(path));
                if (record == null)
                    return Task.FromResult(StoreResult<SaveRecord>.Failed($"Save '{saveId}' did not parse."));

                if (record.FormatVersion != SaveRecord.CurrentFormatVersion)
                {
                    return Task.FromResult(StoreResult<SaveRecord>.VersionMismatch(
                        $"Save '{saveId}' is format version {record.FormatVersion}; this build " +
                        $"reads version {SaveRecord.CurrentFormatVersion}."));
                }

                return Task.FromResult(StoreResult<SaveRecord>.Success(record));
            }
            catch (Exception ex)
            {
                return Task.FromResult(StoreResult<SaveRecord>.Failed(ex.Message));
            }
        }

        public Task<StoreResult<IReadOnlyList<SaveSummary>>> ListSavesAsync()
        {
            try
            {
                var summaries = new List<SaveSummary>();
                if (!Directory.Exists(_directory))
                    return Task.FromResult(
                        StoreResult<IReadOnlyList<SaveSummary>>.Success(summaries));

                foreach (var file in Directory.GetFiles(_directory, "*" + Extension))
                {
                    SaveRecord record;
                    try { record = FromJson(File.ReadAllText(file)); }
                    catch { continue; }
                    if (record == null) continue;

                    summaries.Add(new SaveSummary
                    {
                        SaveId = record.SaveId,
                        ScenarioName = record.ScenarioName,
                        Tick = record.Tick,
                        SavedAtUtc = record.SavedAtUtc,
                        CampaignName = record.CampaignName ?? string.Empty,
                        OperationIndex = record.OperationIndex,
                    });
                }

                summaries.Sort((a, b) => string.CompareOrdinal(b.SavedAtUtc, a.SavedAtUtc));
                return Task.FromResult(
                    StoreResult<IReadOnlyList<SaveSummary>>.Success(summaries));
            }
            catch (Exception ex)
            {
                return Task.FromResult(
                    StoreResult<IReadOnlyList<SaveSummary>>.Failed(ex.Message));
            }
        }

        public Task<StoreResult<bool>> DeleteAsync(string saveId)
        {
            try
            {
                var path = PathFor(saveId);
                if (!File.Exists(path))
                    return Task.FromResult(StoreResult<bool>.Success(false));
                File.Delete(path);
                return Task.FromResult(StoreResult<bool>.Success(true));
            }
            catch (Exception ex)
            {
                return Task.FromResult(StoreResult<bool>.Failed(ex.Message));
            }
        }

        // ─── JSON ─────────────────────────────────────────────────────────────

        private static JsonSerializerSettings Settings => new()
        {
            Formatting = Formatting.Indented,
            ContractResolver = new FieldsOnlyResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new StringEnumConverter(), new Vector2Converter(), new ColorConverter() },
        };

        public static string ToJson(SaveRecord record) => JsonConvert.SerializeObject(record, Settings);

        public static SaveRecord FromJson(string json) =>
            string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<SaveRecord>(json, Settings);

        /// <summary>Strips characters a filesystem would reject. A save id is a slug, not free text.</summary>
        private static string SanitizeFileName(string saveId)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = saveId.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                foreach (var bad in invalid)
                    if (chars[i] == bad) { chars[i] = '_'; break; }
            return new string(chars);
        }
    }
}
