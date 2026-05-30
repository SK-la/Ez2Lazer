// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using osu.Game.Beatmaps.ExternalLibraries;
using Realms;

namespace osu.Game.Beatmaps
{
    public partial class BeatmapSetInfo
    {
        /// <summary>
        /// Absolute path to on-disk content for <see cref="BeatmapSetHostingKind.External"/> sets.
        /// </summary>
        public string ExternalContentRoot { get; set; } = string.Empty;

        [MapTo(nameof(HostingKind))]
        public int HostingKindInt { get; set; } = (int)BeatmapSetHostingKind.Internal;

        public BeatmapSetHostingKind HostingKind
        {
            get => (BeatmapSetHostingKind)HostingKindInt;
            set => HostingKindInt = (int)value;
        }

        public bool IsExternallyHosted => HostingKind == BeatmapSetHostingKind.External;

        /// <summary>
        /// Resolves the content root used for resource loading, preferring the persisted field.
        /// </summary>
        public string? GetEffectiveExternalContentRoot()
        {
            if (!string.IsNullOrWhiteSpace(ExternalContentRoot) && Directory.Exists(ExternalContentRoot))
                return Path.GetFullPath(ExternalContentRoot);

            if (ExternalBeatmapPathEncoding.TryDecode(Hash, out string decoded) && Directory.Exists(decoded))
                return decoded;

            return null;
        }
    }
}
