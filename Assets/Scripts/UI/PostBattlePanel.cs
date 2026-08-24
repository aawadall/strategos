// PostBattlePanel.cs
// #467: post-battle card — outcome stats + newly earned Merit ribbons.

using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Strategos.Medals;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI
{
    public sealed class PostBattlePanel : MonoBehaviour
    {
        private GameObject _root;
        private TMP_Text _title;
        private TMP_Text _stats;
        private TMP_Text _critique;
        private Transform _medalRow;
        private BarMedalCatalog _catalog;

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Build(RectTransform host)
        {
            var root = CreateRect("PostBattlePanel", host);
            Stretch(root);
            root.SetAsLastSibling();
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0.10f, 0.09f, 0.07f, 0.72f);
            dim.raycastTarget = true;

            var card = CreateRect("Card", root);
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(520, 500);
            card.gameObject.AddComponent<Image>().color = Theme.MapPaper;

            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(20, 20, 16, 16);
            v.spacing = 10;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            _title = CreateTmp("Title", card, "AFTER ACTION", 18, FontStyles.Bold);
            _title.color = Theme.Ink;
            _title.alignment = TextAlignmentOptions.Center;
            _title.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;

            _stats = CreateTmp("Stats", card, "", 13, FontStyles.Normal);
            _stats.color = Theme.Ink;
            _stats.alignment = TextAlignmentOptions.TopLeft;
            _stats.enableWordWrapping = true;
            _stats.gameObject.AddComponent<LayoutElement>().preferredHeight = 160;

            var critiqueLabel = CreateTmp("CritiqueLabel", card, "AAR", 12, FontStyles.Bold);
            critiqueLabel.color = Theme.Accent;
            critiqueLabel.characterSpacing = 4f;
            critiqueLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

            _critique = CreateTmp("Critique", card, "", 12, FontStyles.Normal);
            _critique.color = Theme.Ink;
            _critique.alignment = TextAlignmentOptions.TopLeft;
            _critique.enableWordWrapping = true;
            _critique.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;

            var medalsLabel = CreateTmp("MedalsLabel", card, "MEDALS EARNED", 12, FontStyles.Bold);
            medalsLabel.color = Theme.Accent;
            medalsLabel.characterSpacing = 4f;
            medalsLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 20;

            var rowHost = CreateRect("MedalRow", card);
            rowHost.gameObject.AddComponent<LayoutElement>().preferredHeight = 96;
            var rowH = rowHost.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowH.spacing = 10;
            rowH.childAlignment = TextAnchor.MiddleCenter;
            rowH.childControlWidth = false;
            rowH.childControlHeight = false;
            rowH.childForceExpandWidth = false;
            rowH.childForceExpandHeight = false;
            _medalRow = rowHost;

            AddButton(card, "CONTINUE", Close);

            _root = root.gameObject;
            _root.SetActive(false);
            _catalog = BarMedalIO.Load();
        }

        public void Open(PostBattleReview review)
        {
            if (_root == null || review == null) return;
            _catalog ??= BarMedalIO.Load();

            var s = review.Stats ?? new PostBattleStats();
            _title.text = string.IsNullOrEmpty(s.ScenarioName)
                ? "AFTER ACTION"
                : s.ScenarioName.ToUpperInvariant();

            var sb = new StringBuilder();
            sb.AppendLine(s.OutcomeLine);
            sb.AppendLine();
            sb.AppendLine($"TICK  T+{s.Tick:0000}");
            sb.AppendLine($"ENEMY NEUTRALIZED  {s.EnemyNeutralized}");
            sb.AppendLine($"FRIENDLY LOST  {s.FriendlyLost}");
            sb.AppendLine(
                $"OBJECTIVES HELD  {s.ObjectivesHeld} / {s.ObjectiveCount}");
            sb.AppendLine(
                $"STRENGTH LEFT  friendly {s.FriendlyRemaining:P0} · enemy {s.EnemyRemaining:P0}");
            _stats.text = sb.ToString();

            if (_critique != null)
            {
                _critique.text = review.Critiques == null || review.Critiques.Count == 0
                    ? string.Empty
                    : string.Join("\n", review.Critiques);
            }

            ClearMedals();
            if (review.Awards != null)
            {
                for (int i = 0; i < review.Awards.Count; i++)
                    AddMedalChip(review.Awards[i]);
            }

            if (_medalRow.childCount == 0)
            {
                var none = CreateTmp("None", _medalRow, "None this fight", 12, FontStyles.Italic);
                none.color = Theme.InkMuted;
                none.gameObject.AddComponent<LayoutElement>().preferredWidth = 200;
            }

            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }

        public void Close()
        {
            if (_root != null) _root.SetActive(false);
        }

        private void ClearMedals()
        {
            if (_medalRow == null) return;
            for (int i = _medalRow.childCount - 1; i >= 0; i--)
                Object.Destroy(_medalRow.GetChild(i).gameObject);
        }

        /// <summary>One chip per award; numeral / ×Count on Merit stack medals.</summary>
        private void AddMedalChip(BarMedalAward award)
        {
            if (award == null || string.IsNullOrEmpty(award.MedalId)) return;
            var def = BarMedalIO.Find(_catalog, award.MedalId);
            if (def == null) return;

            int numeral = Mathf.Max(1, award.Count);

            var chip = CreateRect($"Medal_{award.MedalId}", _medalRow);
            chip.sizeDelta = new Vector2(140, 80);
            chip.gameObject.AddComponent<LayoutElement>().preferredWidth = 140;
            chip.gameObject.AddComponent<LayoutElement>().preferredHeight = 80;

            var col = chip.gameObject.AddComponent<VerticalLayoutGroup>();
            col.spacing = 4;
            col.childAlignment = TextAnchor.UpperCenter;
            col.childControlWidth = true;
            col.childControlHeight = false;
            col.childForceExpandWidth = true;

            var icon = CreateRect("Icon", chip);
            icon.sizeDelta = new Vector2(96, 28);
            icon.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
            var img = icon.gameObject.AddComponent<Image>();
            img.sprite = BarMedalBaker.For(def, numeral);
            img.preserveAspect = true;
            img.color = Color.white;

            string label = numeral > 1 ? $"{def.Title} ×{numeral}" : def.Title;
            var caption = CreateTmp("Cap", chip, label, 11, FontStyles.Normal);
            caption.color = Theme.InkMuted;
            caption.alignment = TextAlignmentOptions.Center;
            caption.enableWordWrapping = true;
            caption.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
        }
    }
}
