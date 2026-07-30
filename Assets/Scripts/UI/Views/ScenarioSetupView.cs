// ScenarioSetupView.cs
// Configure the scenario's map, and preview it in 2D or 3D.
//
// GENERATION IS NOT LIVE, AND THAT IS DELIBERATE
// MapGenerator.Generate is synchronous on the main thread and erosion dominates its cost, so
// a 512-cell map with erosion and culture stalls for a second or more. Every control here is
// therefore discrete and nothing regenerates until GENERATE is pressed. The dirty indicator
// exists so that reads as intentional rather than broken.
//
// TWO PREVIEW IMAGES, NOT ONE
// Sheet2D must be aspect-cropped, because a stretched map misreports every distance on it.
// Sheet3D must NOT be cropped, because its RenderTexture is allocated at the frame's exact
// aspect. Keeping them as separate siblings makes each invariant structural instead of a mode
// branch inside per-frame crop code, lets the resize poller see only the 2D image, and gives
// each its own pointer handler — pan/zoom versus orbit.

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Strategos.Maps;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI.Views
{
    public sealed class ScenarioSetupView : MonoBehaviour, IAppView
    {
        /// <summary>Extents offered. Dropdowns, not a slider — see the header note.</summary>
        private static readonly int[] Extents = { 128, 192, 256, 384, 512, 768, 1024 };

        /// <summary>Above this, generation is slow enough to warn about.</summary>
        private const int WarnCells = 512;

        private static readonly float[] MetresPerCellChoices = { 5f, 10f, 25f, 50f, 100f };

        private static readonly (string label, int value)[] MeshDetail =
        {
            ("Low (96)", 96), ("Medium (192)", 192), ("High (384)", 384), ("Full (per cell)", 0),
        };

        private AppSession _session;
        private bool _suppress;
        private bool _dirty;
        private int _seenGeneration = -1;

        // Preview
        private RectTransform _previewFrame;
        private MapSheetCard _card;
        private GameObject _sheet3dGo;
        private RawImage _sheet3d;
        private MapDrapeStage _drape;
        private bool _is3d;
        private Image _tab2dFace, _tab3dFace;
        private TMP_Text _tab2dText, _tab3dText;
        private Vector2 _lastFrameSize;

        // Controls
        private TMP_InputField _nameInput, _seedInput;
        private TMP_Dropdown _widthDrop, _heightDrop, _mpcDrop, _profileDrop, _modeDrop, _meshDetailDrop;
        private Toggle _erosion, _culture;
        private Slider _exaggeration;
        private TMP_Text _extentLabel, _statusLabel, _generateLabel;
        private Button _generateBtn;
        private Image _generateFace;
        private TMP_Text _profileSectionNote;

        private Slider[] _reliefSliders;
        private Coroutine _generating;

        public string Title => "SCENARIO";
        public string Key => "scenario";
        public AppSession Session { set => _session = value; }

        // ─── IAppView ─────────────────────────────────────────────────────────

        public void Build(RectTransform host)
        {
            BuildUi(host);
            PopulateOptions();
            LoadProfileIntoSliders();
            Canvas.ForceUpdateCanvases();

            SetPreviewMode(AppShell.View3dRequested);
            RefreshPreview();
            UpdateExtentReadout();
            SetDirty(false);
        }

        public void OnShown()
        {
            if (_session != null && _session.Generation != _seenGeneration) RefreshPreview();
            else if (!_is3d) _card?.UpdateCrop();

            // Only now does the drape camera start rendering again.
            if (_is3d) _drape?.SetRendering(true);
        }

        public void OnHidden()
        {
            // Critical: a camera with a targetTexture renders every frame even when nothing
            // shows the result.
            _drape?.SetRendering(false);
            HideDropdownsIn(transform);
        }

        private void OnDestroy() => _card?.Dispose();

        private void Update()
        {
            if (_is3d) PollDrapeSize();
            else _card?.PollResize();
        }

        // ─── UI ───────────────────────────────────────────────────────────────

        private void BuildUi(RectTransform host)
        {
            var root = CreateRect("Root", host);
            Stretch(root);
            root.gameObject.AddComponent<Image>().color = Theme.StageBg;
            var rootH = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            rootH.childControlWidth = true;
            rootH.childControlHeight = true;
            rootH.childForceExpandWidth = false;
            rootH.childForceExpandHeight = true;

            BuildStage(root);
            BuildRail(root);
        }

        private void BuildStage(Transform root)
        {
            var stage = CreateRect("Stage", root);
            var le = stage.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minWidth = 480f;
            var v = stage.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(20, 20, 16, 16);
            v.spacing = 10;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            // Header: title, 2D/3D tabs
            var head = CreateRect("Head", stage);
            head.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;

            var title = CreateTmp("T", head, "SCENARIO PREVIEW", 15, FontStyles.Bold, withLayout: false);
            Stretch(title.rectTransform);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.color = Theme.Ink;
            title.characterSpacing = 6f;

            var tabs = CreateRect("Tabs", head);
            tabs.anchorMin = new Vector2(1, 0);
            tabs.anchorMax = new Vector2(1, 1);
            tabs.pivot = new Vector2(1, 0.5f);
            tabs.offsetMin = new Vector2(0, 0);
            tabs.offsetMax = new Vector2(0, 0);
            var tabsH = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabsH.spacing = 4;
            tabsH.childControlWidth = true;
            tabsH.childControlHeight = true;
            tabsH.childForceExpandWidth = false;
            tabsH.childForceExpandHeight = true;
            tabs.gameObject.AddComponent<ContentSizeFitter>().horizontalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            (_, _tab2dFace, _tab2dText) = AddTabButton(tabs, "2D", () => SetPreviewMode(false));
            (_, _tab3dFace, _tab3dText) = AddTabButton(tabs, "3D", () => SetPreviewMode(true));

            // Status line
            _statusLabel = CreateTmp("Status", stage, string.Empty, 12, FontStyles.Bold);
            _statusLabel.color = Theme.Accent;
            _statusLabel.characterSpacing = 2f;
            _statusLabel.GetComponent<LayoutElement>().preferredHeight = 18;

            // Preview frame holds both sheets as siblings.
            _previewFrame = CreateRect("PreviewFrame", stage);
            var fle = _previewFrame.gameObject.AddComponent<LayoutElement>();
            fle.flexibleHeight = 1f;
            fle.minHeight = 300f;

            _card = new MapSheetCard(_previewFrame, preferredHeight: 0, out _);

            _sheet3dGo = new GameObject("Sheet3D", typeof(RectTransform));
            _sheet3dGo.transform.SetParent(_previewFrame, false);
            var rt3 = (RectTransform)_sheet3dGo.transform;
            Stretch(rt3);
            rt3.offsetMin = new Vector2(2, 2);
            rt3.offsetMax = new Vector2(-2, -2);
            _sheet3d = _sheet3dGo.AddComponent<RawImage>();
            _sheet3d.color = Color.white;
            // uvRect stays the full 0-1 rect: the RenderTexture is allocated at this frame's
            // aspect, so cropping it would be wrong.
            _sheet3d.uvRect = new Rect(0, 0, 1, 1);
            _sheet3d.texture = Texture2D.whiteTexture;

            var region3d = _sheet3dGo.AddComponent<PointerRegion>();
            region3d.Dragged = e => _drape?.Drag(e.delta, e.button != UnityEngine.EventSystems.PointerEventData.InputButton.Left);
            region3d.Scrolled = e => _drape?.Zoom(e.scrollDelta.y);

            _drape = gameObject.AddComponent<MapDrapeStage>();
        }

        private void BuildRail(Transform root)
        {
            var panel = CreateRect("Rail", root);
            var le = panel.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 440;
            le.minWidth = 400;
            le.flexibleWidth = 0;
            panel.gameObject.AddComponent<Image>().color = Theme.RailBg;

            var edge = CreateRect("Edge", panel);
            edge.anchorMin = new Vector2(0, 0);
            edge.anchorMax = new Vector2(0, 1);
            edge.pivot = new Vector2(0, 0.5f);
            edge.sizeDelta = new Vector2(2, 0);
            edge.gameObject.AddComponent<Image>().color = Theme.CardLine;

            var chrome = CreateRect("Chrome", panel);
            chrome.anchorMin = new Vector2(0, 1);
            chrome.anchorMax = new Vector2(1, 1);
            chrome.pivot = new Vector2(0.5f, 1);
            chrome.sizeDelta = new Vector2(0, 38);
            chrome.gameObject.AddComponent<Image>().color = Theme.Accent;
            var cl = CreateTmp("L", chrome, "SCENARIO SETUP", 14, FontStyles.Bold, withLayout: false);
            Stretch(cl.rectTransform);
            cl.alignment = TextAlignmentOptions.Center;
            cl.color = Theme.AccentText;
            cl.characterSpacing = 6f;

            var content = UiScroll.CreateColumn("Scroll", panel, Theme.RailBg, out var scroll);
            var srt = (RectTransform)scroll.transform;
            srt.offsetMin = new Vector2(2, 0);
            srt.offsetMax = new Vector2(0, -38);

            AddSection(content, "SCENARIO");
            _nameInput = AddInput(content, "NAME", "SCENARIO", MarkDirty);
            _seedInput = AddInput(content, "SEED", AppSession.DefaultSeed.ToString(), MarkDirty);
            AddButton(content, "RANDOMISE SEED", RandomiseSeed);

            AddSection(content, "EXTENT");
            _widthDrop  = AddDropdown(content, "WIDTH (CELLS)", OnExtentChanged);
            _heightDrop = AddDropdown(content, "HEIGHT (CELLS)", OnExtentChanged);
            _mpcDrop    = AddDropdown(content, "METRES PER CELL", OnExtentChanged);
            _extentLabel = CreateTmp("Extent", content, string.Empty, 11, FontStyles.Bold);
            _extentLabel.color = Theme.InkMuted;
            _extentLabel.characterSpacing = 1.5f;
            _extentLabel.GetComponent<LayoutElement>().preferredHeight = 20;

            AddSection(content, "TERRAIN");
            _profileDrop = AddDropdown(content, "RELIEF PROFILE", OnProfileChanged);
            _erosion = AddToggle(content, "EROSION (SLOWER)", true, MarkDirty);
            _culture = AddToggle(content, "SETTLEMENTS AND ROADS", true, MarkDirty);

            AddSection(content, "ADVANCED RELIEF");
            _profileSectionNote = CreateTmp("Note", content, "FROM PROFILE", 10, FontStyles.Italic);
            _profileSectionNote.color = Theme.InkMuted;
            _profileSectionNote.GetComponent<LayoutElement>().preferredHeight = 16;
            BuildReliefSliders(content);
            AddButton(content, "RESET TO PROFILE", ResetToProfile);

            AddSection(content, "PRESENTATION");
            _modeDrop = AddDropdown(content, "RENDER MODE", OnPresentationChanged);
            _meshDetailDrop = AddDropdown(content, "MESH DETAIL (3D)", OnMeshDetailChanged);
            (_exaggeration, _) = AddSlider(content, "VERTICAL EXAGGERATION", 1, 8, 2,
                OnExaggerationChanged, format: "0", suffix: "x");
            AddButton(content, "RESET 3D VIEW", () => _drape?.ResetView());

            _generateBtn = AddButton(content, "GENERATE", StartGenerate);
            _generateFace = _generateBtn.GetComponent<Image>();
            _generateLabel = _generateBtn.transform.Find("T").GetComponent<TMP_Text>();
        }

        /// <summary>
        /// The 15 ReliefParameters fields, in the order they act on the terrain.
        ///
        /// The ranges are chosen so that no profile's authored value is clamped, which is not
        /// a cosmetic concern: a clamped slider displays a value the profile does not hold,
        /// and the moment any slider is touched ApplySlidersAsOverride writes the clamped
        /// reading back and silently changes the terrain. The two that caught this out were
        /// TreelineFraction (up to 1.5) and SnowlineFraction (up to 2.0) — despite the name
        /// they are not 0-1 fractions, so a 0-100% slider pinned them at 100% and would have
        /// rewritten 1.5 as 1.0. Actual spans across the seven profiles:
        ///
        ///   base -40..400 m    relief 90..2000 m     ridged 0.05..0.85   warp 0.35..0.90
        ///   octaves 5..7       feature 150..260      droplets 18..120    talus 32..40 deg
        ///   sea 0..0.12        aridity 0.15..0.95    treeline 0.3..1.5   snowline 0.5..2.0
        ///   contour 5..50 m    settlements 0.02..0.11/kcell   river 0.0010..0.0035
        ///
        /// Settlements and the river threshold also need two decimals: at "0" format a real
        /// density of 0.09 per kilocell renders as a flat "0", which reads as "no towns" on a
        /// map that plainly has four.
        /// </summary>
        private void BuildReliefSliders(Transform content)
        {
            _reliefSliders = new Slider[15];
            int i = 0;
            _reliefSliders[i++] = S(content, "BASE ELEVATION", -200, 1500, "0", " M");
            _reliefSliders[i++] = S(content, "RELIEF", 10, 3000, "0", " M");
            _reliefSliders[i++] = S(content, "RIDGED WEIGHT", 0, 100, "0", "%");
            _reliefSliders[i++] = S(content, "WARP STRENGTH", 0, 150, "0", "%");
            _reliefSliders[i++] = S(content, "OCTAVES", 1, 12, "0", "");
            _reliefSliders[i++] = S(content, "FEATURE SCALE", 16, 512, "0", " CELLS");
            _reliefSliders[i++] = S(content, "EROSION DROPLETS", 0, 300, "0", " /KCELL");
            _reliefSliders[i++] = S(content, "TALUS ANGLE", 5, 60, "0", " DEG");
            _reliefSliders[i++] = S(content, "SEA LEVEL", 0, 100, "0", "%");
            _reliefSliders[i++] = S(content, "ARIDITY", 0, 100, "0", "%");
            _reliefSliders[i++] = S(content, "TREELINE", 0, 300, "0", "%");
            _reliefSliders[i++] = S(content, "SNOWLINE", 0, 300, "0", "%");
            _reliefSliders[i++] = S(content, "CONTOUR INTERVAL", 1, 200, "0", " M");
            _reliefSliders[i++] = S(content, "SETTLEMENTS", 0, 0.5f, "0.00", " /KCELL");
            _reliefSliders[i]   = S(content, "RIVER THRESHOLD", 0, 2, "0.00", "%");
        }

        private Slider S(Transform parent, string label, float min, float max,
            string format, string suffix)
        {
            var (slider, _) = AddSlider(parent, label, min, max, min,
                OnReliefSliderChanged, format: format, suffix: suffix, wholeNumbers: false);
            return slider;
        }

        // ─── Options ──────────────────────────────────────────────────────────

        private void PopulateOptions()
        {
            _suppress = true;

            var extentLabels = new string[Extents.Length];
            for (int i = 0; i < Extents.Length; i++)
            {
                extentLabels[i] = Extents[i] > WarnCells
                    ? $"{Extents[i]}  (SLOW)"
                    : Extents[i].ToString();
            }

            int defaultExtent = System.Array.IndexOf(Extents, AppSession.DefaultCells);
            SetDrop(_widthDrop, extentLabels, defaultExtent);
            SetDrop(_heightDrop, extentLabels, defaultExtent);

            var mpcLabels = new string[MetresPerCellChoices.Length];
            for (int i = 0; i < MetresPerCellChoices.Length; i++)
                mpcLabels[i] = $"{MetresPerCellChoices[i]:0} M";
            SetDrop(_mpcDrop, mpcLabels, 2);   // 25 m

            SetDrop(_profileDrop, DisplayNames.ProfileLabels(), 0);
            SetDrop(_modeDrop, DisplayNames.RenderModeLabels(), 1);   // Topographic

            var detailLabels = new string[MeshDetail.Length];
            for (int i = 0; i < MeshDetail.Length; i++) detailLabels[i] = MeshDetail[i].label;
            SetDrop(_meshDetailDrop, detailLabels, 1);   // Medium

            _suppress = false;
        }

        /// <summary>
        /// Pushes the selected profile's parameters into the sliders and CLEARS the override.
        ///
        /// ResolveParameters is `ParameterOverride ?? ReliefProfiles.For(Profile)`, so the
        /// override replaces the profile wholesale. The classic bug is a stale override
        /// silently pinning the old values after a profile change, which is why selecting a
        /// profile is a one-way load and only touching a slider re-establishes an override.
        /// </summary>
        private void LoadProfileIntoSliders()
        {
            var p = ReliefProfiles.For(SelectedProfile);
            _suppress = true;

            Set(0, p.BaseElevationMetres);
            Set(1, p.ReliefMetres);
            Set(2, p.RidgedWeight * 100f);
            Set(3, p.WarpStrength * 100f);
            Set(4, p.Octaves);
            Set(5, p.FeatureScaleCells);
            Set(6, p.ErosionDropletsPerKiloCell);
            Set(7, p.TalusAngleDegrees);
            Set(8, p.SeaLevelFraction * 100f);
            Set(9, p.Aridity * 100f);
            Set(10, p.TreelineFraction * 100f);
            Set(11, p.SnowlineFraction * 100f);
            Set(12, p.ContourIntervalMetres);
            Set(13, p.SettlementsPerKiloCell);
            Set(14, p.RiverThresholdFraction * 100f);

            _suppress = false;

            _session.Settings.ParameterOverride = null;
            if (_profileSectionNote != null) _profileSectionNote.text = "FROM PROFILE";

            // SetSliderValue, not SetValueWithoutNotify: the latter leaves the numeric
            // label showing the old value.
            void Set(int i, float value) => SetSliderValue(_reliefSliders[i], value);
        }

        /// <summary>Collects all 16 sliders into a ReliefParameters and installs it as the override.</summary>
        private void ApplySlidersAsOverride()
        {
            var p = ReliefProfiles.For(SelectedProfile);

            p.BaseElevationMetres        = _reliefSliders[0].value;
            p.ReliefMetres               = _reliefSliders[1].value;
            p.RidgedWeight               = _reliefSliders[2].value / 100f;
            p.WarpStrength               = _reliefSliders[3].value / 100f;
            p.Octaves                    = Mathf.RoundToInt(_reliefSliders[4].value);
            p.FeatureScaleCells          = _reliefSliders[5].value;
            p.ErosionDropletsPerKiloCell = _reliefSliders[6].value;
            p.TalusAngleDegrees          = _reliefSliders[7].value;
            p.SeaLevelFraction           = _reliefSliders[8].value / 100f;
            p.Aridity                    = _reliefSliders[9].value / 100f;
            p.TreelineFraction           = _reliefSliders[10].value / 100f;
            p.SnowlineFraction           = _reliefSliders[11].value / 100f;
            p.ContourIntervalMetres      = _reliefSliders[12].value;
            p.SettlementsPerKiloCell     = _reliefSliders[13].value;
            p.RiverThresholdFraction     = _reliefSliders[14].value / 100f;

            _session.Settings.ParameterOverride = p;
            if (_profileSectionNote != null) _profileSectionNote.text = "PROFILE (MODIFIED)";
        }

        private ReliefProfile SelectedProfile =>
            Pick(DisplayNames.Profiles, _profileDrop, ReliefProfile.Rolling);

        private int SelectedWidth  => Extents[Mathf.Clamp(_widthDrop.value, 0, Extents.Length - 1)];
        private int SelectedHeight => Extents[Mathf.Clamp(_heightDrop.value, 0, Extents.Length - 1)];

        private float SelectedMetresPerCell =>
            MetresPerCellChoices[Mathf.Clamp(_mpcDrop.value, 0, MetresPerCellChoices.Length - 1)];

        private int SelectedMeshDetail =>
            MeshDetail[Mathf.Clamp(_meshDetailDrop.value, 0, MeshDetail.Length - 1)].value;

        // ─── Control handlers ─────────────────────────────────────────────────

        private void MarkDirty()
        {
            if (_suppress) return;
            SetDirty(true);
        }

        private void OnExtentChanged()
        {
            if (_suppress) return;
            UpdateExtentReadout();
            SetDirty(true);
        }

        private void OnProfileChanged()
        {
            if (_suppress) return;
            LoadProfileIntoSliders();
            SetDirty(true);
        }

        private void OnReliefSliderChanged()
        {
            if (_suppress) return;
            ApplySlidersAsOverride();
            SetDirty(true);
        }

        private void OnMeshDetailChanged()
        {
            if (_suppress) return;
            // Mesh detail is a presentation choice, not a generation one: rebuild the drape
            // from the map already in hand.
            RefreshPreview();
        }

        private void OnExaggerationChanged()
        {
            if (_suppress) return;
            _drape?.SetVerticalExaggeration(_exaggeration.value);
        }

        private void OnPresentationChanged()
        {
            if (_suppress) return;
            _session.Mode = Pick(DisplayNames.RenderModes, _modeDrop, MapRenderMode.Topographic);
            RefreshPreview();
        }

        private void RandomiseSeed()
        {
            _seedInput.SetTextWithoutNotify(Random.Range(1, int.MaxValue).ToString());
            SetDirty(true);
        }

        private void ResetToProfile()
        {
            LoadProfileIntoSliders();
            SetDirty(true);
        }

        private void UpdateExtentReadout()
        {
            float km = SelectedWidth * SelectedMetresPerCell / 1000f;
            float kmH = SelectedHeight * SelectedMetresPerCell / 1000f;
            long cells = (long)SelectedWidth * SelectedHeight;
            _extentLabel.text = $"{km:0.#} × {kmH:0.#} KM   ·   {cells:N0} CELLS";
        }

        /// <summary>
        /// Marks settings changed. The lightened GENERATE face and the status line are what
        /// make "the preview is not live" read as a design decision.
        /// </summary>
        private void SetDirty(bool dirty)
        {
            _dirty = dirty;
            if (_generateFace != null)
                _generateBtn.colors = AccentButtonColors(
                    dirty ? Theme.Lighten(Theme.Accent, 0.14f) : Theme.Accent);
            if (_statusLabel != null)
                _statusLabel.text = dirty ? "SETTINGS CHANGED - PRESS GENERATE" : string.Empty;
        }

        // ─── Generation ───────────────────────────────────────────────────────

        private void StartGenerate()
        {
            if (_generating != null) return;
            _generating = StartCoroutine(GenerateRoutine());
        }

        /// <summary>
        /// Applies the settings and regenerates.
        ///
        /// The yield is not decoration: Generate blocks the main thread, so a label set in the
        /// same frame never reaches the screen. One frame of latency buys an honest progress
        /// indicator for a stall that can run into seconds at 1024 cells with erosion.
        /// </summary>
        private IEnumerator GenerateRoutine()
        {
            _generateBtn.interactable = false;
            _generateLabel.text = "GENERATING...";
            _statusLabel.text = "GENERATING - THE WINDOW WILL NOT RESPOND";
            yield return null;

            ApplySettings();
            _session.Generate();

            _generateLabel.text = "GENERATE";
            _generateBtn.interactable = true;
            SetDirty(false);

            RefreshPreview();
            _generating = null;
        }

        private void ApplySettings()
        {
            var s = _session.Settings;
            s.Name = string.IsNullOrWhiteSpace(_nameInput.text) ? "SCENARIO" : _nameInput.text;
            s.Seed = int.TryParse(_seedInput.text, out var seed) ? seed : AppSession.DefaultSeed;
            s.Width = SelectedWidth;
            s.Height = SelectedHeight;
            s.MetresPerCell = SelectedMetresPerCell;
            s.Profile = SelectedProfile;
            s.EnableErosion = _erosion.isOn;
            s.EnableCulture = _culture.isOn;
            // ParameterOverride is already maintained by the slider / profile handlers.
        }

        // ─── Preview ──────────────────────────────────────────────────────────

        private void SetPreviewMode(bool three)
        {
            _is3d = three;

            _card.Rect.parent.gameObject.SetActive(!three);
            _sheet3dGo.SetActive(three);

            StyleTab(_tab2dFace, _tab2dText, !three);
            StyleTab(_tab3dFace, _tab3dText, three);

            if (three)
            {
                PollDrapeSize();
                _drape.SetRendering(true);
                _sheet3d.texture = _drape.Output;
            }
            else
            {
                _drape.SetRendering(false);
                _card.UpdateCrop();
            }
        }

        /// <summary>
        /// Keeps the RenderTexture matched to the frame. Quantised inside the stage so a
        /// window drag does not reallocate every frame.
        /// </summary>
        private void PollDrapeSize()
        {
            if (_previewFrame == null || _drape == null) return;

            Rect r = _previewFrame.rect;
            if (r.width < 2f || r.height < 2f) return;
            if (Mathf.Abs(r.width - _lastFrameSize.x) < 8f &&
                Mathf.Abs(r.height - _lastFrameSize.y) < 8f) return;

            _lastFrameSize = new Vector2(r.width, r.height);

            float scale = 1f;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) scale = canvas.scaleFactor;

            _drape.SetSize(Mathf.RoundToInt(r.width * scale), Mathf.RoundToInt(r.height * scale));
            _sheet3d.texture = _drape.Output;
        }

        private void RefreshPreview()
        {
            if (_session == null) return;

            var map = _session.EnsureMap();
            _seenGeneration = _session.Generation;

            // 2D
            var options = MapRenderOptions.Default;
            options.Mode = Pick(DisplayNames.RenderModes, _modeDrop, MapRenderMode.Topographic);
            options.PixelsPerCell = Mathf.Clamp(1600f / Mathf.Max(map.Width, map.Height), 0.5f, 4f);
            _card.Render(map, options);
            _card.SetMarginaliaFor(map, _session.Settings.Seed);

            // 3D
            var meshOptions = MapMeshOptions.Default;
            meshOptions.MaxVerticesPerSide = SelectedMeshDetail;

            var drapeOptions = MapDrapeTexture.DefaultOptions();
            drapeOptions.Mode = options.Mode;

            _drape.SetMap(map, meshOptions, drapeOptions);
            _drape.SetVerticalExaggeration(_exaggeration != null ? _exaggeration.value : 2f);

            if (_is3d)
            {
                PollDrapeSize();
                _sheet3d.texture = _drape.Output;
            }
        }
    }
}
