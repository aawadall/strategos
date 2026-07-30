// ReliefProfile.cs
// Named terrain characters and the parameter sets behind them.
//
// A profile is data, never a code branch: every stage reads the same fields and
// only their values differ. Adding a new landscape character should mean adding a
// row here, not an `if` in the generator.

using UnityEngine;

namespace Strategos.Maps
{
    /// <summary>Broad terrain character. Selects a <see cref="ReliefParameters"/> preset.</summary>
    public enum ReliefProfile
    {
        Plains    = 0,
        Rolling   = 1,
        Hills     = 2,
        Mountains = 3,
        Coastal   = 4,
        Desert    = 5,
        Arctic    = 6,
    }

    /// <summary>
    /// The numbers that give a profile its character. Fractions are of the map's
    /// own elevation range unless stated otherwise.
    /// </summary>
    public struct ReliefParameters
    {
        /// <summary>Elevation in metres of the lowest point on the map.</summary>
        public float BaseElevationMetres;

        /// <summary>Vertical span in metres from the lowest point to the highest.</summary>
        public float ReliefMetres;

        /// <summary>Blend from pure fBm (0) to pure ridged multifractal (1).</summary>
        public float RidgedWeight;

        /// <summary>Domain-warp displacement, in noise units.</summary>
        public float WarpStrength;

        /// <summary>Octaves of noise in the base landform.</summary>
        public int Octaves;

        /// <summary>
        /// Cells per unit of noise space. Larger means broader landforms — this is
        /// the knob that decides whether a 512-cell map holds one valley or twenty.
        /// </summary>
        public float FeatureScaleCells;

        /// <summary>Hydraulic erosion droplets per 1000 cells.</summary>
        public float ErosionDropletsPerKiloCell;

        /// <summary>Thermal erosion repose angle in degrees; slopes above it slump.</summary>
        public float TalusAngleDegrees;

        /// <summary>
        /// Sea level as a fraction of the relief span. Zero or less means the map
        /// is entirely inland and no coastline is traced.
        /// </summary>
        public float SeaLevelFraction;

        /// <summary>0 = humid, 1 = arid. Shifts landcover away from forest.</summary>
        public float Aridity;

        /// <summary>Elevation fraction above which forest gives way to rock.</summary>
        public float TreelineFraction;

        /// <summary>Elevation fraction above which permanent snow sits. &gt;1 disables it.</summary>
        public float SnowlineFraction;

        /// <summary>Contour spacing in metres appropriate to the relief.</summary>
        public float ContourIntervalMetres;

        /// <summary>Settlements per 1000 cells, before terrain suitability culling.</summary>
        public float SettlementsPerKiloCell;

        /// <summary>
        /// Flow accumulation, as a fraction of the map's cell count, above which a
        /// drainage cell is promoted from stream to river. Lower means wetter maps.
        /// </summary>
        public float RiverThresholdFraction;
    }

    public static class ReliefProfiles
    {
        public static ReliefParameters For(ReliefProfile profile)
        {
            switch (profile)
            {
                case ReliefProfile.Plains:
                    return new ReliefParameters
                    {
                        BaseElevationMetres        = 60f,
                        ReliefMetres               = 90f,
                        RidgedWeight               = 0.05f,
                        WarpStrength               = 0.35f,
                        Octaves                    = 5,
                        FeatureScaleCells          = 260f,
                        ErosionDropletsPerKiloCell = 30f,
                        TalusAngleDegrees          = 32f,
                        SeaLevelFraction           = 0f,
                        Aridity                    = 0.25f,
                        TreelineFraction           = 1.5f,
                        SnowlineFraction           = 2f,
                        ContourIntervalMetres      = 5f,
                        SettlementsPerKiloCell     = 0.10f,
                        RiverThresholdFraction     = 0.0016f,
                    };

                case ReliefProfile.Rolling:
                    return new ReliefParameters
                    {
                        BaseElevationMetres        = 120f,
                        ReliefMetres               = 240f,
                        RidgedWeight               = 0.18f,
                        WarpStrength               = 0.5f,
                        Octaves                    = 6,
                        FeatureScaleCells          = 200f,
                        ErosionDropletsPerKiloCell = 55f,
                        TalusAngleDegrees          = 34f,
                        SeaLevelFraction           = 0f,
                        Aridity                    = 0.2f,
                        TreelineFraction           = 1.5f,
                        SnowlineFraction           = 2f,
                        ContourIntervalMetres      = 10f,
                        SettlementsPerKiloCell     = 0.09f,
                        RiverThresholdFraction     = 0.0014f,
                    };

                case ReliefProfile.Hills:
                    return new ReliefParameters
                    {
                        BaseElevationMetres        = 180f,
                        ReliefMetres               = 520f,
                        RidgedWeight               = 0.4f,
                        WarpStrength               = 0.6f,
                        Octaves                    = 6,
                        FeatureScaleCells          = 165f,
                        ErosionDropletsPerKiloCell = 80f,
                        TalusAngleDegrees          = 36f,
                        SeaLevelFraction           = 0f,
                        Aridity                    = 0.18f,
                        TreelineFraction           = 0.92f,
                        SnowlineFraction           = 2f,
                        ContourIntervalMetres      = 20f,
                        SettlementsPerKiloCell     = 0.07f,
                        RiverThresholdFraction     = 0.0012f,
                    };

                case ReliefProfile.Mountains:
                    return new ReliefParameters
                    {
                        BaseElevationMetres        = 400f,
                        ReliefMetres               = 2000f,
                        RidgedWeight               = 0.85f,
                        WarpStrength               = 0.7f,
                        Octaves                    = 7,
                        FeatureScaleCells          = 150f,
                        ErosionDropletsPerKiloCell = 120f,
                        TalusAngleDegrees          = 40f,
                        SeaLevelFraction           = 0f,
                        Aridity                    = 0.15f,
                        TreelineFraction           = 0.62f,
                        SnowlineFraction           = 0.84f,
                        ContourIntervalMetres      = 50f,
                        SettlementsPerKiloCell     = 0.04f,
                        RiverThresholdFraction     = 0.0010f,
                    };

                case ReliefProfile.Coastal:
                    return new ReliefParameters
                    {
                        BaseElevationMetres        = -40f,
                        ReliefMetres               = 420f,
                        RidgedWeight               = 0.3f,
                        WarpStrength               = 0.65f,
                        Octaves                    = 6,
                        FeatureScaleCells          = 210f,
                        ErosionDropletsPerKiloCell = 70f,
                        TalusAngleDegrees          = 35f,
                        SeaLevelFraction           = 0.12f,
                        Aridity                    = 0.15f,
                        TreelineFraction           = 1.1f,
                        SnowlineFraction           = 2f,
                        ContourIntervalMetres      = 20f,
                        SettlementsPerKiloCell     = 0.11f,
                        RiverThresholdFraction     = 0.0013f,
                    };

                case ReliefProfile.Desert:
                    return new ReliefParameters
                    {
                        BaseElevationMetres        = 240f,
                        ReliefMetres               = 380f,
                        RidgedWeight               = 0.5f,
                        WarpStrength               = 0.9f,
                        Octaves                    = 6,
                        FeatureScaleCells          = 190f,
                        // Arid landscapes are wind- not water-carved; heavy hydraulic
                        // erosion would cut a drainage network that should not exist.
                        ErosionDropletsPerKiloCell = 18f,
                        TalusAngleDegrees          = 33f,
                        SeaLevelFraction           = 0f,
                        Aridity                    = 0.95f,
                        TreelineFraction           = 1.5f,
                        SnowlineFraction           = 2f,
                        ContourIntervalMetres      = 20f,
                        SettlementsPerKiloCell     = 0.03f,
                        RiverThresholdFraction     = 0.0035f,
                    };

                case ReliefProfile.Arctic:
                    return new ReliefParameters
                    {
                        BaseElevationMetres        = 80f,
                        ReliefMetres               = 700f,
                        RidgedWeight               = 0.6f,
                        WarpStrength               = 0.55f,
                        Octaves                    = 6,
                        FeatureScaleCells          = 175f,
                        ErosionDropletsPerKiloCell = 45f,
                        TalusAngleDegrees          = 38f,
                        SeaLevelFraction           = 0.06f,
                        Aridity                    = 0.55f,
                        TreelineFraction           = 0.3f,
                        SnowlineFraction           = 0.5f,
                        ContourIntervalMetres      = 25f,
                        SettlementsPerKiloCell     = 0.02f,
                        RiverThresholdFraction     = 0.0018f,
                    };

                default:
                    return For(ReliefProfile.Rolling);
            }
        }
    }

    /// <summary>
    /// Everything needed to generate a map. Two settings objects that compare equal
    /// must produce byte-identical output — that property is what the determinism
    /// test asserts.
    /// </summary>
    public sealed class MapGenerationSettings
    {
        public string Name = "Generated";
        public int    Seed = 1;

        public int   Width         = 512;
        public int   Height        = 512;
        public float MetresPerCell = 25f;

        public ReliefProfile Profile = ReliefProfile.Rolling;

        /// <summary>Anchor for the military grid. Purely cosmetic to generation.</summary>
        public GeoOrigin Origin = GeoOrigin.Default;

        /// <summary>Set to skip erosion — much faster, used by small preview bakes.</summary>
        public bool EnableErosion = true;

        /// <summary>Set to skip settlements and the road network.</summary>
        public bool EnableCulture = true;

        /// <summary>
        /// Overrides the profile's parameters wholesale when non-null. Fixture maps
        /// use this to tune a named location without inventing a new profile.
        /// </summary>
        public ReliefParameters? ParameterOverride;

        public ReliefParameters ResolveParameters() =>
            ParameterOverride ?? ReliefProfiles.For(Profile);

        public MapGenerationSettings Clone() => (MapGenerationSettings)MemberwiseClone();
    }
}
