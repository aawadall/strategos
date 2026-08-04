// SideReward.cs
// #103: per-step reward for a side — terminal outcome plus potential-based shaping.
//
// WHY THIS SHAPING, NOT THE OTHER CANDIDATES
//
// Sparse ±1 at t=3600 is untrainable; the issue leaves the function open and names the risk:
// contact-gained rewards exposure, and raw hold-tick farming can reward camping that is not
// decisive. The chosen Φ uses only what already decides the shipped victory kinds:
//
//   Φ = w_obj × owned_objectives + w_force × force_advantage
//   r_step = Φ(s') − Φ(s)     (potential-based; preserves the terminal optimum in the
//                               Ng/Russell sense when the true return is the terminal signal)
//   r_terminal ∈ {+1, 0, −1}  on win / draw / loss when the episode is done
//
// Contact reports are deliberately absent — a policy must not be paid to be seen. Casualties
// enter only through RemainingFraction (DestroyEnemy's own denominator), not as a separate
// kill bounty. Hold-duration clocks are not shaped: HoldObjectives already requires ownership
// long enough to win, and paying per hold-tick would overweight sitting still.
//
// Not wired into Simulation.Step — that is #104's env lifecycle. Callers (probes, later env)
// Capture → Step → Capture.

using Strategos.Objectives;
using Strategos.Units;

namespace Strategos.Reward
{
    public static class SideReward
    {
        public const float TerminalWin = 1f;
        public const float TerminalLoss = -1f;
        public const float TerminalDraw = 0f;

        /// <summary>Weight of each owned objective inside Φ.</summary>
        public const float ObjectiveWeight = 0.05f;

        /// <summary>Weight of force advantage (−1..+1) inside Φ.</summary>
        public const float ForceWeight = 0.05f;

        /// <summary>Φ(s) for potential-based shaping.</summary>
        public static float Potential(in SideRewardSnapshot snapshot) =>
            ObjectiveWeight * snapshot.OwnedObjectives +
            ForceWeight * snapshot.ForceAdvantage;

        /// <summary>
        /// Reward for the transition <paramref name="previous"/> → <paramref name="current"/>.
        /// When <paramref name="episodeDone"/> and victory is decided, adds the terminal
        /// win/draw/loss term for <paramref name="side"/>.
        /// </summary>
        public static float Step(
            SideId side,
            in SideRewardSnapshot previous,
            in SideRewardSnapshot current,
            VictoryEvaluator victory,
            bool episodeDone)
        {
            float r = Potential(current) - Potential(previous);

            if (episodeDone && victory != null && victory.IsDecided)
            {
                var outcome = victory.Outcome;
                if (outcome.IsDraw) r += TerminalDraw;
                else if (outcome.Winner == side) r += TerminalWin;
                else r += TerminalLoss;
            }

            return r;
        }
    }
}
