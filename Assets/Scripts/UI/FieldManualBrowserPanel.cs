// FieldManualBrowserPanel.cs
// #206 / #207 / #124: in-session read-only glossary browser (pause nested).
// Loads alpha-glossary via GlossaryIO; shows DrillRefs on the detail pane.

using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Strategos.FieldManual;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI
{
    public sealed class FieldManualBrowserPanel : MonoBehaviour
    {
        private GameObject _root;
        private RectTransform _indexContent;
        private TMP_Text _detailTitle;
        private TMP_Text _detailBody;
        private readonly List<Button> _termButtons = new();
        private GlossaryTerm[] _terms = System.Array.Empty<GlossaryTerm>();
        private int _selected = -1;

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Build(RectTransform host)
        {
            var root = CreateRect("FieldManualBrowser", host);
            Stretch(root);
            root.SetAsLastSibling();
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0.10f, 0.09f, 0.07f, 0.65f);

            var card = CreateRect("Card", root);
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(720, 520);
            card.gameObject.AddComponent<Image>().color = Theme.MapPaper;

            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(20, 20, 16, 16);
            v.spacing = 8;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            var title = CreateTmp("Title", card, "FIELD MANUAL", 18, FontStyles.Bold);
            title.color = Theme.Ink;
            title.alignment = TextAlignmentOptions.Center;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;

            var note = CreateTmp("Note", card,
                "Read-only · alpha glossary · related drills listed on each term · full binder is DRILLS",
                12, FontStyles.Normal);
            note.color = Theme.InkMuted;
            note.alignment = TextAlignmentOptions.Center;
            note.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

            var split = CreateRect("Split", card);
            split.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            split.gameObject.AddComponent<LayoutElement>().preferredHeight = 360;
            var h = split.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;

            var indexHost = CreateRect("Index", split);
            indexHost.gameObject.AddComponent<LayoutElement>().preferredWidth = 200;
            indexHost.gameObject.AddComponent<LayoutElement>().flexibleWidth = 0f;
            indexHost.gameObject.AddComponent<Image>().color = Theme.RailBg;
            var indexScroll = indexHost.gameObject.AddComponent<ScrollRect>();
            indexScroll.horizontal = false;
            indexScroll.vertical = true;
            indexScroll.movementType = ScrollRect.MovementType.Clamped;

            var indexViewport = CreateRect("Viewport", indexHost);
            Stretch(indexViewport);
            indexViewport.gameObject.AddComponent<RectMask2D>();
            indexScroll.viewport = indexViewport;

            _indexContent = CreateRect("Content", indexViewport);
            _indexContent.anchorMin = new Vector2(0, 1);
            _indexContent.anchorMax = new Vector2(1, 1);
            _indexContent.pivot = new Vector2(0.5f, 1);
            _indexContent.sizeDelta = new Vector2(0, 0);
            var indexV = _indexContent.gameObject.AddComponent<VerticalLayoutGroup>();
            indexV.padding = new RectOffset(6, 6, 6, 6);
            indexV.spacing = 4;
            indexV.childControlWidth = true;
            indexV.childControlHeight = true;
            indexV.childForceExpandWidth = true;
            indexV.childForceExpandHeight = false;
            _indexContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            indexScroll.content = _indexContent;

            var detail = CreateRect("Detail", split);
            detail.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            detail.gameObject.AddComponent<Image>().color = Theme.CardBg;
            var detailV = detail.gameObject.AddComponent<VerticalLayoutGroup>();
            detailV.padding = new RectOffset(14, 14, 12, 12);
            detailV.spacing = 8;
            detailV.childControlWidth = true;
            detailV.childControlHeight = true;
            detailV.childForceExpandWidth = true;
            detailV.childForceExpandHeight = false;

            _detailTitle = CreateTmp("DetailTitle", detail, "", 16, FontStyles.Bold);
            _detailTitle.color = Theme.Ink;
            _detailTitle.alignment = TextAlignmentOptions.TopLeft;
            _detailTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            var detailScrollHost = CreateRect("DetailScroll", detail);
            detailScrollHost.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            detailScrollHost.gameObject.AddComponent<LayoutElement>().preferredHeight = 280;
            var detailScroll = detailScrollHost.gameObject.AddComponent<ScrollRect>();
            detailScroll.horizontal = false;
            detailScroll.vertical = true;
            detailScroll.movementType = ScrollRect.MovementType.Clamped;

            var detailViewport = CreateRect("Viewport", detailScrollHost);
            Stretch(detailViewport);
            detailViewport.gameObject.AddComponent<RectMask2D>();
            detailScroll.viewport = detailViewport;

            var detailContent = CreateRect("Content", detailViewport);
            detailContent.anchorMin = new Vector2(0, 1);
            detailContent.anchorMax = new Vector2(1, 1);
            detailContent.pivot = new Vector2(0.5f, 1);
            detailContent.sizeDelta = new Vector2(0, 0);
            var detailContentV = detailContent.gameObject.AddComponent<VerticalLayoutGroup>();
            detailContentV.childControlWidth = true;
            detailContentV.childControlHeight = true;
            detailContentV.childForceExpandWidth = true;
            detailContentV.childForceExpandHeight = false;
            detailContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            detailScroll.content = detailContent;

            _detailBody = CreateTmp("DetailBody", detailContent, "", 13, FontStyles.Normal);
            _detailBody.color = Theme.Ink;
            _detailBody.alignment = TextAlignmentOptions.TopLeft;
            _detailBody.enableWordWrapping = true;
            _detailBody.gameObject.AddComponent<LayoutElement>().preferredHeight = 400;

            AddButton(card, "CLOSE", Close);

            _root = root.gameObject;
            _root.SetActive(false);
        }

        public void Open()
        {
            if (_root == null) return;
            Refresh();
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }

        /// <summary>Open and select the first term that cites <paramref name="drillCode"/> (#207).</summary>
        public void OpenOnDrill(string drillCode)
        {
            Open();
            if (_terms == null || string.IsNullOrWhiteSpace(drillCode)) return;
            for (int i = 0; i < _terms.Length; i++)
            {
                var refs = _terms[i].DrillRefs;
                if (refs == null) continue;
                foreach (var r in refs)
                {
                    if (string.Equals(r, drillCode, System.StringComparison.OrdinalIgnoreCase))
                    {
                        Select(i);
                        return;
                    }
                }
            }
        }

        public void Close()
        {
            if (_root != null) _root.SetActive(false);
        }

        private void Refresh()
        {
            ClearIndex();
            var pack = GlossaryIO.Load(GlossaryIO.DefaultPackName);
            if (pack?.Terms == null || pack.Terms.Length == 0)
            {
                _terms = System.Array.Empty<GlossaryTerm>();
                _detailTitle.text = "(no glossary)";
                _detailBody.text = "Could not load Resources/FieldManual/" +
                    GlossaryIO.DefaultPackName + ".";
                return;
            }

            _terms = pack.Terms;
            for (int i = 0; i < _terms.Length; i++)
            {
                int idx = i;
                var term = _terms[i];
                var label = string.IsNullOrEmpty(term.Title) ? term.Id : term.Title;
                var row = CreateRect("Term_" + term.Id, _indexContent);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
                var img = row.gameObject.AddComponent<Image>();
                img.color = Theme.CardBg;
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => Select(idx));
                var labelTmp = CreateTmp("Label", row, label, 13, FontStyles.Normal);
                Stretch(labelTmp.rectTransform);
                labelTmp.color = Theme.Ink;
                labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
                labelTmp.margin = new Vector4(8, 0, 8, 0);
                labelTmp.raycastTarget = false;
                _termButtons.Add(btn);
            }

            Select(0);
        }

        private void Select(int index)
        {
            if (_terms == null || index < 0 || index >= _terms.Length) return;
            _selected = index;
            var term = _terms[index];
            _detailTitle.text = string.IsNullOrEmpty(term.Title) ? term.Id : term.Title;

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(term.Body))
                sb.Append(term.Body);
            if (term.DrillRefs != null && term.DrillRefs.Length > 0)
            {
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append("Related drills: ");
                for (int i = 0; i < term.DrillRefs.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(term.DrillRefs[i]);
                }
                sb.Append("\n(Open the DRILLS tab binder for the full page.)");
            }
            _detailBody.text = sb.ToString();

            for (int i = 0; i < _termButtons.Count; i++)
            {
                if (_termButtons[i] == null || _termButtons[i].targetGraphic == null) continue;
                _termButtons[i].targetGraphic.color =
                    i == _selected ? Theme.SelectFill : Theme.CardBg;
            }
        }

        private void ClearIndex()
        {
            _termButtons.Clear();
            _selected = -1;
            if (_indexContent == null) return;
            for (int i = _indexContent.childCount - 1; i >= 0; i--)
                Destroy(_indexContent.GetChild(i).gameObject);
        }
    }
}
