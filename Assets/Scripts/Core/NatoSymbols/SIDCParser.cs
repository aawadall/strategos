// SIDCParser.cs
// Parses a 20-character APP-6D SIDC string into a fully structured SIDCCode.
// Optional third ten digits (30 total) are accepted and ignored.
// Layout: APP-6(D) Annex A §A.3–A.11.

using System;
using UnityEngine;

namespace Strategos.NatoSymbols
{
    public static class SIDCParser
    {
        private const int SIDCLength = 20;
        private const int SIDCExtendedLength = 30;

        /// <summary>
        /// Parses a 20-character APP-6D SIDC string (or 30 with optional extension).
        /// Returns true and populates <paramref name="result"/> on success.
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

            if (sidc.Length != SIDCLength && sidc.Length != SIDCExtendedLength)
            {
                Debug.LogError($"[SIDCParser] SIDC '{sidc}' has length {sidc.Length}; expected {SIDCLength} or {SIDCExtendedLength}.");
                return false;
            }

            // Work with the required first 20 digits; ignore optional third ten.
            string core = sidc.Length >= SIDCLength ? sidc.Substring(0, SIDCLength) : sidc;

            try
            {
                result.Raw       = core;
                result.Context   = ParseContext(Digit(core, 2));
                result.Affiliation = ParseAffiliation(Digit(core, 3));
                result.SymbolSet = ParseSymbolSet(int.Parse(core.Substring(4, 2)));
                result.Status    = ParseStatus(Digit(core, 6));
                result.HqTfDummy = ParseHqTfDummy(Digit(core, 7));
                result.Echelon   = ParseEchelon(int.Parse(core.Substring(8, 2)));
                result.EntityCode    = int.Parse(core.Substring(10, 2));
                result.EntityType    = int.Parse(core.Substring(12, 2));
                result.EntitySubtype = int.Parse(core.Substring(14, 2));
                result.Modifier1     = int.Parse(core.Substring(16, 2));
                result.Modifier2     = int.Parse(core.Substring(18, 2));

                result.Designation      = string.Empty;
                result.HigherFormation  = string.Empty;
                result.StrengthLabel    = string.Empty;
                result.StrengthModifier = StrengthModifier.None;

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SIDCParser] Failed to parse SIDC '{sidc}': {ex.Message}");
                result = default;
                return false;
            }
        }

        public static SIDCCode Parse(string sidc)
        {
            if (TryParse(sidc, out SIDCCode result))
                return result;
            throw new ArgumentException($"Invalid SIDC string: '{sidc}'");
        }

        /// <summary>
        /// Serialises a SIDCCode back to its canonical 20-character string.
        /// </summary>
        public static string Serialise(SIDCCode code)
        {
            if (!string.IsNullOrEmpty(code.Raw) && code.Raw.Length == SIDCLength)
                return code.Raw;

            return string.Format(
                "10{0}{1}{2:D2}{3}{4}{5:D2}{6:D2}{7:D2}{8:D2}{9:D2}{10:D2}",
                (int)code.Context,
                (int)code.Affiliation,
                (int)code.SymbolSet,
                (int)code.Status,
                (int)code.HqTfDummy,
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

        private static char Digit(string sidc, int index) => sidc[index];

        private static SymbolContext ParseContext(char c)
        {
            if (int.TryParse(c.ToString(), out int val) && Enum.IsDefined(typeof(SymbolContext), val))
                return (SymbolContext)val;

            Debug.LogWarning($"[SIDCParser] Unknown context '{c}', defaulting to Reality.");
            return SymbolContext.Reality;
        }

        private static Affiliation ParseAffiliation(char c)
        {
            if (int.TryParse(c.ToString(), out int val) && Enum.IsDefined(typeof(Affiliation), val))
                return (Affiliation)val;

            // Letter codes sometimes appear in legacy / hand-drawn contexts.
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

        private static SymbolSet ParseSymbolSet(int code)
        {
            if (Enum.IsDefined(typeof(SymbolSet), code))
                return (SymbolSet)code;

            Debug.LogWarning($"[SIDCParser] Unknown symbol set {code}, defaulting to LandUnit.");
            return SymbolSet.LandUnit;
        }

        private static UnitStatus ParseStatus(char c)
        {
            if (int.TryParse(c.ToString(), out int val) && Enum.IsDefined(typeof(UnitStatus), val))
                return (UnitStatus)val;
            return UnitStatus.Present;
        }

        private static HeadquartersTaskForceDummy ParseHqTfDummy(char c)
        {
            // Table A-7: 0=none, 1=feint, 2=HQ, 3=feint+HQ, 4=TF, 5=feint+TF, 6=TF+HQ, 7=all
            if (int.TryParse(c.ToString(), out int val)
                && Enum.IsDefined(typeof(HeadquartersTaskForceDummy), val))
                return (HeadquartersTaskForceDummy)val;

            return HeadquartersTaskForceDummy.None;
        }

        private static Echelon ParseEchelon(int code)
        {
            if (code == 0) return Echelon.None;
            if (Enum.IsDefined(typeof(Echelon), code))
                return (Echelon)code;

            Debug.LogWarning($"[SIDCParser] Unknown echelon/amplifier code {code}, defaulting to None.");
            return Echelon.None;
        }
    }
}
