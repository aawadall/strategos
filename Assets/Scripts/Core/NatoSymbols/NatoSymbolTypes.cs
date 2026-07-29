// NatoSymbolTypes.cs
// Enumerations, structs, and data types for the NATO APP-6D symbol system.
// See docs/nato-symbol-generator.md for the full design spec.

using System;
using UnityEngine;

namespace Strategos.NatoSymbols
{
    // -------------------------------------------------------------------------
    // Standard Identity / Affiliation (SIDC position 3)
    // -------------------------------------------------------------------------
    public enum Affiliation
    {
        Pending         = 0,
        Unknown         = 1,
        AssumedFriend   = 2,
        Friend          = 3,
        NeutralFriend   = 4,
        Neutral         = 5,
        Suspect         = 6,
        Hostile         = 7,
        Joker           = 8,    // Exercise
        Faker           = 9,    // Exercise
        ExercisePending = 10,
        ExerciseUnknown = 11,
    }

    // -------------------------------------------------------------------------
    // Symbol Set / Dimension (SIDC positions 4–5)
    // -------------------------------------------------------------------------
    public enum SymbolDimension
    {
        Land            = 10,
        Air             = 01,
        Space           = 05,
        Sea             = 30,
        Subsurface      = 35,
        Cyberspace      = 60,
        Activity        = 00,
    }

    // -------------------------------------------------------------------------
    // Unit Status (SIDC position 6)
    // -------------------------------------------------------------------------
    public enum UnitStatus
    {
        Present             = 0,    // Solid frame
        AnticipatedPlanned  = 1,    // Dashed frame
    }

    // -------------------------------------------------------------------------
    // HQ / Task Force / Feint indicator (SIDC position 7)
    // -------------------------------------------------------------------------
    [Flags]
    public enum SymbolModifierFlag
    {
        None            = 0,
        Headquarters    = 1 << 0,   // HQ line below symbol
        TaskForce       = 1 << 1,   // Bracket above symbol
        FeintDummy      = 1 << 2,   // Diagonal feint line
    }

    // -------------------------------------------------------------------------
    // Echelon (SIDC positions 8–9)
    // -------------------------------------------------------------------------
    public enum Echelon
    {
        None        = 00,
        Team        = 11,   // ○
        Squad       = 12,   // •
        Section     = 13,   // ••
        Platoon     = 14,   // •••
        Company     = 15,   // •••  (alternate)
        Battalion   = 16,   // I
        Regiment    = 17,   // II
        Brigade     = 18,   // X
        Division    = 21,   // XX
        Corps       = 22,   // XXX
        Army        = 23,   // XXXX
        ArmyGroup   = 24,   // XXXXX
        Theater     = 25,   // XXXXXX
    }

    // -------------------------------------------------------------------------
    // Land unit entity codes (SIDC positions 10–11, Symbol Set = Land)
    // -------------------------------------------------------------------------
    public enum LandEntityCode
    {
        Unknown             = 00,
        Infantry            = 12,
        Armor               = 16,
        Artillery           = 13,
        AirDefense          = 15,
        Aviation            = 11,
        CombatEngineering   = 14,
        SignalsCommunication = 18,
        Reconnaissance      = 19,
        LogisticsSupport    = 20,
        Medical             = 21,
        Headquarters        = 25,
        SpecialOperations   = 30,
        MissileBallistic    = 35,
        Cyber               = 40,
    }

    // -------------------------------------------------------------------------
    // Strength modifier (text field, not directly in SIDC)
    // -------------------------------------------------------------------------
    public enum StrengthModifier
    {
        None        = 0,
        Reinforced  = 1,    // + suffix
        Reduced     = 2,    // - suffix
    }

    // -------------------------------------------------------------------------
    // Affiliation colour palette (APP-6D standard)
    // -------------------------------------------------------------------------
    public static class AffiliationColour
    {
        public static readonly Color Friend        = new Color(0.502f, 0.878f, 1.000f); // #80E0FF
        public static readonly Color Hostile       = new Color(1.000f, 0.502f, 0.502f); // #FF8080
        public static readonly Color Neutral       = new Color(0.667f, 1.000f, 0.667f); // #AAFFAA
        public static readonly Color Unknown       = new Color(1.000f, 1.000f, 0.502f); // #FFFF80
        public static readonly Color Pending       = Color.white;

        public static Color ForAffiliation(Affiliation a)
        {
            switch (a)
            {
                case Affiliation.Friend:
                case Affiliation.AssumedFriend:
                case Affiliation.NeutralFriend:
                    return Friend;
                case Affiliation.Hostile:
                case Affiliation.Suspect:
                case Affiliation.Faker:
                    return Hostile;
                case Affiliation.Neutral:
                    return Neutral;
                case Affiliation.Unknown:
                case Affiliation.Joker:
                    return Unknown;
                default:
                    return Pending;
            }
        }
    }

    // -------------------------------------------------------------------------
    // SIDCCode — fully parsed symbol identity
    // -------------------------------------------------------------------------
    [Serializable]
    public struct SIDCCode : IEquatable<SIDCCode>
    {
        [Tooltip("Raw 20-character APP-6D SIDC string (source of truth).")]
        public string Raw;

        public Affiliation      Affiliation;
        public SymbolDimension  Dimension;
        public UnitStatus       Status;
        public SymbolModifierFlag ModifierFlags;
        public Echelon          Echelon;
        public int              EntityCode;     // 2-digit entity
        public int              EntityType;     // 2-digit entity type
        public int              EntitySubtype;  // 2-digit entity subtype
        public int              Modifier1;      // sector 1
        public int              Modifier2;      // sector 2

        // Text fields (not part of SIDC, stored separately on UnitInstance)
        public string           Designation;    // e.g. "1-7 IN"
        public string           HigherFormation; // e.g. "3 ID"
        public string           StrengthLabel;  // e.g. "850" personnel
        public StrengthModifier StrengthModifier;

        public bool IsHeadquarters  => (ModifierFlags & SymbolModifierFlag.Headquarters) != 0;
        public bool IsTaskForce     => (ModifierFlags & SymbolModifierFlag.TaskForce)    != 0;
        public bool IsFeintDummy    => (ModifierFlags & SymbolModifierFlag.FeintDummy)   != 0;
        public bool IsPlanned       => Status == UnitStatus.AnticipatedPlanned;

        public bool Equals(SIDCCode other) => Raw == other.Raw
            && Designation == other.Designation
            && HigherFormation == other.HigherFormation;

        public override bool Equals(object obj) => obj is SIDCCode other && Equals(other);
        public override int GetHashCode() => Raw?.GetHashCode() ?? 0;
        public override string ToString() => $"SIDC[{Raw}] {Designation}/{HigherFormation} {Echelon}";

        public static readonly SIDCCode Empty = new SIDCCode { Raw = "10031500001211000000" };
    }

    // -------------------------------------------------------------------------
    // SymbolLayer — identifies which visual layer is being operated on
    // -------------------------------------------------------------------------
    public enum SymbolLayer
    {
        Frame       = 0,
        Icon        = 1,
        Echelon     = 2,
        Modifier    = 3,
        Text        = 4,
    }
}
