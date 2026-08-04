// UnitCatalogue.cs
// The set of unit types available to a scenario, keyed by capability id.
//
// A unit instance stores a capability *id*, not the capability itself, so a hundred rifle
// companies reference one object rather than carrying a hundred copies of the same numbers —
// and so a scenario file stays readable, saying "inf-mech" rather than restating every stat.
//
// The built-in catalogue below is a starting set, not a claim to authority. The figures are
// plausible rather than researched: enough for movement and engagement to behave differently
// per type, and expected to be tuned once there is something to play. They are not APP-6D
// data and nothing in the standard prescribes them.

using System.Collections.Generic;
using Strategos.Maps;

namespace Strategos.Units
{
    public sealed class UnitCatalogue
    {
        public List<UnitCapabilities> Types = new();

        private Dictionary<string, UnitCapabilities> _byId;

        /// <summary>
        /// Capabilities for <paramref name="id"/>, or <see cref="Fallback"/> if unknown.
        ///
        /// Never null: a scenario naming a type that no longer exists should render and move
        /// as something generic rather than throwing halfway through a load.
        /// </summary>
        public UnitCapabilities Get(string id)
        {
            if (_byId == null || _byId.Count != Types.Count) Reindex();
            if (!string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out var caps)) return caps;
            return Fallback;
        }

        public bool Contains(string id)
        {
            if (_byId == null || _byId.Count != Types.Count) Reindex();
            return !string.IsNullOrEmpty(id) && _byId.ContainsKey(id);
        }

        private void Reindex()
        {
            _byId = new Dictionary<string, UnitCapabilities>();
            foreach (var t in Types)
                if (!string.IsNullOrEmpty(t.Id)) _byId[t.Id] = t;
        }

        /// <summary>Used when a capability id is missing or unknown.</summary>
        public static UnitCapabilities Fallback { get; } = new()
        {
            Id = "generic",
            Name = "Generic Unit",
        };

        // ─── Built-in types ───────────────────────────────────────────────────

        public const string InfantryFoot = "inf-foot";
        public const string InfantryMech = "inf-mech";
        public const string InfantryMotor = "inf-motor";
        public const string Armor = "armor";
        public const string ReconMotor = "recon-motor";
        public const string Artillery = "artillery";

        public static UnitCatalogue Default()
        {
            var c = new UnitCatalogue();

            // Foot infantry: slow, but the only thing here that goes almost anywhere.
            c.Types.Add(new UnitCapabilities
            {
                Id = InfantryFoot,
                Name = "Infantry (foot)",
                CrossCountrySpeedMps = 1.2f,     // ~4.3 km/h
                RoadSpeedMps = 1.6f,
                MaxClimbDegrees = 35f,
                ImpassableLandcover = new[] { LandcoverClass.Water },
                LandcoverSpeedFactor = Factors(open: 1f, crop: 0.9f, forest: 0.6f, urban: 0.7f,
                    water: 0f, marsh: 0.4f, rock: 0.5f, sand: 0.8f, snow: 0.5f),
                DetectionRangeMetres = 1500f,
                EngagementRangeMetres = 500f,
                Firepower = 10f,
                Protection = 1f,
                Fatigue = new FatigueRates
                {
                    PerHourMoving = 20f, PerHourEngaged = 26f,
                    RecoveryPerHourResting = 11f,
                },
            });

            // Mechanised: fast, well protected, stopped by marsh and steep ground.
            c.Types.Add(new UnitCapabilities
            {
                Id = InfantryMech,
                Name = "Infantry (mechanised)",
                CrossCountrySpeedMps = 7f,       // ~25 km/h
                RoadSpeedMps = 15f,              // ~54 km/h
                MaxClimbDegrees = 25f,
                ImpassableLandcover = new[] { LandcoverClass.Water, LandcoverClass.Marsh },
                LandcoverSpeedFactor = Factors(open: 1f, crop: 0.9f, forest: 0.35f, urban: 0.5f,
                    water: 0f, marsh: 0f, rock: 0.3f, sand: 0.7f, snow: 0.5f),
                DetectionRangeMetres = 2000f,
                EngagementRangeMetres = 1200f,
                Firepower = 18f,
                Protection = 3f,
                Consumption = new ConsumptionRates
                {
                    RationsPerHour = 0.6f, WaterPerHour = 1.2f,
                    AmmunitionPerHourEngaged = 30f, FuelPerHourMoving = 12f,
                },
                Fatigue = new FatigueRates
                {
                    PerHourMoving = 7f, PerHourEngaged = 24f,
                    RecoveryPerHourResting = 12f,
                },
            });

            // Motorised: road-bound. Fast on a road, poor off it.
            c.Types.Add(new UnitCapabilities
            {
                Id = InfantryMotor,
                Name = "Infantry (motorised)",
                CrossCountrySpeedMps = 4f,
                RoadSpeedMps = 18f,
                MaxClimbDegrees = 18f,
                ImpassableLandcover = new[] { LandcoverClass.Water, LandcoverClass.Marsh },
                LandcoverSpeedFactor = Factors(open: 0.8f, crop: 0.7f, forest: 0.2f, urban: 0.6f,
                    water: 0f, marsh: 0f, rock: 0.2f, sand: 0.5f, snow: 0.35f),
                DetectionRangeMetres = 1800f,
                EngagementRangeMetres = 800f,
                Firepower = 12f,
                Protection = 1.6f,
                Fatigue = new FatigueRates
                {
                    PerHourMoving = 9f, PerHourEngaged = 24f,
                    RecoveryPerHourResting = 12f,
                },
            });

            c.Types.Add(new UnitCapabilities
            {
                Id = Armor,
                Name = "Armour",
                CrossCountrySpeedMps = 9f,
                RoadSpeedMps = 17f,
                MaxClimbDegrees = 28f,
                ImpassableLandcover = new[] { LandcoverClass.Water, LandcoverClass.Marsh },
                LandcoverSpeedFactor = Factors(open: 1f, crop: 0.95f, forest: 0.25f, urban: 0.4f,
                    water: 0f, marsh: 0f, rock: 0.35f, sand: 0.75f, snow: 0.55f),
                DetectionRangeMetres = 2200f,
                EngagementRangeMetres = 2500f,
                Firepower = 40f,
                Protection = 8f,
                Consumption = new ConsumptionRates
                {
                    RationsPerHour = 0.5f, WaterPerHour = 1f,
                    AmmunitionPerHourEngaged = 40f, FuelPerHourMoving = 22f,
                },
                Fatigue = new FatigueRates
                {
                    PerHourMoving = 6f, PerHourEngaged = 22f,
                    RecoveryPerHourResting = 12f,
                },
            });

            // Recon: sees furthest, fights least.
            c.Types.Add(new UnitCapabilities
            {
                Id = ReconMotor,
                Name = "Reconnaissance (motorised)",
                CrossCountrySpeedMps = 8f,
                RoadSpeedMps = 20f,
                MaxClimbDegrees = 22f,
                ImpassableLandcover = new[] { LandcoverClass.Water, LandcoverClass.Marsh },
                LandcoverSpeedFactor = Factors(open: 1f, crop: 0.9f, forest: 0.3f, urban: 0.6f,
                    water: 0f, marsh: 0f, rock: 0.3f, sand: 0.7f, snow: 0.45f),
                DetectionRangeMetres = 4000f,
                DetectionInForestFactor = 0.5f,
                EngagementRangeMetres = 900f,
                Firepower = 6f,
                Protection = 1.4f,
                Fatigue = new FatigueRates
                {
                    PerHourMoving = 10f, PerHourEngaged = 26f,
                    RecoveryPerHourResting = 12f,
                },
            });

            // Artillery: outranges everything and cannot defend itself.
            c.Types.Add(new UnitCapabilities
            {
                Id = Artillery,
                Name = "Artillery",
                CrossCountrySpeedMps = 5f,
                RoadSpeedMps = 14f,
                MaxClimbDegrees = 18f,
                ImpassableLandcover = new[] { LandcoverClass.Water, LandcoverClass.Marsh },
                LandcoverSpeedFactor = Factors(open: 0.9f, crop: 0.8f, forest: 0.2f, urban: 0.5f,
                    water: 0f, marsh: 0f, rock: 0.2f, sand: 0.6f, snow: 0.4f),
                DetectionRangeMetres = 1200f,
                EngagementRangeMetres = 18000f,
                Firepower = 55f,
                Protection = 1f,
                CanDigIn = false,
                Consumption = new ConsumptionRates
                {
                    RationsPerHour = 0.5f, WaterPerHour = 1f,
                    AmmunitionPerHourEngaged = 60f, FuelPerHourMoving = 10f,
                },
                Fatigue = new FatigueRates
                {
                    PerHourMoving = 8f, PerHourEngaged = 18f,
                    RecoveryPerHourResting = 12f,
                },
            });

            return c;
        }

        /// <summary>
        /// Builds the per-landcover speed table in <see cref="LandcoverClass"/> order.
        /// Named arguments at the call site so the ordering cannot be silently wrong.
        /// </summary>
        private static float[] Factors(float open, float crop, float forest, float urban,
            float water, float marsh, float rock, float sand, float snow) =>
            new[] { open, crop, forest, urban, water, marsh, rock, sand, snow };
    }
}
