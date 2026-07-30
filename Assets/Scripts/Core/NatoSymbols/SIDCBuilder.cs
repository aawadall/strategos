// SIDCBuilder.cs
// Builds a SIDCCode from its fields.
//
// This exists because SIDCParser.Serialise is not the inverse of TryParse: it returns
// code.Raw verbatim whenever Raw is already 20 characters, so mutating a field on a
// parsed SIDCCode and then serialising hands back the OLD string. Anything that composes
// a code from pickers must therefore build the raw digits itself — and must do it in one
// place, or the format string gets copied and the copies drift.

namespace Strategos.NatoSymbols
{
    public static class SIDCBuilder
    {
        /// <summary>
        /// Assembles the 20-digit code and parses it back so every derived property
        /// (IdentityGroup, IsHeadquarters, IsPlanned …) is populated from the digits
        /// rather than set by hand.
        /// </summary>
        /// <remarks>
        /// Digit layout, per SIDCParser.TryParse:
        /// 1-2 version, 3 context, 4 identity, 5-6 symbol set, 7 status, 8 HQ/TF/dummy,
        /// 9-10 echelon, 11-12 entity, 13-14 entity type, 15-16 subtype,
        /// 17-18 sector 1, 19-20 sector 2.
        /// </remarks>
        public static SIDCCode Build(
            Affiliation affiliation,
            Echelon echelon,
            int entityCode,
            int entityType,
            HeadquartersTaskForceDummy hqTfDummy = HeadquartersTaskForceDummy.None,
            UnitStatus status = UnitStatus.Present,
            int modifier1 = 0,
            int modifier2 = 0,
            int entitySubtype = 0,
            SymbolSet symbolSet = SymbolSet.LandUnit,
            SymbolContext context = SymbolContext.Reality)
        {
            string raw = string.Format(
                "10{0}{1}{2:D2}{3}{4}{5:D2}{6:D2}{7:D2}{8:D2}{9:D2}{10:D2}",
                (int)context,
                (int)affiliation,
                (int)symbolSet,
                (int)status,
                (int)hqTfDummy,
                (int)echelon,
                entityCode,
                entityType,
                entitySubtype,
                modifier1,
                modifier2);

            if (SIDCParser.TryParse(raw, out var code)) return code;

            // Parsing its own output should not fail, but a field out of range would do
            // it. Fall back to the fields as given so the caller still gets a usable
            // code rather than a default-constructed one.
            return new SIDCCode
            {
                Raw = raw,
                Context = context,
                Affiliation = affiliation,
                SymbolSet = symbolSet,
                Status = status,
                HqTfDummy = hqTfDummy,
                Echelon = echelon,
                EntityCode = entityCode,
                EntityType = entityType,
                EntitySubtype = entitySubtype,
                Modifier1 = modifier1,
                Modifier2 = modifier2,
            };
        }
    }
}
