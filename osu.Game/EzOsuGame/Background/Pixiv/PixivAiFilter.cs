// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    /// <summary>
    /// Pixiv illust_ai_type: 0 = unknown (treat as non-AI unless tags say otherwise),
    /// 1 = human-created, 2 = AI-generated.
    /// Tag fallback catches mis-labelled works (exact tag match only).
    /// </summary>
    internal static class PixivAiFilter
    {
        public static bool IsAiGenerated(PixivIllustInfo illust)
        {
            if (illust.IllustAiType == PixivConstants.ILLUST_AI_TYPE_AI)
                return true;

            return hasAiGeneratedTag(illust.Tags);
        }

        public static bool IsTagIndicatedAi(string[] tags) => hasAiGeneratedTag(tags);

        private static bool hasAiGeneratedTag(string[] tags)
        {
            foreach (string tag in tags)
            {
                foreach (string marker in PixivConstants.AI_GENERATED_TAGS)
                {
                    if (string.Equals(tag, marker, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
    }
}
