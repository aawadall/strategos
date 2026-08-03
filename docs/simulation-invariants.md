# Simulation invariants

The fixed-step simulation: three topics, per-unit queues, combat,
reflexes, objectives. **Read before touching anything under `Core/Commands`, `Core/Reports`,
`Core/Combat`, `Core/Reactions`, `Core/Direction`, `Core/Directives`, `Core/Objectives`,
`Core/Movement` or `Core/Messaging`.** Breaking one of these does not throw — it makes a replay diverge some
number of steps later.

[docs/command-architecture.md](command-architecture.md) carries the reasoning; this carries
the rules. [CLAUDE.md](../CLAUDE.md) is the index.

---

## Command and reporting invariants

Three topics — orders down, reports up, directives in from higher — over one `MessageBus<T>`.
`docs/command-architecture.md` carries the reasoning; these are the things that break silently.

`Simulation.Step` fixes the order, and it is part of the contract:

```
Tick++
  └─ Bus.Deliver()          commands published before this step
     └─ Reports.Deliver()    reports published before this step
        └─ Directives.Deliver()  directives published before this step
        └─ Director.Evaluate()    side-level intent, before reflexes
           └─ Reactions.Evaluate() reflexes, from the start-of-step picture
           └─ AdvanceUnit ×n      in scenario order, never dictionary order
              └─ ResolveEngagements   resolve ALL, then apply ALL
                 └─ DecaySuppression ×n
                    └─ ContactTracker.Sweep   publishes for delivery next step
                       └─ Victory.Evaluate   last, on the state the tick ended in
```

- **Nothing in the simulation may read `Time.deltaTime`, wall-clock time,
  `UnityEngine.Random`, or iterate a `Dictionary`/`HashSet`.** Each one makes a replay
  diverge, and the divergence surfaces long after the change that caused it. Presentation
  interpolates; the simulation counts ticks. `Simulation.SecondsPerTick` is a `const` and
  not a setting because changing it changes every outcome.
- **Training costs time and only time.** `UnitInstance.Training` produces
  `HesitationTicks`, and the head of a `CommandQueue` will not leave `Pending` until it has
  waited that many ticks. **One gate covers both effects on purpose**: a reflex is issued as a
  preempting command onto the *front* of the queue, so the same hesitation that delays a march
  delays returning fire. `TicksPending` is part of the queue signature — it decides *when* an
  order starts, so a replay that did not reproduce it would diverge on the next tick — and
  `InsertFront` resets it, or a green unit could dodge its own hesitation by being interrupted.
  A green observer's contact is held in `ContactTracker` and published late with
  `ObservedTick` untouched, which is what that field was always for. **Training must never
  make a unit do the *wrong* thing** — a model where an order is sometimes carried out
  differently is unlearnable, indistinguishable from a bug, and impossible to plan around.
  Default is 100, meaning zero hesitation and behaviour identical to before the feature, so
  any movement in another probe's numbers is a real regression.
- **Fatigue is a world rule, and it has a floor.** `FatigueModel.Apply` runs once per unit
  per step, after engagements resolve so "was this unit in a fight this tick" is answerable
  from the intent list. Marching and fighting spend `Readiness`; halted and unengaged gives
  it back, more slowly, because a resource that refills as fast as it empties is topped up
  rather than spent. **`MinReadiness` is not tuning** — `Effectiveness` multiplies strength,
  readiness and suppression, so an unfloored readiness is a fourth independent path to zero
  and a long march alone would render a unit combat ineffective without a shot fired. Only
  casualties may reach zero, and `IsDestroyed` is for that. Terrain difficulty is the
  reciprocal of the unit's own landcover speed factor, so what slows a unit also tires it,
  from one authored number rather than a second table that could drift. Deterministic
  throughout: no draw anywhere, only posture, terrain and whether an engagement named it.
- **A formation is a `UnitInstance` that owns subordinates, and it is not a combatant.**
  `Simulation.Units` deliberately keeps meaning what it always meant — the things that fight —
  and now returns **leaves**, so every consumer that enumerates units to answer "what can be
  seen, shot at, moved or counted" stayed correct with no edit. `AllUnits` is the explicit way
  to ask for formations too: the dangerous list is the one you have to name. A formation
  leaking into the fighting list is invisible — a battalion detected separately from its own
  companies, engaged separately and counted separately for victory, all of it merely *wrong*
  rather than broken — which is why `HierarchyProbe`'s first assertion is that boring one.
  **This applies to views as well**, and PlayView got it wrong first time: drawing
  `_scenario.Units` put a battalion symbol on top of its own companies.
- **State rolls up; it is never stored twice.** A formation's strength, readiness and position
  come from `UnitHierarchy`, not from its own fields, which are meaningless on a unit that
  owns others. Position especially: a stored one would let a battalion stand where its troops
  are not.
- **A formation holds no queue.** An order addressed to one decomposes at *delivery* into one
  order per immediate subordinate, each issued through `Issue` so the log records the parent
  order **and** what it became. One echelon per step, which is not an inefficiency — it is the
  propagation delay phases.md 5.2 wants, arriving from the structure rather than from a timer.
  Subordinate order is fixed at construction from the scenario's own list; dictionary order
  there would diverge a replay.
- **A directive is not an order, and the word means exactly one thing.** `Core/Directives/`
  holds a message from higher: it states intent and constraints and leaves the *how* to the
  receiver. It is published on `Directives`, **never** on `Bus`, and never reaches
  `OnCommandDelivered` — which is the whole point, because that method decomposes *any*
  formation-addressed command before it inspects the kind, so a directive expressed as a
  `Command` would become orders on arrival and leave the player nothing to decide. A
  directive arriving must append nothing to `CommandLog` and leave every subordinate queue
  empty. `DirectiveProbe` asserts exactly that, and has been seen to fail when it is broken.
- **A directive's `Id` is authored; its `Seq` is stamped by the log.** Anything that must
  reference a directive from scenario data — `VictoryCondition` does — references the `Id`.
  A scenario cannot know a runtime sequence in advance, and referencing `Seq` would work only
  by the accident that the constructor publishes the one directive first.
- **Victory is sourced from the directive, not duplicated by it.** A `VictoryCondition`
  records which directive produced it. There is still exactly one evaluator and one source of
  truth: the directive is *where a condition came from*, not a second authority that could
  drift from the condition it appears to predict.
- **Acknowledge only — there is deliberately no refusal.** #73 allows a directive to be
  refused; v1 does not implement it, because nothing yet reacts to a refusal and an
  unreachable path is untested code pretending to be a feature. Add `DirectiveRefused` and
  its handling together, or not at all.
- **A player's answer to a directive must never be replay-invisible.** #94: `AcknowledgeDirective`
  used to touch no log at all — a report went out, but nothing a replay reads recorded that the
  call had happened, so a replayed run never acknowledged anything the original did and the two
  `Signature()`s diverged the moment anyone pressed the button. It does **not** get a
  `CommandKind.Acknowledge` and does **not** go through `Bus` — that would put a directive's
  *response* on the *command* topic, cutting against the separation `OnCommandDelivered`'s own
  note holds (a directive must never reach that method). The fix is a second log,
  `DirectiveResponseLog`, alongside `CommandLog`: replay is a single definition
  (`Commands.Replayer`) that reads both, and it must call `AcknowledgeDirective` itself rather
  than re-publish the `DirectiveAcknowledged` report directly — only that method holds the
  idempotence guard over `_acknowledgedDirectives`, and re-publishing the report around it would
  leave a replayed run's guard empty even though its `ReportLog` matched.
- **A destroyed unit becomes a wreck, and a wreck is not a contact.** It stays on the map on
  purpose — ground where a company was destroyed is information, and removing it deletes the
  only trace a fight happened there — but it stops being a *unit*: not commandable, not
  detected, not counted among a side's troops. `ContactTracker` had never been asked this and
  was silently wrong: a burnt-out company went on being reported as a live contact for the
  rest of the scenario, so its enemy's commander believed there was a going concern on ground
  that had been cleared. A held contact on something just destroyed reports **ContactLost** —
  it has stopped being a threat, which is what the report means.
- **A loss is recorded at the crossing, not derived after.** `Simulation` stamps
  `DestroyedAtTick` and appends to `CasualtyLog` in the one moment the information exists;
  afterwards the only evidence is a strength of zero, which cannot say when it happened or
  what did it. The casualty log is **in the divergence signature**, because two runs ending
  with the same strengths but different losses have diverged in everything a campaign carries
  forward. The symbol uses APP-6D's own `PresentDestroyed` status, which `ConditionDecorator`
  already drew.
- **`Defend` is a state, not a task, and it is the first order that never ends.** MoveTo
  completes on arrival and Engage when the target dies; Defend returns `Running` for ever and
  leaves the queue only by being cancelled. It is also **the first thing in the project to set
  `Posture.DugIn`** — `EngagementResolver.PostureFactor` has paid out 0.5 for it since combat
  landed and nothing had ever produced it, so the value was unreachable. Digging in takes
  `DefendExecutor.DigInTicks` and is re-applied every tick rather than set once, because a
  reflex sends the unit to `Halted` while it returns fire and the posture has to come back.
  **`Hold` moved from the control range into the world range** to make this possible, which is
  what stopped it being a byte-for-byte copy of `Abort`.
- **`Screen` is the other never-ending hold, and it buys reach rather than protection.** Same
  queue shape as Defend, but posture is `Posture.Screening` — combat exposure matches Halted
  (`PostureFactor` 1.0), and `ContactTracker` multiplies detection by
  `UnitCapabilities.DetectionPostureFactor` (1.35). Digging in is deliberately not on the
  path: a Screen that dug in would be Defend with a longer radio.
- **`Guard` sits between Screen and Cover on the security ladder.** Same dig-in clock as
  Defend (`DefendExecutor.DigInTicks`); once prepared, posture is `Posture.Guarding` —
  `PostureFactor` 0.5 like DugIn, detection ×1.15 (lighter watch than Screen).
- **`Cover` is the heaviest security hold.** Digs in to `Posture.Covering` (fire 0.5, no
  detection stretch). While any Cover remains on the queue, `ReactionController` will not
  break contact — including when a return-fire Engage sits above it.
- **`Withdraw` expands at delivery** into Abort + MoveTo away from a threat
  (`ReactionController.WithdrawCells`). `AgainstUnit` / `TargetCell` name the threat and its
  believed position (LastSeen); empty falls back to `NearestHostile` (#34 shortcut). The
  break-contact reflex now Issues Withdraw rather than Abort+MoveTo directly — one path.
- **`Delay` holds Halted until the break-contact threshold**, then Completes and Simulation
  Issues Withdraw — trade ground for time. Cover suppresses leaving; Delay wants it.
- **`Attack` expands at delivery** into MoveTo (to `AttackStandoffCells` of the threat) +
  Engage. Already inside standoff → Engage only. Empty `AgainstUnit` → `NearestHostile`.
- **`Recon` expands at delivery** into MoveTo (to `ReconStandoffCells`, farther than Attack) +
  Screen — move to see, then watch.
- **`Exploit` expands at delivery** into MoveTo *past* the threat (`ExploitDepthCells`) +
  Engage — follow-through, not Attack's close-and-standoff. Remaining under #85: Pursue (#153).
- **A drill expands at delivery; it never reaches an executor.** `CommandKind.Drill` names a
  code and is unpacked into the orders its steps become, each issued through `Issue` so the log
  records the drill **and** what it became. The formation check runs first, so a drill given to
  a battalion decomposes to its companies and each of them expands it — which is what
  "2 Squad, React to Contact" has to mean.
- **A drill's steps are unbound, and the only thing to bind against is the threat.** Most of
  doctrine is directional relative to contact — assault toward it, break away from it, hold
  where you are — so `StepBinding` says which way and `Simulation` supplies the nearest hostile.
  Ground chosen for its own qualities (a reverse slope, a treeline) needs terrain reasoning and
  is not attempted. **It binds against ground truth, which is a shortcut**: once belief layers
  land (#34) it must bind against the threat the commander *knows about*, or a unit is reacting
  to an enemy it cannot see.
- **A step that is not an order is not silently dropped.** `StepNature` separates the three
  cases — an order it issues, something the simulation already does (reporting), and a mechanic
  that does not exist. Calling every non-order step "no executor" was wrong about two thirds of
  them: `ContactTracker` publishes *report contact to higher* unasked. The honest split is 71%
  orders, 17% inherent, 11% unmodelled, and `DrillProbe` prints it.
- **Both buses publish to the *next* step.** That is what bounds a report → reaction →
  order → report cascade, and it is the degenerate case of the propagation delay Phase 5.2
  wants. `Publish` never delivers inline even when called from outside a dispatch, or an
  order issued by the UI would arrive a step earlier than one issued by a reacting unit.
- **Subscriber order is `Order` ascending, ties by registration.** The insertion sort in
  `MessageBus.Subscribe` is deliberate: `List.Sort` is introsort and **not stable**, so
  equal orders could permute between runs. The unit layer subscribes at 0 and is the only
  subscriber allowed to mutate anything; observers go above it.
- **Detection publishes edges, not state.** `ContactTracker` reports a hostile *entering*
  and *leaving* range, never the current picture. The alternative is one message per
  observer per hostile per tick, which is a poll wearing a message's clothes and drowns the
  log. `LossHysteresis` exists because a unit halted on the boundary would otherwise flap.
- **Nothing downstream of `ContactTracker` may ask the world about hostiles.** A consumer
  that reads unit positions can never be deceived, delayed or jammed, so every one written
  that way has to be rewritten when C3 lands. Read reports. This is unenforceable by a
  compiler and is the rule most likely to be broken by a view, which has every unit in hand
  already — see `PlayView.OnReport`.
- **Detection range goes through `UnitCapabilities.DetectionRangeAt`,** the documented
  single call to replace when terrain line of sight arrives. Do not compare distances
  anywhere else.
- **Executors mutate their own unit and nothing else.** Reports about a finished order are
  published by `Simulation.AdvanceUnit` from the outcome, not by the executor, so an
  executor added later reports without being written to. An engagement touches two units,
  so `EngageExecutor` appends to `ExecutionContext.Engagements` — it declares *intent* and
  the simulation applies the effect.
- **Fire resolves in two passes: resolve everything, then apply everything.** Every shot in
  a tick is computed against start-of-tick state. One pass would hand whichever unit the
  loop reached first a free shot at an undamaged enemy — a first-mover advantage set by the
  order units happen to appear in the scenario file, invisible in any one game and very hard
  to find once someone notices the unit listed first tends to win. `CombatProbe`'s
  simultaneity check is what holds this.
- **`EngagementResolver.Resolve` reads state and writes none.** `Apply` is separate for the
  reason above. Do not fold them back together.
- **The only stochastic term is seeded from `(tick, attacker, defender)`,** so it is
  reproducible from state alone — no generator is carried between ticks, which means a
  replay reproduces each *draw* rather than a random *stream*. The ids are mixed with
  different multipliers so an exchange does not share one roll in both directions.
- **`Strength` is a float and must stay one.** Firepower is authored per minute and the
  simulation steps per second, so a tick of fire is a fraction of a point; held as an int,
  every exchange rounds to zero and two units shoot at each other for ever. Display through
  `StrengthPercent` — the raw float must never reach a symbol's `StrengthLabel`, which is
  part of the sprite cache key.
- **Suppression is temporary and must never cancel an order.** Sustained fire pins a unit in
  about forty-five seconds; treating that as failure would permanently drop the engage order
  of whoever came off worst in an opening exchange. `AttackerSuppressed` and
  `AttackerDestroyed` are separate outcomes for exactly this reason.
- **`SuppressionPerDamage` is tuned against `SuppressionDecayPerSecond`, not chosen.** Below
  about 7 the decay out-paces the gain and suppression never rises at all — a unit under
  sustained fire that reads as perfectly calm.
- **`ReactionController` may read reports and a unit's *own* state, and nothing else.** It
  never asks the world whether an enemy is in range, never reads another unit's position,
  and picks targets by the cell a contact was last *reported* at. A unit that polls cannot
  be deceived, delayed or spoofed, so reaction logic written that way has to be rebuilt
  rather than wrapped when C3 lands. Own strength, suppression and ammunition are
  introspection, not observation — no message has to arrive for a company to know it is out
  of ammunition.
- **Reactions evaluate in scenario unit order, and that order carries no advantage.** A
  reaction issues a *command*, commands are delivered on the following step, and fire
  resolves against start-of-tick state — so two units that notice each other on the same
  tick open fire on the same tick. `ReactionProbe.CheckMutualReactionIsFair` holds this; it
  would stop being true the moment somebody resolved a reaction inline to save a tick.
- **A reflex preempts, it never deletes.** `Command.Preempt` puts a reactive engagement at
  the head of the queue and pushes the displaced order back to Pending, so a unit fired on
  mid-march shoots back now and resumes the march after. Appended instead, it would answer
  fire when it arrived, twenty minutes later.
- **ROE governs initiative, not permission.** A unit on Hold Fire still carries out an
  engage order it was given; refusing a direct order would be a bug.
- **Suppression is deliberately not a break-contact trigger.** It saturates near 100 within
  about fifteen seconds of sustained fire, so any threshold below the cap made every unit
  disengage almost the moment it was shot at — the probe caught one leaving at 67.8%
  strength, barely scratched. It is also backwards: suppression models being *pinned*, and a
  pinned unit is one that cannot move, not one that has decided to leave.
- **An objective's centre must be ground a unit can occupy.** A MoveTo to an impassable
  cell fails on the tick it is issued, so `SideDirector` finds the unit idle and reissues —
  the shipped skirmish once ran its full hour with 1080 autonomous orders and nobody moving,
  because the crossroads was one cell into a lake. `DirectorProbe` asserts it and names the
  nearest passable cell.
- **`SideDirector.RetryInterval` is the guard against that order storm,** not politeness. Any
  unreachable destination reproduces it otherwise, and the command log is what a replay and an
  after-action review both read.
- **Side-level decision-making sits behind `ISidePolicy`, and `SideDirector` is the default
  implementation, not the only possible one (#100).** `Decide(SideKnowledge)` is pull-based on
  purpose: a policy returns the commands it wants issued and `Simulation.Step` issues each one
  itself, exactly where `Director?.Evaluate()` used to call `_sim.Issue` inline — same tick,
  same order, same `Signature()`. A policy that instead took `Simulation` as an argument would
  satisfy the compiler while keeping the exact coupling the seam exists to remove, which is why
  `SideKnowledge` — `Tick`, `IsOver`, `Units`, `Victory`, and a `QueueOf` lookup, the same five
  facts `SideDirector` always read off `Simulation` — is the only thing `Decide` is handed.
  **Not an observation encoding**: `Units`/`Victory` stay ground truth, same as before this
  refactor; #101, if it lands, defines the belief-correct shape separately. `Simulation.Director`
  stays typed as the concrete `SideDirector` (an `as` cast over the policy field) because
  existing callers read `OrdersIssued` and the save/load retry memory off it specifically;
  `Simulation.SetPolicy` is the general seam, and `DirectorProbe`'s
  `NoSimulationStubPolicy` — a `SideId` field and nothing else — is plugged in through it as the
  direct disproof of the issue's own stated failure mode.
- **Breaking contact withdraws; it does not merely cease fire.** An Abort alone left the unit
  standing where it was, to be destroyed a few seconds later.
- **`VictoryEvaluator` is handed its objectives, never fetching them.** They are scenario data
  today and will not stay so — under the command-chain model an objective is the content of a
  *directive*, so "the objectives in force for this side" has to be able to change mid-scenario.
  A constructor argument survives that; a static reach-in does not.
- **Objective control: uncontested presence takes, contested freezes, ownership is sticky.**
  Arriving is not taking — a side takes ground by having a living unit on it with no living
  enemy on it, so an objective must be *cleared*. Walking off does not hand it back, which is
  what makes holding worth doing.
- **Ownership is a sampled-position check, not a swept-path one, and that is a bound an author
  must respect.** `UpdateOwnership` reads each unit's live `.Cell` only on the tick `Evaluate`
  runs, `EvaluationInterval` ticks apart — no movement trail, no swept-segment test — so a
  unit could in principle cross an objective between samples and never be seen there for the
  purpose of *taking* it. The rule: **ownership sampling is sound only while an objective's
  diameter takes at least one full evaluation interval to cross at the fastest catalogue road
  speed.** Today's numbers hold it, but only by authored coincidence: the fastest catalogue
  unit, Recon, covers `RoadSpeedMps = 20` at `MetresPerCell = 25` — 0.8 cells/tick, 8 cells
  over the 10-tick gap — against the smallest shipped objective, OBJECTIVE ANVIL, at
  `RadiusCells: 10` (20 cells across, 25 ticks to cross). 25 > 10, so a sample must land
  inside any transit. `Scenario.ValidateVictory` rejects only `RadiusCells <= 0`; nothing
  enforces the bound above — break-even is `RadiusCells = 4`, still sound, and anything
  smaller is legal and crossable unseen.
- **The occupancy clock is not on that sampled cadence, and #91 is why it no longer can be.**
  `_occupiedSince` — what a `HoldObjectives` condition's clock actually reads — used to be set
  and cleared only inside the same sampled sweep as ownership, so a unit that stepped off an
  objective and back on **between two samples** was never observed absent and the whole
  excursion counted as unbroken hold, silently, at up to `EvaluationInterval` granularity.
  `UpdateOccupancy` now runs every tick, above `Evaluate`'s sampling gate, and only reads
  `_owner` — it never mutates it — so running it more often than ownership changes costs an
  extra `O(objectives x units)` sweep per tick and moves nothing about *when* ownership itself
  transfers. `VictoryProbe.CheckLeaveAndReturnWithinWindow` is what proved this red before it
  was fixed: a leave-and-return timed to land entirely inside one sampled window, asserting
  the clock reflects the second arrival rather than the first.
- **`DestroyEnemy` measures against STARTING strength, captured once at construction.** Against
  a side's current total a force can never fall below a share of itself and the condition never
  fires.
- **Victory precedence is a `Priority` field, ties broken by authored list order.** Two
  conditions can come true on the same evaluation, and "whichever the loop reached first" makes
  the winner a function of list order. Evaluation tests every condition, not the first match.
- **Condition-testing and the victory decision run every `EvaluationInterval` ticks, and that
  constant is not a setting** — changing it changes when a satisfied hold duration is *seen*,
  which is an outcome, not a preference. This no longer covers the occupancy clock itself
  (`_occupiedSince`, see above) — only when `Evaluate` next checks whether a condition has been
  satisfied for long enough, which can now lag the clock reaching `HoldTicks` by at most
  `EvaluationInterval` ticks rather than silently missing a broken hold altogether.
- **`Simulation.Signature()` is what the divergence tests compare.** It covers unit state,
  queue state *and* the report log — a run that lands units correctly but reports
  differently has diverged in what its commander knows, which is exactly what an AI will
  act on.
- **Not everything that must replay correctly belongs in `Signature()`.** `DirectiveResponseLog`
  (#94) is deliberately absent: an acknowledgement already produces a `DirectiveAcknowledged`
  report, which `ReportLog.Signature()` already covers, so folding the response log in as well
  would assert the same fact twice and move every archived baseline for no new coverage. The
  same reasoning already covered `_acknowledgedDirectives` staying outside `Signature()` — this
  is not a new exception, it is the same one applied to the log that makes that set
  reconstructible on replay.
- **`Signature()` is a divergence oracle, not a completeness oracle, and #74 (save/load) is what
  that distinction cost.** It exists to answer "did two runs of the *same code* diverge", so it
  deliberately omits anything derivable and anything that cannot differ *within* one run — and
  both of those are exactly the state a snapshot is most likely to drop, because a round-trip
  signature comparison cannot see either omission. **Not covered, and each needed its own
  restore path rather than a mention in a checklist:** `_acknowledgedDirectives` (derivable from
  `DirectiveResponseLog`, already documented above); `_openingReported` (derivable from
  `ReportLog` — every `ReportKind.Engaged` entry's `AboutCommand` is the `Command.Seq` that
  opened it — and found only on a second read of this file after the first version of #74's
  probe was already green: left unreconstructed, a restored simulation republishes the
  opening-fire report on the very next tick of any engagement still running across the snapshot
  boundary, which "check what exists; do not assume the list is complete" is the standing
  warning against); `ContactTracker`'s `_seen` matrix and its `_pending` held-back reports — the
  player's *knowledge*, including a green observer's contact that has been seen but not yet
  reported, which restoring an empty tracker both forgets and silently drops; `VictoryEvaluator`'s
  `_startingStrength` baseline, fixed once at construction and never revisited — a restored
  `Simulation`'s constructor runs again and would recompute it from whatever `Scenario.Units`
  currently holds, which by save time is already-damaged current strength, not the true tick-zero
  baseline, silently changing what "reduced below 25%" means for the rest of the run without
  moving `Signature()` at the moment of restore at all; `SideDirector`'s per-unit retry memory,
  which is *not* derivable the way `ReactionController`'s picture is — a director-issued order and
  a player-issued one are both logged under the same `ActorId.ForSide`, so there is no way to
  tell them apart by reading `CommandLog` back; every `UnitInstance` field outside the six
  `Signature()` reads (`Cell`, `Strength`, `Readiness`, `Suppression`, `Posture`,
  `Supply.Ammunition`) — `Training`, `Roe`, `DestroyedAtTick`, and three of `SupplyLevels`' four
  classes (`Rations`, `Water`, `Fuel`); the full `CommandLog`/`ReportLog`/`DirectiveLog` history,
  since `Signature()` folds in the *queue's* signature and the report/directive logs' own
  signatures, not "every order ever issued" — a completed or aborted order that has left the
  queue leaves the live-queue signature identical whether or not the history behind it survived;
  and messages sitting in a `MessageBus<T>`'s pending inbox at the exact tick a snapshot is
  taken — published, so the log already holds them, but not yet delivered, so no consumer has
  acted on them, and a restore that drops them silently skips one step of in-flight orders,
  reports or directives. **The `readonly struct Casualty` bug is the same shape at the
  serialisation layer**: see `docs/unity-gotchas.md`. None of this shows up as a red assertion in
  a signature comparison taken *at the moment of restore* — several of these rows only diverge
  once the restored run is stepped further (the acknowledgement guard and the victory baseline
  both do), which is why `SaveLoadProbe` asserts a stepped-forward comparison as well as an
  immediate one, and a dedicated assertion per row besides.
- **`CancelFrom` addresses a `QueuedCommand.Ordinal`, not a live-list position, and resets
  posture exactly when `Abort` would (#56/#57).** A position goes stale the instant an earlier
  entry completes: `CommandQueue.Finish` shifts everything behind it down one, so a `CancelFrom`
  captured against row 2 of a four-leg plan and delivered a tick later — after the head has
  finished — lands on row 1 instead, silently. `Ordinal` is a per-queue counter `Enqueue` and
  `InsertFront` hand out once and never reuse or shift; `CancelFrom(ordinal)` cancels the first
  live entry whose ordinal is at or past the one given, and everything after it, which is the
  entry the caller meant regardless of what has completed and left the queue since. Cancelling
  the entry actually executing must halt and reset posture the same way `Abort` does — otherwise
  a unit cancelled out of a running `MoveTo` stays flagged `Posture.Moving` for ever, taking
  `EngagementResolver`'s 1.25x posture factor while standing still — but cancelling only a
  still-pending tail must touch neither posture nor the report log, since nothing was actually
  under way. `CommandQueue.CancelFrom` reports which case it was through an `out bool
  executingCancelled` rather than making `Simulation.OnCommandDelivered` re-derive it from
  whether anything at all was cancelled, which is the trap: a pending-tail cancel also returns a
  nonzero cancelled count.
- **`CommandProbe`'s rescued #56 fixture had an off-by-one — a redundant setup `sim.Step()`
  the fix itself could not satisfy.** `FirstUnit` in `CommandProbe.NewSim()` is a fully-trained
  company (`Training = 100`, so `HesitationTicks = 0`), which means delivery and the start of
  execution land on the same tick — a second "settle in" step before checking `Posture.Moving`
  was already redundant. Worse, that redundant step pushed the cancel-delivery tick to exactly
  tick 3, which collided with two things unrelated to either bug: `StubMoveExecutor`'s own
  `TicksToComplete = 3` naturally finishing whatever was executing that same tick (masking a
  correct pending-tail cancel behind a real, unrelated completion), and the Skirmish scenario's
  own first detection sweep, which produces three `Contact` reports on tick 3 regardless of what
  `CancelFrom` does. Both were confirmed independent of the fix by running the fixture against
  broken and correct `CancelFrom` implementations alike and observing identical pollution either
  way. One step, not two, avoids both.

---
