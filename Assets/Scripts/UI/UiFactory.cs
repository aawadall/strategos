// UiFactory.cs
// The shared widget kit: every control the views build, in one place.
//
// Extracted from SymbolBuilderPanel, which built all of these privately. Views pull
// them in with `using static Strategos.UI.UiFactory;` so call sites read the same as
// they did when the methods were members.
//
// Two conventions worth knowing before adding a control here:
//
//   * Unity UI has no border on Image, so a control is an outer edge-coloured rect
//     with an inset face rect. AddBorderedFace returns the face for tinting.
//   * Geometric-shape glyphs are NOT in the bundled font atlas. Anything that would
//     be a triangle, circle, bullet or checkmark must be an Image, not a character —
//     see UiSprites and the AddToggle mark below.
//
// Every control takes an optional `Action onChanged` rather than wiring a fixed
// callback, because these used to be instance methods that closed over one view's
// RefreshPreview and could not be shared.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Strategos.UI
{
    public static class UiFactory
    {
        // ─── Scaffolding ──────────────────────────────────────────────────────

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Unity UI has no border on Image, so a control is an outer edge-coloured
        /// rect with an inset face rect. Returns the face image for tinting.
        /// </summary>
        public static Image AddBorderedFace(RectTransform outer, float inset = 2f)
        {
            outer.gameObject.AddComponent<Image>().color = UiTheme.ControlEdge;
            var face = CreateRect("Face", outer);
            Stretch(face);
            face.offsetMin = new Vector2(inset, inset);
            face.offsetMax = new Vector2(-inset, -inset);
            var img = face.gameObject.AddComponent<Image>();
            img.color = Color.white; // tinted via ColorBlock
            return img;
        }

        public static ColorBlock ControlColors()
        {
            var c = ColorBlock.defaultColorBlock;
            c.normalColor = UiTheme.ControlFace;
            c.highlightedColor = UiTheme.ControlHover;
            c.pressedColor = UiTheme.SelectFill;
            c.selectedColor = UiTheme.ControlFace;
            c.disabledColor = UiTheme.RailEdge;
            c.fadeDuration = 0.08f;
            return c;
        }

        // ─── Text ─────────────────────────────────────────────────────────────

        public static TMP_Text CreateTmp(string name, Transform parent, string text, float size,
            FontStyles style, bool withLayout = true)
        {
            var rt = CreateRect(name, parent);
            if (withLayout)
            {
                var le = rt.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = size + 8;
            }
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (UiFonts.Ui != null) tmp.font = UiFonts.Ui;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = UiTheme.Ink;
            tmp.raycastTarget = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        public static TMP_Text CreateOverlayTmp(string name, Transform parent, string text,
            float size, Color color)
        {
            // For dropdown captions / items — no LayoutElement (breaks TMP_Dropdown).
            var tmp = CreateTmp(name, parent, text, size, FontStyles.Bold, withLayout: false);
            tmp.color = color;
            return tmp;
        }

        public static void AddSection(Transform parent, string title)
        {
            var bar = CreateRect($"Sec_{title}", parent);
            bar.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
            bar.gameObject.AddComponent<Image>().color = UiTheme.SectionBg;

            var accent = CreateRect("Accent", bar);
            accent.anchorMin = new Vector2(0, 0);
            accent.anchorMax = new Vector2(0, 1);
            accent.pivot = new Vector2(0, 0.5f);
            accent.sizeDelta = new Vector2(4, 0);
            accent.gameObject.AddComponent<Image>().color = UiTheme.Accent;

            var tmp = CreateTmp("T", bar, title, 12, FontStyles.Bold, withLayout: false);
            Stretch(tmp.rectTransform);
            tmp.rectTransform.offsetMin = new Vector2(12, 0);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = UiTheme.Ink;
            tmp.characterSpacing = 3f;
        }

        // ─── Controls ─────────────────────────────────────────────────────────

        public static TMP_Dropdown AddDropdown(Transform parent, string label, Action onChanged = null)
        {
            var wrap = CreateRect($"DD_{label}", parent);
            var wrapV = wrap.gameObject.AddComponent<VerticalLayoutGroup>();
            wrapV.spacing = 3;
            wrapV.childControlWidth = true;
            wrapV.childControlHeight = true;
            wrapV.childForceExpandWidth = true;
            wrap.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;

            var lbl = CreateTmp("L", wrap, label, 11, FontStyles.Bold);
            lbl.color = UiTheme.InkMuted;
            lbl.characterSpacing = 2f;
            lbl.GetComponent<LayoutElement>().preferredHeight = 15;

            var dropRt = CreateRect("Dropdown", wrap);
            dropRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
            var faceImg = AddBorderedFace(dropRt);

            var drop = dropRt.gameObject.AddComponent<TMP_Dropdown>();
            drop.targetGraphic = faceImg;
            drop.colors = ControlColors();

            var caption = CreateOverlayTmp("Caption", dropRt, "Select", 14, UiTheme.Ink);
            Stretch(caption.rectTransform);
            caption.rectTransform.offsetMin = new Vector2(10, 2);
            caption.rectTransform.offsetMax = new Vector2(-30, -2);
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            caption.overflowMode = TextOverflowModes.Ellipsis;
            caption.raycastTarget = false;
            drop.captionText = caption;

            // Drawn rather than typed: the geometric-shape glyphs (▾ U+25BE) are not
            // in the LiberationSans atlas TMP ships with and render as tofu.
            var arrow = CreateRect("Arrow", dropRt);
            arrow.anchorMin = new Vector2(1, 0.5f);
            arrow.anchorMax = new Vector2(1, 0.5f);
            arrow.pivot = new Vector2(1, 0.5f);
            arrow.sizeDelta = new Vector2(11, 7);
            arrow.anchoredPosition = new Vector2(-11, 0);
            var arrowImg = arrow.gameObject.AddComponent<Image>();
            arrowImg.sprite = UiSprites.Arrow;
            arrowImg.color = UiTheme.Accent;
            arrowImg.raycastTarget = false;

            BuildDropdownTemplate(drop, dropRt);

            drop.onValueChanged.AddListener(_ => onChanged?.Invoke());
            return drop;
        }

        private static void BuildDropdownTemplate(TMP_Dropdown drop, RectTransform dropRt)
        {
            var template = CreateRect("Template", dropRt);
            template.gameObject.SetActive(false);
            template.anchorMin = new Vector2(0, 0);
            template.anchorMax = new Vector2(1, 0);
            template.pivot = new Vector2(0.5f, 1);
            template.anchoredPosition = new Vector2(0, 2);
            template.sizeDelta = new Vector2(0, 240);
            template.gameObject.AddComponent<Image>().color = UiTheme.ControlEdge;

            var templateScroll = template.gameObject.AddComponent<ScrollRect>();
            templateScroll.horizontal = false;
            templateScroll.movementType = ScrollRect.MovementType.Clamped;
            templateScroll.scrollSensitivity = 24f;

            var tViewport = CreateRect("Viewport", template);
            Stretch(tViewport);
            tViewport.offsetMin = new Vector2(2, 2);
            tViewport.offsetMax = new Vector2(-2, -2);
            tViewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            tViewport.gameObject.AddComponent<Image>().color = Color.white;
            templateScroll.viewport = tViewport;

            var tContent = CreateRect("Content", tViewport);
            tContent.anchorMin = new Vector2(0, 1);
            tContent.anchorMax = new Vector2(1, 1);
            tContent.pivot = new Vector2(0.5f, 1);
            tContent.sizeDelta = new Vector2(0, 34);
            templateScroll.content = tContent;

            var item = CreateRect("Item", tContent);
            item.anchorMin = new Vector2(0, 0.5f);
            item.anchorMax = new Vector2(1, 0.5f);
            item.sizeDelta = new Vector2(0, 34);
            var itemToggle = item.gameObject.AddComponent<Toggle>();
            var itemBg = item.gameObject.AddComponent<Image>();
            itemBg.color = Color.white; // tinted via ColorBlock
            itemToggle.targetGraphic = itemBg;

            var itemColors = ColorBlock.defaultColorBlock;
            itemColors.normalColor = UiTheme.ControlFace;
            itemColors.highlightedColor = UiTheme.ControlHover;
            itemColors.pressedColor = UiTheme.SelectFill;
            itemColors.selectedColor = UiTheme.ControlHover;
            itemColors.fadeDuration = 0.05f;
            itemToggle.colors = itemColors;

            // Selection marker: an opaque accent bar down the left edge. A full-bleed
            // translucent overlay would wash out the item text instead.
            var itemCheck = CreateRect("Item Checkmark", item);
            itemCheck.anchorMin = new Vector2(0, 0);
            itemCheck.anchorMax = new Vector2(0, 1);
            itemCheck.pivot = new Vector2(0, 0.5f);
            itemCheck.sizeDelta = new Vector2(5, 0);
            var checkImg = itemCheck.gameObject.AddComponent<Image>();
            checkImg.color = UiTheme.Accent;
            itemToggle.graphic = checkImg;

            var itemLabel = CreateOverlayTmp("Item Label", item, "Option", 13, UiTheme.Ink);
            Stretch(itemLabel.rectTransform);
            itemLabel.rectTransform.offsetMin = new Vector2(14, 0);
            itemLabel.rectTransform.offsetMax = new Vector2(-8, 0);
            itemLabel.alignment = TextAlignmentOptions.MidlineLeft;
            itemLabel.textWrappingMode = TextWrappingModes.NoWrap;
            itemLabel.overflowMode = TextOverflowModes.Ellipsis;
            itemLabel.raycastTarget = false;

            drop.template = template;
            drop.itemText = itemLabel;
        }

        /// <summary>
        /// A labelled slider with a live value readout. <paramref name="format"/> and
        /// <paramref name="suffix"/> exist because not every slider is a percentage —
        /// the relief parameters are metres, degrees and counts.
        /// </summary>
        public static (Slider slider, TMP_Text value) AddSlider(Transform parent, string label,
            float min, float max, float value, Action onChanged = null,
            string format = "0", string suffix = "%", bool wholeNumbers = true)
        {
            var wrap = CreateRect($"SL_{label}", parent);
            var wrapV = wrap.gameObject.AddComponent<VerticalLayoutGroup>();
            wrapV.spacing = 3;
            wrapV.childControlWidth = true;
            wrapV.childControlHeight = true;
            wrapV.childForceExpandWidth = true;
            wrap.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;

            var header = CreateRect("H", wrap);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 15;
            var headerH = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerH.childControlWidth = true;
            headerH.childForceExpandWidth = true;

            var lbl = CreateTmp("L", header, label, 11, FontStyles.Bold);
            lbl.color = UiTheme.InkMuted;
            lbl.characterSpacing = 2f;
            var val = CreateTmp("V", header, value.ToString(format) + suffix, 12, FontStyles.Bold);
            val.alignment = TextAlignmentOptions.MidlineRight;
            val.color = UiTheme.Accent;

            var sliderRt = CreateRect("Slider", wrap);
            sliderRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            AddBorderedFace(sliderRt);

            var fillArea = CreateRect("Fill Area", sliderRt);
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(6, 9);
            fillArea.offsetMax = new Vector2(-6, -9);
            var fill = CreateRect("Fill", fillArea);
            Stretch(fill);
            fill.gameObject.AddComponent<Image>().color = UiTheme.Accent;

            var handleArea = CreateRect("Handle Slide Area", sliderRt);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(8, 0);
            handleArea.offsetMax = new Vector2(-8, 0);
            var handle = CreateRect("Handle", handleArea);
            handle.sizeDelta = new Vector2(16, 20);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = Color.white;

            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            var sliderColors = ColorBlock.defaultColorBlock;
            sliderColors.normalColor = UiTheme.Ink;
            sliderColors.highlightedColor = UiTheme.Accent;
            sliderColors.pressedColor = UiTheme.Accent;
            slider.colors = sliderColors;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.value = value;

            // The readout is the kit's own business, so a caller never has to remember to
            // update it alongside its own handler. It lives on a component rather than in
            // this closure so that SetSliderValue below can refresh it too — a value set
            // through SetValueWithoutNotify does not fire this listener, and a label left
            // showing the old number is worse than no label.
            var readout = sliderRt.gameObject.AddComponent<UiSliderReadout>();
            readout.Init(slider, val, format, suffix);

            slider.onValueChanged.AddListener(_ =>
            {
                readout.Refresh();
                onChanged?.Invoke();
            });
            return (slider, val);
        }

        /// <summary>
        /// Sets a slider's value without firing its callback, keeping the numeric label in
        /// step. Use this for seeding controls; a bare SetValueWithoutNotify leaves the label
        /// showing the previous value.
        /// </summary>
        public static void SetSliderValue(Slider slider, float value)
        {
            if (slider == null) return;
            var readout = slider.GetComponent<UiSliderReadout>();
            if (readout != null) readout.SetValueSilently(value);
            else slider.SetValueWithoutNotify(value);
        }

        public static TMP_InputField AddInput(Transform parent, string label, string defaultText,
            Action onChanged = null)
        {
            var wrap = CreateRect($"IN_{label}", parent);
            var wrapV = wrap.gameObject.AddComponent<VerticalLayoutGroup>();
            wrapV.spacing = 3;
            wrapV.childControlWidth = true;
            wrapV.childControlHeight = true;
            wrapV.childForceExpandWidth = true;
            wrap.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;

            var lbl = CreateTmp("L", wrap, label, 11, FontStyles.Bold);
            lbl.color = UiTheme.InkMuted;
            lbl.characterSpacing = 2f;
            lbl.GetComponent<LayoutElement>().preferredHeight = 15;

            var fieldRt = CreateRect("Field", wrap);
            fieldRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
            var faceImg = AddBorderedFace(fieldRt);

            var textArea = CreateRect("Text Area", fieldRt);
            Stretch(textArea);
            textArea.offsetMin = new Vector2(10, 4);
            textArea.offsetMax = new Vector2(-10, -4);
            textArea.gameObject.AddComponent<RectMask2D>();

            var text = CreateTmp("Text", textArea, defaultText, 14, FontStyles.Bold, withLayout: false);
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = UiTheme.Ink;

            var placeholder = CreateTmp("Placeholder", textArea, label, 14, FontStyles.Italic, withLayout: false);
            Stretch(placeholder.rectTransform);
            placeholder.color = UiTheme.InkMuted;
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;

            var input = fieldRt.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = textArea;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.targetGraphic = faceImg;
            input.colors = ControlColors();
            input.caretColor = UiTheme.Accent;
            input.customCaretColor = true;
            input.selectionColor = new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.3f);
            input.text = defaultText;
            input.onEndEdit.AddListener(_ => onChanged?.Invoke());
            input.onSubmit.AddListener(_ => onChanged?.Invoke());
            return input;
        }

        public static Button AddButton(Transform parent, string label, Action onClick)
        {
            var rt = CreateRect($"BTN_{label}", parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 44;
            le.minHeight = 44;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Color.white;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.colors = AccentButtonColors(UiTheme.Accent);
            btn.onClick.AddListener(() => onClick?.Invoke());

            var tmp = CreateTmp("T", rt, label, 14, FontStyles.Bold, withLayout: false);
            Stretch(tmp.rectTransform);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = UiTheme.AccentText;
            tmp.characterSpacing = 4f;
            tmp.raycastTarget = false;
            return btn;
        }

        /// <summary>
        /// Button colour states derived from one authored face colour. Exposed so a
        /// caller can restate them after recolouring a button (the scenario view
        /// lightens GENERATE while settings are dirty).
        /// </summary>
        public static ColorBlock AccentButtonColors(Color face)
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = face;
            colors.highlightedColor = UiTheme.Lighten(face, 0.14f);
            colors.pressedColor = UiTheme.Lighten(face, -0.10f);
            colors.selectedColor = face;
            return colors;
        }

        /// <summary>
        /// Labelled checkbox. The mark is an inset Image, not a '✓' — that glyph is
        /// not in the bundled atlas and would render as a tofu box.
        /// </summary>
        public static Toggle AddToggle(Transform parent, string label, bool on, Action onChanged = null)
        {
            var wrap = CreateRect($"TG_{label}", parent);
            wrap.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;

            var boxRt = CreateRect("Box", wrap);
            boxRt.anchorMin = new Vector2(0, 0.5f);
            boxRt.anchorMax = new Vector2(0, 0.5f);
            boxRt.pivot = new Vector2(0, 0.5f);
            boxRt.sizeDelta = new Vector2(22, 22);
            boxRt.anchoredPosition = new Vector2(0, 0);
            var faceImg = AddBorderedFace(boxRt);

            var mark = CreateRect("Mark", boxRt);
            Stretch(mark);
            mark.offsetMin = new Vector2(6, 6);
            mark.offsetMax = new Vector2(-6, -6);
            var markImg = mark.gameObject.AddComponent<Image>();
            markImg.color = UiTheme.Accent;
            markImg.raycastTarget = false;

            var lbl = CreateTmp("L", wrap, label, 12, FontStyles.Bold, withLayout: false);
            Stretch(lbl.rectTransform);
            lbl.rectTransform.offsetMin = new Vector2(32, 0);
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            lbl.color = UiTheme.InkMuted;
            lbl.characterSpacing = 1.5f;

            var toggle = wrap.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = faceImg;
            toggle.graphic = markImg;
            toggle.colors = ControlColors();
            toggle.SetIsOnWithoutNotify(on);
            toggle.onValueChanged.AddListener(_ => onChanged?.Invoke());
            return toggle;
        }

        /// <summary>
        /// A tab in a view-switch strip. Returns the face and text so the owner can
        /// restyle the selected tab; see <see cref="StyleTab"/>.
        /// </summary>
        public static (Button btn, Image face, TMP_Text text) AddTabButton(Transform parent,
            string label, Action onClick)
        {
            var rt = CreateRect($"TAB_{label}", parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 132;
            le.flexibleWidth = 0;
            le.preferredHeight = 30;

            var face = rt.gameObject.AddComponent<Image>();
            face.color = UiTheme.SectionBg;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = face;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.86f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.72f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.06f;
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var tmp = CreateTmp("T", rt, label, 12, FontStyles.Bold, withLayout: false);
            Stretch(tmp.rectTransform);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = UiTheme.InkMuted;
            tmp.characterSpacing = 3f;
            tmp.raycastTarget = false;

            // Selected-state underline, hidden until StyleTab turns it on.
            var rule = CreateRect("Rule", rt);
            rule.anchorMin = new Vector2(0, 0);
            rule.anchorMax = new Vector2(1, 0);
            rule.pivot = new Vector2(0.5f, 0);
            rule.sizeDelta = new Vector2(0, 3);
            var ruleImg = rule.gameObject.AddComponent<Image>();
            ruleImg.color = UiTheme.Accent;
            ruleImg.raycastTarget = false;
            rule.gameObject.SetActive(false);

            return (btn, face, tmp);
        }

        /// <summary>Applies selected / unselected styling to a tab built by <see cref="AddTabButton"/>.</summary>
        public static void StyleTab(Image face, TMP_Text text, bool selected)
        {
            face.color = selected ? UiTheme.ControlFace : UiTheme.SectionBg;
            text.color = selected ? UiTheme.Accent : UiTheme.InkMuted;
            var rule = face.transform.Find("Rule");
            if (rule != null) rule.gameObject.SetActive(selected);
        }

        // ─── Dropdown population / reading ────────────────────────────────────

        public static void SetDrop(TMP_Dropdown drop, string[] options, int index)
        {
            if (drop == null) return;
            drop.ClearOptions();
            drop.AddOptions(new List<string>(options));
            drop.SetValueWithoutNotify(Mathf.Clamp(index, 0, options.Length - 1));
            drop.RefreshShownValue();
            if (drop.captionText != null)
            {
                drop.captionText.color = UiTheme.Ink;
                if (UiFonts.Ui != null) drop.captionText.font = UiFonts.Ui;
            }
            if (drop.itemText != null)
            {
                drop.itemText.color = UiTheme.Ink;
                if (UiFonts.Ui != null) drop.itemText.font = UiFonts.Ui;
            }
        }

        public static T Pick<T>(T[] table, TMP_Dropdown drop, T fallback)
        {
            if (table == null || drop == null || table.Length == 0) return fallback;
            return table[Mathf.Clamp(drop.value, 0, table.Length - 1)];
        }

        public static int PickCode((string label, int code)[] table, TMP_Dropdown drop, int fallback)
        {
            if (table == null || drop == null || table.Length == 0) return fallback;
            return table[Mathf.Clamp(drop.value, 0, table.Length - 1)].code;
        }

        /// <summary>
        /// Closes every dropdown under <paramref name="root"/>. A view must call this
        /// when it is hidden: a TMP_Dropdown left open re-appears open — and floating
        /// over the wrong view — the next time the view is shown.
        /// </summary>
        public static void HideDropdownsIn(Transform root)
        {
            if (root == null) return;
            foreach (var d in root.GetComponentsInChildren<TMP_Dropdown>(true))
                d.Hide();
        }
    }
}
