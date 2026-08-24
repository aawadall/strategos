// CareerProfile.cs
// #109 / #212: career fields that outlive one CampaignChain.
//
// Distinct from within-chain ORBAT carry-over (#75): that moves Strength/Training between
// operations of the *same* authored chain. This profile is what a player takes into a
// *second* campaign — rank (#76) plus the formation higher still addresses (#214).

using System;
using System.Collections.Generic;
using Strategos.Commands;
using Strategos.Medals;
using Strategos.NatoSymbols;
using Strategos.Scenarios;
using Strategos.Units;

namespace Strategos.Campaigns
{
    /// <summary>
    /// One finished campaign chain, recorded at the rank and formation the player held
    /// when it ended — a career page reads this list to show where a player has been,
    /// distinct from <see cref="CampaignChainEntry.Outcome"/> which lives only for the
    /// duration of one loaded <see cref="CampaignChain"/>.
    /// </summary>
    [Serializable]
    public sealed class CareerCampaignRecord
    {
        public string ChainName = string.Empty;
        public OperationOutcome Outcome = OperationOutcome.Unplayed;
        public string CareerRankId = string.Empty;
        public string FormationDesignation = string.Empty;
    }

    /// <summary>
    /// One bar medal on the career rack (#467 W07) — the rack shows this, not the
    /// per-fight <see cref="BarMedalAward"/>, so a numeral Merit medal reads as a career
    /// total rather than resetting every time <see cref="PostBattleReviewer"/> runs.
    /// </summary>
    [Serializable]
    public sealed class CareerEarnedMedal
    {
        public string MedalId = string.Empty;
        public int Count = 1;
        public string FirstScenarioName = string.Empty;
    }

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

        /// <summary>
        /// Finished campaign chains, oldest first. Appended by
        /// <see cref="RecordCampaignCompletion"/> when a chain's last operation is decided —
        /// never mutated afterward, so it is a true log, not projected state.
        /// </summary>
        public List<CareerCampaignRecord> History = new();

        /// <summary>
        /// Bar medals earned across every fight this career has finished, one entry per
        /// <see cref="BarMedalDef.Id"/> — see <see cref="GrantMedals"/>.
        /// </summary>
        public List<CareerEarnedMedal> EarnedMedals = new();

        public static CareerProfile Default() => new();

        /// <summary>
        /// Folds one decision's awards into the career rack (#467 W07). Idempotent per
        /// <see cref="BarMedalAward.MedalId"/>: an Id already on the rack does not add a
        /// second entry, its <see cref="CareerEarnedMedal.Count"/> accumulates instead.
        /// Call once per decision — see <c>PlayView.ShowOutcome</c>.
        /// </summary>
        public void GrantMedals(IEnumerable<BarMedalAward> awards)
        {
            if (awards == null) return;
            foreach (var award in awards)
            {
                if (award == null || string.IsNullOrEmpty(award.MedalId)) continue;
                int count = award.Count > 1 ? award.Count : 1;

                CareerEarnedMedal existing = null;
                for (int i = 0; i < EarnedMedals.Count; i++)
                {
                    if (EarnedMedals[i].MedalId == award.MedalId)
                    {
                        existing = EarnedMedals[i];
                        break;
                    }
                }

                if (existing != null)
                {
                    existing.Count += count;
                }
                else
                {
                    EarnedMedals.Add(new CareerEarnedMedal
                    {
                        MedalId = award.MedalId,
                        Count = count,
                        FirstScenarioName = award.ScenarioName ?? string.Empty,
                    });
                }
            }
        }

        /// <summary>
        /// Appends one finished-chain record at the career's current rank and formation.
        /// Call once, when a chain's last operation is decided — see
        /// <c>PlayView.ShowOutcome</c>.
        /// </summary>
        public void RecordCampaignCompletion(string chainName, OperationOutcome outcome)
        {
            History.Add(new CareerCampaignRecord
            {
                ChainName = chainName ?? string.Empty,
                Outcome = outcome,
                CareerRankId = CareerRankId,
                FormationDesignation = FormationDesignation,
            });
        }

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
