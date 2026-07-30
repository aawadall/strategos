// UnitModelProbe.cs
// Verifies the unit and side model numerically, in batch mode, with no graphics device.
//
// The model is data with no visual output, so there is nothing to screenshot and no reason
// to build a player to check it. Following MapMeshProbe: assert the numbers headlessly.
//
// Menu:  Strategos > Probe Unit Model
// Batch: -executeMethod Strategos.Editor.UnitModelProbe.Run

#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.Maps;
using Strategos.NatoSymbols;
using Strategos.Units;

namespace Strategos.Editor
{
    public static class UnitModelProbe
    {
        private const int Seed = 20260729;
        private const int Cells = 128;

        [MenuItem("Strategos/Probe Unit Model")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            var map = MapGenerator.Generate(new MapGenerationSettings
            {
                Name = "UNITPROBE",
                Seed = Seed,
                Width = Cells,
                Height = Cells,
                MetresPerCell = 25f,
                Profile = ReliefProfile.Rolling,
                // Nothing asserted here depends on erosion, and it dominates generation cost.
                EnableErosion = false,
            });

            log.AppendLine($"map {map.Width}x{map.Height} @ {map.Header.MetresPerCell} m/cell  " +
                           $"elevation {map.Header.MinElevation:0.0} .. {map.Header.MaxElevation:0.0} m");

            bad += CheckNotComponents(log);
            bad += CheckIds(log);
            bad += CheckSides(log);
            bad += CheckUnit(map, log);
            bad += CheckCellWorldRoundTrip(map, log);

            log.AppendLine(bad == 0 ? "PROBE PASSED" : $"PROBE FAILED with {bad} problem(s)");
            if (bad == 0) Debug.Log("[UnitModelProbe]\n" + log);
            else Debug.LogError("[UnitModelProbe]\n" + log);
        }

        /// <summary>
        /// The acceptance criterion "no MonoBehaviour, no scene dependency" is otherwise
        /// unobservable until someone tries to use the model headlessly and cannot. Assert it.
        /// </summary>
        private static int CheckNotComponents(StringBuilder log)
        {
            int bad = 0;
            foreach (var t in new[] { typeof(UnitInstance), typeof(Side) })
            {
                if (typeof(MonoBehaviour).IsAssignableFrom(t) ||
                    typeof(ScriptableObject).IsAssignableFrom(t))
                {
                    log.AppendLine($"  FAIL {t.Name} derives from a Unity object type");
                    bad++;
                }
            }
            log.AppendLine($"  types are plain data: {(bad == 0 ? "yes" : "NO")}");
            return bad;
        }

        private static int CheckIds(StringBuilder log)
        {
            int bad = 0;

            if (UnitId.None.IsValid) { log.AppendLine("  FAIL UnitId.None reports valid"); bad++; }
            if (SideId.None.IsValid) { log.AppendLine("  FAIL SideId.None reports valid"); bad++; }
            if (!new UnitId(1).IsValid) { log.AppendLine("  FAIL UnitId(1) reports invalid"); bad++; }

            // default(UnitId) must equal None, or a freshly deserialised unit with no parent
            // would not read as parentless.
            if (default(UnitId) != UnitId.None)
            {
                log.AppendLine("  FAIL default(UnitId) != UnitId.None");
                bad++;
            }

            if (new UnitId(7) != new UnitId(7)) { log.AppendLine("  FAIL UnitId equality"); bad++; }
            if (new UnitId(7) == new UnitId(8)) { log.AppendLine("  FAIL UnitId inequality"); bad++; }

            log.AppendLine($"  ids: None invalid, equality by value  {(bad == 0 ? "ok" : "FAILED")}");
            return bad;
        }

        private static int CheckSides(StringBuilder log)
        {
            int bad = 0;

            var blue = new Side(new SideId(1), "BLUFOR", Affiliation.Friend);
            var red = new Side(new SideId(2), "OPFOR", Affiliation.Hostile);

            if (blue.Id == red.Id) { log.AppendLine("  FAIL sides share an id"); bad++; }

            // Colour comes from the shared AffiliationColour table so sides and symbols agree.
            if (blue.Colour != AffiliationColour.ForAffiliation(Affiliation.Friend))
            {
                log.AppendLine("  FAIL blue side colour does not match the affiliation table");
                bad++;
            }
            if (blue.Colour == red.Colour)
            {
                log.AppendLine("  FAIL friend and hostile default to the same colour");
                bad++;
            }

            // A side is not an affiliation: two sides may share one and stay distinct.
            var blue2 = new Side(new SideId(3), "Allied contingent", Affiliation.Friend);
            if (blue2.Affiliation != blue.Affiliation || blue2.Id == blue.Id)
            {
                log.AppendLine("  FAIL two sides cannot share an affiliation while staying distinct");
                bad++;
            }

            log.AppendLine($"  sides: {blue.Name} / {red.Name} distinct, colours from the " +
                           $"affiliation table  {(bad == 0 ? "ok" : "FAILED")}");
            return bad;
        }

        private static int CheckUnit(MapData map, StringBuilder log)
        {
            int bad = 0;

            // Canonical friend infantry company.
            const string sidc = "10031000151211000000";
            var u = new UnitInstance(new UnitId(1), new SideId(1), sidc,
                new Vector2(40.5f, 60.25f), "1-7 IN", "3 ID", strength: 85);

            if (u.ParentId.IsValid)
            {
                log.AppendLine("  FAIL a new unit has a parent; ParentId must default to None");
                bad++;
            }

            var code = u.ToSidcCode();
            if (code.Raw != sidc) { log.AppendLine($"  FAIL SIDC raw round trip: {code.Raw}"); bad++; }
            if (code.Affiliation != Affiliation.Friend) { log.AppendLine("  FAIL affiliation"); bad++; }
            if (code.Echelon != Echelon.Company) { log.AppendLine("  FAIL echelon"); bad++; }
            if (code.Designation != "1-7 IN") { log.AppendLine("  FAIL designation not carried"); bad++; }
            if (code.HigherFormation != "3 ID") { log.AppendLine("  FAIL higher formation not carried"); bad++; }
            if (code.StrengthLabel != "85") { log.AppendLine("  FAIL strength label not carried"); bad++; }

            // Position derives everything from the map; nothing is stored twice.
            float elevation = u.Elevation(map);
            string mgrs = u.Mgrs(map);
            var cover = u.Landcover(map);

            if (elevation < map.Header.MinElevation - 0.01f ||
                elevation > map.Header.MaxElevation + 0.01f)
            {
                log.AppendLine($"  FAIL elevation {elevation:0.0} outside the map's range");
                bad++;
            }
            if (string.IsNullOrWhiteSpace(mgrs)) { log.AppendLine("  FAIL empty MGRS"); bad++; }
            if (!u.IsOnMap(map)) { log.AppendLine("  FAIL unit reports off-map"); bad++; }

            // The bounds convention is the viewport's: a cell coordinate names a sample
            // point, so the map spans -0.5 to width-0.5.
            var off = new UnitInstance(new UnitId(2), new SideId(1), sidc,
                new Vector2(map.Width + 5f, 10f));
            if (off.IsOnMap(map)) { log.AppendLine("  FAIL off-map unit reports on-map"); bad++; }

            log.AppendLine($"  unit: {u}");
            log.AppendLine($"        {mgrs}   {elevation:0} m   {LandcoverInfo.DisplayName(cover)}");
            return bad;
        }

        /// <summary>
        /// Cell space is the single source of position. If cell -> world -> cell does not
        /// round trip, every later consumer (pathfinding, the drape, hit-testing) inherits
        /// the error.
        /// </summary>
        private static int CheckCellWorldRoundTrip(MapData map, StringBuilder log)
        {
            int bad = 0;
            var probes = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(40.5f, 60.25f),
                new Vector2(map.Width - 1f, map.Height - 1f),
            };

            float worst = 0f;
            foreach (var cell in probes)
            {
                var u = new UnitInstance(new UnitId(1), new SideId(1),
                    SIDCCode.Empty.Raw, cell);
                Vector3 world = u.WorldPosition(map);
                Vector2 back = map.WorldToCell(world);
                float err = Vector2.Distance(cell, back);
                worst = Mathf.Max(worst, err);

                if (err > 1e-3f)
                {
                    log.AppendLine($"  FAIL cell {cell} -> world {world} -> cell {back} (err {err:0.####})");
                    bad++;
                }

                // y is elevation in metres, and x/z are metres from the SW corner.
                float expectX = cell.x * map.Header.MetresPerCell;
                float expectZ = cell.y * map.Header.MetresPerCell;
                if (Mathf.Abs(world.x - expectX) > 0.01f || Mathf.Abs(world.z - expectZ) > 0.01f)
                {
                    log.AppendLine($"  FAIL world xz {world.x:0.##},{world.z:0.##} " +
                                   $"expected {expectX:0.##},{expectZ:0.##}");
                    bad++;
                }
            }

            log.AppendLine($"  cell/world round trip over {probes.Length} probes: " +
                           $"worst error {worst:0.######} cells");
            return bad;
        }
    }
}
#endif
