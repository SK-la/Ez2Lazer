// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Optional accent-colour source for beatmap panels in song select.
    /// </summary>
    /// <remarks>
    /// Rulesets that want the left-hand accent strip on <see cref="PanelBeatmap"/> /
    /// <see cref="PanelBeatmapStandalone"/> to reflect something other than star rating
    /// (BMS clear lamps, etc.) can <c>[Cached(typeof(IPanelAccentColourProvider))]</c>
    /// an implementation on a screen (or parent) that owns song select.
    /// <para>
    /// When nothing registers a provider, panels must resolve this type with
    /// <c>[Resolved(canBeNull: true)]</c> so dependency injection yields <see langword="null"/>
    /// instead of throwing; callers then fall back to the default star-rating colour.
    /// Gameplay and rulesets do not depend on this hook — it is display-only.
    /// </para>
    /// <para>
    /// Implementations may return <see langword="null"/> from <see cref="GetAccentColourFor"/>
    /// for a given beatmap to defer to the same star-rating fallback for that row only.
    /// </para>
    /// </remarks>
    public interface IPanelAccentColourProvider
    {
        /// <summary>
        /// Resolve the accent colour to use for <paramref name="beatmap"/>'s panel,
        /// or <see langword="null"/> to fall back to the default star-rating colour.
        /// </summary>
        Color4? GetAccentColourFor(BeatmapInfo beatmap);
    }
}
