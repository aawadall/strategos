// ScenarioSamples.cs
// Built-in scenarios, constructed in code.
//
// The committed JSON under Resources/Scenarios is the serialised form of what is here, and
// ScenarioProbe asserts the two still agree. That direction matters: code is the source of
// truth and the file is its rendering, so a change to the model that silently breaks the
// shipped file fails the probe rather than failing in front of a player.

using UnityEngine;
using Strategos.Directives;
using Strategos.Maps;
using Strategos.NatoSymbols;
using Strategos.Objectives;
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
            var blue = new Side(new SideId(1), "BLUFOR", Affiliation.Friend)
            {
                RankLadder = RankLadderDefaults.UsArmy,
            };
            var red = new Side(new SideId(2), "OPFOR", Affiliation.Hostile)
            {
                RankLadder = RankLadderDefaults.Soviet,
            };

            var s = new Scenario
            {
                Name = "Meeting Engagement",
                Description =
                    "Two mechanised companies advance into the same valley from opposite " +
                    "ends. Neither side knows the other is there.",
                // Placements sit inside the middle band of the map on purpose. A square map
                // shown in a wide card is cropped vertically — MapSheetCard never stretches,
                // because a map with a different scale on each axis misreports every distance
                // on it — so units near the north or south edge are simply not on screen
                // until there is pan and zoom.
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
            s.PlayerSide = blue.Id;

            // Each side gets a battalion above its units, so there is something to command
            // *through* rather than only around. A formation is a UnitInstance that owns
            // subordinates — it does not fight, is not detected, and its strength, readiness
            // and position roll up from what it owns.
            Add(s, blue.Id, 7, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Battalion, new Vector2(60f, 66f), "TF 1-7 IN", "3 BDE",
                UnitCatalogue.InfantryMech);
            Add(s, red.Id, 8, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Regiment, new Vector2(182f, 180f), "2 MRR", "2 MRD",
                UnitCatalogue.InfantryMotor);

            // South-west, advancing north-east.
            Add(s, blue.Id, 1, LandEntityCode.Infantry, IconDecorator.VarMechanized,
                Echelon.Company, new Vector2(58f, 72f), "A/1-7 IN", "1-7 IN",
                UnitCatalogue.InfantryMech, parent: 7);
            Add(s, blue.Id, 2, LandEntityCode.Armor, IconDecorator.VarStandard,
                Echelon.Platoon, new Vector2(76f, 60f), "1/A/2-69 AR", "2-69 AR",
                UnitCatalogue.Armor, parent: 7);
            // The scouts are green, and that is the interesting case rather than an
            // arbitrary one: a screen exists to report, so a slow reporter is the unit whose
            // training the commander feels first. Contacts reach the feed several ticks after
            // they were seen, carrying their own staleness.
            Add(s, blue.Id, 3, LandEntityCode.Reconnaissance, IconDecorator.VarMotorized,
                Echelon.Platoon, new Vector2(88f, 92f), "SCT/1-7 IN", "1-7 IN",
                UnitCatalogue.ReconMotor, training: 55f, parent: 7);

            // North-east, advancing south-west.
            // One green formation on each side, so training is a texture of the scenario
            // rather than a handicap on one of them.
            Add(s, red.Id, 4, LandEntityCode.Infantry, IconDecorator.VarMotorized,
                Echelon.Company, new Vector2(180f, 176f), "3/2 MRR", "2 MRR",
                UnitCatalogue.InfantryMotor, training: 70f, parent: 8);
            Add(s, red.Id, 5, LandEntityCode.Armor, IconDecorator.VarStandard,
                Echelon.Platoon, new Vector2(178f, 196f), "1/3/2 MRR", "2 MRR",
                UnitCatalogue.Armor, parent: 8);
            // Company echelon: APP-6D's one-bar mark covers company, battery and troop.
            Add(s, red.Id, 6, LandEntityCode.Artillery, IconDecorator.VarStandard,
                Echelon.Company, new Vector2(186f, 150f), "BTY/2 MRR", "2 MRR",
                UnitCatalogue.Artillery, strength: 90, parent: 8);

            // One objective, roughly equidistant, so the meeting engagement has somewhere to
            // meet. Neutral at the start: neither side is defending, which is what makes it a
            // meeting engagement rather than an attack.
            s.Objectives.Add(new Objective
            {
                Id = 1,
                Name = "OBJECTIVE ANVIL",
                // #95/#96: (119,123) was one cell into the lake once erosion ran as shipped
                // (EnableErosion: true) — every leaf unit on both sides got goalPassable=False
                // and the objective was never contested. (119,114) keeps the same x, so the
                // east-west balance between the two forces is unchanged, and moves 9 cells
                // south onto open, 0.6deg ground about 8 m above the lake — real margin, not
                // the next cell over. NetworkStage's road network (a 5-edge spanning tree over
                // 6 perimeter settlements, no loops) does not reach this valley on this seed:
                // the closest a road gets is 68 cells away, so "beside a junction" was not
                // achievable without abandoning the equidistant premise; this is deliberately
                // chosen high ground instead, verified with a real PathFinder.Find from every
                // leaf unit of both sides (see Artifacts/agents/shipped-map.md).
                // ShippedMapProbe is what exercises this against the real shipped map;
                // DirectorProbe and the other erosion-off probes cannot, which is #96.
                Cell = new Vector2(119f, 114f),
                RadiusCells = 10f,
                InitialOwner = SideId.None,
            });

            // #73/#36: a directive from higher, addressed to the one BLUFOR root (TF 1-7 IN,
            // unit 7) and never decomposed into orders — the player reads it, acknowledges it,
            // and works out the how themselves. From is TF 1-7 IN's own HigherFormation, not
            // invented here: "3 BDE" already exists on the unit below.
            s.Directive = new Directive
            {
                Id = 1,
                TargetUnit = new UnitId(7),
                From = "3 BDE",
                Intent = "Seize and hold OBJECTIVE ANVIL. Task force denies 2 MRR's advance " +
                         "through the valley, holding the open ground in their path and " +
                         "keeping it clear for brigade's follow-on forces.",
                Constraints = "Do not become decisively engaged beyond the objective. " +
                              "Preserve combat power for brigade's main effort.",
                DeadlineTick = 1200,
                ObjectiveIds = new[] { 1 },
            };

            // Four ways this ends. Holding is worth more than attrition, so a side that takes
            // the ground and keeps it beats one that merely survives — hence the priorities.
            const int TenMinutes = 600;

            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.HoldObjectives, Side = blue.Id, Priority = 10,
                ObjectiveIds = new[] { 1 }, HoldTicks = TenMinutes,
                DirectiveId = 1,
                Description = "BLUFOR held OBJECTIVE ANVIL for ten minutes.",
            });
            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.HoldObjectives, Side = red.Id, Priority = 10,
                ObjectiveIds = new[] { 1 }, HoldTicks = TenMinutes,
                Description = "OPFOR held OBJECTIVE ANVIL for ten minutes.",
            });
            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.DestroyEnemy, Side = blue.Id, Priority = 5,
                StrengthThresholdPercent = 25f,
                Description = "OPFOR was rendered combat ineffective.",
            });
            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.DestroyEnemy, Side = red.Id, Priority = 5,
                StrengthThresholdPercent = 25f,
                Description = "BLUFOR was rendered combat ineffective.",
            });

            // One hour. Undecided at the deadline is a draw, which is a real outcome here:
            // two companies that never closed have not won anything.
            s.TimeLimitTicks = 3600;

            return s;
        }

        /// <summary>Name of the push-north follow-on operation under Resources/Scenarios.</summary>
        public const string PushNorthName = "push-north";

        /// <summary>
        /// A small follow-on operation for #75 chunk 3's campaign-merge probe: a second
        /// meeting engagement, deliberately reusing <see cref="Skirmish"/>'s six leaf unit ids
        /// (1-3 BLUFOR, 4-6 OPFOR) at this operation's own placements — the Id-consistency
        /// authoring rule <c>docs/campaign-invariants.md</c> states for a
        /// <c>CampaignChain</c>'s constituent scenarios. Whichever of those six survive
        /// <c>Skirmish</c> find a match here; the ones that do not are simply this operation's
        /// own authored reinforcements, same as unit 9 below, which has no counterpart in
        /// <c>Skirmish</c> at all.
        ///
        /// Small (64x64) and erosion-off, unlike the shipped <see cref="Skirmish"/>: this
        /// scenario exists to be played unattended to a decision quickly and repeatably by
        /// <c>CampaignChainDriverProbe</c>, not to be a second piece of real campaign content —
        /// flagged here and in the probe's own header for whoever adds real campaign scenarios
        /// next, since <c>ShippedMapProbe</c> validates it exactly as if it were one.
        /// </summary>
        public static Scenario PushNorth()
        {
            var blue = new Side(new SideId(1), "BLUFOR", Affiliation.Friend)
            {
                RankLadder = RankLadderDefaults.UsArmy,
            };
            var red = new Side(new SideId(2), "OPFOR", Affiliation.Hostile)
            {
                RankLadder = RankLadderDefaults.Soviet,
            };

            var s = new Scenario
            {
                Name = "Push North",
                Description =
                    "The follow-on operation after the valley is secured: both sides commit " +
                    "what is left of their forces to a second meeting engagement over the " +
                    "next piece of open ground.",
                Map = new MapGenerationSettings
                {
                    Name = "North Ridge",
                    Seed = 20260801,
                    Width = 64,
                    Height = 64,
                    MetresPerCell = 25f,
                    Profile = ReliefProfile.Plains,
                    EnableErosion = false,
                    EnableCulture = false,
                },
            };

            s.Sides.Add(blue);
            s.Sides.Add(red);
            s.PlayerSide = blue.Id;

            // BLUFOR, west side. Same ids and capabilities as Skirmish's leaves 1-3, new
            // Cell and no ParentId (this operation's own ORBAT, deliberately flat rather than
            // reusing Skirmish's formation ids 7/8 — the merge must keep this, not the
            // carried-over parent).
            Add(s, blue.Id, 1, LandEntityCode.Infantry, IconDecorator.VarMechanized,
                Echelon.Company, new Vector2(16f, 28f), "A/1-7 IN", "1-7 IN",
                UnitCatalogue.InfantryMech);
            Add(s, blue.Id, 2, LandEntityCode.Armor, IconDecorator.VarStandard,
                Echelon.Platoon, new Vector2(16f, 32f), "1/A/2-69 AR", "2-69 AR",
                UnitCatalogue.Armor);
            Add(s, blue.Id, 3, LandEntityCode.Reconnaissance, IconDecorator.VarMotorized,
                Echelon.Platoon, new Vector2(16f, 36f), "SCT/1-7 IN", "1-7 IN",
                UnitCatalogue.ReconMotor);

            // OPFOR, east side. Same ids and capabilities as Skirmish's leaves 4-6.
            Add(s, red.Id, 4, LandEntityCode.Infantry, IconDecorator.VarMotorized,
                Echelon.Company, new Vector2(48f, 28f), "3/2 MRR", "2 MRR",
                UnitCatalogue.InfantryMotor);
            Add(s, red.Id, 5, LandEntityCode.Armor, IconDecorator.VarStandard,
                Echelon.Platoon, new Vector2(48f, 32f), "1/3/2 MRR", "2 MRR",
                UnitCatalogue.Armor);
            Add(s, red.Id, 6, LandEntityCode.Artillery, IconDecorator.VarStandard,
                Echelon.Company, new Vector2(48f, 36f), "BTY/2 MRR", "2 MRR",
                UnitCatalogue.Artillery, strength: 90);

            // Reinforcement: id 9 has no counterpart in Skirmish at all, so it proves the
            // "no match keeps its own authored state entirely" half of the merge rule.
            Add(s, blue.Id, 9, LandEntityCode.Infantry, IconDecorator.VarMechanized,
                Echelon.Platoon, new Vector2(20f, 44f), "1/B/1-7 IN", "1-7 IN",
                UnitCatalogue.InfantryMech);

            // One objective so SideDirector has somewhere to send an idle unit — see
            // Direction/SideDirector.cs: with none, NearestUnheld always returns null and an
            // unattended run never moves. Dead centre of the small map, well clear of either
            // start line.
            s.Objectives.Add(new Objective
            {
                Id = 1,
                Name = "OBJECTIVE RIDGE",
                Cell = new Vector2(32f, 32f),
                RadiusCells = 8f,
                InitialOwner = SideId.None,
            });

            // Destroy-enemy only, no hold requirement — this fixture only needs to decide
            // quickly and repeatably, not model a real operation's win conditions.
            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.DestroyEnemy, Side = blue.Id, Priority = 5,
                StrengthThresholdPercent = 40f,
                Description = "OPFOR was rendered combat ineffective.",
            });
            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.DestroyEnemy, Side = red.Id, Priority = 5,
                StrengthThresholdPercent = 40f,
                Description = "BLUFOR was rendered combat ineffective.",
            });

            s.TimeLimitTicks = 1800;

            return s;
        }

        private static void Add(Scenario s, SideId side, int id, LandEntityCode entity,
            int variant, Echelon echelon, Vector2 cell, string designation,
            string higherFormation, string capabilityId, int strength = 100,
            float training = 100f, int parent = 0)
        {
            var affiliation = s.FindSide(side)?.Affiliation ?? Affiliation.Friend;

            var code = SIDCBuilder.Build(
                affiliation: affiliation,
                echelon: echelon,
                entityCode: (int)entity,
                entityType: variant);

            var unit = new UnitInstance(new UnitId(id), side, code.Raw, cell,
                designation, higherFormation, strength, capabilityId)
            {
                Training = training,
                ParentId = new UnitId(parent),
            };
            s.Units.Add(unit);
        }
    }
}
