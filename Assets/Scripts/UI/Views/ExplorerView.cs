// ExplorerView.cs
// The "explore" tab: a thin host over its own sub-tabs.
//
// Symbols and maps are both things you browse rather than configure, so they share a tab
// at the top level — but each is single-purpose enough to stay its own view. This is the
// second ViewHost in the app, which is the cheapest possible proof that the switcher
// composes.

using UnityEngine;
using UnityEngine.UI;

using Theme = Strategos.UI.UiTheme;
using static Strategos.UI.UiFactory;

namespace Strategos.UI.Views
{
    public sealed class ExplorerView : MonoBehaviour, IAppView
    {
        private const float SubBarHeight = 34f;

        private ViewHost _sub;

        public string Title => "EXPLORE";
        public string Key => "explore";

        /// <summary>Injected by AppShell before Build; forwarded to the sub-views.</summary>
        public AppSession Session { get; set; }

        public void Build(RectTransform host)
        {
            var root = CreateRect("Root", host);
            Stretch(root);
            root.gameObject.AddComponent<Image>().color = Theme.StageBg;

            // Sub-tab strip, left-aligned so it reads as subordinate to the top bar's
            // right-aligned tabs rather than competing with them.
            var bar = CreateRect("SubBar", root);
            bar.anchorMin = new Vector2(0, 1);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(0, SubBarHeight);
            bar.gameObject.AddComponent<Image>().color = Theme.RailBg;

            var strip = CreateRect("Strip", bar);
            strip.anchorMin = new Vector2(0, 0);
            strip.anchorMax = new Vector2(0, 1);
            strip.pivot = new Vector2(0, 0.5f);
            strip.offsetMin = new Vector2(12, 4);
            strip.offsetMax = new Vector2(12, -4);
            var stripH = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
            stripH.spacing = 4;
            stripH.childControlWidth = true;
            stripH.childControlHeight = true;
            stripH.childForceExpandWidth = false;
            stripH.childForceExpandHeight = true;
            stripH.childAlignment = TextAnchor.MiddleLeft;
            strip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var content = CreateRect("SubContent", root);
            Stretch(content);
            content.offsetMax = new Vector2(0, -SubBarHeight);

            _sub = new ViewHost(content, strip);
            _sub.Add<SymbolLibraryView>(v => ((SymbolLibraryView)v).Session = Session);
            _sub.Add<MapExplorerView>(v => ((MapExplorerView)v).Session = Session);

            Canvas.ForceUpdateCanvases();

            // Honour -view naming one of the sub-views, so a capture run can reach the map
            // without a click. Select ignores keys it does not know, leaving Current null.
            _sub.Select(AppShell.RequestedView);
            if (_sub.Current == null) _sub.SelectFirst();
        }

        public void OnShown() => _sub?.Current?.OnShown();

        public void OnHidden()
        {
            _sub?.Current?.OnHidden();
            HideDropdownsIn(transform);
        }
    }
}
