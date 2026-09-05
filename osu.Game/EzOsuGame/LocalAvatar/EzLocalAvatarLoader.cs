// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame.LocalAvatar
{
    /// <summary>
    /// Local avatars under <see cref="EzModifyPath.AVATARS_PATH"/>.
    /// Still: <c>{key}.png</c>. Animation frames in folder <c>{key}/</c> as <c>name-0.png</c> / <c>name_0.png</c>.
    /// Frames use <see cref="EzTextureUsage.AnimationSafe"/>; stills use <see cref="EzTextureUsage.Large"/>.
    /// </summary>
    public class EzLocalAvatarLoader
    {
        public const string DEFAULT_CLIP = "idle";
        public const int MAX_FRAMES = 120;
        public const double DEFAULT_FRAME_LENGTH = 1000.0 / 12.0;

        /// <summary>Resource-store relative prefix (under EzResources).</summary>
        public const string RESOURCE_PREFIX = "Modify/avatars";

        /// <summary>
        /// <c>prefix-0.png</c> / <c>prefix_0.png</c> (prefix = animation name).
        /// </summary>
        private static readonly Regex frame_regex = new Regex(
            @"^(?<prefix>.+?)[-_](?<index>\d+)\.(png|jpg|jpeg)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly Storage avatarsStorage;
        private readonly EzResourceStore resources;

        public EzLocalAvatarLoader(Storage gameStorage, EzResourceStore resources)
        {
            avatarsStorage = gameStorage.GetStorageForDirectory(EzModifyPath.AVATARS_PATH);
            this.resources = resources;
        }

        /// <summary>
        /// Animation prefixes found as files under <c>avatars/{avatarKey}/</c>.
        /// </summary>
        public IReadOnlyList<string> ListClipNames(string avatarKey)
        {
            var grouped = groupFramesByPrefix(avatarKey);
            if (grouped.Count == 0)
                return Array.Empty<string>();

            var names = new List<string>(grouped.Keys);
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

        public Texture[] LoadClipFrames(string avatarKey, string clipName)
        {
            if (string.IsNullOrEmpty(avatarKey) || string.IsNullOrEmpty(clipName))
                return Array.Empty<Texture>();

            if (!groupFramesByPrefix(avatarKey).TryGetValue(clipName, out var frames) &&
                !tryGetFramesIgnoreCase(avatarKey, clipName, out frames))
                return Array.Empty<Texture>();

            var textures = new List<Texture>(frames.Count);

            foreach (string frameName in frames)
            {
                Texture? texture = resources.Get($"{RESOURCE_PREFIX}/{avatarKey}/{frameName}", EzTextureUsage.AnimationSafe);
                if (texture != null)
                    textures.Add(texture);
            }

            return textures.Count > 0 ? textures.ToArray() : Array.Empty<Texture>();
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

                    foreach (Texture texture in textures)
                        animation.AddFrame(texture);

                    return animation;
            }
        }

        private bool tryGetFramesIgnoreCase(string avatarKey, string clipName, out List<string> frames)
        {
            frames = new List<string>();

            foreach ((string prefix, List<string> list) in groupFramesByPrefix(avatarKey))
            {
                if (!string.Equals(prefix, clipName, StringComparison.OrdinalIgnoreCase))
                    continue;

                frames = list;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Maps animation prefix → frame names without extension, sorted by index.
        /// </summary>
        private Dictionary<string, List<string>> groupFramesByPrefix(string avatarKey)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(avatarKey) || !avatarsStorage.ExistsDirectory(avatarKey))
                return result;

            IEnumerable<string> files;

            try
            {
                files = avatarsStorage.GetFiles(avatarKey);
            }
            catch
            {
                return result;
            }

            // prefix → (index → nameWithoutExt)
            var buckets = new Dictionary<string, SortedDictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                if (string.IsNullOrEmpty(name))
                    continue;

                var match = frame_regex.Match(name);
                if (!match.Success)
                    continue;

                string prefix = match.Groups["prefix"].Value;
                if (string.IsNullOrEmpty(prefix))
                    continue;

                if (!int.TryParse(match.Groups["index"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int index))
                    continue;

                if (!buckets.TryGetValue(prefix, out var byIndex))
                    buckets[prefix] = byIndex = new SortedDictionary<int, string>();

                string withoutExtension = Path.GetFileNameWithoutExtension(name);
                if (string.IsNullOrEmpty(withoutExtension))
                    continue;

                if (!byIndex.TryGetValue(index, out string? existing) || string.CompareOrdinal(withoutExtension, existing) < 0)
                    byIndex[index] = withoutExtension;
            }

            foreach ((string prefix, SortedDictionary<int, string> byIndex) in buckets)
            {
                var list = new List<string>(Math.Min(byIndex.Count, MAX_FRAMES));

                foreach ((_, string frame) in byIndex)
                {
                    list.Add(frame);
                    if (list.Count >= MAX_FRAMES)
                        break;
                }

                if (list.Count > 0)
                    result[prefix] = list;
            }

            return result;
        }
    }
}
