// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Online.API;

namespace osu.Game.EzOsuGame.Online
{
    public static class APIStateExtensions
    {
        /// <summary>
        /// 会话可用：用户已登录且可以进入需要身份的功能，包含本地 / 局域网账号。
        /// 与 <c>== <see cref="APIState.Online"/></c> 的区别在于后者要求能真正访问 osu! 服务器。
        /// </summary>
        public static bool IsSessionActive(this APIState state) => state == APIState.Online || state == APIState.LocalOnline;
    }
}
