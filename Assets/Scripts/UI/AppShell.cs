// AppShell.cs
// The application root: one Canvas, one EventSystem, a tab bar, and a host for the views.
//
// Before this existed, SymbolBuilderPanel created its own Canvas at sortingOrder 100 and
// was installed by its own [RuntimeInitializeOnLoadMethod] gated on the scene's name. Two
// such panels would have meant two stacked canvases and two EventSystems' worth of
// raycasting, so canvas ownership moves here and the views own only their content.
//
// Layout uses anchored rects rather than layout groups for the chrome. A horizontal group
// holding one fixed-width child and one flexible one is exactly the configuration where
// childForceExpandWidth inflates the fixed child past the screen edge, and the chrome is
// simple enough not to need a group at all.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Strategos.Demo;
using Strategos.NatoSymbols;
using Strategos.UI.Views;
using Strategos.Units;

namespace Strategos.UI
{
    public sealed class AppShell : MonoBehaviour
    {
        /// <summary>Height of the top chrome bar, in reference-resolution px.</summary>
        private const float TopBarHeight = 44f;

        /// <summary>
        /// Layer the 3D map drape renders on. The drape's own camera is masked to it and
        /// the scene's main camera is masked out of it, so a terrain mesh that only ever
        /// appears inside a card is not also drawn behind the whole UI.
        /// </summary>
        public const int MapDrapeLayer = 8;

        private ViewHost _views;
        private AppSession _session;
        private RectTransform _insignia;
        private Image _insigniaImage;

        /// <summary>Shared map / symbol state. Views read and write this, not each other.</summary>
        public AppSession Session => _session;

        // ─── Bootstrap ────────────────────────────────────────────────────────

        /// <summary>
        /// Installs the shell if the scene does not already contain one.
        ///
        /// Deliberately NOT gated on the scene's name. The old gate existed only because
        /// the committed demo scene had no builder object in it, and it would now mean a
        /// build launched from any other scene showed nothing at all.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;
            if (FindAnyObjectByType<AppShell>() != null) return;

            new GameObject("AppShell").AddComponent<AppShell>();
        }

        private void Start()
        {
            UiFactory.EnsureEventSystem();
            MaskDrapeLayerFromSceneCameras();

            _session = new AppSession();

            BuildChrome(out var tabStrip, out var contentHost);
            _views = new ViewHost(contentHost, tabStrip);

            _views.Add<PlayView>(v => ((PlayView)v).Session = _session);
            _views.Add<ExplorerView>(v => ((ExplorerView)v).Session = _session);
            _views.Add<ScenarioSetupView>(v => ((ScenarioSetupView)v).Session = _session);
            _views.Add<TtpView>(v => ((TtpView)v).Session = _session);
            _views.Add<SymbolBuilderPanel>();

            // Canvas sizes have to be settled before a view measures its own rects.
            Canvas.ForceUpdateCanvases();

            var requested = RequestedViewKey();
            if (requested != null && _views.Has(requested)) _views.Select(requested);
            else _views.SelectFirst();

            // Worth a line in Player.log: a UI exception truncates a layout silently, so
            // "the shell came up and selected a view" is otherwise unobservable from
            // outside — and a batch build that shipped stale assemblies looks identical to
            // one that shipped nothing at all.
            Debug.Log($"[AppShell] {_views.Count} view(s), showing '{_views.Current?.Key}' " +
                      $"at {Screen.width}x{Screen.height}, F11 toggles full screen");
        }

        // ─── Window ───────────────────────────────────────────────────────────

        /// <summary>Windowed size to come back to, remembered before going full screen.</summary>
        private Vector2Int _windowed;

        /// <summary>
        /// F11 toggles full screen, as it does in every application with a window.
        /// </summary>
        /// <remarks>
        /// Lives here rather than in a view because it is a property of the *application*, and
        /// a view that owned it would stop working the moment the player was on a different
        /// tab. PlayView's Space is the counterpart — that one is a game control and belongs
        /// to the game.
        ///
        /// Borderless (<see cref="FullScreenMode.FullScreenWindow"/>) rather than exclusive:
        /// it alt-tabs cleanly, changes no display mode, and needs no resolution list. The
        /// CanvasScaler is already ScaleWithScreenSize against a 1920x1080 reference, so the
        /// UI simply scales — and `MapSheetCard` re-crops on resize rather than stretching, so
        /// a different aspect ratio shows more or less ground rather than a squashed sheet.
        ///
        /// The windowed size is remembered rather than recomputed. Restoring to
        /// `Screen.currentResolution` would come back full-screen-sized in a window, which
        /// looks like the toggle failed.
        /// </remarks>
        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.F11)) return;

            if (Screen.fullScreen)
            {
                var size = _windowed.x > 0 ? _windowed : new Vector2Int(1600, 900);
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
            }
            else
            {
                _windowed = new Vector2Int(Screen.width, Screen.height);
                var display = Screen.currentResolution;
                Screen.SetResolution(display.width, display.height,
                    FullScreenMode.FullScreenWindow);
            }
        }

        // ─── Chrome ───────────────────────────────────────────────────────────

        private void BuildChrome(out Transform tabStrip, out RectTransform contentHost)
        {
            var canvasGo = new GameObject("AppCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var root = UiFactory.CreateRect("Root", canvasGo.transform);
            UiFactory.Stretch(root);
            root.gameObject.AddComponent<Image>().color = UiTheme.StageBg;

            // --- Top bar ---
            var bar = UiFactory.CreateRect("TopBar", root);
            bar.anchorMin = new Vector2(0, 1);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(0, TopBarHeight);
            bar.anchoredPosition = Vector2.zero;
            bar.gameObject.AddComponent<Image>().color = UiTheme.Accent;

            var brand = UiFactory.CreateTmp("Brand", bar, "STRATEGOS", 15,
                FontStyles.Bold, withLayout: false);
            brand.rectTransform.anchorMin = new Vector2(0, 0);
            brand.rectTransform.anchorMax = new Vector2(0, 1);
            brand.rectTransform.pivot = new Vector2(0, 0.5f);
            brand.rectTransform.sizeDelta = new Vector2(140, 0);
            brand.rectTransform.anchoredPosition = new Vector2(18, 0);
            brand.alignment = TextAlignmentOptions.MidlineLeft;
            brand.color = UiTheme.AccentText;
            brand.characterSpacing = 8f;

            // Shoulder board for the echelon the player commands (#38). Hidden until PLAY
            // publishes a command context. Derived from ORBAT + Side.RankLadder — never a
            // free-floating rank setting.
            _insignia = UiFactory.CreateRect("RankInsignia", bar);
            _insignia.anchorMin = new Vector2(0, 0.5f);
            _insignia.anchorMax = new Vector2(0, 0.5f);
            _insignia.pivot = new Vector2(0, 0.5f);
            _insignia.sizeDelta = new Vector2(RankInsignia.Width * 0.7f, RankInsignia.Height * 0.7f);
            _insignia.anchoredPosition = new Vector2(158, 0);
            _insigniaImage = _insignia.gameObject.AddComponent<Image>();
            _insigniaImage.preserveAspect = true;
            _insigniaImage.raycastTarget = false;
            _insignia.gameObject.SetActive(false);

            _session.CommandContextChanged += RefreshInsignia;
            RefreshInsignia();

            // Tab strip, right-aligned. childForceExpandWidth stays off: the tabs are
            // fixed width and must not absorb the bar's surplus.
            var strip = UiFactory.CreateRect("TabStrip", bar);
            strip.anchorMin = new Vector2(1, 0);
            strip.anchorMax = new Vector2(1, 1);
            strip.pivot = new Vector2(1, 0.5f);
            strip.offsetMin = new Vector2(0, 7);
            strip.offsetMax = new Vector2(-10, -7);
            var stripH = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
            stripH.spacing = 4;
            stripH.childControlWidth = true;
            stripH.childControlHeight = true;
            stripH.childForceExpandWidth = false;
            stripH.childForceExpandHeight = true;
            stripH.childAlignment = TextAnchor.MiddleRight;
            var stripFit = strip.gameObject.AddComponent<ContentSizeFitter>();
            stripFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            // --- Content host ---
            contentHost = UiFactory.CreateRect("ContentHost", root);
            UiFactory.Stretch(contentHost);
            contentHost.offsetMax = new Vector2(0, -TopBarHeight);

            tabStrip = strip;
        }

        /// <summary>
        /// Shoulder board for the player's command echelon — epaulette, not a text label (#38).
        /// </summary>
        private void RefreshInsignia()
        {
            if (_insignia == null || _insigniaImage == null || _session == null) return;

            var side = _session.PlayerSide;
            var echelon = _session.PlayerCommandEchelon;
            if (side == null || echelon == Echelon.None)
            {
                _insignia.gameObject.SetActive(false);
                return;
            }

            var ladder = RankLadderIO.Resolve(side.RankLadder);
            var step = ladder.For(echelon);
            _insigniaImage.sprite = RankInsignia.For(step);
            _insigniaImage.color = Color.white;
            _insignia.gameObject.name = $"RankInsignia_{step.Title}";
            _insignia.gameObject.SetActive(true);
        }

        // ─── Command line ─────────────────────────────────────────────────────

        /// <summary>
        /// Reads <c>-view &lt;key&gt;</c>. Lets a capture run screenshot any view without
        /// driving the UI, which is the difference between a change being verifiable and
        /// not.
        /// </summary>
        private static string RequestedViewKey() => CommandLineValue("-view");

        /// <summary>
        /// The requested view key, exposed so a view hosting sub-views can honour it too.
        /// A key naming a sub-view will not match at the top level, so the shell falls back
        /// to the first view, which then selects the sub-view itself.
        /// </summary>
        public static string RequestedView => CommandLineValue("-view");

        /// <summary>Value following <paramref name="flag"/> on the command line, or null.</summary>
        private static string CommandLineValue(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        /// <summary>True when the player was launched with <c>-view3d</c>.</summary>
        public static bool View3dRequested
        {
            get
            {
                foreach (var a in Environment.GetCommandLineArgs())
                    if (string.Equals(a, "-view3d", StringComparison.OrdinalIgnoreCase))
                        return true;
                return false;
            }
        }

        // ─── Layers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Keeps every camera already in the scene off the drape layer.
        ///
        /// SceneBootstrapper sets this on the camera it creates, but doing it again here
        /// means a stale or hand-edited scene cannot resurrect the problem — and the
        /// symptom would be invisible (the drape hides behind the overlay canvas) while
        /// costing a full terrain render every frame.
        /// </summary>
        private static void MaskDrapeLayerFromSceneCameras()
        {
            foreach (var cam in Camera.allCameras)
                cam.cullingMask &= ~(1 << MapDrapeLayer);
        }
    }
}
