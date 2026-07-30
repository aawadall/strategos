# Strategos — Command Topics and Unit Queues

How orders reach units, how reports reach the commander, and the rules that keep both
deterministic.

> This document is the reference for the messaging architecture: topics, message shapes,
> delivery and ordering. For build commands, rendering invariants and known traps, see
> [CLAUDE.md](../CLAUDE.md). For the phase breakdown this feeds, see
> [phases.md](phases.md) §5.

**Status: design note, not yet implemented.** It exists so the reasoning survives outside
the conversation that produced it. Nothing below is in the codebase yet.

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

Two topics, matching the direction of real command flow:

```
                    ┌─────────────────────┐
   player / AI ────►│   COMMAND topic     │────► units (filter by addressee)
                    │   (orders down)     │────► UI            (read-only)
                    └─────────────────────┘────► recorder      (read-only)

                    ┌─────────────────────┐
   units ──────────►│   SITUATION topic   │────► UI            (read-only)
                    │   (reports up)      │────► other units   (reaction, Phase 5/8)
                    └─────────────────────┘────► intel system  (Phase 5.3)
```

This mirrors C2 doctrine rather than merely borrowing its vocabulary: orders are directed
downward to an addressee, reports flow upward and outward to whoever is listening. The
topology and the subject matter agree, which is what makes the later phases fit without
reshaping it.

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
- Graphic control measures: axes, phase lines, boundaries, engagement areas — Phase 5.1.
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
