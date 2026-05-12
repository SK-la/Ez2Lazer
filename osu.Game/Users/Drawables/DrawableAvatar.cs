// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.EzOsuGame;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Users.Drawables
{
    [LongRunningLoad]
    public partial class DrawableAvatar : Sprite
    {
        private readonly IUser user;

        /// <summary>
        /// A simple, non-interactable avatar sprite for the specified user.
        /// </summary>
        /// <param name="user">The user. A null value will get a placeholder avatar.</param>
        public DrawableAvatar(IUser user = null)
        {
            this.user = user;

            RelativeSizeAxes = Axes.Both;
            FillMode = FillMode.Fit;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures, EzResourceStore ezResourceStore)
        {
            // 优先尝试加载自定义头像
            if (user != null && !string.IsNullOrEmpty(user.Username))
            {
                string customAvatarPath = $"Modify/avatars/{user.Username}";
                Texture = ezResourceStore.Get(customAvatarPath, true);
            }

            // 如果没有自定义头像，使用默认逻辑
            if (Texture == null && user != null && user.OnlineID > 1)
            {
                // TODO: The fallback here should not need to exist. Users should be looked up and populated via UserLookupCache or otherwise
                // in remaining cases where this is required (chat tabs, local leaderboard), at which point this should be removed.
                Texture = textures.Get((user as APIUser)?.AvatarUrl ?? $@"https://a.ppy.sh/{user.OnlineID}");
            }

            // 最后回退：先尝试本地 guest.png，再使用内置默认头像
            Texture ??= ezResourceStore.Get(@"Modify/avatars/guest", true) ?? textures.Get(@"Online/avatar-guest");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            this.FadeInFromZero(300, Easing.OutQuint);
        }
    }
}
