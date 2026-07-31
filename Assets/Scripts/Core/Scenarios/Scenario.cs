// Scenario.cs
// Everything needed to set up a playable situation: the ground, who is fighting, and where
// they start.
//
// One type covers all three flavours the milestone asks for:
//
//   Procedural   generate the map from Map, scatter placements by rule
//   Constructed  author the JSON by hand
//   Static       commit a JSON fixture (see Resources/Scenarios)
//
// The map itself is never stored — only the settings that generate it. Generation is
// deterministic (DeterministicRandom is integer-only PCG precisely so it is platform-stable),
// so a few hundred bytes of settings reproduce the same several megabytes of terrain
// anywhere. That is also what makes scenarios shareable.

using System.Collections.Generic;
using UnityEngine;
using Strategos.Maps;
using Strategos.Movement;
using Strategos.NatoSymbols;
using Strategos.Objectives;
using Strategos.Units;

namespace Strategos.Scenarios
{
    public sealed class Scenario
    {
        /// <summary>
        /// Bumped when the on-disk shape changes incompatibly, so an old file can be
        /// recognised rather than silently misread.
        /// </summary>
        public int FormatVersion = 1;

        public string Name = "Untitled";
        public string Description = string.Empty;

        /// <summary>How to generate the ground. Not the ground itself — see the file header.</summary>
        public MapGenerationSettings Map = new();

        public List<Side> Sides = new();

        /// <summary>
        /// Starting positions and composition.
        ///
        /// These are <see cref="UnitInstance"/> directly rather than a parallel placement
        /// type, because at t = 0 a placement *is* a unit and a second type would have to be
        /// kept in step with the first for no gain. When #11 adds runtime state — readiness,
        /// supply, suppression — the split becomes worth making, and the scenario should
        /// then carry initial state only.
        /// </summary>
        public List<UnitInstance> Units = new();

        /// <summary>
        /// The side the player commands. <see cref="SideId.None"/> means every side is
        /// playable, which is how a hot-seat or an observer session is spelled.
        /// </summary>
        /// <remarks>
        /// Until this existed the player could select and order *any* unit on the map,
        /// including the enemy's — not a bug so much as a concept that was never introduced,
        /// and it meant a scenario had no adversary at all. #36 will extend this with the
        /// echelon the player occupies, which decides what they can see and address.
        /// </remarks>
        public SideId PlayerSide;

        /// <summary>
        /// Ground worth taking. Definitions only — who holds what is runtime state and lives
        /// in <see cref="Objectives.VictoryEvaluator"/>.
        /// </summary>
        public List<Objective> Objectives = new();

        /// <summary>
        /// How this scenario can end. An empty list means it cannot, which is legal — a
        /// sandbox is a scenario you are not trying to win.
        /// </summary>
        public List<VictoryCondition> Victory = new();

        /// <summary>
        /// Ticks after which an undecided scenario is a draw. Zero means no limit.
        ///
        /// Separate from a <see cref="VictoryKind.SurviveUntil"/> deadline because they are
        /// different statements: that one says a side wins by lasting, this one says nobody
        /// did. A scenario can have either, both or neither.
        /// </summary>
        public int TimeLimitTicks;

        // ─── Lookup ───────────────────────────────────────────────────────────

        public Side FindSide(SideId id)
        {
            foreach (var s in Sides) if (s.Id == id) return s;
            return null;
        }

        public UnitInstance FindUnit(UnitId id)
        {
            foreach (var u in Units) if (u.Id == id) return u;
            return null;
        }

        public IEnumerable<UnitInstance> UnitsOf(SideId side)
        {
            foreach (var u in Units) if (u.Side == side) yield return u;
        }

        public int CountUnitsOf(SideId side)
        {
            int n = 0;
            foreach (var u in Units) if (u.Side == side) n++;
            return n;
        }

        /// <summary>Lowest unused unit id. Ids start at 1; 0 is <see cref="UnitId.None"/>.</summary>
        public UnitId NextUnitId()
        {
            int max = 0;
            foreach (var u in Units) if (u.Id.Value > max) max = u.Id.Value;
            return new UnitId(max + 1);
        }

        public SideId NextSideId()
        {
            int max = 0;
            foreach (var s in Sides) if (s.Id.Value > max) max = s.Id.Value;
            return new SideId(max + 1);
        }

        /// <summary>Generates the ground this scenario describes.</summary>
        public MapData GenerateMap() => MapGenerator.Generate(Map);

        // ─── Validation ───────────────────────────────────────────────────────

        /// <summary>
        /// Structural problems, one string each; empty means the scenario is loadable.
        ///
        /// Worth having because the failure modes here are silent: a unit referencing a side
        /// that does not exist renders with no affiliation and belongs to nobody, and a unit
        /// placed off the map is simply never drawn. Both look like rendering bugs.
        /// </summary>
        /// <param name="catalogue">
        /// When supplied, capability ids are checked against it. Worth passing: an unknown
        /// id degrades to the generic fallback rather than throwing, so a typo turns a
        /// mechanised company into a walking one and reads as a movement bug.
        /// </param>
        /// <param name="map">
        /// When supplied along with a catalogue, each unit's starting cell is checked against
        /// what its own capabilities can actually occupy. Costs a generation, so callers that
        /// already have the map should pass it and others should not bother.
        ///
        /// This catches the placement that looks like nothing at all: a unit sitting in a
        /// lake. The generator produces a lot of standing water (see the closed-basin note in
        /// CLAUDE.md), so a plausible-looking coordinate lands in one more often than you
        /// would expect, and the symbol draws perfectly well on top of it.
        /// </param>
        public List<string> Validate(UnitCatalogue catalogue = null, MapData map = null)
        {
            var problems = new List<string>();

            if (string.IsNullOrWhiteSpace(Name))
                problems.Add("Scenario has no name.");

            if (Map == null)
            {
                problems.Add("Scenario has no map settings.");
                return problems;   // nothing below can be checked without them
            }

            if (Map.Width < 8 || Map.Height < 8)
                problems.Add($"Map is {Map.Width}x{Map.Height}; the generator clamps below 8.");
            if (Map.MetresPerCell < 0.1f)
                problems.Add($"MetresPerCell {Map.MetresPerCell} is below the generator's 0.1 minimum.");

            // Two or more sides is the milestone's own definition of a scenario.
            if (Sides.Count < 2)
                problems.Add($"Scenario has {Sides.Count} side(s); at least 2 are required.");

            var seenSides = new HashSet<int>();
            foreach (var s in Sides)
            {
                if (!s.Id.IsValid) problems.Add($"Side '{s.Name}' has no id.");
                else if (!seenSides.Add(s.Id.Value)) problems.Add($"Duplicate side id {s.Id}.");
                if (string.IsNullOrWhiteSpace(s.Name)) problems.Add($"Side {s.Id} has no name.");
            }

            var seenUnits = new HashSet<int>();
            foreach (var u in Units)
            {
                string who = u.Id.IsValid ? u.Id.ToString() : $"'{u.Designation}'";

                if (!u.Id.IsValid) problems.Add($"Unit {who} has no id.");
                else if (!seenUnits.Add(u.Id.Value)) problems.Add($"Duplicate unit id {u.Id}.");

                if (FindSide(u.Side) == null)
                    problems.Add($"Unit {who} belongs to side {u.Side}, which does not exist.");

                // None is legitimate — it is how "no parent" is spelled.
                if (u.ParentId.IsValid && FindUnit(u.ParentId) == null)
                    problems.Add($"Unit {who} has parent {u.ParentId}, which does not exist.");
                if (u.ParentId.IsValid && u.ParentId == u.Id)
                    problems.Add($"Unit {who} is its own parent.");

                if (!SIDCParser.TryParse(u.Sidc, out _))
                    problems.Add($"Unit {who} has an unparseable SIDC '{u.Sidc}'.");

                // The viewport's convention: a cell coordinate names a sample point, so the
                // map spans -0.5 to size-0.5.
                if (u.Cell.x < -0.5f || u.Cell.x > Map.Width - 0.5f ||
                    u.Cell.y < -0.5f || u.Cell.y > Map.Height - 0.5f)
                    problems.Add($"Unit {who} at ({u.Cell.x:0.##}, {u.Cell.y:0.##}) is off the map.");

                if (u.Strength < 0 || u.Strength > 100)
                    problems.Add($"Unit {who} has strength {u.Strength}, outside 0-100.");

                if (catalogue != null && !catalogue.Contains(u.CapabilityId))
                    problems.Add($"Unit {who} has unknown capability id '{u.CapabilityId}'.");

                if (catalogue != null && map != null && u.IsOnMap(map))
                {
                    var caps = catalogue.Get(u.CapabilityId);
                    var cover = u.Landcover(map);
                    int cx = Mathf.Clamp(Mathf.RoundToInt(u.Cell.x), 0, map.Width - 1);
                    int cy = Mathf.Clamp(Mathf.RoundToInt(u.Cell.y), 0, map.Height - 1);
                    float slope = map.SampleSlopeDegrees(cx, cy);

                    if (!caps.CanEnter(cover, slope))
                        problems.Add(
                            $"Unit {who} starts on {LandcoverInfo.DisplayName(cover)} at " +
                            $"{slope:0}deg, which '{caps.Id}' cannot occupy.");
                }
            }

            foreach (var s in Sides)
                if (CountUnitsOf(s.Id) == 0)
                    problems.Add($"Side {s.Id} '{s.Name}' has no units.");

            if (PlayerSide.IsValid && FindSide(PlayerSide) == null)
                problems.Add($"Scenario is played as {PlayerSide}, which does not exist.");

            ValidateVictory(problems);

            if (catalogue != null && map != null) CheckReachability(problems, catalogue, map);

            return problems;
        }

        /// <summary>
        /// Objectives and victory conditions.
        /// </summary>
        /// <remarks>
        /// Every failure here is silent at runtime: a condition naming an objective that does
        /// not exist simply never comes true, and a scenario that cannot be won looks exactly
        /// like one you are losing.
        /// </remarks>
        private void ValidateVictory(List<string> problems)
        {
            var seen = new HashSet<int>();
            foreach (var o in Objectives)
            {
                if (!seen.Add(o.Id)) problems.Add($"Duplicate objective id {o.Id}.");
                if (o.RadiusCells <= 0f)
                    problems.Add($"Objective {o.Id} '{o.Name}' has a radius of {o.RadiusCells}.");
                if (Map != null &&
                    (o.Cell.x < -0.5f || o.Cell.x > Map.Width - 0.5f ||
                     o.Cell.y < -0.5f || o.Cell.y > Map.Height - 0.5f))
                    problems.Add($"Objective {o.Id} '{o.Name}' is off the map.");
                if (o.InitialOwner.IsValid && FindSide(o.InitialOwner) == null)
                    problems.Add($"Objective {o.Id} starts owned by {o.InitialOwner}, " +
                                 "which does not exist.");
            }

            foreach (var c in Victory)
            {
                if (c.Kind == VictoryKind.None)
                    problems.Add("A victory condition has no kind.");
                if (FindSide(c.Side) == null)
                    problems.Add($"Victory condition '{c}' awards {c.Side}, which does not exist.");

                if (c.Kind != VictoryKind.HoldObjectives) continue;

                if (c.ObjectiveIds == null || c.ObjectiveIds.Length == 0)
                {
                    problems.Add($"Victory condition '{c}' names no objectives, so it can " +
                                 "never come true.");
                    continue;
                }

                foreach (int id in c.ObjectiveIds)
                    if (!seen.Contains(id))
                        problems.Add($"Victory condition '{c}' names objective {id}, " +
                                     "which does not exist.");
            }

            if (TimeLimitTicks < 0)
                problems.Add($"Time limit {TimeLimitTicks} is negative.");
        }

        /// <summary>
        /// Every unit must be able to reach every other one it might have to fight.
        ///
        /// The failure this catches is completely silent: a unit placed on an island, or in a
        /// pocket walled off by lakes, sits on perfectly passable ground and simply never
        /// arrives anywhere. It was found in this project's own sample scenario, where one
        /// company of a *meeting* engagement occupied a 24-cell pocket it could not leave.
        ///
        /// One flood fill per distinct capability, because passability is a property of the
        /// unit: ground that isolates a mechanised company may be walkable on foot.
        /// </summary>
        private void CheckReachability(List<string> problems, UnitCatalogue catalogue, MapData map)
        {
            var byCapability = new Dictionary<string, MovementGrid>();

            foreach (var probe in Units)
            {
                var caps = catalogue.Get(probe.CapabilityId);
                if (!byCapability.TryGetValue(caps.Id, out var grid))
                    byCapability[caps.Id] = grid = MovementGrid.Build(map, caps);
                if (grid == null) continue;

                int px = Mathf.Clamp(Mathf.RoundToInt(probe.Cell.x), 0, map.Width - 1);
                int py = Mathf.Clamp(Mathf.RoundToInt(probe.Cell.y), 0, map.Height - 1);
                if (!grid.Passable(px, py)) continue;   // already reported as impassable

                var reached = Flood(grid, px, py);

                foreach (var other in Units)
                {
                    if (other.Id == probe.Id) continue;

                    int ox = Mathf.Clamp(Mathf.RoundToInt(other.Cell.x), 0, map.Width - 1);
                    int oy = Mathf.Clamp(Mathf.RoundToInt(other.Cell.y), 0, map.Height - 1);
                    if (!grid.Passable(ox, oy)) continue;

                    if (!reached[oy * map.Width + ox])
                    {
                        string who = string.IsNullOrEmpty(probe.Designation)
                            ? probe.Id.ToString() : probe.Designation;
                        string them = string.IsNullOrEmpty(other.Designation)
                            ? other.Id.ToString() : other.Designation;
                        problems.Add($"Unit {who} cannot reach {them}; they are in separate " +
                                     $"regions for '{caps.Id}'.");
                        break;   // one report per unit is enough to act on
                    }
                }
            }
        }

        private static bool[] Flood(MovementGrid grid, int x, int y)
        {
            int w = grid.Width, h = grid.Height;
            var seen = new bool[w * h];
            var queue = new Queue<int>();

            seen[y * w + x] = true;
            queue.Enqueue(y * w + x);

            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };

            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                int cx = c % w, cy = c / w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + dx[d], ny = cy + dy[d];
                    if (!grid.Passable(nx, ny)) continue;
                    int i = ny * w + nx;
                    if (seen[i]) continue;
                    seen[i] = true;
                    queue.Enqueue(i);
                }
            }
            return seen;
        }

        public bool IsValid => Validate().Count == 0;

        public override string ToString() =>
            $"{Name}: {Sides.Count} sides, {Units.Count} units, " +
            $"{Map?.Width}x{Map?.Height} @ {Map?.MetresPerCell} m/cell, seed {Map?.Seed}";
    }
}
