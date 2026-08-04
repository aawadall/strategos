// DisplayNames.cs
// Human-readable names and curated option orderings for the SIDC fields and map enums.
//
// These are UI orderings, not domain data, which is why they live here and not in
// Strategos.NatoSymbols: the order is "what a person wants to see first", the subsets
// are "what actually renders today", and both change for presentation reasons that the
// symbol core should not know about.
//
// GLYPH SAFETY: the bundled LiberationSans SDF atlas has no geometric-shape glyphs.
// '○' U+25CB, '•' U+2022 and '−' U+2212 render as tofu boxes, and '–' U+2013 renders as
// *nothing at all*. Everything below is Latin-1: 'o', '·' U+00B7, '±', '+', '-'.

using System;
using Strategos.Maps;
using Strategos.NatoSymbols;

namespace Strategos.UI
{
    public static class DisplayNames
    {
        // ─── Curated option orderings ─────────────────────────────────────────
        // Each array is the set a picker offers, in the order it offers them.

        public static readonly Affiliation[] Affiliations =
        {
            Affiliation.Friend, Affiliation.Hostile, Affiliation.Neutral, Affiliation.Unknown,
            Affiliation.AssumedFriend, Affiliation.Suspect, Affiliation.Pending,
        };

        public static readonly Echelon[] Echelons =
        {
            Echelon.Team, Echelon.Squad, Echelon.Section, Echelon.Platoon,
            Echelon.Company, Echelon.Battalion, Echelon.Regiment, Echelon.Brigade,
            Echelon.Division, Echelon.Corps, Echelon.Army, Echelon.ArmyGroup,
            Echelon.Theater, Echelon.Command,
        };

        /// <summary>
        /// Land entities that resolve an icon, plus SpecialOperations and
        /// MissileBallistic which do not — IconDecorator.ResolveLandIcon handles 11 of
        /// the 14 LandEntityCode values and the rest fall through to a bare frame. They
        /// are listed on purpose: a picker that hides them hides the gap.
        /// </summary>
        public static readonly LandEntityCode[] UnitTypes =
        {
            LandEntityCode.Infantry, LandEntityCode.Armor, LandEntityCode.Artillery,
            LandEntityCode.Reconnaissance, LandEntityCode.CombatEngineering,
            LandEntityCode.AirDefense, LandEntityCode.Aviation,
            LandEntityCode.SignalsCommunication, LandEntityCode.LogisticsSupport,
            LandEntityCode.Medical, LandEntityCode.Headquarters,
            LandEntityCode.SpecialOperations, LandEntityCode.MissileBallistic,
        };

        /// <summary>
        /// Entity-type variants. Project stubs, not APP-6D Annex A values — in real
        /// APP-6D mobility belongs in the sector modifier, not the entity type.
        /// </summary>
        public static readonly (string label, int code)[] Variants =
        {
            ("Standard / Foot", IconDecorator.VarStandard),
            ("Mechanized",      IconDecorator.VarMechanized),
            ("Motorized",       IconDecorator.VarMotorized),
            ("Air Assault",     IconDecorator.VarAirAssault),
            ("Amphibious",      IconDecorator.VarAmphibious),
            ("Mountain",        IconDecorator.VarMountain),
            ("Arctic",          IconDecorator.VarArctic),
            ("Heavy",           IconDecorator.VarHeavy),
            ("Light",           IconDecorator.VarLight),
        };

        /// <summary>Sector modifiers. Also project stubs pending the Annex A tables.</summary>
        public static readonly (string label, int code)[] SectorMods =
        {
            ("None", 0),
            ("Airborne",    SectorModifierDecorator.ModAirborne),
            ("Air Assault", SectorModifierDecorator.ModAirAssault),
            ("Wheeled",     SectorModifierDecorator.ModWheeled),
            ("Mountain",    SectorModifierDecorator.ModMountain),
            ("Amphibious",  SectorModifierDecorator.ModAmphibious),
        };

        public static readonly HeadquartersTaskForceDummy[] HqTf =
        {
            HeadquartersTaskForceDummy.None,
            HeadquartersTaskForceDummy.Headquarters,
            HeadquartersTaskForceDummy.TaskForce,
            HeadquartersTaskForceDummy.TaskForceHeadquarters,
            HeadquartersTaskForceDummy.FeintDummy,
            HeadquartersTaskForceDummy.FeintDummyHeadquarters,
            HeadquartersTaskForceDummy.FeintDummyTaskForce,
            HeadquartersTaskForceDummy.FeintDummyTaskForceHeadquarters,
        };

        public static readonly UnitStatus[] Statuses =
        {
            UnitStatus.Present, UnitStatus.AnticipatedPlanned,
            UnitStatus.PresentFullyCapable, UnitStatus.PresentDamaged,
            UnitStatus.PresentDestroyed, UnitStatus.PresentFullToCapacity,
        };

        public static readonly StrengthModifier[] StrengthMods =
        {
            StrengthModifier.None, StrengthModifier.Reinforced,
            StrengthModifier.Reduced, StrengthModifier.ReinforcedReduced,
        };

        public static readonly string[] StrengthModLabels =
        {
            "None", "Reinforced (+)", "Reduced (-)", "Reinforced & reduced (±)",
        };

        /// <summary>
        /// Relief profiles, Rolling first because it is the least surprising default.
        /// </summary>
        public static readonly ReliefProfile[] Profiles =
        {
            ReliefProfile.Rolling, ReliefProfile.Plains, ReliefProfile.Hills,
            ReliefProfile.Mountains, ReliefProfile.Coastal, ReliefProfile.Desert,
            ReliefProfile.Arctic,
        };

        /// <summary>
        /// Render modes, Schematic first: it is the operations-map look, where everything
        /// that is not a symbol or a control measure steps back. That is what an underlay
        /// behind a symbol wants.
        /// </summary>
        public static readonly MapRenderMode[] RenderModes =
        {
            MapRenderMode.Schematic, MapRenderMode.Topographic,
            MapRenderMode.Hybrid, MapRenderMode.Terrain,
            MapRenderMode.NatoTopo,
        };

        // ─── Labels ───────────────────────────────────────────────────────────

        /// <summary>CamelCase to spaced words: "AssumedFriend" -> "Assumed Friend".</summary>
        public static string Prettify(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var chars = new System.Collections.Generic.List<char> { s[0] };
            for (int i = 1; i < s.Length; i++)
            {
                if (char.IsUpper(s[i])) chars.Add(' ');
                chars.Add(s[i]);
            }
            return new string(chars.ToArray());
        }

        /// <summary>
        /// The echelon mark AmplifierDecorator.DrawEchelon actually draws above the frame.
        ///
        /// These were off by one before commit 2039fe0 — Company showed three dots,
        /// Battalion one bar — so do NOT "correct" them back. Company is one bar,
        /// Battalion two, Regiment three, and the X series starts at Brigade.
        /// </summary>
        public static string EchelonMark(Echelon e) => e switch
        {
            Echelon.Team      => "o",
            Echelon.Squad     => "·",
            Echelon.Section   => "··",
            Echelon.Platoon   => "···",
            Echelon.Company   => "I",
            Echelon.Battalion => "II",
            Echelon.Regiment  => "III",
            Echelon.Brigade   => "X",
            Echelon.Division  => "XX",
            Echelon.Corps     => "XXX",
            Echelon.Army      => "XXXX",
            Echelon.ArmyGroup => "XXXXX",
            Echelon.Theater   => "XXXXXX",
            Echelon.Command   => "++",
            _                 => string.Empty,
        };

        public static string EchelonName(Echelon e) => e switch
        {
            Echelon.Team      => "Team / Crew",
            Echelon.ArmyGroup => "Army Group",
            _                 => Prettify(e.ToString()),
        };

        /// <summary>Name and mark together, for a picker: "Company  I".</summary>
        public static string EchelonLabel(Echelon e) => $"{EchelonName(e)}  {EchelonMark(e)}";

        public static string StatusLabel(UnitStatus s) => s switch
        {
            UnitStatus.Present => "Present",
            UnitStatus.AnticipatedPlanned => "Planned",
            UnitStatus.PresentFullyCapable => "Fully capable",
            UnitStatus.PresentDamaged => "Damaged",
            UnitStatus.PresentDestroyed => "Destroyed",
            UnitStatus.PresentFullToCapacity => "Full capacity",
            _ => s.ToString(),
        };

        public static string UnitTypeLabel(int entityCode)
        {
            if (Enum.IsDefined(typeof(LandEntityCode), entityCode))
                return Prettify(((LandEntityCode)entityCode).ToString());
            return $"Entity {entityCode:D2}";
        }

        /// <summary>
        /// Whether <paramref name="entityCode"/> resolves an icon, or renders as a bare
        /// frame.
        ///
        /// IconDecorator.ResolveLandIcon handles 11 of the 14 LandEntityCode values;
        /// Unknown, SpecialOperations, MissileBallistic and Cyber fall through to its
        /// default and draw nothing inside the frame. A library that showed only the codes
        /// that work would hide that, so this exists to caption the gap rather than to
        /// filter it out. Keep in step with ResolveLandIcon.
        /// </summary>
        public static bool RendersIcon(int entityCode) => entityCode switch
        {
            (int)LandEntityCode.Unknown            => false,
            (int)LandEntityCode.SpecialOperations   => false,
            (int)LandEntityCode.MissileBallistic    => false,
            (int)LandEntityCode.Cyber               => false,
            _                                       => true,
        };

        public static string VariantLabel(int code)
        {
            foreach (var v in Variants)
                if (v.code == code) return v.label;
            return code.ToString("D2");
        }

        public static string SectorModLabel(int code)
        {
            if (code == 0) return "None";
            foreach (var m in SectorMods)
                if (m.code == code) return m.label;
            return code.ToString("D2");
        }

        public static string AffiliationLabel(Affiliation a) => Prettify(a.ToString());
        public static string HqTfLabel(HeadquartersTaskForceDummy h) => Prettify(h.ToString());
        public static string ProfileLabel(ReliefProfile p) => Prettify(p.ToString());
        public static string RenderModeLabel(MapRenderMode m) => m switch
        {
            MapRenderMode.NatoTopo => "US/NATO Topo",
            _ => Prettify(m.ToString()),
        };

        // ─── Convenience: label arrays for SetDrop ────────────────────────────

        public static string[] AffiliationLabels() => Array.ConvertAll(Affiliations, AffiliationLabel);
        public static string[] EchelonLabels()     => Array.ConvertAll(Echelons, EchelonLabel);
        public static string[] UnitTypeLabels()    => Array.ConvertAll(UnitTypes, u => Prettify(u.ToString()));
        public static string[] VariantLabels()     => Array.ConvertAll(Variants, v => v.label);
        public static string[] SectorModLabels()   => Array.ConvertAll(SectorMods, m => m.label);
        public static string[] HqTfLabels()        => Array.ConvertAll(HqTf, HqTfLabel);
        public static string[] StatusLabels()      => Array.ConvertAll(Statuses, StatusLabel);
        public static string[] ProfileLabels()     => Array.ConvertAll(Profiles, ProfileLabel);
        public static string[] RenderModeLabels()  => Array.ConvertAll(RenderModes, RenderModeLabel);
    }
}
