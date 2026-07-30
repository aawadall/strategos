// SceneBootstrapper.cs
// Runs once per Editor session when the project first loads.
// Automatically creates Assets/Scenes/Demo/SymbolDemo.unity if it doesn't exist,
// then opens it so symbols are visible immediately.
//
// Menu:  Strategos → Open Demo Scene      (manual trigger)
//        Strategos → Recreate Demo Scene  (force-rebuilds the scene)

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Strategos.Demo;
using Strategos.UI;

namespace Strategos.Editor
{
    [InitializeOnLoad]
    public static class SceneBootstrapper
    {
        // -------------------------------------------------------------------------
        // Constants
        // -------------------------------------------------------------------------

        private const string DemoScenePath = "Assets/Scenes/Demo/SymbolDemo.unity";
        private const string SessionKey    = "Strategos.SceneBootstrapped";

        // -------------------------------------------------------------------------
        // Static constructor — runs on every Editor domain reload
        // -------------------------------------------------------------------------

        static SceneBootstrapper()
        {
            // Defer until the Editor is fully ready.
            EditorApplication.delayCall += Bootstrap;
        }

        // -------------------------------------------------------------------------
        // Bootstrapper
        // -------------------------------------------------------------------------

        private static void Bootstrap()
        {
            // Only run once per Editor session to avoid disrupting the user's workflow.
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);

            if (!File.Exists(DemoScenePath))
            {
                Debug.Log("[SceneBootstrapper] Demo scene not found — creating it now.");
                CreateDemoScene();
            }
        }

        // -------------------------------------------------------------------------
        // Menu items
        // -------------------------------------------------------------------------

        [MenuItem("Strategos/Open Demo Scene _F5")]
        public static void OpenDemoScene()
        {
            if (!File.Exists(DemoScenePath))
                CreateDemoScene();

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(DemoScenePath);
        }

        [MenuItem("Strategos/Recreate Demo Scene")]
        public static void RecreateDemoScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                CreateDemoScene();
                EditorSceneManager.OpenScene(DemoScenePath);
            }
        }

        // -------------------------------------------------------------------------
        // Scene construction
        // -------------------------------------------------------------------------

        private static void CreateDemoScene()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Scenes/Demo");

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera ---
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam   = camGO.AddComponent<Camera>();
            cam.orthographic    = true;
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.10f); // dark op-map background
            cam.nearClipPlane   = 0.1f;
            cam.farClipPlane    = 100f;
            // The 3D map drape has its own camera rendering to a texture inside a card.
            // Without this mask the main camera draws that terrain a second time, hidden
            // behind the overlay canvas where the cost is invisible.
            cam.cullingMask    &= ~(1 << AppShell.MapDrapeLayer);
            camGO.AddComponent<AudioListener>();

            var demoCam = camGO.AddComponent<DemoCamera>();

            // --- Directional light ---
            var lightGO   = new GameObject("Directional Light");
            var light     = lightGO.AddComponent<Light>();
            light.type    = LightType.Directional;
            light.color   = Color.white;
            light.intensity = 1f;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // --- App shell (owns the canvas and hosts every view) ---
            var shellGO = new GameObject("AppShell");
            shellGO.AddComponent<AppShell>();

            // Optional reference grid — disabled by default; enable in Inspector to compare echelons.
            var spawnerGO = new GameObject("SymbolDemoSpawner");
            spawnerGO.SetActive(false);
            spawnerGO.AddComponent<SymbolDemoSpawner>();

            // Camera framed for UI (screen-space builder); world grid optional.
            camGO.transform.position = new Vector3(0f, 0f, -20f);
            cam.orthographicSize = 8f;
            demoCam.SetDefaults(Vector3.zero, 8f);

            // --- Save scene ---
            EditorSceneManager.SaveScene(scene, DemoScenePath);
            AddSceneToBuildSettings(DemoScenePath);

            Debug.Log($"[SceneBootstrapper] Demo scene created → {DemoScenePath}");
        }

        // -------------------------------------------------------------------------
        // Public utility — callable from GameBuild in batch mode
        // -------------------------------------------------------------------------

        /// <summary>
        /// Creates the demo scene if it doesn't exist and registers it in
        /// EditorBuildSettings. Safe to call from the build pipeline in batch mode;
        /// does NOT open the scene (no EditorSceneManager.OpenScene call).
        /// </summary>
        /// <summary>
        /// Rebuilds the demo scene unconditionally. Batch-safe: unlike the menu item it
        /// does not call SaveCurrentModifiedScenesIfUserWantsTo, which would block on a
        /// dialog with no one to answer it.
        /// </summary>
        public static void RecreateSceneBatch()
        {
            CreateDemoScene();
            Debug.Log($"[SceneBootstrapper] Scene recreated in batch mode -> {DemoScenePath}");
        }

        public static void EnsureSceneRegistered()
        {
            if (!File.Exists(DemoScenePath))
            {
                Debug.Log("[SceneBootstrapper] Creating demo scene for build pipeline.");
                CreateDemoScene();
            }
            else
            {
                // Scene exists — just make sure it is in build settings.
                AddSceneToBuildSettings(DemoScenePath);
            }
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts  = path.Split('/');
                var parent = string.Join("/", parts, 0, parts.Length - 1);
                AssetDatabase.CreateFolder(parent, parts[parts.Length - 1]);
            }
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var existing = EditorBuildSettings.scenes;
            foreach (var s in existing)
                if (s.path == path) return;

            var updated = new EditorBuildSettingsScene[existing.Length + 1];
            existing.CopyTo(updated, 0);
            updated[existing.Length] = new EditorBuildSettingsScene(path, true);
            EditorBuildSettings.scenes = updated;
        }
    }
}
#endif
