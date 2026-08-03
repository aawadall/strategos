# Changelog

All notable changes to Strategos are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); the project is still
pre-versioned (pre-production / playable sandbox). Issue numbers refer to
[github.com/aawadall/strategos](https://github.com/aawadall/strategos).

For the living task list see [ROADMAP.md](ROADMAP.md) and [docs/phases.md](docs/phases.md).
Open work is GitHub issues; deferred defects live in [docs/known-gaps.md](docs/known-gaps.md).

---

## [Unreleased]

### Added
- Shoulder-board insignia for the echelon the player commands — procedural marks from
  `Side.RankLadder` (US / Soviet JSON), shown in the AppShell top bar (#38).
- PLAY drill dropdown annotates each entry with the selected unit's T/P/U readiness and
  rebuilds on selection change (#97).
- GitHub Pages landing: Open Graph / Twitter Card meta and a header "View source" CTA
  (#120 — site GIF still open).
- Roadmap and phase checkboxes reconciled against the playable sandbox (2026-08-03 pass).

### Fixed
- CancelFrom posture and stale queue indices; paused-clock order visibility (#56, #57, #59).
- Objective hold clock leave-and-return sampling (#91).

---

## 2026-08-02

### Added
- Save / load a run (#74).
- Campaign chain data and ORBAT carry-over with three-operation probes (#75); PLAY entry
  still open (#114).
- Directives from higher — player as receiver; acknowledgements enter the command log
  (#73, #94).
- `ISidePolicy` seam — `SideDirector` and future agents behind one interface (#100).

---

## 2026-08-01

### Added
- Drill execution with group addressing (#77).
- Hold / Defend dig-in as a real order (#58); mission-type Defend / Occupy path (#72).
- Formation tree (`ParentId` real) (#70); fatigue / readiness decay (#67).
- Wrecks and `CasualtyLog` (#71).
- TTP binder (DRILLS view) (#61).

### Fixed
- Shipped `THE CROSSROADS` / objective placement on the erosion-on map (#95).
- Probes that skipped the shipped erosion setting (#96).
- Hold clock that kept running after ground was abandoned (#87).

---

## 2026-07-31 — Playable scenario sandbox

Sandbox milestone complete: scenario load, selection, A\* movement, direct fire, ROE
reflexes, situation reports, objectives / victory, opposing `SideDirector`, live plan and
abort, in-progress order drawing (#4–#15, #39, #48, #52).

---

*Started 2026-08-03. Earlier history lives in the git log and closed issues.*
