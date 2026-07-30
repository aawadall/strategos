// MapGenerator.cs
// Runs the map generation pipeline in a fixed order.
//
// The order is hard-coded here for the same reason NatoSymbolComposer hard-codes
// the decorator order: later stages read what earlier ones wrote, and letting a
// caller shuffle them would only produce silently wrong maps. Landcover needs the
// eroded surface and the drainage network; settlements need landcover; roads need
// settlements.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Strategos.Maps
{
    /// <summary>One step of the generation pipeline.</summary>
    public interface IMapGenerationStage
    {
        /// <summary>Short name, used in generation logs and the editor preview.</summary>
        string Name { get; }

        void Apply(MapGenContext ctx);
    }

    /// <summary>
    /// Shared state threaded through the pipeline: the map under construction, the
    /// resolved parameters, the random source, and the derived fields that several
    /// stages need but that do not belong in <see cref="MapData"/> itself.
    /// </summary>
    public sealed class MapGenContext
    {
        public MapGenerationSettings Settings { get; }
        public ReliefParameters      Params   { get; }
        public MapData               Map      { get; }

        /// <summary>Base generator. Stages should call <see cref="StageRandom"/> instead.</summary>
        public DeterministicRandom Random { get; }

        /// <summary>
        /// Upslope contributing cell count per cell, filled by HydrologyStage.
        /// Drives river width and, indirectly, moisture.
        /// </summary>
        public float[] FlowAccumulation;

        /// <summary>0–1 wetness per cell, filled by HydrologyStage, read by LandcoverStage.</summary>
        public float[] Moisture;

        /// <summary>D8 downhill neighbour index per cell (0–7), 255 for a sink.</summary>
        public byte[] FlowDirection;

        public int Width  => Map.Header.Width;
        public int Height => Map.Header.Height;
        public int CellCount => Map.Elevation.Length;

        public MapGenContext(MapGenerationSettings settings, MapData map)
        {
            Settings = settings;
            Params   = settings.ResolveParameters();
            Map      = map;
            Random   = new DeterministicRandom(settings.Seed);
        }

        /// <summary>
        /// A generator private to one stage, derived from the map seed alone.
        /// Because the stream id — not the call order — decides the sequence,
        /// changing how many numbers an earlier stage draws does not reshuffle
        /// every later stage's output.
        /// </summary>
        public DeterministicRandom StageRandom(int streamId) =>
            new DeterministicRandom((ulong)(uint)Settings.Seed, (ulong)(uint)streamId);

        // ─── Convenience ──────────────────────────────────────────────────────

        public int Index(int x, int y) => y * Width + x;

        public bool InBounds(int x, int y) =>
            (uint)x < (uint)Width && (uint)y < (uint)Height;

        /// <summary>D8 neighbour offsets, indexed by flow direction code.</summary>
        public static readonly int[] NeighbourDx = { 1, 1, 0, -1, -1, -1,  0,  1 };
        public static readonly int[] NeighbourDy = { 0, 1, 1,  1,  0, -1, -1, -1 };

        /// <summary>Ground distance multiplier per neighbour: 1 orthogonal, √2 diagonal.</summary>
        public static readonly float[] NeighbourDistance =
        {
            1f, 1.4142136f, 1f, 1.4142136f, 1f, 1.4142136f, 1f, 1.4142136f,
        };
    }

    public static class MapGenerator
    {
        /// <summary>
        /// Generates a complete map.
        ///
        /// <paramref name="authoredRelief"/> runs between the base landform and
        /// erosion, so hand-placed ridges and basins get carved by the same
        /// hydrology as generated terrain. <paramref name="authoredFeatures"/> runs
        /// last, so a fixture can force a road or town the generator would not have
        /// chosen. Both are null for a purely procedural map.
        /// </summary>
        public static MapData Generate(
            MapGenerationSettings settings,
            IMapGenerationStage authoredRelief = null,
            IMapGenerationStage authoredFeatures = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var header = new MapHeader
            {
                Name            = settings.Name,
                Seed            = settings.Seed,
                Width           = Mathf.Max(8, settings.Width),
                Height          = Mathf.Max(8, settings.Height),
                MetresPerCell   = Mathf.Max(0.1f, settings.MetresPerCell),
                Origin          = settings.Origin,
                ContourInterval = settings.ResolveParameters().ContourIntervalMetres,
            };

            var map = new MapData(header);
            var ctx = new MapGenContext(settings, map);

            foreach (var stage in BuildPipeline(settings, authoredRelief, authoredFeatures))
            {
                stage.Apply(ctx);
                // Elevation-writing stages leave the header range stale otherwise,
                // and every downstream normalisation reads it.
                map.RecalculateElevationBounds();
            }

            return map;
        }

        /// <summary>The pipeline, in order. Exposed so the editor preview can list it.</summary>
        public static IEnumerable<IMapGenerationStage> BuildPipeline(
            MapGenerationSettings settings,
            IMapGenerationStage authoredRelief = null,
            IMapGenerationStage authoredFeatures = null)
        {
            yield return new TectonicStage();

            if (authoredRelief != null)
                yield return authoredRelief;

            if (settings.EnableErosion)
                yield return new ErosionStage();

            yield return new HydrologyStage();
            yield return new LandcoverStage();

            if (settings.EnableCulture)
            {
                yield return new SettlementStage();
                yield return new NetworkStage();
            }

            if (authoredFeatures != null)
                yield return authoredFeatures;
        }
    }
}
