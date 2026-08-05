# Unity CI licence secrets (#216 / #83)

Hosted GitHub Actions builds skip Unity until credentials exist. The workflow is
[`.github/workflows/build.yml`](../.github/workflows/build.yml). Preflight probes for
secrets; without them, **green CI means the Unity jobs did not run**.

[game-ci](https://game.ci/docs/github/activation) activation is what the Actions use.

---

## Secrets to set

Repo → **Settings → Secrets and variables → Actions → New repository secret**.

| Secret | Required? | Contents |
|---|---|---|
| `UNITY_LICENSE` | Prefer this | Full text of a personal `.ulf` licence file for Unity **6000.0.75f1** (or a base64 blob if you prefer — store what game-ci expects: typically the **decoded XML/ulf body**, matching the workflow comment) |
| `UNITY_EMAIL` | Alt path | Unity ID email |
| `UNITY_PASSWORD` | Alt path (with email) | Unity ID password |

Preflight treats credentials as present when **either**:

1. `UNITY_LICENSE` is non-empty, **or**
2. both `UNITY_EMAIL` and `UNITY_PASSWORD` are non-empty.

A licence file alone is enough; email/password alone is enough. Prefer `UNITY_LICENSE`
so CI does not depend on interactive account MFA quirks.

Do **not** commit `.ulf` files or passwords into the repo.

---

## How to obtain `UNITY_LICENSE`

1. Install / open Unity Hub with editor **6000.0.75f1** (same as `UNITY_VERSION` in
   `build.yml` and local Hub).
2. Editor → **Help → Manage License** (wording varies slightly by Hub version) → activate
   a **Personal** (or seat) licence and **save / export the licence file** (`.ulf`).
3. Open the `.ulf` in a text editor; copy the full contents into the `UNITY_LICENSE`
   secret value (no surrounding quotes).
4. Alternatively follow [game-ci activation](https://game.ci/docs/github/activation)
   (`unity-builder` / manual activation) if your org uses their floating/CI flow.

Personal licences are fine for public open-source CI; confirm seat policy if this moves
to a Unity Pro org later.

---

## Human checklist (ops)

- [ ] Confirm Hub editor version matches `6000.0.75f1`.
- [ ] Export / obtain `.ulf` (or decide on email+password path).
- [ ] Set `UNITY_LICENSE` **or** `UNITY_EMAIL` + `UNITY_PASSWORD` on
      `aawadall/strategos`.
- [ ] Run **Actions → CI — Build & Test → Run workflow** (`workflow_dispatch`) on `master`.
- [ ] Confirm Preflight logs `Unity credentials found` and a Build — Windows job runs
      (not skipped).
- [ ] On success, remove the CI skip note from [known-gaps.md](known-gaps.md) (or strike
      through) in a follow-up PR — only after a real green Unity job, not Preflight-only.

---

## What “done” means for #216

This issue is the **ops handoff document**: secrets names, how to get them, and the
verify steps above. Actually pasting secrets into GitHub is a human action outside git;
closing #216 when this page ships is correct. Enabling CI is verified when Preflight
flips and a matrix build runs.

---

## Cross-links

- Workflow header comment in `.github/workflows/build.yml`
- Skip behaviour: [known-gaps.md](known-gaps.md) (CI Unity activation)
- Local builds (no secrets): [build-and-verify.md](build-and-verify.md)
- Release epic: #83
