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

        /// <summary>
        /// Whether two sides are enemies. **The single place that decides**, so an alliance
        /// model replaces one method rather than a search.
        /// </summary>
        /// <remarks>
        /// The rule is "different side, different affiliation", which is a stand-in and not a
        /// model. It gets the coalition case right — two allied contingents are distinct sides
        /// that both draw as Friend, and this correctly treats them as allies — and it gets
        /// one case wrong: two mutually hostile factions that both draw as Hostile read as
        /// allies here, because nothing in the data says otherwise.
        ///
        /// Fixing that needs an actual alliance graph on the scenario, which is a Phase 3 item
        /// and not worth inventing for a two-sided sandbox. Until then a three-way scenario
        /// must give its factions distinct affiliations.
        /// </remarks>
        public static bool AreHostile(Side a, Side b)
        {
            if (a == null || b == null) return false;
            if (a.Id == b.Id) return false;
            return a.Affiliation != b.Affiliation;
        }

        public override string ToString() => $"{Id} {Name} ({Affiliation})";
    }
}
