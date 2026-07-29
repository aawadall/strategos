// SIDCParser.cs
// Parses a 20-character APP-6D SIDC string into a fully structured SIDCCode.
// See docs/nato-symbol-generator.md for the SIDC field layout.

using System;
using UnityEngine;

namespace Strategos.NatoSymbols
{
    public static class SIDCParser
    {
        // Expected length of an APP-6D SIDC string.
        private const int SIDCLength = 20;

        /// <summary>
        /// Parses a 20-character APP-6D SIDC string.
        /// Returns true and populates <paramref name="result"/> on success.
        /// Returns false and logs an error on failure.
        /// </summary>
        public static bool TryParse(string sidc, out SIDCCode result)
        {
            result = default;

            if (string.IsNullOrEmpty(sidc))
            {
                Debug.LogError("[SIDCParser] SIDC string is null or empty.");
                return false;
            }

            sidc = sidc.Trim().ToUpperInvariant();

            if (sidc.Length != SIDCLength)
            {
                Debug.LogError($"[SIDCParser] SIDC '{sidc}' has length {sidc.Length}; expected {SIDCLength}.");
                return false;
            }

            try
            {
                result.Raw              = sidc;
                result.Affiliation      = ParseAffiliation(sidc[2]);
                result.Dimension        = ParseDimension(int.Parse(sidc.Substring(3, 2)));
                result.Status           = ParseStatus(sidc[5]);
                result.ModifierFlags    = ParseModifierFlags(sidc[6]);
                result.Echelon          = ParseEchelon(int.Parse(sidc.Substring(7, 2)));
                result.EntityCode       = int.Parse(sidc.Substring(9, 2));
                result.EntityType       = int.Parse(sidc.Substring(11, 2));
                result.EntitySubtype    = int.Parse(sidc.Substring(13, 2));
                result.Modifier1        = int.Parse(sidc.Substring(15, 2));
                result.Modifier2        = int.Parse(sidc.Substring(17, 2));

                // Text label fields are not encoded in SIDC — caller sets these separately.
                result.Designation      = string.Empty;
                result.HigherFormation  = string.Empty;
                result.StrengthLabel    = string.Empty;
                result.StrengthModifier = StrengthModifier.None;

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SIDCParser] Failed to parse SIDC '{sidc}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Parses a SIDC string and throws on failure. Prefer TryParse for runtime use.
        /// </summary>
        public static SIDCCode Parse(string sidc)
        {
            if (TryParse(sidc, out SIDCCode result))
                return result;
            throw new ArgumentException($"Invalid SIDC string: '{sidc}'");
        }

        /// <summary>
        /// Serialises a SIDCCode back to its canonical 20-character string.
        /// If the Raw field is set and valid, returns it directly.
        /// </summary>
        public static string Serialise(SIDCCode code)
        {
            if (!string.IsNullOrEmpty(code.Raw) && code.Raw.Length == SIDCLength)
                return code.Raw;

            // Reconstruct from fields if Raw is stale or missing.
            return string.Format("10{0}{1:D2}{2}{3}{4:D2}{5:D2}{6:D2}{7:D2}{8:D2}{9:D2}0",
                (int)code.Affiliation,
                (int)code.Dimension,
                (int)code.Status,
                (int)code.ModifierFlags & 0xF,
                (int)code.Echelon,
                code.EntityCode,
                code.EntityType,
                code.EntitySubtype,
                code.Modifier1,
                code.Modifier2);
        }

        // -----------------------------------------------------------------
        // Private field parsers
        // -----------------------------------------------------------------

        private static Affiliation ParseAffiliation(char c)
        {
            if (int.TryParse(c.ToString(), out int val) && Enum.IsDefined(typeof(Affiliation), val))
                return (Affiliation)val;

            // APP-6D also uses letter codes in some contexts — map common ones.
            switch (c)
            {
                case 'F': return Affiliation.Friend;
                case 'H': return Affiliation.Hostile;
                case 'N': return Affiliation.Neutral;
                case 'U': return Affiliation.Unknown;
                case 'P': return Affiliation.Pending;
                case 'A': return Affiliation.AssumedFriend;
                case 'S': return Affiliation.Suspect;
                default:
                    Debug.LogWarning($"[SIDCParser] Unknown affiliation code '{c}', defaulting to Unknown.");
                    return Affiliation.Unknown;
            }
        }

        private static SymbolDimension ParseDimension(int code)
        {
            if (Enum.IsDefined(typeof(SymbolDimension), code))
                return (SymbolDimension)code;

            Debug.LogWarning($"[SIDCParser] Unknown symbol set code {code}, defaulting to Land.");
            return SymbolDimension.Land;
        }

        private static UnitStatus ParseStatus(char c)
        {
            return c == '1' ? UnitStatus.AnticipatedPlanned : UnitStatus.Present;
        }

        private static SymbolModifierFlag ParseModifierFlags(char c)
        {
            // APP-6D position 7: 0=none, 1=HQ, 2=TF, 3=HQ+TF, 4=Feint, 5=HQ+Feint, 6=TF+Feint, 7=HQ+TF+Feint
            if (!int.TryParse(c.ToString(), out int val))
                return SymbolModifierFlag.None;

            var flags = SymbolModifierFlag.None;
            if ((val & 1) != 0) flags |= SymbolModifierFlag.Headquarters;
            if ((val & 2) != 0) flags |= SymbolModifierFlag.TaskForce;
            if ((val & 4) != 0) flags |= SymbolModifierFlag.FeintDummy;
            return flags;
        }

        private static Echelon ParseEchelon(int code)
        {
            if (code == 0) return Echelon.None;
            if (Enum.IsDefined(typeof(Echelon), code))
                return (Echelon)code;

            Debug.LogWarning($"[SIDCParser] Unknown echelon code {code}, defaulting to None.");
            return Echelon.None;
        }
    }
}
