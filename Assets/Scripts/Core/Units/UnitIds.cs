// UnitIds.cs
// Stable identifiers for units and sides.
//
// Typed rather than bare ints, because a scenario file, a command's addressee and a unit's
// parent are all "an int" and mixing them up is a silent bug rather than a compile error.
//
// TWO SERIALISATION COMPROMISES, both deliberate:
//
//   1. The backing field is public and not readonly. Unity's serialiser — which
//      Strategos.Scenarios will use for scenario JSON — does not serialise readonly fields.
//      Treat these as immutable regardless; nothing should assign Value after construction.
//
//   2. There is no nullable form. UnityEngine.JsonUtility does not serialise Nullable<T>, so
//      "no parent" is the sentinel UnitId.None (Value 0) rather than a UnitId?. Real ids
//      start at 1. Semantically None means null; it just survives a round trip.

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
