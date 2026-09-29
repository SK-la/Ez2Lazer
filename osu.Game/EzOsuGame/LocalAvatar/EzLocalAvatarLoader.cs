// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;

namespace osu.Game.EzOsuGame.LocalAvatar
{
    /// <summary>
    /// Local avatars under <see cref="EzModifyPath.AVATARS_PATH"/>.
    /// Still: <c>{key}.png</c>. Frames either flat (<c>{clip}-0.png</c> / <c>{clip}_0.png</c>) or in a folder
    /// (<c>{clip}/000.png</c>) — both layouts are resolved by <see cref="EzResourceStore"/>'s three templates.
    /// Frames use <see cref="EzTextureUsage.AnimationSafe"/>; stills use <see cref="EzTextureUsage.Large"/>.
    /// </summary>
    public class EzLocalAvatarLoader
    {
        public const string DEFAULT_CLIP = "idle";
        public const int MAX_FRAMES = 120;
        public const double DEFAULT_FRAME_LENGTH = 1000.0 / 12.0;

        /// <summary>Resource-store relative prefix (under EzResources).</summary>
        public const string RESOURCE_PREFIX = "Modify/avatars";

        private readonly EzResourceStore resources;

        public EzLocalAvatarLoader(EzResourceStore resources)
        {
            this.resources = resources;
        }

        /// <summary>
        /// Clip names found under <c>avatars/{avatarKey}/</c>:平铺命名取帧前缀，子目录命名取目录名。
        /// </summary>
        public IReadOnlyList<string> ListClipNames(string avatarKey)
        {
            if (string.IsNullOrEmpty(avatarKey))
                return Array.Empty<string>();

            string directory = $"{RESOURCE_PREFIX}/{avatarKey}";
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string prefix in resources.ListFramePrefixes(directory))
            {
                if (prefix.Length > 0 && seen.Add(prefix))
                    names.Add(prefix);
            }

            foreach (string subdirectory in resources.ListSubdirectories(directory))
            {
                if (seen.Add(subdirectory))
                    names.Add(subdirectory);
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        public string? ResolveDefaultClip(string avatarKey)
        {
            var clips = ListClipNames(avatarKey);
            if (clips.Count == 0)
                return null;

            foreach (string clip in clips)
            {
                if (string.Equals(clip, DEFAULT_CLIP, StringComparison.OrdinalIgnoreCase))
                    return clip;
            }

            return clips[0];
        }

        /// <summary>
        /// Frames of one clip, in frame order. 只取真正的多帧：单图不算动画（静态图由
        /// <see cref="GetStaticTexture"/> 负责），故此处不回退单图。
        /// </summary>
        public Texture[] LoadClipFrames(string avatarKey, string clipName)
        {
            if (string.IsNullOrEmpty(avatarKey) || string.IsNullOrEmpty(clipName))
                return Array.Empty<Texture>();

            var request = new EzAnimationRequest
            {
                Path = $"{RESOURCE_PREFIX}/{avatarKey}/{clipName}",
                MaxFrames = MAX_FRAMES,
                Usage = EzTextureUsage.AnimationSafe,
                AllowSingleFallback = false,
            };

            return resources.GetTextureFrames(request);
        }

        public Drawable? CreateAnimation(string avatarKey, string clipName, bool looping = true, double? frameLength = null)
            => CreateDrawableFromFrames(LoadClipFrames(avatarKey, clipName), looping, frameLength);

        /// <summary>
        /// Default looping animation from <c>avatars/{avatarKey}/</c>, or <c>null</c> if no frames.
        /// </summary>
        public Drawable? TryCreateDefaultAnimation(string avatarKey)
        {
            string? clip = ResolveDefaultClip(avatarKey);
            return clip == null ? null : CreateAnimation(avatarKey, clip, looping: true);
        }

        public Texture? GetStaticTexture(string avatarKey)
        {
            if (string.IsNullOrEmpty(avatarKey))
                return null;

            return resources.Get($"{RESOURCE_PREFIX}/{avatarKey}", EzTextureUsage.Large);
        }

        public static Drawable? CreateDrawableFromFrames(Texture[] textures, bool looping = true, double? frameLength = null)
        {
            switch (textures.Length)
            {
                case 0:
                    return null;

                case 1:
                    return new Sprite { Texture = textures[0] };

                default:
                    var animation = new TextureAnimation(startAtCurrentTime: true)
                    {
                        DefaultFrameLength = frameLength ?? DEFAULT_FRAME_LENGTH,
                        Loop = looping,
                    };

                    animation.AddFrames(textures);
                    return animation;
            }
        }
    }
}
