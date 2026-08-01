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
// at invocation belongs to the palette (#53), not here.
//
// NOBODY AT THE BOTTOM WRITES THESE. A fire team leader executes drills and authors none of
// them; battle drills are standardised Army-wide precisely so a team leader can become a
// casualty mid-fight and the drill still runs. Units below battalion adapt through their SOP,
// and published TTP changes on an institutional cycle measured in months. That makes
// *authoring* an echelon-gated capability rather than a flat feature.
//
// THE NUMBERING IS REAL. Infantry battle drills are numbered, a letter suffix marks a variant,
// and the numbers below follow the FM 7-8 lineage rather than being invented. That is the
// whole reason the codes are worth learning instead of being an arbitrary cipher. Team-level
// material gets a separate T-series on purpose: numbered battle drills are squad and platoon
// tasks, and shoehorning fire-team drills into the same numbers would misrepresent both.

using System.Collections.Generic;
using UnityEngine;
using Strategos.Commands;

namespace Strategos.Doctrine
{
    /// <summary>
    /// The echelon a drill is rehearsed and executed at.
    /// </summary>
    /// <remarks>
    /// Ordered, so readiness can ask whether a unit is large enough: a platoon can run a squad
    /// drill because its squads do, and a squad cannot run a platoon attack. Comparing display
    /// strings could not answer that.
    /// </remarks>
    public enum DrillEchelon
    {
        Team = 0,
        Squad = 1,
        Platoon = 2,
        Company = 3,
    }

    /// <summary>Which body of material a drill belongs to.</summary>
    public enum DrillSeries
    {
        /// <summary>Fire-team tasks. The training end, and where a player starts.</summary>
        Team = 0,

        /// <summary>Numbered infantry battle drills. Squad and platoon tasks.</summary>
        BattleDrill = 1,
    }

    /// <summary>
    /// What kind of thing a step is, which is not the same question as which command it becomes.
    /// </summary>
    /// <remarks>
    /// The binder used to call every step without a `CommandKind` "no executor", and that was
    /// the wrong statement about two thirds of them. *Report contact to higher* does not need
    /// an executor — `ContactTracker` publishes it without being asked, so the engine already
    /// does it and marking it missing implied it could not. Separating the two makes the
    /// honest count: of 52 authored steps, 6 are genuinely unmodelled and 9 happen by
    /// themselves.
    /// </remarks>
    public enum StepNature
    {
        /// <summary>Becomes a command in the unit's queue.</summary>
        Command = 0,

        /// <summary>The simulation already does this — reporting, mostly. Nothing to issue.</summary>
        Inherent = 1,

        /// <summary>Wants a mechanic that does not exist. Room clearing, dispersal.</summary>
        Unmodelled = 2,
    }

    /// <summary>
    /// Where a step's command points, when the drill is invoked.
    /// </summary>
    /// <remarks>
    /// **A drill is parameterised and its steps are unbound** — "take the nearest cover" names
    /// no cell and "return fire" names no target, which is exactly what makes a drill reusable
    /// rather than an order. Binding needs something to bind *to*, and the only thing available
    /// without a tactical planner is the threat: where the enemy is.
    ///
    /// That is enough for most of doctrine, because most of doctrine is directional relative to
    /// contact — you assault toward it, you break away from it, you hold where you are. What it
    /// cannot express is ground chosen for its own qualities ("the reverse slope", "the treeline
    /// on the left"), which needs terrain reasoning and is Phase 8.
    /// </remarks>
    public enum StepBinding
    {
        /// <summary>No parameter needed. Defend, and anything acting in place.</summary>
        Here = 0,

        /// <summary>Fire at the threat.</summary>
        AtThreat = 1,

        /// <summary>Move toward it — assault, bound forward, close.</summary>
        TowardThreat = 2,

        /// <summary>Move away from it — cover, break contact, out of the impact area.</summary>
        AwayFromThreat = 3,
    }

    /// <summary>One step of a drill.</summary>
    public struct TtpStep
    {
        /// <summary>What the step is, as a subordinate would hear it.</summary>
        public string Text;

        /// <summary>
        /// The command this step becomes when a drill is executed, or
        /// <see cref="CommandKind.None"/> for a step the simulation cannot yet carry out.
        /// </summary>
        /// <remarks>
        /// **None is a real answer here, not a gap to be tidied away.** Most of what a battle
        /// drill actually asks for — bound, report, seek cover, establish a base of fire — has
        /// no executor, because `MoveTo` and `Engage` are the only world commands that exist.
        /// Recording that per step lets the binder mark which lines the engine can honour
        /// today, which is the clearest statement available of how much of doctrine is still
        /// presentation. Omitting those steps would make the drills look complete and the
        /// engine look finished.
        /// </remarks>
        public CommandKind Kind;

        /// <summary>Whether this becomes an order, happens by itself, or is not modelled.</summary>
        public StepNature Nature;

        /// <summary>What the step's command points at when the drill is invoked.</summary>
        public StepBinding Binding;

        public TtpStep(string text, CommandKind kind = CommandKind.None,
            StepBinding binding = StepBinding.Here, StepNature nature = StepNature.Command)
            : this()
        {
            Text = text;
            Kind = kind;
            Binding = binding;

            // A step with no command cannot be a Command step whatever it claims, so the
            // default corrects itself rather than letting the two fields disagree.
            Nature = kind == CommandKind.None && nature == StepNature.Command
                ? StepNature.Unmodelled
                : nature;
        }

        /// <summary>True when invoking the drill puts this step in the unit's queue.</summary>
        public bool IsMechanised => Nature == StepNature.Command && Kind != CommandKind.None;

        /// <summary>True when the simulation does this without being told.</summary>
        public bool IsInherent => Nature == StepNature.Inherent;
    }

    /// <summary>
    /// A named, coded drill.
    /// </summary>
    /// <remarks>
    /// Public fields rather than properties, because this type is persisted and
    /// `FieldsOnlyResolver` serialises fields and ignores properties — see ScenarioIO for why
    /// that rule exists. The computed members below are properties for exactly that reason:
    /// being properties is what keeps them out of the file.
    /// </remarks>
    public sealed class Ttp
    {
        /// <summary>
        /// What a commander transmits. Short on purpose — this is the whole reason the type
        /// exists, and it is what a low-bandwidth channel carries (#46, #62).
        /// </summary>
        public string Code = string.Empty;

        public string Name = string.Empty;

        public DrillSeries Series = DrillSeries.BattleDrill;

        /// <summary>One line. What it does, readable without the sequence.</summary>
        public string Summary = string.Empty;

        /// <summary>
        /// When this drill is *wrong*.
        /// </summary>
        /// <remarks>
        /// The field a plain list would omit and the one that stops a player firing a code
        /// blindly. A drill with no stated limit reads as always-correct, which is how a
        /// brevity code becomes a button rather than a decision.
        /// </remarks>
        public string NotWhen = string.Empty;

        /// <summary>The echelon that executes this. Not the echelon that authored it.</summary>
        public DrillEchelon Echelon = DrillEchelon.Squad;

        public TtpStep[] Steps = System.Array.Empty<TtpStep>();

        /// <summary>The figure on the facing page, or null where geometry adds nothing.</summary>
        public TtpDiagram Diagram;

        /// <summary>How many steps invoking the drill actually issues.</summary>
        public int MechanisedSteps
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Steps.Length; i++) if (Steps[i].IsMechanised) n++;
                return n;
            }
        }

        /// <summary>How many steps the simulation performs without being told.</summary>
        public int InherentSteps
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Steps.Length; i++) if (Steps[i].IsInherent) n++;
                return n;
            }
        }

        /// <summary>How many steps want a mechanic that does not exist yet.</summary>
        public int UnmodelledSteps => Steps.Length - MechanisedSteps - InherentSteps;

        public string EchelonName => Echelon switch
        {
            DrillEchelon.Team => "Fire team",
            DrillEchelon.Squad => "Squad",
            DrillEchelon.Platoon => "Platoon",
            _ => "Company",
        };

        public override string ToString() => $"{Code} {Name} ({Steps.Length} steps)";
    }

    /// <summary>
    /// The drills that ship with the game, in code.
    /// </summary>
    /// <remarks>
    /// **This is the authoring source, not the runtime source.** `Strategos > Write Sample
    /// Drills` serialises it to <c>Resources/Doctrine/</c> and <see cref="TtpLibrary"/> reads
    /// the JSON — exactly the split `ScenarioSamples` and `ScenarioIO` already use, and for
    /// the same reasons: drills are *content*, editing content should not need a recompile,
    /// and doctrine packs are a planned modding and DLC surface (docs/phases.md 9.3,
    /// docs/steam.md). Keeping the set in code as well gives the JSON something to be
    /// generated from and diffed against.
    ///
    /// Ordered team-first, because that is the order they are learned and the order the
    /// echelon curve introduces them.
    /// </remarks>
    public static class DoctrineSamples
    {
        /// <summary>Name of the shipped pack, and its file stem under Resources/Doctrine.</summary>
        public const string PackName = "field-drills";

        public static DoctrinePack Pack() => new()
        {
            Name = "Field Drills",
            Source = "FM 7-8 lineage, abridged",
            Drills = Drills(),
        };

        private static Vector2 P(float x, float y) => new(x, y);

        public static Ttp[] Drills() => new[]
        {
            // ─── Team series: what a fire team trains on ──────────────────────

            new Ttp
            {
                Code = "T1", Name = "Fire and Movement", Series = DrillSeries.Team,
                Echelon = DrillEchelon.Team,
                Summary = "Move by buddy teams so one pair is always firing while the other moves.",
                NotWhen = "You have nothing to suppress with - moving alone under fire is worse.",
                Steps = new[]
                {
                    new TtpStep("One buddy team fires", CommandKind.Engage, StepBinding.AtThreat),
                    new TtpStep("The other bounds forward", CommandKind.MoveTo, StepBinding.TowardThreat),
                    new TtpStep("Bounding team takes cover and fires", CommandKind.Engage, StepBinding.AtThreat),
                    new TtpStep("Alternate until the objective is reached", CommandKind.MoveTo, StepBinding.TowardThreat),
                },
                Diagram = TtpDiagram.Of(
                    "ONE PAIR FIRES WHILE THE OTHER MOVES - NEVER BOTH AT ONCE",
                    FigureElement.Hostile(0.78f, 0.74f),
                    FigureElement.Friendly(0.18f, 0.32f, "A", "team"),
                    FigureElement.Friendly(0.46f, 0.24f, "B", "team"),
                    FigureElement.SupportByFire(P(0.20f, 0.40f), P(0.72f, 0.66f)),
                    FigureElement.Bound("BOUND", P(0.48f, 0.32f), P(0.62f, 0.48f))),
            },

            new Ttp
            {
                Code = "T2", Name = "React to Contact (Team)", Series = DrillSeries.Team,
                Echelon = DrillEchelon.Team,
                Summary = "The team's answer to unexpected fire, before anyone gives an order.",
                NotWhen = "Never - this one is reflex, which is why it is drilled.",
                Steps = new[]
                {
                    new TtpStep("Return fire at once", CommandKind.Engage, StepBinding.AtThreat),
                    new TtpStep("Take the nearest cover", CommandKind.MoveTo, StepBinding.AwayFromThreat),
                    new TtpStep("Call the direction and distance", CommandKind.None, StepBinding.Here, StepNature.Inherent),
                    new TtpStep("Await the squad leader's order", CommandKind.None, StepBinding.Here, StepNature.Inherent),
                },
                Diagram = TtpDiagram.Of(
                    "FIRE FIRST - THEN COVER - THE REPORT COMES AFTER BOTH",
                    FigureElement.Hostile(0.74f, 0.76f),
                    FigureElement.Friendly(0.26f, 0.38f, "TEAM", "team"),
                    FigureElement.Axis("RETURN FIRE", P(0.34f, 0.44f), P(0.66f, 0.70f)),
                    FigureElement.Bound("COVER", P(0.24f, 0.32f), P(0.14f, 0.18f))),
            },

            new Ttp
            {
                Code = "T3", Name = "React to Indirect Fire", Series = DrillSeries.Team,
                Echelon = DrillEchelon.Team,
                Summary = "Get out of the impact area. Do not go to ground and wait it out.",
                NotWhen = "You are already in prepared overhead cover.",
                Steps = new[]
                {
                    new TtpStep("Shout INCOMING", CommandKind.None, StepBinding.Here, StepNature.Inherent),
                    new TtpStep("Move out of the impact area at once", CommandKind.MoveTo, StepBinding.AwayFromThreat),
                    new TtpStep("Disperse while moving", CommandKind.None, StepBinding.Here, StepNature.Unmodelled),
                    new TtpStep("Report the shelling", CommandKind.None, StepBinding.Here, StepNature.Inherent),
                },
                Diagram = TtpDiagram.Of(
                    "MOVE OUT OF IT - GROUND OFFERS NOTHING AGAINST AIRBURST",
                    FigureElement.Objective(0.64f, 0.64f, "IMPACT"),
                    FigureElement.Friendly(0.58f, 0.54f, "TEAM", "team"),
                    FigureElement.Axis("OUT", P(0.48f, 0.46f), P(0.18f, 0.24f))),
            },

            new Ttp
            {
                Code = "T4", Name = "Break Contact (Team)", Series = DrillSeries.Team,
                Echelon = DrillEchelon.Team,
                Summary = "Disengage a team that is outmatched, bounding away under its own fire.",
                NotWhen = "Decisively engaged at close range - turning your back is worse.",
                Steps = new[]
                {
                    new TtpStep("Suppress to buy the first bound", CommandKind.Engage, StepBinding.AtThreat),
                    new TtpStep("Bound back by pairs", CommandKind.MoveTo, StepBinding.AwayFromThreat),
                    new TtpStep("Continue until out of contact", CommandKind.MoveTo, StepBinding.AwayFromThreat),
                    new TtpStep("Report and reorganise", CommandKind.None, StepBinding.Here, StepNature.Inherent),
                },
                Diagram = TtpDiagram.Of(
                    "THE SAME ALTERNATION AS T1 - RUN BACKWARDS",
                    FigureElement.Hostile(0.80f, 0.72f),
                    FigureElement.Friendly(0.54f, 0.46f, "A", "team"),
                    FigureElement.Friendly(0.26f, 0.28f, "B", "team"),
                    FigureElement.SupportByFire(P(0.56f, 0.52f), P(0.76f, 0.66f)),
                    FigureElement.Bound("BREAK", P(0.24f, 0.22f), P(0.10f, 0.12f))),
            },

            // ─── Numbered battle drills: squad and platoon ────────────────────

            new Ttp
            {
                Code = "1", Name = "Platoon Attack", Echelon = DrillEchelon.Platoon,
                Summary = "Close with and destroy a position with a platoon's weight.",
                NotWhen = "The position is not fixed, or you have not found its flank.",
                Steps = new[]
                {
                    new TtpStep("Fix the position with a base of fire", CommandKind.Engage, StepBinding.AtThreat),
                    new TtpStep("Find and report a flank", CommandKind.None, StepBinding.Here, StepNature.Inherent),
                    new TtpStep("Manoeuvre the assault squads", CommandKind.MoveTo, StepBinding.TowardThreat),
                    new TtpStep("Assault through", CommandKind.Engage, StepBinding.AtThreat),
                    new TtpStep("Consolidate and reorganise", CommandKind.Defend, StepBinding.Here),
                },
                Diagram = TtpDiagram.Of(
                    "ONE ELEMENT FIXES FROM THE FRONT - ANOTHER TAKES THE FLANK",
                    FigureElement.Hostile(0.62f, 0.80f),
                    FigureElement.Friendly(0.16f, 0.46f, "1 SQD", "squad"),
                    FigureElement.Friendly(0.42f, 0.18f, "2 SQD", "squad"),
                    FigureElement.SupportByFire(P(0.18f, 0.52f), P(0.54f, 0.74f)),
                    FigureElement.Axis("ASSAULT", P(0.50f, 0.22f), P(0.76f, 0.46f),
                        P(0.70f, 0.70f))),
            },

            new Ttp
            {
                Code = "1A", Name = "Squad Attack", Echelon = DrillEchelon.Squad,
                Summary = "The same shape at squad level: one team fixes, the other assaults.",
                NotWhen = "The enemy is not yet fixed, or you are the smaller force.",
                Steps = new[]
                {
                    new TtpStep("Establish a base of fire", CommandKind.Engage, StepBinding.AtThreat),
                    new TtpStep("Suppress the position", CommandKind.Engage, StepBinding.AtThreat),
                    new TtpStep("Manoeuvre the assault team", CommandKind.MoveTo, StepBinding.TowardThreat),
                    new TtpStep("Assault through", CommandKind.Engage, StepBinding.AtThreat),
                    new TtpStep("Consolidate and reorganise", CommandKind.Defend, StepBinding.Here),
                },
                Diagram = TtpDiagram.Of(
                    "BASE OF FIRE LEFT - ASSAULT RIGHT - FIRE SHIFTS AS THE TEAM CLOSES",
                    FigureElement.Hostile(0.70f, 0.78f),
                    FigureElement.Objective(0.70f, 0.78f, "OBJ"),
                    FigureElement.Friendly(0.16f, 0.42f, "A TM", "team"),
                    FigureElement.Friendly(0.40f, 0.16f, "B TM", "team"),
                    FigureElement.SupportByFire(P(0.18f, 0.48f), P(0.62f, 0.72f)),
                    FigureElement.Axis("ASSAULT", P(0.48f, 0.20f), P(0.78f, 0.44f),
                        P(0.76f, 0.66f))),
            },

            new Ttp
            {
                Code = "2", Name = "React to Contact", Echelon = DrillEchelon.Squad,
                Summary = "Answer unexpected fire, find who is firing, and take the initiative.",
                NotWhen = "In the open with no cover within 50 m - break contact instead.",
                Steps = new[]
                {
                    new TtpStep("Return fire immediately", CommandKind.Engage, StepBinding.AtThreat),
                    new TtpStep("Take the nearest cover", CommandKind.MoveTo, StepBinding.AwayFromThreat),
                    new TtpStep("Report contact to higher", CommandKind.None, StepBinding.Here, StepNature.Inherent),
                    new TtpStep("Locate and suppress", CommandKind.Engage, StepBinding.AtThreat),
                    new TtpStep("Assault or break contact", CommandKind.None, StepBinding.Here, StepNature.Unmodelled),
                },
                Diagram = TtpDiagram.Of(
                    "SUPPRESS FIRST - ASSAULT OR BREAK IS DECIDED AFTER",
                    FigureElement.Hostile(0.74f, 0.76f),
                    FigureElement.Friendly(0.20f, 0.48f, "A TM", "team"),
                    FigureElement.Friendly(0.30f, 0.20f, "B TM", "team"),
                    FigureElement.SupportByFire(P(0.22f, 0.54f), P(0.66f, 0.70f)),
                    FigureElement.Bound("OR BREAK", P(0.28f, 0.14f), P(0.10f, 0.08f))),
            },

            new Ttp
            {
                Code = "3", Name = "Break Contact", Echelon = DrillEchelon.Squad,
                Summary = "Disengage from a fight you should not be having and open the distance.",
                NotWhen = "Decisively engaged at close range - breaking exposes your flank.",
                Steps = new[]
                {
                    new TtpStep("Suppress to buy movement", CommandKind.Engage, StepBinding.AtThreat),
                    new TtpStep("Bound back by team", CommandKind.MoveTo, StepBinding.AwayFromThreat),
                    new TtpStep("Continue until out of contact", CommandKind.MoveTo, StepBinding.AwayFromThreat),
                    new TtpStep("Report and reorganise", CommandKind.None, StepBinding.Here, StepNature.Inherent),
                },
                Diagram = TtpDiagram.Of(
                    "ALTERNATING BOUNDS - SOMEONE IS ALWAYS FIRING",
                    FigureElement.Hostile(0.82f, 0.74f),
                    FigureElement.Friendly(0.54f, 0.48f, "A TM", "team"),
                    FigureElement.Friendly(0.26f, 0.28f, "B TM", "team"),
                    FigureElement.SupportByFire(P(0.56f, 0.54f), P(0.78f, 0.68f)),
                    FigureElement.Bound("BOUND", P(0.24f, 0.22f), P(0.10f, 0.12f))),
            },

            new Ttp
            {
                Code = "4", Name = "React to Ambush", Echelon = DrillEchelon.Squad,
                Summary = "Answer a near ambush by assaulting through the killing zone.",
                NotWhen = "The ambush is far and covered - suppress and manoeuvre instead.",
                Steps = new[]
                {
                    new TtpStep("Return fire without waiting for an order", CommandKind.Engage, StepBinding.AtThreat),
                    new TtpStep("Assault through the near side", CommandKind.MoveTo, StepBinding.TowardThreat),
                    new TtpStep("Clear and consolidate beyond it", CommandKind.Defend, StepBinding.Here),
                    new TtpStep("Treat casualties and report", CommandKind.None, StepBinding.Here, StepNature.Inherent),
                },
                Diagram = TtpDiagram.Of(
                    "THROUGH IT - THE KILLING ZONE IS THE WORST PLACE TO STAY",
                    FigureElement.Hostile(0.68f, 0.68f),
                    FigureElement.Objective(0.42f, 0.46f, "KZ"),
                    FigureElement.Friendly(0.18f, 0.42f, "SQD", "squad"),
                    FigureElement.Axis("ASSAULT", P(0.28f, 0.44f), P(0.62f, 0.60f))),
            },

            new Ttp
            {
                Code = "5", Name = "Knock Out a Bunker", Echelon = DrillEchelon.Squad,
                Summary = "Suppress the aperture, work a team to its blind side, and clear it.",
                NotWhen = "The bunker is mutually supported and the supporting one is unlocated.",
                Steps = new[]
                {
                    new TtpStep("Suppress the aperture", CommandKind.Engage, StepBinding.AtThreat),
                    new TtpStep("Move a team to the blind side", CommandKind.MoveTo, StepBinding.TowardThreat),
                    new TtpStep("Clear the position", CommandKind.None, StepBinding.Here, StepNature.Unmodelled),
                    new TtpStep("Consolidate beyond it", CommandKind.Defend, StepBinding.Here),
                },
                Diagram = TtpDiagram.Of(
                    "APPROACH OUT OF THE APERTURE ARC - NEVER ACROSS ITS FRONT",
                    FigureElement.Hostile(0.66f, 0.76f),
                    FigureElement.BattlePosition(0.66f, 0.76f, string.Empty, 235f),
                    FigureElement.Friendly(0.18f, 0.44f, "A TM", "team"),
                    FigureElement.Friendly(0.44f, 0.16f, "B TM", "team"),
                    FigureElement.SupportByFire(P(0.20f, 0.50f), P(0.58f, 0.70f)),
                    FigureElement.Axis("BLIND SIDE", P(0.52f, 0.20f), P(0.82f, 0.46f),
                        P(0.76f, 0.68f))),
            },

            new Ttp
            {
                Code = "6", Name = "Enter Building and Clear Room", Echelon = DrillEchelon.Squad,
                Summary = "Enter at a point of your choosing and clear the building room by room.",
                NotWhen = "The building is not isolated - clearing into reinforcement is a trap.",
                Steps = new[]
                {
                    new TtpStep("Isolate the building", CommandKind.Engage, StepBinding.AtThreat),
                    new TtpStep("Suppress the entry point", CommandKind.Engage, StepBinding.AtThreat),
                    new TtpStep("Enter at a corner, not the doorway centre", CommandKind.None, StepBinding.Here, StepNature.Unmodelled),
                    new TtpStep("Clear by room, marking as you go", CommandKind.None, StepBinding.Here, StepNature.Unmodelled),
                    new TtpStep("Consolidate and report cleared", CommandKind.Defend, StepBinding.Here),
                },
                Diagram = TtpDiagram.Of(
                    "ISOLATE FIRST - A BUILDING CLEARED INTO REINFORCEMENT IS NOT CLEARED",
                    FigureElement.Objective(0.62f, 0.64f, "BLDG"),
                    FigureElement.Friendly(0.18f, 0.64f, "A TM", "team"),
                    FigureElement.Friendly(0.34f, 0.20f, "B TM", "team"),
                    FigureElement.SupportByFire(P(0.22f, 0.64f), P(0.50f, 0.64f)),
                    FigureElement.Axis("ENTRY", P(0.40f, 0.26f), P(0.56f, 0.50f))),
            },

            new Ttp
            {
                Code = "7", Name = "Enter and Clear a Trench", Echelon = DrillEchelon.Squad,
                Summary = "Gain a foothold in the trench, then clear along it in both directions.",
                NotWhen = "You have no way to suppress along the length of the trench.",
                Steps = new[]
                {
                    new TtpStep("Suppress the trench line", CommandKind.Engage, StepBinding.AtThreat),
                    new TtpStep("Gain a foothold", CommandKind.MoveTo, StepBinding.TowardThreat),
                    new TtpStep("Clear along the trench in both directions", CommandKind.None, StepBinding.Here, StepNature.Unmodelled),
                    new TtpStep("Consolidate and report", CommandKind.Defend, StepBinding.Here),
                },
                // No figure on purpose. The geometry is a line and a foothold, and a drawing
                // of it says less than the sentence does — a page is allowed to have none, and
                // the view has to handle that anyway.
            },
        };
    }
}
