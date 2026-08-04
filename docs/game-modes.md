# Game modes

How PLAY is contested. **Read before changing mode-select, spectator, hotseat, or replay
chrome.** [CLAUDE.md](../CLAUDE.md) is the index. Phase checklist: [phases.md](phases.md) §7.

Epic [#287](https://github.com/aawadall/strategos/issues/287).

---

## ModeKind

`Strategos.Modes.ModeKind` — `Solo`, `Hotseat`, `Spectator`, `Replay`. Held on
`AppSession.PlayMode`. **Not** `MapRenderMode` (that is the topo palette dropdown).

| Mode | Who Issues | Directors | Fog / viewer |
|---|---|---|---|
| Solo | Player side | Opposing sides | `Scenario.PlayerSide` |
| Hotseat | Active hotseat side | None (v1) | Active hotseat side |
| Spectator | Nobody | Every side | Player side if set (watch-only) |
| Replay | Nobody (log feeds `Replayer`) | None | As recorded scenario |

Mode-select sits on the PLAY CAMPAIGN rail **before** scenario / campaign start buttons.
Changing mode does not reload the map by itself; the next `LoadScenario` /
`StartNamedCampaign` / `BindSimulation` applies director and Issue rules.

AI difficulty / personality (#291) live on the same rail and feed
`EnableDirector(..., DifficultyParams)` — see [ai-difficulty.md](ai-difficulty.md).

---

## BindSimulation rules

- **Solo** — today's path: `EnableDirector` on every side except `PlayerSide`, with the
  session's resolved `DifficultyParams`.
- **Spectator** — `EnableDirector` on **all** sides (same params); PLAY Issue chrome is inert
  (`IsPlayerCommanded` false).
- **Hotseat** — no director; `AppSession.HotseatSide` starts at `PlayerSide` (or first
  side); **SWITCH SIDE** flips who may be ordered and which side GCM fog uses.
- **Replay** — restore or keep a recorded `Simulation` with logs; a fresh target is
  stepped via `Commands.Replayer` (same path probes use). Issue chrome off.

Online multiplayer (§7.3) is out of scope for #287.

---

## Probe

`Strategos > Probe Game Modes` / `GameModesProbe.Run` — ModeKind names, spectator enables
two directors and records zero player Issues while an unattended skirmish decides, hotseat
side switch changes the command side id.
