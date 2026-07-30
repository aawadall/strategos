// ErosionStage.cs
// Weathers the raw landform: droplet-based hydraulic erosion, then a thermal
// (talus) pass.
//
// This is the stage that makes a map read as a real place. Raw noise produces
// concentric blobs whose contour lines look nothing like a survey sheet; hydraulic
// erosion carves the dendritic valley networks that give contours their
// characteristic V-shapes pointing upstream, and the thermal pass flattens scree
// slopes to a plausible angle of repose.

using UnityEngine;

namespace Strategos.Maps
{
    public sealed class ErosionStage : IMapGenerationStage
    {
        public string Name => "Erosion";

        private const int StreamId = 202;

        // Droplet model constants. These are tuned together — changing one in
        // isolation usually just produces mush or leaves the terrain untouched.
        private const int   MaxLifetime    = 48;
        private const float Inertia        = 0.05f;  // 0 = follows gradient exactly
        private const float CapacityFactor = 4f;
        private const float MinCapacity    = 0.01f;
        private const float ErodeSpeed     = 0.3f;
        private const float DepositSpeed   = 0.3f;
        private const float EvaporateSpeed = 0.015f;
        private const float Gravity        = 4f;
        private const float InitialWater   = 1f;
        private const float InitialSpeed   = 1f;
        private const int   ErosionRadius  = 3;

        public void Apply(MapGenContext ctx)
        {
            int w = ctx.Width, h = ctx.Height;
            float[] elevation = ctx.Map.Elevation;

            int droplets = Mathf.RoundToInt(
                ctx.Params.ErosionDropletsPerKiloCell * (w * h) / 1000f);
            if (droplets <= 0) return;

            var rng   = ctx.StageRandom(StreamId);
            var brush = BuildBrush(ErosionRadius);

            for (int i = 0; i < droplets; i++)
                SimulateDroplet(elevation, w, h, rng, brush);

            ThermalPass(ctx, elevation, w, h);

            ctx.Map.RecalculateElevationBounds();
        }

        // ─── Hydraulic ────────────────────────────────────────────────────────

        private static void SimulateDroplet(float[] elev, int w, int h,
            DeterministicRandom rng, ErosionBrush brush)
        {
            float posX = rng.Range(1f, w - 2f);
            float posY = rng.Range(1f, h - 2f);
            float dirX = 0f, dirY = 0f;
            float speed = InitialSpeed, water = InitialWater, sediment = 0f;

            for (int life = 0; life < MaxLifetime; life++)
            {
                int cellX = (int)posX;
                int cellY = (int)posY;
                float offX = posX - cellX;
                float offY = posY - cellY;

                if (!SampleHeightAndGradient(elev, w, h, cellX, cellY, offX, offY,
                        out float height, out float gradX, out float gradY))
                    return;

                // Blend the previous heading with the downhill direction so a droplet
                // carries momentum across a flat instead of stalling on it.
                dirX = dirX * Inertia - gradX * (1f - Inertia);
                dirY = dirY * Inertia - gradY * (1f - Inertia);

                float len = Mathf.Sqrt(dirX * dirX + dirY * dirY);
                if (len < 1e-6f) return; // came to rest in a pit
                dirX /= len;
                dirY /= len;

                posX += dirX;
                posY += dirY;

                if (posX < 1f || posX >= w - 2f || posY < 1f || posY >= h - 2f) return;

                int newCellX = (int)posX;
                int newCellY = (int)posY;
                if (!SampleHeightAndGradient(elev, w, h, newCellX, newCellY,
                        posX - newCellX, posY - newCellY,
                        out float newHeight, out _, out _))
                    return;

                float deltaHeight = newHeight - height;

                float capacity = Mathf.Max(
                    -deltaHeight * speed * water * CapacityFactor, MinCapacity);

                if (sediment > capacity || deltaHeight > 0f)
                {
                    // Uphill: drop at most enough to fill the step, so a droplet
                    // never builds a spire higher than the obstacle it hit.
                    float amount = deltaHeight > 0f
                        ? Mathf.Min(deltaHeight, sediment)
                        : (sediment - capacity) * DepositSpeed;

                    sediment -= amount;
                    DepositBilinear(elev, w, h, cellX, cellY, offX, offY, amount);
                }
                else
                {
                    // Spread erosion over a brush so the droplet cuts a valley
                    // rather than a one-cell-wide slot.
                    float amount = Mathf.Min((capacity - sediment) * ErodeSpeed, -deltaHeight);
                    sediment += ErodeBrush(elev, w, h, cellX, cellY, brush, amount);
                }

                speed = Mathf.Sqrt(Mathf.Max(0f, speed * speed + deltaHeight * -Gravity));
                water *= 1f - EvaporateSpeed;
                if (water < 0.01f) return;
            }
        }

        private static bool SampleHeightAndGradient(float[] elev, int w, int h,
            int cellX, int cellY, float offX, float offY,
            out float height, out float gradX, out float gradY)
        {
            height = gradX = gradY = 0f;
            if (cellX < 0 || cellY < 0 || cellX >= w - 1 || cellY >= h - 1) return false;

            int i = cellY * w + cellX;
            float nw = elev[i + w], ne = elev[i + w + 1];
            float sw = elev[i],     se = elev[i + 1];

            gradX = (se - sw) * (1f - offY) + (ne - nw) * offY;
            gradY = (nw - sw) * (1f - offX) + (ne - se) * offX;

            float bottom = sw + (se - sw) * offX;
            float top    = nw + (ne - nw) * offX;
            height = bottom + (top - bottom) * offY;
            return true;
        }

        private static void DepositBilinear(float[] elev, int w, int h,
            int cellX, int cellY, float offX, float offY, float amount)
        {
            if (cellX < 0 || cellY < 0 || cellX >= w - 1 || cellY >= h - 1) return;

            int i = cellY * w + cellX;
            elev[i]         += amount * (1f - offX) * (1f - offY);
            elev[i + 1]     += amount * offX        * (1f - offY);
            elev[i + w]     += amount * (1f - offX) * offY;
            elev[i + w + 1] += amount * offX        * offY;
        }

        /// <summary>Removes <paramref name="amount"/> spread over the brush; returns what was taken.</summary>
        private static float ErodeBrush(float[] elev, int w, int h,
            int cellX, int cellY, ErosionBrush brush, float amount)
        {
            float removed = 0f;

            for (int k = 0; k < brush.Offsets.Length; k++)
            {
                int x = cellX + brush.Offsets[k].x;
                int y = cellY + brush.Offsets[k].y;
                if ((uint)x >= (uint)w || (uint)y >= (uint)h) continue;

                int   i     = y * w + x;
                float share = amount * brush.Weights[k];
                elev[i] -= share;
                removed += share;
            }

            return removed;
        }

        private readonly struct ErosionBrush
        {
            public readonly Vector2Int[] Offsets;
            public readonly float[]      Weights;

            public ErosionBrush(Vector2Int[] offsets, float[] weights)
            {
                Offsets = offsets;
                Weights = weights;
            }
        }

        /// <summary>Radially falling-off, normalised-to-one weight kernel.</summary>
        private static ErosionBrush BuildBrush(int radius)
        {
            var offsets = new System.Collections.Generic.List<Vector2Int>();
            var weights = new System.Collections.Generic.List<float>();
            float total = 0f;

            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                float d2 = x * x + y * y;
                if (d2 > radius * radius) continue;

                float weight = 1f - Mathf.Sqrt(d2) / radius;
                offsets.Add(new Vector2Int(x, y));
                weights.Add(weight);
                total += weight;
            }

            var normalised = new float[weights.Count];
            for (int i = 0; i < weights.Count; i++) normalised[i] = weights[i] / total;

            return new ErosionBrush(offsets.ToArray(), normalised);
        }

        // ─── Thermal ──────────────────────────────────────────────────────────

        /// <summary>
        /// Slumps material off any slope steeper than the angle of repose. Runs a
        /// fixed number of passes over a snapshot of the surface so the result does
        /// not depend on cell visitation order — a sequential in-place version would
        /// bias material toward one corner of the map.
        /// </summary>
        private static void ThermalPass(MapGenContext ctx, float[] elev, int w, int h)
        {
            float cellSize   = ctx.Map.Header.MetresPerCell;
            float maxDrop    = Mathf.Tan(ctx.Params.TalusAngleDegrees * Mathf.Deg2Rad) * cellSize;
            const int passes = 4;
            const float rate = 0.5f;

            var delta = new float[elev.Length];

            for (int pass = 0; pass < passes; pass++)
            {
                System.Array.Clear(delta, 0, delta.Length);

                for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    int   i = y * w + x;
                    float centre = elev[i];

                    float totalExcess = 0f;
                    for (int n = 0; n < 8; n++)
                    {
                        int nx = x + MapGenContext.NeighbourDx[n];
                        int ny = y + MapGenContext.NeighbourDy[n];
                        float drop = centre - elev[ny * w + nx];
                        float limit = maxDrop * MapGenContext.NeighbourDistance[n];
                        if (drop > limit) totalExcess += drop - limit;
                    }

                    if (totalExcess <= 0f) continue;

                    // Move a fraction of the excess, distributed by how far each
                    // neighbour is below the repose limit.
                    float move = totalExcess * rate * 0.5f;
                    for (int n = 0; n < 8; n++)
                    {
                        int nx = x + MapGenContext.NeighbourDx[n];
                        int ny = y + MapGenContext.NeighbourDy[n];
                        float drop = centre - elev[ny * w + nx];
                        float limit = maxDrop * MapGenContext.NeighbourDistance[n];
                        if (drop <= limit) continue;

                        float share = (drop - limit) / totalExcess * move;
                        delta[i]          -= share;
                        delta[ny * w + nx] += share;
                    }
                }

                for (int i = 0; i < elev.Length; i++) elev[i] += delta[i];
            }
        }
    }
}
