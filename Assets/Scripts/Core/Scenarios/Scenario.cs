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
using Strategos.NatoSymbols;
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
        public List<string> Validate()
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
            }

            foreach (var s in Sides)
                if (CountUnitsOf(s.Id) == 0)
                    problems.Add($"Side {s.Id} '{s.Name}' has no units.");

            return problems;
        }

        public bool IsValid => Validate().Count == 0;

        public override string ToString() =>
            $"{Name}: {Sides.Count} sides, {Units.Count} units, " +
            $"{Map?.Width}x{Map?.Height} @ {Map?.MetresPerCell} m/cell, seed {Map?.Seed}";
    }
}
