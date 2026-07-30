// SymbolBuilderPanel.cs
// Light "operations map" presentation:
//   left  — topo underlay, composed symbol, SIDC, digit-by-digit breakdown table
//   right — light control rail with bordered selectors
// All text/background pairs are chosen for >= 7:1 contrast (WCAG AAA).
//
// The underlay is a real generated map: Strategos.Maps generates the terrain and
// MapRasterizer draws the sheet the symbol sits on. It used to be five Gaussian
// hills with fake isolines, which is where the Topographic palette's paper and
// contour colours came from before they were promoted into MapPalette.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Strategos.Maps;
using Strategos.NatoSymbols;
using Strategos.UI;

// The theme, widget kit and table helpers used to be private members of this class.
// They now live in Strategos.UI so other views can match this one; these aliases keep
// every call site below reading exactly as it did when they were members.
using Theme = Strategos.UI.UiTheme;
using TableRow = Strategos.UI.UiTableRow;
using static Strategos.UI.UiFactory;

namespace Strategos.Demo
{
    public class SymbolBuilderPanel : MonoBehaviour, IAppView
    {
        [Header("Optional")]
        [SerializeField] private NatoSymbolDatabase _database;
        [SerializeField] private int _previewSize = 384;

        private RawImage _previewImage;
        private RawImage _topoImage;
        private TMP_Text _sidcLabel;
        private TMP_InputField _designationField;
        private TMP_InputField _formationField;
        private Slider _strengthSlider;
        private TMP_Text _strengthValueLabel;

        private TMP_Dropdown _affiliationDrop;
        private TMP_Dropdown _echelonDrop;
        private TMP_Dropdown _unitTypeDrop;
        private TMP_Dropdown _variantDrop;
        private TMP_Dropdown _mod1Drop;
        private TMP_Dropdown _mod2Drop;
        private TMP_Dropdown _hqTfDrop;
        private TMP_Dropdown _statusDrop;
        private TMP_Dropdown _strengthModDrop;
        private TMP_Dropdown _mapProfileDrop;
        private TMP_Dropdown _mapModeDrop;
        private TMP_Text _mapInfoLabel;

        private Texture2D _previewTex;
        private Texture2D _topoTex;
        private bool _suppress;

        /// <summary>
        /// Seed of the map currently on screen. Fixed at start so the demo opens the
        /// same way every run; NEW MAP advances it.
        /// </summary>
        private int _mapSeed = 20260729;

        /// <summary>Card size the underlay crop was last computed for.</summary>
        private Vector2 _underlayCardSize;

        private TableRow[] _tableRows;

        // -------------------------------------------------------------------------
        // IAppView
        // -------------------------------------------------------------------------

        public string Title => "BUILDER";
        public string Key => "builder";

        /// <summary>
        /// Builds the panel into the shell's content host. Runs once, on first activation,
        /// so the underlay's few hundred milliseconds of generation are paid when this tab
        /// is first opened rather than at startup.
        /// </summary>
        public void Build(RectTransform host)
        {
            BuildUi(host);
            PopulateOptions();
            Canvas.ForceUpdateCanvases();
            RefreshMap();
            RefreshPreview();
        }

        public void OnShown()
        {
            // The card may have been resized while this view was hidden, and the Update
            // poller below was not running to notice.
            UpdateUnderlayCrop();
        }

        public void OnHidden()
        {
            // A dropdown left open re-appears open, floating over whichever view is
            // shown next.
            HideDropdownsIn(transform);
        }

        private void OnDestroy()
        {
            DestroyPreviewAssets();
            if (_topoTex != null) Destroy(_topoTex);
        }

        // -------------------------------------------------------------------------
        // Preview + breakdown
        // -------------------------------------------------------------------------

        private void RefreshPreview()
        {
            if (_suppress || _previewImage == null) return;

            var code = BuildSidc();
            var symbol = NatoSymbolComposer.Compose(code, _database);
            var sprite = NatoSymbolBaker.Bake(symbol, _previewSize);

            DestroyPreviewAssets();
            if (sprite != null && sprite.texture != null)
            {
                _previewTex = sprite.texture;
                // Bake owns the texture via Sprite; keep texture, drop sprite wrapper.
                _previewImage.texture = _previewTex;
                _previewImage.color = Color.white;
                Destroy(sprite);
            }

            if (_sidcLabel != null)
            {
                // <mspace> renders the digits monospaced so they line up with the
                // position column in the table below.
                _sidcLabel.text = string.IsNullOrEmpty(code.Raw)
                    ? "—"
                    : $"<mspace=0.62em>{code.Raw}</mspace>";
            }

            UpdateBreakdownTable(code);
        }

        private void UpdateBreakdownTable(SIDCCode code)
        {
            if (_tableRows == null) return;

            string raw = code.Raw ?? string.Empty;

            for (int i = 0; i < SidcExplain.Fields.Length; i++)
            {
                var f = SidcExplain.Fields[i];
                _tableRows[i].Code.text = Slice(raw, f.Start, f.Len);
                _tableRows[i].Meaning.text = SidcExplain.FieldMeaning(i, code);
            }

            int a = SidcExplain.Fields.Length;
            _tableRows[a + 0].Code.text = Dash(code.Designation);
            _tableRows[a + 0].Meaning.text = "Unique unit label drawn right of the frame";
            _tableRows[a + 1].Code.text = Dash(code.HigherFormation);
            _tableRows[a + 1].Meaning.text = "Parent command drawn right of the frame";
            _tableRows[a + 2].Code.text = SidcExplain.StrengthDisplay(code);
            _tableRows[a + 2].Meaning.text = "+/-/± drawn upper right; % as combat-power bar";
        }

        private static string Slice(string raw, int start, int len)
        {
            if (string.IsNullOrEmpty(raw) || start + len > raw.Length)
                return "—";
            return raw.Substring(start, len);
        }

        private static string Dash(string s) => string.IsNullOrEmpty(s) ? "—" : s;

        private SIDCCode BuildSidc()
        {
            var aff = Pick(DisplayNames.Affiliations, _affiliationDrop, Affiliation.Friend);
            var ech = Pick(DisplayNames.Echelons, _echelonDrop, Echelon.Company);
            var ent = Pick(DisplayNames.UnitTypes, _unitTypeDrop, LandEntityCode.Infantry);
            int type = PickCode(DisplayNames.Variants, _variantDrop, 11);
            int mod1 = PickCode(DisplayNames.SectorMods, _mod1Drop, 0);
            int mod2 = PickCode(DisplayNames.SectorMods, _mod2Drop, 0);
            var hq = Pick(DisplayNames.HqTf, _hqTfDrop, HeadquartersTaskForceDummy.None);
            var status = Pick(DisplayNames.Statuses, _statusDrop, UnitStatus.Present);
            var strMod = Pick(DisplayNames.StrengthMods, _strengthModDrop, StrengthModifier.None);

            int strengthPct = _strengthSlider != null ? Mathf.RoundToInt(_strengthSlider.value) : 100;

            var code = SIDCBuilder.Build(
                affiliation: aff,
                echelon:     ech,
                entityCode:  (int)ent,
                entityType:  type,
                hqTfDummy:   hq,
                status:      status,
                modifier1:   mod1,
                modifier2:   mod2);

            code.Designation = _designationField != null ? _designationField.text : string.Empty;
            code.HigherFormation = _formationField != null ? _formationField.text : string.Empty;
            code.StrengthLabel = strengthPct.ToString();
            code.StrengthModifier = strMod;
            return code;
        }

        private void DestroyPreviewAssets()
        {
            if (_previewImage != null)
                _previewImage.texture = Texture2D.whiteTexture;
            if (_previewTex != null)
            {
                Destroy(_previewTex);
                _previewTex = null;
            }
        }

        // -------------------------------------------------------------------------
        // Options
        // -------------------------------------------------------------------------

        private void PopulateOptions()
        {
            // Programmatic population fires onValueChanged for the plain setters below,
            // so the refresh path is suppressed until every control is seeded.
            _suppress = true;

            SetDrop(_affiliationDrop,  DisplayNames.AffiliationLabels(), 0);
            SetDrop(_echelonDrop,      DisplayNames.EchelonLabels(), 4);   // Company
            SetDrop(_unitTypeDrop,     DisplayNames.UnitTypeLabels(), 0);
            SetDrop(_variantDrop,      DisplayNames.VariantLabels(), 0);
            SetDrop(_mod1Drop,         DisplayNames.SectorModLabels(), 0);
            SetDrop(_mod2Drop,         DisplayNames.SectorModLabels(), 0);
            SetDrop(_hqTfDrop,         DisplayNames.HqTfLabels(), 0);
            SetDrop(_statusDrop,       DisplayNames.StatusLabels(), 0);
            SetDrop(_strengthModDrop,  DisplayNames.StrengthModLabels, 0);
            SetDrop(_mapProfileDrop,   DisplayNames.ProfileLabels(), 0);
            SetDrop(_mapModeDrop,      DisplayNames.RenderModeLabels(), 0);

            if (_designationField != null) _designationField.SetTextWithoutNotify("1-7 IN");
            if (_formationField != null) _formationField.SetTextWithoutNotify("3 ID");
            if (_strengthSlider != null) _strengthSlider.SetValueWithoutNotify(100);

            _suppress = false;
        }

        // -------------------------------------------------------------------------
        // UI construction
        // -------------------------------------------------------------------------

        /// <summary>
        /// Builds the panel into <paramref name="host"/>. The Canvas, CanvasScaler and
        /// GraphicRaycaster this used to create itself now belong to AppShell, so that one
        /// canvas serves every view instead of each view stacking its own.
        /// </summary>
        private void BuildUi(RectTransform host)
        {
            var root = CreateRect("Root", host);
            Stretch(root);
            root.gameObject.AddComponent<Image>().color = Theme.StageBg;
            var rootH = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            rootH.spacing = 0;
            rootH.childControlWidth = true;
            rootH.childControlHeight = true;
            // Must be false: force-expand hands every child a share of the surplus
            // regardless of flexibleWidth, which pushes the fixed-width rail past
            // the screen edge. With it off the stage absorbs all slack instead.
            rootH.childForceExpandWidth = false;
            rootH.childForceExpandHeight = true;

            BuildStage(root);
            BuildControlRail(root);
        }

        private void BuildStage(Transform root)
        {
            var stage = CreateRect("Stage", root);
            var stageLe = stage.gameObject.AddComponent<LayoutElement>();
            stageLe.flexibleWidth = 1f;   // absorbs all width the rail does not take
            stageLe.minWidth = 480f;
            stage.gameObject.AddComponent<Image>().color = Theme.StageBg;

            var stageV = stage.gameObject.AddComponent<VerticalLayoutGroup>();
            stageV.padding = new RectOffset(32, 24, 24, 24);
            stageV.spacing = 12;
            stageV.childAlignment = TextAnchor.UpperCenter;
            stageV.childControlWidth = true;
            // Must be true: with childControlHeight off the group reserves space
            // from LayoutElement.preferredHeight but never resizes the child,
            // leaving zero-height rects.
            stageV.childControlHeight = true;
            stageV.childForceExpandHeight = false;
            stageV.childForceExpandWidth = true;

            var hdr = CreateTmp("Hdr", stage, "STRATEGOS  ·  APP-6(D) SYMBOL", 20, FontStyles.Bold);
            hdr.alignment = TextAlignmentOptions.Center;
            hdr.color = Theme.Ink;
            hdr.characterSpacing = 6f;

            BuildMapCard(stage);
            BuildSidcCard(stage);
            BuildTableCard(stage);
        }

        private void BuildMapCard(Transform stage)
        {
            var mapCard = CreateRect("MapCard", stage);
            mapCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 400;
            mapCard.gameObject.AddComponent<Image>().color = Theme.CardLine;

            var mapStack = CreateRect("MapStack", mapCard);
            Stretch(mapStack);
            mapStack.offsetMin = new Vector2(2, 2);
            mapStack.offsetMax = new Vector2(-2, -2);
            mapStack.gameObject.AddComponent<Image>().color = Theme.MapPaper;

            var topoRt = CreateRect("Topo", mapStack);
            Stretch(topoRt);
            _topoImage = topoRt.gameObject.AddComponent<RawImage>();
            _topoImage.color = Color.white;
            _topoImage.raycastTarget = false;
            // Texture comes from RefreshMap, which needs the map dropdowns to exist and
            // so cannot run until the whole UI is built.
            _topoImage.texture = Texture2D.whiteTexture;

            // Paper halo so the symbol and its amplifier text stay legible over the
            // sheet. Soft-edged, not a flat rectangle: against a real map a hard edge
            // reads as a panel someone forgot to remove, and it cuts the contours it
            // is meant to quieten.
            var halo = CreateRect("Halo", mapStack);
            halo.anchorMin = new Vector2(0.5f, 0.5f);
            halo.anchorMax = new Vector2(0.5f, 0.5f);
            halo.pivot = new Vector2(0.5f, 0.5f);
            halo.sizeDelta = new Vector2(420, 380);
            var haloImg = halo.gameObject.AddComponent<Image>();
            haloImg.sprite = UiSprites.Halo;
            haloImg.color = new Color(1f, 1f, 0.98f, 0.62f);
            haloImg.raycastTarget = false;

            var symbolHolder = CreateRect("SymbolHolder", mapStack);
            symbolHolder.anchorMin = new Vector2(0.5f, 0.5f);
            symbolHolder.anchorMax = new Vector2(0.5f, 0.5f);
            symbolHolder.pivot = new Vector2(0.5f, 0.5f);
            symbolHolder.sizeDelta = new Vector2(320, 320);
            _previewImage = symbolHolder.gameObject.AddComponent<RawImage>();
            _previewImage.color = Color.white;
            _previewImage.texture = Texture2D.whiteTexture;
            _previewImage.raycastTarget = false;

            BuildMapMarginalia(mapStack);
        }

        /// <summary>
        /// The strip along the foot of the sheet carrying seed, extent, elevation range
        /// and contour interval — a map's marginalia. Without it the underlay is a
        /// picture; with it the reader knows what ground and what scale they are on.
        /// </summary>
        private void BuildMapMarginalia(Transform mapStack)
        {
            var strip = CreateRect("Marginalia", mapStack);
            strip.anchorMin = new Vector2(0, 0);
            strip.anchorMax = new Vector2(1, 0);
            strip.pivot = new Vector2(0.5f, 0);
            strip.sizeDelta = new Vector2(0, 20);

            var stripImg = strip.gameObject.AddComponent<Image>();
            stripImg.color = new Color(1f, 1f, 0.98f, 0.72f);
            stripImg.raycastTarget = false;

            _mapInfoLabel = CreateOverlayTmp("Info", strip, string.Empty, 10, Theme.InkMuted);
            Stretch(_mapInfoLabel.rectTransform);
            _mapInfoLabel.rectTransform.offsetMin = new Vector2(8, 0);
            _mapInfoLabel.rectTransform.offsetMax = new Vector2(-8, 0);
            _mapInfoLabel.alignment = TextAlignmentOptions.MidlineLeft;
            _mapInfoLabel.characterSpacing = 1.5f;
            _mapInfoLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _mapInfoLabel.overflowMode = TextOverflowModes.Ellipsis;
            _mapInfoLabel.raycastTarget = false;
        }

        private void BuildSidcCard(Transform stage)
        {
            var sidcCard = CreateRect("SidcCard", stage);
            sidcCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 62;
            sidcCard.gameObject.AddComponent<Image>().color = Theme.CardBg;
            var sidcV = sidcCard.gameObject.AddComponent<VerticalLayoutGroup>();
            sidcV.padding = new RectOffset(16, 16, 8, 8);
            sidcV.spacing = 2;
            sidcV.childAlignment = TextAnchor.MiddleCenter;
            sidcV.childControlWidth = true;
            sidcV.childForceExpandWidth = true;

            var sidcCap = CreateTmp("SidcCap", sidcCard, "SYMBOL IDENTIFICATION CODE", 11, FontStyles.Bold);
            sidcCap.alignment = TextAlignmentOptions.Center;
            sidcCap.color = Theme.InkMuted;
            sidcCap.characterSpacing = 4f;
            sidcCap.GetComponent<LayoutElement>().preferredHeight = 14;

            _sidcLabel = CreateTmp("Sidc", sidcCard, "—", 24, FontStyles.Bold);
            _sidcLabel.alignment = TextAlignmentOptions.Center;
            _sidcLabel.color = Theme.Accent;
            _sidcLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _sidcLabel.GetComponent<LayoutElement>().preferredHeight = 30;
        }

        private void BuildTableCard(Transform stage)
        {
            var tableCard = CreateRect("TableCard", stage);
            var tableLe = tableCard.gameObject.AddComponent<LayoutElement>();
            tableLe.flexibleHeight = 1;
            tableLe.minHeight = 240;
            tableCard.gameObject.AddComponent<Image>().color = Theme.CardBg;

            var tableV = tableCard.gameObject.AddComponent<VerticalLayoutGroup>();
            tableV.padding = new RectOffset(16, 16, 12, 12);
            tableV.spacing = 0;
            tableV.childControlWidth = true;
            tableV.childForceExpandWidth = true;
            tableV.childControlHeight = true;
            tableV.childForceExpandHeight = false;

            var tableTitle = CreateTmp("TableTitle", tableCard, "CODE BREAKDOWN", 13, FontStyles.Bold);
            tableTitle.color = Theme.Ink;
            tableTitle.characterSpacing = 4f;
            tableTitle.GetComponent<LayoutElement>().preferredHeight = 20;

            // Header row
            var header = UiTable.CreateRow(tableCard, "HeaderRow", Theme.SectionBg, out var h);
            header.GetComponent<LayoutElement>().preferredHeight = 24;
            UiTable.SetRowText(h, "POS", "CODE", "FIELD", "MEANING");
            UiTable.ApplyRowStyle(h, Theme.Ink, FontStyles.Bold, 11.5f);

            var divider = CreateRect("Divider", tableCard);
            divider.gameObject.AddComponent<LayoutElement>().preferredHeight = 2;
            divider.gameObject.AddComponent<Image>().color = Theme.CardLine;

            int total = SidcExplain.Fields.Length + SidcExplain.AmplifierFields.Length;
            _tableRows = new TableRow[total];

            for (int i = 0; i < total; i++)
            {
                bool amplifier = i >= SidcExplain.Fields.Length;
                var stripe = (i % 2 == 0) ? Theme.CardBg : Theme.RowStripe;
                UiTable.CreateRow(tableCard, $"Row{i}", stripe, out var r);
                _tableRows[i] = r;

                if (amplifier)
                {
                    r.Pos.text = "amp";
                    r.Field.text = SidcExplain.AmplifierFields[i - SidcExplain.Fields.Length];
                }
                else
                {
                    r.Pos.text = SidcExplain.Fields[i].Pos;
                    r.Field.text = SidcExplain.Fields[i].Field;
                }

                r.Pos.color = Theme.InkMuted;
                r.Code.color = Theme.Accent;
                r.Code.fontStyle = FontStyles.Bold;
                r.Field.color = Theme.Ink;
                r.Meaning.color = Theme.InkMuted;
            }
        }

        private void BuildControlRail(Transform root)
        {
            var panel = CreateRect("ControlRail", root);
            var panelLe = panel.gameObject.AddComponent<LayoutElement>();
            panelLe.preferredWidth = 440;
            panelLe.minWidth = 400;
            panelLe.flexibleWidth = 0;
            panel.gameObject.AddComponent<Image>().color = Theme.RailBg;

            // Left edge rule separating rail from stage
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
            chrome.anchoredPosition = Vector2.zero;
            chrome.gameObject.AddComponent<Image>().color = Theme.Accent;
            var chromeLabel = CreateTmp("ChromeLabel", chrome, "UNIT CONSTRUCTION", 14, FontStyles.Bold, withLayout: false);
            Stretch(chromeLabel.rectTransform);
            chromeLabel.alignment = TextAlignmentOptions.Center;
            chromeLabel.color = Theme.AccentText;
            chromeLabel.characterSpacing = 6f;

            var content = UiScroll.CreateColumn("Scroll", panel, Theme.RailBg, out var scroll);
            var scrollRt = (RectTransform)scroll.transform;
            scrollRt.offsetMin = new Vector2(2, 0);
            scrollRt.offsetMax = new Vector2(0, -38); // clear the chrome header

            AddSection(content, "IDENTITY");
            _affiliationDrop = AddDropdown(content, "AFFILIATION", RefreshPreview);
            _statusDrop = AddDropdown(content, "STATUS / CONDITION", RefreshPreview);

            AddSection(content, "ECHELON");
            _echelonDrop = AddDropdown(content, "COMMAND LEVEL", RefreshPreview);
            _hqTfDrop = AddDropdown(content, "HQ / TASK FORCE / FEINT", RefreshPreview);

            AddSection(content, "UNIT / EQUIPMENT");
            _unitTypeDrop = AddDropdown(content, "UNIT TYPE", RefreshPreview);
            _variantDrop = AddDropdown(content, "VARIANT", RefreshPreview);

            AddSection(content, "SECTOR MODIFIERS");
            _mod1Drop = AddDropdown(content, "SECTOR 1 (UPPER)", RefreshPreview);
            _mod2Drop = AddDropdown(content, "SECTOR 2 (LOWER)", RefreshPreview);

            AddSection(content, "STRENGTH");
            _strengthModDrop = AddDropdown(content, "STRENGTH AMPLIFIER", RefreshPreview);
            (_strengthSlider, _strengthValueLabel) =
                AddSlider(content, "STRENGTH", 0, 100, 100, RefreshPreview);

            AddSection(content, "LABELS");
            _designationField = AddInput(content, "DESIGNATION", "1-7 IN", RefreshPreview);
            _formationField = AddInput(content, "HIGHER FORMATION", "3 ID", RefreshPreview);

            AddButton(content, "REFRESH SYMBOL", RefreshPreview);

            // The map controls rebuild the sheet, not the symbol. Regenerating is orders
            // of magnitude more expensive than recomposing, so the two refresh paths stay
            // separate rather than having one control trigger both.
            AddSection(content, "MAP UNDERLAY");
            _mapProfileDrop = AddDropdown(content, "RELIEF PROFILE", RefreshMapIfReady);
            _mapModeDrop = AddDropdown(content, "RENDER MODE", RefreshMapIfReady);

            AddButton(content, "NEW MAP", NewMap);
        }

        private void RefreshMapIfReady()
        {
            if (_suppress) return;
            RefreshMap();
        }

        /// <summary>Advances the seed and regenerates. Same profile, different ground.</summary>
        private void NewMap()
        {
            _mapSeed = UnityEngine.Random.Range(1, int.MaxValue);
            RefreshMap();
        }

        // -------------------------------------------------------------------------
        // Topo underlay
        // -------------------------------------------------------------------------

        /// <summary>
        /// Cells per side of the underlay map. Square, and cropped to whatever shape
        /// the card happens to be — see <see cref="UpdateUnderlayCrop"/>. 200 cells at
        /// 25 m is a 5 km square, and generates in a few hundred milliseconds.
        /// </summary>
        private const int UnderlayCells = 200;

        /// <summary>
        /// Render scale. Above the overview zoom on purpose: at 1 px per cell the
        /// renderer generalises roads to hairlines and drops spot detail, which is
        /// right for a whole-map sheet and wrong for a card you look at up close.
        /// </summary>
        private const float UnderlayPixelsPerCell = 4f;

        /// <summary>
        /// How many landforms should fit across the sheet. A profile's
        /// <c>FeatureScaleCells</c> is tuned for the 512-cell default map; used as-is on
        /// a 200-cell one, a single hill is wider than the whole sheet and the underlay
        /// comes out as one dome with concentric rings.
        /// </summary>
        private const float UnderlayLandformsAcross = 2.5f;

        /// <summary>
        /// Regenerates the underlay from the current map controls and shows it.
        /// Called from Start and whenever a map control changes; generation costs a few
        /// hundred milliseconds on the main thread, which is why it is on a button and
        /// two dropdowns rather than a slider.
        /// </summary>
        private void RefreshMap()
        {
            if (_topoImage == null) return;

            var profile = SelectedMapProfile;

            // Shrink the landform scale to suit a sheet this small. This is what
            // ParameterOverride is for — the alternative is inventing a near-duplicate
            // profile per map size.
            var tuned = ReliefProfiles.For(profile);
            tuned.FeatureScaleCells = Mathf.Min(
                tuned.FeatureScaleCells, UnderlayCells / UnderlayLandformsAcross);

            var settings = new MapGenerationSettings
            {
                Name              = "UNDERLAY",
                Seed              = _mapSeed,
                Width             = UnderlayCells,
                Height            = UnderlayCells,
                MetresPerCell     = 25f,
                Profile           = profile,
                ParameterOverride = tuned,
            };

            var map = MapGenerator.Generate(settings);

            var options = MapRenderOptions.Default;
            options.Mode          = SelectedMapMode;
            options.PixelsPerCell = UnderlayPixelsPerCell;

            // No names, no spot heights: this sheet exists to be drawn on. Text and
            // point marks under a 320 px symbol are clutter competing with the subject,
            // and the contact sheet is where that detail gets inspected.
            options.DrawLabels = false;
            options.DrawPois   = false;

            var tex = MapRasterizer.Render(map, options);
            tex.name = "TopoUnderlay";

            _topoImage.texture = tex;
            if (_topoTex != null) Destroy(_topoTex);
            _topoTex = tex;

            UpdateUnderlayCrop();

            if (_mapInfoLabel != null)
            {
                var header = map.Header;
                // Hyphen, not an en dash: U+2013 is not in the bundled LiberationSans
                // SDF atlas and renders as nothing, so the elevation range came out as
                // "202 280 M". Latin-1 punctuation only here — see the glyph coverage
                // note in CLAUDE.md.
                _mapInfoLabel.text =
                    $"SEED {_mapSeed}   ·   {header.WidthMetres / 1000f:0.#} × " +
                    $"{header.HeightMetres / 1000f:0.#} KM   ·   " +
                    $"{header.MinElevation:0}-{header.MaxElevation:0} M   ·   " +
                    $"CONTOUR {header.ContourInterval:0} M";
            }
        }

        /// <summary>
        /// Fits the sheet to the card by cropping to a centred region of the card's
        /// shape, never by stretching. A stretched map has a different scale on each
        /// axis, so every distance and bearing read off it is wrong — which for a map
        /// is a correctness problem, not a cosmetic one. The card's aspect depends on
        /// the window, so this is re-evaluated when its size changes rather than baked
        /// into the generated map.
        /// </summary>
        private void UpdateUnderlayCrop()
        {
            if (_topoImage == null || _topoTex == null) return;

            Rect card = _topoImage.rectTransform.rect;
            if (card.width < 1f || card.height < 1f) return;

            _underlayCardSize = new Vector2(card.width, card.height);

            float cardAspect = card.width / card.height;
            float texAspect  = _topoTex.width / (float)_topoTex.height;

            float uvW = 1f, uvH = 1f;
            if (cardAspect > texAspect) uvH = texAspect / cardAspect;
            else                        uvW = cardAspect / texAspect;

            _topoImage.uvRect = new Rect((1f - uvW) * 0.5f, (1f - uvH) * 0.5f, uvW, uvH);
        }

        private void Update()
        {
            // Cheap guard against a window resize reshaping the card. Regenerating on
            // resize would stall for a few hundred milliseconds; re-cropping is free.
            if (_topoImage == null) return;

            Rect card = _topoImage.rectTransform.rect;
            if (Mathf.Abs(card.width  - _underlayCardSize.x) > 0.5f ||
                Mathf.Abs(card.height - _underlayCardSize.y) > 0.5f)
                UpdateUnderlayCrop();
        }

        private ReliefProfile SelectedMapProfile =>
            Pick(DisplayNames.Profiles, _mapProfileDrop, ReliefProfile.Rolling);

        private MapRenderMode SelectedMapMode =>
            Pick(DisplayNames.RenderModes, _mapModeDrop, MapRenderMode.Schematic);

    }
}
