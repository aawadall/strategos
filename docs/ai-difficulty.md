# AI difficulty

How opposing `SideDirector` cadence and aggression are tuned. **Read before changing
director intervals, PLAY difficulty chrome, or `AiPresets`.** [CLAUDE.md](../CLAUDE.md) is
the index. Phase checklist: [phases.md](phases.md) §8.5. Modes that enable directors:
[game-modes.md](game-modes.md).

Epic [#291](https://github.com/aawadall/strategos/issues/291).

---

## What this is (and is not)

`DifficultyParams` retunes the existing reflex opponent — evaluation cadence, retry backoff,
and the Strength floor before a unit is sent forward. It is **not** planning AI, doctrine
trees, or trained personalities (those stay Phase 8 open work).

`SideDirector` still issues ordinary `Command`s through `Simulation.Issue`. Normal difficulty
matches the historical constants (`EvaluationInterval = 20`, `RetryInterval = 300`,
`MinStrengthPercent = ReactionController.BreakStrengthPercent`).

---

## Ladder and personality

| Difficulty | Eval interval | Retry | Min strength |
|---|---|---|---|
| Easy | 40 | 600 | 55% |
| Normal | 20 | 300 | break floor (35%) |
| Hard | 10 | 150 | 20% |

Personalities (`Balanced` / `Aggressive` / `Defensive`) multiply intervals and shift the
strength floor on top of the difficulty base via `AiPresets.Resolve`.

`AppSession.AiDifficulty` / `AiPersonality` hold the PLAY pick; `ResolvedDirectorParams()`
feeds `Simulation.EnableDirector(sides, params)`.

---

## PLAY chrome

On the CAMPAIGN rail, beside **PLAY MODE**: **AI DIFFICULTY** and **AI PERSONALITY**.
Applied on the next `BindSimulation` / scenario start (same as mode-select — changing the
dropdown mid-run does not rebuild an already-enabled director).

---

## Probe

`Strategos > Probe AI Difficulty` / `AiDifficultyProbe.Run` — Easy > Normal > Hard on
intervals and strength floor; personality shifts; session resolve; fixed-tick skirmish under
Hard issues more director orders than Easy.
