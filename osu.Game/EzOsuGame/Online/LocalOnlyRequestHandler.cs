// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Extensions.TypeExtensions;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.EzOsuGame.Online
{
    /// <summary>
    /// 本地账号模式的默认请求应答器。
    /// 只对 UI 依赖的请求返回空成功；其余以预期失败完成，且不走 <see cref="APIRequest.Fail"/> 的网络失败日志。
    /// </summary>
    public class LocalOnlyRequestHandler : ILocalOnlyRequestHandler
    {
        public virtual bool Handle(APIRequest request)
        {
            switch (request)
            {
                // 登录后由 LocalUserState 无条件发出，本地账号没有社交关系，返回空集合。
                case GetFriendsRequest friends:
                    friends.TriggerSuccess(new List<APIRelation>());
                    return true;

                case GetBlocksRequest blocks:
                    blocks.TriggerSuccess(new List<APIRelation>());
                    return true;

                case GetMyFavouriteBeatmapSetsRequest favourites:
                    favourites.TriggerSuccess(new GetMyFavouriteBeatmapSetsResponse());
                    return true;

                // 聊天心跳非常频繁，静默成功以免刷满日志。
                case ChatAckRequest ack:
                    ack.TriggerSuccess(new ChatAckResponse());
                    return true;

                default:
                    // 结算页标签/收藏等会在已登录时自动排队。本地账号无法访问 osu! 服务器是预期行为，
                    // 用 TriggerFailure 完成请求，避免 APIRequest.Fail 把每次打开结果屏都打成网络失败。
                    request.TriggerFailure(new LocalOnlyUnavailableException(request));
                    return true;
            }
        }
    }

    /// <summary>
    /// 请求需要访问 osu! 服务器，但当前处于本地账号模式。
    /// </summary>
    public class LocalOnlyUnavailableException : InvalidOperationException
    {
        public LocalOnlyUnavailableException(APIRequest request)
            : base($@"{request.GetType().ReadableName()} is not available while logged in with a local account.")
        {
        }
    }
}
