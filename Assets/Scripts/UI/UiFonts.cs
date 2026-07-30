// UiFonts.cs
// The one font asset every view's TMP components use.
//
// TMP ships its runtime assets in a .unitypackage that only a human clicking
// Window -> TextMeshPro -> Import TMP Essential Resources unpacks. Without them
// TMP_Settings.instance is null, and TMP_Settings.defaultFontAsset *throws* rather
// than returning null — so it must be guarded or it takes down whatever is building
// the UI. The resources are committed to avoid this; the guard stays because a fresh
// clone that has not run the importer should still get a rendering UI.

using System;
using TMPro;
using UnityEngine;

namespace Strategos.UI
{
    public static class UiFonts
    {
        private static TMP_FontAsset _ui;

        /// <summary>
        /// Shared UI font. Cached statically, so every view pays for it once.
        /// Null only if both TMP and the OS font fallback fail, which callers
        /// already treat as "leave the component's default font alone".
        /// </summary>
        public static TMP_FontAsset Ui
        {
            get
            {
                if (_ui != null) return _ui;

                // See the file header: this property throws when TMP Essential
                // Resources have not been imported.
                if (TMP_Settings.instance != null)
                {
                    try { _ui = TMP_Settings.defaultFontAsset; }
                    catch (Exception) { _ui = null; }
                }
                if (_ui != null) return _ui;

                // Fall back to an OS font so the panel still renders.
                var osFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Segoe UI", "Arial", "Helvetica", "Tahoma", "sans-serif" }, 28);
                if (osFont != null)
                {
                    _ui = TMP_FontAsset.CreateFontAsset(osFont);
                    if (_ui != null)
                        _ui.name = "StrategosUIFont";
                }
                return _ui;
            }
        }
    }
}
