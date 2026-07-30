// MapNames.cs
// Deterministic place-name generation.
//
// Names come out uppercase-safe: the amplifier bitmap font in ProceduralDrawUtil
// covers digits, capitals, space, hyphen, slash and period only, so anything a map
// label can carry has to stay inside that set.

using System.Text;

namespace Strategos.Maps
{
    public static class MapNames
    {
        private static readonly string[] Onsets =
        {
            "B", "BR", "D", "F", "G", "GR", "H", "K", "KR", "L", "M", "N",
            "P", "R", "S", "ST", "T", "TR", "V", "W",
        };

        private static readonly string[] Nuclei =
        {
            "A", "E", "I", "O", "U", "AU", "EI", "IE", "OE", "EN",
        };

        private static readonly string[] Codas =
        {
            "CH", "CK", "DT", "FF", "LD", "LM", "LS", "MM", "ND", "NG",
            "NN", "RG", "RK", "RN", "SS", "ST", "TZ",
        };

        private static readonly string[] Suffixes =
        {
            "BURG", "BERG", "DORF", "FELD", "HEIM", "HOF", "STADT",
            "TAL", "WALD", "BACH", "BRUCK", "AU",
        };

        private static readonly string[] TerrainSuffixes =
        {
            "HILL", "RIDGE", "HEIGHTS", "KNOB", "SPUR", "CREST",
        };

        /// <summary>A settlement name, e.g. <c>KRENDORF</c>.</summary>
        public static string Settlement(DeterministicRandom rng)
        {
            var sb = new StringBuilder();
            sb.Append(Onsets[rng.Range(0, Onsets.Length)]);
            sb.Append(Nuclei[rng.Range(0, Nuclei.Length)]);

            if (rng.Chance(0.55f))
                sb.Append(Codas[rng.Range(0, Codas.Length)]);

            sb.Append(Suffixes[rng.Range(0, Suffixes.Length)]);
            return sb.ToString();
        }

        /// <summary>A high-ground name, e.g. <c>BRAUCH RIDGE</c>.</summary>
        public static string HighGround(DeterministicRandom rng)
        {
            var sb = new StringBuilder();
            sb.Append(Onsets[rng.Range(0, Onsets.Length)]);
            sb.Append(Nuclei[rng.Range(0, Nuclei.Length)]);
            sb.Append(Codas[rng.Range(0, Codas.Length)]);
            sb.Append(' ');
            sb.Append(TerrainSuffixes[rng.Range(0, TerrainSuffixes.Length)]);
            return sb.ToString();
        }

        /// <summary>A watercourse name, e.g. <c>STEIN BACH</c>.</summary>
        public static string Watercourse(DeterministicRandom rng)
        {
            var sb = new StringBuilder();
            sb.Append(Onsets[rng.Range(0, Onsets.Length)]);
            sb.Append(Nuclei[rng.Range(0, Nuclei.Length)]);
            sb.Append(Codas[rng.Range(0, Codas.Length)]);
            sb.Append(rng.Chance(0.5f) ? " BACH" : " RIVER");
            return sb.ToString();
        }
    }
}
