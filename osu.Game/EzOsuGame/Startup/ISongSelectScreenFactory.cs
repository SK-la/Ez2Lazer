// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Screens.Select;

namespace osu.Game.EzOsuGame.Startup
{
    public interface ISongSelectScreenFactory
    {
        SoloSongSelect Create();
    }
}
