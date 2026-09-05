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
    /// <summary>
    /// Non-interactive avatar. Local still/animation under <c>Modify/avatars</c>, else online / guest.
    /// </summary>
    [LongRunningLoad]
    public partial class DrawableAvatar : CompositeDrawable
    {
        private readonly IUser user;

        private readonly Sprite stillSprite;

        private EzLocalAvatarLoader avatarLoader;
        private string avatarKey;
        private Drawable activeContent;
        private FillMode contentFillMode = FillMode.Fit;

        /// <summary>
        /// Applied to the displayed sprite / animation (keeps outer composite filling the parent slot).
        /// </summary>
        public new FillMode FillMode
        {
            get => contentFillMode;
            set
            {
                contentFillMode = value;
                applyFillMode();
            }
        }

        /// <summary>
        /// Animation prefixes under <c>Modify/avatars/{Username}/</c> (<c>name-0.png</c> / <c>name_0.png</c>).
        /// </summary>
        public IReadOnlyList<string> AvailableAnimations { get; private set; } = Array.Empty<string>();

        /// <param name="user">The user. A null value will get a placeholder avatar.</param>
        public DrawableAvatar(IUser user = null)
        {
            this.user = user;

            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            // Same layout pattern as DrawableTeamFlag: sprite exists from construction so parent FillMode works.
            InternalChild = stillSprite = new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                FillMode = FillMode.Fit,
            };

            activeContent = stillSprite;
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures, OnlineAssetCachingStore onlineTextures, EzResourceStore ezResourceStore, Storage storage)
        {
            avatarLoader = new EzLocalAvatarLoader(storage, ezResourceStore);

            if (tryShowLocalAvatar())
                return;

            if (user != null && user.OnlineID > 1)
            {
                // TODO: The fallback here should not need to exist. Users should be looked up and populated via UserLookupCache or otherwise
                // in remaining cases where this is required (chat tabs, local leaderboard), at which point this should be removed.
                Texture online = onlineTextures.Get((user as APIUser)?.AvatarUrl ?? $@"https://a.ppy.sh/{user.OnlineID}");

                if (online != null)
                {
                    showStill(online);
                    return;
                }
            }

            if (tryShowLocalAvatarKey("guest"))
                return;

            Texture guest = textures.Get(@"Online/avatar-guest");
            if (guest != null)
                showStill(guest);
        }

        /// <summary>
        /// Switch to a local animation by file-name prefix (<c>name</c> in <c>name-0.png</c>). No-op if missing.
        /// </summary>
        public bool PlayAnimation(string clipName)
        {
            if (avatarLoader == null || string.IsNullOrEmpty(avatarKey) || string.IsNullOrEmpty(clipName))
                return false;

            Drawable animation = avatarLoader.CreateAnimation(avatarKey, clipName, looping: true);
            if (animation == null)
                return false;

            showDrawable(animation);
            return true;
        }

        private bool tryShowLocalAvatar()
        {
            if (user == null || string.IsNullOrEmpty(user.Username))
                return false;

            return tryShowLocalAvatarKey(user.Username);
        }

        private bool tryShowLocalAvatarKey(string key)
        {
            AvailableAnimations = avatarLoader.ListClipNames(key);
            avatarKey = AvailableAnimations.Count > 0 ? key : null;

            Drawable animation = avatarLoader.TryCreateDefaultAnimation(key);

            if (animation != null)
            {
                showDrawable(animation);
                return true;
            }

            Texture still = avatarLoader.GetStaticTexture(key);
            if (still == null)
                return false;

            showStill(still);
            return true;
        }

        private void showStill(Texture texture)
        {
            // Prefer the ctor sprite so FillMode / layout stay identical to a plain Sprite avatar.
            if (activeContent != stillSprite)
            {
                ClearInternal();
                InternalChild = stillSprite;
                activeContent = stillSprite;
            }

            stillSprite.Texture = texture;
            applyFillMode();
        }

        private void showDrawable(Drawable drawable)
        {
            if (ReferenceEquals(drawable, stillSprite))
            {
                showStill(stillSprite.Texture);
                return;
            }

            // Single-frame "animation" from loader is a Sprite — copy onto stillSprite.
            if (drawable is Sprite sprite && sprite.Texture != null)
            {
                showStill(sprite.Texture);
                return;
            }

            ClearInternal();
            activeContent = drawable;
            activeContent.RelativeSizeAxes = Axes.Both;
            activeContent.Anchor = Anchor.Centre;
            activeContent.Origin = Anchor.Centre;
            applyFillMode();
            InternalChild = activeContent;
        }

        private void applyFillMode()
        {
            stillSprite.FillMode = contentFillMode;
            if (activeContent != null && !ReferenceEquals(activeContent, stillSprite))
                activeContent.FillMode = contentFillMode;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            this.FadeInFromZero(300, Easing.OutQuint);
        }
    }
}
