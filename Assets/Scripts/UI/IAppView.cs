// IAppView.cs
// What a top-level view has to provide to be hosted by AppShell.
//
// Views are MonoBehaviours rather than plain classes because most of them need
// per-frame work — a resize poll, an orbit camera, a hover readout — and being a
// component means SetActive doubles as Show/Hide *and* stops Update, while OnDestroy
// is a natural place to release textures.

using UnityEngine;

namespace Strategos.UI
{
    public interface IAppView
    {
        /// <summary>Tab caption. Upper case; the tab strip does not transform it.</summary>
        string Title { get; }

        /// <summary>
        /// Stable lowercase id. Used by the -view command-line argument, so a capture
        /// script can screenshot this view without a click.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Builds the view's UI as children of <paramref name="host"/>. Called exactly
        /// once, the first time the view is selected.
        ///
        /// This is an explicit method rather than Awake or OnEnable because AddComponent
        /// fires those before the host can hand over the rect.
        /// </summary>
        void Build(RectTransform host);

        /// <summary>
        /// The view has become visible. Re-acquire anything released in
        /// <see cref="OnHidden"/> and re-render if the shared session moved on while the
        /// view was hidden.
        /// </summary>
        void OnShown();

        /// <summary>
        /// The view is about to be hidden. Release GPU resources that are expensive to
        /// hold and cheap to rebuild — RenderTextures especially — and disable any camera
        /// with a targetTexture, because such a camera keeps rendering every frame even
        /// when nothing displays it.
        /// </summary>
        void OnHidden();
    }
}
