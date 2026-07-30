// SymbolLibraryView.cs
// Browse what the symbol library actually contains.
//
// The builder composes one symbol and explains it digit by digit. This answers the other
// question — what does the catalogue hold, and what does each field do to the picture —
// by showing many at once.
//
// WHY AN AXIS MODEL, NOT A SEARCH BOX OVER EVERYTHING
// The full cross product is 7 affiliations x 14 echelons x 13 unit types x 9 variants x
// 6 x 6 sector modifiers x 6 statuses x 8 HQ/TF values, which is about 21 million symbols.
// A filter over that is not a library, it is a hang. So you pick one or two fields to
// enumerate and pin the rest: at most 14 x 13 = 182 tiles, every one of them baked. This
// is the on-screen version of the contact sheet the project already uses for exactly this
// purpose.

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Strategos.NatoSymbols;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI.Views
{
    public sealed class SymbolLibraryView : MonoBehaviour, IAppView
    {
        /// <summary>
        /// Bake size, fixed on purpose. `size` is part of ProceduralSymbolFactory's cache
        /// key, so exposing it as a control would re-bake and re-cache the whole sheet on
        /// every notch. Tiles scale the sprite instead.
        /// </summary>
        private const int BakeSize = 128;

        /// <summary>
        /// Tiles are 4:3, NOT square. The frame is deliberately left of centre —
        /// SymbolLayout.FrameRight is 160 of BASE 256, reserving a right-hand column for
        /// text amplifiers that APP-6D places outside the frame. In a square tile the
        /// symbol therefore looks wrongly shifted left; a landscape tile makes the reserved
        /// column part of the composition. Do not "centre" this.
        /// </summary>
        private static readonly Vector2 TileSize = new(128, 112);

        private const int CaptionHeight = 16;

        /// <summary>Refuse to bake more than this many tiles at once.</summary>
        private const int MaxTiles = 400;

        // ─── Axes ─────────────────────────────────────────────────────────────

        private enum Axis { UnitType, Echelon, Affiliation, Variant, Sector1, Sector2, Status, HqTf }

        private static readonly Axis[] Axes =
        {
            Axis.UnitType, Axis.Echelon, Axis.Affiliation, Axis.Variant,
            Axis.Sector1, Axis.Sector2, Axis.Status, Axis.HqTf,
        };

        private static readonly string[] AxisLabels =
        {
            "UNIT TYPE", "ECHELON", "AFFILIATION", "VARIANT",
            "SECTOR 1", "SECTOR 2", "STATUS", "HQ / TF",
        };

        /// <summary>The pickable fields of a land-unit symbol.</summary>
        private struct Spec
        {
            public Affiliation Aff;
            public Echelon Ech;
            public LandEntityCode Unit;
            public int Variant;
            public int Mod1;
            public int Mod2;
            public UnitStatus Status;
            public HeadquartersTaskForceDummy HqTf;

            public SIDCCode ToCode() => SIDCBuilder.Build(
                affiliation: Aff, echelon: Ech, entityCode: (int)Unit, entityType: Variant,
                hqTfDummy: HqTf, status: Status, modifier1: Mod1, modifier2: Mod2);
        }

        /// <summary>One position along an axis: how to label it and how to apply it.</summary>
        private readonly struct AxisValue
        {
            public readonly string Label;
            public readonly Func<Spec, Spec> Apply;
            public AxisValue(string label, Func<Spec, Spec> apply) { Label = label; Apply = apply; }
        }

        private static AxisValue[] ValuesFor(Axis axis)
        {
            switch (axis)
            {
                case Axis.UnitType:
                    return Build(DisplayNames.UnitTypes,
                        u => DisplayNames.Prettify(u.ToString()),
                        (s, u) => { s.Unit = u; return s; });
                case Axis.Echelon:
                    return Build(DisplayNames.Echelons,
                        DisplayNames.EchelonName,
                        (s, e) => { s.Ech = e; return s; });
                case Axis.Affiliation:
                    return Build(DisplayNames.Affiliations,
                        DisplayNames.AffiliationLabel,
                        (s, a) => { s.Aff = a; return s; });
                case Axis.Variant:
                    return Build(DisplayNames.Variants,
                        v => v.label,
                        (s, v) => { s.Variant = v.code; return s; });
                case Axis.Sector1:
                    return Build(DisplayNames.SectorMods,
                        v => v.label,
                        (s, v) => { s.Mod1 = v.code; return s; });
                case Axis.Sector2:
                    return Build(DisplayNames.SectorMods,
                        v => v.label,
                        (s, v) => { s.Mod2 = v.code; return s; });
                case Axis.Status:
                    return Build(DisplayNames.Statuses,
                        DisplayNames.StatusLabel,
                        (s, st) => { s.Status = st; return s; });
                case Axis.HqTf:
                    return Build(DisplayNames.HqTf,
                        DisplayNames.HqTfLabel,
                        (s, h) => { s.HqTf = h; return s; });
                default:
                    return Array.Empty<AxisValue>();
            }
        }

        private static AxisValue[] Build<T>(T[] source, Func<T, string> label,
            Func<Spec, T, Spec> apply)
        {
            var result = new AxisValue[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var v = source[i];   // capture per iteration, not the loop variable
                result[i] = new AxisValue(label(v), s => apply(s, v));
            }
            return result;
        }

        // ─── State ────────────────────────────────────────────────────────────

        private AppSession _session;
        private bool _suppress;

        private TMP_Dropdown _rowAxisDrop, _colAxisDrop;
        private TMP_Dropdown _affPin, _echPin, _unitPin, _varPin, _mod1Pin, _mod2Pin,
                             _statusPin, _hqPin;
        private TMP_InputField _searchInput;
        private Toggle _showSidcToggle;
        private TMP_Text _countLabel;

        private RectTransform _gridContent;
        private GridLayoutGroup _grid;

        private readonly List<Tile> _tiles = new();
        private Coroutine _bake;

        private RectTransform _inspectorCard;
        private UiTableRow[] _inspectorRows;
        private RawImage _inspectorPreview;
        private Texture2D _inspectorTex;
        private TMP_InputField _inspectorSidc;

        private sealed class Tile
        {
            public GameObject Go;
            public Image Icon;
            public TMP_Text Caption;
            public Button Button;
        }

        // ─── IAppView ─────────────────────────────────────────────────────────

        public string Title => "SYMBOLS";
        public string Key => "symbols";

        /// <summary>Set by the parent view before Build so the shared factory is available.</summary>
        public AppSession Session { set => _session = value; }

        public void Build(RectTransform host)
        {
            BuildUi(host);
            PopulateOptions();
            // Before the first Rebuild, or the pins for the two enumerated fields look live
            // on the opening screen and imply an effect they do not have.
            UpdatePinAvailability();
            Canvas.ForceUpdateCanvases();
            Rebuild();
        }

        public void OnShown() { }

        public void OnHidden() => HideDropdownsIn(transform);

        private void OnDestroy()
        {
            if (_inspectorTex != null) Destroy(_inspectorTex);
            // Tile sprites come from the shared cache and are NOT ours to destroy.
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
            // Off, or the fixed-width rail absorbs a share of the surplus and runs past
            // the screen edge.
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
            stage.gameObject.AddComponent<Image>().color = Theme.StageBg;

            var v = stage.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(24, 24, 18, 18);
            v.spacing = 12;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            var head = CreateRect("Head", stage);
            head.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;
            var title = CreateTmp("T", head, "SYMBOL LIBRARY", 15, FontStyles.Bold, withLayout: false);
            Stretch(title.rectTransform);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.color = Theme.Ink;
            title.characterSpacing = 6f;

            _countLabel = CreateTmp("Count", head, string.Empty, 12, FontStyles.Bold, withLayout: false);
            Stretch(_countLabel.rectTransform);
            _countLabel.alignment = TextAlignmentOptions.MidlineRight;
            _countLabel.color = Theme.Accent;
            _countLabel.characterSpacing = 2f;

            // --- Grid card ---
            var gridCard = CreateRect("GridCard", stage);
            var gle = gridCard.gameObject.AddComponent<LayoutElement>();
            gle.flexibleHeight = 1f;
            gle.minHeight = 260f;
            gridCard.gameObject.AddComponent<Image>().color = Theme.CardLine;

            var gridInner = CreateRect("Inner", gridCard);
            Stretch(gridInner);
            gridInner.offsetMin = new Vector2(2, 2);
            gridInner.offsetMax = new Vector2(-2, -2);

            _gridContent = UiScroll.CreateGridColumn("Scroll", gridInner, Theme.CardBg,
                TileSize, new Vector2(10, 10), out _, out _grid);

            BuildInspector(stage);
        }

        private void BuildInspector(Transform stage)
        {
            _inspectorCard = CreateRect("InspectorCard", stage);
            _inspectorCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 250;
            _inspectorCard.gameObject.AddComponent<Image>().color = Theme.CardBg;

            var h = _inspectorCard.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(14, 14, 10, 10);
            h.spacing = 14;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;

            // Preview column
            var previewCol = CreateRect("Preview", _inspectorCard);
            var ple = previewCol.gameObject.AddComponent<LayoutElement>();
            ple.preferredWidth = 200;
            ple.flexibleWidth = 0;
            _inspectorPreview = previewCol.gameObject.AddComponent<RawImage>();
            _inspectorPreview.texture = Texture2D.whiteTexture;
            _inspectorPreview.color = Color.white;
            _inspectorPreview.raycastTarget = false;

            // Detail column
            var detail = CreateRect("Detail", _inspectorCard);
            detail.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var dv = detail.gameObject.AddComponent<VerticalLayoutGroup>();
            dv.spacing = 2;
            dv.childControlWidth = true;
            dv.childControlHeight = true;
            dv.childForceExpandWidth = true;
            dv.childForceExpandHeight = false;

            var cap = CreateTmp("Cap", detail, "SELECTED SYMBOL", 12, FontStyles.Bold);
            cap.color = Theme.Ink;
            cap.characterSpacing = 4f;
            cap.GetComponent<LayoutElement>().preferredHeight = 18;

            var header = UiTable.CreateRow(detail, "HeaderRow", Theme.SectionBg, out var hr);
            header.GetComponent<LayoutElement>().preferredHeight = 20;
            UiTable.SetRowText(hr, "POS", "CODE", "FIELD", "MEANING");
            UiTable.ApplyRowStyle(hr, Theme.Ink, FontStyles.Bold, 11f);

            // Only the rows that change with the symbol are worth showing here; the
            // builder is where the full 15-row breakdown lives.
            int rows = 6;
            _inspectorRows = new UiTableRow[rows];
            for (int i = 0; i < rows; i++)
            {
                var stripe = (i % 2 == 0) ? Theme.CardBg : Theme.RowStripe;
                UiTable.CreateRow(detail, $"Row{i}", stripe, out _inspectorRows[i]);
                _inspectorRows[i].Pos.color = Theme.InkMuted;
                _inspectorRows[i].Code.color = Theme.Accent;
                _inspectorRows[i].Code.fontStyle = FontStyles.Bold;
                _inspectorRows[i].Field.color = Theme.Ink;
                _inspectorRows[i].Meaning.color = Theme.InkMuted;
            }

            var sidcRow = CreateRect("SidcRow", detail);
            sidcRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;
            var sh = sidcRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            sh.spacing = 8;
            sh.childControlWidth = true;
            sh.childControlHeight = true;
            sh.childForceExpandWidth = false;

            _inspectorSidc = AddInput(sidcRow, "SIDC", string.Empty);
            _inspectorSidc.readOnly = true;
            _inspectorSidc.GetComponent<LayoutElement>().flexibleWidth = 1f;

            var copyWrap = CreateRect("CopyWrap", sidcRow);
            var cle = copyWrap.gameObject.AddComponent<LayoutElement>();
            cle.preferredWidth = 150;
            cle.flexibleWidth = 0;
            var cv = copyWrap.gameObject.AddComponent<VerticalLayoutGroup>();
            cv.childAlignment = TextAnchor.LowerCenter;
            cv.childControlWidth = true;
            cv.childControlHeight = true;
            cv.childForceExpandWidth = true;
            AddButton(copyWrap, "COPY SIDC", CopySidc);

            _inspectorCard.gameObject.SetActive(false);
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
            var cl = CreateTmp("L", chrome, "CATALOGUE", 14, FontStyles.Bold, withLayout: false);
            Stretch(cl.rectTransform);
            cl.alignment = TextAlignmentOptions.Center;
            cl.color = Theme.AccentText;
            cl.characterSpacing = 6f;

            var content = UiScroll.CreateColumn("Scroll", panel, Theme.RailBg, out var scroll);
            var srt = (RectTransform)scroll.transform;
            srt.offsetMin = new Vector2(2, 0);
            srt.offsetMax = new Vector2(0, -38);

            AddSection(content, "ENUMERATE");
            _rowAxisDrop = AddDropdown(content, "ROWS / TILES", OnAxisChanged);
            _colAxisDrop = AddDropdown(content, "AGAINST (COLUMNS)", OnAxisChanged);

            AddSection(content, "FILTER");
            _searchInput = AddInput(content, "SEARCH", string.Empty, Rebuild);
            _showSidcToggle = AddToggle(content, "SHOW SIDC IN CAPTION", false, Rebuild);

            AddSection(content, "PINNED FIELDS");
            _affPin    = AddDropdown(content, "AFFILIATION", Rebuild);
            _echPin    = AddDropdown(content, "ECHELON", Rebuild);
            _unitPin   = AddDropdown(content, "UNIT TYPE", Rebuild);
            _varPin    = AddDropdown(content, "VARIANT", Rebuild);
            _mod1Pin   = AddDropdown(content, "SECTOR 1 (UPPER)", Rebuild);
            _mod2Pin   = AddDropdown(content, "SECTOR 2 (LOWER)", Rebuild);
            _statusPin = AddDropdown(content, "STATUS / CONDITION", Rebuild);
            _hqPin     = AddDropdown(content, "HQ / TASK FORCE / FEINT", Rebuild);
        }

        // ─── Options ──────────────────────────────────────────────────────────

        private void PopulateOptions()
        {
            _suppress = true;

            var against = new string[AxisLabels.Length + 1];
            against[0] = "NONE";
            Array.Copy(AxisLabels, 0, against, 1, AxisLabels.Length);

            SetDrop(_rowAxisDrop, AxisLabels, 0);   // UNIT TYPE
            SetDrop(_colAxisDrop, against, 2);      // ECHELON — a type x echelon matrix

            SetDrop(_affPin,    DisplayNames.AffiliationLabels(), 0);
            SetDrop(_echPin,    DisplayNames.EchelonLabels(), 4);
            SetDrop(_unitPin,   DisplayNames.UnitTypeLabels(), 0);
            SetDrop(_varPin,    DisplayNames.VariantLabels(), 0);
            SetDrop(_mod1Pin,   DisplayNames.SectorModLabels(), 0);
            SetDrop(_mod2Pin,   DisplayNames.SectorModLabels(), 0);
            SetDrop(_statusPin, DisplayNames.StatusLabels(), 0);
            SetDrop(_hqPin,     DisplayNames.HqTfLabels(), 0);

            _suppress = false;
        }

        private void OnAxisChanged()
        {
            if (_suppress) return;
            UpdatePinAvailability();
            Rebuild();
        }

        /// <summary>
        /// Greys out the pins whose field is being enumerated. Leaving them live would
        /// imply they still had an effect, and the axis silently overrides them.
        /// </summary>
        private void UpdatePinAvailability()
        {
            var row = RowAxis;
            var col = ColAxis;
            SetPin(_affPin,    Axis.Affiliation, row, col);
            SetPin(_echPin,    Axis.Echelon,     row, col);
            SetPin(_unitPin,   Axis.UnitType,    row, col);
            SetPin(_varPin,    Axis.Variant,     row, col);
            SetPin(_mod1Pin,   Axis.Sector1,     row, col);
            SetPin(_mod2Pin,   Axis.Sector2,     row, col);
            SetPin(_statusPin, Axis.Status,      row, col);
            SetPin(_hqPin,     Axis.HqTf,        row, col);
        }

        private static void SetPin(TMP_Dropdown pin, Axis field, Axis row, Axis? col)
        {
            bool enumerated = field == row || (col.HasValue && field == col.Value);
            pin.interactable = !enumerated;
        }

        private Axis RowAxis => Axes[Mathf.Clamp(_rowAxisDrop.value, 0, Axes.Length - 1)];

        private Axis? ColAxis
        {
            get
            {
                int v = _colAxisDrop.value;
                if (v <= 0) return null;                 // index 0 is NONE
                return Axes[Mathf.Clamp(v - 1, 0, Axes.Length - 1)];
            }
        }

        /// <summary>The base symbol, from every pin that is not being enumerated.</summary>
        private Spec PinnedSpec() => new()
        {
            Aff     = Pick(DisplayNames.Affiliations, _affPin, Affiliation.Friend),
            Ech     = Pick(DisplayNames.Echelons, _echPin, Echelon.Company),
            Unit    = Pick(DisplayNames.UnitTypes, _unitPin, LandEntityCode.Infantry),
            Variant = PickCode(DisplayNames.Variants, _varPin, IconDecorator.VarStandard),
            Mod1    = PickCode(DisplayNames.SectorMods, _mod1Pin, 0),
            Mod2    = PickCode(DisplayNames.SectorMods, _mod2Pin, 0),
            Status  = Pick(DisplayNames.Statuses, _statusPin, UnitStatus.Present),
            HqTf    = Pick(DisplayNames.HqTf, _hqPin, HeadquartersTaskForceDummy.None),
        };

        // ─── Grid ─────────────────────────────────────────────────────────────

        private void Rebuild()
        {
            if (_suppress || _gridContent == null) return;

            var rowValues = ValuesFor(RowAxis);
            var colAxis = ColAxis;
            var colValues = colAxis.HasValue ? ValuesFor(colAxis.Value) : null;

            string search = (_searchInput != null ? _searchInput.text : string.Empty)?.Trim();
            bool showSidc = _showSidcToggle != null && _showSidcToggle.isOn;
            var basis = PinnedSpec();

            var wanted = new List<(Spec spec, string caption, bool hasIcon)>();

            foreach (var rv in rowValues)
            {
                if (colValues == null)
                {
                    Consider(rv.Apply(basis), rv.Label, search, showSidc, wanted);
                }
                else
                {
                    foreach (var cv in colValues)
                        Consider(cv.Apply(rv.Apply(basis)), $"{rv.Label} · {cv.Label}",
                                 search, showSidc, wanted);
                }
            }

            _grid.constraintCount = colValues != null
                ? Mathf.Max(1, colValues.Length)
                : Mathf.Max(1, Mathf.FloorToInt(_gridContent.rect.width / (TileSize.x + 10f)));

            bool capped = wanted.Count > MaxTiles;
            if (capped) wanted.RemoveRange(MaxTiles, wanted.Count - MaxTiles);

            _countLabel.text = capped
                ? $"{wanted.Count} OF MANY - CAPPED"
                : $"{wanted.Count} COMBINATION{(wanted.Count == 1 ? "" : "S")}";

            if (_bake != null) StopCoroutine(_bake);
            _bake = StartCoroutine(BakeTiles(wanted));
        }

        private void Consider(Spec spec, string caption, string search, bool showSidc,
            List<(Spec, string, bool)> into)
        {
            var code = spec.ToCode();
            bool hasIcon = DisplayNames.RendersIcon((int)spec.Unit);

            if (!string.IsNullOrEmpty(search)
                && caption.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                && code.Raw.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                return;

            into.Add((spec, showSidc ? code.Raw : caption, hasIcon));
        }

        /// <summary>
        /// Bakes the visible set, yielding periodically.
        ///
        /// A few hundred bakes is a few hundred milliseconds on first sight and free
        /// afterwards from the shared cache. Yielding keeps that off a single frame.
        /// This is not the "no generation behind a slider" rule being broken: the controls
        /// here are discrete, and nothing regenerates a map.
        /// </summary>
        private IEnumerator BakeTiles(List<(Spec spec, string caption, bool hasIcon)> wanted)
        {
            EnsureTileCount(wanted.Count);

            for (int i = 0; i < wanted.Count; i++)
            {
                var (spec, caption, hasIcon) = wanted[i];
                var code = spec.ToCode();
                var tile = _tiles[i];

                // From the shared cache — never destroy what this returns.
                tile.Icon.sprite = _session.Symbols.GetSymbolSprite(code, BakeSize);
                tile.Icon.color = tile.Icon.sprite != null ? Color.white : new Color(0, 0, 0, 0);

                tile.Caption.text = hasIcon ? caption : caption + "  (FRAME ONLY)";
                tile.Caption.color = hasIcon ? Theme.InkMuted : Theme.Accent;

                var captured = code;
                tile.Button.onClick.RemoveAllListeners();
                tile.Button.onClick.AddListener(() => ShowInspector(captured));

                tile.Go.SetActive(true);

                if ((i & 15) == 15) yield return null;
            }

            for (int i = wanted.Count; i < _tiles.Count; i++)
                _tiles[i].Go.SetActive(false);

            _bake = null;
        }

        /// <summary>
        /// Grows the pool. Tiles are reused rather than destroyed and recreated, because a
        /// filter change would otherwise churn a few hundred GameObjects per keystroke.
        /// </summary>
        private void EnsureTileCount(int count)
        {
            while (_tiles.Count < count)
                _tiles.Add(CreateTile(_gridContent));
        }

        private Tile CreateTile(Transform parent)
        {
            var rt = CreateRect("Tile", parent);
            var face = rt.gameObject.AddComponent<Image>();
            face.color = Color.white;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = face;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Theme.CardBg;
            colors.highlightedColor = Theme.ControlHover;
            colors.pressedColor = Theme.SelectFill;
            colors.selectedColor = Theme.CardBg;
            colors.fadeDuration = 0.05f;
            btn.colors = colors;

            var icon = CreateRect("Icon", rt);
            Stretch(icon);
            icon.offsetMin = new Vector2(4, CaptionHeight);
            icon.offsetMax = new Vector2(-4, -4);
            var img = icon.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;

            var caption = CreateOverlayTmp("Caption", rt, string.Empty, 9, Theme.InkMuted);
            caption.rectTransform.anchorMin = new Vector2(0, 0);
            caption.rectTransform.anchorMax = new Vector2(1, 0);
            caption.rectTransform.pivot = new Vector2(0.5f, 0);
            caption.rectTransform.sizeDelta = new Vector2(-6, CaptionHeight);
            caption.rectTransform.anchoredPosition = new Vector2(0, 1);
            caption.alignment = TextAlignmentOptions.Center;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.overflowMode = TextOverflowModes.Ellipsis;
            caption.characterSpacing = 0.5f;

            return new Tile { Go = rt.gameObject, Icon = img, Caption = caption, Button = btn };
        }

        // ─── Inspector ────────────────────────────────────────────────────────

        private void ShowInspector(SIDCCode code)
        {
            _inspectorCard.gameObject.SetActive(true);

            // Baked uncached and owned here, so it can be a larger preview than a tile and
            // can be disposed. The cached factory's sprites must never be destroyed.
            var symbol = NatoSymbolComposer.Compose(code);
            var sprite = NatoSymbolBaker.Bake(symbol, 256);
            if (_inspectorTex != null) Destroy(_inspectorTex);
            if (sprite != null && sprite.texture != null)
            {
                _inspectorTex = sprite.texture;
                _inspectorPreview.texture = _inspectorTex;
                Destroy(sprite);
            }

            // The six fields a browser actually varies.
            int[] fields = { 2, 6, 7, 8, 10, 11 };
            for (int i = 0; i < _inspectorRows.Length && i < fields.Length; i++)
            {
                int f = fields[i];
                var meta = SidcExplain.Fields[f];
                UiTable.SetRowText(_inspectorRows[i],
                    meta.Pos,
                    code.Raw.Substring(meta.Start, meta.Len),
                    meta.Field,
                    SidcExplain.FieldMeaning(f, code));
            }

            _inspectorSidc.SetTextWithoutNotify(code.Raw);
        }

        private void CopySidc()
        {
            if (_inspectorSidc == null || string.IsNullOrEmpty(_inspectorSidc.text)) return;
            GUIUtility.systemCopyBuffer = _inspectorSidc.text;
        }
    }
}
