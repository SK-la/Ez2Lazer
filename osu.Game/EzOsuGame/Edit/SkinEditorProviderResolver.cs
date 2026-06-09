// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;

namespace osu.Game.EzOsuGame.Edit
{
    public static class SkinEditorProviderResolver
    {
        private const int mania_ruleset_online_id = 3;

        public static ISkinEditorVirtualProvider? Resolve(IBeatmap? beatmap)
        {
            int rulesetId = beatmap?.BeatmapInfo.Ruleset.OnlineID ?? 0;

            if (rulesetId != 0)
            {
                var fromRegistry = SkinEditorProviderRegistry.Get(rulesetId);
                if (fromRegistry != null)
                    return fromRegistry;
            }

            // Default to mania for editor previews when no beatmap ruleset is available.
            return SkinEditorProviderRegistry.Get(mania_ruleset_online_id);
        }
    }
}
