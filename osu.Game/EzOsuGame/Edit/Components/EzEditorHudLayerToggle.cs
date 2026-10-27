// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.UserInterface;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Shared HUD-layer visibility toggle for editor scene bars.
    /// </summary>
    public partial class EzEditorHudLayerToggle : EzToggleButton
    {
        public EzEditorHudLayerToggle()
        {
            Anchor = Anchor.CentreLeft;
            Origin = Anchor.CentreLeft;
            AutoSizeAxes = Axes.X;
            Text = EzEditorStrings.TOOLBAR_HUD_LAYER;
            TextSize = 14;
            Active.Value = true;
        }
    }
}
