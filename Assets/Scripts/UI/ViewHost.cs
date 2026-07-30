// ViewHost.cs
// Holds a set of views over one host rect and switches between them, with an optional
// tab strip.
//
// Used twice: once by AppShell for the top-level views, and once inside the explorer for
// its sub-tabs. That reuse is the point — a nested switcher costs nothing extra.
//
// Views are built lazily on first selection and then deactivated rather than destroyed.
// Lazily, because building every view up front multiplies the exposure to the failure
// mode where an exception truncates a layout silently, and pays every view's startup cost
// whichever tab you actually wanted. Not destroyed, because rebuilding costs hundreds of
// milliseconds — a map regeneration or a few hundred symbol bakes.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Strategos.UI
{
    public sealed class ViewHost
    {
        private sealed class Entry
        {
            public IAppView View;
            public GameObject Go;
            public bool Built;
            public Image TabFace;
            public TMP_Text TabText;
        }

        private readonly RectTransform _host;
        private readonly Transform _tabStrip;
        private readonly List<Entry> _entries = new();
        private Entry _current;

        /// <param name="host">Rect the views build into. Each view gets a stretched child.</param>
        /// <param name="tabStrip">
        /// Where to put tab buttons. Null for a host driven entirely in code.
        /// </param>
        public ViewHost(RectTransform host, Transform tabStrip = null)
        {
            _host = host;
            _tabStrip = tabStrip;
        }

        public IAppView Current => _current?.View;
        public int Count => _entries.Count;

        /// <summary>
        /// Registers a view. Nothing is instantiated until the view is first selected —
        /// only the tab button is created, so the strip is complete from the start.
        /// </summary>
        public void Add<T>() where T : MonoBehaviour, IAppView
        {
            var go = new GameObject(typeof(T).Name, typeof(RectTransform));
            go.transform.SetParent(_host, false);
            UiFactory.Stretch((RectTransform)go.transform);
            go.SetActive(false);

            var view = go.AddComponent<T>();
            var entry = new Entry { View = view, Go = go, Built = false };

            if (_tabStrip != null)
            {
                var (_, face, text) = UiFactory.AddTabButton(
                    _tabStrip, view.Title, () => Select(view.Key));
                entry.TabFace = face;
                entry.TabText = text;
            }

            _entries.Add(entry);
        }

        /// <summary>Selects a view by <see cref="IAppView.Key"/>. Unknown keys are ignored.</summary>
        public void Select(string key)
        {
            var target = Find(key);
            if (target == null || target == _current) return;

            if (_current != null)
            {
                _current.View.OnHidden();
                _current.Go.SetActive(false);
            }

            _current = target;

            // Activate before Build: a layout group will not compute sizes for an
            // inactive hierarchy, and several views measure their own rects while
            // building.
            target.Go.SetActive(true);
            if (!target.Built)
            {
                target.View.Build((RectTransform)target.Go.transform);
                target.Built = true;
            }
            target.View.OnShown();

            RestyleTabs();
        }

        /// <summary>Selects the first registered view. No-op if none are registered.</summary>
        public void SelectFirst()
        {
            if (_entries.Count > 0) Select(_entries[0].View.Key);
        }

        public bool Has(string key) => Find(key) != null;

        private Entry Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var e in _entries)
                if (string.Equals(e.View.Key, key, System.StringComparison.OrdinalIgnoreCase))
                    return e;
            return null;
        }

        private void RestyleTabs()
        {
            foreach (var e in _entries)
                if (e.TabFace != null)
                    UiFactory.StyleTab(e.TabFace, e.TabText, e == _current);
        }
    }
}
