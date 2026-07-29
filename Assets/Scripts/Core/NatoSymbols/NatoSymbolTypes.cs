// NatoSymbolTypes.cs
// Enumerations, structs, and data types for the NATO APP-6D symbol system.
// SIDC field layout and identity codes follow APP-6(D) Annex A.

using System;
using UnityEngine;

namespace Strategos.NatoSymbols
{
    // -------------------------------------------------------------------------
    // Context (SIDC digit 3 — first half of Standard Identity field)
    // -------------------------------------------------------------------------
    public enum SymbolContext
    {
        Reality    = 0,
        Exercise   = 1, // US use only within APP-6(D)
        Simulation = 2, // US use only within APP-6(D)
    }

    // -------------------------------------------------------------------------
    // Standard Identity (SIDC digit 4 — Table A-2)
    // -------------------------------------------------------------------------
    public enum Affiliation
    {
        Pending       = 0,
        Unknown       = 1,
        AssumedFriend = 2,
        Friend        = 3,
        Neutral       = 4,
        Suspect       = 5, // Also Joker (exercise)
        Hostile       = 6, // Also Faker (exercise)
    }

    // -------------------------------------------------------------------------
    // Standard Identity Group — drives frame shape (Table A-3)
    // -------------------------------------------------------------------------
    public enum IdentityGroup
    {
        Unknown = 0,
        Friend  = 1,
        Neutral = 2,
        Hostile = 3,
    }

    // -------------------------------------------------------------------------
    // Symbol Set (SIDC digits 5–6 — Table A-4)
    // -------------------------------------------------------------------------
    public enum SymbolSet
    {
        Unknown              = 00,
        Air                  = 01,
        AirMissile           = 02,
        Space                = 05,
        SpaceMissile         = 06,
        LandUnit             = 10,
        LandCivilian         = 11,
        LandEquipment        = 15,
        LandInstallation     = 20,
        ControlMeasure       = 25,
        DismountedIndividual = 27,
        SeaSurface           = 30,
        SeaSubsurface        = 35,
        MineWarfare          = 36,
        ActivityEvent        = 40,
        Atmospheric          = 45,
        CyberspaceSpace      = 60,
        CyberspaceAir        = 61,
        CyberspaceLand       = 62,
        CyberspaceSurface    = 63,
        CyberspaceSubsurface = 64,
    }

    /// <summary>
    /// Legacy alias for SymbolSet used by older call sites / database lookups.
    /// Prefer SymbolSet going forward.
    /// </summary>
    public enum SymbolDimension
    {
        Activity    = 00,
        Air         = 01,
        Space       = 05,
        Land        = 10,
        Sea         = 30,
        Subsurface  = 35,
        Cyberspace  = 60,
    }

    // -------------------------------------------------------------------------
    // Unit Status (SIDC digit 7 — Table A-6)
    // -------------------------------------------------------------------------
    public enum UnitStatus
    {
        Present                 = 0, // Solid frame
        AnticipatedPlanned      = 1, // Dashed frame
        PresentFullyCapable     = 2,
        PresentDamaged          = 3,
        PresentDestroyed        = 4,
        PresentFullToCapacity   = 5,
    }

    // -------------------------------------------------------------------------
    // HQ / Task Force / Feint (SIDC digit 8 — Table A-7)
    // -------------------------------------------------------------------------
    public enum HeadquartersTaskForceDummy
    {
        None                         = 0,
        FeintDummy                   = 1,
        Headquarters                 = 2,
        FeintDummyHeadquarters       = 3,
        TaskForce                    = 4,
        FeintDummyTaskForce          = 5,
        TaskForceHeadquarters        = 6,
        FeintDummyTaskForceHeadquarters = 7,
    }

    [Flags]
    public enum SymbolModifierFlag
    {
        None         = 0,
        Headquarters = 1 << 0,
        TaskForce    = 1 << 1,
        FeintDummy   = 1 << 2,
    }

    // -------------------------------------------------------------------------
    // Echelon / Amplifier (SIDC digits 9–10 — Table A-8)
    // -------------------------------------------------------------------------
    public enum Echelon
    {
        None        = 00,
        Team        = 11, // ○
        Squad       = 12, // •
        Section     = 13, // ••
        Platoon     = 14, // •••
        Company     = 15, // •••
        Battalion   = 16, // I
        Regiment    = 17, // II
        Brigade     = 18, // X
        Division    = 21, // XX
        Corps       = 22, // XXX
        Army        = 23, // XXXX
        ArmyGroup   = 24, // XXXXX
        Theater     = 25, // XXXXXX
        Command     = 26,
    }

    // -------------------------------------------------------------------------
    // Land unit entity codes (SIDC digits 11–12, Symbol Set = Land Unit)
    // -------------------------------------------------------------------------
    public enum LandEntityCode
    {
        Unknown              = 00,
        Aviation             = 11,
        Infantry             = 12,
        Artillery            = 13,
        CombatEngineering    = 14,
        AirDefense           = 15,
        Armor                = 16,
        SignalsCommunication = 18,
        Reconnaissance       = 19,
        LogisticsSupport     = 20,
        Medical              = 21,
        Headquarters         = 25,
        SpecialOperations    = 30,
        MissileBallistic     = 35,
        Cyber                = 40,
    }

    // -------------------------------------------------------------------------
    // Strength modifier (text Field F — not encoded in base SIDC)
    // -------------------------------------------------------------------------
    public enum StrengthModifier
    {
        None       = 0,
        Reinforced = 1, // +
        Reduced    = 2, // -
        ReinforcedReduced = 3, // ±
    }

    // -------------------------------------------------------------------------
    // Affiliation colour palette (APP-6D Table 1-8, computer-generated)
    // -------------------------------------------------------------------------
    public static class AffiliationColour
    {
        public static readonly Color Friend  = new Color(0.502f, 0.878f, 1.000f); // #80E0FF Crystal Blue
        public static readonly Color Hostile = new Color(1.000f, 0.502f, 0.502f); // #FF8080 Salmon
        public static readonly Color Neutral = new Color(0.667f, 1.000f, 0.667f); // #AAFFAA Bamboo Green
        public static readonly Color Unknown = new Color(1.000f, 1.000f, 0.502f); // #FFFF80 Light Yellow
        public static readonly Color Pending = Color.white;

        public static Color ForAffiliation(Affiliation a)
        {
            switch (a)
            {
                case Affiliation.Friend:
                case Affiliation.AssumedFriend:
                    return Friend;
                case Affiliation.Hostile:
                case Affiliation.Suspect:
                    return Hostile;
                case Affiliation.Neutral:
                    return Neutral;
                case Affiliation.Unknown:
                    return Unknown;
                default:
                    return Pending;
            }
        }

        public static IdentityGroup GroupFor(Affiliation a)
        {
            switch (a)
            {
                case Affiliation.Friend:
                case Affiliation.AssumedFriend:
                    return IdentityGroup.Friend;
                case Affiliation.Neutral:
                    return IdentityGroup.Neutral;
                case Affiliation.Hostile:
                case Affiliation.Suspect:
                    return IdentityGroup.Hostile;
                default:
                    return IdentityGroup.Unknown;
            }
        }
    }

    // -------------------------------------------------------------------------
    // SIDCCode — fully parsed symbol identity (APP-6D Annex A)
    // -------------------------------------------------------------------------
    [Serializable]
    public struct SIDCCode : IEquatable<SIDCCode>
    {
        [Tooltip("Raw 20-character APP-6D SIDC string (source of truth). Optional third ten ignored.")]
        public string Raw;

        public SymbolContext Context;
        public Affiliation   Affiliation;
        public SymbolSet     SymbolSet;
        public UnitStatus    Status;
        public HeadquartersTaskForceDummy HqTfDummy;
        public Echelon       Echelon;
        public int           EntityCode;     // digits 11–12
        public int           EntityType;     // digits 13–14
        public int           EntitySubtype;  // digits 15–16
        public int           Modifier1;      // digits 17–18 sector 1
        public int           Modifier2;      // digits 19–20 sector 2

        // Text amplifiers (not part of SIDC digits)
        public string           Designation;     // Field T
        public string           HigherFormation; // Field M
        public string           StrengthLabel;
        public StrengthModifier StrengthModifier; // Field F

        /// <summary>Legacy alias — Land Unit maps to SymbolDimension.Land.</summary>
        public SymbolDimension Dimension
        {
            get
            {
                switch (SymbolSet)
                {
                    case SymbolSet.Air:
                    case SymbolSet.AirMissile:
                        return SymbolDimension.Air;
                    case SymbolSet.Space:
                    case SymbolSet.SpaceMissile:
                        return SymbolDimension.Space;
                    case SymbolSet.SeaSurface:
                        return SymbolDimension.Sea;
                    case SymbolSet.SeaSubsurface:
                    case SymbolSet.MineWarfare:
                        return SymbolDimension.Subsurface;
                    case SymbolSet.CyberspaceSpace:
                    case SymbolSet.CyberspaceAir:
                    case SymbolSet.CyberspaceLand:
                    case SymbolSet.CyberspaceSurface:
                    case SymbolSet.CyberspaceSubsurface:
                        return SymbolDimension.Cyberspace;
                    case SymbolSet.ActivityEvent:
                        return SymbolDimension.Activity;
                    default:
                        return SymbolDimension.Land;
                }
            }
            set
            {
                switch (value)
                {
                    case SymbolDimension.Air:        SymbolSet = SymbolSet.Air; break;
                    case SymbolDimension.Space:      SymbolSet = SymbolSet.Space; break;
                    case SymbolDimension.Sea:        SymbolSet = SymbolSet.SeaSurface; break;
                    case SymbolDimension.Subsurface: SymbolSet = SymbolSet.SeaSubsurface; break;
                    case SymbolDimension.Cyberspace: SymbolSet = SymbolSet.CyberspaceLand; break;
                    case SymbolDimension.Activity:   SymbolSet = SymbolSet.ActivityEvent; break;
                    default:                        SymbolSet = SymbolSet.LandUnit; break;
                }
            }
        }

        public SymbolModifierFlag ModifierFlags
        {
            get
            {
                var flags = SymbolModifierFlag.None;
                switch (HqTfDummy)
                {
                    case HeadquartersTaskForceDummy.FeintDummy:
                        flags = SymbolModifierFlag.FeintDummy;
                        break;
                    case HeadquartersTaskForceDummy.Headquarters:
                        flags = SymbolModifierFlag.Headquarters;
                        break;
                    case HeadquartersTaskForceDummy.FeintDummyHeadquarters:
                        flags = SymbolModifierFlag.FeintDummy | SymbolModifierFlag.Headquarters;
                        break;
                    case HeadquartersTaskForceDummy.TaskForce:
                        flags = SymbolModifierFlag.TaskForce;
                        break;
                    case HeadquartersTaskForceDummy.FeintDummyTaskForce:
                        flags = SymbolModifierFlag.FeintDummy | SymbolModifierFlag.TaskForce;
                        break;
                    case HeadquartersTaskForceDummy.TaskForceHeadquarters:
                        flags = SymbolModifierFlag.TaskForce | SymbolModifierFlag.Headquarters;
                        break;
                    case HeadquartersTaskForceDummy.FeintDummyTaskForceHeadquarters:
                        flags = SymbolModifierFlag.FeintDummy | SymbolModifierFlag.TaskForce | SymbolModifierFlag.Headquarters;
                        break;
                }
                return flags;
            }
            set
            {
                bool feint = (value & SymbolModifierFlag.FeintDummy) != 0;
                bool hq    = (value & SymbolModifierFlag.Headquarters) != 0;
                bool tf    = (value & SymbolModifierFlag.TaskForce) != 0;
                int code = (feint ? 1 : 0) | (hq ? 2 : 0) | (tf ? 4 : 0);
                HqTfDummy = (HeadquartersTaskForceDummy)code;
            }
        }

        public IdentityGroup IdentityGroup => AffiliationColour.GroupFor(Affiliation);

        public bool IsHeadquarters => (ModifierFlags & SymbolModifierFlag.Headquarters) != 0;
        public bool IsTaskForce    => (ModifierFlags & SymbolModifierFlag.TaskForce)    != 0;
        public bool IsFeintDummy   => (ModifierFlags & SymbolModifierFlag.FeintDummy)   != 0;
        public bool IsPlanned      => Status == UnitStatus.AnticipatedPlanned;

        /// <summary>
        /// Uncertain identity uses dotted frame (Assumed Friend, Suspect, Pending).
        /// Status dashed/planned is not shown for these identities (APP-6D §1.2.1.c.3).
        /// </summary>
        public bool HasUncertainIdentity =>
            Affiliation == Affiliation.Pending
            || Affiliation == Affiliation.AssumedFriend
            || Affiliation == Affiliation.Suspect;

        public bool Equals(SIDCCode other) => Raw == other.Raw
            && Designation == other.Designation
            && HigherFormation == other.HigherFormation;

        public override bool Equals(object obj) => obj is SIDCCode other && Equals(other);
        public override int GetHashCode() => Raw?.GetHashCode() ?? 0;
        public override string ToString() => $"SIDC[{Raw}] {Designation}/{HigherFormation} {Echelon}";

        /// <summary>Canonical friend land infantry company (Annex A layout).</summary>
        public static readonly SIDCCode Empty = new SIDCCode { Raw = "10031000151211000000" };
    }

    // -------------------------------------------------------------------------
    // SymbolLayer — identifies which visual layer is being operated on
    // -------------------------------------------------------------------------
    public enum SymbolLayer
    {
        Frame       = 0,
        Icon        = 1,
        Modifier    = 2,
        Amplifier   = 3,
        Text        = 4,
    }

    public enum FrameLineStyle
    {
        Solid  = 0,
        Dashed = 1,
        Dotted = 2,
    }

    public enum IconPlacement
    {
        MainSector  = 0,
        FullFrame   = 1,
        FullOctagon = 2,
    }
}
