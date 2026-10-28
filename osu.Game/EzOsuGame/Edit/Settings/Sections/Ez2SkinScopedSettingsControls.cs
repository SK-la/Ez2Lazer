// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    /// <summary>
    /// All Ez2Circle dedicated settings for Appearance → Skin-specific.
    /// </summary>
    public partial class Ez2SkinScopedSettingsControls : FillFlowContainer
    {
        public Ez2SkinScopedSettingsControls()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(8);

            Child = new EzHitTargetScopedSettingsControls();
        }
    }
}
