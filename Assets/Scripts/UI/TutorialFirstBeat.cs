// TutorialFirstBeat.cs
// #310: first tutorial beat — select your squad, then MoveTo on the real command path.
// Non-blocking banner (map clicks still work). Driven by PlayView Select / IssueMoveTo.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI
{
    public enum TutorialBeatPhase
    {
        Inactive = 0,
        SelectUnit = 1,
        IssueMove = 2,
        Complete = 3,
    }

    /// <summary>Checklist for the squad tutorial's first select → MOVE beat (#310).</summary>
    public sealed class TutorialFirstBeat : MonoBehaviour
    {
        private GameObject _root;
        private TMP_Text _title;
        private TMP_Text _body;

        public TutorialBeatPhase Phase { get; private set; } = TutorialBeatPhase.Inactive;

        public bool IsActive =>
            Phase == TutorialBeatPhase.SelectUnit || Phase == TutorialBeatPhase.IssueMove;

        public void Build(RectTransform host)
        {
            var root = CreateRect("TutorialFirstBeat", host);
            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(560, 96);
            root.anchoredPosition = new Vector2(0, -12);
            root.SetAsLastSibling();

            var face = root.gameObject.AddComponent<Image>();
            face.color = Theme.CardBg;
            face.raycastTarget = false;

            var v = root.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(16, 16, 10, 10);
            v.spacing = 4;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            _title = CreateTmp("Title", root, "TUTORIAL", 14, FontStyles.Bold);
            _title.alignment = TextAlignmentOptions.Center;
            _title.color = Theme.Ink;
            _title.raycastTarget = false;
            _title.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

            _body = CreateTmp("Body", root, "", 13, FontStyles.Normal);
            _body.alignment = TextAlignmentOptions.Center;
            _body.color = Theme.InkMuted;
            _body.enableWordWrapping = true;
            _body.raycastTarget = false;
            _body.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;

            _root = root.gameObject;
            _root.SetActive(false);
        }

        /// <summary>Starts the select → MoveTo checklist (tutorial scenario load).</summary>
        public void Begin()
        {
            Phase = TutorialBeatPhase.SelectUnit;
            Show("1 / 2  ·  SELECT",
                "Left-click your squad on the map (or the ORBAT). Real selection — same as any scenario.");
        }

        public void Reset()
        {
            Phase = TutorialBeatPhase.Inactive;
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>
        /// Advances when the player selects a commandable friendly unit during SelectUnit.
        /// </summary>
        public void OnUnitSelected(bool playerCommanded)
        {
            if (Phase != TutorialBeatPhase.SelectUnit || !playerCommanded) return;
            Phase = TutorialBeatPhase.IssueMove;
            Show("2 / 2  ·  MOVE",
                "Arm MOVE (M or the MOVE button), then left-click a destination. Issues a real MoveTo order.");
        }

        /// <summary>Completes when PlayView issues MoveTo through the normal command path.</summary>
        public void OnMoveIssued()
        {
            if (Phase != TutorialBeatPhase.IssueMove) return;
            Phase = TutorialBeatPhase.Complete;
            Show("BEAT COMPLETE",
                "Select and MoveTo used the live command path. More beats land with later #289 work.");
        }

        private void Show(string title, string body)
        {
            if (_title != null) _title.text = title;
            if (_body != null) _body.text = body;
            if (_root != null)
            {
                _root.SetActive(true);
                _root.transform.SetAsLastSibling();
            }
        }
    }
}
