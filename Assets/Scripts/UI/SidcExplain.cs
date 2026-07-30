// SidcExplain.cs
// Prose explaining what each SIDC digit group means and what it makes the renderer draw.
//
// Extracted from SymbolBuilderPanel so the symbol library's inspector can show the same
// breakdown instead of growing a second copy of 140 lines of switch expressions.
//
// These describe what THIS project's decorators actually draw, not what APP-6D
// prescribes. Where the two differ the project is the stub — see the entity-type variant
// and sector-modifier notes in DisplayNames.

using Strategos.NatoSymbols;

namespace Strategos.UI
{
    public static class SidcExplain
    {
        /// <summary>
        /// APP-6(D) Annex A field layout of the 20-digit SIDC.
        /// Start/Len index into <see cref="SIDCCode.Raw"/>.
        /// </summary>
        public static readonly (string Pos, int Start, int Len, string Field)[] Fields =
        {
            ("1–2",   0, 2, "Version / standard"),
            ("3",     2, 1, "Context"),
            ("4",     3, 1, "Standard identity"),
            ("5–6",   4, 2, "Symbol set"),
            ("7",     6, 1, "Status / condition"),
            ("8",     7, 1, "HQ / TF / dummy"),
            ("9–10",  8, 2, "Echelon / mobility"),
            ("11–12",10, 2, "Entity"),
            ("13–14",12, 2, "Entity type"),
            ("15–16",14, 2, "Entity subtype"),
            ("17–18",16, 2, "Sector 1 modifier"),
            ("19–20",18, 2, "Sector 2 modifier"),
        };

        /// <summary>
        /// Text amplifiers are drawn beside the frame but are not encoded in the SIDC.
        /// </summary>
        public static readonly string[] AmplifierFields =
        {
            "Designation (T)", "Higher formation (M)", "Strength (F)",
        };

        /// <summary>What field <paramref name="index"/> of <see cref="Fields"/> means for this code.</summary>
        public static string FieldMeaning(int index, SIDCCode code) => index switch
        {
            0  => "APP-6(D) symbology version",
            1  => $"{code.Context} — {ContextMeaning(code.Context)}",
            2  => $"{code.Affiliation} — {FrameMeaning(code.Affiliation)}",
            3  => $"{DisplayNames.Prettify(code.SymbolSet.ToString())} (set {(int)code.SymbolSet:D2})",
            4  => $"{DisplayNames.StatusLabel(code.Status)} — {StatusMeaning(code.Status)}",
            5  => HqMeaning(code.HqTfDummy),
            6  => EchelonMeaning(code.Echelon),
            7  => $"{DisplayNames.UnitTypeLabel(code.EntityCode)} — main icon inside the frame",
            8  => $"{DisplayNames.VariantLabel(code.EntityType)} — {VariantMark(code)}",
            9  => code.EntitySubtype == 0
                    ? "Not used by this symbol"
                    : $"Subtype {code.EntitySubtype:D2}",
            10 => $"{DisplayNames.SectorModLabel(code.Modifier1)} — upper octagon sector",
            11 => $"{DisplayNames.SectorModLabel(code.Modifier2)} — lower octagon sector",
            _  => string.Empty,
        };

        /// <summary>Describes the mark IconDecorator actually draws for the variant.</summary>
        public static string VariantMark(SIDCCode code)
        {
            string mark = code.EntityType switch
            {
                IconDecorator.VarMechanized => "ellipse around the icon",
                IconDecorator.VarMotorized  => "wheels, lower sector",
                IconDecorator.VarAirAssault => "chevron, lower sector",
                IconDecorator.VarAmphibious => "waves, lower sector",
                IconDecorator.VarMountain   => "mountain, lower sector",
                IconDecorator.VarArctic     => "arch, lower sector",
                IconDecorator.VarHeavy      => "H, lower sector",
                IconDecorator.VarLight      => "L, lower sector",
                _                           => "no additional mark",
            };

            // The lower-sector mark yields to an explicit Sector 2 modifier.
            if (code.Modifier2 != 0 && code.EntityType != IconDecorator.VarMechanized
                && code.EntityType != IconDecorator.VarStandard)
                return mark + " (hidden — sector 2 in use)";

            return mark;
        }

        public static string ContextMeaning(SymbolContext c) => c switch
        {
            SymbolContext.Reality    => "live operational data",
            SymbolContext.Exercise   => "exercise track",
            SymbolContext.Simulation => "simulated track",
            _ => "context",
        };

        public static string FrameMeaning(Affiliation a) => a switch
        {
            Affiliation.Friend or Affiliation.AssumedFriend => "blue rectangle frame",
            Affiliation.Hostile or Affiliation.Suspect => "red diamond frame",
            Affiliation.Neutral => "green square frame",
            Affiliation.Unknown or Affiliation.Pending => "yellow ellipse frame",
            _ => "standard identity frame",
        };

        public static string StatusMeaning(UnitStatus s) => s switch
        {
            UnitStatus.Present => "solid frame, confirmed location, no bar",
            UnitStatus.AnticipatedPlanned => "dashed frame, planned / anticipated",
            UnitStatus.PresentDamaged => "amber condition bar below frame",
            UnitStatus.PresentDestroyed => "red condition bar below frame",
            UnitStatus.PresentFullyCapable => "green condition bar below frame",
            UnitStatus.PresentFullToCapacity => "blue condition bar below frame",
            _ => "status / condition amplifier",
        };

        public static string HqMeaning(HeadquartersTaskForceDummy h) => h switch
        {
            HeadquartersTaskForceDummy.None => "No HQ / TF / feint amplifiers",
            HeadquartersTaskForceDummy.Headquarters => "HQ staff line below the frame",
            HeadquartersTaskForceDummy.TaskForce => "Task-force bracket above the frame",
            HeadquartersTaskForceDummy.TaskForceHeadquarters => "HQ staff line + task-force bracket",
            HeadquartersTaskForceDummy.FeintDummy => "Feint / dummy dashed inverted V",
            _ => "Combined HQ / TF / feint graphic",
        };

        public static string EchelonMeaning(Echelon e) => e switch
        {
            Echelon.Team => "o  Team / crew",
            Echelon.Squad => "·  Squad",
            Echelon.Section => "··  Section",
            Echelon.Platoon => "···  Platoon",
            Echelon.Company => "I Company / battery / troop",
            Echelon.Battalion => "II Battalion / squadron",
            Echelon.Regiment => "III Regiment / group",
            Echelon.Brigade => "X Brigade",
            Echelon.Division => "XX Division",
            Echelon.Corps => "XXX Corps",
            Echelon.Army => "XXXX Army",
            Echelon.ArmyGroup => "XXXXX Army group / front",
            Echelon.Theater => "XXXXXX Theater",
            Echelon.Command => "++ Command",
            _ => "No echelon mark",
        };

        /// <summary>
        /// Strength as the Field F amplifier reads it. The percentage is a game
        /// amplifier with no APP-6D equivalent; only the + / - / ± marker is standard.
        /// </summary>
        public static string StrengthDisplay(SIDCCode code)
        {
            var s = string.IsNullOrEmpty(code.StrengthLabel) ? "—" : code.StrengthLabel + "%";
            return code.StrengthModifier switch
            {
                StrengthModifier.Reinforced => s + " (+)",
                StrengthModifier.Reduced => s + " (-)",
                StrengthModifier.ReinforcedReduced => s + " (±)",
                _ => s,
            };
        }
    }
}
