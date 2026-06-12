// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public class PixivAuthFile
    {
        [JsonProperty("refresh_token")]
        public string? RefreshToken { get; set; }
    }
}
