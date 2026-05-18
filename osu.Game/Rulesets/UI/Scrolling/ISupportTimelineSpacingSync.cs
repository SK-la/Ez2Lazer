// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.Rulesets.UI.Scrolling
{
    /// <summary>
    /// Denotes a scrolling editor ruleset that can mirror timeline zoom spacing onto the main playfield.
    /// </summary>
    public interface ISupportTimelineSpacingSync
    {
        BindableBool SyncTimelineSpacing { get; }
    }
}
