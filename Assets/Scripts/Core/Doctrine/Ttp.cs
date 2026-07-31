// Ttp.cs
// A drill: the tactics, techniques and procedures a unit rehearses so a commander can order
// them with one code instead of describing them.
//
// TTP AND DOCTRINE TEMPLATE, NOT "PLAYBOOK". The military term is the one this project uses;
// see docs/phases.md 5.4, which already calls the eventual library a TTP library.
//
// PARAMETERISED, NOT BOUND. A drill names an action and leaves its target to the moment it is
// invoked. That is forced rather than chosen: a drill pinned to a grid reference is not a
// drill, it is an order, and could never be the reusable thing the code addresses. What binds
// at invocation — which unit, against what, on which ground — belongs to the palette (#53),
// not here.
//
// CODE-ADDRESSED, BECAUSE BREVITY IS THE POINT. A short code is what makes commanding at
// echelon playable rather than tedious: at company you tell three platoons where to go, at
// corps you cannot issue four hundred orders and something has to expand intent into
// subordinate action. Real doctrine numbers its battle drills for exactly this reason, and
// carries them over voice as brevity codes — which is also why they have to survive being
// misheard. See #62 on what happens when one is garbled.
//
// The library below is deliberately small and deliberately honest: several steps are doctrine
// the simulation cannot yet carry out, and they say so rather than being quietly omitted. A
// catalogue that hides its gaps is worse than one that shows them — the same rule the symbol
// library follows when it captions four entity codes FRAME ONLY.

using System.Collections.Generic;
using Strategos.Commands;

namespace Strategos.Doctrine
{
    /// <summary>One step of a drill.</summary>
    public readonly struct TtpStep
    {
        /// <summary>What the step is, as a subordinate would hear it.</summary>
        public readonly string Text;

        /// <summary>
        /// The command this step becomes when a drill is executed, or
        /// <see cref="CommandKind.None"/> for a step the simulation cannot yet carry out.
        /// </summary>
        /// <remarks>
        /// **None is a real answer here, not a gap to be tidied away.** Most of what a battle
        /// drill actually asks for — bound, report, seek cover, establish a base of fire — has
        /// no executor, because `MoveTo` and `Engage` are the only two world commands that
        /// exist. Recording that per step lets the binder mark which lines the engine can
        /// honour today, which is the clearest statement available of how much of doctrine is
        /// still presentation. Omitting those steps instead would make the drills look
        /// complete and the engine look finished.
        /// </remarks>
        public readonly CommandKind Kind;

        public TtpStep(string text, CommandKind kind = CommandKind.None)
        {
            Text = text;
            Kind = kind;
        }

        /// <summary>True when an executor exists for this step today.</summary>
        public bool IsMechanised => Kind != CommandKind.None;
    }

    /// <summary>
    /// A named, coded drill.
    /// </summary>
    /// <remarks>
    /// Settable properties rather than `init`: Unity's scripting profile does not define
    /// `System.Runtime.CompilerServices.IsExternalInit`, so an init-only setter does not
    /// compile at all here. Treat these as write-once anyway — nothing should mutate a drill
    /// after <see cref="TtpLibrary"/> has built it.
    /// </remarks>
    public sealed class Ttp
    {
        /// <summary>
        /// What a commander transmits. Short on purpose — this is the whole reason the type
        /// exists, and it is what a low-bandwidth channel carries (#46, #62).
        /// </summary>
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>One line. What it does, readable without the sequence.</summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// When this drill is *wrong*.
        /// </summary>
        /// <remarks>
        /// The field a plain list would omit and the one that stops a player firing a code
        /// blindly. A drill with no stated limit reads as always-correct, which is how a
        /// brevity code becomes a button rather than a decision.
        /// </remarks>
        public string NotWhen { get; set; } = string.Empty;

        /// <summary>The echelon this is rehearsed at. Informational until #36 lands.</summary>
        public string Echelon { get; set; } = string.Empty;

        public IReadOnlyList<TtpStep> Steps { get; set; } = System.Array.Empty<TtpStep>();

        /// <summary>How many steps the simulation can actually carry out today.</summary>
        public int MechanisedSteps
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Steps.Count; i++) if (Steps[i].IsMechanised) n++;
                return n;
            }
        }

        public override string ToString() => $"{Code} {Name} ({Steps.Count} steps)";
    }

    /// <summary>
    /// The shipped drills.
    /// </summary>
    /// <remarks>
    /// Hard-coded rather than loaded from Resources, because there is no authoring tool yet
    /// (Phase 5.4) and a JSON file nobody can edit in the app is a format decision made early
    /// and for no benefit. When the editor lands this becomes the default set that ships
    /// alongside whatever the player writes.
    ///
    /// Numbering follows the real convention — infantry battle drills are numbered, and a
    /// letter suffix marks a variant — because that is what makes the codes worth learning
    /// rather than an invented cipher.
    /// </remarks>
    public static class TtpLibrary
    {
        private static Ttp[] _all;

        public static IReadOnlyList<Ttp> All => _all ??= Build();

        public static Ttp Find(string code)
        {
            var all = All;
            for (int i = 0; i < all.Count; i++)
                if (string.Equals(all[i].Code, code, System.StringComparison.OrdinalIgnoreCase))
                    return all[i];
            return null;
        }

        private static Ttp[] Build() => new[]
        {
            new Ttp
            {
                Code = "1",
                Name = "Squad Attack",
                Echelon = "Squad",
                Summary = "Close with and destroy a position that has been located and fixed.",
                NotWhen = "The enemy is not yet fixed, or you are the smaller force.",
                Steps = new[]
                {
                    new TtpStep("Establish a base of fire"),
                    new TtpStep("Suppress the position", CommandKind.Engage),
                    new TtpStep("Manoeuvre the assault element", CommandKind.MoveTo),
                    new TtpStep("Assault through", CommandKind.Engage),
                    new TtpStep("Consolidate and reorganise", CommandKind.Hold),
                },
            },
            new Ttp
            {
                Code = "1A",
                Name = "Squad Attack, Supported",
                Echelon = "Squad",
                Summary = "As Drill 1, with suppression provided by a supporting element.",
                NotWhen = "No supporting fires are available or in range.",
                Steps = new[]
                {
                    new TtpStep("Call for supporting fire"),
                    new TtpStep("Confirm the fire is falling on the position"),
                    new TtpStep("Manoeuvre under it", CommandKind.MoveTo),
                    new TtpStep("Shift fire and assault", CommandKind.Engage),
                    new TtpStep("Consolidate and reorganise", CommandKind.Hold),
                },
            },
            new Ttp
            {
                Code = "2",
                Name = "React to Contact",
                Echelon = "Squad / Platoon",
                Summary = "Answer unexpected fire, find who is firing, and take the initiative.",
                NotWhen = "In the open with no cover within 50 m - break contact instead.",
                Steps = new[]
                {
                    new TtpStep("Return fire immediately", CommandKind.Engage),
                    new TtpStep("Take the nearest cover", CommandKind.MoveTo),
                    new TtpStep("Report contact to higher"),
                    new TtpStep("Locate and suppress", CommandKind.Engage),
                    new TtpStep("Assault or break contact"),
                },
            },
            new Ttp
            {
                Code = "3",
                Name = "Break Contact",
                Echelon = "Squad / Platoon",
                Summary = "Disengage from a fight you should not be having and open the distance.",
                NotWhen = "Decisively engaged at close range - breaking exposes your flank.",
                Steps = new[]
                {
                    new TtpStep("Suppress to buy movement", CommandKind.Engage),
                    new TtpStep("Bound back by element", CommandKind.MoveTo),
                    new TtpStep("Continue until out of contact", CommandKind.MoveTo),
                    new TtpStep("Report and reorganise"),
                },
            },
            new Ttp
            {
                Code = "4",
                Name = "React to Ambush",
                Echelon = "Squad / Platoon",
                Summary = "Answer a near ambush by assaulting through the killing zone.",
                NotWhen = "The ambush is far and covered - suppress and manoeuvre instead.",
                Steps = new[]
                {
                    new TtpStep("Return fire without waiting for an order", CommandKind.Engage),
                    new TtpStep("Assault through the near side", CommandKind.MoveTo),
                    new TtpStep("Clear and consolidate beyond it", CommandKind.Hold),
                    new TtpStep("Treat casualties and report"),
                },
            },
            new Ttp
            {
                Code = "6",
                Name = "Occupy a Battle Position",
                Echelon = "Platoon / Company",
                Summary = "Move onto ground and hold it, oriented on a likely approach.",
                NotWhen = "The ground is not yet cleared or is under observed fire.",
                Steps = new[]
                {
                    new TtpStep("Move to the position", CommandKind.MoveTo),
                    new TtpStep("Occupy and orient", CommandKind.Hold),
                    new TtpStep("Prepare fighting positions"),
                    new TtpStep("Report set"),
                },
            },
        };
    }
}
