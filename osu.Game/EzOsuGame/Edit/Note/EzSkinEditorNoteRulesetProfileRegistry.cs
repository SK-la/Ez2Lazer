// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Edit.Note
{
    public static class EzSkinEditorNoteRulesetProfileRegistry
    {
        private static readonly List<IEzSkinEditorNoteRulesetProfile> profiles = new List<IEzSkinEditorNoteRulesetProfile>();

        public static IReadOnlyList<IEzSkinEditorNoteRulesetProfile> All => profiles;

        public static void Register(IEzSkinEditorNoteRulesetProfile profile)
        {
            profiles.RemoveAll(p => p.RulesetOnlineId == profile.RulesetOnlineId);
            profiles.Add(profile);
        }

        public static IEzSkinEditorNoteRulesetProfile? Get(RulesetInfo? ruleset) =>
            ruleset == null ? null : profiles.FirstOrDefault(p => p.RulesetOnlineId == ruleset.OnlineID);

        public static IEzSkinEditorNoteRulesetProfile? Get(int rulesetOnlineId) =>
            profiles.FirstOrDefault(p => p.RulesetOnlineId == rulesetOnlineId);
    }
}
