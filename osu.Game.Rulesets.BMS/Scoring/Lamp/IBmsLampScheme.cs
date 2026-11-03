// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osuTK.Graphics;

namespace osu.Game.Rulesets.BMS.Scoring.Lamp
{
    /// <summary>
    /// Plug-point for "which lamp did this play earn, and what colour is it?".
    /// Implement this once per lamp ruleset (beatoraja-compatible, LR2-style, custom…)
    /// and register it in DI. UI components only depend on this interface, so swapping
    /// the lamp scheme is a one-line change in <see cref="BMSRuleset"/>.
    /// </summary>
    public interface IBmsLampScheme
    {
        /// <summary>
        /// A short, human-readable name of the scheme (shown in settings / debug overlays).
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Resolve which lamp a single play earned, given the gameplay context.
        /// Implementations should be deterministic and side-effect free.
        /// </summary>
        BmsClearLamp ResolveLamp(BmsLampContext context);

        /// <summary>
        /// Resolve the display colour for a lamp tier. Used by song-select panels and
        /// any other "lamp at a glance" UI.
        /// </summary>
        Color4 GetLampColour(BmsClearLamp lamp);

        /// <summary>
        /// Resolve the text-foreground colour to draw on top of <see cref="GetLampColour"/>.
        /// Useful when the lamp background is very dark/light and the default content
        /// colour would be unreadable.
        /// </summary>
        Color4 GetLampTextColour(BmsClearLamp lamp);
    }
}
