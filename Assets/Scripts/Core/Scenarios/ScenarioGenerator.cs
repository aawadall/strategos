// ScenarioGenerator.cs
// #334 / #349–#351: build a playable Scenario from ScenarioGenerationSettings.
//
// Reuses MapGenerator for terrain. Assembles ORBAT from UnitCatalogue ids, stubs one
// objective cell (defer feature placement to #51), and applies engagement victory
// templates. ValidateGenerated is the gate before train/play — see docs/scenario-generation.md.

using System;
using System.Collections.Generic;
using UnityEngine;
using Strategos.Maps;
using Strategos.Movement;
using Strategos.NatoSymbols;
using Strategos.Objectives;
using Strategos.Units;

namespace Strategos.Scenarios
{
    public static class ScenarioGenerator
    {
        private static readonly string[] FriendlyLeafCaps =
        {
            UnitCatalogue.InfantryMech,
            UnitCatalogue.Armor,
            UnitCatalogue.ReconMotor,
        };

        private static readonly string[] EnemyLeafCaps =
        {
            UnitCatalogue.InfantryMotor,
            UnitCatalogue.Armor,
            UnitCatalogue.Artillery,
        };

        /// <summary>
        /// Builds a Scenario and generates its map once so placements are on real ground.
        /// Caller may call <see cref="Scenario.GenerateMap"/> again; same seed reproduces.
        /// </summary>
        public static Scenario Generate(ScenarioGenerationSettings settings,
            out MapData map, UnitCatalogue catalogue = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            catalogue ??= UnitCatalogue.Default();

            settings.ForceRatio = Mathf.Clamp(settings.ForceRatio, 0.5f, 3f);
            if (settings.Width < 32) settings.Width = 32;
            if (settings.Height < 32) settings.Height = 32;

            var scenario = new Scenario
            {
                Name = $"Generated {settings.Engagement} {settings.Echelon}",
                Description =
                    "Procedurally generated scenario (#334 / #51). Map from MapGenerator; " +
                    "ORBAT from UnitCatalogue; objective PlaceNear when a POI exists.",
                Map = settings.ToMapSettings(),
                PlayerEchelon = settings.Echelon,
                TimeLimitTicks = Mathf.Max(0, settings.TimeLimitTicks),
            };

            var blue = new Side(new SideId(1), "BLUFOR", Affiliation.Friend)
            {
                RankLadder = RankLadderDefaults.UsArmy,
            };
            var red = new Side(new SideId(2), "OPFOR", Affiliation.Hostile)
            {
                RankLadder = RankLadderDefaults.Soviet,
            };
            scenario.Sides.Add(blue);
            scenario.Sides.Add(red);
            scenario.PlayerSide = blue.Id;

            map = scenario.GenerateMap();

            int friendlyLeaves = LeafCountFor(settings.Echelon);
            int enemyLeaves = Mathf.Max(1,
                Mathf.RoundToInt(friendlyLeaves * settings.ForceRatio));

            var foot = catalogue.Get(UnitCatalogue.InfantryFoot);
            var grid = MovementGrid.Build(map, foot);
            var blueAnchor = FindPassable(grid, map.Width / 4, map.Height / 4, map);
            var redAnchor = FindPassable(grid, (map.Width * 3) / 4, (map.Height * 3) / 4, map);
            var objCell = FindPassable(grid, map.Width / 2, map.Height / 2, map);

            int nextId = 1;
            int blueRoot = nextId++;
            int redRoot = nextId++;

            AddUnit(scenario, blue.Id, blueRoot, RootEntity(settings.Echelon),
                IconDecorator.VarStandard, settings.Echelon, blueAnchor,
                $"TF {settings.Echelon}", "GEN BDE", RootCapability(true), parent: 0);
            AddUnit(scenario, red.Id, redRoot, RootEntity(settings.Echelon),
                IconDecorator.VarStandard, settings.Echelon, redAnchor,
                $"2 {settings.Echelon}", "GEN DIV", RootCapability(false), parent: 0);

            Echelon leafEchelon = LeafEchelon(settings.Echelon);
            PlaceLeaves(scenario, blue.Id, ref nextId, blueRoot, friendlyLeaves,
                leafEchelon, blueAnchor, FriendlyLeafCaps, true, grid, map);
            PlaceLeaves(scenario, red.Id, ref nextId, redRoot, enemyLeaves,
                leafEchelon, redAnchor, EnemyLeafCaps, false, grid, map);

            var initialOwner = settings.Engagement switch
            {
                EngagementType.Defend => blue.Id,
                EngagementType.Attack => red.Id,
                _ => SideId.None,
            };

            scenario.Objectives.Add(new Objective
            {
                Id = 1,
                Name = "OBJECTIVE GENERATED",
                Cell = new Vector2(objCell.x, objCell.y),
                RadiusCells = 6f,
                InitialOwner = initialOwner,
                PlaceNearKind = settings.PlaceNearKind,
            });

            // Objectives were added after GenerateMap — resolve now. Training fallback (#359):
            // clear refs that did not match so ValidateGenerated still passes on sparse maps.
            var unresolved = ObjectivePlacement.Apply(scenario, map);
            if (unresolved.Count > 0)
            {
                for (int i = 0; i < scenario.Objectives.Count; i++)
                {
                    var o = scenario.Objectives[i];
                    if (!ObjectivePlacement.HasFeatureRef(o)) continue;
                    if (ObjectivePlacement.TryResolve(o, map, out _, out _)) continue;
                    o.PlaceNearKind = null;
                    o.PlaceNearName = string.Empty;
                }
            }

            ApplyVictoryTemplate(scenario, settings, blue.Id, red.Id);
            return scenario;
        }

        /// <summary>Convenience when the caller does not need the MapData handle.</summary>
        public static Scenario Generate(ScenarioGenerationSettings settings,
            UnitCatalogue catalogue = null) =>
            Generate(settings, out _, catalogue);

        /// <summary>
        /// Gate before train/play (#348): Scenario.Validate + per-side path to objectives +
        /// force-balance against settings.ForceRatio.
        /// </summary>
        public static List<string> ValidateGenerated(Scenario scenario, MapData map,
            ScenarioGenerationSettings settings, UnitCatalogue catalogue = null)
        {
            var problems = new List<string>();
            if (scenario == null)
            {
                problems.Add("Scenario is null.");
                return problems;
            }

            catalogue ??= UnitCatalogue.Default();
            if (map == null) map = scenario.GenerateMap();

            problems.AddRange(scenario.Validate(catalogue, map));
            CheckObjectivesReachablePerSide(problems, scenario, map, catalogue);
            if (settings != null) CheckForceBalance(problems, scenario, settings);
            return problems;
        }

        // ─── Victory templates (#350) ─────────────────────────────────────────

        private static void ApplyVictoryTemplate(Scenario s, ScenarioGenerationSettings settings,
            SideId blue, SideId red)
        {
            int hold = Mathf.Max(0, settings.HoldTicks);
            int deadline = Mathf.Max(1, settings.TimeLimitTicks);
            int[] objs = { 1 };

            switch (settings.Engagement)
            {
                case EngagementType.Defend:
                    s.Victory.Add(new VictoryCondition
                    {
                        Kind = VictoryKind.SurviveUntil, Side = blue, Priority = 10,
                        DeadlineTick = deadline,
                        Description = "BLUFOR held out to the deadline.",
                    });
                    s.Victory.Add(new VictoryCondition
                    {
                        Kind = VictoryKind.HoldObjectives, Side = blue, Priority = 10,
                        ObjectiveIds = objs, HoldTicks = hold,
                        Description = "BLUFOR held the objective.",
                    });
                    s.Victory.Add(new VictoryCondition
                    {
                        Kind = VictoryKind.HoldObjectives, Side = red, Priority = 8,
                        ObjectiveIds = objs, HoldTicks = hold,
                        Description = "OPFOR seized the objective.",
                    });
                    s.Victory.Add(new VictoryCondition
                    {
                        Kind = VictoryKind.DestroyEnemy, Side = red, Priority = 5,
                        StrengthThresholdPercent = 25f,
                        Description = "BLUFOR was rendered combat ineffective.",
                    });
                    break;

                case EngagementType.Attack:
                    s.Victory.Add(new VictoryCondition
                    {
                        Kind = VictoryKind.HoldObjectives, Side = blue, Priority = 10,
                        ObjectiveIds = objs, HoldTicks = hold,
                        Description = "BLUFOR seized the objective.",
                    });
                    s.Victory.Add(new VictoryCondition
                    {
                        Kind = VictoryKind.DestroyEnemy, Side = blue, Priority = 5,
                        StrengthThresholdPercent = 25f,
                        Description = "OPFOR was rendered combat ineffective.",
                    });
                    s.Victory.Add(new VictoryCondition
                    {
                        Kind = VictoryKind.SurviveUntil, Side = red, Priority = 10,
                        DeadlineTick = deadline,
                        Description = "OPFOR held out to the deadline.",
                    });
                    s.Victory.Add(new VictoryCondition
                    {
                        Kind = VictoryKind.HoldObjectives, Side = red, Priority = 8,
                        ObjectiveIds = objs, HoldTicks = hold,
                        Description = "OPFOR retained the objective.",
                    });
                    break;

                default: // Meeting
                    s.Victory.Add(new VictoryCondition
                    {
                        Kind = VictoryKind.HoldObjectives, Side = blue, Priority = 10,
                        ObjectiveIds = objs, HoldTicks = hold,
                        Description = "BLUFOR held the objective.",
                    });
                    s.Victory.Add(new VictoryCondition
                    {
                        Kind = VictoryKind.HoldObjectives, Side = red, Priority = 10,
                        ObjectiveIds = objs, HoldTicks = hold,
                        Description = "OPFOR held the objective.",
                    });
                    s.Victory.Add(new VictoryCondition
                    {
                        Kind = VictoryKind.DestroyEnemy, Side = blue, Priority = 5,
                        StrengthThresholdPercent = 25f,
                        Description = "OPFOR was rendered combat ineffective.",
                    });
                    s.Victory.Add(new VictoryCondition
                    {
                        Kind = VictoryKind.DestroyEnemy, Side = red, Priority = 5,
                        StrengthThresholdPercent = 25f,
                        Description = "BLUFOR was rendered combat ineffective.",
                    });
                    break;
            }
        }

        // ─── ORBAT (#349) ─────────────────────────────────────────────────────

        private static int LeafCountFor(Echelon echelon) => echelon switch
        {
            Echelon.Platoon => 2,
            Echelon.Company => 2,
            Echelon.Battalion => 3,
            Echelon.Regiment => 3,
            Echelon.Brigade => 3,
            _ => 2,
        };

        private static Echelon LeafEchelon(Echelon root) => root switch
        {
            Echelon.Platoon => Echelon.Squad,
            Echelon.Company => Echelon.Platoon,
            Echelon.Battalion => Echelon.Company,
            Echelon.Regiment => Echelon.Battalion,
            Echelon.Brigade => Echelon.Battalion,
            _ => Echelon.Platoon,
        };

        private static LandEntityCode RootEntity(Echelon _) => LandEntityCode.Infantry;

        private static string RootCapability(bool friendly) =>
            friendly ? UnitCatalogue.InfantryMech : UnitCatalogue.InfantryMotor;

        private static void PlaceLeaves(Scenario s, SideId side, ref int nextId, int parentId,
            int count, Echelon leafEchelon, Vector2Int anchor, string[] caps, bool friendly,
            MovementGrid grid, MapData map)
        {
            for (int i = 0; i < count; i++)
            {
                string cap = caps[i % caps.Length];
                var entity = EntityForCap(cap);
                int variant = VariantForCap(cap);
                var cell = OffsetPassable(grid, anchor, i, friendly, map);
                string des = friendly ? $"A{i + 1}" : $"3/{i + 1}";
                AddUnit(s, side, nextId++, entity, variant, leafEchelon, cell,
                    des, "GEN HQ", cap, parent: parentId);
            }
        }

        private static LandEntityCode EntityForCap(string cap) => cap switch
        {
            UnitCatalogue.Armor => LandEntityCode.Armor,
            UnitCatalogue.Artillery => LandEntityCode.Artillery,
            UnitCatalogue.ReconMotor => LandEntityCode.Reconnaissance,
            _ => LandEntityCode.Infantry,
        };

        private static int VariantForCap(string cap) => cap switch
        {
            UnitCatalogue.InfantryMech => IconDecorator.VarMechanized,
            UnitCatalogue.InfantryMotor => IconDecorator.VarMotorized,
            UnitCatalogue.ReconMotor => IconDecorator.VarMotorized,
            _ => IconDecorator.VarStandard,
        };

        private static void AddUnit(Scenario s, SideId side, int id, LandEntityCode entity,
            int variant, Echelon echelon, Vector2Int cell, string designation,
            string higherFormation, string capabilityId, int parent)
        {
            var affiliation = s.FindSide(side)?.Affiliation ?? Affiliation.Friend;
            var code = SIDCBuilder.Build(
                affiliation: affiliation,
                echelon: echelon,
                entityCode: (int)entity,
                entityType: variant);
            var unit = new UnitInstance(new UnitId(id), side, code.Raw,
                new Vector2(cell.x, cell.y), designation, higherFormation, 100f, capabilityId)
            {
                ParentId = new UnitId(parent),
            };
            s.Units.Add(unit);
        }

        // ─── Placement helpers ────────────────────────────────────────────────

        private static Vector2Int FindPassable(MovementGrid grid, int preferX, int preferY,
            MapData map)
        {
            preferX = Mathf.Clamp(preferX, 0, map.Width - 1);
            preferY = Mathf.Clamp(preferY, 0, map.Height - 1);
            if (grid != null && grid.Passable(preferX, preferY))
                return new Vector2Int(preferX, preferY);

            int maxR = Mathf.Max(map.Width, map.Height);
            for (int r = 1; r < maxR; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                    int x = preferX + dx, y = preferY + dy;
                    if (grid != null && grid.Passable(x, y)) return new Vector2Int(x, y);
                }
            }

            return new Vector2Int(preferX, preferY);
        }

        private static Vector2Int OffsetPassable(MovementGrid grid, Vector2Int anchor, int index,
            bool friendly, MapData map)
        {
            int dx = friendly ? (index + 1) * 2 : -(index + 1) * 2;
            int dy = friendly ? (index % 2) * 2 : -((index % 2) * 2);
            return FindPassable(grid, anchor.x + dx, anchor.y + dy, map);
        }

        // ─── Validation (#348) ────────────────────────────────────────────────

        private static void CheckObjectivesReachablePerSide(List<string> problems,
            Scenario scenario, MapData map, UnitCatalogue catalogue)
        {
            if (scenario.Objectives == null || scenario.Objectives.Count == 0) return;

            var hierarchy = new UnitHierarchy(scenario.Units);
            var grids = new Dictionary<string, MovementGrid>();

            MovementGrid GridFor(string capId)
            {
                if (grids.TryGetValue(capId, out var g)) return g;
                g = MovementGrid.Build(map, catalogue.Get(capId));
                grids[capId] = g;
                return g;
            }

            foreach (var objective in scenario.Objectives)
            {
                var goal = new Vector2Int(
                    Mathf.Clamp(Mathf.RoundToInt(objective.Cell.x), 0, map.Width - 1),
                    Mathf.Clamp(Mathf.RoundToInt(objective.Cell.y), 0, map.Height - 1));

                foreach (var side in scenario.Sides)
                {
                    int total = 0, reachable = 0;
                    foreach (var leaf in hierarchy.Leaves)
                    {
                        if (leaf.Side != side.Id) continue;
                        total++;
                        var start = new Vector2Int(
                            Mathf.Clamp(Mathf.RoundToInt(leaf.Cell.x), 0, map.Width - 1),
                            Mathf.Clamp(Mathf.RoundToInt(leaf.Cell.y), 0, map.Height - 1));
                        var grid = GridFor(leaf.CapabilityId);
                        if (grid == null) continue;
                        var path = PathFinder.Find(grid, start, goal);
                        if (path.Found) reachable++;
                    }

                    if (total > 0 && reachable == 0)
                        problems.Add(
                            $"Side {side.Name}: no leaf can PathFinder-reach objective " +
                            $"{objective.Id} at ({goal.x},{goal.y}).");
                }
            }
        }

        private static void CheckForceBalance(List<string> problems, Scenario scenario,
            ScenarioGenerationSettings settings)
        {
            var hierarchy = new UnitHierarchy(scenario.Units);
            int friendly = 0, enemy = 0;
            foreach (var leaf in hierarchy.Leaves)
            {
                if (leaf.Side == scenario.PlayerSide) friendly++;
                else enemy++;
            }

            if (friendly <= 0)
                problems.Add("Force balance: player side has no leaf units.");
            if (enemy <= 0)
                problems.Add("Force balance: opposing side has no leaf units.");
            if (friendly <= 0 || enemy <= 0) return;

            float actual = (float)enemy / friendly;
            float want = settings.ForceRatio;
            float tol = Mathf.Max(0.05f, settings.ForceRatioTolerance);
            if (Mathf.Abs(actual - want) > tol)
                problems.Add(
                    $"Force balance: enemy/friendly leaf ratio {actual:0.##} outside " +
                    $"{want:0.##} ± {tol:0.##}.");
        }
    }
}
