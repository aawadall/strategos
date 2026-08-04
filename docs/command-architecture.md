# Strategos — Command Topics and Unit Queues

How orders reach units, how reports reach the commander, and the rules that keep both
deterministic.

> This document is the reference for the messaging architecture: topics, message shapes,
> delivery and ordering. For build commands, rendering invariants and known traps, see
> [CLAUDE.md](../CLAUDE.md). For the phase breakdown this feeds, see
> [phases.md](phases.md) §5.

**Status: built, and this describes the code.** The command and situation topics, the
queues, the delivery rules, the command log and the report log are implemented under
`Assets/Scripts/Core/Messaging/`, `Core/Commands/` and `Core/Reports/`, and are covered by
`CommandProbe` and `ReportProbe`. Directives from higher (#73) ride a third topic
(`DirectiveBus` under `Core/Directives/`); the player sits as a node between that inbound
face and outbound orders (#36).

Three things below are still forward-looking and say so where they appear: the **C3 section**
(noise, latency, hijack), **mid-run FRAGO streams from higher**, and everything filed under a
later phase. The message shapes carry the fields those need — `ObservedTick`, `Confidence`,
`TargetGroup`, `ParentId` — with no behaviour behind them yet, deliberately: a field that is
currently a copy is cheap, and a message format that cannot express staleness has to be
changed everywhere at once.

---

## Why messaging at all

A direct call from the UI into a unit is the obvious implementation and the wrong one,
because four separate things in the roadmap all need to observe or inject orders:

| Wants | Needs |
|---|---|
| Drawing in-progress actions (#10) | to see orders as they are issued |
| Replay of saved battles (vision, Phase 8) | to record orders as data |
| Online multiplayer (Phase 7) | to ship orders over a wire |
| AI (Phase 8) | to issue orders through the same path a player does |

Each is painful to retrofit onto direct calls and nearly free if an order is a message. The
messaging layer is not decoupling for its own sake — it is the one shape that serves all
four.

---

## Topology

Three topics, matching the direction of real command flow. The player is a **node** in the
chain (#36): directives arrive from higher on one face; orders leave to subordinates on
another. Reports still climb the situation topic.

```
                    ┌─────────────────────┐
   higher HQ ──────►│   DIRECTIVE topic   │────► player (acknowledge; never auto-decompose)
                    │   (intent down)     │────► UI            (read-only)
                    └─────────────────────┘────► recorder      (read-only)

                    ┌─────────────────────┐
   player / AI ────►│   COMMAND topic     │────► units (filter by addressee)
                    │   (orders down)     │────► UI            (read-only)
                    └─────────────────────┘────► recorder      (read-only)

                    ┌─────────────────────┐
   units ──────────►│   SITUATION topic   │────► UI            (read-only)
                    │   (reports up)      │────► other units   (reaction, Phase 5/8)
                    └─────────────────────┘────► intel system  (Phase 5.3)
```

A directive is **not** an order: publishing one on the command topic would hit formation
decomposition and quietly issue moves the player never chose (#73). Orders addressed above
the player's authored or derived echelon (`Scenario.PlayerEchelon` /
`CommandScope`) are refused at `Simulation.Issue` and hidden in PLAY (#268).

This mirrors C2 doctrine rather than merely borrowing its vocabulary: intent from higher,
orders downward to an addressee, reports upward and outward. The topology and the subject
matter agree, which is what makes the later phases fit without reshaping it.

### Player as a node — two interfaces

| Face | Topic | What the player does |
|---|---|---|
| In | Directive | Receive one opening directive; acknowledge (no refuse path in v1) |
| Out | Command | Issue / Abort / CancelFrom to units at or below their echelon band |

Career rank (#76) authorizes the seat; `PlayerEchelon` *is* the seat. Zoom uses the same
band. Mid-run re-task from higher (a live FRAGO stream onto `DirectiveBus`) is deferred —
v1's plan cut is `CancelFrom` on the command topic, and higher's voice is the single
scenario directive published at start (#269).

---

## Message shapes

### Command

```
Command
    ulong        Seq          total order — the basis of determinism
    int          Tick         simulation step it takes effect on
    UnitId?      TargetUnit   addressee; null = formation or broadcast
    FormationId? TargetGroup
    CommandKind  Kind         MoveTo, Action, Abort, Hold, CancelFrom, …
    <payload>                 per kind: destination, action id, queue index
```

`Seq` is a monotonic sequence number, **not** a timestamp. Wall-clock ordering differs
between machines and defeats replay.

`TargetUnit` / `TargetGroup` exist from the start even though every unit will initially
subscribe to everything and filter locally. That is correct at sandbox scale and wrong at
the theatre scale the roadmap ends at; having the addressee on the message means routing
becomes an optimisation later rather than a message-format change.

### Situation report

```
SituationReport
    ulong          Seq
    int            Tick
    UnitId         Source        who observed it
    int            ObservedTick  when — may precede Tick if reporting is delayed
    ReportKind     Kind          Contact, Arrived, Engaged, Halted, Depleted, …
    Confidence     Confidence    Confirmed | Suspected | Possible
    <payload>
```

`Source`, `ObservedTick` and `Confidence` carry no behaviour yet. They are three fields now
because Phase 5.3 wants confidence levels and multi-source fusion, and a report that does
not record who saw a thing and when cannot be fused afterwards.

---

## Per-unit command queues

The topic is the **transport**. The queue is the unit's **plan**.

A unit subscribes to the command topic, keeps the commands addressed to it, and appends
them to its own ordered queue. Command *n+1* begins when *n* completes.

```
U1  [1] MoveTo(X,Y)   Executing
    [2] Action A2      Pending
    [3] MoveTo(P,Q)    Pending
```

Each command carries a status: `Pending → Executing → Completed | Cancelled | Failed`.

**Control commands are a distinct kind.** `MoveTo` and `Action` act on the world; `Abort`,
`Hold` and `CancelFrom` act on the queue. Keeping them separate in the type system stops
cancellation being threaded as a special case through every executor.

---

## Delivery and ordering rules

These four are what make the difference between a bus that enables replay and one that
quietly prevents it.

Both topics are one generic `MessageBus<T>`, and that is not incidental. Two hand-written
copies would be two places for "publish to the next step" to drift, and a drift here does
not throw — it produces a replay that diverges some number of steps after the change that
caused it. `CommandBus` and `ReportBus` are named subclasses rather than raw instantiations
so that publishing a report onto the command topic is a compile error and not a surprise.

### 1. Publish to the next step

**A message published during step N is delivered at step N+1.** Never within the step that
produced it.

Without this, a report can trigger a reaction that publishes a command that triggers
another report, all inside one step — unbounded, and ordered by whatever the dispatcher
happened to do first.

The mechanism doubles as a feature. Phase 5.2 wants communication range by echelon,
degradation in terrain, and order propagation delay; a uniform one-step delay is the
degenerate case of exactly that. Later the delay varies by distance, echelon and terrain
instead of being constant. Determinism now, realism later, one mechanism.

### 2. Dispatch synchronously, in a defined order

Within a step, subscribers are invoked in a declared, stable order. Not async, not
thread-pooled, not "whenever". `DeterministicRandom` is integer-only PCG precisely so
results are platform-stable; asynchronous dispatch would throw that away at the layer
above.

### 3. Only the owner mutates

Any subscriber may observe. Only a unit may modify its own queue, and only the executor may
change world state. If a UI or cross-cutting handler can mutate simulation state, dispatch
order becomes semantics — and the resulting bugs reproduce only at one interleaving.

### 4. Subscribe for events, read for state

If a subscriber has to *remember* what it heard in order to stay correct, it should be
reading state instead.

The UI draws arrows by walking each unit's queue every frame — always correct, no
reconstruction, nothing to miss. It does **not** rebuild a shadow copy of the queue by
listening to `MoveTo` and `Abort`. That is the classic pub/sub failure: two components
holding the same truth, diverging silently.

Subscribe for transient things — a flash, a sound, a log line. Read for anything durable.

---

## Abort and supersession

`Abort` is a queue operation delivered as a command. It affects commands that are **queued
but not yet executed**; it does not reach backwards into anything already done.

```
U1  [1] MoveTo(X,Y)   Executing   ← recon reports a trap at X,Y
    [2] Action A2      Pending
    [3] MoveTo(P,Q)    Pending

    → Abort

U1  [1] MoveTo(X,Y)   Cancelled
    [2] Action A2      Cancelled
    [3] MoveTo(P,Q)    Cancelled
```

This is a FRAGO in the Phase 5.1 sense: modify a plan without reissuing it. `CancelFrom(n)`
and insert/replace are the same mechanism at finer grain.

**The log is never rewritten.** `Abort` is appended at its own `Seq`. Replaying the log
reconstructs the queue *including* the fact that the plan was cut short and when — which is
what an after-action review needs in order to answer why U1 stopped short of X,Y.

---

## The command log

The ordered command stream **is** the log. There is no separate recording mechanism:
append-only, sequence-numbered, and replayable.

- **Replay** — re-read the stream.
- **Multiplayer** — ship the stream (Phase 7; see the caveat below).
- **AI training** — expose the stream at a process boundary (Phase 8).
- **After-action review** — read the stream.

**Replay is production code, not a test fixture.** `Strategos.Commands.Replayer` (`Core/Commands/`)
is the single definition of what replaying a run means, and it is what `CommandProbe`'s
divergence check calls — the check does not keep its own copy. Until #94, it did: the only
replay mechanism in this project was reimplemented privately inside the probe that tested it,
which meant the property "a recorded run replays" was never actually exercised on anything that
shipped. #94 found the gap because it is exactly the shape a directive's *response* falls
into: a directive itself is a message and replays like any other, but a player *acknowledging*
one is not on the command topic (see the C2 note above — a directive never reaches
`OnCommandDelivered`, so it gets neither a `CommandKind` nor a route through `Bus`) and so was
invisible to a replay driven from `CommandLog` alone. The command stream is still the one
`CommandLog` carries; `Replayer` additionally reads a second, parallel log —
`DirectiveResponseLog` — for exactly the class of player action that happens outside the command
topic on purpose, and drives both by tick with the same one-step delivery delay. Acknowledging
replays through `Simulation.AcknowledgeDirective` itself, never by re-publishing its report
directly, because that method is also where the idempotence guard over acknowledged directives
lives and rebuilds.

### Determinism caveat

Same build on the same machine: replay is achievable and enough for review and what-if
analysis.

**Cross-platform lockstep is a harder claim.** Integer-only PCG solves the RNG, but the
simulation does float arithmetic — elevation sampling, slope, distance — and float
determinism across platforms and compilers is an engineering problem, not a discipline
problem. Treat replay as achievable now and cross-platform lockstep as open until Phase 7
forces the question.

### Discipline required

Determinism is maintained, not added. In simulation code:

- No `Dictionary` / `HashSet` iteration — order is not guaranteed.
- No wall-clock, no `Time.deltaTime`, no `UnityEngine.Random`. Fixed-step ticks; presentation
  may interpolate between them.
- Nothing may read uncontrolled external state.

Write the divergence test with the first executor: run N steps, record, replay, assert
identical state. It is cheap to add early and close to impossible to retrofit confidence
into after something has quietly broken it.

---

## C3: noise, latency and hijack

The intended next phase is **C3** — Command, Control and *Communications* — meaning
messages that arrive late, arrive wrong, or do not arrive at all, and adversaries who
listen or inject.

### Why the topology already supports it

Because publication and delivery are separate concerns, a **channel** can be inserted
between them without either end knowing:

```
   publisher ──► [ CHANNEL ] ──► subscriber
                     │
                     ├─ delay        by echelon, distance, terrain
                     ├─ loss         message never arrives
                     ├─ corruption   message arrives altered
                     ├─ interception adversary receives a copy
                     └─ injection    adversary publishes as someone else
```

Delivery rule 1 — publish to the next step — is the degenerate case: a uniform one-step
delay. C3 makes the delay a function of echelon, distance and terrain, and adds the other
four effects alongside it. The mechanism does not change; only its parameters do.

This is why publication and delivery must stay separate even while comms are perfect. A
unit that polls world state directly cannot be deceived; a unit that acts on what it was
*told* can be.

### Determinism still applies

Loss and corruption are stochastic, and stochastic means replay-breaking unless it is
seeded. Derive every channel roll from `DeterministicRandom` keyed on the message's `Seq`,
never from ambient randomness. A dropped message must drop identically on every replay and
every machine, or none of the guarantees above survive.

### Provenance: the message shape changes

Hijack requires distinguishing **what a message claims** from **what is true**. A unit
believes an order came from its headquarters; the simulation knows it did not.

That means a claimed source separate from the actual one, and an authenticity state the
receiver cannot see but the simulation can. Worth knowing now because it is a change to the
message shape, which is the expensive kind.

### The consequence that breaks a rule

**Delivery rule 4 needs qualifying.** "Read for state" is right, but *whose* state?

While comms are perfect, a unit's queue and what the commander believes it is doing are the
same object. Under C3 they diverge — a unit may be executing a plan the commander does not
know about, or sitting idle because a FRAGO never arrived. At that point a UI reading the
unit's real queue is **cheating**: it renders ground truth the commander has not earned.

So the rule becomes: *read state, but read the observer's state.* There has to be a
command-post model — the commander's picture, built from reports received rather than from
the world.

The cheap preparation, worth doing now: route reads through an accessor named for the
observer (a believed-plan or command-post view), even though it trivially returns the real
queue today. Renaming a call later is easy; finding every place that quietly read ground
truth is not. This is recorded in #10.

### What C3 does *not* need

It does not need engagement resolution, victory conditions or AI. C3 is orthogonal to the
combat milestone and can be built before, after, or alongside it — the only hard
prerequisite is the command and situation topics themselves.

### Decision: C3 is deferred, and stays cheap if three seams are respected

C3 is a maturity feature, not a foundation. It is deliberately **not** scheduled — but it
only stays cheap if three things hold, and each is close to free now and expensive later.

**Genuinely additive later, no preparation needed:** the channel itself — delay, loss,
corruption, interception, injection — plus varying delay by echelon and terrain, and
EW/jamming/SIGINT as channel effects. All of it slots into an existing gap and touches
neither publisher nor subscriber.

**The three seams to respect now:**

1. **Publication and delivery stay separate.** Already delivery rule 1; zero extra cost.
2. **Commander-facing reads go through an accessor named for the observer**, even while it
   trivially returns the real queue. A naming decision, not code. Recorded in #10.
3. **Units act on messages received, not on polled world state.** Recorded as an acceptance
   criterion in #13 and #15.

The third is the one that matters. **The cost of adding C3 scales with how much code reads
ground truth directly** — near zero today, small after the sandbox, and a rewrite of the
reaction system once #12 and #13 exist written against the world rather than against
reports. A unit that asks *"is there a hostile in range?"* can never be deceived; one that
asks *"have I received a contact report?"* needs no change at all when the reports start
arriving late, wrong, or forged.

Respect those three and C3 is a feature. Ignore the third and it is a rewrite of the AI.

### Relation to the Byzantine Generals Problem

Worth stating precisely, because only part of C3 is Byzantine:

| Concern | Fault class | Byzantine? |
|---|---|---|
| Latency | Timing / asynchrony | No |
| Noise — loss, random corruption | Omission / crash | No |
| Hijack — forged or injected orders | Arbitrary / malicious | **Yes** |

Two differences matter more than the resemblance. BGP is about **consensus** among peers;
this is **command** down a hierarchy — there is no agreement protocol to run. And BGP exists
to *defeat* the problem, whereas a game exists to *simulate* it: the fog of command is the
product working. Do not implement Byzantine fault tolerance in the simulation.

Two things do transfer. **Authentication** — Lamport's result is that unforgeable signatures
collapse the problem, which is the design justification for separating a message's claimed
source from its actual one, and makes comms security a modellable capability rather than
bookkeeping. And **redundancy** — Phase 5.3's intel fusion across multiple sources with
confidence levels is soft Byzantine-tolerant sensing arrived at from doctrine rather than
from proofs.

The one place it becomes a real engineering constraint is Phase 7 multiplayer, where a
cheating client is a traitor in the technical sense — though games almost universally answer
that with an authoritative server validating the command stream rather than with BFT
consensus.

---

## Open decisions

Recorded rather than settled, because each affects behaviour a player will notice.

1. **Does `Abort` halt the executing command, or only cancel what follows?** Militarily
   "abort" means stop now, but a unit mid-river-crossing or already in contact cannot
   cleanly freeze. Proposal: interruptibility is a property of the command, halt-now the
   default, a few commands marked must-complete.
2. **What posture follows an abort?** A unit that stops because recon found a trap should
   probably go defensive rather than stand in march order. Abort may imply a posture
   change, not merely an emptied queue.
3. **Does a plan survive contact?** If a unit is engaged mid-plan, does the queue continue
   or suspend? This is the seam where autonomous reaction (#13) meets the queue.
4. **Turn, fixed tick, or continuous?** The whole design assumes a fixed step. Rules 1 and 2
   above depend on it. This is the largest open decision in the project and should not be
   made against a deadline.

---

## Out of scope

Deliberately absent, with the phase that owns each:

- Order propagation delay varying by echelon, distance or terrain — Phase 5.2.
- Mission-type orders (Attack, Defend, Delay, Screen …) — Phase 5.1.
- Graphic control measures ship (#160–#166): checkpoints, phase lines, boundaries, arrows,
  areas; PLAY `afterPixels` + opposing-side filter.
- Intelligence fusion and source correlation — Phase 5.3.
- An external message broker. Bridging the in-process stream to MQTT/NATS/gRPC is
  attractive for reinforcement-learning agents, but it belongs at the process boundary and
  only when Phase 8 asks for it. Not underneath the simulation.
- Snapshot, rewind and branching timelines. Considered and set aside: the requirement was a
  plan being cut short, which a queue with cancellation satisfies without any rollback
  machinery.

---

## Glossary — C2 and its extensions

The acronym grows as scope widens. Sources vary and the later numbers are not standardised.

| Term | Expansion |
|---|---|
| **C2** | Command and Control |
| **C3** | Command, Control, **Communications** |
| **C4** | C3 + **Computers** |
| **C5** | C4 + **Cyber** (US Army usage, e.g. the C5ISR Center). Also seen as *Combat Systems* in naval contexts, or *Collaboration* — the fifth C is the least settled |

Suffixes compose with any of the above:

| Suffix | Adds |
|---|---|
| **I** | Intelligence — C3I, C4I |
| **ISR** | Intelligence, Surveillance, Reconnaissance — C4ISR, C5ISR |
| **ISTAR** | ISR + Target Acquisition (British usage) |

**Where Strategos sits.** Phase 5 is titled "Command & Control (C2)", but its contents are
broader than C2 proper: §5.1 orders is C2, §5.2 communications adds the third C, and §5.3
intelligence adds the I. As specified, Phase 5 is **C3I**. The fourth C, Computers, is the
engine itself rather than a simulated element — though electronic warfare in §5.2 and any
future cyber dimension would push toward C5.

Worth knowing when naming things: calling the whole subsystem `C2` will under-describe it
by the time §5.3 lands.
