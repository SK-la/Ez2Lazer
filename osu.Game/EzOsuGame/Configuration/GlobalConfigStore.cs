// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Configuration;

namespace osu.Game.EzOsuGame.Configuration
{
    public static class GlobalConfigStore
    {
        public static OsuConfigManager Config { get; set; } = null!;
        public static Ez2ConfigManager EzConfig { get; set; } = null!;
    }
}
