// CareerProfile.cs
// #109 / #212: career fields that outlive one CampaignChain.
//
// Distinct from within-chain ORBAT carry-over (#75): that moves Strength/Training between
// operations of the *same* authored chain. This profile is what a player takes into a
// *second* campaign — rank (#76) plus the formation higher still addresses (#214).

using System;
using Strategos.Commands;
using Strategos.NatoSymbols;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Campaigns
{
    /// <summary>Persistent career seat across campaign boundaries.</summary>
    [Serializable]
    public sealed class CareerProfile
    {
        public int FormatVersion = 1;

        /// <summary>Id into <see cref="RankAuthorityIO"/> — same string PLAY already uses.</summary>
        public string CareerRankId = RankAuthorityDefaults.DefaultRankId;

        /// <summary>Designation of the formation the player last commanded.</summary>
        public string FormationDesignation = string.Empty;

        /// <summary>
        /// Who addresses the player from higher — stamped from the seat unit's
        /// <see cref="UnitInstance.HigherFormation"/>, and what a follow-on directive's
        /// <c>From</c> should still name (#214).
        /// </summary>
        public string HigherFormation = string.Empty;

        public static CareerProfile Default() => new();

        /// <summary>
        /// Copies formation labels from the player's seat unit on
        /// <paramref name="scenario"/>. Does not change <see cref="CareerRankId"/>.
        /// </summary>
        public void StampFormationFrom(Scenario scenario)
        {
            if (scenario == null || !scenario.PlayerSide.IsValid) return;

            var band = CommandScope.EffectivePlayerEchelon(scenario);
            UnitInstance seat = null;
            UnitInstance fallback = null;
            var fallbackEchelon = Echelon.None;

            for (int i = 0; i < scenario.Units.Count; i++)
            {
                var u = scenario.Units[i];
                if (u == null || u.Side != scenario.PlayerSide) continue;

                var e = u.ToSidcCode().Echelon;
                if (e == band)
                {
                    seat = u;
                    break;
                }

                if ((int)e > (int)fallbackEchelon)
                {
                    fallbackEchelon = e;
                    fallback = u;
                }
            }

            seat ??= fallback;
            if (seat == null) return;

            FormationDesignation = seat.Designation ?? string.Empty;
            HigherFormation = seat.HigherFormation ?? string.Empty;
        }
    }
}
