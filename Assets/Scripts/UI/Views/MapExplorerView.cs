// MapExplorerView.cs
// Inspect a generated map: pan, zoom, read coordinates and elevation off it, and turn the
// renderer's layers on and off.
//
// PAN AND ZOOM WITHOUT RE-RENDERING PER FRAME
// MapRasterizer re-traces contours over the WHOLE map on every call regardless of
// CellWindow, so a live re-render on drag is not affordable. So:
//
//     uvRect is the interactive transform; CellWindow is the committed one.
//
// Dragging and scrolling move and scale the RawImage's uvRect, which is free and exactly
// correct, just soft once you are past 1:1. When the gesture settles, the accumulated
// uvRect is converted into a cell window, the sheet is re-rendered at a pixel density
// derived from the new zoom, and uvRect returns to the plain aspect-fit crop. This is the
// same trick MapSheetCard already uses to fit a sheet, generalised from "fit" to
// "transform".

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Strategos.Maps;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI.Views
{
    public sealed class MapExplorerView : MonoBehaviour, IAppView
    {
        /// <summary>Seconds of stillness after a gesture before the window is committed.</summary>
        private const float SettleDelay = 0.15f;

        /// <summary>
        /// Pixel-density bounds. The lower bound keeps an overview from becoming a smear;
        /// the upper stops a deep zoom allocating an enormous texture.
        /// </summary>
        private const float MinPixelsPerCell = 0.25f;
        private const float MaxPixelsPerCell = 8f;

        private AppSession _session;
        private bool _suppress;
        private int _seenGeneration = -1;

        private MapSheetCard _card;
        private TMP_Text _readout;

        private TMP_Dropdown _modeDrop;
        private Toggle _hillshade, _contours, _areas, _lines, _pois, _labels, _gridToggle, _fast;
        private Slider _exaggeration, _sunAzimuth;

        /// <summary>Cell-space window currently rendered. Null means the whole map.</summary>
        private Rect? _window;

        private Coroutine _settle;
        private bool _dirtyWindow;

        public string Title => "MAP";
        public string Key => "map";
        public AppSession Session { set => _session = value; }

        // ─── IAppView ─────────────────────────────────────────────────────────

        public void Build(RectTransform host)
        {
            BuildUi(host);
            PopulateOptions();
            Canvas.ForceUpdateCanvases();
            RefreshMap();
        }

        public void OnShown()
        {
            // Only re-render if the scenario view regenerated while this was hidden.
            if (_session != null && _session.Generation != _seenGeneration) RefreshMap();
            else _card?.UpdateCrop();
        }

        public void OnHidden() => HideDropdownsIn(transform);

        private void OnDestroy() => _card?.Dispose();

        private void Update() => _card?.PollResize();

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

            // --- Stage ---
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

            var head = CreateRect("Head", stage);
            head.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;
            _readout = CreateTmp("Readout", head, "MOVE THE CURSOR OVER THE MAP", 12,
                FontStyles.Bold, withLayout: false);
            Stretch(_readout.rectTransform);
            _readout.alignment = TextAlignmentOptions.MidlineLeft;
            _readout.color = Theme.InkMuted;
            _readout.characterSpacing = 2f;

            // The card fills the stage; 0 height means "stretch", so give it flexible height
            // inside the vertical group instead.
            var holder = CreateRect("CardHolder", stage);
            var hle = holder.gameObject.AddComponent<LayoutElement>();
            hle.flexibleHeight = 1f;
            hle.minHeight = 300f;

            _card = new MapSheetCard(holder, preferredHeight: 0, out _);

            // Pointer handling goes on the sheet itself so the rail's ScrollRect never
            // competes with scroll-to-zoom.
            var region = _card.Rect.gameObject.AddComponent<PointerRegion>();
            _card.Image.raycastTarget = true;
            region.Dragged  = OnDrag;
            region.Scrolled = OnScroll;
            region.Moved    = OnMove;
            region.Released = OnReleased;

            BuildRail(root);
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
            var cl = CreateTmp("L", chrome, "SHEET", 14, FontStyles.Bold, withLayout: false);
            Stretch(cl.rectTransform);
            cl.alignment = TextAlignmentOptions.Center;
            cl.color = Theme.AccentText;
            cl.characterSpacing = 6f;

            var content = UiScroll.CreateColumn("Scroll", panel, Theme.RailBg, out var scroll);
            var srt = (RectTransform)scroll.transform;
            srt.offsetMin = new Vector2(2, 0);
            srt.offsetMax = new Vector2(0, -38);

            AddSection(content, "PRESENTATION");
            _modeDrop = AddDropdown(content, "RENDER MODE", OnRenderOptionChanged);

            AddSection(content, "LAYERS");
            // One toggle per Draw* flag on MapRenderOptions. No new code paths — it just
            // makes the renderer's fixed layer order something a person can see.
            _hillshade  = AddToggle(content, "HILLSHADE", true,  OnRenderOptionChanged);
            _contours   = AddToggle(content, "CONTOURS", true,   OnRenderOptionChanged);
            _areas      = AddToggle(content, "AREAS", true,      OnRenderOptionChanged);
            _lines      = AddToggle(content, "LINES (RIVERS / ROADS)", true, OnRenderOptionChanged);
            _pois       = AddToggle(content, "POINT MARKS", true, OnRenderOptionChanged);
            _labels     = AddToggle(content, "LABELS", true,     OnRenderOptionChanged);
            _gridToggle = AddToggle(content, "GRID", true,       OnRenderOptionChanged);

            AddSection(content, "RELIEF SHADING");
            (_exaggeration, _) = AddSlider(content, "EXAGGERATION", 1, 8, 3,
                OnRenderOptionChanged, format: "0", suffix: "x");
            (_sunAzimuth, _) = AddSlider(content, "SUN AZIMUTH", 0, 359, 315,
                OnRenderOptionChanged, format: "0", suffix: " DEG");

            AddSection(content, "INTERACTION");
            // Contours dominate the cost of a render, so this is the honest lever when
            // panning at high zoom feels heavy.
            _fast = AddToggle(content, "FAST (DROP CONTOURS)", false, OnRenderOptionChanged);
            AddButton(content, "RESET VIEW", ResetView);
            AddButton(content, "NEW MAP", NewMap);
        }

        private void PopulateOptions()
        {
            _suppress = true;
            SetDrop(_modeDrop, DisplayNames.RenderModeLabels(), 1);   // Topographic
            _suppress = false;
        }

        // ─── Rendering ────────────────────────────────────────────────────────

        private MapRenderOptions CurrentOptions(float pixelsPerCell)
        {
            var o = MapRenderOptions.Default;
            o.Mode = Pick(DisplayNames.RenderModes, _modeDrop, MapRenderMode.Topographic);
            o.PixelsPerCell = pixelsPerCell;
            o.CellWindow = _window;

            o.DrawHillshade = _hillshade.isOn;
            o.DrawContours  = _contours.isOn && !_fast.isOn;
            o.DrawAreas     = _areas.isOn;
            o.DrawLines     = _lines.isOn;
            o.DrawPois      = _pois.isOn;
            o.DrawLabels    = _labels.isOn;
            o.DrawGrid      = _gridToggle.isOn;

            o.HillshadeExaggeration = _exaggeration.value;
            o.SunAzimuthDegrees = _sunAzimuth.value;
            return o;
        }

        /// <summary>
        /// Pixel density for the current window: enough pixels to fill the card, so detail
        /// arrives as you zoom in and the renderer's own generalisation takes over as you
        /// zoom out.
        /// </summary>
        private float PixelsPerCellForWindow(MapData map)
        {
            float cardWidth = _card.Rect.rect.width;
            if (cardWidth < 1f) cardWidth = 900f;

            float windowCells = _window?.width ?? map.Width;
            if (windowCells < 1f) windowCells = map.Width;

            return Mathf.Clamp(cardWidth / windowCells, MinPixelsPerCell, MaxPixelsPerCell);
        }

        private void RefreshMap()
        {
            if (_session == null || _card == null) return;

            var map = _session.EnsureMap();
            _seenGeneration = _session.Generation;

            _card.Render(map, CurrentOptions(PixelsPerCellForWindow(map)));
            _card.SetMarginaliaFor(map, _session.Settings.Seed);
        }

        private void OnRenderOptionChanged()
        {
            if (_suppress) return;
            RefreshMap();
        }

        private void ResetView()
        {
            _window = null;
            _card.Image.uvRect = new Rect(0, 0, 1, 1);
            RefreshMap();
        }

        private void NewMap()
        {
            _window = null;
            _session.Reseed();
            RefreshMap();
        }

        // ─── Interaction ──────────────────────────────────────────────────────

        private void OnDrag(PointerEventData e)
        {
            var uv = _card.Image.uvRect;
            Rect card = _card.Rect.rect;
            if (card.width < 1f || card.height < 1f) return;

            // Drag moves the sheet with the cursor, so the window moves against it.
            uv.x -= e.delta.x / card.width  * uv.width;
            uv.y -= e.delta.y / card.height * uv.height;
            _card.Image.uvRect = uv;

            _dirtyWindow = true;
        }

        private void OnScroll(PointerEventData e)
        {
            if (Mathf.Approximately(e.scrollDelta.y, 0f)) return;

            var uv = _card.Image.uvRect;
            float factor = e.scrollDelta.y > 0f ? 0.85f : 1f / 0.85f;

            // Zoom about the cursor, not the centre, so the feature under the pointer stays
            // under the pointer.
            if (LocalToUv(e, out var pivot))
            {
                float nw = uv.width * factor;
                float nh = uv.height * factor;
                uv.x = pivot.x - (pivot.x - uv.x) * (nw / uv.width);
                uv.y = pivot.y - (pivot.y - uv.y) * (nh / uv.height);
                uv.width = nw;
                uv.height = nh;
                _card.Image.uvRect = uv;
            }

            _dirtyWindow = true;
            RestartSettle();
        }

        private void OnReleased(PointerEventData e)
        {
            if (_dirtyWindow) RestartSettle();
        }

        private void RestartSettle()
        {
            if (_settle != null) StopCoroutine(_settle);
            _settle = StartCoroutine(CommitAfterSettle());
        }

        /// <summary>
        /// Converts the accumulated uvRect into a cell window and re-renders at the density
        /// that window deserves. Deferred until the gesture stops because a render costs a
        /// whole-map contour trace.
        /// </summary>
        private IEnumerator CommitAfterSettle()
        {
            yield return new WaitForSeconds(SettleDelay);
            _settle = null;
            if (!_dirtyWindow) yield break;
            _dirtyWindow = false;

            var map = _session?.Map;
            if (map == null) yield break;

            var view = _card.Viewport;
            var uv = _card.Image.uvRect;

            // uv is in texture space; the viewport says what cells that texture covers.
            var w = view.CellWindow;
            var next = new Rect(
                w.x + uv.x * w.width,
                w.y + uv.y * w.height,
                w.width * uv.width,
                w.height * uv.height);

            // Never zoom out past the whole map, and keep the window on the map.
            if (next.width >= map.Width || next.height >= map.Height)
            {
                _window = null;
            }
            else
            {
                float minCells = 8f;
                next.width  = Mathf.Max(minCells, next.width);
                next.height = Mathf.Max(minCells, next.height);
                // The viewport's own convention: a cell coordinate names a sample point, so
                // a whole-map window starts at -0.5, not 0.
                next.x = Mathf.Clamp(next.x, -0.5f, map.Width  - 0.5f - next.width);
                next.y = Mathf.Clamp(next.y, -0.5f, map.Height - 0.5f - next.height);
                _window = next;
            }

            _card.Image.uvRect = new Rect(0, 0, 1, 1);
            RefreshMap();
        }

        private void OnMove(PointerEventData e)
        {
            var map = _session?.Map;
            if (map == null || _readout == null) return;
            if (!LocalToUv(e, out var uv)) return;

            // uv -> texture pixel -> cell, through the viewport the sheet was rendered with.
            var view = _card.Viewport;
            var shown = _card.Image.uvRect;
            float tx = shown.x + uv.x * shown.width;
            float ty = shown.y + uv.y * shown.height;

            int px = Mathf.Clamp(Mathf.FloorToInt(tx * view.Width), 0, view.Width - 1);
            int py = Mathf.Clamp(Mathf.FloorToInt(ty * view.Height), 0, view.Height - 1);
            Vector2 cell = view.PixelToCell(px, py);

            int cx = Mathf.Clamp(Mathf.RoundToInt(cell.x), 0, map.Width - 1);
            int cy = Mathf.Clamp(Mathf.RoundToInt(cell.y), 0, map.Height - 1);

            string mgrs = MapCoordinates.FormatMgrs(map.Header, cell.x, cell.y);
            float elevation = map.SampleElevation(cell.x, cell.y);
            var cover = LandcoverInfo.DisplayName(map.GetLandcover(cx, cy));
            float slope = map.SampleSlopeDegrees(cx, cy);

            // Hyphens and middots only: the atlas has no en dash and it renders as nothing.
            _readout.text =
                $"{mgrs}   ·   {elevation:0} M   ·   {cover.ToUpperInvariant()}   ·   " +
                $"SLOPE {slope:0} DEG   ·   {view.PixelsPerCell:0.##} PX/CELL";
        }

        /// <summary>Pointer position as a 0-1 coordinate within the sheet rect.</summary>
        private bool LocalToUv(PointerEventData e, out Vector2 uv)
        {
            uv = default;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _card.Rect, e.position, e.pressEventCamera, out var local))
                return false;

            Rect r = _card.Rect.rect;
            if (r.width < 1f || r.height < 1f) return false;

            uv = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
            return true;
        }
    }
}
