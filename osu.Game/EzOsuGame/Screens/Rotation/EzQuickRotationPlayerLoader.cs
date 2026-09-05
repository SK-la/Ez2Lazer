// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    public partial class EzQuickRotationPlayerLoader : PlayerLoader
    {
        public EzQuickRotationPlayerLoader(Func<Player> createPlayer)
            : base(createPlayer)
        {
        }

        public override bool ShowFooter => !QuickRestart;
    }
}
