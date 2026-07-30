// MapCoordinates.cs
// Georeferencing for the map model: cell ↔ UTM ↔ WGS84 lat/lon ↔ MGRS.
// This is the source of the military grid overlay and of any future DEM import,
// so the conversions are the standard Snyder series rather than an approximation.

using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Strategos.Maps
{
    /// <summary>A point in the Universal Transverse Mercator grid.</summary>
    public struct UtmPoint
    {
        public int    Zone;      // 1–60
        public bool   North;     // hemisphere
        public double Easting;   // metres
        public double Northing;  // metres

        public UtmPoint(int zone, bool north, double easting, double northing)
        {
            Zone     = zone;
            North    = north;
            Easting  = easting;
            Northing = northing;
        }

        public override string ToString() =>
            $"{Zone}{(North ? 'N' : 'S')} {Easting:F0}E {Northing:F0}N";
    }

    /// <summary>
    /// Conversions between map cells, UTM, WGS84 geographic coordinates and MGRS.
    ///
    /// A map is treated as a flat patch of its UTM zone: cell → UTM is a plain
    /// translation from <see cref="MapHeader.Origin"/>. That is exact enough
    /// because UTM is already a plane projection and a single Strategos map spans
    /// tens of kilometres, well inside one zone. Maps that would straddle a zone
    /// boundary are out of scope.
    /// </summary>
    public static class MapCoordinates
    {
        // WGS84 ellipsoid.
        private const double A   = 6378137.0;               // semi-major axis, metres
        private const double F   = 1.0 / 298.257223563;     // flattening
        private const double K0  = 0.9996;                  // UTM scale factor
        private const double E2  = F * (2.0 - F);           // first eccentricity squared
        private const double EP2 = E2 / (1.0 - E2);         // second eccentricity squared

        private const double FalseEasting  = 500000.0;
        private const double FalseNorthing = 10000000.0;

        /// <summary>MGRS 100 km row letters: A–V omitting I and O (20 letters).</summary>
        private const string RowLetters = "ABCDEFGHJKLMNPQRSTUV";

        /// <summary>MGRS 100 km column letters by zone set (zone mod 3).</summary>
        private static readonly string[] ColumnSets =
        {
            "STUVWXYZ", // zone % 3 == 0
            "ABCDEFGH", // zone % 3 == 1
            "JKLMNPQR", // zone % 3 == 2
        };

        /// <summary>MGRS latitude bands, 8° each from 80°S; X spans 12° to 84°N.</summary>
        private const string BandLetters = "CDEFGHJKLMNPQRSTUVWX";

        // ─── Cell ↔ UTM ───────────────────────────────────────────────────────

        /// <summary>UTM position of a fractional cell coordinate.</summary>
        public static UtmPoint CellToUtm(MapHeader header, float x, float y)
        {
            var o = header.Origin;
            return new UtmPoint(
                o.Zone,
                o.IsNorthernHemisphere,
                o.Easting  + x * header.MetresPerCell,
                o.Northing + y * header.MetresPerCell);
        }

        /// <summary>Fractional cell coordinate of a UTM position in the map's own zone.</summary>
        public static Vector2 UtmToCell(MapHeader header, UtmPoint utm)
        {
            var o = header.Origin;
            return new Vector2(
                (float)((utm.Easting  - o.Easting)  / header.MetresPerCell),
                (float)((utm.Northing - o.Northing) / header.MetresPerCell));
        }

        // ─── UTM ↔ WGS84 ──────────────────────────────────────────────────────

        /// <summary>
        /// Geographic coordinates → UTM (Snyder forward series, 6th order).
        /// Latitude and longitude are in degrees.
        /// </summary>
        public static UtmPoint LatLonToUtm(double latDeg, double lonDeg)
        {
            int zone = ZoneForLongitude(lonDeg);
            return LatLonToUtm(latDeg, lonDeg, zone);
        }

        /// <summary>Geographic coordinates → UTM, forced into a given zone.</summary>
        public static UtmPoint LatLonToUtm(double latDeg, double lonDeg, int zone)
        {
            double phi    = latDeg * Mathf.Deg2Rad;
            double lambda = lonDeg * Mathf.Deg2Rad;
            double lambda0 = CentralMeridianRadians(zone);

            double sinPhi = Math.Sin(phi);
            double cosPhi = Math.Cos(phi);
            double tanPhi = Math.Tan(phi);

            double n = A / Math.Sqrt(1.0 - E2 * sinPhi * sinPhi);
            double t = tanPhi * tanPhi;
            double c = EP2 * cosPhi * cosPhi;
            double a = (lambda - lambda0) * cosPhi;

            double m = MeridionalArc(phi);

            double a2 = a * a, a3 = a2 * a, a4 = a3 * a, a5 = a4 * a, a6 = a5 * a;

            double easting = K0 * n * (
                    a
                  + (1.0 - t + c) * a3 / 6.0
                  + (5.0 - 18.0 * t + t * t + 72.0 * c - 58.0 * EP2) * a5 / 120.0)
                + FalseEasting;

            double northing = K0 * (m + n * tanPhi * (
                    a2 / 2.0
                  + (5.0 - t + 9.0 * c + 4.0 * c * c) * a4 / 24.0
                  + (61.0 - 58.0 * t + t * t + 600.0 * c - 330.0 * EP2) * a6 / 720.0));

            bool north = latDeg >= 0.0;
            if (!north) northing += FalseNorthing;

            return new UtmPoint(zone, north, easting, northing);
        }

        /// <summary>
        /// UTM → geographic coordinates (Snyder inverse series).
        /// Returns degrees as (latitude, longitude).
        /// </summary>
        public static void UtmToLatLon(UtmPoint utm, out double latDeg, out double lonDeg)
        {
            double x = utm.Easting - FalseEasting;
            double y = utm.Northing;
            if (!utm.North) y -= FalseNorthing;

            double m  = y / K0;
            double e1 = (1.0 - Math.Sqrt(1.0 - E2)) / (1.0 + Math.Sqrt(1.0 - E2));
            double mu = m / (A * (1.0 - E2 / 4.0 - 3.0 * E2 * E2 / 64.0 - 5.0 * E2 * E2 * E2 / 256.0));

            double e1_2 = e1 * e1, e1_3 = e1_2 * e1, e1_4 = e1_3 * e1;

            double phi1 = mu
                + (3.0 * e1 / 2.0 - 27.0 * e1_3 / 32.0) * Math.Sin(2.0 * mu)
                + (21.0 * e1_2 / 16.0 - 55.0 * e1_4 / 32.0) * Math.Sin(4.0 * mu)
                + (151.0 * e1_3 / 96.0) * Math.Sin(6.0 * mu)
                + (1097.0 * e1_4 / 512.0) * Math.Sin(8.0 * mu);

            double sinPhi1 = Math.Sin(phi1);
            double cosPhi1 = Math.Cos(phi1);
            double tanPhi1 = Math.Tan(phi1);

            double c1 = EP2 * cosPhi1 * cosPhi1;
            double t1 = tanPhi1 * tanPhi1;
            double n1 = A / Math.Sqrt(1.0 - E2 * sinPhi1 * sinPhi1);
            double r1 = A * (1.0 - E2) / Math.Pow(1.0 - E2 * sinPhi1 * sinPhi1, 1.5);
            double d  = x / (n1 * K0);

            double d2 = d * d, d3 = d2 * d, d4 = d3 * d, d5 = d4 * d, d6 = d5 * d;

            double phi = phi1 - (n1 * tanPhi1 / r1) * (
                    d2 / 2.0
                  - (5.0 + 3.0 * t1 + 10.0 * c1 - 4.0 * c1 * c1 - 9.0 * EP2) * d4 / 24.0
                  + (61.0 + 90.0 * t1 + 298.0 * c1 + 45.0 * t1 * t1 - 252.0 * EP2 - 3.0 * c1 * c1) * d6 / 720.0);

            double lambda = CentralMeridianRadians(utm.Zone) + (
                    d
                  - (1.0 + 2.0 * t1 + c1) * d3 / 6.0
                  + (5.0 - 2.0 * c1 + 28.0 * t1 - 3.0 * c1 * c1 + 8.0 * EP2 + 24.0 * t1 * t1) * d5 / 120.0
                ) / cosPhi1;

            latDeg = phi    * Mathf.Rad2Deg;
            lonDeg = lambda * Mathf.Rad2Deg;
        }

        public static int ZoneForLongitude(double lonDeg)
        {
            int zone = (int)Math.Floor((lonDeg + 180.0) / 6.0) + 1;
            return Mathf.Clamp(zone, 1, 60);
        }

        /// <summary>MGRS latitude band letter for a latitude in degrees.</summary>
        public static char BandForLatitude(double latDeg)
        {
            if (latDeg < -80.0 || latDeg >= 84.0) return 'Z'; // polar — UPS, not modelled
            int idx = (int)Math.Floor((latDeg + 80.0) / 8.0);
            idx = Mathf.Clamp(idx, 0, BandLetters.Length - 1);
            return BandLetters[idx];
        }

        // ─── MGRS ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Formats a UTM position as an MGRS reference, e.g. <c>32U MA 1234 5678</c>.
        /// <paramref name="digits"/> is per axis: 1 = 10 km, 3 = 100 m, 5 = 1 m.
        /// </summary>
        public static string FormatMgrs(UtmPoint utm, char band, int digits = 4, bool spaced = true)
        {
            digits = Mathf.Clamp(digits, 1, 5);

            GetHundredKmSquare(utm, out char col, out char row);

            double e = utm.Easting  - Math.Floor(utm.Easting  / 100000.0) * 100000.0;
            double n = utm.Northing - Math.Floor(utm.Northing / 100000.0) * 100000.0;

            int divisor = (int)Math.Pow(10, 5 - digits);
            int ei = (int)(e / divisor);
            int ni = (int)(n / divisor);

            var sb = new StringBuilder();
            sb.Append(utm.Zone.ToString(CultureInfo.InvariantCulture));
            sb.Append(band);
            if (spaced) sb.Append(' ');
            sb.Append(col).Append(row);
            if (spaced) sb.Append(' ');
            sb.Append(ei.ToString("D" + digits, CultureInfo.InvariantCulture));
            if (spaced) sb.Append(' ');
            sb.Append(ni.ToString("D" + digits, CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>MGRS reference for a fractional cell coordinate.</summary>
        public static string FormatMgrs(MapHeader header, float x, float y, int digits = 4) =>
            FormatMgrs(CellToUtm(header, x, y), header.Origin.Band, digits);

        /// <summary>
        /// The two-letter 100 km square designator containing a UTM position.
        /// </summary>
        public static void GetHundredKmSquare(UtmPoint utm, out char column, out char row)
        {
            int colIndex = (int)Math.Floor(utm.Easting / 100000.0) - 1;
            colIndex = Mathf.Clamp(colIndex, 0, 7);
            column = ColumnSets[utm.Zone % 3][colIndex];

            int rowIndex = (int)Math.Floor(utm.Northing / 100000.0) % 20;
            if (rowIndex < 0) rowIndex += 20;
            // Even-numbered zones start their row lettering at 'F' rather than 'A'.
            if (utm.Zone % 2 == 0) rowIndex = (rowIndex + 5) % 20;
            row = RowLetters[rowIndex];
        }

        /// <summary>
        /// Parses an MGRS reference back to UTM.
        ///
        /// A 100 km row letter repeats every 2 000 km northing, so the reference
        /// alone is ambiguous. <paramref name="near"/> resolves it: the closest
        /// candidate to that origin wins. Passing the map's own origin makes the
        /// round-trip exact for any position on the map.
        /// </summary>
        public static bool TryParseMgrs(string mgrs, GeoOrigin near, out UtmPoint utm)
        {
            utm = default;
            if (string.IsNullOrWhiteSpace(mgrs)) return false;

            string s = mgrs.Replace(" ", string.Empty).ToUpperInvariant();
            int i = 0;

            // Zone digits.
            while (i < s.Length && char.IsDigit(s[i])) i++;
            if (i == 0 || i > 2) return false;
            if (!int.TryParse(s.Substring(0, i), NumberStyles.Integer, CultureInfo.InvariantCulture, out int zone))
                return false;
            if (zone < 1 || zone > 60) return false;

            // Band + 100 km square letters.
            if (i + 3 > s.Length) return false;
            char band = s[i++];
            char col  = s[i++];
            char row  = s[i++];
            if (BandLetters.IndexOf(band) < 0) return false;

            int colIndex = ColumnSets[zone % 3].IndexOf(col);
            if (colIndex < 0) return false;

            int rowIndex = RowLetters.IndexOf(row);
            if (rowIndex < 0) return false;
            if (zone % 2 == 0) rowIndex = (rowIndex - 5 + 20) % 20;

            // Numeric part: an even number of digits, split evenly.
            string digits = s.Substring(i);
            if (digits.Length == 0 || digits.Length % 2 != 0 || digits.Length > 10) return false;
            foreach (char c in digits) if (!char.IsDigit(c)) return false;

            int half = digits.Length / 2;
            double scale = Math.Pow(10, 5 - half);
            double e = int.Parse(digits.Substring(0, half), CultureInfo.InvariantCulture) * scale;
            double n = int.Parse(digits.Substring(half),    CultureInfo.InvariantCulture) * scale;

            double easting = (colIndex + 1) * 100000.0 + e;

            // Resolve the 2 000 km northing ambiguity against the reference.
            double baseNorthing = rowIndex * 100000.0 + n;
            double k = Math.Round((near.Northing - baseNorthing) / 2000000.0);
            double northing = baseNorthing + k * 2000000.0;

            utm = new UtmPoint(zone, band >= 'N', easting, northing);
            return true;
        }

        /// <summary>Cell coordinate for an MGRS reference on a given map.</summary>
        public static bool TryMgrsToCell(MapHeader header, string mgrs, out Vector2 cell)
        {
            cell = default;
            if (!TryParseMgrs(mgrs, header.Origin, out var utm)) return false;
            cell = UtmToCell(header, utm);
            return true;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static double CentralMeridianRadians(int zone) =>
            ((zone - 1) * 6.0 - 180.0 + 3.0) * Mathf.Deg2Rad;

        /// <summary>Meridional arc length from the equator to <paramref name="phi"/>.</summary>
        private static double MeridionalArc(double phi)
        {
            double e4 = E2 * E2, e6 = e4 * E2;
            return A * (
                  (1.0 - E2 / 4.0 - 3.0 * e4 / 64.0 - 5.0 * e6 / 256.0) * phi
                - (3.0 * E2 / 8.0 + 3.0 * e4 / 32.0 + 45.0 * e6 / 1024.0) * Math.Sin(2.0 * phi)
                + (15.0 * e4 / 256.0 + 45.0 * e6 / 1024.0) * Math.Sin(4.0 * phi)
                - (35.0 * e6 / 3072.0) * Math.Sin(6.0 * phi));
        }
    }
}
