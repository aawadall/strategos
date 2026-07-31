# Unity and VCS gotchas

Project-level traps: what must stay tracked, what must not go in
`Resources/`, serialisation, and TextMeshPro. **Read before adding an asset, a package or a
serialised type.**

[CLAUDE.md](../CLAUDE.md) is the index.

---

## Unity / VCS gotchas

- **`.meta` files and `ProjectSettings/` must stay tracked.** Unity stores asset GUIDs in
  `.meta` sidecars; without them a fresh clone regenerates GUIDs and every scene, prefab
  and asmdef reference breaks. Both were untracked before commit `5e20475`, so CI built a
  different project than local (default settings, no URP pipeline asset).
- **`Assets/TextMesh Pro/` is committed on purpose — do not re-ignore it.** See below.
- **Everything under `Assets/Resources/` ships in every build, unconditionally.** Only things
  that must be *loadable by name at runtime on every target* belong there: the drape shader
  (`Shader.Find` does not work in a player) and the scenario fixtures
  (`Resources` works on WebGL where `StreamingAssets` needs `UnityWebRequest`). It is not a
  general dumping ground — anything that can be a normal asset reference should be one.
- **Scenario JSON goes through Newtonsoft, not `JsonUtility`** (`com.unity.nuget.newtonsoft-json`,
  auto-referenced, no asmdef entry needed). `JsonUtility` cannot serialise `Nullable<T>` and
  `MapGenerationSettings.ParameterOverride` is a `ReliefParameters?`. **Newtonsoft serialises
  public properties as well as fields**, and Unity's maths types have properties returning
  their own type — `Vector2.normalized` is a `Vector2` — so it recurses until the depth
  limit. `ScenarioIO` fixes this two ways and both matter: explicit converters for `Vector2`
  and `Color`, and `FieldsOnlyResolver`, which serialises public non-readonly fields only.
  Without the resolver every computed property lands in the file, including
  `Scenario.IsValid`, which runs the whole validator on every save.
- **`com.unity.modules.screencapture` and `…imageconversion` are deliberately absent**, so
  `ScreenCapture.CaptureScreenshot` and `Texture2D.EncodeToPNG` do not exist. Screenshot
  from outside with `capture.ps1` rather than adding engine modules to every shipped build
  to serve a test harness.
- Binary assets go through Git LFS (`.gitattributes`). Verify with
  `git check-attr text filter -- <path>`.

---

## TextMeshPro gotchas

TMP ships its runtime assets in a `.unitypackage` that only a human clicking
*Window → TextMeshPro → Import TMP Essential Resources* unpacks. Without them:

- `TMP_Settings.instance` is `null`, and **`TMP_Settings.defaultFontAsset` throws rather
  than returning null** — guard it, or it takes down whatever is building the UI.
- `TextMeshProUGUI.Awake()` throws too, so the component renders nothing.

The resources are committed to avoid this. To regenerate: `Strategos → Import TMP
Essential Resources`. **`AssetDatabase.ImportPackage` is asynchronous** — a batch import
must *not* use `-quit` or the editor exits before the import runs; exit from the
`importPackageCompleted` callback instead (`TmpResources.ImportBatch`).

---

## C# language surface

**`init`-only setters do not compile.** Unity's scripting profile does not define
`System.Runtime.CompilerServices.IsExternalInit`, so `public string Code { get; init; }`
fails with `CS0518: Predefined type ... is not defined or imported` — a confusing error
that names a type nobody wrote. Use `{ get; set; }` and treat the property as write-once
by convention, or add the shim type yourself. `Ttp` takes the first option.

The same missing-shim problem takes out `record` and `with` expressions, for the same
reason and with the same error.

---

