// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;

namespace osu.Game.EzOsuGame.Pets
{
    /// <summary>
    /// Whole-token (case-insensitive) match of beatmap Artist / Tags against <c>live2d.metadataTriggers</c>.
    /// </summary>
    public static class EzPetMetadataTriggerMatcher
    {
        public const string ACTION_LIP_SYNC = "lipSync";

        /// <summary>
        /// Whether the metadata gate for <paramref name="action"/> passes.
        /// No triggers for that action → <c>true</c> (feature uses its own enabled flag only).
        /// Triggers present → <c>true</c> only when any word fully matches Artist/Tags/UserTags.
        /// </summary>
        public static bool PassesGate(
            IReadOnlyList<EzPetMetadataTriggerDefinition>? triggers,
            string action,
            string? artist,
            string? artistUnicode,
            string? tags,
            IEnumerable<string>? userTags)
        {
            if (string.IsNullOrWhiteSpace(action))
                return true;

            if (triggers == null || triggers.Count == 0)
                return true;

            bool hasActionTrigger = false;

            foreach (var trigger in triggers)
            {
                if (!string.Equals(trigger.Action, action, StringComparison.OrdinalIgnoreCase))
                    continue;

                hasActionTrigger = true;

                if (trigger.Words.Count == 0)
                    continue;

                if (matchesAnyWord(trigger.Words, artist, artistUnicode, tags, userTags))
                    return true;
            }

            // No trigger targets this action → ungated.
            return !hasActionTrigger;
        }

        public static bool PassesGate(
            IReadOnlyList<EzPetMetadataTriggerDefinition>? triggers,
            string action,
            IBeatmapMetadataInfo? metadata,
            IEnumerable<string>? userTags = null)
        {
            if (metadata == null)
                return PassesGate(triggers, action, null, null, null, userTags);

            IEnumerable<string>? resolvedUserTags = userTags;

            if (resolvedUserTags == null && metadata is BeatmapMetadata realmMeta)
                resolvedUserTags = realmMeta.UserTags;

            return PassesGate(
                triggers,
                action,
                metadata.Artist,
                metadata.ArtistUnicode,
                metadata.Tags,
                resolvedUserTags);
        }

        private static bool matchesAnyWord(
            IReadOnlyList<string> words,
            string? artist,
            string? artistUnicode,
            string? tags,
            IEnumerable<string>? userTags)
        {
            foreach (string raw in words)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                string word = raw.Trim();

                if (tokenEqualsAny(artist, word)
                    || tokenEqualsAny(artistUnicode, word)
                    || tokenEqualsAny(tags, word)
                    || userTagEquals(userTags, word))
                    return true;
            }

            return false;
        }

        private static bool tokenEqualsAny(string? field, string word)
        {
            if (string.IsNullOrWhiteSpace(field))
                return false;

            foreach (string token in field.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (string.Equals(token, word, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool userTagEquals(IEnumerable<string>? userTags, string word)
        {
            if (userTags == null)
                return false;

            foreach (string tag in userTags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                    continue;

                if (string.Equals(tag.Trim(), word, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
