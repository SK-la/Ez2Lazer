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
    /// Loads local avatar stills / clip folders under <see cref="EzModifyPath.AVATARS_PATH"/>.
    /// Clip frames always use <see cref="EzTextureUsage.AnimationSafe"/>.
    /// </summary>
    public class EzLocalAvatarLoader
    {
        public const string DEFAULT_CLIP = "idle";
        public const int MAX_FRAMES = 120;
        public const double DEFAULT_FRAME_LENGTH = 1000.0 / 12.0;

        /// <summary>Resource-store relative prefix (under EzResources), e.g. <c>Modify/avatars</c>.</summary>
        public const string RESOURCE_PREFIX = "Modify/avatars";

        private static readonly Regex pure_index = new Regex(@"^(\d+)\.(png|jpg|jpeg)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex suffix_index = new Regex(@"_(\d+)\.(png|jpg|jpeg)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly Storage avatarsStorage;
        private readonly EzResourceStore resources;

        public EzLocalAvatarLoader(Storage gameStorage, EzResourceStore resources)
        {
            avatarsStorage = gameStorage.GetStorageForDirectory(EzModifyPath.AVATARS_PATH);
            this.resources = resources;
        }

        /// <summary>
        /// Subfolder names under <paramref name="avatarKey"/> that contain at least one indexed frame file.
        /// </summary>
        public IReadOnlyList<string> ListClipNames(string avatarKey)
        {
            if (string.IsNullOrEmpty(avatarKey) || !avatarsStorage.ExistsDirectory(avatarKey))
                return Array.Empty<string>();

            var names = new List<string>();

            try
            {
                foreach (string dir in avatarsStorage.GetDirectories(avatarKey))
                {
                    string clip = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (string.IsNullOrEmpty(clip))
                        continue;

                    if (listFrameFileNames(avatarKey, clip).Count > 0)
                        names.Add(clip);
                }
            }
            catch
            {
                return Array.Empty<string>();
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

        public Texture[] LoadClipFrames(string avatarKey, string clipName)
        {
            var frameNames = listFrameFileNames(avatarKey, clipName);
            if (frameNames.Count == 0)
                return Array.Empty<Texture>();

            var textures = new List<Texture>(frameNames.Count);

            foreach (string frameName in frameNames)
            {
                string path = $"{RESOURCE_PREFIX}/{avatarKey}/{clipName}/{frameName}";
                Texture? texture = resources.Get(path, EzTextureUsage.AnimationSafe);
                if (texture != null)
                    textures.Add(texture);
            }

            return textures.Count > 0 ? textures.ToArray() : Array.Empty<Texture>();
        }

        private IReadOnlyList<string> listFrameFileNames(string avatarKey, string clipName)
        {
            if (string.IsNullOrEmpty(avatarKey) || string.IsNullOrEmpty(clipName))
                return Array.Empty<string>();

            string directory = Path.Combine(avatarKey, clipName);

            try
            {
                if (!avatarsStorage.ExistsDirectory(directory))
                    return Array.Empty<string>();

                return CollectIndexedFrameNames(avatarsStorage.GetFiles(directory));
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public Drawable? CreateAnimation(string avatarKey, string clipName, bool looping = true, double? frameLength = null)
        {
            Texture[] textures = LoadClipFrames(avatarKey, clipName);
            return CreateDrawableFromFrames(textures, looping, frameLength);
        }

        /// <summary>
        /// Default looping clip animation, or <c>null</c> when no clip folders exist.
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

        /// <summary>
        /// Pure numeric <c>000.png</c> or pet-style <c>name_000.png</c>; returns names without extension, sorted by index.
        /// </summary>
        public static IReadOnlyList<string> CollectIndexedFrameNames(IEnumerable<string> fileNames)
        {
            var byIndex = new SortedDictionary<int, string>();

            foreach (string fileName in fileNames)
            {
                string name = Path.GetFileName(fileName);
                if (string.IsNullOrEmpty(name))
                    continue;

                if (!tryGetFrameIndex(name, out int index))
                    continue;

                string withoutExtension = Path.GetFileNameWithoutExtension(name);
                if (string.IsNullOrEmpty(withoutExtension))
                    continue;

                if (!byIndex.TryGetValue(index, out string? existing) || string.CompareOrdinal(withoutExtension, existing) < 0)
                    byIndex[index] = withoutExtension;
            }

            var names = new List<string>(Math.Min(byIndex.Count, MAX_FRAMES));

            foreach ((_, string frame) in byIndex)
            {
                names.Add(frame);
                if (names.Count >= MAX_FRAMES)
                    break;
            }

            return names;
        }

        private static bool tryGetFrameIndex(string fileName, out int index)
        {
            index = -1;

            var pure = pure_index.Match(fileName);
            if (pure.Success)
                return int.TryParse(pure.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out index);

            var suffix = suffix_index.Match(fileName);
            if (suffix.Success)
                return int.TryParse(suffix.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out index);

            return false;
        }
    }
}
