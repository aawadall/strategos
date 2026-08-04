// SideObservation.cs
// #101: a side's knowledge at tick t as a fixed-shape float buffer.
//
// Separate from SideKnowledge (#100): that seam still hands ground-truth Units/Victory to
// ISidePolicy.Decide. This type is belief-correct for hostiles — contact slots come from
// report-derived LastSeen only — and is what a learned or scripted policy will eventually
// consume once the environment lifecycle (#104) wraps Encode.

using System;

namespace Strategos.Observation
{
    /// <summary>
    /// Fixed-length observation for one side. Layout is documented on
    /// <see cref="SideObservationEncoder"/>; unused slots are zero.
    /// </summary>
    public sealed class SideObservation
    {
        public const int MaxOwnUnits = 16;
        public const int MaxContacts = 16;
        public const int MaxObjectives = 8;

        public const int HeaderFloats = 2;
        public const int OwnUnitFloats = 5;      // alive, x, y, strength, posture
        public const int ContactFloats = 3;      // present, lastSeenX, lastSeenY
        public const int ObjectiveFloats = 3;    // ownerSide, x, y

        public const int Length =
            HeaderFloats +
            MaxOwnUnits * OwnUnitFloats +
            MaxContacts * ContactFloats +
            MaxObjectives * ObjectiveFloats;

        public const int OwnUnitsOffset = HeaderFloats;
        public const int ContactsOffset = OwnUnitsOffset + MaxOwnUnits * OwnUnitFloats;
        public const int ObjectivesOffset = ContactsOffset + MaxContacts * ContactFloats;

        /// <summary>The packed buffer. Length is always <see cref="Length"/>.</summary>
        public float[] Values { get; }

        public SideObservation(float[] values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Length != Length)
                throw new ArgumentException(
                    $"observation must be {Length} floats, got {values.Length}", nameof(values));
            Values = values;
        }

        /// <summary>True when every float matches <paramref name="other"/> exactly.</summary>
        public bool EqualsExact(SideObservation other)
        {
            if (other == null) return false;
            var a = Values;
            var b = other.Values;
            for (int i = 0; i < Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>Count of floats that differ; for probe diagnostics.</summary>
        public int DifferCount(SideObservation other)
        {
            if (other == null) return Length;
            int n = 0;
            var a = Values;
            var b = other.Values;
            for (int i = 0; i < Length; i++)
                if (a[i] != b[i]) n++;
            return n;
        }
    }
}
