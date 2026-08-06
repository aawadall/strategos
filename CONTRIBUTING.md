# Contributing to Strategos

Thanks for looking at this. This file is an entry point, not the whole story — the same
rule [CLAUDE.md](CLAUDE.md) applies to itself. Read that file first; it indexes the
detail pages under `docs/` and states four rules that are cheap to follow and expensive
to discover. This file adds the process on top: getting set up, code standards, branching,
commits, and how the backlog is filed.

---

## Onboarding

1. **Install Unity 6 (`6000.0.75f1`)** via Unity Hub, with the Universal Render Pipeline
   module. This is the exact version the project is built and tested against — a newer
   6000.x may open the project but is not what CI or any contributor has verified.
2. **Clone and open** the project root in Unity Hub — there's no separate client project
   or submodule step.
3. **Confirm your setup works before writing any code.** Two ways, cheapest first:
   - In the editor: `Strategos → Open Demo Scene` (or press F5), press Play, and confirm
     `AppShell` logs `[AppShell] n view(s), showing '<key>'` in the Console. That's the
     cheap check that the tab shell and its views came up at all.
   - From the command line: `.\scripts\build.ps1 -Target Windows64` then
     `.\scripts\capture.ps1`, and open the resulting screenshot. This is also the
     workflow you'll use for every change from here on — see "Verify before you claim it
     works" below.
4. **Read `docs/build-and-verify.md` in full** before your first change. It documents the
   exact commands, the full probe table, and a list of traps that have each cost real
   time on this project (a batch build silently shipping the previous revision, enum
   renames breaking saved content, indexing `Scenario.Units` by position). Knowing these
   up front is much cheaper than rediscovering them.
5. **Pick something small first.** Prefer issues labelled
   [**good first issue**](https://github.com/aawadall/strategos/labels/good%20first%20issue)
   (docs, storefront copy, small site/media). After one PR, look at
   [**help wanted**](https://github.com/aawadall/strategos/labels/help%20wanted)
   (Unity loaders, splash, screenshots, audio tooling). Comment on the issue to claim it.
   `docs/known-gaps.md` and other `size:5m` tickets are also real backlog — not toys —
   but the labelled lists are the curated door for newcomers.

---

## Scope of contributions

The project is licensed under MIT terms with a **non-military-use restriction** — see
[LICENSE](LICENSE). Contributions are accepted on the same terms: this is a game, not a
training tool, and nothing contributed here may be aimed at operational military,
intelligence or security use. If you're unsure whether an idea fits that line, ask in the
issue before opening a PR.

Third-party assets (code, fonts, icons, audio, geodata) need an entry in
[ATTRIBUTIONS.md](ATTRIBUTIONS.md) in the same change that adds them — see that file's
own "How to Add a New Attribution" section.

---

## Standards

These are observed from the codebase as it exists today, not aspirational — read a
neighbouring file before deviating from what you see here.

- **Comments explain WHY, not WHAT.** Look at `Objective.cs`, `VictoryEvaluator.cs`, or
  `TtpView.cs`'s header for the pattern: a short block naming the non-obvious constraint,
  the reason a shortcut was rejected, or the bug a design choice prevents — never a
  restatement of what the next line of code obviously does. If removing a comment
  wouldn't confuse a future reader, it shouldn't be there.
- **Data is inert until something explicitly renders or bakes it.** The symbol composer
  and the map generator both build a data-only description first and rasterise it only at
  the very end (`NatoSymbolBaker.Bake`, `MapRasterizer.RenderPixels`). New systems that
  produce something visual should follow the same shape rather than drawing as they go.
- **No abstraction ahead of a second real use.** This codebase repeats three similar lines
  rather than introducing a premature helper, and doesn't build for a requirement that
  doesn't exist yet. Match that; don't generalise on the first use.
- **A check that cannot fail is a bug.** This project has shipped several probes and
  contact sheets that passed while proving nothing — see `docs/build-and-verify.md`'s
  notes on stale fixtures and guards that skip when data is uninteresting. When you add a
  probe or an issue's acceptance criteria, ask what would make it fail, and make sure that
  case is actually reachable.
- **Follow the local file's naming and structure before importing a convention from
  elsewhere.** There's no project-wide style doc beyond this one; consistency is enforced
  by matching what's already in the file and the directory, not by an external linter
  config.

---

## Verify before you claim it works

`CLAUDE.md` states four rules up front; they apply to every change, not just UI ones:

1. **Verify in the player, not by inspection.** Build (`scripts/build.ps1`), capture
   (`scripts/capture.ps1`), read the screenshot. Code review does not catch a symbol
   rendering at 6/255 alpha or an objective one cell inside a lake; one frame does.
2. **Check `Player.log` after any UI change.** A silent exception can truncate a whole
   layout to a background colour with nothing on screen and nothing in the build output.
3. **Run the probes for whatever you touched, and read their numbers, not just
   pass/fail.** `docs/build-and-verify.md` lists all of them and which ones share
   `EnableErosion = false` and why.
4. **A generator's output is a picture, so read the picture.** Contact sheets exist for
   symbols, maps and paper stock precisely so you don't have to click through the GUI to
   check a rendering change.

There's no CI gate to fall back on today — the Unity licence secrets `build.yml` needs
are unset, so a green check on this repo currently means the workflow didn't run, not
that it passed (`docs/known-gaps.md` records this). That makes the steps above the only
real gate; treat them that way.

---

## Branching strategy

Branch off `master`, named `<type>/<short-slug>`, matching whichever of these fits:

- `feat/…` — new capability
- `fix/…` — a bug fix
- `docs/…` — documentation only

Include the issue number in the slug when the branch closes one (`feat/env-lifecycle-104`
for #104); not required otherwise. Keep a branch scoped to one change — one feature, one
fix, or one docs update, not a bundle of unrelated edits, so the PR and its verification
stay legible.

Open the PR against `master` (not `main` — this repo's default branch is `master`).
Existing PRs in this repo merge via a merge commit (`git log` shows
`Merge pull request #N from …`), not squash or rebase — match that unless asked otherwise.

---

## Commits

Commit messages are short, imperative, present-tense statements of what changed — "Add
Pursue mission order", "Fix CancelFrom posture" — with the issue number in parentheses
where one applies. Look at `git log` for the actual register; it's plainer than most
open-source commit-message guides ask for, and that's deliberate: state what changed, not
why it's exciting.

---

## Docs are part of the change

If your change alters behaviour one of the pages in `CLAUDE.md`'s table covers —
`docs/simulation-invariants.md`, `docs/symbol-invariants.md`, `docs/map-invariants.md`,
`docs/ui-invariants.md`, `docs/campaign-invariants.md`, `docs/unity-gotchas.md`,
`docs/known-gaps.md` — update it **in the same PR**, not a follow-up. A note that's wrong
is worse than no note, because it's trusted. The same goes for `ROADMAP.md` and
`docs/phases.md` when a change closes or reshapes backlog they describe: check their
checkboxes and "Outstanding themes" list before opening the PR, not after.

---

## How to file issues

Check `docs/known-gaps.md` and the open issue list before filing — most things that look
like a gap have either already been recorded as a deliberate one, or already have an
issue. When you do file:

- **State what's actually true, verified against the code (`file:line`), not what you
  assume.** An issue that gets a claim about current behaviour wrong is worse than one
  that says less.
- **State what would make it wrong or make it fail.** Acceptance criteria that can't be
  failed are the same defect as a probe that can't fail.
- **Where something is genuinely undecided, present the options and don't manufacture a
  recommendation.** Plenty of existing issues do this; match their register rather than
  writing marketing copy or a hard sell for one approach.
- **Cross-reference the epic or parent issue if one exists**, and say explicitly how a
  new issue differs from anything adjacent to it — don't leave that inference to the
  reader.
- For a change of any real size, an epic-plus-children split (one issue framing the whole
  thing, separate issues for each independently doable piece, ordered by dependency) reads
  better than one large issue — see any of the epics already in the tracker for the shape.

---

## Getting oriented

`CLAUDE.md`'s path table is the map of the codebase; start there for "where does X live."
`docs/command-architecture.md` explains why orders and reports are messages rather than
direct calls, which is worth reading before touching anything under `Core/Commands`,
`Core/Reports` or `Core/Messaging`. Everything else is indexed from `CLAUDE.md` — this
file doesn't repeat it.
