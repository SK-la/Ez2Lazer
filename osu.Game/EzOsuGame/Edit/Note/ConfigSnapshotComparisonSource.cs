// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Edit.Note
{
    public sealed class ConfigSnapshotComparisonSource : IEzSkinEditorNoteComparisonSource
    {
        private readonly Ez2ConfigManager config;
        private readonly EzSkinEditorComparisonSnapshot snapshot;
        private readonly bool useEzNoteVariants;
        private readonly string variantId;
        private readonly RulesetInfo ruleset;

        public ConfigSnapshotComparisonSource(
            Ez2ConfigManager config,
            EzSkinEditorComparisonSnapshot snapshot,
            bool useEzNoteVariants,
            string variantId,
            RulesetInfo ruleset)
        {
            this.config = config;
            this.snapshot = snapshot;
            this.useEzNoteVariants = useEzNoteVariants;
            this.variantId = variantId;
            this.ruleset = ruleset;
        }

        public RulesetInfo? Ruleset => ruleset;

        public EzSkinEditorNotePreviewRequest GetLiveRequest(EzSkinEditorNoteCompareKind compareKind) =>
            EzSkinEditorNotePreviewRequestFactory.FromConfig(config, useEzNoteVariants, compareKind, variantId, ruleset);

        public EzSkinEditorNotePreviewRequest GetSnapshotRequest(EzSkinEditorNoteCompareKind compareKind) =>
            EzSkinEditorNotePreviewRequestFactory.FromDocument(snapshot.Document, config, useEzNoteVariants, compareKind, variantId, ruleset);
    }
}
