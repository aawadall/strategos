# Build & verify

How to build, run and prove a change in Strategos.
**Read before running anything**; the traps here have each cost real time.
[CLAUDE.md](../CLAUDE.md) is the index.

---

## Build & verify

```powershell
.\scripts\build.ps1 -Target Windows64      # Windows64 | Linux64 | macOS | WebGL | All
.\scripts\capture.ps1 -View scenario       # launch player on one view, screenshot, close
```

**`build.ps1` waits for the editor; do not "simplify" it back to the call operator.**
`Unity.exe` is a GUI-subsystem binary and PowerShell does not wait for those, so
`& $UnityExe …` returns in about 0.1 s with the build still running — measured 0.1 s
versus 19 s after the fix. Anything sequenced after it then races the build, and
`capture.ps1` screenshots the *previous* player, so a UI change looks like it did
nothing. `-Wait` is also wrong: it waits on the whole process tree and Unity leaves
helper children alive, which hung a 19 s build for ten minutes. Use `-PassThru` plus
`WaitForExit()` on the returned object.

**`capture.ps1` verifies it actually captured the player.** `SetForegroundWindow` fails
silently when the caller is not itself the foreground process, and `CopyFromScreen` then
saves whatever window occupies those coordinates — it once saved an unrelated
application, which is indistinguishable from a catastrophically broken layout. It now
retries focus and errors out rather than saving a lie. If it reports focus failure,
something is stealing focus; it is not a UI bug. **Kill stray `Strategos` processes
before capturing** — a lingering window at the same coordinates will be photographed
instead.

`-View <key>` selects a view without driving the UI: `explore`, `symbols`, `map`,
`scenario`, `builder`. Add `-view3d` (passed straight to the player) to open the
scenario preview in 3D. `AppShell` logs `[AppShell] n view(s), showing 'key'` on start,
which is the cheap check that the shell came up at all.

**A batch build can silently ship the previous revision.** `BuildPlayer` will package
whatever is already in `Library/ScriptAssemblies`, so a build started right after an edit
can succeed, report success, and run your last change but one — twice in a row, which is
long enough to send you hunting for a bug in code that is not in the player. `GameBuild.Run`
now calls `AssetDatabase.Refresh()` first and refuses to build while
`EditorApplication.isCompiling`. If you are still unsure which revision you are looking
at, put something visible in the frame and confirm it: the map card's marginalia strip
carries seed and extent, which is exactly what it is for.

Bake a grid of symbol permutations — **prefer this over clicking the GUI** when checking
rendering changes:

```powershell
# Menu: Strategos > Bake Symbol Contact Sheet
& "C:\Program Files\Unity\Hub\Editor\6000.0.75f1\Editor\Unity.exe" `
    -batchmode -quit -nographics -projectPath . `
    -executeMethod Strategos.Editor.SymbolContactSheet.Bake -logFile sheet.log
# -> Artifacts/symbol-contact-sheet.png
```

Same idea for maps, and the same advice — a generator's output is a picture, so read the
picture. `MapContactSheet` bakes every relief profile against every render mode at 1 px
per cell, plus one map at 3 px per cell where labels and cased roads are checkable:

```powershell
# Menu: Strategos > Bake Map Contact Sheet
& "C:\Program Files\Unity\Hub\Editor\6000.0.75f1\Editor\Unity.exe" `
    -batchmode -quit -nographics -projectPath . `
    -executeMethod Strategos.Editor.MapContactSheet.Bake -logFile map-sheet.log
# -> Artifacts/map-contact-sheet.png, Artifacts/map-detail.png
```

It logs per-profile elevation range, feature counts and a landcover breakdown. Check
those numbers before reading the image: a landcover percentage that has moved says the
generator changed, where the image alone cannot tell you whether generation or the
palette moved.

The simulation has no picture to read, so it has probes instead. All four run under
`-batchmode -quit -nographics` and print a summary followed by `PROBE PASSED`/`FAILED`:

| `-executeMethod` | Asserts |
|---|---|
| `Strategos.Editor.MapMeshProbe.Run` | Drape mesh counts, extent, UVs, skirt, no NaN |
| `Strategos.Editor.ScenarioProbe.Run` | Round trip, and that it regenerates *the same ground* |
| `Strategos.Editor.CommandProbe.Run` | The four delivery rules, queues, A\*, replay divergence |
| `Strategos.Editor.ReportProbe.Run` | Detection edges, report timing, replay of reports |
| `Strategos.Editor.CombatProbe.Run` | The engagement matrix, terrain, simultaneity, replay |
| `Strategos.Editor.ReactionProbe.Run` | Each ROE, reflex preemption, break contact, fairness |
| `Strategos.Editor.VictoryProbe.Run` | Objective control, hold duration, draws, precedence |
| `Strategos.Editor.DirectorProbe.Run` | An unattended scenario reaches a decision |

**Run `CommandProbe`, `ReportProbe` and `CombatProbe` after touching anything under
`Core/Commands`, `Core/Reports`, `Core/Combat`, `Core/Movement` or `Core/Messaging`.**
Their divergence tests are the only thing standing between a determinism bug and finding
out months later that a replay does not reproduce — nothing about that failure is visible
at the time it is introduced.

**`CombatProbe`'s table is the point of it, not its pass/fail.** Balance the combat model by
reading the printed damage-per-minute matrix; the assertions only catch the model breaking,
not the model being wrong. It stamps landcover onto one fixed pair of cells rather than
hunting the map for a forest cell — searching varies elevation and distance along with the
cover, so "forest halves incoming fire" gets measured against a different slope on a
different hill, and the number cannot be attributed to anything.

Player log (**always check after a UI change**, see UI gotchas below):

```
%USERPROFILE%\AppData\LocalLow\DefaultCompany\strategos\Player.log
```

Editor menu: `Strategos → Build/…`, `NATO Symbol Generator`, `Open Demo Scene` (F5),
`Recreate Demo Scene`, `Bake Symbol Contact Sheet`, `Bake Map Contact Sheet`,
`Probe Map Mesh`, `Probe Scenario`, `Probe Commands`, `Probe Reports`,
`Write Sample Scenarios`, `Import TMP Essential Resources`.

---
