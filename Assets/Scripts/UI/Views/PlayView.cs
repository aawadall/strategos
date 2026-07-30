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

            foreach (var m in _markers)
            {
                bool visible = _card.CellToLocal(m.Unit.Cell, out var local);
                m.Go.SetActive(visible);
                if (!visible) continue;

                ((RectTransform)m.Go.transform).anchoredPosition = local - frameOffset;
                m.Label.gameObject.SetActive(labels);
            }
        }

        // ─── Order of battle ──────────────────────────────────────────────────

        private void BuildOrbat()
        {
            for (int i = _orbatRoot.childCount - 1; i >= 0; i--)
                Destroy(_orbatRoot.GetChild(i).gameObject);
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
