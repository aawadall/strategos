// SplashView.cs
// #430 / #426: branded boot frame before MainMenuView — paper + STRATEGOS, click or
// timeout to continue. Skippable; AppShell skips splash when -view is set or in
// batchmode so probes stay deterministic.

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI.Views
{
    public sealed class SplashView : MonoBehaviour, IAppView, IPointerClickHandler
    {
        public const float DefaultHoldSeconds = 2.25f;

        public string Title => "SPLASH";
        public string Key => "splash";

        public AppShell Shell { get; set; }

        /// <summary>Hold duration before auto-advance; probes may set near-zero.</summary>
        public float HoldSeconds { get; set; } = DefaultHoldSeconds;

        private Texture2D _paperTex;
        private float _shownAt = -1f;
        private bool _dismissed;

        public void Build(RectTransform host)
        {
            var root = CreateRect("Root", host);
            Stretch(root);
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = Theme.StageBg;
            bg.raycastTarget = true;

            _paperTex = PaperTexture.Create(960, 1080, PaperTexture.SeedFor("splash"),
                PaperOptions.Clean);
            var paper = CreateRect("Paper", root);
            Stretch(paper);
            paper.offsetMin = new Vector2(120, 80);
            paper.offsetMax = new Vector2(-120, -80);
            var raw = paper.gameObject.AddComponent<RawImage>();
            raw.texture = _paperTex;
            raw.color = Color.white;
            raw.raycastTarget = false;

            var brand = CreateTmp("Brand", paper, "STRATEGOS", 56, FontStyles.Bold,
                withLayout: false);
            Stretch(brand.rectTransform);
            brand.rectTransform.offsetMin = new Vector2(40, 120);
            brand.rectTransform.offsetMax = new Vector2(-40, -80);
            brand.alignment = TextAlignmentOptions.Center;
            brand.color = Theme.Ink;
            brand.characterSpacing = 14f;
            brand.raycastTarget = false;

            var sub = CreateTmp("Sub", paper,
                "Tactical command simulation",
                16, FontStyles.Normal, withLayout: false);
            Stretch(sub.rectTransform);
            sub.rectTransform.anchorMin = new Vector2(0, 0);
            sub.rectTransform.anchorMax = new Vector2(1, 0);
            sub.rectTransform.pivot = new Vector2(0.5f, 0);
            sub.rectTransform.sizeDelta = new Vector2(0, 40);
            sub.rectTransform.anchoredPosition = new Vector2(0, 72);
            sub.alignment = TextAlignmentOptions.Center;
            sub.color = Theme.InkMuted;
            sub.raycastTarget = false;

            var hint = CreateTmp("Hint", paper,
                "Click or wait",
                12, FontStyles.Italic, withLayout: false);
            Stretch(hint.rectTransform);
            hint.rectTransform.anchorMin = new Vector2(0, 0);
            hint.rectTransform.anchorMax = new Vector2(1, 0);
            hint.rectTransform.pivot = new Vector2(0.5f, 0);
            hint.rectTransform.sizeDelta = new Vector2(0, 28);
            hint.rectTransform.anchoredPosition = new Vector2(0, 36);
            hint.alignment = TextAlignmentOptions.Center;
            hint.color = Theme.InkMuted;
            hint.raycastTarget = false;
        }

        public void OnShown()
        {
            _dismissed = false;
            _shownAt = Time.unscaledTime;
        }

        public void OnHidden()
        {
            _shownAt = -1f;
        }

        public void OnPointerClick(PointerEventData eventData) => Dismiss();

        private void Update()
        {
            if (_dismissed || _shownAt < 0f) return;
            if (Input.anyKeyDown)
            {
                Dismiss();
                return;
            }

            if (HoldSeconds <= 0f || Time.unscaledTime - _shownAt >= HoldSeconds)
                Dismiss();
        }

        /// <summary>Advance to the main menu (idempotent).</summary>
        public void Dismiss()
        {
            if (_dismissed) return;
            _dismissed = true;
            Shell?.GoToMainMenu();
        }

        private void OnDestroy()
        {
            if (_paperTex != null)
            {
                Destroy(_paperTex);
                _paperTex = null;
            }
        }
    }
}
