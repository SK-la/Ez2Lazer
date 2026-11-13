// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Models;
using osu.Game.Online.API;
using osu.Game.Rulesets;

namespace osu.Game.Beatmaps
{
    public static class BeatmapSetInfoExtensions
    {
        /// <summary>
        /// Returns the storage path for the file in this beatmapset with the given filename, if any exists, otherwise null.
        /// The path returned is relative to the user file storage.
        /// The lookup is case insensitive.
        /// </summary>
        /// <param name="model">The model to operate on.</param>
        /// <param name="filename">The name of the file to get the storage path of.</param>
        public static string? GetPathForFile(this IHasRealmFiles model, string filename) => model.GetFile(filename)?.File.GetStoragePath();

        /// <summary>
        /// Returns all file usages in this model with the given filename.
        /// The lookup is case insensitive.
        /// </summary>
        /// <param name="model">The model to operate on.</param>
        /// <param name="filename">The name of the file to match.</param>
        public static IEnumerable<RealmNamedFileUsage> GetFiles(this IHasRealmFiles model, string filename) =>
            model.Files.Where(f => string.Equals(f.Filename, filename, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Returns the file usage for the file in this beatmapset with the given filename, if any exists, otherwise null.
        /// If multiple entries match, the first is returned.
        /// The lookup is case insensitive.
        /// </summary>
        /// <param name="model">The model to operate on.</param>
        /// <param name="filename">The name of the file to get the storage path of.</param>
        public static RealmNamedFileUsage? GetFile(this IHasRealmFiles model, string filename) =>
            model.GetFiles(filename).FirstOrDefault();

        /// <summary>
        /// Get the beatmapset info page URL, or <c>null</c> if unavailable.
        /// </summary>
        public static string? GetOnlineURL(this IBeatmapSetInfo beatmapSetInfo, IAPIProvider api, IRulesetInfo? ruleset = null)
        {
            if (beatmapSetInfo.OnlineID <= 0)
                return null;

            if (ruleset != null)
                return $@"{api.Endpoints.WebsiteUrl}/beatmapsets/{beatmapSetInfo.OnlineID}#{ruleset.ShortName}";

            return $@"{api.Endpoints.WebsiteUrl}/beatmapsets/{beatmapSetInfo.OnlineID}";
        }
    }
}
