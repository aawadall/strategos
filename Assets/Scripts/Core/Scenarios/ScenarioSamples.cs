// ScenarioSamples.cs
// Built-in scenarios, constructed in code.
//
// The committed JSON under Resources/Scenarios is the serialised form of what is here, and
// ScenarioProbe asserts the two still agree. That direction matters: code is the source of
// truth and the file is its rendering, so a change to the model that silently breaks the
// shipped file fails the probe rather than failing in front of a player.

using UnityEngine;
using Strategos.Maps;
using Strategos.NatoSymbols;
using Strategos.Units;

namespace Strategos.Scenarios
{
    public static class ScenarioSamples
    {
        /// <summary>Name of the skirmish under Resources/Scenarios.</summary>
        public const string SkirmishName = "skirmish";

        /// <summary>
        /// A meeting engagement: two companies approaching from opposite corners of a
        /// 256-cell rolling map.
        ///
        /// 256 cells at 25 m is 6.4 km square, which is a plausible company-level frontage
        /// and generates in a fraction of the time a 512-cell map takes. The seed is the
        /// project's usual fixed one so the ground is the same every run.
        /// </summary>
        public static Scenario Skirmish()
        {
            var blue = new Side(new SideId(1), "BLUFOR", Affiliation.Friend);
            var red = new Side(new SideId(2), "OPFOR", Affiliation.Hostile);

            var s = new Scenario
            {
                Name = "Meeting Engagement",
                Description =
                    "Two mechanised companies advance into the same valley from opposite " +
                    "ends. Neither side knows the other is there.",
                Map = new MapGenerationSettings
                {
                    Name = "Valley",
                    Seed = 20260729,
                    Width = 256,
                    Height = 256,
                    MetresPerCell = 25f,
                    Profile = ReliefProfile.Rolling,
                    EnableErosion = true,
                    EnableCulture = true,
                },
            };

            s.Sides.Add(blue);
            s.Sides.Add(red);

            // South-west, advancing north-east.
            Add(s, blue.Id, 1, LandEntityCode.Infantry, IconDecorator.VarMechanized,
                Echelon.Company, new Vector2(40f, 40f), "A/1-7 IN", "1-7 IN",
                UnitCatalogue.InfantryMech);
            Add(s, blue.Id, 2, LandEntityCode.Armor, IconDecorator.VarStandard,
                Echelon.Platoon, new Vector2(52f, 34f), "1/A/2-69 AR", "2-69 AR",
                UnitCatalogue.Armor);
            Add(s, blue.Id, 3, LandEntityCode.Reconnaissance, IconDecorator.VarMotorized,
                Echelon.Platoon, new Vector2(60f, 52f), "SCT/1-7 IN", "1-7 IN",
                UnitCatalogue.ReconMotor);

            // North-east, advancing south-west.
            Add(s, red.Id, 4, LandEntityCode.Infantry, IconDecorator.VarMotorized,
                Echelon.Company, new Vector2(215f, 210f), "3/2 MRR", "2 MRR",
                UnitCatalogue.InfantryMotor);
            Add(s, red.Id, 5, LandEntityCode.Armor, IconDecorator.VarStandard,
                Echelon.Platoon, new Vector2(203f, 218f), "1/3/2 MRR", "2 MRR",
                UnitCatalogue.Armor);
            // Company echelon: APP-6D's one-bar mark covers company, battery and troop.
            Add(s, red.Id, 6, LandEntityCode.Artillery, IconDecorator.VarStandard,
                Echelon.Company, new Vector2(228f, 232f), "BTY/2 MRR", "2 MRR",
                UnitCatalogue.Artillery, strength: 90);

            return s;
        }

        private static void Add(Scenario s, SideId side, int id, LandEntityCode entity,
            int variant, Echelon echelon, Vector2 cell, string designation,
            string higherFormation, string capabilityId, int strength = 100)
        {
            var affiliation = s.FindSide(side)?.Affiliation ?? Affiliation.Friend;

            var code = SIDCBuilder.Build(
                affiliation: affiliation,
                echelon: echelon,
                entityCode: (int)entity,
                entityType: variant);

            s.Units.Add(new UnitInstance(new UnitId(id), side, code.Raw, cell,
                designation, higherFormation, strength, capabilityId));
        }
    }
}
