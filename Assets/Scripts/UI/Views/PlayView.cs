// PlayView.cs
// A scenario on its map, with both sides' units drawn where they stand.
//
// Read-only for now: no selection, no orders. #7 adds selection, #8 movement, #10 the order
// arrows. This is the step that makes the previous three Core-only issues visible.
//
// 2D, NOT 3D, DELIBERATELY.
// The 3D drape in the scenario view stays a preview. Anchoring units in a perspective world
// needs billboarding, depth sorting and a picking ray, and buys nothing for "see a unit on a
// map". MapRasterizer.RenderPixels' own doc comment names unit symbols as the layer a caller
// composites in the same pixel space, and MapLabelPlacer.Reserve exists so a unit symbol can
// be protected from being labelled over — the 2D seam was cut for exactly this.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Strategos.Maps;
using Strategos.NatoSymbols;
using Strategos.Scenarios;
using Strategos.Units;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI.Views
{
    public sealed class PlayView : MonoBehaviour, IAppView
    {
        /// <summary>
        /// On-screen size of a unit symbol, in reference-resolution px.
        ///
        /// Fixed rather than zoom-scaled. Phase 2.2 wants proper LOD — detailed symbol close
        /// in, simplified icon at distance, a dot at theatre scale — and a naive linear scale
        /// now would have to be unpicked to get there.
        /// </summary>
        private const float SymbolSize = 56f;

        /// <summary>
        /// Bake size, fixed so it stays out of the factory's cache key. Larger than the
        /// on-screen size so symbols stay sharp if that grows.
        /// </summary>
        private const int BakeSize = 128;

        private AppSession _session;
        private Scenario _scenario;
        private MapData _map;

        private MapSheetCard _card;
        private RectTransform _unitLayer;
        private TMP_Text _headerLabel;
        private TMP_Text _statusLabel;
        private TMP_Dropdown _modeDrop;
        private Toggle _showLabels;

        private readonly List<Marker> _markers = new();
        private bool _suppress;

        /// <summary>
        /// Currently selected units.
        ///
        /// A set that happens to hold at most one, not a single UnitInstance field. Multi-
        /// and formation-select are out of scope here but should not be *precluded*:
        /// commanding at echelon is eventually the core mechanic, and the Command message
        /// already reserves a group addressee. The cost of getting this wrong is not the
        /// selection code, which is small — it is that every call site downstream assumes one
        /// unit, so widening it later means touching the whole ordering path.
        /// </summary>
        private readonly List<UnitId> _selection = new();

        private RectTransform _selectionMark;
        private RectTransform _detailsCard;
        private TMP_Text _detailsTitle;
        private TMP_Text _detailsBody;

        /// <summary>One unit's on-map presence: the symbol and its caption.</summary>
        private sealed class Marker
        {
            public UnitInstance Unit;
            public GameObject Go;
            public Image Icon;
            public TMP_Text Label;
        }

        public string Title => "PLAY";
        public string Key => "play";
        public AppSession Session { set => _session = value; }

        // ─── IAppView ─────────────────────────────────────────────────────────

        public void Build(RectTransform host)
        {
            BuildUi(host);
            PopulateOptions();
            Canvas.ForceUpdateCanvases();
            LoadScenario(ScenarioSamples.SkirmishName);
        }

        public void OnShown()
        {
            _card?.UpdateCrop();
            LayOutMarkers();
        }

        public void OnHidden() => HideDropdownsIn(transform);

        private void OnDestroy() => _card?.Dispose();

        private void Update()
        {
            _card?.PollResize();
            // Markers follow the sheet rather than caching screen positions, so a window
            // resize or a re-crop keeps every symbol on its own ground. Cheap at this unit
            // count; if it ever is not, drive it from the crop changing instead.
            LayOutMarkers();
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
            rootH.childForceExpandWidth = false;   // keep the fixed rail off the surplus
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
            v.spacing = 8;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            _headerLabel = CreateTmp("Header", stage, "SCENARIO", 15, FontStyles.Bold);
            _headerLabel.color = Theme.Ink;
            _headerLabel.characterSpacing = 6f;
            _headerLabel.GetComponent<LayoutElement>().preferredHeight = 22;

            _statusLabel = CreateTmp("Status", stage, string.Empty, 11, FontStyles.Normal);
            _statusLabel.color = Theme.InkMuted;
            _statusLabel.characterSpacing = 1f;
            _statusLabel.GetComponent<LayoutElement>().preferredHeight = 18;

            var holder = CreateRect("CardHolder", stage);
            var hle = holder.gameObject.AddComponent<LayoutElement>();
            hle.flexibleHeight = 1f;
            hle.minHeight = 320f;

            _card = new MapSheetCard(holder, preferredHeight: 0, out var stack);

            // Symbols overlay the sheet, inside the card's own stack so they move with it.
            _unitLayer = CreateRect("Units", stack);
            Stretch(_unitLayer);

            // Masked, because a square map in a wide card is cropped vertically and units
            // near the map's north or south edge then fall outside it. Without this they are
            // still drawn — floating over the tab bar and the rail, which looks like a
            // layout failure rather than a unit being off-screen.
            _unitLayer.gameObject.AddComponent<RectMask2D>();

            // Selection brackets live in the unit layer so they are clipped with it, and
            // above the symbols so they read as chrome rather than part of a symbol.
            _selectionMark = CreateRect("Selection", _unitLayer);
            _selectionMark.anchorMin = _selectionMark.anchorMax = new Vector2(0.5f, 0.5f);
            _selectionMark.pivot = new Vector2(0.5f, 0.5f);
            _selectionMark.sizeDelta = new Vector2(SymbolSize * 1.5f, SymbolSize * 1.5f);
            var markImg = _selectionMark.gameObject.AddComponent<Image>();
            markImg.sprite = UiSprites.SelectionBrackets;
            markImg.color = Theme.Accent;
            markImg.raycastTarget = false;
            _selectionMark.gameObject.SetActive(false);

            // Pointer handling goes on the sheet itself, as MapExplorerView does, so a later
            // scroll-to-zoom cannot fight the rail's ScrollRect for the wheel.
            _card.Image.raycastTarget = true;
            var region = _card.Rect.gameObject.AddComponent<PointerRegion>();
            region.Clicked = OnMapClicked;

            // The marginalia strip is built by the card but must stay above the symbols.
            var strip = stack.Find("Marginalia");
            if (strip != null) strip.SetAsLastSibling();
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
            var cl = CreateTmp("L", chrome, "ORDER OF BATTLE", 14, FontStyles.Bold, withLayout: false);
            Stretch(cl.rectTransform);
            cl.alignment = TextAlignmentOptions.Center;
            cl.color = Theme.AccentText;
            cl.characterSpacing = 6f;

            var content = UiScroll.CreateColumn("Scroll", panel, Theme.RailBg, out var scroll);
            var srt = (RectTransform)scroll.transform;
            srt.offsetMin = new Vector2(2, 0);
            srt.offsetMax = new Vector2(0, -38);

            AddSection(content, "SELECTED UNIT");
            BuildDetailsCard(content);

            AddSection(content, "PRESENTATION");
            _modeDrop = AddDropdown(content, "RENDER MODE", RefreshSheet);
            _showLabels = AddToggle(content, "UNIT LABELS", true, LayOutMarkers);

            _orbatRoot = CreateRect("Orbat", content);
            var ov = _orbatRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            ov.spacing = 2;
            ov.childControlWidth = true;
            ov.childControlHeight = true;
            ov.childForceExpandWidth = true;
            ov.childForceExpandHeight = false;
            _orbatRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
        }

        private RectTransform _orbatRoot;

        private void PopulateOptions()
        {
            _suppress = true;
            SetDrop(_modeDrop, DisplayNames.RenderModeLabels(), 1);   // Topographic
            _suppress = false;
        }

        // ─── Scenario ─────────────────────────────────────────────────────────

        private void LoadScenario(string name)
        {
            _scenario = ScenarioIO.Load(name);
            if (_scenario == null)
            {
                Debug.LogError($"[PlayView] no scenario '{name}' in Resources/{ScenarioIO.ResourceFolder}");
                _statusLabel.text = $"SCENARIO '{name.ToUpperInvariant()}' NOT FOUND";
                return;
            }

            _map = _scenario.GenerateMap();

            // With the map in hand, validation can also check that each unit is standing
            // somewhere its own capabilities allow — a unit in a lake draws perfectly well.
            var problems = _scenario.Validate(UnitCatalogue.Default(), _map);
            foreach (var p in problems) Debug.LogWarning($"[PlayView] {_scenario.Name}: {p}");

            _headerLabel.text = _scenario.Name.ToUpperInvariant();

            // Hyphens and middots only; the atlas has no en dash and renders it as nothing.
            _statusLabel.text =
                $"{_scenario.Sides.Count} SIDES   ·   {_scenario.Units.Count} UNITS   ·   " +
                $"{_map.Header.WidthMetres / 1000f:0.#} × {_map.Header.HeightMetres / 1000f:0.#} KM" +
                (problems.Count > 0 ? $"   ·   {problems.Count} VALIDATION WARNING(S)" : string.Empty);

            RefreshSheet();
            BuildMarkers();
            BuildOrbat();
            ClearSelection();

            Debug.Log($"[PlayView] {_scenario} — {problems.Count} validation problem(s)");
        }

        private void RefreshSheet()
        {
            if (_suppress || _map == null || _card == null) return;

            var options = MapRenderOptions.Default;
            options.Mode = Pick(DisplayNames.RenderModes, _modeDrop, MapRenderMode.Topographic);
            options.PixelsPerCell =
                Mathf.Clamp(1600f / Mathf.Max(_map.Width, _map.Height), 0.5f, 4f);

            _card.Render(_map, options);
            _card.SetMarginaliaFor(_map, _scenario.Map.Seed);
            LayOutMarkers();
        }

        // ─── Unit markers ─────────────────────────────────────────────────────

        private void BuildMarkers()
        {
            foreach (var m in _markers) Destroy(m.Go);
            _markers.Clear();
            if (_scenario == null) return;

            foreach (var unit in _scenario.Units)
                _markers.Add(CreateMarker(unit));

            LayOutMarkers();
        }

        private Marker CreateMarker(UnitInstance unit)
        {
            var rt = CreateRect($"Unit_{unit.Id}", _unitLayer);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(SymbolSize, SymbolSize);

            var icon = rt.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            // From the shared cache — never Destroy what this returns. Only ClearCache may,
            // and it frees the textures for every holder.
            icon.sprite = _session.Symbols.GetSymbolSprite(unit.ToSidcCode(), BakeSize);
            if (icon.sprite == null) icon.color = new Color(0, 0, 0, 0);

            var label = CreateOverlayTmp("Label", rt, unit.Designation, 10, Theme.Ink);
            label.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.sizeDelta = new Vector2(140, 14);
            label.rectTransform.anchoredPosition = new Vector2(0, -2);
            label.alignment = TextAlignmentOptions.Top;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;

            return new Marker { Unit = unit, Go = rt.gameObject, Icon = icon, Label = label };
        }

        /// <summary>
        /// Places every marker from its cell position through the card's transform.
        ///
        /// Recomputed rather than cached, so pan, zoom, re-render and window resize all keep
        /// symbols on their own ground — the acceptance criterion that a screenshot at one
        /// zoom cannot demonstrate on its own.
        /// </summary>
        private void LayOutMarkers()
        {
            if (_card == null) return;

            bool labels = _showLabels == null || _showLabels.isOn;

            // The frame is left of centre inside the baked texture, so placing the sprite by
            // its pivot would put the *texture* centre on the unit's position and leave every
            // symbol visibly offset. Shift by the pivot-to-frame-centre vector instead.
            Vector2 frameOffset = SymbolLayout.PivotToFrameCentre * SymbolSize;

            bool markedThisPass = false;

            foreach (var m in _markers)
            {
                bool visible = _card.CellToLocal(m.Unit.Cell, out var local);
                m.Go.SetActive(visible);
                if (!visible) continue;

                ((RectTransform)m.Go.transform).anchoredPosition = local - frameOffset;
                m.Label.gameObject.SetActive(labels);

                // The brackets sit on the frame, not on the marker's centre, so they stay
                // concentric with the symbol the user is actually looking at.
                if (_selectionMark != null && IsSelected(m.Unit.Id))
                {
                    _selectionMark.anchoredPosition = local;
                    _selectionMark.gameObject.SetActive(true);
                    markedThisPass = true;
                }
            }

            // Hidden when nothing is selected, and also when the selected unit is scrolled
            // out of the cropped view — brackets floating over empty ground would imply a
            // unit is there.
            if (_selectionMark != null && !markedThisPass)
                _selectionMark.gameObject.SetActive(false);
        }

        // ─── Selection ────────────────────────────────────────────────────────

        private void BuildDetailsCard(Transform parent)
        {
            _detailsCard = CreateRect("Details", parent);
            _detailsCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 92;
            _detailsCard.gameObject.AddComponent<Image>().color = UiTheme.CardBg;

            _detailsTitle = CreateTmp("T", _detailsCard, string.Empty, 13, FontStyles.Bold,
                withLayout: false);
            _detailsTitle.rectTransform.anchorMin = new Vector2(0, 1);
            _detailsTitle.rectTransform.anchorMax = new Vector2(1, 1);
            _detailsTitle.rectTransform.pivot = new Vector2(0.5f, 1);
            _detailsTitle.rectTransform.sizeDelta = new Vector2(-20, 22);
            _detailsTitle.rectTransform.anchoredPosition = new Vector2(0, -6);
            _detailsTitle.alignment = TextAlignmentOptions.MidlineLeft;
            _detailsTitle.color = Theme.Ink;
            _detailsTitle.characterSpacing = 2f;

            _detailsBody = CreateTmp("B", _detailsCard, string.Empty, 11, FontStyles.Normal,
                withLayout: false);
            Stretch(_detailsBody.rectTransform);
            _detailsBody.rectTransform.offsetMin = new Vector2(10, 6);
            _detailsBody.rectTransform.offsetMax = new Vector2(-10, -28);
            _detailsBody.alignment = TextAlignmentOptions.TopLeft;
            _detailsBody.color = Theme.InkMuted;
            _detailsBody.lineSpacing = 12f;

            ClearSelection();
        }

        private void OnMapClicked(UnityEngine.EventSystems.PointerEventData e)
        {
            var hit = UnitAt(e);
            if (hit == null) ClearSelection();
            else Select(hit.Id);
        }

        /// <summary>
        /// The unit under the pointer, or null.
        ///
        /// Hit-tested in the card's LOCAL space against the drawn symbol size, not in cell
        /// space. Symbols are drawn at a fixed on-screen size, so a fixed local radius always
        /// matches what is actually on screen; a fixed *cell* radius would shrink away as you
        /// zoom out and make units progressively harder to click. (The issue's note said cell
        /// space — that reasoning was written before #6 fixed the symbol size on screen.)
        ///
        /// Nearest wins rather than first found, so overlapping units resolve to the one the
        /// pointer is actually closest to instead of whichever happens to come first in the
        /// scenario's list.
        /// </summary>
        private UnitInstance UnitAt(UnityEngine.EventSystems.PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _card.Rect, e.position, e.pressEventCamera, out var click))
                return null;

            float radius = SymbolSize * 0.6f;
            float bestSq = radius * radius;
            UnitInstance best = null;

            // Test against `local`, which is where the *frame* is drawn. LayOutMarkers
            // offsets each marker's centre by -frameOffset precisely so the frame lands on
            // the unit's position, so the frame — the thing the eye targets — is at `local`.
            foreach (var m in _markers)
            {
                if (!m.Go.activeSelf) continue;
                if (!_card.CellToLocal(m.Unit.Cell, out var local)) continue;

                float d = (click - local).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = m.Unit; }
            }

            return best;
        }

        private void Select(UnitId id)
        {
            _selection.Clear();
            if (id.IsValid) _selection.Add(id);
            RefreshSelection();
        }

        private void ClearSelection()
        {
            _selection.Clear();
            RefreshSelection();
        }

        private bool IsSelected(UnitId id) => _selection.Contains(id);

        private void RefreshSelection()
        {
            UnitInstance unit = null;
            if (_selection.Count > 0 && _scenario != null)
                unit = _scenario.FindUnit(_selection[0]);

            if (_detailsTitle != null)
            {
                if (unit == null)
                {
                    _detailsTitle.text = "NONE";
                    _detailsBody.text = "Click a unit on the map.";
                }
                else
                {
                    var side = _scenario.FindSide(unit.Side);
                    var caps = unit.Capabilities(UnitCatalogue.Default());
                    var code = unit.ToSidcCode();

                    int cx = Mathf.Clamp(Mathf.RoundToInt(unit.Cell.x), 0, _map.Width - 1);
                    int cy = Mathf.Clamp(Mathf.RoundToInt(unit.Cell.y), 0, _map.Height - 1);

                    _detailsTitle.text = string.IsNullOrEmpty(unit.Designation)
                        ? unit.Id.ToString()
                        : unit.Designation.ToUpperInvariant();

                    // Hyphens and middots only — the atlas renders an en dash as nothing.
                    _detailsBody.text =
                        $"{side?.Name ?? "?"}   ·   {DisplayNames.EchelonName(code.Echelon)}   ·   " +
                        $"{DisplayNames.UnitTypeLabel(code.EntityCode)}\n" +
                        $"{caps.Name}   ·   STR {unit.Strength}%   ·   " +
                        $"RDY {unit.Readiness:0}%   ·   EFF {unit.Effectiveness * 100f:0}%\n" +
                        $"{unit.Mgrs(_map)}   ·   {unit.Elevation(_map):0} M   ·   " +
                        $"{LandcoverInfo.DisplayName(unit.Landcover(_map)).ToUpperInvariant()}   ·   " +
                        $"SLOPE {_map.SampleSlopeDegrees(cx, cy):0} DEG";
                }
            }

            LayOutMarkers();
            RefreshOrbatHighlight();
        }

        // ─── Order of battle ──────────────────────────────────────────────────

        /// <summary>Row background per unit id, so the selected one can be picked out.</summary>
        private readonly Dictionary<int, Image> _orbatRows = new();

        private void RefreshOrbatHighlight()
        {
            foreach (var kv in _orbatRows)
            {
                if (kv.Value == null) continue;
                kv.Value.color = IsSelected(new UnitId(kv.Key))
                    ? UiTheme.SelectFill
                    : UiTheme.CardBg;
            }
        }

        private void BuildOrbat()
        {
            for (int i = _orbatRoot.childCount - 1; i >= 0; i--)
                Destroy(_orbatRoot.GetChild(i).gameObject);
            _orbatRows.Clear();
            if (_scenario == null) return;

            var catalogue = UnitCatalogue.Default();

            foreach (var side in _scenario.Sides)
            {
                var head = CreateRect($"Side_{side.Id}", _orbatRoot);
                head.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
                head.gameObject.AddComponent<Image>().color = Theme.SectionBg;

                // A side's own colour, not its affiliation's — two allied contingents share
                // an affiliation and still need telling apart.
                var swatch = CreateRect("Swatch", head);
                swatch.anchorMin = new Vector2(0, 0);
                swatch.anchorMax = new Vector2(0, 1);
                swatch.pivot = new Vector2(0, 0.5f);
                swatch.sizeDelta = new Vector2(6, 0);
                swatch.gameObject.AddComponent<Image>().color = side.Colour;

                var t = CreateTmp("T", head,
                    $"{side.Name.ToUpperInvariant()}   ·   {_scenario.CountUnitsOf(side.Id)} UNITS",
                    12, FontStyles.Bold, withLayout: false);
                Stretch(t.rectTransform);
                t.rectTransform.offsetMin = new Vector2(14, 0);
                t.alignment = TextAlignmentOptions.MidlineLeft;
                t.color = Theme.Ink;
                t.characterSpacing = 2f;

                foreach (var unit in _scenario.UnitsOf(side.Id))
                {
                    var caps = unit.Capabilities(catalogue);
                    var row = CreateRect($"Unit_{unit.Id}", _orbatRoot);
                    row.gameObject.AddComponent<LayoutElement>().preferredHeight = 34;

                    // Selectable from the list as well as the map. Cheap, and the list is the
                    // only way to reach a unit that is currently cropped off the sheet.
                    var rowImg = row.gameObject.AddComponent<Image>();
                    rowImg.color = UiTheme.CardBg;
                    var rowBtn = row.gameObject.AddComponent<Button>();
                    rowBtn.targetGraphic = rowImg;
                    var rowColors = ColorBlock.defaultColorBlock;
                    rowColors.normalColor = Color.white;
                    rowColors.highlightedColor = new Color(0.94f, 0.94f, 0.90f);
                    rowColors.pressedColor = new Color(0.88f, 0.90f, 0.86f);
                    rowColors.selectedColor = Color.white;
                    rowColors.fadeDuration = 0.05f;
                    rowBtn.colors = rowColors;

                    var captured = unit.Id;
                    rowBtn.onClick.AddListener(() => Select(captured));
                    _orbatRows[captured.Value] = rowImg;

                    var name = CreateTmp("N", row,
                        string.IsNullOrEmpty(unit.Designation) ? unit.Id.ToString() : unit.Designation,
                        12, FontStyles.Bold, withLayout: false);
                    name.rectTransform.anchorMin = new Vector2(0, 0.5f);
                    name.rectTransform.anchorMax = new Vector2(1, 1f);
                    name.rectTransform.offsetMin = new Vector2(14, 0);
                    name.rectTransform.offsetMax = new Vector2(-8, 0);
                    name.alignment = TextAlignmentOptions.MidlineLeft;
                    name.color = Theme.Ink;

                    var detail = CreateTmp("D", row,
                        $"{caps.Name}   ·   {unit.Strength}%   ·   " +
                        $"{unit.Mgrs(_map)}   ·   " +
                        $"{LandcoverInfo.DisplayName(unit.Landcover(_map)).ToUpperInvariant()}",
                        10, FontStyles.Normal, withLayout: false);
                    detail.rectTransform.anchorMin = new Vector2(0, 0f);
                    detail.rectTransform.anchorMax = new Vector2(1, 0.5f);
                    detail.rectTransform.offsetMin = new Vector2(14, 0);
                    detail.rectTransform.offsetMax = new Vector2(-8, 0);
                    detail.alignment = TextAlignmentOptions.MidlineLeft;
                    detail.color = Theme.InkMuted;
                }
            }
        }
    }
}
