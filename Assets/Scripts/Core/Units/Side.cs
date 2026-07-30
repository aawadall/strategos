// Side.cs
// One party in a scenario. A scenario has two or more.
//
// A side is not the same thing as an affiliation. Affiliation is how a symbol is *drawn*
// (friend blue rectangle, hostile red diamond) and is a property of the SIDC. A side is who
// is playing. Two allied national contingents are distinct sides that both draw as Friend;
// a three-way scenario has three sides drawn from four affiliations. Keeping them separate
// now is what makes coalitions possible later without reworking every unit.

using System;
using UnityEngine;
using Strategos.NatoSymbols;

namespace Strategos.Units
{
    [Serializable]
    public class Side
    {
        public SideId Id;

        /// <summary>Display name, e.g. "BLUFOR" or "3rd Shock Army".</summary>
        public string Name = "Side";

        /// <summary>How this side's units are framed. Drives frame shape and colour.</summary>
        public Affiliation Affiliation = Affiliation.Friend;

        /// <summary>
        /// Display colour for side-coloured chrome — legends, selection, arrows.
        ///
        /// Held separately from <see cref="Affiliation"/> rather than derived from it,
        /// because two sides can share an affiliation and still need telling apart.
        /// <see cref="DefaultColour"/> is the sensible starting value.
        /// </summary>
        public Color Colour = Color.white;

        public Side() { }

        public Side(SideId id, string name, Affiliation affiliation)
        {
            Id = id;
            Name = name;
            Affiliation = affiliation;
            Colour = DefaultColour(affiliation);
        }

        /// <summary>
        /// The APP-6D colour for an affiliation, from the shared
        /// <see cref="AffiliationColour"/> table so sides and symbols agree.
        /// </summary>
        public static Color DefaultColour(Affiliation affiliation) =>
            AffiliationColour.ForAffiliation(affiliation);

        public override string ToString() => $"{Id} {Name} ({Affiliation})";
    }
}
