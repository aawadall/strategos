// ScenarioSamples.cs
// Built-in scenarios, constructed in code.
//
// The committed JSON under Resources/Scenarios is the serialised form of what is here, and
// ScenarioProbe asserts the two still agree. That direction matters: code is the source of
// truth and the file is its rendering, so a change to the model that silently breaks the
// shipped file fails the probe rather than failing in front of a player.

using UnityEngine;
using Strategos.ControlMeasures;
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

            // #161–#163: one of each authored GCM family so the sheet and ScenarioProbe
            // exercise the path. Placed clear of the lake and the objective circle.
            s.ControlMeasures.Add(new ControlMeasure
            {
                Id = 1,
                Kind = ControlMeasureKind.Checkpoint,
                Name = "CP ROCK",
                Owner = blue.Id,
                Cell = new Vector2(90f, 100f),
                RadiusCells = 4f,
            });
            s.ControlMeasures.Add(new ControlMeasure
            {
                Id = 2,
                Kind = ControlMeasureKind.PhaseLine,
                Name = "PL AMBER",
                Owner = blue.Id,
                Points =
                {
                    new Vector2(70f, 130f),
                    new Vector2(110f, 128f),
                    new Vector2(150f, 132f),
                },
            });
            s.ControlMeasures.Add(new ControlMeasure
            {
                Id = 3,
                Kind = ControlMeasureKind.Boundary,
                Name = "BDE BOUNDARY",
                Owner = blue.Id,
                Echelon = Echelon.Brigade,
                Points =
                {
                    new Vector2(40f, 80f),
                    new Vector2(100f, 90f),
                    new Vector2(160f, 85f),
                },
            });

            // #164 / #165: arrows and areas — one of each family the drawer paints.
            s.ControlMeasures.Add(new ControlMeasure
            {
                Id = 4,
                Kind = ControlMeasureKind.AxisOfAdvance,
                Name = "AXIS BLUE",
                Owner = blue.Id,
                AxisRole = AxisOfAdvanceRole.Main,
                Points =
                {
                    new Vector2(55f, 70f),
                    new Vector2(90f, 95f),
                    new Vector2(115f, 110f),
                },
            });
            s.ControlMeasures.Add(new ControlMeasure
            {
                Id = 5,
                Kind = ControlMeasureKind.DirectionOfAttack,
                Name = "DOA NORTH",
                Owner = blue.Id,
                Points =
                {
                    new Vector2(100f, 70f),
                    new Vector2(118f, 100f),
                },
            });
            s.ControlMeasures.Add(new ControlMeasure
            {
                Id = 6,
                Kind = ControlMeasureKind.EngagementArea,
                Name = "EA ANVIL",
                Owner = blue.Id,
                Points =
                {
                    new Vector2(108f, 104f),
                    new Vector2(130f, 104f),
                    new Vector2(130f, 124f),
                    new Vector2(108f, 124f),
                },
            });
            // Opposing-owned CP — PLAY with player=blue must hide it (#186).
            s.ControlMeasures.Add(new ControlMeasure
            {
                Id = 7,
                Kind = ControlMeasureKind.Checkpoint,
                Name = "CP RED",
                Owner = red.Id,
                Cell = new Vector2(170f, 170f),
                RadiusCells = 3f,
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

        /// <summary>Name of the highland opening under Resources/Scenarios (#109 / #213).</summary>
        public const string HighlandOpeningName = "highland-opening";

        /// <summary>
        /// Second-theatre fixture: Skirmish ORBAT with a Regiment above the BN, authored
        /// <see cref="Scenario.PlayerEchelon"/> = Regiment, and a directive whose
        /// <c>From</c> is still <c>3 BDE</c> — the same higher HQ that addressed the valley
        /// seat (#214). Requires career rank regiment (#76) after a valley win promotes.
        /// </summary>
        public static Scenario HighlandOpening()
        {
            var s = Skirmish();
            s.Name = "Highland Opening";
            s.Description =
                "Regiment-seat opening after a valley campaign — same higher HQ still speaks.";
            s.PlayerEchelon = Echelon.Regiment;

            var blue = s.PlayerSide;
            Add(s, blue, 17, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Regiment, new Vector2(60f, 66f), "1st Inf Regt", "3 BDE",
                UnitCatalogue.InfantryMech);

            var bn = s.FindUnit(new UnitId(7));
            if (bn != null)
            {
                bn.ParentId = new UnitId(17);
                bn.HigherFormation = "1st Inf Regt";
            }

            if (s.Directive.HasValue)
            {
                var d = s.Directive.Value;
                d.TargetUnit = new UnitId(17);
                d.From = "3 BDE";
                d.Intent =
                    "Open the highland approach. Hold the regiment's assigned ground and " +
                    "deny the enemy the ridge line for brigade's follow-on forces.";
                s.Directive = d;
            }

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
            // #51 / #238: bind to a SpotHeight POI (LandcoverStage always stamps these; works
            // with culture off). Stub Cell is the pre-resolve hint / fallback for distance.
            s.Objectives.Add(new Objective
            {
                Id = 1,
                Name = "OBJECTIVE RIDGE",
                Cell = new Vector2(32f, 32f),
                RadiusCells = 8f,
                InitialOwner = SideId.None,
                PlaceNearKind = MapPoiKind.SpotHeight,
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

        /// <summary>Name of the squad tutorial under Resources/Scenarios (#309).</summary>
        public const string TutorialName = "tutorial-squad";

        /// <summary>Display name authored on <see cref="Tutorial"/> — used by PLAY to detect the beat (#310).</summary>
        public const string TutorialDisplayName = "Squad Tutorial";

        /// <summary>True when <paramref name="scenario"/> is the #309 squad tutorial.</summary>
        public static bool IsTutorial(Scenario scenario) =>
            scenario != null &&
            string.Equals(scenario.Name, TutorialDisplayName, System.StringComparison.Ordinal);

        /// <summary>
        /// #309: squad-echelon skeleton for the interactive tutorial (#289). One friendly
        /// squad is the player's seat (ROADMAP: at squad you *are* the unit); one distant
        /// OPFOR squad and a centre objective give the same select/order path as skirmish
        /// without company C2. Small map, erosion off — same reasoning as
        /// <see cref="PushNorth"/>: load and Validate fast; #310 adds the first beat.
        /// </summary>
        public static Scenario Tutorial()
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
                Name = TutorialDisplayName,
                Description =
                    "Learn select and move at squad echelon — you are the unit. " +
                    "A quiet patch of ground before the full command problem.",
                Map = new MapGenerationSettings
                {
                    Name = "Tutorial Glade",
                    Seed = 20260804,
                    Width = 64,
                    Height = 64,
                    MetresPerCell = 25f,
                    Profile = ReliefProfile.Plains,
                    EnableErosion = false,
                    EnableCulture = false,
                },
                PlayerEchelon = Echelon.Squad,
            };

            s.Sides.Add(blue);
            s.Sides.Add(red);
            s.PlayerSide = blue.Id;

            Add(s, blue.Id, 1, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Squad, new Vector2(20f, 32f), "1 SQD / A", "A PLT",
                UnitCatalogue.InfantryFoot);

            Add(s, red.Id, 2, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Squad, new Vector2(48f, 32f), "OPFOR SQD", "OPFOR",
                UnitCatalogue.InfantryFoot, training: 70f);

            s.Objectives.Add(new Objective
            {
                Id = 1,
                Name = "RALLY POINT",
                Cell = new Vector2(32f, 32f),
                RadiusCells = 6f,
                InitialOwner = SideId.None,
                PlaceNearKind = MapPoiKind.SpotHeight,
            });

            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.HoldObjectives, Side = blue.Id, Priority = 10,
                ObjectiveIds = new[] { 1 }, HoldTicks = 300,
                Description = "BLUFOR held the rally point.",
            });
            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.HoldObjectives, Side = red.Id, Priority = 10,
                ObjectiveIds = new[] { 1 }, HoldTicks = 300,
                Description = "OPFOR held the rally point.",
            });
            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.DestroyEnemy, Side = blue.Id, Priority = 5,
                StrengthThresholdPercent = 40f,
                Description = "OPFOR rendered combat ineffective.",
            });
            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.DestroyEnemy, Side = red.Id, Priority = 5,
                StrengthThresholdPercent = 40f,
                Description = "BLUFOR rendered combat ineffective.",
            });

            s.TimeLimitTicks = 1800;

            return s;
        }

        /// <summary>Name of the T1 drill range under Resources/Scenarios (#475 W04).</summary>
        public const string DrillRangeT1Name = "drill-range-t1";

        /// <summary>Display name authored on <see cref="DrillRangeT1"/>.</summary>
        public const string DrillRangeT1DisplayName = "Drill Range — T1 Fire and Movement";

        /// <summary>
        /// #475 W04: a fixed, repeatable practice ground for one field drill rather than a
        /// generated skirmish — see docs/drill-range.md. Shares <see cref="Tutorial"/>'s
        /// squad-echelon, small-flat-map shape (fast, unattended-safe) but the OPFOR squad is
        /// dug into the only covered approach so crossing it without alternating fire and
        /// movement costs suppression the way it would anywhere else in the sim. T1 is the
        /// one drill the field manual already cross-links (#207) — see field-manual.md.
        /// Not wired into the main menu yet; reachable via <c>ScenarioIO.Load(DrillRangeT1Name)</c>
        /// until W05 makes it playable.
        /// </summary>
        public static Scenario DrillRangeT1()
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
                Name = DrillRangeT1DisplayName,
                Description =
                    "Practice ground, not a fight to win by attrition. OPFOR holds the only " +
                    "covered approach to the objective and will not move. Order your squad " +
                    "into drill T1 (Fire and Movement) from the DRILLS binder instead of a " +
                    "bare MOVE — crossing open ground without alternating fire and movement " +
                    "costs suppression the same way it would in any other engagement.",
                Map = new MapGenerationSettings
                {
                    Name = "Drill Range",
                    Seed = 20260831,
                    Width = 64,
                    Height = 64,
                    MetresPerCell = 25f,
                    Profile = ReliefProfile.Plains,
                    EnableErosion = false,
                    EnableCulture = false,
                },
                PlayerEchelon = Echelon.Squad,
            };

            s.Sides.Add(blue);
            s.Sides.Add(red);
            s.PlayerSide = blue.Id;

            Add(s, blue.Id, 1, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Squad, new Vector2(16f, 32f), "1 SQD / A", "A PLT",
                UnitCatalogue.InfantryFoot);

            Add(s, red.Id, 2, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Squad, new Vector2(48f, 32f), "OPFOR SQD", "OPFOR",
                UnitCatalogue.InfantryFoot, training: 70f);

            // Authored exactly on OPFOR's position, deliberately not a PlaceNearKind ref: a
            // range needs the same ground every time, not the nearest POI the generator
            // happens to place near this hint (which on some seeds is nowhere near either
            // squad — see docs/drill-range.md).
            s.Objectives.Add(new Objective
            {
                Id = 1,
                Name = "RANGE OBJECTIVE",
                Cell = new Vector2(48f, 32f),
                RadiusCells = 6f,
                InitialOwner = red.Id,
            });

            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.HoldObjectives, Side = blue.Id, Priority = 10,
                ObjectiveIds = new[] { 1 }, HoldTicks = 300,
                Description = "BLUFOR took and held the range objective.",
            });
            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.DestroyEnemy, Side = blue.Id, Priority = 5,
                StrengthThresholdPercent = 40f,
                Description = "OPFOR rendered combat ineffective.",
            });

            s.TimeLimitTicks = 1800;

            return s;
        }

        /// <summary>Resources name for climb op 1 — squad seat (#405 / #403).</summary>
        public const string ClimbSquadName = "climb-squad";

        /// <summary>Resources name for climb op 2 — company seat (#405 / #403).</summary>
        public const string ClimbCompanyName = "climb-company";

        /// <summary>Resources name for climb op 3 — battalion seat (#405 / #403).</summary>
        public const string ClimbBattalionName = "climb-battalion";

        public const string ClimbSquadDisplayName = "Climb — Squad Seat";
        public const string ClimbCompanyDisplayName = "Climb — Company Seat";
        public const string ClimbBattalionDisplayName = "Climb — Battalion Seat";

        /// <summary>
        /// #405: climb op 1. Thin variant of <see cref="Tutorial"/> with a different display
        /// name so <see cref="IsTutorial"/> / #310 does not fire mid-campaign. Leaf ids 1
        /// (friendly) and 2 (hostile) are the stable carry-over pair for the whole climb
        /// chain — see docs/climb-campaign.md.
        /// </summary>
        public static Scenario ClimbSquad()
        {
            var s = Tutorial();
            s.Name = ClimbSquadDisplayName;
            s.Description =
                "Climb campaign op 1: command a single squad. You are the unit — no C2 " +
                "problem yet. Surviving leaf ids carry into the company seat.";
            s.Map.Name = "Climb Glade";
            s.Map.Seed = 20260805;
            return s;
        }

        /// <summary>
        /// #405: climb op 2. Company HQ (id 11) over squad leaf 1 plus reinforcement squad 3;
        /// hostile company 12 over leaf 2 plus squad 4. PlayerEchelon = Company.
        /// </summary>
        public static Scenario ClimbCompany()
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
                Name = ClimbCompanyDisplayName,
                Description =
                    "Climb campaign op 2: take the company seat. Leaf squad 1 reports to " +
                    "company HQ 11; a second squad attaches as reinforcement.",
                Map = new MapGenerationSettings
                {
                    Name = "Climb Company Ground",
                    Seed = 20260806,
                    Width = 64,
                    Height = 64,
                    MetresPerCell = 25f,
                    Profile = ReliefProfile.Plains,
                    EnableErosion = false,
                    EnableCulture = false,
                },
                PlayerEchelon = Echelon.Company,
            };

            s.Sides.Add(blue);
            s.Sides.Add(red);
            s.PlayerSide = blue.Id;

            Add(s, blue.Id, 11, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Company, new Vector2(18f, 32f), "A CO", "1 BN",
                UnitCatalogue.InfantryFoot);
            Add(s, blue.Id, 1, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Squad, new Vector2(20f, 30f), "1 SQD / A", "A CO",
                UnitCatalogue.InfantryFoot, parent: 11);
            Add(s, blue.Id, 3, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Squad, new Vector2(20f, 34f), "2 SQD / A", "A CO",
                UnitCatalogue.InfantryFoot, parent: 11);

            Add(s, red.Id, 12, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Company, new Vector2(46f, 32f), "OPFOR CO", "OPFOR BN",
                UnitCatalogue.InfantryFoot);
            Add(s, red.Id, 2, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Squad, new Vector2(48f, 30f), "OPFOR SQD 1", "OPFOR CO",
                UnitCatalogue.InfantryFoot, training: 70f, parent: 12);
            Add(s, red.Id, 4, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Squad, new Vector2(48f, 34f), "OPFOR SQD 2", "OPFOR CO",
                UnitCatalogue.InfantryFoot, training: 70f, parent: 12);

            AddClimbObjectiveAndVictory(s, blue.Id, red.Id);
            s.TimeLimitTicks = 1800;
            return s;
        }

        /// <summary>
        /// #405: climb op 3. Battalion HQ (id 7) over company 11 (leaves 1, 3); hostile
        /// battalion 8 over company 12 (leaves 2, 4). PlayerEchelon = Battalion.
        /// </summary>
        public static Scenario ClimbBattalion()
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
                Name = ClimbBattalionDisplayName,
                Description =
                    "Climb campaign op 3: take the battalion seat. Company 11 falls under " +
                    "battalion HQ 7; formation orders now decompose one echelon per step.",
                Map = new MapGenerationSettings
                {
                    Name = "Climb Battalion Ground",
                    Seed = 20260807,
                    Width = 128,
                    Height = 128,
                    MetresPerCell = 25f,
                    Profile = ReliefProfile.Rolling,
                    EnableErosion = true,
                    EnableCulture = false,
                },
                PlayerEchelon = Echelon.Battalion,
            };

            s.Sides.Add(blue);
            s.Sides.Add(red);
            s.PlayerSide = blue.Id;

            Add(s, blue.Id, 7, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Battalion, new Vector2(36f, 64f), "1 BN", "3 BDE",
                UnitCatalogue.InfantryFoot);
            Add(s, blue.Id, 11, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Company, new Vector2(40f, 64f), "A CO", "1 BN",
                UnitCatalogue.InfantryFoot, parent: 7);
            Add(s, blue.Id, 1, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Squad, new Vector2(42f, 60f), "1 SQD / A", "A CO",
                UnitCatalogue.InfantryFoot, parent: 11);
            Add(s, blue.Id, 3, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Squad, new Vector2(42f, 68f), "2 SQD / A", "A CO",
                UnitCatalogue.InfantryFoot, parent: 11);

            Add(s, red.Id, 8, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Battalion, new Vector2(92f, 64f), "OPFOR BN", "OPFOR BDE",
                UnitCatalogue.InfantryFoot);
            Add(s, red.Id, 12, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Company, new Vector2(88f, 64f), "OPFOR CO", "OPFOR BN",
                UnitCatalogue.InfantryFoot, parent: 8);
            Add(s, red.Id, 2, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Squad, new Vector2(86f, 60f), "OPFOR SQD 1", "OPFOR CO",
                UnitCatalogue.InfantryFoot, training: 70f, parent: 12);
            Add(s, red.Id, 4, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Squad, new Vector2(86f, 68f), "OPFOR SQD 2", "OPFOR CO",
                UnitCatalogue.InfantryFoot, training: 70f, parent: 12);

            AddClimbObjectiveAndVictory(s, blue.Id, red.Id, objectiveCell: new Vector2(64f, 64f),
                radius: 8f);
            s.TimeLimitTicks = 2400;
            return s;
        }

        private static void AddClimbObjectiveAndVictory(Scenario s, SideId blue, SideId red,
            Vector2? objectiveCell = null, float radius = 6f)
        {
            var cell = objectiveCell ?? new Vector2(32f, 32f);
            s.Objectives.Add(new Objective
            {
                Id = 1,
                Name = "RALLY POINT",
                Cell = cell,
                RadiusCells = radius,
                InitialOwner = SideId.None,
                PlaceNearKind = MapPoiKind.SpotHeight,
            });

            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.HoldObjectives, Side = blue, Priority = 10,
                ObjectiveIds = new[] { 1 }, HoldTicks = 300,
                Description = "BLUFOR held the rally point.",
            });
            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.HoldObjectives, Side = red, Priority = 10,
                ObjectiveIds = new[] { 1 }, HoldTicks = 300,
                Description = "OPFOR held the rally point.",
            });
            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.DestroyEnemy, Side = blue, Priority = 5,
                StrengthThresholdPercent = 40f,
                Description = "OPFOR rendered combat ineffective.",
            });
            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.DestroyEnemy, Side = red, Priority = 5,
                StrengthThresholdPercent = 40f,
                Description = "BLUFOR rendered combat ineffective.",
            });
        }

        /// <summary>Name of the Little Round Top historical scenario (#333 / #342).</summary>
        public const string LittleRoundTopName = "little-round-top-20th-maine";

        /// <summary>
        /// Gettysburg — 20th Maine on Little Round Top (1863-07-02), converted from
        /// <c>Research/historical/little-round-top-20th-maine.md</c> (#333).
        ///
        /// Map is a procedural Hills approximation of a rocky wooded spur — not the real
        /// Gettysburg ground (Phase 1 has no SRTM/GeoTIFF ingestion). Crest binds via
        /// <see cref="Objective.PlaceNearKind"/> SpotHeight (#51).
        /// </summary>
        public static Scenario LittleRoundTop()
        {
            var union = new Side(new SideId(1), "UNION", Affiliation.Friend)
            {
                RankLadder = RankLadderDefaults.UsArmy,
            };
            var confederate = new Side(new SideId(2), "CONFEDERATE", Affiliation.Hostile)
            {
                RankLadder = RankLadderDefaults.Soviet,
            };

            var hills = ReliefProfiles.For(ReliefProfile.Hills);
            hills.Aridity = 0.12f;
            hills.SettlementsPerKiloCell = 0.02f;

            var s = new Scenario
            {
                Name = "Little Round Top — 20th Maine",
                Description =
                    "2 July 1863: the 20th Maine holds the left of Vincent's brigade on a " +
                    "rocky wooded spur against Law's Alabamians. " +
                    "TERRAIN CAVEAT: the map is a procedural Hills approximation chosen for " +
                    "wooded-spur character (relief, forest, sparse culture) — not the real " +
                    "Gettysburg ground. Real-terrain import is still Phase 1 deferred. " +
                    "Research: Research/historical/little-round-top-20th-maine.md (CMH PD).",
                Map = new MapGenerationSettings
                {
                    Name = "Little Round Top (approx.)",
                    Seed = 18630702,
                    Width = 128,
                    Height = 128,
                    MetresPerCell = 25f,
                    Profile = ReliefProfile.Hills,
                    EnableErosion = true,
                    EnableCulture = false,
                    ParameterOverride = hills,
                },
            };

            s.Sides.Add(union);
            s.Sides.Add(confederate);
            s.PlayerSide = union.Id;
            s.PlayerEchelon = Echelon.Regiment;

            // Union — regiment HQ + 20th Maine company (+ attached infantry).
            Add(s, union.Id, 10, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Regiment, new Vector2(88f, 64f), "20th Maine", "Vincent Bde",
                UnitCatalogue.InfantryFoot);
            Add(s, union.Id, 1, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Company, new Vector2(84f, 60f), "20 ME Co", "20th Maine",
                UnitCatalogue.InfantryFoot, parent: 10);
            Add(s, union.Id, 2, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Platoon, new Vector2(90f, 68f), "20 ME Det", "20th Maine",
                UnitCatalogue.InfantryFoot, parent: 10);

            // Confederate — abstracted Alabama attack (source lacks company-perfect IDs).
            Add(s, confederate.Id, 20, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Battalion, new Vector2(40f, 64f), "Law Bde (-)", "Hood Div",
                UnitCatalogue.InfantryFoot);
            Add(s, confederate.Id, 3, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Company, new Vector2(36f, 58f), "AL Inf A", "Law Bde",
                UnitCatalogue.InfantryFoot, parent: 20);
            Add(s, confederate.Id, 4, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Company, new Vector2(38f, 70f), "AL Inf B", "Law Bde",
                UnitCatalogue.InfantryFoot, parent: 20);

            // Crest — historical objective; SpotHeight resolves after map gen.
            s.Objectives.Add(new Objective
            {
                Id = 1,
                Name = "LITTLE ROUND TOP",
                Cell = new Vector2(80f, 64f),
                RadiusCells = 8f,
                InitialOwner = union.Id,
                PlaceNearKind = MapPoiKind.SpotHeight,
            });

            // #344: defensive battle position on the spur; Confederate axis of advance uphill.
            // Sources do not name APP-6D graphics; these are scenario briefing aids matching
            // the research intent (hold crest / attack up the western slope).
            s.ControlMeasures.Add(new ControlMeasure
            {
                Id = 1,
                Kind = ControlMeasureKind.BattlePosition,
                Name = "BP MAINE",
                Owner = union.Id,
                Points =
                {
                    new Vector2(76f, 56f),
                    new Vector2(92f, 56f),
                    new Vector2(92f, 72f),
                    new Vector2(76f, 72f),
                },
            });
            s.ControlMeasures.Add(new ControlMeasure
            {
                Id = 2,
                Kind = ControlMeasureKind.AxisOfAdvance,
                Name = "AXIS LAW",
                Owner = confederate.Id,
                AxisRole = AxisOfAdvanceRole.Main,
                Points =
                {
                    new Vector2(40f, 64f),
                    new Vector2(60f, 64f),
                    new Vector2(78f, 64f),
                },
            });

            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.HoldObjectives, Side = union.Id, Priority = 10,
                ObjectiveIds = new[] { 1 }, HoldTicks = 900,
                Description = "UNION held the crest.",
            });
            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.HoldObjectives, Side = confederate.Id, Priority = 10,
                ObjectiveIds = new[] { 1 }, HoldTicks = 600,
                Description = "CONFEDERATE seized the crest.",
            });
            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.DestroyEnemy, Side = union.Id, Priority = 5,
                StrengthThresholdPercent = 35f,
                Description = "CONFEDERATE attack broken.",
            });
            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.DestroyEnemy, Side = confederate.Id, Priority = 5,
                StrengthThresholdPercent = 35f,
                Description = "UNION rendered combat ineffective.",
            });

            // ~45 minutes of intense action (research: 1–2 hours within the larger day).
            s.TimeLimitTicks = 2700;

            s.HistoricalNotes.Add(new HistoricalNote
            {
                When = HistoricalNoteWhen.Briefing,
                Text =
                    "Historically the 20th Maine held the extreme left of Vincent's brigade. " +
                    "Late in the fight, low on ammunition, Chamberlain ordered a bayonet charge " +
                    "down the slope rather than withdraw. This scenario's mechanics already model " +
                    "Depleted and Withdraw vs Attack — watch what you choose when the line thins. " +
                    "Source: Research/historical/little-round-top-20th-maine.md (CMH PD).",
            });

            return s;
        }

        /// <summary>Name of the Belleau Wood historical scenario (#460 / #459).</summary>
        public const string BelleauWoodName = "belleau-wood-1st-marine-brigade";

        /// <summary>
        /// Belleau Wood — Marine battalion attack day (1918-06), from
        /// <c>Research/historical/belleau-wood-1st-marine-brigade.md</c> (#460).
        /// Procedural Rolling approximation — dense timber + village POI, not the real
        /// Aisne-Marne ground.
        /// </summary>
        public static Scenario BelleauWood()
        {
            var us = new Side(new SideId(1), "US MARINES", Affiliation.Friend)
            {
                RankLadder = RankLadderDefaults.UsArmy,
            };
            var german = new Side(new SideId(2), "GERMAN", Affiliation.Hostile)
            {
                RankLadder = RankLadderDefaults.Soviet,
            };

            var rolling = ReliefProfiles.For(ReliefProfile.Rolling);
            rolling.Aridity = 0.08f;
            rolling.SettlementsPerKiloCell = 0.06f;

            var s = new Scenario
            {
                Name = "Belleau Wood — Marine battalion",
                Description =
                    "6 June 1918 (attack day slice): a Marine battalion clears a wooded " +
                    "strongpoint and anchors on the village of Bouresches. " +
                    "TERRAIN CAVEAT: procedural Rolling map with humid timber — not the real " +
                    "Belleau Wood / Lucy-le-Bocage ground. " +
                    "Research: Research/historical/belleau-wood-1st-marine-brigade.md (CMH PD).",
                Map = new MapGenerationSettings
                {
                    Name = "Belleau Wood (approx.)",
                    Seed = 19180606,
                    Width = 128,
                    Height = 128,
                    MetresPerCell = 25f,
                    Profile = ReliefProfile.Rolling,
                    EnableErosion = true,
                    EnableCulture = true,
                    ParameterOverride = rolling,
                },
            };

            s.Sides.Add(us);
            s.Sides.Add(german);
            s.PlayerSide = us.Id;
            s.PlayerEchelon = Echelon.Battalion;

            Add(s, us.Id, 10, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Battalion, new Vector2(36f, 64f), "5th Mar Bn (-)", "4th Mar Bde",
                UnitCatalogue.InfantryFoot);
            Add(s, us.Id, 1, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Company, new Vector2(32f, 58f), "Co A", "5th Mar Bn",
                UnitCatalogue.InfantryFoot, parent: 10);
            Add(s, us.Id, 2, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Company, new Vector2(34f, 70f), "Co B", "5th Mar Bn",
                UnitCatalogue.InfantryFoot, parent: 10);
            Add(s, us.Id, 3, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Platoon, new Vector2(40f, 64f), "MG Det", "5th Mar Bn",
                UnitCatalogue.InfantryFoot, parent: 10, training: 90f);

            Add(s, german.Id, 20, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Battalion, new Vector2(92f, 64f), "Wood Garrison", "Reserve Div",
                UnitCatalogue.InfantryFoot);
            Add(s, german.Id, 4, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Company, new Vector2(88f, 56f), "MG Nest N", "Wood Garrison",
                UnitCatalogue.InfantryFoot, parent: 20);
            Add(s, german.Id, 5, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Company, new Vector2(90f, 72f), "MG Nest S", "Wood Garrison",
                UnitCatalogue.InfantryFoot, parent: 20);

            s.Objectives.Add(new Objective
            {
                Id = 1,
                Name = "BELLEAU WOOD",
                Cell = new Vector2(80f, 64f),
                RadiusCells = 10f,
                InitialOwner = german.Id,
                PlaceNearKind = MapPoiKind.SpotHeight,
            });
            s.Objectives.Add(new Objective
            {
                Id = 2,
                Name = "BOURESCHES",
                Cell = new Vector2(96f, 48f),
                RadiusCells = 6f,
                InitialOwner = german.Id,
                PlaceNearKind = MapPoiKind.Village,
            });

            s.ControlMeasures.Add(new ControlMeasure
            {
                Id = 1,
                Kind = ControlMeasureKind.AxisOfAdvance,
                Name = "AXIS WOOD",
                Owner = us.Id,
                AxisRole = AxisOfAdvanceRole.Main,
                Points =
                {
                    new Vector2(36f, 64f),
                    new Vector2(60f, 64f),
                    new Vector2(80f, 64f),
                },
            });

            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.HoldObjectives, Side = us.Id, Priority = 10,
                ObjectiveIds = new[] { 1, 2 }, HoldTicks = 600,
                Description = "US MARINES cleared the wood and held Bouresches.",
            });
            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.HoldObjectives, Side = german.Id, Priority = 10,
                ObjectiveIds = new[] { 1 }, HoldTicks = 900,
                Description = "GERMAN held the wood.",
            });
            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.DestroyEnemy, Side = us.Id, Priority = 5,
                StrengthThresholdPercent = 40f,
                Description = "GERMAN garrison broken.",
            });

            s.TimeLimitTicks = 3600;

            s.HistoricalNotes.Add(new HistoricalNote
            {
                When = HistoricalNoteWhen.Briefing,
                Text =
                    "Belleau Wood was cleared over roughly three weeks; this fight is one " +
                    "attack day at battalion scale. Approaches cross open ground into timber " +
                    "and machine-gun fire from the wood edge. " +
                    "Source: Research/historical/belleau-wood-1st-marine-brigade.md (CMH PD).",
            });

            return s;
        }

        /// <summary>Name of the Remagen bridge historical scenario (#461 / #459).</summary>
        public const string RemagenName = "remagen-ludendorff-bridge";

        /// <summary>
        /// Remagen — Ludendorff Bridge coup (1945-03-07), company/battalion slice (#461).
        /// Digest: <c>Research/historical/remagen-ludendorff-bridge.md</c>.
        /// </summary>
        public static Scenario Remagen()
        {
            var us = new Side(new SideId(1), "US ARMY", Affiliation.Friend)
            {
                RankLadder = RankLadderDefaults.UsArmy,
            };
            var german = new Side(new SideId(2), "GERMAN", Affiliation.Hostile)
            {
                RankLadder = RankLadderDefaults.Soviet,
            };

            var rolling = ReliefProfiles.For(ReliefProfile.Rolling);
            rolling.Aridity = 0.15f;
            rolling.SettlementsPerKiloCell = 0.08f;
            rolling.RiverThresholdFraction = 0.0010f;

            var s = new Scenario
            {
                Name = "Remagen — Ludendorff Bridge",
                Description =
                    "7 March 1945: a US combat command seizes the Ludendorff Bridge at Remagen " +
                    "before German demolition succeeds. " +
                    "TERRAIN CAVEAT: procedural Rolling map with culture bridges — not the real " +
                    "Rhine gorge. Research: Research/historical/remagen-ludendorff-bridge.md.",
                Map = new MapGenerationSettings
                {
                    Name = "Remagen (approx.)",
                    Seed = 19450307,
                    Width = 128,
                    Height = 128,
                    MetresPerCell = 25f,
                    Profile = ReliefProfile.Rolling,
                    EnableErosion = true,
                    EnableCulture = true,
                    ParameterOverride = rolling,
                },
            };

            s.Sides.Add(us);
            s.Sides.Add(german);
            s.PlayerSide = us.Id;
            s.PlayerEchelon = Echelon.Company;

            Add(s, us.Id, 10, LandEntityCode.Armor, IconDecorator.VarStandard,
                Echelon.Battalion, new Vector2(32f, 64f), "CCB (-)", "9th Armd Div",
                UnitCatalogue.Armor);
            Add(s, us.Id, 1, LandEntityCode.Armor, IconDecorator.VarStandard,
                Echelon.Company, new Vector2(28f, 60f), "A/27 Arm", "CCB",
                UnitCatalogue.Armor, parent: 10);
            Add(s, us.Id, 2, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Company, new Vector2(30f, 68f), "A/47 Inf", "CCB",
                UnitCatalogue.InfantryMech, parent: 10);

            Add(s, german.Id, 20, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Company, new Vector2(96f, 64f), "Bridge Guard", "LXVII Corps",
                UnitCatalogue.InfantryFoot);
            Add(s, german.Id, 3, LandEntityCode.Infantry, IconDecorator.VarStandard,
                Echelon.Platoon, new Vector2(92f, 58f), "Demo Det", "Bridge Guard",
                UnitCatalogue.InfantryFoot, parent: 20, training: 70f);

            s.Objectives.Add(new Objective
            {
                Id = 1,
                Name = "LUDENDORFF BRIDGE",
                Cell = new Vector2(72f, 64f),
                RadiusCells = 6f,
                InitialOwner = german.Id,
                PlaceNearKind = MapPoiKind.Bridge,
            });

            s.ControlMeasures.Add(new ControlMeasure
            {
                Id = 1,
                Kind = ControlMeasureKind.AxisOfAdvance,
                Name = "AXIS BRIDGE",
                Owner = us.Id,
                AxisRole = AxisOfAdvanceRole.Main,
                Points =
                {
                    new Vector2(32f, 64f),
                    new Vector2(52f, 64f),
                    new Vector2(72f, 64f),
                },
            });

            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.HoldObjectives, Side = us.Id, Priority = 10,
                ObjectiveIds = new[] { 1 }, HoldTicks = 450,
                Description = "US ARMY seized and held the bridge.",
            });
            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.HoldObjectives, Side = german.Id, Priority = 10,
                ObjectiveIds = new[] { 1 }, HoldTicks = 900,
                Description = "GERMAN held or denied the crossing.",
            });
            s.Victory.Add(new VictoryCondition
            {
                Kind = VictoryKind.DestroyEnemy, Side = us.Id, Priority = 5,
                StrengthThresholdPercent = 35f,
                Description = "GERMAN bridge guard broken.",
            });

            s.TimeLimitTicks = 2400;

            s.HistoricalNotes.Add(new HistoricalNote
            {
                When = HistoricalNoteWhen.Briefing,
                Text =
                    "On 7 March 1945 US armor found the Ludendorff Bridge still standing and " +
                    "rushed a coup de main before demolition charges finished the job. Speed " +
                    "to the objective matters more than a set-piece clearance. " +
                    "Source: Research/historical/remagen-ludendorff-bridge.md (CMH PD).",
            });

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
