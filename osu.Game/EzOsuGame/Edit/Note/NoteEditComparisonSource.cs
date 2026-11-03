// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Edit.Note
{
    public sealed class NoteEditComparisonSource : IEzSkinEditorNoteComparisonSource
    {
        private readonly EzSkinEditorNoteEditSession session;
        private readonly EzSkinEditorNoteEditSnapshot snapshot;
        private readonly bool useEzNoteVariants;

        public NoteEditComparisonSource(
            EzSkinEditorNoteEditSession session,
            EzSkinEditorNoteEditSnapshot snapshot,
            bool useEzNoteVariants)
        {
            this.session = session;
            this.snapshot = snapshot;
            this.useEzNoteVariants = useEzNoteVariants;
        }

        public RulesetInfo? Ruleset => session.Ruleset.Value;

        public EzSkinEditorNotePreviewRequest GetLiveRequest(EzSkinEditorNoteCompareKind compareKind) =>
            session.ToPreviewRequest(useEzNoteVariants, compareKind);

        public EzSkinEditorNotePreviewRequest GetSnapshotRequest(EzSkinEditorNoteCompareKind compareKind) =>
            snapshot.ToPreviewRequest(useEzNoteVariants, compareKind);
    }
}
