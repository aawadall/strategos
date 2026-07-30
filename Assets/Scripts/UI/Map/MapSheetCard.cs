// MapSheetCard.cs
// A framed map sheet: the RawImage showing a rendered texture, the marginalia strip along
// its foot, and the crop that keeps the sheet's scale honest as the card resizes.
//
// Extracted from SymbolBuilderPanel, which had the only one. Three places need it now —
// the builder's underlay, the map explorer, and the scenario view's 2D preview — and the
// crop rule below is the sort of thing that gets "simplified" into a bug if each of them
// reimplements it.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Strategos.Maps;

namespace Strategos.UI
{
    public sealed class MapSheetCard
    {
        private readonly RawImage _image;
        private readonly TMP_Text _marginalia;
        private Texture2D _texture;
        private Vector2 _lastCardSize;

        /// <summary>The rect the sheet is drawn into. Pan/zoom handlers attach here.</summary>
        public RectTransform Rect => _image.rectTransform;

        /// <summary>The RawImage itself, for callers that drive uvRect directly.</summary>
        public RawImage Image => _image;

        /// <summary>The sheet currently displayed, or null before the first SetSheet.</summary>
        public Texture2D Texture => _texture;

        /// <summary>Viewport the current sheet was rendered with. Needed to map pixels back to cells.</summary>
        public MapViewport Viewport { get; private set; }

        /// <summary>
        /// Builds the card into <paramref name="parent"/>.
        /// </summary>
        /// <param name="preferredHeight">
        /// LayoutElement height when the parent is a layout group. Pass 0 to stretch to the
        /// parent instead, which is what a full-bleed explorer wants.
        /// </param>
        /// <param name="content">
        /// Receives the stack rect, for callers that want to overlay their own children —
        /// the builder puts a halo and a symbol on top of the sheet.
        /// </param>
        public MapSheetCard(Transform parent, float preferredHeight, out RectTransform content,
            bool withMarginalia = true)
        {
            var card = UiFactory.CreateRect("MapCard", parent);
            if (preferredHeight > 0f)
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = preferredHeight;
            else
                UiFactory.Stretch(card);
            card.gameObject.AddComponent<Image>().color = UiTheme.CardLine;

            var stack = UiFactory.CreateRect("MapStack", card);
            UiFactory.Stretch(stack);
            stack.offsetMin = new Vector2(2, 2);
            stack.offsetMax = new Vector2(-2, -2);
            stack.gameObject.AddComponent<Image>().color = UiTheme.MapPaper;

            var sheet = UiFactory.CreateRect("Sheet", stack);
            UiFactory.Stretch(sheet);
            _image = sheet.gameObject.AddComponent<RawImage>();
            _image.color = Color.white;
            _image.raycastTarget = false;
            // A real texture arrives from SetSheet, which cannot run until whatever drives
            // generation exists.
            _image.texture = Texture2D.whiteTexture;

            if (withMarginalia) _marginalia = BuildMarginalia(stack);

            content = stack;
        }

        /// <summary>
        /// The strip along the foot of the sheet carrying seed, extent, elevation range and
        /// contour interval — a map's marginalia. Without it the sheet is a picture; with it
        /// the reader knows what ground and what scale they are on. It doubles as the
        /// in-frame revision marker when you are unsure which build you are looking at.
        /// </summary>
        private static TMP_Text BuildMarginalia(Transform stack)
        {
            var strip = UiFactory.CreateRect("Marginalia", stack);
            strip.anchorMin = new Vector2(0, 0);
            strip.anchorMax = new Vector2(1, 0);
            strip.pivot = new Vector2(0.5f, 0);
            strip.sizeDelta = new Vector2(0, 20);

            var stripImg = strip.gameObject.AddComponent<Image>();
            stripImg.color = new Color(1f, 1f, 0.98f, 0.72f);
            stripImg.raycastTarget = false;

            var label = UiFactory.CreateOverlayTmp("Info", strip, string.Empty, 10, UiTheme.InkMuted);
            UiFactory.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(8, 0);
            label.rectTransform.offsetMax = new Vector2(-8, 0);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.characterSpacing = 1.5f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// Renders <paramref name="map"/> and shows the result, replacing any previous sheet.
        ///
        /// Uses RenderPixels rather than Render so the viewport comes back with the pixels:
        /// a card that cannot map a pixel to a cell cannot report coordinates under the
        /// cursor, and rederiving the transform from the texture size is guesswork.
        /// </summary>
        public void Render(MapData map, MapRenderOptions options)
        {
            if (map == null) return;

            var pixels = MapRasterizer.RenderPixels(map, options, out var view);
            var tex = new Texture2D(view.Width, view.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
                name       = "MapSheet",
            };
            tex.SetPixels32(pixels);
            tex.Apply(false);
            SetSheet(tex, view);
        }

        /// <summary>
        /// Shows a new sheet and disposes the one it replaces. The card owns the texture
        /// from here on.
        /// </summary>
        public void SetSheet(Texture2D texture, MapViewport viewport)
        {
            Viewport = viewport;
            _image.texture = texture;
            if (_texture != null && _texture != texture)
                UnityEngine.Object.Destroy(_texture);
            _texture = texture;
            UpdateCrop();
        }

        /// <summary>Sets the marginalia text verbatim.</summary>
        public void SetMarginalia(string text)
        {
            if (_marginalia != null) _marginalia.text = text;
        }

        /// <summary>
        /// Standard marginalia for a whole map: seed, extent, elevation range, contour.
        ///
        /// Hyphen, not an en dash. U+2013 is not in the bundled LiberationSans SDF atlas
        /// and renders as *nothing at all*, so an elevation range came out as "202 280 M" —
        /// which reads as a formatting bug rather than a missing glyph. Latin-1 punctuation
        /// only here; see the glyph coverage note in CLAUDE.md.
        /// </summary>
        public void SetMarginaliaFor(MapData map, int seed)
        {
            if (_marginalia == null || map == null) return;
            var h = map.Header;
            _marginalia.text =
                $"SEED {seed}   ·   {h.WidthMetres / 1000f:0.#} × " +
                $"{h.HeightMetres / 1000f:0.#} KM   ·   " +
                $"{h.MinElevation:0}-{h.MaxElevation:0} M   ·   " +
                $"CONTOUR {h.ContourInterval:0} M";
        }

        /// <summary>
        /// Fits the sheet to the card by cropping to a centred region of the card's shape,
        /// never by stretching.
        ///
        /// A stretched map has a different scale on each axis, so every distance and bearing
        /// read off it is wrong — for a map that is a correctness problem, not a cosmetic
        /// one. The card's aspect follows the window, so this is re-evaluated on resize
        /// rather than baked into the generated sheet, because regenerating would stall for
        /// a few hundred milliseconds and re-cropping is free.
        /// </summary>
        public void UpdateCrop()
        {
            if (_texture == null) return;

            Rect card = _image.rectTransform.rect;
            if (card.width < 1f || card.height < 1f) return;

            _lastCardSize = new Vector2(card.width, card.height);

            float cardAspect = card.width / card.height;
            float texAspect  = _texture.width / (float)_texture.height;

            float uvW = 1f, uvH = 1f;
            if (cardAspect > texAspect) uvH = texAspect / cardAspect;
            else                        uvW = cardAspect / texAspect;

            _image.uvRect = new Rect((1f - uvW) * 0.5f, (1f - uvH) * 0.5f, uvW, uvH);
        }

        /// <summary>
        /// Cell coordinate to a local position inside <see cref="Rect"/>, ready for an
        /// overlay's <c>anchoredPosition</c>.
        ///
        /// This is the transform anything drawn *on* the map needs — unit symbols, order
        /// arrows, control measures — and it lives here so those all agree rather than each
        /// re-deriving it. The chain is cell -> texture pixel (via the viewport the sheet was
        /// rendered with) -> texture uv -> the visible sub-rectangle after cropping -> local
        /// rect position.
        ///
        /// Returns false when the cell is outside the visible crop, so callers can hide
        /// rather than draw off the edge of the card.
        /// </summary>
        public bool CellToLocal(Vector2 cell, out Vector2 local)
        {
            local = Vector2.zero;
            if (_texture == null) return false;

            var view = Viewport;
            if (view.Width < 1 || view.Height < 1) return false;

            Vector2 px = view.CellToPixel(cell.x, cell.y);
            var uv = new Vector2(px.x / view.Width, px.y / view.Height);

            // The RawImage shows only the uvRect sub-rectangle; map into it.
            Rect shown = _image.uvRect;
            if (shown.width <= 0f || shown.height <= 0f) return false;

            var t = new Vector2((uv.x - shown.x) / shown.width,
                                (uv.y - shown.y) / shown.height);

            Rect r = _image.rectTransform.rect;
            local = new Vector2(r.xMin + t.x * r.width, r.yMin + t.y * r.height);

            // A small margin so a symbol straddling the edge is not popped out abruptly.
            const float slack = 0.15f;
            return t.x >= -slack && t.x <= 1f + slack &&
                   t.y >= -slack && t.y <= 1f + slack;
        }

        /// <summary>
        /// Re-crops if the card changed shape. Call from the owning view's Update; cheap
        /// enough to poll, and far cheaper than regenerating.
        /// </summary>
        public void PollResize()
        {
            if (_texture == null) return;
            Rect card = _image.rectTransform.rect;
            if (Mathf.Abs(card.width  - _lastCardSize.x) > 0.5f ||
                Mathf.Abs(card.height - _lastCardSize.y) > 0.5f)
                UpdateCrop();
        }

        /// <summary>Releases the sheet texture. Call from the owning view's OnDestroy.</summary>
        public void Dispose()
        {
            if (_image != null) _image.texture = Texture2D.whiteTexture;
            if (_texture != null)
            {
                UnityEngine.Object.Destroy(_texture);
                _texture = null;
            }
        }
    }
}
