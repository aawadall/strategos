// TmpResources.cs
// TextMesh Pro ships its runtime assets ("TMP Settings", the default font asset
// and shaders) inside a .unitypackage that is only imported when a human clicks
// Window > TextMeshPro > Import TMP Essential Resources.
//
// Without them TMP_Settings.instance is null, TMP_Settings.defaultFontAsset
// throws, and every TextMeshProUGUI fails in Awake — so a batch-mode build
// produces a player with no text at all.
//
// This imports them headlessly so local and CI builds behave the same.

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Strategos.Editor
{
    public static class TmpResources
    {
        private const string SettingsAsset = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        // TMP moved into the ugui package in Unity 6; keep the standalone path as
        // a fallback for older package layouts.
        private static readonly string[] PackagePaths =
        {
            "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage",
            "Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage",
        };

        public static bool IsInstalled => File.Exists(SettingsAsset);

        [MenuItem("Strategos/Import TMP Essential Resources")]
        public static void Import()
        {
            if (IsInstalled)
            {
                Debug.Log("[TmpResources] TMP essential resources already present.");
                return;
            }

            foreach (var path in PackagePaths)
            {
                if (!File.Exists(path)) continue;

                Debug.Log($"[TmpResources] Importing {path}");
                AssetDatabase.ImportPackage(path, interactive: false);
                AssetDatabase.Refresh();
                return;
            }

            Debug.LogError(
                "[TmpResources] Could not find 'TMP Essential Resources.unitypackage'. " +
                "Searched: " + string.Join(", ", PackagePaths));
        }

        /// <summary>
        /// Imports the resources when missing. Safe to call from the build
        /// pipeline; no-op once the assets are committed.
        /// </summary>
        public static void EnsureImported()
        {
            if (!IsInstalled) Import();
        }

        /// <summary>
        /// Batch-mode entry point. AssetDatabase.ImportPackage is asynchronous, so
        /// this must NOT be run with -quit: the editor would exit before the import
        /// runs. Exits the editor from the completion callback instead.
        ///
        /// Unity.exe -batchmode -nographics -projectPath . \
        ///           -executeMethod Strategos.Editor.TmpResources.ImportBatch
        /// </summary>
        public static void ImportBatch()
        {
            if (IsInstalled)
            {
                Debug.Log("[TmpResources] Already present; nothing to do.");
                EditorApplication.Exit(0);
                return;
            }

            AssetDatabase.importPackageCompleted += _ =>
            {
                AssetDatabase.Refresh();
                Debug.Log("[TmpResources] Import complete.");
                EditorApplication.Exit(IsInstalled ? 0 : 1);
            };

            AssetDatabase.importPackageFailed += (_, error) =>
            {
                Debug.LogError($"[TmpResources] Import failed: {error}");
                EditorApplication.Exit(1);
            };

            Import();
        }
    }
}
#endif
