// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.LocalAvatar;
using osu.Game.Graphics;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Users.Drawables
{
    [LongRunningLoad]
    public partial class DrawableAvatar : CompositeDrawable
    {
        private readonly IUser user;

        private EzLocalAvatarLoader avatarLoader;
        private string avatarKey;
        private Drawable content;
        private FillMode contentFillMode = FillMode.Fit;

        /// <summary>
        /// Forwarded to the inner sprite / animation (outer composite stays stretch-fill of parent).
        /// </summary>
        public new FillMode FillMode
        {
            get => contentFillMode;
            set
            {
                contentFillMode = value;
                content?.FillMode = value;
            }
        }

        /// <summary>
        /// Local clip folder names under <c>Modify/avatars/{Username}/</c>. Empty when using a still or online avatar.
        /// </summary>
        public IReadOnlyList<string> AvailableAnimations { get; private set; } = Array.Empty<string>();

        /// <summary>
        /// A simple, non-interactable avatar for the specified user.
        /// </summary>
        /// <param name="user">The user. A null value will get a placeholder avatar.</param>
        public DrawableAvatar(IUser user = null)
        {
            this.user = user;

            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures, OnlineAssetCachingStore onlineTextures, EzResourceStore ezResourceStore, Storage storage)
        {
            avatarLoader = new EzLocalAvatarLoader(storage, ezResourceStore);

            Drawable local = tryLoadLocalAvatar();

            if (local != null)
            {
                setContent(local);
                return;
            }

            if (user != null && user.OnlineID > 1)
            {
                // TODO: The fallback here should not need to exist. Users should be looked up and populated via UserLookupCache or otherwise
                // in remaining cases where this is required (chat tabs, local leaderboard), at which point this should be removed.
                Texture online = onlineTextures.Get((user as APIUser)?.AvatarUrl ?? $@"https://a.ppy.sh/{user.OnlineID}");

                if (online != null)
                {
                    setContent(new Sprite { Texture = online });
                    return;
                }
            }

            Drawable guest = tryLoadLocalAvatarKey("guest")
                             ?? createSprite(textures.Get(@"Online/avatar-guest"));
            if (guest != null)
                setContent(guest);
        }

        /// <summary>
        /// Switch to a local clip by subfolder name and loop it. No-op if the clip is missing.
        /// </summary>
        public bool PlayAnimation(string clipName)
        {
            if (avatarLoader == null || string.IsNullOrEmpty(avatarKey) || string.IsNullOrEmpty(clipName))
                return false;

            Drawable animation = avatarLoader.CreateAnimation(avatarKey, clipName, looping: true);
            if (animation == null)
                return false;

            setContent(animation);
            return true;
        }

        private Drawable tryLoadLocalAvatar()
        {
            if (user == null || string.IsNullOrEmpty(user.Username))
                return null;

            return tryLoadLocalAvatarKey(user.Username);
        }

        private Drawable tryLoadLocalAvatarKey(string key)
        {
            AvailableAnimations = avatarLoader.ListClipNames(key);
            avatarKey = AvailableAnimations.Count > 0 ? key : null;

            Drawable animation = avatarLoader.TryCreateDefaultAnimation(key);
            if (animation != null)
                return animation;

            Texture still = avatarLoader.GetStaticTexture(key);
            return still != null ? new Sprite { Texture = still } : null;
        }

        private static Drawable createSprite(Texture texture) =>
            texture == null ? null : new Sprite { Texture = texture };

        private void setContent(Drawable drawable)
        {
            content?.Expire();
            content = drawable;

            content.RelativeSizeAxes = Axes.Both;
            content.FillMode = contentFillMode;
            content.Anchor = Anchor.Centre;
            content.Origin = Anchor.Centre;

            InternalChild = content;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            this.FadeInFromZero(300, Easing.OutQuint);
        }
    }
}
