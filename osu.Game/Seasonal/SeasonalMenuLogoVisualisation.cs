// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Screens.Menu;

namespace osu.Game.Seasonal
{
    internal partial class SeasonalMenuLogoVisualisation : EzMenuLogoVisualisation
    {
        protected override void UpdateColour() => Colour = SeasonalUIConfig.AMBIENT_COLOUR_1;
    }
}
