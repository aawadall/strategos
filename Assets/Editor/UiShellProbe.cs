// UiShellProbe.cs
// #371 / #306: MainMenuView / SettingsView / PauseOverlay; view keys stable for -view.
// Batch: -executeMethod Strategos.Editor.UiShellProbe.Run

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;
using Strategos.UI;
using Strategos.UI.Views;

namespace Strategos.Editor
{
    public static class UiShellProbe
    {
        [MenuItem("Strategos/Probe UI Shell")]
        public static void Run()
        {
            var log = new StringBuilder();
            int bad = 0;

            var menuGo = new GameObject("probe-menu", typeof(RectTransform));
            var menu = menuGo.AddComponent<MainMenuView>();
            if (menu.Key != "menu" || menu.Title != "MENU")
            {
                log.AppendLine("  FAIL MainMenuView key/title");
                bad++;
            }
            else log.AppendLine("  MainMenuView key=menu ok");

            try
            {
                menu.Build(menuGo.GetComponent<RectTransform>());
                // #427 / #428 / #429: scroll + EXIT + AUDIO on the front door.
                if (!FindNamed(menuGo.transform, "Scroll"))
                {
                    log.AppendLine("  FAIL MainMenuView missing Scroll (#427)");
                    bad++;
                }
                else log.AppendLine("  MainMenuView Scroll ok");

                if (!FindNamed(menuGo.transform, "BTN_EXIT"))
                {
                    log.AppendLine("  FAIL MainMenuView missing EXIT (#428)");
                    bad++;
                }
                else log.AppendLine("  MainMenuView EXIT ok");

                if (!FindNamed(menuGo.transform, "BTN_AUDIO"))
                {
                    log.AppendLine("  FAIL MainMenuView missing AUDIO (#429)");
                    bad++;
                }
                else log.AppendLine("  MainMenuView AUDIO ok");
            }
            catch (System.Exception ex)
            {
                log.AppendLine("  FAIL MainMenuView.Build: " + ex.Message);
                bad++;
            }
            finally { Object.DestroyImmediate(menuGo); }

            var splashGo = new GameObject("probe-splash", typeof(RectTransform));
            var splash = splashGo.AddComponent<SplashView>();
            if (splash.Key != "splash" || splash.Title != "SPLASH")
            {
                log.AppendLine("  FAIL SplashView key/title");
                bad++;
            }
            else log.AppendLine("  SplashView key=splash ok");

            try
            {
                splash.Build(splashGo.GetComponent<RectTransform>());
                if (!FindNamed(splashGo.transform, "Brand"))
                {
                    log.AppendLine("  FAIL SplashView missing Brand");
                    bad++;
                }
                else log.AppendLine("  SplashView.Build Brand ok");

                // Dismiss without Shell is a no-op navigate; must not throw (#430).
                splash.HoldSeconds = 0f;
                splash.OnShown();
                splash.Dismiss();
                log.AppendLine("  SplashView Dismiss ok");
            }
            catch (System.Exception ex)
            {
                log.AppendLine("  FAIL SplashView: " + ex.Message);
                bad++;
            }
            finally { Object.DestroyImmediate(splashGo); }

            if (AppShell.ShouldShowSplash() && Application.isBatchMode)
            {
                log.AppendLine("  FAIL ShouldShowSplash true under batchmode");
                bad++;
            }
            else log.AppendLine("  ShouldShowSplash skipped in batch/probe ok");

            var settingsGo = new GameObject("probe-settings", typeof(RectTransform));
            var settings = settingsGo.AddComponent<SettingsView>();
            if (settings.Key != "settings" || settings.Title != "OPTIONS")
            {
                log.AppendLine("  FAIL SettingsView key/title");
                bad++;
            }
            else log.AppendLine("  SettingsView key=settings ok");

            string[] want = { "GRAPHICS", "AUDIO", "GAMEPLAY", "ACCESSIBILITY" };
            if (SettingsView.Categories == null || SettingsView.Categories.Length != want.Length)
            {
                log.AppendLine("  FAIL SettingsView.Categories count");
                bad++;
            }
            else
            {
                bool catsOk = true;
                for (int i = 0; i < want.Length; i++)
                {
                    if (SettingsView.Categories[i] != want[i])
                    {
                        log.AppendLine("  FAIL SettingsView.Categories[" + i + "]=" +
                                       SettingsView.Categories[i]);
                        bad++;
                        catsOk = false;
                    }
                }
                if (catsOk)
                    log.AppendLine("  SettingsView categories GRAPHICS/AUDIO/GAMEPLAY/ACCESSIBILITY ok");
            }

            try
            {
                settings.Build(settingsGo.GetComponent<RectTransform>());
                log.AppendLine("  SettingsView.Build ok");
            }
            catch (System.Exception ex)
            {
                log.AppendLine("  FAIL SettingsView.Build: " + ex.Message);
                bad++;
            }
            finally { Object.DestroyImmediate(settingsGo); }

            var play = new GameObject("probe-play").AddComponent<PlayView>();
            if (play.Key != "play") { log.AppendLine("  FAIL PlayView.Key"); bad++; }
            else log.AppendLine("  PlayView key=play ok");
            Object.DestroyImmediate(play.gameObject);

            var hostGo = new GameObject("probe-host", typeof(RectTransform));
            var host = hostGo.GetComponent<RectTransform>();
            var pause = hostGo.AddComponent<PauseOverlay>();
            try
            {
                pause.Build(host, () => { }, () => { }, () => { }, () => { });
                pause.Open();
                if (!pause.IsOpen) { log.AppendLine("  FAIL PauseOverlay.Open"); bad++; }
                pause.Close();
                if (pause.IsOpen) { log.AppendLine("  FAIL PauseOverlay.Close"); bad++; }
                log.AppendLine("  PauseOverlay Build/Open/Close ok");
            }
            catch (System.Exception ex)
            {
                log.AppendLine("  FAIL PauseOverlay: " + ex.Message);
                bad++;
            }
            finally { Object.DestroyImmediate(hostGo); }

            log.AppendLine(bad == 0 ? "PROBE PASSED" : ("PROBE FAILED with " + bad + " problem(s)"));
            if (bad == 0) Debug.Log("[UiShellProbe]\n" + log);
            else Debug.LogError("[UiShellProbe]\n" + log);
        }

        private static bool FindNamed(Transform root, string name)
        {
            if (root.name == name) return true;
            for (int i = 0; i < root.childCount; i++)
                if (FindNamed(root.GetChild(i), name)) return true;
            return false;
        }
    }
}
#endif
