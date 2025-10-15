// filepath: osu.Game/Screens/Play/IGameplayBackgroundSource.cs
// Provides a proxy-able composite of gameplay background-related visual content (background + storyboard layers if desired)
// to allow rulesets (e.g. Mania) to apply localised effects (blur, dim, masking) without re-implementing loading logic
// or performing full-screen framebuffer captures.

using osu.Framework.Graphics;

namespace osu.Game.Rulesets.Mania.UI
{
    public interface IGameplayBackgroundSource
    {
        /// <summary>
        /// Creates a proxy drawable containing the background-related composite (eg. storyboard, background imagery).
        /// Returned drawable is expected to be RelativeSizeAxes = Both (or otherwise scalable by consumer) and safe to proxy repeatedly.
        /// </summary>
        Drawable CreateCompositeProxy();

        /// <summary>
        /// Creates a proxy drawable containing only the background (without storyboard layers).
        /// Returned drawable is expected to be RelativeSizeAxes = Both (or otherwise scalable by consumer) and safe to proxy repeatedly.
        /// </summary>
        Drawable? CreateBackgroundOnlyProxy();

        /// <summary>
        /// Creates a proxy drawable containing the background and video (if available).
        /// Returned drawable is expected to be RelativeSizeAxes = Both (or otherwise scalable by consumer) and safe to proxy repeatedly.
        /// </summary>
        Drawable? CreateBackgroundWithVideoProxy();

        /// <summary>
        /// Creates a standalone drawable containing the background.
        /// Returned drawable is expected to be RelativeSizeAxes = Both (or otherwise scalable by consumer) and safe to proxy repeatedly.
        /// </summary>
        Drawable CreateStandaloneBackground();
    }
}
