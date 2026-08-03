// IGameStore.cs
// The seam #66 said to decide before the first consumer: player data — saves today, campaign
// state and profile later — goes behind an interface with a file-backed implementation first,
// because WebGL cannot take a native SQLite plugin and desktop and WebGL must not diverge later
// over a decision made after the fact.
//
// NOTHING IN CORE KNOWS WHERE THE BYTES GO. This file defines the contract and the records it
// carries — plain data, same as everything else Core exposes. It does not read or write
// anything. The first implementation (Assets/Scripts/Persistence/FileGameStore.cs) lives
// outside Core on purpose: Core knows how to turn a Simulation into a SimulationSnapshot and
// back (Simulation.Snapshot() / Simulation.Restore()), and knows nothing about files, SQLite,
// or IndexedDB. When the embedded-database implementation lands (a follow-up — #74 scopes this
// change to the seam and the file-backed one only) it is a second class implementing this same
// interface, not a change to Core.
//
// STRUCTURED COLUMNS BESIDE A JSON PAYLOAD, per #66/#74. SaveRecord's own fields — SaveId,
// ScenarioName, Tick, SavedAtUtc — are what a query filters on; Snapshot is the document. A
// SQLite implementation makes the first group real columns and the second a JSON1 column
// without changing this shape at all, which is the point of settling it now.

using System.Collections.Generic;

namespace Strategos.Persistence
{
    /// <summary>
    /// A saved run: what a store answers "which saves exist" with, before paying to load any
    /// one of them in full.
    /// </summary>
    public struct SaveSummary
    {
        public string SaveId;
        public string ScenarioName;
        public int Tick;

        /// <summary>ISO-8601, UTC. A string rather than <c>DateTime</c> — see SaveRecord's own note.</summary>
        public string SavedAtUtc;

        /// <summary>
        /// Campaign display name when this save is mid-chain (#140), or empty for a
        /// single-scenario run.
        /// </summary>
        public string CampaignName;

        /// <summary>Active operation index, or -1 when not a campaign save.</summary>
        public int OperationIndex;
    }

    /// <summary>A save, in full — the structured columns plus the document they describe.</summary>
    public sealed class SaveRecord
    {
        public string SaveId;

        /// <summary>
        /// The save format's own version — distinct from <see cref="SimulationSnapshot"/>'s
        /// shape and from <see cref="Scenarios.Scenario.FormatVersion"/>, because a save
        /// record's wrapper, the snapshot, and a scenario change on three different schedules.
        /// Bumped by hand; an <see cref="IGameStore"/> implementation must refuse to load a
        /// record whose version it does not recognise rather than deserialise it and hope.
        /// </summary>
        public int FormatVersion = CurrentFormatVersion;

        /// <summary>
        /// Remains 1: campaign fields (#140) are additive optional columns. Missing them means
        /// single-scenario — same as an empty <see cref="CampaignChainJson"/>.
        /// </summary>
        public const int CurrentFormatVersion = 1;

        public string ScenarioName;
        public int Tick;

        /// <summary>
        /// ISO-8601, UTC, as text. Not <c>System.DateTime</c>: Newtonsoft's default DateTime
        /// handling round-trips through the local machine's time zone unless configured
        /// otherwise, which is exactly the kind of platform-dependent behaviour this project's
        /// determinism rules exist to keep out of anything that gets compared or replayed. A
        /// save's timestamp is metadata for a player, not simulation state, so it costs nothing
        /// to keep as a plain, unambiguous string.
        /// </summary>
        public string SavedAtUtc;

        public SimulationSnapshot Snapshot;

        /// <summary>
        /// Campaign display name when saving mid-chain (#140), or empty / null for
        /// single-scenario PLAY.
        /// </summary>
        public string CampaignName = string.Empty;

        /// <summary>
        /// Index into the saved chain's operations, or -1 when <see cref="CampaignChainJson"/>
        /// is absent.
        /// </summary>
        public int OperationIndex = -1;

        /// <summary>
        /// Live <see cref="Campaigns.CampaignChain"/> JSON via
        /// <see cref="Campaigns.CampaignChainIO.ToJson"/> — Outcomes and CarriedOverUnits
        /// included. Null / empty means this is not a campaign save; do not reload authored
        /// Resources JSON in its place or carry-over state is lost.
        /// </summary>
        public string CampaignChainJson;
    }

    /// <summary>
    /// Where saves live. One consumer, several possible backends — see the file header.
    /// </summary>
    public interface IGameStore
    {
        /// <summary>Writes a save, replacing any existing one with the same <see cref="SaveRecord.SaveId"/>.</summary>
        void Save(SaveRecord record);

        /// <summary>
        /// Reads a save back, or null if <paramref name="saveId"/> does not exist.
        /// </summary>
        /// <exception cref="SaveVersionMismatchException">
        /// The record's <see cref="SaveRecord.FormatVersion"/> does not match
        /// <see cref="SaveRecord.CurrentFormatVersion"/>. Refusing here is the whole point —
        /// #74 requires a save to state its version and refuse rather than misload across an
        /// incompatible one, so this is thrown before any of the payload is trusted, not
        /// discovered later as a field that silently deserialised to its default.
        /// </exception>
        SaveRecord Load(string saveId);

        /// <summary>Every save this store holds, newest first. Cheap: summaries only.</summary>
        IReadOnlyList<SaveSummary> ListSaves();

        /// <summary>True if a save existed and was removed.</summary>
        bool Delete(string saveId);
    }

    /// <summary>
    /// A save's format version does not match what this build can read. See
    /// <see cref="IGameStore.Load"/>.
    /// </summary>
    public sealed class SaveVersionMismatchException : System.Exception
    {
        public int FoundVersion { get; }
        public int ExpectedVersion { get; }

        public SaveVersionMismatchException(string saveId, int foundVersion, int expectedVersion)
            : base($"Save '{saveId}' is format version {foundVersion}; this build reads " +
                   $"version {expectedVersion}. Refusing to load rather than misreading it.")
        {
            FoundVersion = foundVersion;
            ExpectedVersion = expectedVersion;
        }
    }
}
