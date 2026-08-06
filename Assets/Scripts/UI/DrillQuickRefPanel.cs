// DrillQuickRefPanel.cs
// #371: in-session drills quick-reference (interpretation a — lookup, not quests).
// Read-only list from TtpLibrary; execute stays on PLAY's drill rail.

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Strategos.Doctrine;
using Strategos.FieldManual;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI
{
    public sealed class DrillQuickRefPanel : MonoBehaviour
    {
        private GameObject _root;
        private TMP_Text _body;

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Build(RectTransform host)
        {
            var root = CreateRect("DrillQuickRef", host);
            Stretch(root);
            root.SetAsLastSibling();
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0.10f, 0.09f, 0.07f, 0.65f);

            var card = CreateRect("Card", root);
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(640, 520);
            card.gameObject.AddComponent<Image>().color = Theme.MapPaper;

            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(20, 20, 16, 16);
            v.spacing = 8;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            var title = CreateTmp("Title", card, "DRILLS — QUICK REFERENCE", 18, FontStyles.Bold);
            title.color = Theme.Ink;
            title.alignment = TextAlignmentOptions.Center;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;

            var note = CreateTmp("Note", card,
                "Lookup only · issue drills from the PLAY rail · full binder is DRILLS tab",
                12, FontStyles.Normal);
            note.color = Theme.InkMuted;
            note.alignment = TextAlignmentOptions.Center;
            note.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

            var scrollHost = CreateRect("Scroll", card);
            scrollHost.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            scrollHost.gameObject.AddComponent<LayoutElement>().preferredHeight = 360;
            var scroll = scrollHost.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = CreateRect("Viewport", scrollHost);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            var content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, 0);
            var contentV = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentV.spacing = 6;
            contentV.childControlWidth = true;
            contentV.childControlHeight = true;
            contentV.childForceExpandWidth = true;
            contentV.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            _body = CreateTmp("Body", content, "", 13, FontStyles.Normal);
            _body.color = Theme.Ink;
            _body.alignment = TextAlignmentOptions.TopLeft;
            _body.enableWordWrapping = true;
            _body.gameObject.AddComponent<LayoutElement>().preferredHeight = 800;

            AddButton(card, "CLOSE", Close);

            _root = root.gameObject;
            _root.SetActive(false);
        }

        public void Open()
        {
            if (_root == null) return;
            RefreshBody();
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }

        public void Close()
        {
            if (_root != null) _root.SetActive(false);
        }

        private void RefreshBody()
        {
            if (_body == null) return;
            var drills = TtpLibrary.All;
            if (drills == null || drills.Count == 0)
            {
                _body.text = "(no doctrine pack loaded)";
                return;
            }

            var sb = new System.Text.StringBuilder();
            var glossary = GlossaryIO.Load(GlossaryIO.DefaultPackName);
            for (int i = 0; i < drills.Count; i++)
            {
                var d = drills[i];
                sb.Append(d.Code).Append("  —  ").Append(d.Name);
                if (!string.IsNullOrEmpty(d.Summary))
                    sb.Append('\n').Append("    ").Append(d.Summary);
                var linked = GlossaryIO.TermsForDrill(glossary, d.Code);
                if (linked != null && linked.Length > 0)
                {
                    sb.Append('\n').Append("    Field manual: ");
                    for (int t = 0; t < linked.Length; t++)
                    {
                        if (t > 0) sb.Append(", ");
                        sb.Append(string.IsNullOrEmpty(linked[t].Title)
                            ? linked[t].Id
                            : linked[t].Title);
                    }
                }
                sb.Append("\n\n");
            }
            _body.text = sb.ToString();
        }
    }
}
