// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Online.API;

namespace osu.Game.EzOsuGame.Online
{
    /// <summary>
    /// 在 <see cref="APIState.LocalOnline"/> 下就地应答 API 请求，使本地账号不必联网也能满足 UI 的数据依赖。
    /// </summary>
    public interface ILocalOnlyRequestHandler
    {
        /// <summary>
        /// 尝试在本地应答请求。
        /// </summary>
        /// <returns>
        /// <see langword="true"/> 表示请求已被处理（无论成功或失败）；
        /// <see langword="false"/> 表示无法在本地应答，调用方应让请求失败。
        /// </returns>
        bool Handle(APIRequest request);
    }
}
