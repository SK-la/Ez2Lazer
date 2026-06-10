// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Edit
{
    public static class EzSkinEditorPreviewModes
    {
        private const int mania_ruleset_online_id = 3;

        private static readonly EzBeatmapPreviewMode[] shared_modes =
        {
            EzBeatmapPreviewMode.Dynamic,
            EzBeatmapPreviewMode.Static,
        };

        private static readonly EzBeatmapPreviewMode[] mania_modes =
        {
            EzBeatmapPreviewMode.Dynamic,
            EzBeatmapPreviewMode.Static,
            EzBeatmapPreviewMode.StaticFullMap,
            EzBeatmapPreviewMode.StaticScroll,
        };

        public static bool SupportsBeatmapPreview(RulesetInfo ruleset) => ruleset.OnlineID is >= 0 and <= mania_ruleset_online_id;

        public static IReadOnlyList<EzBeatmapPreviewMode> GetAvailableModes(RulesetInfo ruleset) =>
            IsManiaRuleset(ruleset) ? mania_modes : shared_modes;

        public static EzBeatmapPreviewMode GetDefaultMode(RulesetInfo ruleset) =>
            IsManiaRuleset(ruleset) ? EzBeatmapPreviewMode.StaticFullMap : EzBeatmapPreviewMode.Static;

        public static EzBeatmapPreviewMode ValidateMode(EzBeatmapPreviewMode mode, RulesetInfo ruleset)
        {
            var available = GetAvailableModes(ruleset);

            foreach (var candidate in available)
            {
                if (candidate == mode)
                    return mode;
            }

            return GetDefaultMode(ruleset);
        }

        public static bool IsManiaRuleset(RulesetInfo? ruleset) => ruleset?.OnlineID == mania_ruleset_online_id;
    }
}
