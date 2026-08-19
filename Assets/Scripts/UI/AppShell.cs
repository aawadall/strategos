// AppShell.cs
// The application root: one Canvas, one EventSystem, a tab bar, and a host for the views.
//
// #371: boots into MainMenuView (front door). PLAY is a session entered from the menu;
// EXPLORE / SCENARIO / DRILLS / BUILDER remain Tools. Pause overlay lives inside PlayView.
// #306: SettingsView is a no-tab screen reached from menu Options.
// #387: F11 fullscreen/windowed goes through ToggleFullscreen / ApplyWindowed / ApplyFullscreen.
// #391: Start loads PlayerPreferences and applies fullscreen / windowed size.
// #218: top-bar version label from Application.version (stamped at build — #217).

using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Strategos.Audio;
using Strategos.Demo;
using Strategos.NatoSymbols;
using Strategos.Persistence.Files;
using Strategos.Preferences;
using Strategos.Steam;
using Strategos.UI.Views;
using Strategos.Units;

namespace Strategos.UI
{
    public sealed class AppShell : MonoBehaviour
    {
        private const float TopBarHeight = 44f;
        public const int MapDrapeLayer = 8;

        private ViewHost _views;
        private AppSession _session;
        private RectTransform _insignia;
        private Image _insigniaImage;
        private GameObject _tabStripGo;

        public AppSession Session => _session;

        /// <summary>#218 — identifiable in screenshots; leading <c>v</c> plus bundleVersion.</summary>
        public static string VersionLabel
        {
            get
            {
                string v = Application.version;
                if (string.IsNullOrEmpty(v)) return "v?";
                return v[0] == 'v' || v[0] == 'V' ? v : "v" + v;
            }
        }

        /// <summary>Injected for probes; defaults to <see cref="JsonPreferenceStore"/>.</summary>
        public IPreferenceStore PreferenceStore { get; set; }

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
            // #302: SteamAPI.Init seam — NullSteamClient no-ops without App ID / package.
            SteamClientHost.Bootstrap();

            PreferenceStore ??= new JsonPreferenceStore();
            var prefs = PreferenceStore.Load();
            ApplyDisplayPreferences(prefs);

            var audio = AudioService.Ensure(gameObject);
            audio?.ApplyPreferences(prefs);

            UiFactory.EnsureEventSystem();
            MaskDrapeLayerFromSceneCameras();
            _session = new AppSession();

            BuildChrome(out var tabStrip, out var contentHost);
            _tabStripGo = tabStrip.gameObject;
            _views = new ViewHost(contentHost, tabStrip);

            _views.Add<SplashView>(v =>
            {
                var s = (SplashView)v;
                s.Shell = this;
                // Capture helper: hold indefinitely so the boot frame can be
                // photographed instead of racing capture.ps1's screenshot timer.
                if (FreezeSplashRequested) s.HoldSeconds = float.PositiveInfinity;
            }, showTab: false);
            _views.Add<MainMenuView>(v =>
            {
                var m = (MainMenuView)v;
                m.Session = _session;
                m.Shell = this;
            }, showTab: false);
            _views.Add<SettingsView>(v =>
            {
                ((SettingsView)v).Shell = this;
            }, showTab: false);
            _views.Add<PlayView>(v =>
            {
                var p = (PlayView)v;
                p.Session = _session;
                p.Shell = this;
            });
            _views.Add<ExplorerView>(v => ((ExplorerView)v).Session = _session);
            _views.Add<ScenarioSetupView>(v => ((ScenarioSetupView)v).Session = _session);
            _views.Add<TtpView>(v => ((TtpView)v).Session = _session);
            _views.Add<SymbolBuilderPanel>();

            if (tabStrip != null)
                UiFactory.AddTabButton(tabStrip, "MENU", GoToMainMenu);

            Canvas.ForceUpdateCanvases();

            var requested = RequestedViewKey();
            if (requested != null && _views.Has(requested)) Navigate(requested);
            else if (ShouldShowSplash()) Navigate("splash");
            else Navigate("menu");

            Debug.Log($"[AppShell] {_views.Count} view(s), showing '{_views.Current?.Key}' " +
                      $"at {Screen.width}x{Screen.height}, F11 toggles full screen");
        }

        /// <summary>
        /// #430: splash on a normal boot. Skip when <c>-view</c> is set, or under
        /// batchmode / nographics so probes and captures stay deterministic.
        /// </summary>
        public static bool ShouldShowSplash()
        {
            if (RequestedView != null) return false;
            if (Application.isBatchMode) return false;
            foreach (var a in Environment.GetCommandLineArgs())
            {
                if (string.Equals(a, "-nographics", StringComparison.OrdinalIgnoreCase))
                    return false;
                if (string.Equals(a, "-batchmode", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        public void Navigate(string key)
        {
            if (string.IsNullOrEmpty(key) || !_views.Has(key)) return;
            // Menu and settings are outside the Tools tab strip (#371 / #306).
            bool tools = !IsChromeHiddenView(key);
            if (_tabStripGo != null) _tabStripGo.SetActive(tools);
            _views.Select(key);
            UpdateSoundtrack(key);
        }

        /// <summary>
        /// Menu / tools / settings share the menu loop (#253); PLAY gets the ambient bed (#254).
        /// </summary>
        private static void UpdateSoundtrack(string key)
        {
            var audio = AudioService.Instance;
            if (audio == null) return;
            if (string.Equals(key, "play", StringComparison.OrdinalIgnoreCase))
                audio.PlayMusicLoop(AudioService.PlayAmbientResource);
            else
                audio.PlayMusicLoop(AudioService.MenuLoopResource);
        }

        private static bool IsChromeHiddenView(string key) =>
            string.Equals(key, "splash", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "menu", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "settings", StringComparison.OrdinalIgnoreCase);

        public void GoToMainMenu() => Navigate("menu");
        public void OpenSettings() => Navigate("settings");
        public void EnterPlaySession() => Navigate("play");
        public void EnterTools(string key) => Navigate(key);

        public void StartValleyFromMenu()
        {
            Navigate("play");
            _views.Get<PlayView>()?.StartValleyCampaignPublic();
        }

        public void StartHighlandFromMenu()
        {
            Navigate("play");
            _views.Get<PlayView>()?.StartHighlandCampaignPublic();
        }

        public void StartClimbFromMenu()
        {
            Navigate("play");
            _views.Get<PlayView>()?.StartClimbCampaignPublic();
        }

        public void LoadScenarioFromMenu(string name)
        {
            Navigate("play");
            _views.Get<PlayView>()?.LoadScenarioPublic(name);
        }

        public void QuickSaveFromMenu() => _views.Get<PlayView>()?.QuickSavePublic();

        public void QuickLoadFromMenu()
        {
            Navigate("play");
            _views.Get<PlayView>()?.QuickLoadPublic();
        }

        /// <summary>
        /// Last windowed size remembered across F11 toggles and seeded from prefs on boot (#391).
        /// Empty until prefs or an explicit ApplyWindowed; then falls back to 1600×900.
        /// </summary>
        private Vector2Int _windowed;

        /// <summary>True when the player is in borderless fullscreen (not exclusive).</summary>
        public bool IsFullscreen => Screen.fullScreen;

        /// <summary>Windowed size F11 / Settings will restore to (default 1600×900).</summary>
        public Vector2Int RememberedWindowedSize =>
            _windowed.x > 0 ? _windowed : new Vector2Int(1600, 900);

        /// <summary>
        /// Load prefs (or use <paramref name="prefs"/>) and apply fullscreen / windowed size.
        /// Seeds <see cref="RememberedWindowedSize"/> so F11 restore survives restart (#391).
        /// </summary>
        public void ApplyDisplayPreferences(PlayerPreferences prefs = null)
        {
            PreferenceStore ??= new JsonPreferenceStore();
            prefs ??= PreferenceStore.Load();
            int w = prefs.WindowWidth > 0 ? prefs.WindowWidth : 1600;
            int h = prefs.WindowHeight > 0 ? prefs.WindowHeight : 900;
            _windowed = new Vector2Int(w, h);

            if (prefs.Fullscreen)
            {
                var display = Screen.currentResolution;
                Screen.SetResolution(display.width, display.height, FullScreenMode.FullScreenWindow);
            }
            else
                Screen.SetResolution(w, h, FullScreenMode.Windowed);
        }

        /// <summary>
        /// F11 and Settings share this path (#387) — borderless fullscreen matches the display;
        /// windowed uses the remembered size (or an explicit preset from #390).
        /// </summary>
        public void ToggleFullscreen()
        {
            if (IsFullscreen) ApplyWindowed();
            else ApplyFullscreen();
        }

        /// <summary>
        /// Enter windowed mode. Optional <paramref name="width"/>/<paramref name="height"/>
        /// become the remembered size when both are positive.
        /// </summary>
        public void ApplyWindowed(int width = 0, int height = 0)
        {
            if (width > 0 && height > 0)
                _windowed = new Vector2Int(width, height);
            var size = RememberedWindowedSize;
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
        }

        /// <summary>
        /// Enter borderless fullscreen at the current display resolution. Remembers the
        /// outgoing windowed size when leaving windowed mode.
        /// </summary>
        public void ApplyFullscreen()
        {
            if (!Screen.fullScreen)
                _windowed = new Vector2Int(Screen.width, Screen.height);
            var display = Screen.currentResolution;
            Screen.SetResolution(display.width, display.height, FullScreenMode.FullScreenWindow);
        }

        private void OnApplicationQuit()
        {
            SteamClientHost.Shutdown();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F11))
                ToggleFullscreen();
        }

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

            // #218: version sits after the brand so a capture names the build.
            var version = UiFactory.CreateTmp("Version", bar, VersionLabel, 12,
                FontStyles.Normal, withLayout: false);
            version.rectTransform.anchorMin = new Vector2(0, 0);
            version.rectTransform.anchorMax = new Vector2(0, 1);
            version.rectTransform.pivot = new Vector2(0, 0.5f);
            version.rectTransform.sizeDelta = new Vector2(160, 0);
            version.rectTransform.anchoredPosition = new Vector2(150, 0);
            version.alignment = TextAlignmentOptions.MidlineLeft;
            version.color = new Color(UiTheme.AccentText.r, UiTheme.AccentText.g,
                UiTheme.AccentText.b, 0.72f);
            version.raycastTarget = false;

            _insignia = UiFactory.CreateRect("RankInsignia", bar);
            _insignia.anchorMin = new Vector2(0, 0.5f);
            _insignia.anchorMax = new Vector2(0, 0.5f);
            _insignia.pivot = new Vector2(0, 0.5f);
            _insignia.sizeDelta = new Vector2(RankInsignia.Width * 0.7f, RankInsignia.Height * 0.7f);
            _insignia.anchoredPosition = new Vector2(320, 0);
            _insigniaImage = _insignia.gameObject.AddComponent<Image>();
            _insigniaImage.preserveAspect = true;
            _insigniaImage.raycastTarget = false;
            _insignia.gameObject.SetActive(false);

            _session.CommandContextChanged += RefreshInsignia;
            RefreshInsignia();

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

            contentHost = UiFactory.CreateRect("ContentHost", root);
            UiFactory.Stretch(contentHost);
            contentHost.offsetMax = new Vector2(0, -TopBarHeight);
            tabStrip = strip;
        }

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

        private static string RequestedViewKey() => CommandLineValue("-view");
        public static string RequestedView => CommandLineValue("-view");

        private static string CommandLineValue(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

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

        /// <summary>
        /// Capture helper: open a canned post-battle panel on PLAY (#467 Pages still).
        /// </summary>
        public static bool DemoPostBattleRequested
        {
            get
            {
                foreach (var a in Environment.GetCommandLineArgs())
                    if (string.Equals(a, "-demo-postbattle", StringComparison.OrdinalIgnoreCase))
                        return true;
                return false;
            }
        }

        /// <summary>
        /// Capture helper: open pause on a canned career profile (#519 Pages still).
        /// </summary>
        public static bool DemoCareerRequested
        {
            get
            {
                foreach (var a in Environment.GetCommandLineArgs())
                    if (string.Equals(a, "-demo-career", StringComparison.OrdinalIgnoreCase))
                        return true;
                return false;
            }
        }

        /// <summary>
        /// Capture helper: hold the splash boot frame indefinitely instead of
        /// auto-advancing, so it can be screenshotted (#482 Pages still).
        /// </summary>
        public static bool FreezeSplashRequested
        {
            get
            {
                foreach (var a in Environment.GetCommandLineArgs())
                    if (string.Equals(a, "-freeze-splash", StringComparison.OrdinalIgnoreCase))
                        return true;
                return false;
            }
        }

        private static void MaskDrapeLayerFromSceneCameras()
        {
            foreach (var cam in Camera.allCameras)
                cam.cullingMask &= ~(1 << MapDrapeLayer);
        }
    }
}
