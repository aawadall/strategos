// UnitIds.cs
// Stable identifiers for units and sides.
//
// Typed rather than bare ints, because a scenario file, a command's addressee and a unit's
// parent are all "an int" and mixing them up is a silent bug rather than a compile error.
//
// TWO SHAPE DECISIONS, both deliberate:
//
//   1. The backing field is public and NOT readonly, because the project's serialisation
//      contract is public non-readonly instance fields — see ScenarioIO.FieldsOnlyResolver,
//      which skips readonly fields along with every computed property. Treat these as
//      immutable regardless; nothing should assign Value after construction.
//
//   2. There is no nullable form. "No parent" is the sentinel UnitId.None (Value 0) rather
//      than a UnitId?, so that default(UnitId) already means "none" — a freshly deserialised
//      unit with no parent reads correctly without ceremony, and use sites are spared
//      .HasValue / .Value at every step. Real ids start at 1.
//
//      NOTE: an earlier version of this comment justified the sentinel by claiming
//      JsonUtility cannot serialise Nullable<T>. That is true of JsonUtility but irrelevant
//      here — scenarios serialise through Newtonsoft (com.unity.nuget.newtonsoft-json,
//      already in the manifest), which handles nullables fine. The sentinel stands on the
//      reasoning above, not on a serialiser limitation.

using System;

namespace Strategos.Units
{
    /// <summary>Stable identity for a <see cref="UnitInstance"/>. 0 means "none".</summary>
    [Serializable]
    public struct UnitId : IEquatable<UnitId>
    {
        public int Value;

        public UnitId(int value) => Value = value;

        /// <summary>The absent id. Used for "no parent" — see the file header.</summary>
        public static UnitId None => new(0);

        /// <summary>False for <see cref="None"/>; real ids start at 1.</summary>
        public bool IsValid => Value != 0;

        public bool Equals(UnitId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is UnitId o && Equals(o);
        public override int GetHashCode() => Value;
        public override string ToString() => IsValid ? $"U{Value}" : "U-none";

        public static bool operator ==(UnitId a, UnitId b) => a.Value == b.Value;
        public static bool operator !=(UnitId a, UnitId b) => a.Value != b.Value;
    }

    /// <summary>Stable identity for a <see cref="Side"/>. 0 means "none".</summary>
    [Serializable]
    public struct SideId : IEquatable<SideId>
    {
        public int Value;

        public SideId(int value) => Value = value;

        public static SideId None => new(0);

        public bool IsValid => Value != 0;

        public bool Equals(SideId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SideId o && Equals(o);
        public override int GetHashCode() => Value;
        public override string ToString() => IsValid ? $"S{Value}" : "S-none";

        public static bool operator ==(SideId a, SideId b) => a.Value == b.Value;
        public static bool operator !=(SideId a, SideId b) => a.Value != b.Value;
    }
}
